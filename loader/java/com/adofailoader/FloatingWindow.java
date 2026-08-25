//我非常愤怒因为我只会Java一点点 c语言（C C++ C#等）啥的根本不懂😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠  
//了.cpp的文件全都是ai写的😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠😠

package com.adofailoader;

import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import java.io.File;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class FloatingWindow {
    private static WindowManager windowManager;
    private static LinearLayout floatingView;
    private static boolean isShowing = false;
    private static String currentPath = "/storage/emulated/0/Android/data/com.fizzd.connectedworlds/files/";
    private static List<String> fileList = new ArrayList<>();
    private static List<String> pathList = new ArrayList<>();
    private static ListView listView;
    private static TextView pathTextView;
    private static Button loadButton;
    private static String selectedFilePath = null;
    private static Context appContext;

    public static void show(final Context context) {
        if (isShowing) return;
        appContext = context;

        // 确保在主线程执行
        if (context instanceof Activity) {
            ((Activity) context).runOnUiThread(() -> createWindow(context));
        } else {
            new Handler(Looper.getMainLooper()).post(() -> createWindow(context));
        }
    }

    private static void createWindow(Context context) {
        // 检查悬浮窗权限
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            if (!Settings.canDrawOverlays(context)) {
                Toast.makeText(context, "请授予悬浮窗权限", Toast.LENGTH_LONG).show();
                return;
            }
        }

        windowManager = (WindowManager) context.getSystemService(Context.WINDOW_SERVICE);
        if (windowManager == null) {
            Toast.makeText(context, "无法获取 WindowManager", Toast.LENGTH_LONG).show();
            return;
        }

        // 创建悬浮窗布局
        floatingView = new LinearLayout(context);
        floatingView.setOrientation(LinearLayout.VERTICAL);
        floatingView.setBackgroundColor(Color.parseColor("#DD222222"));
        floatingView.setPadding(20, 20, 20, 20);
        floatingView.setElevation(20);

        // 标题
        TextView title = new TextView(context);
        title.setText("选择关卡ing...");
        title.setTextColor(Color.WHITE);
        title.setTextSize(20);
        title.setPadding(10, 10, 10, 20);
        floatingView.addView(title);

        // 当前路径显示
        pathTextView = new TextView(context);
        pathTextView.setText(currentPath);
        pathTextView.setTextColor(Color.GRAY);
        pathTextView.setTextSize(12);
        pathTextView.setPadding(10, 5, 10, 15);
        floatingView.addView(pathTextView);

        // 文件列表
        listView = new ListView(context);
        listView.setBackgroundColor(Color.parseColor("#44000000"));
        listView.setDividerHeight(1);
        listView.setDivider(Color.parseColor("#66666666"));
        LinearLayout.LayoutParams listParams = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            400
        );
        listView.setLayoutParams(listParams);
        floatingView.addView(listView);

        // 按钮区域
        LinearLayout buttonRow = new LinearLayout(context);
        buttonRow.setOrientation(LinearLayout.HORIZONTAL);
        buttonRow.setPadding(0, 15, 0, 0);

        Button backBtn = new Button(context);
        backBtn.setText("← 返回");
        backBtn.setBackgroundColor(Color.parseColor("#66444444"));
        backBtn.setTextColor(Color.WHITE);
        LinearLayout.LayoutParams btnParams = new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1
        );
        btnParams.setMargins(0, 0, 5, 0);
        backBtn.setLayoutParams(btnParams);
        backBtn.setOnClickListener(v -> goBack());
        buttonRow.addView(backBtn);

        Button rootBtn = new Button(context);
        rootBtn.setText("Is 根目录");
        rootBtn.setBackgroundColor(Color.parseColor("#66444444"));
        rootBtn.setTextColor(Color.WHITE);
        LinearLayout.LayoutParams rootParams = new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1
        );
        rootParams.setMargins(5, 0, 5, 0);
        rootBtn.setLayoutParams(rootParams);
        rootBtn.setOnClickListener(v -> goRoot());
        buttonRow.addView(rootBtn);

        Button refreshBtn = new Button(context);
        refreshBtn.setText("CULCK ME 刷新");
        refreshBtn.setBackgroundColor(Color.parseColor("#66444444"));
        refreshBtn.setTextColor(Color.WHITE);
        LinearLayout.LayoutParams refreshParams = new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1
        );
        refreshParams.setMargins(5, 0, 0, 0);
        refreshBtn.setLayoutParams(refreshParams);
        refreshBtn.setOnClickListener(v -> refreshList());
        buttonRow.addView(refreshBtn);

        floatingView.addView(buttonRow);

        // 加载按钮
        loadButton = new Button(context);
        loadButton.setText("▶ 加载选中关卡");
        loadButton.setBackgroundColor(Color.parseColor("#66555588"));
        loadButton.setTextColor(Color.WHITE);
        loadButton.setEnabled(false);
        loadButton.setOnClickListener(v -> loadSelectedLevel());
        LinearLayout.LayoutParams loadParams = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT
        );
        loadParams.setMargins(0, 15, 0, 0);
        loadButton.setLayoutParams(loadParams);
        floatingView.addView(loadButton);

        // 窗口参数
        WindowManager.LayoutParams params = new WindowManager.LayoutParams(
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
                : WindowManager.LayoutParams.TYPE_PHONE,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE,
            PixelFormat.TRANSLUCENT
        );
        params.gravity = Gravity.TOP | Gravity.START;
        params.x = 50;
        params.y = 150;

        windowManager.addView(floatingView, params);
        isShowing = true;
        refreshList();
        setupDrag(floatingView, params);

        Toast.makeText(context, "悬浮窗已显示", Toast.LENGTH_SHORT).show();
    }

    private static void setupDrag(View view, WindowManager.LayoutParams params) {
        final float[] dX = new float[1];
        final float[] dY = new float[1];
        view.setOnTouchListener((v, event) -> {
            switch (event.getAction()) {
                case android.view.MotionEvent.ACTION_DOWN:
                    dX[0] = params.x - event.getRawX();
                    dY[0] = params.y - event.getRawY();
                    return true;
                case android.view.MotionEvent.ACTION_MOVE:
                    params.x = (int) (event.getRawX() + dX[0]);
                    params.y = (int) (event.getRawY() + dY[0]);
                    windowManager.updateViewLayout(floatingView, params);
                    return true;
                case android.view.MotionEvent.ACTION_UP:
                    return true;
            }
            return false;
        });
    }

    private static void refreshList() {
        File dir = new File(currentPath);
        if (!dir.exists() || !dir.isDirectory()) {
            Toast.makeText(appContext, "目录不存在: " + currentPath, Toast.LENGTH_SHORT).show();
            return;
        }

        fileList.clear();
        pathList.clear();

        if (!currentPath.equals("/")) {
            fileList.add(" ... (返回上级)");
            pathList.add("...");
        }

        File[] files = dir.listFiles();
        if (files == null) return;

        List<File> dirs = new ArrayList<>();
        List<File> adofaiFiles = new ArrayList<>();

        for (File f : files) {
            if (f.getName().startsWith(".")) continue;
            if (f.isDirectory()) {
                dirs.add(f);
            } else if (f.getName().toLowerCase().endsWith(".adofai")) {
                adofaiFiles.add(f);
            }
        }

        Collections.sort(dirs, (a, b) -> a.getName().compareToIgnoreCase(b.getName()));
        Collections.sort(adofaiFiles, (a, b) -> a.getName().compareToIgnoreCase(b.getName()));

        for (File d : dirs) {
            fileList.add("谱: " + d.getName() + "/");
            pathList.add(d.getAbsolutePath());
        }

        for (File f : adofaiFiles) {
            fileList.add("音乐: " + f.getName());
            pathList.add(f.getAbsolutePath());
        }

        ArrayAdapter<String> adapter = new ArrayAdapter<String>(appContext,
            android.R.layout.simple_list_item_1, fileList) {
            @Override
            public View getView(int position, View convertView, ViewGroup parent) {
                View view = super.getView(position, convertView, parent);
                TextView text = (TextView) view;
                String item = fileList.get(position);
                if (item.startsWith("LEVEL: ")) {
                    text.setTextColor(Color.parseColor("#FFCC66"));
                } else if (item.startsWith("MUSIC: ")) {
                    text.setTextColor(Color.parseColor("#66CCFF"));
                } else {
                    text.setTextColor(Color.WHITE);
                }
                return view;
            }
        };
        listView.setAdapter(adapter);

        listView.setOnItemClickListener((parent, view, position, id) -> {
            String selected = pathList.get(position);
            if (selected.equals("..")) {
                goBack();
                return;
            }
            File selectedFile = new File(selected);
            if (selectedFile.isDirectory()) {
                currentPath = selected;
                pathTextView.setText(currentPath);
                refreshList();
            } else if (selectedFile.isFile() && selected.toLowerCase().endsWith(".adofai")) {
                selectedFilePath = selected;
                loadButton.setEnabled(true);
                loadButton.setText("▶ 加载: " + selectedFile.getName());
                Toast.makeText(appContext, "已选择: " + selectedFile.getName(), Toast.LENGTH_SHORT).show();
            }
        });

        pathTextView.setText(currentPath);
        selectedFilePath = null;
        loadButton.setEnabled(false);
        loadButton.setText("▶ 加载选中关卡");
    }

    private static void goBack() {
        File current = new File(currentPath);
        File parent = current.getParentFile();
        if (parent != null && parent.exists()) {
            currentPath = parent.getAbsolutePath();
            pathTextView.setText(currentPath);
            refreshList();
        }
    }

    private static void goRoot() {
        currentPath = "/storage/emulated/0/Android/data/com.fizzd.connectedworlds/files/";
        pathTextView.setText(currentPath);
        refreshList();
    }

    private static void loadSelectedLevel() {
        if (selectedFilePath == null) {
            Toast.makeText(appContext, "请先选择一个 .adofai 文件", Toast.LENGTH_SHORT).show();
            return;
        }
        Toast.makeText(appContext, "正在加载: " + selectedFilePath, Toast.LENGTH_SHORT).show();
        ToolBridge.loadLevel(appContext, selectedFilePath);
    }

    public static void hide() {
        if (isShowing && windowManager != null && floatingView != null) {
            windowManager.removeView(floatingView);
            isShowing = false;
        }
    }
}