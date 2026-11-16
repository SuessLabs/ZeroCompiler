---
layout: default
title: "Appendix II — ASCII Character Set"
---

# Appendix II — ASCII Character Set

Appendices

[Previous](zasm-07-01-mnemonic-lookup.md) · [Contents](index.md) · [Back to contents](index.md)

ASCII defines values 0–127. Values 0–31 and 127 are controls; 32 is space.
Values 128–255 are not a universal extended ASCII set. ZASM strings use UTF-8,
where the ASCII range has the same byte values.

Useful values: NUL (0, string terminator), HT (9, tab), LF (10, line feed), CR
(13, carriage return), and space (32). Windows text commonly uses CR followed
by LF. A string's ending zero is separate from its line ending.

```asm
message db 'Hello', 13, 10, 0
```

Place this data behind a jump. `puts` adds its own newline; omit the explicit
CR/LF if you want only the line ending supplied by `puts`.

## Complete 7-bit table

| Decimal | Hex | Character or control |
| --- | --- | --- |
| 0 | 00 | NUL |
| 1 | 01 | SOH |
| 2 | 02 | STX |
| 3 | 03 | ETX |
| 4 | 04 | EOT |
| 5 | 05 | ENQ |
| 6 | 06 | ACK |
| 7 | 07 | BEL |
| 8 | 08 | BS |
| 9 | 09 | HT |
| 10 | 0A | LF |
| 11 | 0B | VT |
| 12 | 0C | FF |
| 13 | 0D | CR |
| 14 | 0E | SO |
| 15 | 0F | SI |
| 16 | 10 | DLE |
| 17 | 11 | DC1 |
| 18 | 12 | DC2 |
| 19 | 13 | DC3 |
| 20 | 14 | DC4 |
| 21 | 15 | NAK |
| 22 | 16 | SYN |
| 23 | 17 | ETB |
| 24 | 18 | CAN |
| 25 | 19 | EM |
| 26 | 1A | SUB |
| 27 | 1B | ESC |
| 28 | 1C | FS |
| 29 | 1D | GS |
| 30 | 1E | RS |
| 31 | 1F | US |
| 32 | 20 | Space |
| 33 | 21 | &#33; |
| 34 | 22 | &#34; |
| 35 | 23 | &#35; |
| 36 | 24 | &#36; |
| 37 | 25 | &#37; |
| 38 | 26 | &#38; |
| 39 | 27 | &#39; |
| 40 | 28 | &#40; |
| 41 | 29 | &#41; |
| 42 | 2A | &#42; |
| 43 | 2B | &#43; |
| 44 | 2C | &#44; |
| 45 | 2D | &#45; |
| 46 | 2E | &#46; |
| 47 | 2F | &#47; |
| 48 | 30 | &#48; |
| 49 | 31 | &#49; |
| 50 | 32 | &#50; |
| 51 | 33 | &#51; |
| 52 | 34 | &#52; |
| 53 | 35 | &#53; |
| 54 | 36 | &#54; |
| 55 | 37 | &#55; |
| 56 | 38 | &#56; |
| 57 | 39 | &#57; |
| 58 | 3A | &#58; |
| 59 | 3B | &#59; |
| 60 | 3C | &#60; |
| 61 | 3D | &#61; |
| 62 | 3E | &#62; |
| 63 | 3F | &#63; |
| 64 | 40 | &#64; |
| 65 | 41 | &#65; |
| 66 | 42 | &#66; |
| 67 | 43 | &#67; |
| 68 | 44 | &#68; |
| 69 | 45 | &#69; |
| 70 | 46 | &#70; |
| 71 | 47 | &#71; |
| 72 | 48 | &#72; |
| 73 | 49 | &#73; |
| 74 | 4A | &#74; |
| 75 | 4B | &#75; |
| 76 | 4C | &#76; |
| 77 | 4D | &#77; |
| 78 | 4E | &#78; |
| 79 | 4F | &#79; |
| 80 | 50 | &#80; |
| 81 | 51 | &#81; |
| 82 | 52 | &#82; |
| 83 | 53 | &#83; |
| 84 | 54 | &#84; |
| 85 | 55 | &#85; |
| 86 | 56 | &#86; |
| 87 | 57 | &#87; |
| 88 | 58 | &#88; |
| 89 | 59 | &#89; |
| 90 | 5A | &#90; |
| 91 | 5B | &#91; |
| 92 | 5C | &#92; |
| 93 | 5D | &#93; |
| 94 | 5E | &#94; |
| 95 | 5F | &#95; |
| 96 | 60 | &#96; |
| 97 | 61 | &#97; |
| 98 | 62 | &#98; |
| 99 | 63 | &#99; |
| 100 | 64 | &#100; |
| 101 | 65 | &#101; |
| 102 | 66 | &#102; |
| 103 | 67 | &#103; |
| 104 | 68 | &#104; |
| 105 | 69 | &#105; |
| 106 | 6A | &#106; |
| 107 | 6B | &#107; |
| 108 | 6C | &#108; |
| 109 | 6D | &#109; |
| 110 | 6E | &#110; |
| 111 | 6F | &#111; |
| 112 | 70 | &#112; |
| 113 | 71 | &#113; |
| 114 | 72 | &#114; |
| 115 | 73 | &#115; |
| 116 | 74 | &#116; |
| 117 | 75 | &#117; |
| 118 | 76 | &#118; |
| 119 | 77 | &#119; |
| 120 | 78 | &#120; |
| 121 | 79 | &#121; |
| 122 | 7A | &#122; |
| 123 | 7B | &#123; |
| 124 | 7C | &#124; |
| 125 | 7D | &#125; |
| 126 | 7E | &#126; |
| 127 | 7F | DEL |

---

[Previous](zasm-07-01-mnemonic-lookup.md) · [Contents](index.md) · [Back to contents](index.md)
