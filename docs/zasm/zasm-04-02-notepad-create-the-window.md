---
layout: default
title: "Notepad Project — Create the Window"
---

# Notepad Project — Create the Window

Section 4 — Creating a Notepad Project · Chapter 4.2

[Previous](zasm-04-01-notepad-project-setup.md) · [Contents](index.md) · [Next](zasm-04-03-notepad-add-the-text-editor.md)

Read `start` in your copy. It obtains an instance handle, builds `WNDCLASSA` on
the stack, registers it, and creates a window. `window_proc` is its callback.

1. Change `window_class` to `ZeroNotepadWindow` and `window_title` to
   `Zero Notepad`. Retain their ending zero bytes.
2. In the top-level `CreateWindowExA` block, change height 220 to 480 and
   width 420 to 680. Height is pushed before width because argument order is
   right to left.
3. Retain `ShowWindow`, `UpdateWindow`, and the `GetMessageA` loop.
4. Retain the zero check for `WM_QUIT` and the `0xFFFFFFFF` error check.
5. Retain the `WM_DESTROY` handler calling `PostQuitMessage`.

Build and run. The larger window should have its new title, with controls at
their old positions. Closing it must still end the program.

The callback forwards unhandled messages to `DefWindowProcA`. Its EBP-based
frame exposes `hwnd` at +8, message at +12, `wParam` at +16, and `lParam` at +20.
Keep `pop ebp` and `ret 16` matched on every callback exit.

**Checkpoint:** A visible window needs both a responsive message loop and a
working destruction path.

---

[Previous](zasm-04-01-notepad-project-setup.md) · [Contents](index.md) · [Next](zasm-04-03-notepad-add-the-text-editor.md)
