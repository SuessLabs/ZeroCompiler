---
layout: default
title: "Hello, Zero Assembly"
---

# Hello, Zero Assembly

Section 1 — Intro · Chapter 1.5

[Previous](zasm-01-04-using-zero-ide-my-first-project.md) · [Contents](index.md) · [Next](zasm-01-06-keywords-and-directives.md)

Save this complete Windows x86 program as `hello.zasm`:

```asm
using "windows/console.zh"

  jmp start

message db 'Hello, Zero Assembly!', 0

start:
  push message
  call [puts]
  add esp, 4

  push 0
  call [exit]
```

Open PowerShell in the source folder. For the default installed compiler:

```powershell
& 'C:\Program Files\SuessLabs\Zero\bin\zero.exe' run hello.zasm
```

For a portable compiler, substitute its full executable path. The program prints:

```text
Hello, Zero Assembly!
```

`using` loads DLL declarations. `jmp start` skips inline data: the first emitted
byte is the entry point, and `start` is only a label. `db` emits text and an
ending zero. `push message` passes its address. `call [puts]` calls through an
imported function pointer. `puts` adds a newline. Its cdecl convention makes us
remove four argument bytes with `add esp, 4`. `exit(0)` ends the process.

Later snippets are **fragments** unless called complete programs. Insert them
into a complete program before termination; never execute inline data.

> **Exercise:** Change the greeting and run it again.

---

[Previous](zasm-01-04-using-zero-ide-my-first-project.md) · [Contents](index.md) · [Next](zasm-01-06-keywords-and-directives.md)
