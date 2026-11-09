---
layout: default
title: Windows API Headers
---

# Windows API Headers

[Documentation home](index.md) · [Include syntax](Zero-Assembly-Include-Files.md) ·
[Language reference](zasm-reference.md)

ZASM ships curated `.zh` headers for common Windows DLLs. They contain `#IMPORT`
declarations, so your program can share library imports instead of repeating them.
The headers target the current Windows x86 backend.

## Choose a Header

| Area | Header | Example functions |
| --- | --- | --- |
| Basic console text | `console.zh` | `puts`, `printf`, `exit` |
| Minimal Windows startup | `windows.zh` | `ExitProcess`, `GetModuleHandleA`, `MessageBoxA` |
| Core OS services | `kernel32.zh` | `CreateFileA/W`, `ReadFile`, `WriteFile`, `HeapAlloc`, `CreateThread` |
| Windows and messages | `user32.zh` | `CreateWindowExA/W`, `GetMessageA/W`, `SendMessageA/W`, `MessageBoxA/W` |
| Drawing and fonts | `gdi32.zh` | `BitBlt`, `CreateFontA/W`, `TextOutA/W`, `DeleteObject` |
| Common controls | `comctl32.zh` | `InitCommonControlsEx`, `ImageList_Create`, `CreateStatusWindowA/W` |
| Standard dialogs | `comdlg32.zh` | `GetOpenFileNameA/W`, `ChooseColorA/W`, `PrintDlgA/W` |
| Shell integration | `shell32.zh` | `ShellExecuteA/W`, `SHGetKnownFolderPath`, `Shell_NotifyIconA/W` |
| Modern sockets | `ws2_32.zh` | `WSAStartup`, `socket`, `connect`, `send`, `recv`, `getaddrinfo` |
| Legacy sockets | `wsock32.zh` | Winsock 1.1-compatible socket functions |
| Registry and security | `advapi32.zh` | `RegOpenKeyExA/W`, `RegQueryValueExA/W`, `OpenProcessToken` |
| COM support | `ole32.zh` | `CoInitializeEx`, `CoCreateInstance`, `CoTaskMemFree` |
| Automation data | `oleaut32.zh` | `SysAllocString`, `VariantClear`, `SafeArrayCreateVector` |
| Path/string helpers | `shlwapi.zh` | `PathFileExistsA/W`, `PathFindFileNameA/W`, `StrStrIA/W` |
| Sound and timers | `winmm.zh` | `PlaySoundA/W`, `timeGetTime`, `timeBeginPeriod` |
| HTTP requests | `winhttp.zh` | `WinHttpOpen`, `WinHttpSendRequest`, `WinHttpReadData` |
| File version resources | `version.zh` | `GetFileVersionInfoA/W`, `VerQueryValueA/W` |

`A/W` in this table abbreviates two separate exports, such as `CreateFileA` and
`CreateFileW`; it is not ZASM syntax. Read the actual header for its complete list.
See the [header catalog](https://github.com/SuessLabs/ZeroCompiler/blob/master/include/readme.md)
for usage notes. Microsoft documents each function's parameters and DLL in its
[Windows API index](https://learn.microsoft.com/en-us/windows/win32/apiindex/windows-api-list).

## Why There Is No ComCtl64 Header

The Windows common-controls DLL is named `comctl32.dll` on both 32-bit and 64-bit
Windows. There is no separate standard `comctl64.dll`; `comctl32.zh` is the correct
header. Including it does not switch the compiler to a 64-bit target.
See Microsoft's [common-control versions](https://learn.microsoft.com/en-us/windows/win32/controls/common-control-versions).

## Basic Usage

```asm
using "kernel32.zh"
using "user32.zh"
using "comctl32.zh"
```

Include only the libraries you need. Every function declared by an included header
is added to the executable's import table, even if the code does not call it.
For just one or two functions, a direct declaration can keep dependencies smaller:

```asm
#IMPORT kernel32.dll, ExitProcess
```

`windows.zh` keeps its original three imports. It does not automatically include
all Windows headers. Existing examples continue to use it unchanged.

## Common-Controls Example

Run the shipped sample from the published directory:

```powershell
.\zero.exe run samples\hello-windows-headers.zasm
```

It loads `console.zh`, `kernel32.zh`, and `comctl32.zh`, initializes common controls,
and prints:

```text
Common controls initialized. Process ID: <your process ID>
```

The sample builds an `INITCOMMONCONTROLSEX` structure on the writable stack:

```asm
push 0x000000FF          ; dwICC = ICC_WIN95_CLASSES.
push 8                   ; dwSize = 8 bytes.
lea eax, [esp]           ; Address of the structure.
push eax
call [InitCommonControlsEx]
add esp, 8               ; Release the structure after the API returns.
```

Pushing the last field first places the size at the lower address, where the API
expects it. The stdcall function removes its pointer argument; the caller still
needs to remove the structure's eight bytes. Check EAX for success before proceeding,
as the sample does. See Microsoft's
[InitCommonControlsEx reference](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-initcommoncontrolsex).
Version 6 visual styles also need an activation manifest; this sample does not
add one or open any windows.

## Strings, Structures, and Cleanup

- **ANSI and Unicode:** use actual `A` or `W` function names. `DB` emits UTF-8, whereas `W` APIs require UTF-16. A small UTF-16 string can be written as `text: dw 72, 105, 0` for `Hi`.
- **Arguments:** push them right to left. Windows stdcall functions remove their own arguments; cdecl functions such as `printf` require caller cleanup.
- **Structures:** these headers do not define layouts, sizes, constants, callbacks, or type checking. Supply the documented memory layout and use writable storage for output parameters.
- **Resources:** release handles, memory, sockets, and GDI objects with the matching API. For example, use `closesocket` for a socket, not `CloseHandle`.
- **Initialization:** Winsock requires `WSAStartup`; COM commonly requires `CoInitializeEx`. Check each API's result and balance successful initialization with cleanup.
- **Names:** macros such as C common-control helpers and `GetWindowLongPtr` are not automatically exported function names for this x86 target. These headers list actual DLL exports.
- **Availability:** imports are resolved when the application loads. Test on the Windows versions you intend to support; the catalog is not a promise that every function exists on all versions.

Prefer `ws2_32.zh` for new socket programs. Avoid mixing it with `wsock32.zh`
unless you use DLL-qualified import names for overlapping functions, such as
`call [IAT_ws2_32_dll_socket]`.

## Validation and Shipping

The build copies root `include` into `publish/include`, and release ZIPs include
that directory. Unit tests compile every shipped header separately and all headers
together. On Windows, they also verify declared names against installed x86 DLL
export tables without calling any of those APIs.

## Coming Soon

- Named API constants and flag combinations when constant expressions are supported.
- Structure declarations and field offsets when structured data is supported.
- Complete examples for file dialogs, GDI drawing, sockets, HTTP, and COM.
- Manifest/resource support for common-controls version 6 visual styles.

These headers supply imports today; they do not implement the language features
listed above. Track those separately in the [ZASM roadmap](zasm-coming-soon.md).
