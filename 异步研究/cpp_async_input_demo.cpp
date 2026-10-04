/*
 * cpp_async_input_demo.cpp — 异步输入系统“引擎侧”管线的 C++ 最小复刻
 *
 * 对应游戏里的链路：
 *   scrConductor.Update          -> 本文件的 MainLoop
 *   scrController.UpdateInput     -> AsyncInputSystem::UpdateInput
 *   scrController.ProcessKeyInputs-> AsyncInputSystem::ProcessBatch
 *   scrPlayer.Simulated_PlayerControl_Update -> Game::SimulatePlayerUpdate
 *   AsyncInputUtils.UpdateOffsetTime        -> Clocks::UpdateOffsetTime (EMA)
 *   AsyncInputUtils.GetAngle / scrPlanet.AsyncRefreshAngles -> Game::AngleAt
 *
 * 编译（无第三方依赖，跨平台）：
 *   g++ -std=c++17 -O2 cpp_async_input_demo.cpp -o demo
 *   cl /EHsc /std:c++17 /O2 cpp_async_input_demo.cpp
 *
 * 真实接入时：把“钩子线程”换成 skyhook_native/skyhook.c 的 DLL（或直接
 * 用 WH_KEYBOARD_LL 内嵌），事件来源从 SimulateEvents 改为回调即可，
 * 其余管线代码原样可用。
 */
#include <cstdint>
#include <cmath>
#include <cstdio>
#include <queue>
#include <vector>
#include <thread>
#include <mutex>
#include <atomic>
#include <chrono>
#include <random>
#include <condition_variable>

/* ============ 1. 事件与线程安全队列 ============ */

struct KeyEvent {
    int64_t ticks;    // 100ns 单位，挂钟时间戳（与 C# DateTime.Ticks 同坐标系）
    bool    pressed;  // true=按下 false=抬起
    uint16_t key;     // 物理键码（扫描码）
};

struct MinByTicks {
    bool operator()(const KeyEvent& a, const KeyEvent& b) const {
        return a.ticks > b.ticks;   // priority_queue 是大顶堆，反转比较器成小顶堆
    }
};

/* 钩子线程 -> 主线程 的桥。真实实现里就是托管层的 ConcurrentQueue。 */
class EventQueue {
    std::mutex mtx_;
    std::priority_queue<KeyEvent, std::vector<KeyEvent>, MinByTicks> q_;
public:
    void push(KeyEvent e) {
        std::lock_guard<std::mutex> lk(mtx_);
        q_.push(e);
    }
    /* 取出并清空（调用方再自行按 tick 分批） */
    std::vector<KeyEvent> drain() {
        std::lock_guard<std::mutex> lk(mtx_);
        std::vector<KeyEvent> out;
        while (!q_.empty()) { out.push_back(q_.top()); q_.pop(); }
        return out;
    }
};

/* ============ 2. 双时钟：挂钟 vs 音频时钟 ============ */

struct Clocks {
    static int64_t WallTicks() {   // 100ns 单位
        using namespace std::chrono;
        return duration_cast<nanoseconds>(
                   steady_clock::now().time_since_epoch()).count() / 100;
    }
    /* 音频时钟（秒）：真实实现来自音频后端（Unity 是 AudioSettings.dspTime /
     * 你自己的引擎就是音频流的采样位置）。这里模拟为匀速时钟：
     * 从“开始播放”的挂钟时刻推导，与真实时间严格同步。 */
    double  audioNowSec = 0.0;
    int64_t audioStartTicks = 0;
    void StartAudio()  { audioStartTicks = WallTicks(); audioNowSec = 0.0; }
    void UpdateAudioClock() { audioNowSec = (double)(WallTicks() - audioStartTicks) / 1e7; }

    /* 对应 AsyncInputUtils.UpdateOffsetTime：
     *   offsetTick = currFrameTick - dspTime*1e7
     *   fixDivider=1 时立即对齐；否则每帧只收敛 1/fixDivider，防跳变。 */
    int64_t offsetTick = 0;         // 挂钟tick - 音频tick
    bool    offsetUpdated = false;

