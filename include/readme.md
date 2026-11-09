# Zero Assembly Includes

Shared headers are organized by platform. The build ships this entire tree in
`publish/include` beside `zero.exe` and `zcc.exe`.

```text
include/
  windows/                 Working Windows x86 imports
    console.zh
    windows.zh
    kernel32.zh
    user32.zh
    comctl32.zh
    ...
  linux/                   Drafts for future ELF support
    libc/
      console.zh
      files.zh
      memory.zh
      process.zh
      sockets.zh
      threads.zh
      time.zh
      dynamic.zh
    syscalls/
      x64.zh               Comment-only ABI/number reference
      arm64.zh             Comment-only ABI/number reference
```

## Windows: Available Now

```asm
using "windows/console.zh"
using "windows/kernel32.zh"
using "windows/user32.zh"
```

See the [Windows header catalog](windows/readme.md) and
[Windows API guide](../docs/windows-headers.md) for all 17 Windows headers.
The former flat paths have moved: replace `using "console.zh"` with
`using "windows/console.zh"`. No compatibility copies are kept at the root.
Alternatively, `-I "path/to/include/windows"` lets an older program keep its
short filenames; explicit platform paths are recommended for new code.

## Linux: Future ELF Support

The [Linux header catalog](linux/readme.md) contains eight draft glibc import
headers and architecture-specific syscall references. They are source preparation,
not working Linux executable support. Do not use these drafts with the current
Windows PE compiler: a `.so` import does not make its output an ELF program.

## Search Paths and Other Platforms

The resolver searches the current working directory, each `-I` directory, and
`include` beside the compiler. Include the platform portion of a filename under
one of these roots; nested headers use the same search order.

Keep Linux, Windows, future macOS, and STM32 platform interfaces separate. Add
`macos` and `stm32` folders when their first headers are ready. Shared platform-neutral
headers can get their own folder when an actual common interface exists.

See [include syntax](../docs/Zero-Assembly-Include-Files.md) and the
[ZASM reference](../docs/zasm-reference.md).
