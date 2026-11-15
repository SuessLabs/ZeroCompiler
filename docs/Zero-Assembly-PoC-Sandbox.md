# Zero - Assembly Compiler Proof of Concept Sandbox

> **Design proposal, not current syntax.** The directives and `PROC`/`ENDP`
> forms below are experimental ideas. Use the [current ZASM reference](zasm-reference.md)
> and [working examples](zasm-examples.md) for supported programs.
> [Back to documentation home](index.md).

The following document contains a sandbox before becoming a Mark-N revision (*i.e. MK-1*).

## Compiler Switches

### Defining Sections

Zero Assembly:

```asm
#.586              ; Target processor.  Use instructions for Pentium class machines
#.MODEL FLAT, C    ; Use the flat memory model. Use C calling conventions
#.STACK            ; Define a stack segment of 1KB (Not required for this example)
#.DATA             ; Create a near data segment.  Local variables are declared after
                   ; this directive (Not required for this example)
#.CODE             ; Indicates the start of a code segment.

clear PROC
  xor eax, eax
  xor ebx, ebx
  ret
clear ENDP
END
```