    void UpdateOffsetTime(long fixDivider = 100) {
        int64_t now  = WallTicks() - (int64_t)(audioNowSec * 1e7);
        int64_t diff = now - offsetTick;
        bool changed = true;
        if (fixDivider == 1) offsetTick = now;
        else if (diff != 0)  offsetTick += diff / fixDivider;
        else                 changed = false;
        if (changed) offsetUpdated = true;
    }

    /* 事件 tick -> 歌曲时间（秒）。对应 AsyncInputUtils.GetSongPosition。 */
    double SongPosAt(int64_t eventTick) const {
        return (double)(eventTick - offsetTick) / 1e7;
    }
};

/* ============ 3. 游戏判定（对应 scrPlanet/scrPlayer 的角度判定） ============ */

struct Game {
    static constexpr double PI = 3.14159265358979323846;
    double lastHit   = 0.0;      // 上一次命中的歌曲时间（秒）
    double snapped   = 0.0;      // 上次命中时的角度
    int    beatIndex = 1;        // 下一个要打的拍（第 0 拍是关卡起点 t=0，事件从第 1 拍开始）
    const double crotchet = 0.5; // 每拍时长（秒），BPM=120
    const double speed    = 1.0; // 行星转速
    const double dir      = 1.0; // 顺/逆时针
    const double window   = 0.12;// 判定窗口（秒换算成角度窗口见下）

    /* 对应 scrPlanet.AsyncRefreshAngles / AsyncInputUtils.GetAngle：
     *   angle = snapped + (songPos(tick) - lastHit) / crotchet * π * speed * dir */
    double AngleAt(int64_t tick, const Clocks& clk) const {
        double t = clk.SongPosAt(tick);
        return snapped + (t - lastHit) / crotchet * PI * speed * dir;
    }

    /* 对应 scrPlayer.Simulated_PlayerControl_Update(tick)：
     * 以“事件发生时刻”的角度做判定，而不是以“当前帧”的角度。 */
    void SimulatePlayerUpdate(int64_t tick, const Clocks& clk) {
        double angle  = AngleAt(tick, clk);
        double target = beatIndex * PI * speed * dir;   // 每拍前进 π
        double diff   = angle - target;
        /* 角度窗口 = 时间窗口 * 角速度 */
        if (std::fabs(diff) < window / crotchet * PI * speed) {
            double t = clk.SongPosAt(tick);
            /* 下一拍的理想时刻 = 上一次命中 + 一拍（对应 ADOFAI 中
             * floor.entryTime 逐格推进的模型） */
            double early = t - (lastHit + crotchet);
            std::printf("  [HIT ] beat %d, 事件偏离理想点 %+.2f ms\n",
                        beatIndex, early * 1000.0);
            snapped   = target;      // 吸附
            lastHit   = t;           // 对应 player.lastHit = floor.entryTime
            beatIndex++;
        }
    }
};

/* ============ 4. 异步输入系统（对应 AsyncInputManager + scrController.UpdateInput） ============ */

class AsyncInputSystem {
public:
    EventQueue queue;
    Clocks     clk;
    Game       game;
    std::atomic<bool> running{true};
    std::atomic<int64_t> frameTick{0};   // 对应 AsyncInputManager.currFrameTick

