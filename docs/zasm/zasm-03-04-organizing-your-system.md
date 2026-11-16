---
layout: default
title: "Organizing Your System"
---

# Organizing Your System

Section 3 — Level-Up Learning · Chapter 3.4

[Previous](zasm-03-03-working-with-strings.md) · [Contents](index.md) · [Next](zasm-03-05-arithmetic-operations.md)

Start with a small layout:

```text
MyProgram/
  main.zasm
  headers/
    imports.zh
  notes/
```

Put shared import declarations in `headers/imports.zh`. Include them with
`using "headers/imports.zh"`. Includes compose source; they do not provide
separate object-file compilation or linking.

The loader searches the current working directory, repeatable `-I` directories
in order, `include` beside the compiler, then `../include` for a compiler
installed in `bin`. Nested includes use the same search order; do not assume
they resolve relative to their containing header. Run terminal builds from a
consistent directory. The IDE uses the source folder as the working directory.

A file is included once; cycles are diagnosed. Use unique labels across the
combined source. Included code and data affect the first emitted byte and
control flow, even though import declarations emit no instructions.

See [include-file details](ZASM-Include-Files.md). Multi-file linking, namespaces,
and a project manifest are future work.

**Exercise:** Move only the hello program's import declarations into a local
header and verify that the program behaves the same way.

---

[Previous](zasm-03-03-working-with-strings.md) · [Contents](index.md) · [Next](zasm-03-05-arithmetic-operations.md)
