package com.adofailoader;

import android.content.Context;
import android.widget.Toast;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;

public class ToolBridge {
    
    // 通过配置文件通知 Tool 加载关卡
    public static void loadLevel(Context context, String levelPath) {
        try {
            File configFile = new File("/data/data/com.fizzd.connectedworlds/files/tool_config.json");
            configFile.getParentFile().mkdirs();
            
            FileWriter writer = new FileWriter(configFile);
            writer.write("{\n");
            writer.write("    \"load_level\": \"" + levelPath + "\"\n");
            writer.write("}\n");
            writer.close();
            
            Toast.makeText(context, "已通知 Tool 加载关卡", Toast.LENGTH_SHORT).show();
            
            // 尝试调用 Tool 的 JNI 函数（如果存在）
            try {
                callToolLoadLevel(levelPath);
            } catch (UnsatisfiedLinkError e) {
                // Tool 没有导出这个函数，忽略
            }
            
        } catch (IOException e) {
            Toast.makeText(context, "加载失败: " + e.getMessage(), Toast.LENGTH_LONG).show();
        }
    }
    
    // Native 方法：直接调用 Tool 的加载函数
    public static native void callToolLoadLevel(String path);
}