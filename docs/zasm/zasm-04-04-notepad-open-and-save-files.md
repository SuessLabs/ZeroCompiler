---
layout: default
title: "Notepad Project — Open, Save, Save As, and Close"
---

# Notepad Project — Open, Save, Save As, and Close

Section 4 — Creating a Notepad Project · Chapter 4.4

[Previous](zasm-04-03-notepad-add-the-text-editor.md) · [Contents](index.md) · [Next](zasm-04-05-notepad-testing-and-next-steps.md)

## The Finished Application

Replace `notepad.zasm` with the **entire listing below**. It is also provided as
[win-ui-10-notepad.zasm](../../samples/Windows/win-ui-10-notepad.zasm).
This listing includes window creation, all four buttons, resizing, dialogs,
file routines, error reporting, and shutdown. No additional project files are
required.

## Connect the Four Buttons

`WM_COMMAND` combines the notification code with the control ID. For an ordinary
button click, `BN_CLICKED` is zero, so comparing `wParam` with its ID is sufficient.
Editor changes arrive with `EN_CHANGE` in the high word and ID 201 in the low
word; the handler marks the document dirty.

| Button | ID | Handler | Behavior |
| --- | --- | --- | --- |
| Open | 101 | `button_open` → `open_document` | Confirm unsaved changes, choose a file, read and display it |
| Save | 102 | `button_save` → `save_document` | Write to the current path; use Save As for an unnamed document |
| Save As | 103 | `button_save_as` → `save_as_document` | Choose a destination and write the document |
| Close | 104 | `button_close` → `WM_CLOSE` | Confirm unsaved changes and destroy the window |

The Close button sits after Save As. Both it and the title-bar X reach
`window_close`, so neither bypasses the unsaved-changes prompt.

## Build the File Dialog Structure

