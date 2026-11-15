#pragma once

// Defensive fixes for third-party render code shipped with the game.
//
// Some Camera Filter Pack effects (e.g. Glow_Glow_Color.OnRenderImage) size
// their scratch textures from the camera target they are handed. When that
// target is 0x0 - a transition frame, a 0-height viewport - they call
// RenderTexture.GetTemporary(0, h) and Unity throws
//
//   ArgumentException: RenderTextureDesc height must be greater than zero.
//
// which kills the process. We guard RenderTexture.GetTemporary() so the
// request is clamped to a 1x1 texture instead: that frame simply loses the
// glow, the game keeps running.
//
// Must be called from the game main thread (it touches il2cpp metadata) and is
// idempotent - safe to call on every lazy-init retry.
void renderfix_install();
