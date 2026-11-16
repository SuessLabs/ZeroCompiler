---
layout: default
title: "Notepad Project — Add the Text Editor"
---

# Notepad Project — Add the Text Editor

Section 4 — Creating a Notepad Project · Chapter 4.3

[Previous](zasm-04-02-notepad-create-the-window.md) · [Contents](index.md) · [Next](zasm-04-04-notepad-open-and-save-files.md)

Convert the single-line textbox into a multiline editing area. Keep ID 102 and
the Show message button for a quick readback test.

1. Replace the block labeled `; Textbox:` up to, but not including, the
   `; Button:` block with:

```asm
  ; Multiline EDIT control, ID 102, at (20, 46), size 620 x 300.
  ; CreateWindowExA arguments are pushed right to left (last parameter first).
  push 0              ; lpParam:      No application-defined creation data.
  push esi            ; hInstance:    This application's instance handle.
  push 102            ; hMenu:        Control ID 102 for this child window.
  push ebx            ; hWndParent:   Handle of the main application window.
  push 300            ; nHeight:      Editor height in pixels.
  push 620            ; nWidth:       Editor width in pixels.
  push 46             ; Y:            Top position in the parent's client area, in pixels.
  push 20             ; X:            Left position in the parent's client area, in pixels.
  push 0x50B110C4     ; dwStyle:      Child, visible, border, tab stop, scroll bars,
                      ;               multiline, automatic scrolling, and Enter-key input.
  push initial_text   ; lpWindowName: Address of the initial zero-terminated text.
  push edit_class     ; lpClassName:  Address of the zero-terminated "EDIT" class name.
  push 0              ; dwExStyle:    No extended window styles.
  call [CreateWindowExA]
  test eax, eax
  jz failed
```

2. The style combines child/visible/border/tab-stop, scroll bars, multiline,
   automatic scrolling, and Enter-key input. In the `; Button:` block change
   y from `push 90` to `push 360` to position the button below the editor.
3. Rebuild. Enter multiple lines, scroll, then click Show message. The callback
   reads up to 255 characters into a 256-byte stack buffer: the popup is a
   short preview, not a full-document export.
4. Keep fixed dimensions for now. Later, handle `WM_SIZE` and call `MoveWindow`
   after obtaining the client area to implement resizing.

This milestone provides a basic editor with readback. Persistence comes next.
The current APIs are ANSI; start with ASCII test input.

> **Checkpoint:** Confirm Enter inserts a line break and closing still exits.

---

[Previous](zasm-04-02-notepad-create-the-window.md) · [Contents](index.md) · [Next](zasm-04-04-notepad-open-and-save-files.md)
