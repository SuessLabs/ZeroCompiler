# Zero assembly samples

Build the compiler with `.\build.bat` from the repository root. It copies these
samples into `publish\samples` alongside the native compiler.

From the `publish` folder, compile and run a sample:

Windows samples are in `samples/Windows`; Linux samples are in `samples/Linux`.

```powershell
.\zero.exe run samples\Windows\win-ui-01-msgbox.zasm

.\zero.exe run samples\Windows\win-cli-02-integer-math.zasm
```

| Sample | Behavior |
| --- | --- |
| `win-cli-03-console-write.zasm` | Prints `Hello from Zero!` using Windows console output. |
| `win-cli-05-string-concat.zasm` | Prints `Hello` and `Zero Assembly!` on separate lines, then joins them with a space and prints `Hello Zero Assembly!`. |
| `win-cli-02-console-printf-db-vars.zasm` | Declares a named string with `message db 'Hello, World!', 0x0A, 0` and prints it. |
| `win-ui-01-msgbox.zasm` | Opens a Windows MessageBox titled `Zero` displaying `Hello Zero!`. Click OK to exit. |
| `win-cli-01-hello-using.zasm` | Loads the shipped `windows/console.zh` header and prints `Hello Zero!`. |
| `win-ui-03-init-common-ctrls.zasm` | Loads console, Kernel32, and ComCtl32 headers, initializes common controls, and prints its process ID. |
| `linux64-cli-01-hello.zasm` | Declares `#TARGET linux-x64`, creates a static ELF64 executable, and prints `Hello Zero from Linux x86-64!` through Linux syscalls. |
| `win-cli-06-timer.zasm` | Windows PE32 console timer: prints an incrementing unsigned 32-bit counter every second using a periodic waitable timer. |
| `linux64-cli-06-timer.zasm` | Linux ELF64 console timer: prints an incrementing unsigned 32-bit counter every second using `timerfd` syscalls, without libc. |
| `win-ui-02-controls.zasm` | Opens a native Windows x86 window with a label, an editable `Hello Zero!` textbox, and a button that displays the current text. |
| [win-ui-10-notepad.zasm](Windows/win-ui-10-notepad.zasm) | Complete resizable text editor with Open, Save, Save As, Close, native file dialogs, and unsaved-change prompts. See [the Section 4 tutorial](../docs/zasm/zasm-04-01-notepad-project-setup.md) for all three standalone stages. |
| `win-cli-02-integer-math.zasm` | Calculates `7 + 5` and `6 * 7`, then prints the computed results. |

`zero run <source> [output.exe]` builds the source, then runs the executable in the
current terminal and waits for it to exit. It returns the program's exit code.
The output defaults to the source path with an `.exe` extension, so the examples
above create their executables inside `samples`. To choose another output path:

```powershell
.\zero.exe run samples\Windows\win-cli-03-console-write.zasm win-cli-03-console-write.exe
```

Omit `run` to compile without executing. Compilation failures prevent execution,
including when an executable from an earlier successful build already exists.

Run the string-concatenation sample with:

```powershell
.\zero.exe run samples\Windows\win-cli-05-string-concat.zasm
```

It uses `strcpy` and `strcat` from Windows' system `msvcrt.dll` to build the final
string in a writable 64-byte stack buffer. `puts` prints each line, and `exit`
flushes output. The joined string is constructed at runtime from the two source
strings and a space; its fixed contents require 21 bytes including the final zero.

The arithmetic sample produces:

```text
7 + 5 = 12
6 * 7 = 42
```

Multiplication uses repeated addition because the assembler does not yet emit
`MUL`. The sample uses Windows' system `msvcrt.dll` for `printf` and `exit`;
`exit` flushes buffered console output. Both results are calculated at runtime.
If you change the operands, update the descriptive numbers in the format string
as well. Strings use numeric ASCII `DB` operands; terminating zero bytes are
required for MessageBox and printf strings.

