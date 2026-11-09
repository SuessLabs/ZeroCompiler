# Zero Assembly includes

These `.zh` files ship in `include` beside `zero.exe` and `zcc.exe`.

| Header | Imports |
| --- | --- |
| `console.zh` | `puts`, `printf`, `exit` from `msvcrt.dll` |
| `windows.zh` | `ExitProcess`, `GetModuleHandleA`, `MessageBoxA` |

Use `using "console.zh"` in a `.zasm` file. Headers expand at the directive's
position and are included once per compilation. These headers declare imports
for the compiler's current Windows x86 output.

Add your own headers here or pass additional directories with `-I "my headers"`.
See `samples/hello-using.zasm` and `docs/Zero-Assembly-Include-Files.md` in the repository.
