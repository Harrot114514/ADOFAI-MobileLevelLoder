#pragma once

// Overlay: ImGui window with the level file browser.
// Rendering is driven from the eglSwapBuffers hook (render.cpp).

bool overlay_gl_init(int fb_w, int fb_h);
void overlay_gl_shutdown();
void overlay_render_frame(int fb_w, int fb_h);
