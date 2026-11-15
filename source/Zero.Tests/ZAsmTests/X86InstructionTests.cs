// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class X86InstructionTests
{
  [TestMethod]
  [OSCondition(OperatingSystems.Windows)]
  public void CallbackReadsItsArgumentAndRestoresTheCallerStack()
  {
    var source = """
      #IMPORT kernel32.dll, ExitProcess
        push esp
        pop ebx
        push 37
        call callback
        push esp
        pop ecx
        cmp ecx, ebx
        jnz failed
        push eax
        call [IAT_ExitProcess]
      failed:
        push 1
        call [IAT_ExitProcess]
      callback:
        mov eax, [esp + 4]
        ret 4
      """;

    var bytes = new Assembler().Assemble(source.Split('\n'));

    Assert.IsNotNull(bytes);
    var executable = Path.Combine(Path.GetTempPath(), $"zero-callback-{Guid.NewGuid():N}.exe");

    try
    {
      File.WriteAllBytes(executable, bytes);

      using var process = Process.Start(new ProcessStartInfo(executable)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
      });

      Assert.IsNotNull(process);

      if (!process.WaitForExit(10000))
      {
        process.Kill();
        process.WaitForExit();
        Assert.Fail("The stdcall test timed out.");
      }

      Assert.AreEqual(37, process.ExitCode);
    }
    finally
    {
      File.Delete(executable);
    }
  }

  [TestMethod]
  [DataRow("mov eax, [ebp + 12]", "8B850C000000")]
  [DataRow("mov edx, [esp + 4]", "8B942404000000")]
  [DataRow("mov ecx, [eax]", "8B8800000000")]
  [DataRow("mov eax, [0x401000]", "8B0500104000")]
  [DataRow("ret", "C3")]
  [DataRow("ret 0", "C20000")]
  [DataRow("ret 16", "C21000")]
  [DataRow("ret 65535", "C2FFFF")]
  public void MemoryLoadsAndReturnsHaveExactEncodings(string instruction, string hex)
  {
    var bytes = new Assembler().Assemble([instruction, "ret"]);
    Assert.IsNotNull(bytes);

    using var stream = new MemoryStream(bytes);
    using var reader = new PEReader(stream);

    var text = reader.PEHeaders.SectionHeaders[0];
    var content = reader.GetSectionData(text.VirtualAddress).GetContent(0, text.VirtualSize);
    var actual = new byte[content.Length];
    content.CopyTo(actual);

    Assert.AreSequenceEqual(Convert.FromHexString(hex + "C3"), actual);
  }

  [TestMethod]
  [DataRow("mov ax, [esp]")]
  [DataRow("mov rax, [esp]")]
  [DataRow("mov eax, [ax]")]
  [DataRow("mov eax, [rax + 4]")]
  [DataRow("ret -1")]
  [DataRow("ret 65536")]
  [DataRow("ret 1, 2")]
  public void UnsupportedWidthsAndInvalidStackCleanupAreRejected(string instruction)
  {
    Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble([instruction]));
  }
}
