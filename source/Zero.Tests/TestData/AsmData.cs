// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Tests.TestData;

public static class AsmData
{
  public const string MovCmpJz = """
    ; Zero v0.1 Minimal demo: sets EAX=42 and returns.
    start:
      mov eax, 42
      cmp eax, 42
      jz done
      mov eax, 1
    done:
      ret
    """;

  public const string ImportTable1 = """
    #IMPORT kernel32.dll, ExitProcess

    start:
      push 0
      call [IAT_ExitProcess]
    """;

  public const string ImportTableShortJumps = """
    ; Demonstrates import table + new instructions
    #IMPORT kernel32.dll, ExitProcess

    start:
      xor eax, eax          ; XOR r32, r32
      test eax, eax         ; TEST r32, r32 (sets ZF)
      jz8 .zero             ; short jump if zero (rel8)

      ; Unreachable in this tiny demo:
      mov eax, 1234
      jmp done

    .zero:
      ; LEA r32, [disp32] (here: load absolute address into EAX – for demo)
      lea eax, [0x401000]   ; not required—just to demo LEA disp32
      and eax, 0xFFFFFFFF   ; AND r32, imm32

    done:
      push 0
      call [IAT_ExitProcess]
    """;
}
