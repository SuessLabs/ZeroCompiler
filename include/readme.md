# Zero Assembly Includes

These `.zh` files ship in `include` beside `zero.exe` and `zcc.exe`. Choose the
headers you need, then call functions by their exported names:

```asm
using "kernel32.zh"
using "user32.zh"
using "comctl32.zh"
```

## Header Catalog

| Header | DLL | Useful for |
| --- | --- | --- |
| `console.zh` | `msvcrt.dll` | Small console programs: `puts`, `printf`, `exit`. |
| `windows.zh` | `kernel32.dll`, `user32.dll` | Existing minimal convenience header: `ExitProcess`, `GetModuleHandleA`, `MessageBoxA`. |
| `kernel32.zh` | `kernel32.dll` | Files, console I/O, processes, threads, memory, timers, synchronization, text conversion. |
| `user32.zh` | `user32.dll` | Windows, messages, controls, keyboard/mouse-related window interaction, painting, menus, clipboard. |
| `gdi32.zh` | `gdi32.dll` | Drawing, fonts, bitmaps, device contexts, and brushes. |
| `comctl32.zh` | `comctl32.dll` | Common-controls initialization, image lists, status bars, and toolbars. |
| `comdlg32.zh` | `comdlg32.dll` | Open/save dialogs, color/font pickers, find/replace, printing. |
| `shell32.zh` | `shell32.dll` | Shell execution, folders, file information, notification icons, drag-and-drop. |
| `ws2_32.zh` | `ws2_32.dll` | Preferred socket header: Winsock 2, TCP/UDP, address lookup, socket events. |
| `wsock32.zh` | `wsock32.dll` | Legacy Winsock imports for older API usage. |
| `advapi32.zh` | `advapi32.dll` | Registry keys, user names, access tokens, and privileges. |
| `ole32.zh` | `ole32.dll` | COM initialization, instance creation, GUIDs, and COM task memory. |
| `oleaut32.zh` | `oleaut32.dll` | Automation BSTR strings, VARIANTs, and SAFEARRAYs. |
| `shlwapi.zh` | `shlwapi.dll` | Path checks, filename/extension lookup, and substring search. |
| `winmm.zh` | `winmm.dll` | Sound playback and multimedia timer functions. |
| `winhttp.zh` | `winhttp.dll` | HTTP sessions, connections, requests, and response reading. |
| `version.zh` | `version.dll` | Read version information from existing files. |

Each header is a curated set of imports, not a complete Windows SDK translation.
Open the `.zh` file for the exact exported functions. All functions in an included
header enter the import table, even when unused. For a smaller import table, use
`#IMPORT` to declare only the functions you need. `windows.zh` remains deliberately small.

## Architecture and Names

The headers currently target Zero's **Windows x86** output. They do not enable x64
code generation. Windows retains names such as `kernel32.dll`, `user32.dll`, and
`comctl32.dll` for its 64-bit libraries as well; **there is no separate standard
`comctl64.dll`**, so no `comctl64.zh` header is supplied.
See Microsoft's [common-control versions](https://learn.microsoft.com/en-us/windows/win32/controls/common-control-versions).
The OS loads the DLL architecture appropriate for the generated executable.

## Calling Rules

- Push arguments right to left. Most imported Windows APIs use x86 stdcall and remove their own arguments.
- C runtime `puts` and `printf` use cdecl: remove their arguments with `add esp, ...` after the call.
- Explicit `A` exports take ANSI/code-page strings; explicit `W` exports take UTF-16. `DB` strings are UTF-8, so ASCII text works with the examples' `A` calls, but non-ASCII text may need conversion.
- `using` does not create constants, structures, API prototypes, or automatic argument validation. Use numeric flags and manually lay out required structures in writable memory.
- Initialize subsystems as required: for example, `WSAStartup` for Winsock and `CoInitializeEx` for COM. Check results and use the correct cleanup function.
- Common-control message helpers from C headers are often macros, not DLL exports. Use `SendMessageA/W` with the correct message and structure.
- Common-controls version 6 visual styles require an activation manifest; importing `comctl32.zh` alone does not add one.
- COM headers declare DLL functions; they do not add interface declarations, vtable-call syntax, or higher-level COM language support.

Do not normally include `wsock32.zh` and `ws2_32.zh` together: both declare names
such as `socket` and `WSAStartup`. If you intentionally combine them, select a DLL
with qualified aliases such as `call [IAT_ws2_32_dll_socket]`.
Winsock 2 is the preferred choice for new code; see
[initializing Winsock](https://learn.microsoft.com/en-us/windows/win32/winsock/initializing-winsock).

## Try a Sample

From the published folder:

```powershell
.\zero.exe run samples\hello-windows-headers.zasm
```

It initializes common controls and prints the process ID without opening a window.
The [Windows header guide](../docs/windows-headers.md) explains its stack structure.

The unit tests discover every shipped `.zh` header and compile it individually
and in combination. On Windows they also check function spellings against the
installed x86 DLL export tables, without loading or invoking those DLL functions.
This checks the local Windows installation, not availability on every Windows version.

See [include syntax and search order](../docs/Zero-Assembly-Include-Files.md) and
the [language reference](../docs/zasm-reference.md). Add your own headers here or
pass extra search directories with `-I "my headers"`.
