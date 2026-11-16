---
layout: default
title: "Notepad Project — Set Up the Workspace"
---

# Notepad Project — Set Up the Workspace

Section 4 — Creating a Notepad Project · Chapter 4.1

[Previous](zasm-03-07-creating-functions.md) · [Contents](index.md) · [Next](zasm-04-02-notepad-create-the-window.md)

Grow the shipped controls example into a small Notepad-like editor. The first
milestone is an editable window; the second is file saving and loading. This
Windows x86 project retains the current console subsystem, so a console window
may accompany the editor.

1. Create a writable `ZeroNotepad` folder.
2. Copy [win-ui-02-controls.zasm](../../samples/Windows/win-ui-02-controls.zasm)
   into it as `notepad.zasm`. Keep the original for comparison.
3. Open the folder and file in Zero.IDE. Select the installed compiler.
4. Build and run unchanged. Type text and press **Show message**.
5. Close the window and verify the process exits.

Alternatively, from a terminal in the new source folder:

```powershell
& 'C:\Program Files\SuessLabs\Zero\bin\zero.exe' notepad.zasm
.\notepad.exe
```

Use the next chapters in order and save a working copy after each milestone.

> **Checkpoint:** Get the original sample working before changing registration,
> the callback, or the message loop.

---

[Previous](zasm-03-07-creating-functions.md) · [Contents](index.md) · [Next](zasm-04-02-notepad-create-the-window.md)
