# Zero assembly samples

Build the compiler with `.\build.bat` from the repository root. It copies these
samples into `publish\samples` alongside the native compiler.

From the `publish` folder, compile and run a sample:

```powershell
.\zcc.exe run samples\v002-hello-msgbox.zasm

.\zcc.exe run samples\v002-integer-math.zasm
```

| Sample | Behavior |
| --- | --- |
| `hello-console.zasm` | Prints `Hello from Zero!` using Windows console output. |
| `v002-hello-msgbox.zasm` | Opens a Windows MessageBox titled `Zero` displaying `Hello Zero!`. Click OK to exit. |
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
