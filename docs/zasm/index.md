---
layout: default
title: Zero Assembly Programmers Guide
---

# Zero Assembly Programmer's Guide

[Documentation library](../index.md) · [Start reading](zasm-01-01-what-is-zero-assembly.md)

Learn Zero Assembly from your first instruction to a small Windows editing
application. Read in order or revisit individual topics through the contents.
Every chapter links to its neighbors and back to this page.

## How to use this book

The main path uses Windows x86 on a Windows x64 computer. Linux x64 examples
are labeled separately. Fragments require surrounding code; chapters explicitly
identify complete programs. Commands assume the stated working directory and
compiler layout.

Current compiler limits appear beside the affected lessons. Structure declarations,
full instruction-set coverage, and advanced Notepad features are not implied by
a chapter title. The Notepad project starts with a working sample, builds a
multiline editor, then provides a workshop for persistence and later features.

Sections are consecutive in reading order: Notepad is Section 4, Compiler and
Debugging is Section 5, and Architecture is Section 6. Section 7 holds the two
appendices. Filenames follow `zasm-{section}-{chapter}-{title}.md`, using two-digit
numbers and lowercase, hyphenated titles. For example,
`zasm-02-01-moving-numbers.md` is Chapter 2.1.

## Contents

### Section 1 — Intro

1. [What Is Zero Assembly?](zasm-01-01-what-is-zero-assembly.md)
2. [Why Learn Assembly Today?](zasm-01-02-why-learn-assembly-today.md)
3. [Installing Zero.IDE](zasm-01-03-installing-zero-ide.md)
4. [Using Zero.IDE — My First Project](zasm-01-04-using-zero-ide-my-first-project.md)
5. [Hello, Zero Assembly](zasm-01-05-hello-zero-assembly.md)
6. [Zero Assembly Keywords and Directives](zasm-01-06-keywords-and-directives.md)

### Section 2 — Basics

1. [Moving Numbers](zasm-02-01-moving-numbers.md)
2. [Performing Jumps](zasm-02-02-performing-jumps.md)
3. [Storing Data and Printing](zasm-02-03-storing-data-and-printing.md)
4. [Boolean Algebra](zasm-02-04-boolean-algebra.md)
5. [Basic Variables](zasm-02-05-basic-variables.md)

### Section 3 — Level-Up Learning

1. [Calling External Library Functions](zasm-03-01-importing-and-calling-external-library.md)
2. [Creating Structs](zasm-03-02-creating-structs.md)
3. [Working with Strings](zasm-03-03-working-with-strings.md)
4. [Organizing Your System](zasm-03-04-organizing-your-system.md)
5. [Arithmetic Operations](zasm-03-05-arithmetic-operations.md)
6. [Performing Bit Manipulations](zasm-03-06-performing-bit-manipulations.md)
7. [Creating Functions](zasm-03-07-creating-functions.md)

### Section 4 — Creating a Notepad Project

1. [Notepad Project — Set Up the Workspace](zasm-04-01-notepad-project-setup.md)
2. [Notepad Project — Create the Window](zasm-04-02-notepad-create-the-window.md)
3. [Notepad Project — Add the Text Editor](zasm-04-03-notepad-add-the-text-editor.md)
4. [Notepad Project — Open and Save Files](zasm-04-04-notepad-open-and-save-files.md)
5. [Notepad Project — Test and Extend](zasm-04-05-notepad-testing-and-next-steps.md)

### Section 5 — Compiler and Debugging

1. [Zero Compiler (Zero.CLI)](zasm-05-01-zero-cli-compiler.md)
2. [Compiler Switches](zasm-05-02-compiler-switches.md)
3. [Zero Debugger (Zero.DBG)](zasm-05-03-zero-debugger.md)

### Section 6 — Architecture

1. [The x86 Instruction Set](zasm-06-01-x86-instruction-set.md)
2. [The x64 Instruction Set](zasm-06-02-x64-instruction-set.md)
3. [What Is Windows PE?](zasm-06-03-what-is-windows-pe.md)
4. [What Is ELF?](zasm-06-04-what-is-elf.md)
5. [What Is ARM?](zasm-06-05-what-is-arm.md)

### Appendices

1. [Appendix I — Mnemonic Lookup](zasm-07-01-mnemonic-lookup.md)
2. [Appendix II — ASCII Character Set](zasm-07-02-ascii-character-set.md)

## Reference shelf

Existing reference filenames remain intact for bookmarks and external links.
Use the book for learning and these pages for detailed lookup:

- [Language reference](ZASM-Reference.md): syntax, operands, imports, and limitations.
- [Mnemonic inventory](ZASM-Mnemonics.md): implemented and missing forms by target.
- [Include files](ZASM-Include-Files.md): header loading and search rules.
- [Complete examples](ZASM-Examples.md) and [assembly overview](ZASM-Intro.md).
- [Linux x64](../linux64.md) and [Windows headers](../windows-headers.md).
- [Feature roadmap](ZASM-Coming-Soon.md) and [syntax proposals](ZASM-vNext-Sandbox.md).

## Companion books

- [Zero and Zero.CLI](../zero/index.md) — planned language and compiler guide.
- [Zero.IDE](../ide/index.md) — planned IDE user guide.
- [Zero.DBG](../dbg/index.md) — planned debugger user guide; existing technical docs remain available.
