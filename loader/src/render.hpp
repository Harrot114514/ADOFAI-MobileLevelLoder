#pragma once

// Installs the eglSwapBuffers hook that drives the ImGui overlay rendering.
// Must be called once (any thread) after process start. Returns false on failure.
bool render_install_egl_hook();
