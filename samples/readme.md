# Zero assembly samples

Build the compiler with `.\build.bat` from the repository root. It copies these
samples into `publish\samples` alongside the native compiler.

From the `publish` folder, compile and run a sample:

```powershell
.\zcc.exe run samples\hello-msgbox.zasm

.\zcc.exe run samples\v002-integer-math.zasm
```

| Sample | Behavior |
| --- | --- |
| `hello-console.zasm` | Prints `Hello from Zero!` using Windows console output. |
| `hello-concat.zasm` | Prints `Hello` and `Zero Assembly!` on separate lines, then joins them with a space and prints `Hello Zero Assembly!`. |
| `hello-db-vars.zasm` | Declares a named string with `message db 'Hello, World!', 0x0A, 0` and prints it. |
| `hello-msgbox.zasm` | Opens a Windows MessageBox titled `Zero` displaying `Hello Zero!`. Click OK to exit. |
| `hello-using.zasm` | Loads the shipped `console.zh` header and prints `Hello Zero!`. |
| `hello-windows-headers.zasm` | Loads console, Kernel32, and ComCtl32 headers, initializes common controls, and prints its process ID. |
| `hello-win86-controls.zasm` | Opens a native Windows x86 window with a label, an editable `Hello Zero!` textbox, and a button that displays the current text. |
| `v002-integer-math.zasm` | Calculates `7 + 5` and `6 * 7`, then prints the computed results. |

`zcc run <source> [output.exe]` builds the source, then runs the executable in the
current terminal and waits for it to exit. It returns the program's exit code.
The output defaults to the source path with an `.exe` extension, so the examples
above create their executables inside `samples`. To choose another output path:

```powershell
.\zcc.exe run samples\hello-console.zasm hello-console.exe
```

Omit `run` to compile without executing. Compilation failures prevent execution,
including when an executable from an earlier successful build already exists.

Run the string-concatenation sample with:

```powershell
.\zcc.exe run samples\hello-concat.zasm
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

These applications target Windows x86. The compiler currently writes the console
subsystem for all executables, so the MessageBox sample can also open a console
window when launched from Explorer. Run console samples from an existing terminal
to keep their output visible.

## Calling imported functions

Shared headers can declare imports for you:

```asm
using "console.zh" ; Imports puts, printf, and exit
```

The build ships `console.zh` and `windows.zh` in `publish/include` beside both
compiler executables. Run `.\zcc.exe run samples\hello-using.zasm` from `publish`
to try it. Add your own search directories with repeated `-I "headers"` options.
See [Using included files](../docs/Zero-Assembly-Include-Files.md).
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
shown in `hello-db-vars.zasm`. These names identify stored bytes; they do not yet
provide writable variables or separate data sections.

```powershell
.\zcc.exe run samples\hello-db-vars.zasm
```
