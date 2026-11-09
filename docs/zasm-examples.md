---
layout: default
title: ZASM Basic Examples
---

# ZASM Basic Examples

[Documentation home](index.md) · [Language reference](zasm-reference.md)

Save each complete program under the suggested filename. Run the commands from
the extracted compiler folder, keeping `include` beside `zero.exe` and `zcc.exe`.
All examples target Windows x86 and use only the currently implemented forms.

## Print Text

Save as `hello-text.zasm`:

```asm
using "windows/console.zh"

  jmp start
message db 'Hello Zero!', 0

start:
  push message
  call [puts]
  add esp, 4

  push 0
  call [exit]
```

```powershell
.\zero.exe run hello-text.zasm
```

Output:

```text
Hello Zero!
```

`DB` stores the string and terminating zero. `puts` prints it and adds a newline.
`add esp, 4` removes the argument after the cdecl call.

## Add Two Integers

Save as `add-integers.zasm`:

```asm
using "windows/console.zh"

  jmp start
format db '7 + 5 = %d', 10, 0

start:
  mov eax, 7
  add eax, 5

  push eax             ; Value for %d.
  push format          ; First argument: format string.
  call [printf]
  add esp, 8           ; Remove both 4-byte arguments.

  push 0
  call [exit]
```

```powershell
.\zero.exe run add-integers.zasm
```

Output:

```text
7 + 5 = 12
```

The addition happens in EAX. `printf` formats the calculated value as decimal
text; it does not evaluate the expression in the string. If you change the numbers,
update the descriptive text too. The shipped
[arithmetic sample](https://github.com/SuessLabs/ZeroCompiler/blob/master/samples/v002-integer-math.zasm)
also multiplies using repeated addition because `MUL` is not implemented yet.

## Show a Windows MessageBox

Save as `hello-messagebox.zasm`:

```asm
using "windows/windows.zh"

  jmp start
message db 'Hello Zero!', 0
caption db 'Zero', 0

start:
  push 0               ; MB_OK.
  push caption
  push message
  push 0               ; No parent window.
  call [MessageBoxA]

  push 0
  call [ExitProcess]
```

```powershell
.\zero.exe run hello-messagebox.zasm
```

A dialog titled **Zero** displays **Hello Zero!**. Click **OK** to exit.
Arguments are pushed right to left. These Win32 functions clean up their own
arguments, so no `add esp, ...` follows the calls. The executable still uses the
console subsystem; this example does not select a separate GUI subsystem.

## Next Steps

- [Include guide](Zero-Assembly-Include-Files.md): put shared source in `.zh` files and use custom `-I` paths.
- [String concatenation tutorial](tutorial-zasm-concat.md): print separate strings, then join them with a space.
- [Sample guide](https://github.com/SuessLabs/ZeroCompiler/blob/master/samples/readme.md): explore the full sample collection, including editable Windows controls.
- [Instruction reference](zasm-reference.md#instruction-reference): choose supported operand forms.

## Coming Soon

- **Loops:** explain comparison flags and a counted loop step by step.
- **Procedures:** introduce local calls, stack arguments, return values, and register preservation.
- **Writable buffers:** walk through stack allocation and safe buffer sizes.
- **Windows controls:** explain message handling, callbacks, and the controls sample.
- **Higher-level Zero:** add the first runnable `.zsrc` tutorial when compilation is implemented.
