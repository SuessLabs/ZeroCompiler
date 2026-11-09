# Linux Headers: Future ELF Support

These files prepare an interface catalog for a future Linux backend. **They cannot
produce runnable Linux applications with the current Windows PE compiler.** The
`.zh` format and `#IMPORT` notation are shared source conventions; the future ELF
backend must still implement shared-library imports and target calling conventions.

## Scope: glibc 2.34 or Newer

The draft libc headers import `libc.so.6` and intentionally target glibc 2.34+.
They are not a universal Linux ABI profile. musl, older glibc versions, and other
libc implementations need separate library/ABI decisions.

In glibc 2.34, pthread and dynamic-loader functionality was integrated into libc.
That is why `threads.zh` and `dynamic.zh` use `libc.so.6`, rather than always
importing `libpthread.so.0` and `libdl.so.2`.
See the [glibc 2.34 announcement](https://sourceware.org/pipermail/libc-announce/2021/000032.html)
and the [Linux libc overview](https://man7.org/linux/man-pages/man7/libc.7.html).

## Draft Library Headers

| Source path under `include` | Area | Example functions |
| --- | --- | --- |
| `linux/libc/console.zh` | Text input/output | `puts`, `printf`, `snprintf`, `getchar`, `perror` |
| `linux/libc/files.zh` | Files, directories, streams | `open`, `read`, `write`, `close`, `opendir`, `fopen` |
| `linux/libc/memory.zh` | Allocations, buffers, mappings | `malloc`, `free`, `memcpy`, `mmap`, `munmap` |
| `linux/libc/process.zh` | Processes, environment, exit | `getpid`, `fork`, `execve`, `waitpid`, `exit` |
| `linux/libc/sockets.zh` | Networking | `socket`, `bind`, `connect`, `send`, `recv`, `getaddrinfo` |
| `linux/libc/threads.zh` | POSIX threads and synchronization | `pthread_create`, `pthread_join`, mutex/condition APIs |
| `linux/libc/time.zh` | Time and delays | `clock_gettime`, `nanosleep`, `time`, `strftime` |
| `linux/libc/dynamic.zh` | Shared-library loading | `dlopen`, `dlsym`, `dlclose`, `dlerror` |

Future source inclusion will use a platform-qualified path:

```asm
; Future Linux/ELF usage, not a runnable Windows example.
using "linux/libc/console.zh"
```

Only function imports are declared. Constants, structure layouts, exported data
such as `stdout`, pointer-sized types, errno access, and signatures remain to be
defined. A function name alone does not specify how to pass its arguments.

## Direct Syscall References

| File | Architecture | Status |
| --- | --- | --- |
| `syscalls/x64.zh` | Native Linux x86-64, excluding x32/i386 | Comment-only syscall numbers and register conventions. |
| `syscalls/arm64.zh` | Linux AArch64 | Comment-only syscall numbers and register conventions. |

These files deliberately define no active constants or macros until ZASM supports
that syntax. They do not import libc and are not callable wrappers. Syscalls have
architecture-specific instructions, numbers, argument registers, and raw error
returns. Do not substitute a Windows calling convention or reuse one architecture's
numbers on another. See [syscall conventions](https://man7.org/linux/man-pages/man2/syscall.2.html).

## Work Needed Before These Become Usable

- ELF output, program startup, and an appropriate dynamic interpreter.
- Shared-library dependencies, relocations, symbol/version resolution, and PLT/GOT handling or an equivalent import mechanism.
- Target-specific machine code, Linux C ABI calls, stack alignment, and variadic-call rules.
- Matching integer/pointer widths, structure layouts, constants, and callback support.
- Tests on actual supported Linux/libc/architecture combinations.
- Syscall instruction encodings, equates/macros, and checked syscall helpers if direct syscalls are implemented.

Windows tests only check that the draft declarations load and are well formed;
they do not validate Linux exports, linking, or runtime behavior. The build ships
the drafts for reference, while runnable samples continue to use Windows headers.

[Platform catalog](../readme.md) · [Linux documentation](../../docs/linux-headers.md)
