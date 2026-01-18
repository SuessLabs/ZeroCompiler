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
}
