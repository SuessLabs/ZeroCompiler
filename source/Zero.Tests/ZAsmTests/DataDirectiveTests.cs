// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class DataDirectiveTests
{
  [TestMethod]
  [DataRow("message db 'Hello, World!', 0x0A, 0 ; newline", "Hello, World!\n\0")]
  [DataRow("message\tDB\t\"Zero; Assembly: Hello, World!\", 0", "Zero; Assembly: Hello, World!\0")]
  [DataRow("message: db 'It''s Zero', 0", "It's Zero\0")]
  [DataRow("db \"Say \"\"Hi\"\"\", 0", "Say \"Hi\"\0")]
  [DataRow("db '', 1, \"\", 2", "\u0001\u0002")]
  [DataRow("db 'c:\\temp', 0", "c:\\temp\0")]
  [DataRow("db 'caf\u00e9', 0", "caf\u00e9\0")]
  [DataRow("db 'Hi', 0 ; ignore an unmatched quote here '", "Hi\0")]
  public void QuotedAndNumericOperandsEmitExpectedBytes(string source, string expected)
  {
    var (code, _) = ReadTextSection(Assemble(source));
    CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expected), code);
  }

  [TestMethod]
  public void DataAddressesAndRelativeJumpsUseTheirCorrectAddressSpaces()
  {
    var (code, address) = ReadTextSection(Assemble("""
      jmp start
      message db 'abc', 0
      start:
        push message
        mov eax, message
        lea ecx, [message]
        ret
      """));

    Assert.AreEqual(26, code.Length);
    Assert.AreEqual(4, BitConverter.ToInt32(code, 1)); // JMP skips the four data bytes.
    Assert.AreEqual((byte)0x68, code[9]);             // PUSH imm32.
    Assert.AreEqual(address + 5, BitConverter.ToUInt32(code, 10));
    Assert.AreEqual((byte)0xB8, code[14]);            // MOV EAX, imm32.
    Assert.AreEqual(address + 5, BitConverter.ToUInt32(code, 15));
    Assert.AreEqual((byte)0x8D, code[19]);            // LEA ECX, [disp32].
    Assert.AreEqual(address + 5, BitConverter.ToUInt32(code, 21));
    Assert.AreEqual((byte)0xC3, code[25]);            // No padding trap after LEA.
  }

  [TestMethod]
  public void ForwardDataReferenceEndingInHResolvesBeforeHexNumbers()
  {
    var (code, address) = ReadTextSection(Assemble("""
      push path
      ret
      path db 'C:', 0
      """));

    Assert.AreEqual(address + 6, BitConverter.ToUInt32(code, 1));
    CollectionAssert.AreEqual(new byte[] { 67, 58, 0 }, code[6..]);
  }

  [TestMethod]
  [DataRow("message db 'unterminated")]
  [DataRow("message db 'abc' garbage")]
  [DataRow("message db 1,,2")]
  [DataRow("message db 1,")]
  [DataRow("message db")]
  [DataRow("message db 1\nmessage db 2")]
  [DataRow("message: db 1\nmessage db 2")]
  [DataRow("eax db 1")]
  [DataRow("1message db 1")]
  public void MalformedDeclarationsReportTheirSourceLine(string source)
  {
    var error = Assert.ThrowsExactly<AssemblerException>(() => Assemble(source));
    StringAssert.Contains(error.Message, "Line ");
  }

  [TestMethod]
  [DataRow("push message")]
  [DataRow("mov eax, message\n  push eax")]
  [DataRow("lea eax, [message]\n  push eax")]
  public void NamedDataCanBePassedToAnImportedFunction(string addressInstructions)
  {
    if (!OperatingSystem.IsWindows())
      Assert.Inconclusive("The assembler produces Windows x86 executables.");

    var bytes = Assemble($"""
      #IMPORT msvcrt.dll, printf, exit
        jmp start
      message db 'Hello, World!', 0x0A, 0
      start:
        {addressInstructions}
        call [IAT_printf]
        add esp, 4
        push 0
        call [IAT_exit]
      """);
    var executable = Path.Combine(Path.GetTempPath(), $"zero-db-{Guid.NewGuid():N}.exe");

    try
    {
      File.WriteAllBytes(executable, bytes);
      using var process = Process.Start(new ProcessStartInfo(executable)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
      });

      Assert.IsNotNull(process);

      if (!process.WaitForExit(10000))
      {
        process.Kill();
        process.WaitForExit();
        Assert.Fail("The named-data sample timed out.");
      }

      Assert.AreEqual(0, process.ExitCode);
      Assert.AreEqual("Hello, World!\r\n", process.StandardOutput.ReadToEnd());
    }
    finally
    {
      File.Delete(executable);
    }
  }

  private static byte[] Assemble(string source)
  {
    var bytes = new Assembler().Assemble(source.Split('\n'));
    Assert.IsNotNull(bytes);
    return bytes;
  }

  private static (byte[] Code, uint Address) ReadTextSection(byte[] executable)
  {
    using var stream = new MemoryStream(executable);
    using var reader = new PEReader(stream);

    foreach (var section in reader.PEHeaders.SectionHeaders)
    {
      if (section.Name != ".text")
        continue;

      var content = reader.GetSectionData(section.VirtualAddress).GetContent(0, section.VirtualSize);
      var code = new byte[content.Length];
      content.CopyTo(code);

      return (code, checked((uint)(reader.PEHeaders.PEHeader!.ImageBase + (uint)section.VirtualAddress)));
    }

    throw new InvalidOperationException("Missing .text section.");
  }
}
