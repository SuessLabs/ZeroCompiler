---
layout: default
title: "Notepad Project — Open and Save Files"
---

# Notepad Project — Open and Save Files

Section 4 — Creating a Notepad Project · Chapter 4.4

[Previous](zasm-04-03-notepad-add-the-text-editor.md) · [Contents](index.md) · [Next](zasm-04-05-notepad-testing-and-next-steps.md)

**Extension workshop:** the preceding edits and shipped sample do not implement
persistence. These are the next implementation steps, not a supplied finished
Notepad application. The file APIs are available through imports today.

Start with a fixed test filename in the workspace; add dialogs after loading
and saving work. `kernel32.zh` declares file APIs and `user32.zh` declares text
control APIs. Declarations alone do not implement these routines.

For **Save**:

1. Obtain control 102 with `GetDlgItem`; call `GetWindowTextLengthA`.
2. Allocate length plus one for an ending zero. Use a bounded stack reservation
   for a deliberately small exercise, or heap storage for variable-sized text.
   Keep it alive until the write finishes.
3. Call `GetWindowTextA` with capacity including the zero. Use its returned
   count as the byte count for this ANSI exercise, excluding the terminator.
4. Call `CreateFileA` with write access and a deliberate creation policy.
   Check its failure sentinel before writing.
5. Call `WriteFile` with writable storage for the bytes-written result. Check
   success and the byte count, then call `CloseHandle`.
6. On every failure, release acquired resources and show an error. Preserve
   document state until saving is confirmed.

For **Open**, open an existing file, determine and bound its size, allocate
size plus one, and call `ReadFile`. Check the actual count. Append an ending
zero in writable storage before `SetWindowTextA`. Direct x86 stores are missing:
use an imported routine such as `memset` for initialization/termination or a
zero-initialized heap allocation. Close the file and free the buffer on every
path. Treat embedded zero bytes as unsupported input.

Retain the current document if reading fails. Do not mark it saved after a
failed or partial write. Then add New/Open/Save commands, dialogs, encoding
conversion, and an unsaved-changes prompt as separate tested features.

**Checkpoint:** Define ownership, capacity, failure handling, and cleanup for
every pointer and handle before implementing these calls.

---

[Previous](zasm-04-03-notepad-add-the-text-editor.md) · [Contents](index.md) · [Next](zasm-04-05-notepad-testing-and-next-steps.md)
