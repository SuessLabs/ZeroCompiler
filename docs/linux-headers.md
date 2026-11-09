---
layout: default
title: Linux Headers and Future ELF Support
---

# Linux Headers and Future ELF Support

[Documentation home](index.md) · [Platform header catalog](https://github.com/SuessLabs/ZeroCompiler/blob/master/include/readme.md) ·
[Future targets](designs/Future-Formats-and-Targets.md)

Linux headers live under `include/linux`. These are drafts for future ELF work,
not implemented Linux compilation. The compiler still generates Windows x86 PE
executables; do not build a Linux program with the Windows backend simply because
it accepts the source's `#IMPORT` syntax.

## Library Calls

The initial profile targets **glibc 2.34 or newer**, using `libc.so.6`. This profile
does not cover musl or all Linux distributions. glibc 2.34 merged pthread and
dynamic-loader functions into libc; older profiles need different library choices.
See the [glibc release announcement](https://sourceware.org/pipermail/libc-announce/2021/000032.html).

| Header | Draft imports |
| --- | --- |
| `linux/libc/console.zh` | Text I/O: `puts`, `printf`, `snprintf`, `getchar`, `perror`. |
| `linux/libc/files.zh` | File descriptors, directory enumeration, and C streams. |
| `linux/libc/memory.zh` | Allocation, memory copying, and memory mappings. |
| `linux/libc/process.zh` | Process IDs, `fork`, `execve`, waiting, environment, termination. |
| `linux/libc/sockets.zh` | Sockets, TCP/UDP operations, address conversion/lookup. |
| `linux/libc/threads.zh` | POSIX threads, mutexes, and condition variables. |
| `linux/libc/time.zh` | Clocks, sleep, and date/time formatting. |
| `linux/libc/dynamic.zh` | `dlopen`, `dlsym`, `dlclose`, `dlerror`. |

Future programs will use explicit platform paths, for example:

```asm
; Planned Linux/ELF usage only.
using "linux/libc/console.zh"
```

The files contain proposed shared-library declarations, not startup code or
wrappers. They do not define structure layouts, constants, signatures, exported
variables, or how to pass arguments. Linux C ABIs differ from Windows x86 stdcall
and from the kernel's raw syscall ABI.

## Direct System Calls

`linux/syscalls/x64.zh` and `linux/syscalls/arm64.zh` are comment-only references
with selected syscall numbers and register conventions. No equates, executable
helpers, or supported syscall instructions are implied. Keeping the architectures
separate avoids confusing Linux x86-64 with AArch64, x32, i386, or 32-bit ARM.
See the Linux [syscall reference](https://man7.org/linux/man-pages/man2/syscall.2.html).

## Coming Soon

- ELF writing, process startup, and dynamic loader selection.
- Shared-library symbol resolution, relocations, and PLT/GOT handling or an equivalent mechanism.
- Target instructions, argument registers, stack alignment, and variadic-call rules.
- Target-sized data, constants, structures, and errno/runtime helpers.
- Linux export checks and runnable tests on each supported libc/architecture combination.
- Direct syscall instruction support and constant/helper definitions.

Current tests validate the draft source's format and include loading only. They
do not compile ELF, inspect Linux exports, or run Linux code. Windows header tests
continue to validate only `include/windows`.

The build ships both platform directories. Existing runnable samples use
`using "windows/..."`. For fuller notes, see the
[Linux header README](https://github.com/SuessLabs/ZeroCompiler/blob/master/include/linux/readme.md)
and [ZASM roadmap](zasm-coming-soon.md).
