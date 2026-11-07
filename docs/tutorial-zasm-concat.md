# Joining strings in Zero Assembly

[hello-concat.zasm](../samples/hello-concat.zasm) prints two pieces of text, then
joins them with a space. Joining strings is called **concatenation**.

## 1. Import helper functions

```asm
#IMPORT msvcrt.dll, puts, strcpy, strcat, exit
```

This loads functions from a Windows library:

| Function | Job |
| --- | --- |
| `puts` | Print text and move to the next line. |
| `strcpy` | Copy text into another memory area. |
| `strcat` | Add text to the end of an existing string. |
| `exit` | Finish the program. |

## 2. Make room for the joined text

```asm
sub esp, 64
lea esi, [esp]
```

This reserves 64 bytes on the **stack**, a temporary storage area. `esi` is a
CPU register: a small storage location holding the buffer's address.

The final string needs 21 bytes, including its ending marker.

## 3. Store the words

```asm
call hello_ready
hello_text:
  db 72, 101, 108, 108, 111, 0
hello_ready:
  pop edi
```

`db` stores bytes. Here, the numbers are ASCII codes spelling `Hello`.
The final `0` marks the end; it is not the character `0`.

`call` jumps over the text and saves its address on the stack. `pop edi` takes
that address into `edi`. The program repeats this trick for `Zero Assembly!`,
storing its address in `ebx`.

## 4. Print each string

```asm
push edi
call [IAT_puts]
add esp, 4
```

`push` supplies the string's address to `puts`. `IAT_puts` points to the imported
function. Afterward, `add esp, 4` removes that argument from the stack.
The program repeats this for the second string.

## 5. Build and print the combined string

The next calls change the buffer in three steps:

```text
strcpy → Hello
strcat → Hello[space]
strcat → Hello Zero Assembly!
```

For these two-argument functions, the source address is pushed first, then the
destination address. `add esp, 8` removes both arguments afterward.

The program prints the buffer with `puts`, then calls `exit` with `0` to signal
success. The joined sentence is built while the program runs.

## Try it

From the repository root, after building the compiler:

```powershell
./zero.exe run samples/hello-concat.zasm
```

Output:

```text
Hello
Zero Assembly!
Hello Zero Assembly!
```

## Reference

A **mnemonic** is a short name for a CPU instruction. This table covers the
instructions and assembler directives used in the sample.

| Mnemonic | Name | What it does | Example from the sample |
| --- | --- | --- | --- |
| `sub`   | Subtract | Subtracts a number. Here, moving the stack pointer down reserves 64 bytes of storage. | `sub esp, 64` |
| `lea`   | Load effective address | Saves a memory address in a register. It does not read the text stored there. | `lea esi, [esp]` |
| `call`  | Call a function or label | Saves the return address on the stack, then jumps to the requested code. | `call hello_ready` |
| `pop`   | Take from the stack | Takes the top value off the stack and puts it in a register. | `pop edi` |
| `push`  | Place on the stack | Places a value on the stack. This sample uses it to pass function arguments. | `push edi` |
| `add`   | Add | Adds a number. Here, moving the stack pointer up removes a used argument. | `add esp, 4` |
| `db`    | Define byte | Stores raw bytes in the program, such as ASCII letters and an ending zero. This is an assembler directive. | `db 32, 0` stores a space and its ending zero. |
| `#IMPORT` | Import functions | Declares which functions the program needs from a DLL. This is an assembler directive. | `#IMPORT msvcrt.dll, puts, strcpy, strcat, exit` |

Names ending in `:`, such as `hello_ready:`, are **labels** that name locations
in the program. Names such as `esp`, `esi`, and `edi` are **registers**, not
instructions. `esp` holds the address of the top of the stack.
