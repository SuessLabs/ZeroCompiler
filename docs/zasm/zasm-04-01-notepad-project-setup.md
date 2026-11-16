---
layout: default
title: "Notepad Project — Overview and Setup"
---

# Notepad Project — Overview and Setup

Section 4 — Creating a Notepad Project · Chapter 4.1

[Previous](zasm-03-07-creating-functions.md) · [Contents](index.md) · [Next](zasm-04-02-notepad-create-the-window.md)

## What You Will Build

Build a working Windows x86 text editor entirely in Zero Assembly. The finished
program has an initial 800×600 window, a top row of Open, Save, Save As, and Close
buttons, and a multiline editor that fills the remaining client area as you resize.
Native dialogs choose filenames. Save remembers the current path; Save As selects
a new one. Open and Close ask whether to save modified text first.

This section provides **complete source listings**, not replacement fragments.
Save each chapter's listing as `notepad.zasm`, replacing the previous stage's
contents. Each stage compiles independently. Chapter 4.4 contains the finished
application with every routine included; no earlier code needs to be assembled
by hand.

## Project Stages and Source Files

| Chapter | Result | Standalone source |
| --- | --- | --- |
| 4.2 | Titled 800×600 window, message handler, three top buttons | [notepad-01-window.zasm](../../samples/Windows/notepad/notepad-01-window.zasm) |
| 4.3 | Multiline editor and resize handling | [notepad-02-editor.zasm](../../samples/Windows/notepad/notepad-02-editor.zasm) |
| 4.4 | Open, Save, Save As, Close, and unsaved-change handling | [win-ui-10-notepad.zasm](../../samples/Windows/win-ui-10-notepad.zasm) |
| 4.5 | Build, round-trip tests, boundaries, and further work | Use the completed source above |

The first two stages show an informational message when a file button is clicked;
the third replaces that temporary handler with real file operations.

## Prerequisites

Use Windows with support for x86 applications, a working Zero compiler, and a
writable project folder. The earlier setup chapters explain IDE installation.
These sources declare imports directly, so no local headers or assets are needed.
Windows supplies the standard DLLs used by the program.

Create this folder and open it with **File > Open Folder...** in Zero.IDE:

```text
ZeroNotepad/
  notepad.zasm
```

Select your compiler under **Tools > Options > Compiler**. Save a listing, then
choose **Build > Run Without Debugging**, or compile from PowerShell in that folder:

```powershell
& 'C:\Program Files\SuessLabs\Zero\bin\zero.exe' notepad.zasm
.\notepad.exe
```

Replace the compiler path if installed elsewhere. The repository samples can
also be compiled directly using `publish/zero.exe` from the repository root.

## Scope of This Editor

The example supports small, zero-free text files up to **65,535 bytes**, using
Windows ANSI APIs. Use ASCII text for predictable results across code pages.
It preserves text bytes through binary C-runtime I/O; it is not a UTF-8/UTF-16
encoding converter. Paths have a 260-byte buffer, including their ending zero.
The initial 800×600 dimensions include the title bar and frame, so the client
area is smaller. The current compiler emits a console-subsystem executable.

The code uses zero-initialized heap storage because inline data is read-only.
`store_dword` uses `memcpy` to update four-byte state fields because the current
Windows x86 encoder lacks direct memory stores. This keeps all source compatible
with the implemented instruction set.

> **Checkpoint:** Locate the three standalone sources and identify which chapter
> contains the finished editor. Create a writable folder before continuing.

---

[Previous](zasm-03-07-creating-functions.md) · [Contents](index.md) · [Next](zasm-04-02-notepad-create-the-window.md)