    /* 钩子线程：真实实现见 skyhook_native/skyhook.c。这里按拍点生成按键事件，
     * 并故意让事件时刻与渲染帧错开，以演示“异步”的意义。 */
    std::thread SpawnHookThread() {
        return std::thread([this] {
            std::mt19937 rng(12345);
            std::uniform_int_distribution<int> jitterMs(-30, 30); // ±30ms 抖动
            auto t0 = std::chrono::steady_clock::now();
            for (int beat = 0; beat < 8 && running; ++beat) {
                // 目标时刻 = 拍点 + 抖动
                std::this_thread::sleep_until(
                    t0 + std::chrono::milliseconds(500 * beat + jitterMs(rng)));
                int64_t tick = Clocks::WallTicks();
                queue.push({tick, true, 0x39});                 // 按下
                queue.push({tick + 50'000, false, 0x39});       // 5ms 后抬起
            }
        });
    }

    /* 对应 scrController.UpdateInput：
     *   1) 清 Down/Up 掩码  2) 队列 -> 按 tick 排序
     *   3) 同 tick 事件打包  4) 每批触发一次玩家模拟 */
    void UpdateInput() {
        std::vector<KeyEvent> events = queue.drain();   // 已按 tick 升序
        int64_t batchTick = 0;
        bool    batchHasDown = false;
        for (size_t i = 0; i <= events.size(); ++i) {
            bool newBatch = (i == events.size()) ||
                            (i > 0 && events[i].ticks != events[i-1].ticks);
            if (newBatch && batchTick != 0) {
                ProcessBatch(batchTick, batchHasDown);
                batchHasDown = false;
                batchTick = 0;
            }
            if (i < events.size()) {
                if (batchTick == 0) batchTick = events[i].ticks;
                if (events[i].pressed) batchHasDown = true;
                // 真实实现在这里维护 keyMask / keyDownMask / keyUpMask
            }
        }
    }

    /* 对应 scrController.ProcessKeyInputs */
    void ProcessBatch(int64_t eventTick, bool anyDown) {
        if (paused_) return;
        // 真实实现：若本批有按下事件 -> frameOnAsyncInputDetected = 当前帧号
        (void)anyDown;
        int64_t tick = (eventTick != 0) ? eventTick : frameTick.load();
        if (clk.offsetUpdated)
            game.SimulatePlayerUpdate(tick, clk);   // 对应 Simulated_PlayerControl_Update
        lastReportedTick_ = tick;
    }

    void MainLoop(double fps, double seconds) {
        using namespace std::chrono;
        auto step = duration_cast<steady_clock::duration>(duration<double>(1.0 / fps));
        auto next = steady_clock::now();
        auto end  = next + duration_cast<steady_clock::duration>(duration<double>(seconds));
        while (running && steady_clock::now() < end) {
            next += step;
            frameTick.store(Clocks::WallTicks());          // 帧边界 tick
            clk.UpdateAudioClock();                        // 音频时钟按真实时间推进
            clk.UpdateOffsetTime(100);                     // 对应 UpdateOffsetTime(100)
            UpdateInput();                                 // 处理所有积压事件
            std::this_thread::sleep_until(next);
        }
    }

    void SetPaused(bool p) {
        paused_ = p;
        if (p)      clk.offsetUpdated = false;   // 对应 TogglePauseGame
        else        clk.UpdateOffsetTime(1);     // 恢复时立即重对齐
    }

private:
    bool paused_ = false;
    int64_t lastReportedTick_ = 0;
};

/* ============ 5. 演示：对比“帧采样输入” vs “异步输入” ============ */

int main() {
    std::printf("=== 异步输入管线演示 ===\n");
    AsyncInputSystem sys;
    std::thread hook = sys.SpawnHookThread();

    /* 预热 0.3s：让 offsetTick 收敛（对应游戏里前 100 帧的 EMA），
     * 随后在 8 拍内以 60fps 跑完整管线。 */
    sys.running = true;
    sys.clk.StartAudio();          // 模拟音乐开始播放（音频时钟起点）
    sys.clk.UpdateOffsetTime(1);   // 对应进入 PlayerControl 状态时的瞬间对齐：
                                   // 真实游戏里 EMA(÷100) 只负责追踪后续慢漂移
    sys.MainLoop(60.0, 4.5);
    sys.running = false;
    hook.join();

    /* 输出结果：若判定时刻完全跟随“事件 tick”，每次 Hit 的偏离都应接近
     * 注入的抖动值本身（±30ms 内），与 60fps 的 16.7ms 帧周期无关；
     * 若改成按帧时刻判定，就会多出平均半帧(~8ms)的额外误差。 */
    std::printf("=== 完成。Hit 偏离越接近注入抖动，说明时间戳链路越准确 ===\n");
    return 0;
}