Most applications target Windows x86. `linux64-cli-01-hello.zasm` targets Linux x86-64
and must run in Linux/WSL; it can be compiled on Windows. See the
[Linux x64 guide](../docs/linux64.md) for commands and supported encodings.
The Windows compiler backend currently writes the console
subsystem for all executables, so the MessageBox sample can also open a console
window when launched from Explorer. Run console samples from an existing terminal
to keep their output visible.

## One-second timers

Both timer samples print `1`, `2`, `3`, ... on separate lines, starting after one
second. Press **Ctrl+C** to stop. The counter is an unsigned 32-bit value
(`uint32_t`, or DWORD), wrapping to zero after `4294967295`; a QWORD is 64 bits.

From `publish`, run the Windows x86 PE32 sample:

```powershell
.\zero.exe run samples\Windows\win-cli-06-timer.zasm
```

It uses an auto-reset waitable timer with an initial delay of one second and a
1000 ms period. The main thread blocks in `WaitForSingleObject`, increments the
counter, prints it with `%u`, and flushes output each tick. It does not busy-wait.
Windows timer signals can coalesce if the program is delayed processing output.

Compile the Linux x86-64 ELF64 sample from `publish` on Windows:

```powershell
.\zero.exe samples\Linux\linux64-cli-06-timer.zasm linux64-cli-06-timer
```

Copy the executable to Linux/WSL, then run:

```sh
chmod +x linux64-cli-06-timer
./linux64-cli-06-timer
```

The Linux sample uses a periodic `CLOCK_MONOTONIC` timer through `timerfd_create`
and `timerfd_settime`. Blocking reads report elapsed ticks, including ticks missed
while delayed; it prints one increment for each. Decimal conversion uses repeated
subtraction because Zero does not yet support division. All writable storage is
on the stack, and no libc or dynamic loader is required. Timer scheduling and
console output are subject to normal operating-system delays on both platforms.

## Calling imported functions

Shared headers can declare imports for you:

```asm
using "windows/console.zh" ; Imports puts, printf, and exit
```

The build ships `windows/console.zh` and `windows/windows.zh` in `publish/include` beside both
compiler executable. Run `.\zero.exe run samples\Windows\win-cli-01-hello-using.zasm` from `publish`
to try it. Add your own search directories with repeated `-I "headers"` options.
See [Using included files](../docs/zasm/ZASM-Include-Files.md).
For the full DLL-specific catalog, see [Windows API headers](../docs/windows-headers.md).

Import Address Table lookup references imported functions and can be called by their names in brackets:

```asm
#IMPORT kernel32.dll, ExitProcess

push 0
call [ExitProcess]
```

`call [ExitProcess]` reads the function address from the import table.

The older `call [IAT_ExitProcess]` and `call [__imp_ExitProcess]` forms still work and generate
the same machine code. Calls to your own procedures keep their direct syntax,
such as `call window_proc`, without brackets.

Local labels must not reuse imported function names. If two DLLs import the same
function name, use a DLL-qualified alias such as `call [IAT_kernel32_dll_ExitProcess]`
to select its library explicitly.

## Named byte declarations

```asm
message db 'Hello, World!', 0x0A, 0
```

`message` names the address of these bytes. `push message` passes that address;
`mov eax, message` or `lea eax, [message]` can also load it. A colon is optional
for byte declarations: `message: db 'Hello, World!', 0x0A, 0` works too.

Quoted strings emit UTF-8 bytes and can use single or double quotes. Commas,
semicolons, and colons inside a string are ordinary text. Double a matching quote
to include it, as in `'It''s Zero'`. Backslashes are literal; use numeric bytes
such as `0x0A` for a newline and `0` for a terminating zero. No ending zero is
added automatically.

Data is currently stored inline with the code in the read-only `.text` section.
When declarations come before instructions, use `jmp start` to skip the data, as
shown in `win-cli-02-console-printf-db-vars.zasm`. These names identify stored bytes; they do not yet
provide writable variables or separate data sections.

```powershell
.\zero.exe run samples\Windows\win-cli-02-console-printf-db-vars.zasm
```