`choose_path` constructs the 88-byte x86 `OPENFILENAMEA` on the stack. Every field
is commented with its offset. It calls `GetOpenFileNameA` or `GetSaveFileNameA`
and distinguishes user cancellation from an API error with `CommDlgExtendedError`.
The filter contains display/pattern pairs and ends with two zero bytes. See
[Microsoft's OPENFILENAMEA reference](https://learn.microsoft.com/en-us/windows/win32/api/commdlg/ns-commdlg-openfilenamea)
for its fields and flags.

The dialog writes to a **pending path**, keeping the current path unchanged
until file I/O succeeds. Save As requests overwrite confirmation. Both dialogs
preserve the application's working directory with `OFN_NOCHANGEDIR`.

## Read, Write, and Own the Buffers

The state block is allocated once and freed after the message loop exits:

| Offset | Storage | Purpose |
| --- | --- | --- |
| 0 | 4 bytes | Editor HWND |
| 4 | 4 bytes | Unsaved-change flag |
| 8 | 260 bytes | Current filename, including terminator |
| 268 | 260 bytes | Pending dialog filename |
| 528 | 65,537 bytes | File/text buffer and termination space |

`open_document` reads up to 65,536 bytes, one beyond the accepted limit. A read
of that size is rejected; smaller reads are checked for embedded zero bytes.
The buffer is zeroed first, ensuring termination for accepted input. Only a
successful read replaces the editor text and current path.

`write_pending` retrieves the editor text, writes the returned byte count without
its terminator, checks the write count, and checks `fclose` for flush failures.
The code uses `rb`/`wb` C-runtime modes so newline bytes are not translated by
the file layer. Every cdecl call has explicit argument cleanup.

Successful I/O copies the pending path to the current path, updates the title
to show that path, and clears the dirty flag. Failed writes retain the dirty flag.
Saving replaces the destination directly; a disk failure can leave a partial
file. Atomic replacement and backup files are further improvements, not provided
by this teaching example.

## Confirm Unsaved Changes and Close

`confirm_discard` shows Yes/No/Cancel before opening another file or closing.
Yes calls Save, No proceeds without saving, and Cancel aborts the action.
Canceling Save As or encountering a save error also aborts the pending action.
`DestroyWindow` leads to `WM_DESTROY`, which posts `WM_QUIT`. The main loop then
frees the state and calls `ExitProcess`.

## Complete Source: Finished Zero Notepad


```asm
; Zero Notepad: Windows x86. Compile this file with zero.exe.
; Numeric constants below are Win32 values; no macros or writable sections needed.
#TARGET windows-x86
#IMPORT kernel32.dll, GetModuleHandleA, GlobalAlloc, GlobalFree, ExitProcess
#IMPORT user32.dll, LoadCursorA, RegisterClassA, CreateWindowExA, ShowWindow, UpdateWindow
#IMPORT user32.dll, GetMessageA, TranslateMessage, DispatchMessageA, DefWindowProcA, PostQuitMessage
#IMPORT user32.dll, GetWindowLongA, SetWindowLongA, SendMessageA, MessageBoxA
#IMPORT user32.dll, GetClientRect, MoveWindow, GetWindowTextA, GetWindowTextLengthA, SetWindowTextA, DestroyWindow
#IMPORT msvcrt.dll, memcpy
#IMPORT comdlg32.dll, GetOpenFileNameA, GetSaveFileNameA, CommDlgExtendedError
#IMPORT msvcrt.dll, memset, strcpy, strlen, fopen, fread, fwrite, ferror, fclose
jmp start

window_class db 'ZeroNotepadClass', 0
window_title db 'Zero Notepad', 0
button_class db 'BUTTON', 0
edit_class db 'EDIT', 0
empty_text db 0
open_text db 'Open', 0
save_text db 'Save', 0
save_as_text db 'Save As', 0
close_text db 'Close', 0
stage_text db 'The file buttons are connected in Chapter 4.4.', 0
file_filter db 'Text files (*.txt)', 0, '*.txt', 0, 'All files (*.*)', 0, '*.*', 0, 0
default_ext db 'txt', 0
read_mode db 'rb', 0
write_mode db 'wb', 0
read_error db 'Cannot open this text file. It must be at most 65535 bytes and contain no zero bytes.', 0
write_error db 'Saving failed. Check the destination and available disk space. The file may be incomplete.', 0
dialog_error db 'The file dialog could not be opened.', 0
discard_text db 'Save changes before continuing?', 0
start:
  ; Zero-initialized, writable state: +0 editor HWND, +4 dirty flag,
  ; +8 current path[260], +268 dialog path[260], +528 text buffer[65537].
  push 66080
  push 0x40              ; GPTR: fixed, zero-initialized allocation.
  call [GlobalAlloc]
  test eax, eax
  jz failed
  push eax
  pop esi                ; ESI = state for the lifetime of the application.

  push 0
  call [GetModuleHandleA]
  push eax
  pop edi                ; EDI = application instance.
  push 32512             ; IDC_ARROW.
  push 0
  call [LoadCursorA]

  ; WNDCLASSA: 40 bytes, constructed last field first.
  push window_class      ; lpszClassName.
  push 0                 ; lpszMenuName.
  push 16                ; hbrBackground = COLOR_BTNFACE + 1.
  push eax               ; hCursor.
  push 0                 ; hIcon.
  push edi               ; hInstance.
  push 0                 ; cbWndExtra.
  push 0                 ; cbClsExtra.
  push window_proc       ; lpfnWndProc.
  push 3                 ; style: redraw on horizontal/vertical changes.
  lea eax, [esp]
  push eax
  call [RegisterClassA]
  add esp, 40
  test eax, eax
  jz failed

  ; CreateWindowExA parameters, last to first.
  push 0                 ; lpParam: no creation data.
  push edi               ; hInstance: application instance.
  push 0                 ; hMenu: no menu bar.
  push 0                 ; hWndParent: top-level window.
  push 600               ; nHeight: outer height, including frame/title.
  push 800               ; nWidth: outer width in pixels.
  push 0x80000000        ; Y: CW_USEDEFAULT.
  push 0x80000000        ; X: CW_USEDEFAULT.
  push 0x02CF0000        ; dwStyle: WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN.
  push window_title      ; lpWindowName: zero-terminated title.
  push window_class      ; lpClassName: registered class name.
  push 0                 ; dwExStyle: no extended styles.
  call [CreateWindowExA]
  test eax, eax
  jz failed
  push eax
  pop ebx                ; EBX = main window HWND.

  push esi               ; New window user-data value: state pointer.
  push 0xFFFFFFEB        ; GWL_USERDATA = -21.
  push ebx
  call [SetWindowLongA]

  ; add_button(id, text, x): arguments pushed right to left.
  push 8
  push open_text
  push 101
  call add_button
  push 104
  push save_text
  push 102
  call add_button
  push 200
  push save_as_text
  push 103
  call add_button
push 296
push close_text
push 104
call add_button
; Multiline editing area. WM_SIZE supplies its final dimensions.
push 0                 ; lpParam: no creation data.
push edi               ; hInstance: application instance.
push 201               ; hMenu: child control ID 201.
push ebx               ; hWndParent: main window.
push 1                 ; nHeight: temporary; layout_editor resizes it.
push 1                 ; nWidth: temporary; layout_editor resizes it.
push 44                ; Y: below the 44-pixel button strip.
push 0                 ; X: align with the client area's left edge.
push 0x50B110C4        ; dwStyle: child/visible/border/tab stop,
                       ; scroll bars, multiline, auto scroll, want Return.
push empty_text        ; lpWindowName: initially empty document.
push edit_class        ; lpClassName: built-in EDIT class.
push 0x200             ; dwExStyle: WS_EX_CLIENTEDGE.
call [CreateWindowExA]
test eax, eax
jz failed
lea edx, [esi]
call store_dword       ; state.editor = returned HWND.

push 0                 ; lParam: unused.
push 65535             ; wParam: maximum typed/pasted text bytes.
push 0xC5              ; EM_SETLIMITTEXT.
mov eax, [esi]
push eax
call [SendMessageA]
call layout_editor
push 1
  push ebx
  call [ShowWindow]
  push ebx
  call [UpdateWindow]
  sub esp, 32             ; Writable x86 MSG storage.
  lea edi, [esp]
message_loop:
  push 0
  push 0
  push 0
  push edi
  call [GetMessageA]
  test eax, eax
  jz finished
  cmp eax, 0xFFFFFFFF     ; GetMessageA returns -1 on failure.
  jz failed
  push edi
  call [TranslateMessage]
  push edi
  call [DispatchMessageA]
  jmp message_loop
finished:
  add esp, 32
  push esi
  call [GlobalFree]
  push 0
  call [ExitProcess]
failed:
  push 1
  call [ExitProcess]      ; Process teardown releases startup allocations.

; EBX = parent, EDI = instance. Callee removes our three arguments.
add_button:
  push ebp
  lea ebp, [esp]
  push 0                 ; lpParam: no creation data.
  push edi               ; hInstance: application instance.
  mov eax, [ebp + 8]
  push eax               ; hMenu: child button ID.
  push ebx               ; hWndParent: main window.
  push 28                ; nHeight: button height.
  push 88                ; nWidth: button width.
  push 8                 ; Y: top padding.
  mov eax, [ebp + 16]
  push eax               ; X: horizontal position supplied by caller.
  push 0x50010000        ; dwStyle: child, visible, tab stop, push button.
  mov eax, [ebp + 12]
  push eax               ; lpWindowName: button caption.
  push button_class      ; lpClassName: built-in BUTTON class.
  push 0                 ; dwExStyle: no extended styles.
  call [CreateWindowExA]
  test eax, eax
  jz failed
  pop ebp
  ret 12

; Windows x86 lacks MOV memory stores in ZASM. Copy four bytes via memcpy.
; Input: EDX = destination, EAX = value. Preserves EBX/ESI/EDI/EBP.
store_dword:
  push eax
  lea ecx, [esp]
  push 4
  push ecx
  push edx
  call [memcpy]
  add esp, 12            ; cdecl: remove memcpy's arguments.
  add esp, 4             ; Remove the temporary value.
  ret

window_proc:
  push ebp
  lea ebp, [esp]         ; +8 hwnd, +12 message, +16 wParam, +20 lParam.
  push ebx
  push esi
  push edi               ; Preserve the Win32 callback's nonvolatile registers.
  mov ebx, [ebp + 8]
  push 0xFFFFFFEB        ; GWL_USERDATA.
  push ebx
  call [GetWindowLongA]
  push eax
  pop esi
  test esi, esi          ; Early creation messages arrive before state is set.
  jz default_message
  mov eax, [ebp + 12]
  cmp eax, 2             ; WM_DESTROY.
  jz window_destroyed
  cmp eax, 0x111         ; WM_COMMAND.
  jz window_command
cmp eax, 5             ; WM_SIZE.
jz window_size
cmp eax, 0x10          ; WM_CLOSE from title-bar X or our Close button.
jz window_close
default_message:
  mov eax, [ebp + 20]
  push eax
  mov eax, [ebp + 16]
  push eax
  mov eax, [ebp + 12]
  push eax
  push ebx
  call [DefWindowProcA]
  jmp callback_done
window_destroyed:
  push 0
  call [PostQuitMessage]
  jmp callback_handled
window_size:
  call layout_editor
  jmp callback_handled
window_command:
  mov eax, [ebp + 16]
cmp eax, 0x030000C9    ; EN_CHANGE (0x300), editor ID 201 (0xC9).
  jz document_changed
  cmp eax, 101           ; BN_CLICKED = 0, Open ID 101.
  jz button_open
  cmp eax, 102
  jz button_save
  cmp eax, 103
  jz button_save_as
  cmp eax, 104
  jz button_close
  jmp default_message
document_changed:
  mov eax, 1
  lea edx, [esi + 4]
  call store_dword
  jmp callback_handled
button_open:
  call open_document
  jmp callback_handled
button_save:
  call save_document
  jmp callback_handled
button_save_as:
  call save_as_document
  jmp callback_handled
button_close:
  push 0
  push 0
  push 0x10              ; Send WM_CLOSE through the same unsaved-check path.
  push ebx
  call [SendMessageA]
  jmp callback_handled
window_close:
  call confirm_discard
  test eax, eax
  jz callback_handled
  push ebx
  call [DestroyWindow]   ; WM_DESTROY posts WM_QUIT for the message loop.
  jmp callback_handled
callback_handled:
  xor eax, eax
callback_done:
  pop edi
  pop esi
  pop ebx
  pop ebp
  ret 16                 ; stdcall: discard four window-procedure arguments.
; EBX = parent, ESI = state. Query the client area on every resize.
layout_editor:
  mov eax, [esi]
  test eax, eax
  jz layout_done         ; No editor yet during early startup messages.
  sub esp, 16            ; RECT { left, top, right, bottom }.
  lea eax, [esp]
  push eax
  push ebx
  call [GetClientRect]
  test eax, eax
  jz layout_release
  mov ecx, [esp + 8]     ; Client width (left is zero).
  mov edx, [esp + 12]    ; Client height (top is zero).
  sub edx, 44            ; Leave room for buttons.
  test edx, 0x80000000   ; Clamp a negative editor height to zero.
  jz layout_move
  xor edx, edx
layout_move:
  push 1                 ; bRepaint.
  push edx               ; nHeight: remaining client height.
  push ecx               ; nWidth: entire client width.
  push 44                ; Y: immediately below the toolbar.
  push 0                 ; X: left edge.
  mov eax, [esi]
  push eax               ; hWnd: multiline editor.
  call [MoveWindow]
layout_release:
  add esp, 16
layout_done:
  ret
; Shared file routines: EBX = parent HWND, ESI = state. Return EAX = 1/0.
; C runtime functions use cdecl, so every call removes its argument bytes.

; Ask before Open or Close discards edits. Yes saves; No proceeds; Cancel stops.
confirm_discard:
  mov eax, [esi + 4]
  test eax, eax
  jz operation_ok
  push 0x23              ; MB_YESNOCANCEL | MB_ICONQUESTION.
  push window_title
  push discard_text
  push ebx
  call [MessageBoxA]
  cmp eax, 6             ; IDYES.
  jz save_document
  cmp eax, 7             ; IDNO.
  jz operation_ok
operation_cancel:
  xor eax, eax
  ret
operation_ok:
  mov eax, 1
  ret

; Save uses the current path, or Save As for a new document.
save_document:
  lea eax, [esi + 8]
  push eax
  call [strlen]
  add esp, 4
  test eax, eax
  jz save_as_document
  lea eax, [esi + 8]
  push eax
  lea eax, [esi + 268]
  push eax
  call [strcpy]          ; pending_path = current_path (both 260-byte buffers).
  add esp, 8
  jmp write_pending
save_as_document:
  mov eax, 1             ; Save dialog mode.
  call choose_path
  test eax, eax
  jz operation_cancel
  jmp write_pending

; EAX = 0 for Open, 1 for Save As. Current path changes only after successful I/O.
choose_path:
  push edi
  push eax
  pop edi                ; Preserve the dialog mode across cdecl calls.
  lea eax, [esi + 8]
  push eax
  lea eax, [esi + 268]
  push eax
  call [strcpy]
  add esp, 8
  mov ecx, 0x81808       ; EXPLORER | FILEMUSTEXIST | PATHMUSTEXIST | NOCHANGEDIR.
  test edi, edi
  jz dialog_structure
  mov ecx, 0x8080A       ; EXPLORER | PATHMUSTEXIST | NOCHANGEDIR | OVERWRITEPROMPT.
dialog_structure:
  ; x86 OPENFILENAMEA: 88 bytes. Fields pushed in reverse offset order.
  push 0                 ; +84 FlagsEx.
  push 0                 ; +80 dwReserved.
  push 0                 ; +76 pvReserved.
  push 0                 ; +72 lpTemplateName.
  push 0                 ; +68 lpfnHook.
  push 0                 ; +64 lCustData.
  push default_ext       ; +60 lpstrDefExt: append .txt when appropriate.
  push 0                 ; +56 nFileOffset and nFileExtension (two WORDs).
  push ecx               ; +52 Flags: Open or Save As behavior.
  push 0                 ; +48 lpstrTitle: use the dialog's default title.
  push 0                 ; +44 lpstrInitialDir.
  push 0                 ; +40 nMaxFileTitle.
  push 0                 ; +36 lpstrFileTitle.
  push 260               ; +32 nMaxFile: capacity including the ending zero.
  lea eax, [esi + 268]
  push eax               ; +28 lpstrFile: writable pending-path buffer.
  push 1                 ; +24 nFilterIndex: first filter.
  push 0                 ; +20 nMaxCustFilter.
  push 0                 ; +16 lpstrCustomFilter.
  push file_filter       ; +12 lpstrFilter: double-zero-terminated pairs.
  push 0                 ; +8 hInstance: no custom template.
  push ebx               ; +4 hwndOwner: main window.
  push 88                ; +0 lStructSize: x86 OPENFILENAMEA size.
  lea eax, [esp]
  push eax
  test edi, edi
  jnz dialog_save
  call [GetOpenFileNameA]
  jmp dialog_finished
dialog_save:
  call [GetSaveFileNameA]
dialog_finished:
  add esp, 88
  pop edi
  test eax, eax
  jnz operation_ok
  call [CommDlgExtendedError]
  test eax, eax          ; Zero means the user canceled, not an API failure.
  jz operation_cancel
  mov eax, dialog_error
  jmp show_error

open_document:
  push edi
  call confirm_discard
  test eax, eax
  jz open_cancel
  xor eax, eax
  call choose_path
  test eax, eax
  jz open_cancel

  push 65537
  push 0
  lea eax, [esi + 528]
  push eax
  call [memset]          ; Ensure a zero terminator after any accepted read.
  add esp, 12
  push read_mode
  lea eax, [esi + 268]
  push eax
  call [fopen]
  add esp, 8
  test eax, eax
  jz open_error
  push eax
  pop edi                ; EDI = FILE*, preserved by the CRT.
  push edi
  push 65536             ; Read one byte beyond the accepted 65535-byte limit.
  push 1
  lea eax, [esi + 528]
  push eax
  call [fread]
  add esp, 16
  push eax               ; Save actual byte count across ferror/fclose.
  push edi
  call [ferror]
  add esp, 4
  test eax, eax
  jnz open_close_error
  push edi
  call [fclose]
  add esp, 4
  pop edx                ; Restore count and balance the stack even on failure.
  test eax, eax
  jnz open_error
  cmp edx, 65536
  jz open_error          ; Reject oversized input instead of silently truncating.
  push edx
  lea eax, [esi + 528]
  push eax
  call [strlen]
  add esp, 4
  pop edx
  cmp eax, edx
  jnz open_error         ; Embedded NUL would truncate the edit control's text.
  lea eax, [esi + 528]
  push eax
  mov eax, [esi]
  push eax
  call [SetWindowTextA]
  test eax, eax
  jz open_error
  call commit_path
  pop edi
  mov eax, 1
  ret
open_close_error:
  push edi
  call [fclose]
  add esp, 4
  add esp, 4             ; Drop the saved count on the read-error path.
open_error:
  mov eax, read_error
  call show_error
open_cancel:
  pop edi
  xor eax, eax
  ret

write_pending:
  push edi
  push 65536             ; Buffer capacity: limit + ending zero.
  lea eax, [esi + 528]
  push eax
  mov eax, [esi]
  push eax
  call [GetWindowTextA]
  push eax               ; Actual byte count; do not write the ending zero.
  push write_mode
  lea eax, [esi + 268]
  push eax
  call [fopen]           ; Binary write: replace/create the chosen file.
  add esp, 8
  test eax, eax
  jz write_error_count
  push eax
  pop edi
  mov eax, [esp]         ; Saved text length.
  push edi
  push eax
  push 1
  lea eax, [esi + 528]
  push eax
  call [fwrite]
  add esp, 16
  mov ecx, [esp]
  cmp eax, ecx
  jnz write_close_error
  push edi
  call [fclose]          ; Detect buffered flush/close failures as well.
  add esp, 4
  test eax, eax
  jnz write_error_count
  add esp, 4
  call commit_path
  pop edi
  mov eax, 1
  ret
write_close_error:
  push edi
  call [fclose]
  add esp, 4
write_error_count:
  add esp, 4             ; Drop saved length; retain dirty flag and current path.
  pop edi
  mov eax, write_error
  jmp show_error

commit_path:
  lea eax, [esi + 268]
  push eax
  lea eax, [esi + 8]
  push eax
  call [strcpy]
  add esp, 8
  xor eax, eax
  lea edx, [esi + 4]
  call store_dword       ; Mark clean only after successful loading/saving.
  lea eax, [esi + 8]
  push eax
  push ebx
  call [SetWindowTextA]  ; Show the current full filename in the title bar.
  ret

; Input EAX = error message address. Returns zero to cancel the operation.
show_error:
  push 0x10              ; MB_OK | MB_ICONERROR.
  push window_title
  push eax
  push ebx
  call [MessageBoxA]
  xor eax, eax
  ret
```

## Build and Use the Finished Program

From the folder containing your saved `notepad.zasm`:

```powershell
& 'C:\Program Files\SuessLabs\Zero\bin\zero.exe' notepad.zasm
.\notepad.exe
```

Type text and click Save. Because the document has no filename yet, the Save As
dialog appears. Choose a new `.txt` file in your workspace. Modify the text and
click Save again: it writes to that path. Save As selects a different path; Open
loads a chosen file. Close and the title-bar X share the unsaved-change prompt.

> **Checkpoint:** Create, save, reopen, and modify a text file. Save a second copy
> with Save As, then close using the fourth button. Verify the file contents on disk.

---

[Previous](zasm-04-03-notepad-add-the-text-editor.md) · [Contents](index.md) · [Next](zasm-04-05-notepad-testing-and-next-steps.md)
