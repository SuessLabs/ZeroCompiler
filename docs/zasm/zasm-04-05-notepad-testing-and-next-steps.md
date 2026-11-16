---
layout: default
title: "Notepad Project — Test and Extend"
---

# Notepad Project — Test and Extend

Section 4 — Creating a Notepad Project · Chapter 4.5

[Previous](zasm-04-04-notepad-open-and-save-files.md) · [Contents](index.md) · [Next](zasm-05-01-zero-cli-compiler.md)

## Build the Finished Source

Use the full listing from Chapter 4.4 or the standalone
[finished source](../../samples/Windows/win-ui-10-notepad.zasm).
From the repository root, compile to an explicit path:

```powershell
.\publish\zero.exe samples\Windows\win-ui-10-notepad.zasm output\notepad.exe
.\output\notepad.exe
```

For your own `ZeroNotepad` folder, use the commands from Chapter 4.4 instead.
No NuGet packages, headers, images, or resource files are required by the sample.

## Run the Automated Source and File Checks

With Python 3 and the native compiler available on Windows, run from the
repository root:

```powershell
python samples/Windows/notepad/verify.py
```

Pass `--compiler "C:\Path\To\zero.exe"` to select another compiler. The check
compares each chapter's full listing with its standalone sample, compiles all
three stages, and exercises the finished file routines against a hidden multiline
edit control. It verifies text bytes, empty files, size limits, rejection of zero
bytes, failed-write dirty state, and balanced stacks. Fixtures and executables
stay beneath `output/notepad-check`.

Interactive filename selection and error popups are replaced with deterministic
test stubs. Run the manual dialog and unsaved-prompt checks below as well.

## Test the Window and Editor

| Action | Expected result |
| --- | --- |
| Launch | Initial 800×600 outer window titled Zero Notepad |
| Inspect top row | Open, Save, Save As, Close in that order |
| Enter several lines | Enter inserts line breaks; text remains editable |
| Resize or maximize | Editor fills the width and height below the button strip |
| Select/copy/paste | Standard Windows edit-control operations work |
| Minimize and restore | Document and controls remain available |

The dimensions describe the outer window in the API's coordinate space; display
scaling and window borders affect the visible client dimensions.

## Test Open, Save, and Save As

Use new test files inside a writable workspace:

1. Type two lines and click **Save**. A new document should open Save As.
2. Save as `first.txt`. Verify the two lines in another text editor.
3. Append a third line and click **Save**. The same file should change without
   asking for its filename again.
4. Click **Save As** and choose `second.txt`. The title should show the new path;
   later Save operations should update `second.txt`.
5. Click **Open** and load `first.txt`. Its original saved contents should return.
6. Repeat with an empty file and a path containing spaces.
7. Cancel each dialog. The current document and filename should remain intact.

## Test Unsaved Changes and Closing

After typing an edit, try both Open and Close. Repeat the close test with the
title-bar X as well as the Close button.

| Prompt choice | Expected result |
| --- | --- |
| Yes | Save first; continue only after a successful save |
| No | Continue without saving the edits |
| Cancel | Keep the current document and window |
| Yes, then cancel Save As | Abort the original Open/Close action |
| Yes, then encounter a save error | Report error and keep the document open |

Programmatic `SetWindowTextA` is not a substitute for a typing test of dirty
tracking: this multiline control's normal user edits send `EN_CHANGE`.

## Test Limits and Failures

| Input or condition | Expected result |
| --- | --- |
| Zero-byte file | Opens as an empty document and saves successfully |
| Zero-free ASCII file of 65,535 bytes | Accepted |
| File of 65,536 bytes or more | Rejected without replacing the current text |
| File containing a zero byte | Rejected as unsupported text |
| Missing/unreadable source | Dialog or application reports an error |
| Unwritable destination | Saving reports failure; dirty state remains |
| Existing Save As destination | Native dialog asks before overwrite |

Only the current file-I/O operation reports success after its count and close
checks pass. Writes are direct replacements, so use disposable test destinations
when exercising failure conditions.

## Debug the Message and File Paths

Save sources, press F9 on an instruction, then F5 to launch a debug build.
Useful labels are `window_command`, `window_close`, `choose_path`,
`open_document`, `write_pending`, and `commit_path`. Step over API calls with F10.
Check EAX for results and ESP for balanced cleanup. The callback restores EBX,
ESI, EDI, and EBP before `ret 16`; changing that contract can corrupt message dispatch.

## Next Steps

The source already implements the requested four-button editor. Further projects
can add UTF-8/UTF-16 conversion, Unicode filenames and long paths, atomic saves,
backups, New, keyboard shortcuts, a modified-title indicator, and larger documents.
The buttons intentionally remain a fixed-width row; a more elaborate toolbar
could reflow them in very narrow windows. GUI-subsystem output also requires
future compiler support if you want to remove the accompanying console.

> **Checkpoint:** Finish a full open/edit/save/save-as/close round trip and verify
> Cancel preserves work. Keep the completed source as your baseline before extending it.

---

[Previous](zasm-04-04-notepad-open-and-save-files.md) · [Contents](index.md) · [Next](zasm-05-01-zero-cli-compiler.md)
