---
layout: default
title: "Notepad Project — Create the Window"
---

# Notepad Project — Create the Window

Section 4 — Creating a Notepad Project · Chapter 4.2

[Previous](zasm-04-01-notepad-project-setup.md) · [Contents](index.md) · [Next](zasm-04-03-notepad-add-the-text-editor.md)

## Define the Title, Size, and Buttons

Save the complete listing below as `notepad.zasm`, or use
[notepad-01-window.zasm](../../samples/Windows/notepad/notepad-01-window.zasm).
`window_title` defines the title, and the main `CreateWindowExA` call requests an
**800×600 outer window**. Its style enables resizing and standard title-bar controls.

Three child buttons occupy the top strip. The reusable `add_button` routine
receives an ID, caption pointer, and horizontal position. Every `PUSH` argument
to `CreateWindowExA` is labeled with its parameter name and meaning.

| Button | ID | Horizontal position |
| --- | --- | --- |
| Open | 101 | 8 |
| Save | 102 | 104 |
| Save As | 103 | 200 |

## Handle Window Messages

`RegisterClassA` associates `window_proc` with the window class. The message loop
uses `GetMessageA`, `TranslateMessage`, and `DispatchMessageA`. The callback handles
button clicks through `WM_COMMAND` and shutdown through `WM_DESTROY`; other
messages go to `DefWindowProcA`. That default handler processes the title-bar X.

Window callbacks preserve EBX, ESI, EDI, and EBP and return with `ret 16`. State is
attached using `GWL_USERDATA`, so callbacks can recover it without writable global
data. Early creation messages arrive before that pointer is attached and use
the default handler safely.

## Complete Source: Window and Three Buttons


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
window_command:
  mov eax, [ebp + 16]
cmp eax, 101
  jz stage_notice
  cmp eax, 102
  jz stage_notice
  cmp eax, 103
  jnz default_message
stage_notice:
  push 0x40
  push window_title
  push stage_text
  push ebx
  call [MessageBoxA]
callback_handled:
  xor eax, eax
callback_done:
  pop edi
  pop esi
  pop ebx
  pop ebp
  ret 16                 ; stdcall: discard four window-procedure arguments.
```

## Build and Check

Build and run the listing using the command from Chapter 4.1. Expect the title
**Zero Notepad**, an empty client area, and the three buttons. Each button shows
the temporary Chapter 4.4 notice. Close the window using its title-bar X.

> **Checkpoint:** Build this listing independently. Verify the title, initial
> window dimensions, three buttons, and normal process exit before adding text.

---

[Previous](zasm-04-01-notepad-project-setup.md) · [Contents](index.md) · [Next](zasm-04-03-notepad-add-the-text-editor.md)
