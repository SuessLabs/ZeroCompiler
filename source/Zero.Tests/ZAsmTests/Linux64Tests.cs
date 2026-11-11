// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using Zero.Assembly;
using Zero.Cli;
using Zero.Compilation;
using Zero.Exceptions;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class Linux64Tests
{
  [TestMethod]
  [DataRow("mov rax, 0x1122334455667788", "48B88877665544332211")]
  [DataRow("mov r8, 42", "49B82A00000000000000")]
  [DataRow("mov r9d, 42", "41B92A000000")]
  [DataRow("mov rax, r8", "4C89C0")]
  [DataRow("mov r12, r13", "4D89EC")]
  [DataRow("mov eax, ebx", "89D8")]
  [DataRow("mov edi, 0xFFFFFFFF", "BFFFFFFFFF")]
  [DataRow("mov rax, [rsp + 8]", "488B842408000000")]
  [DataRow("mov r8, [r12 + 16]", "4D8B842410000000")]
  [DataRow("mov [r13 - 8], r9", "4D898DF8FFFFFF")]
  [DataRow("mov [rsp], eax", "89842400000000")]
  [DataRow("lea r10, [r12 + 8]", "4D8D942408000000")]
  [DataRow("add rax, -1", "4881C0FFFFFFFF")]
  [DataRow("sub r9, 7", "4981E907000000")]
  [DataRow("cmp rax, rbx", "4839D8")]
  [DataRow("test r8d, r9d", "4585C8")]
  [DataRow("xor edi, edi", "31FF")]
  [DataRow("and r10, 7", "4981E207000000")]
  [DataRow("or rax, rdx", "4809D0")]
  [DataRow("push r15", "4157")]
  [DataRow("pop r8", "4158")]
  [DataRow("push -1", "68FFFFFFFF")]
  [DataRow("call r8", "41FFD0")]
  [DataRow("jmp r15", "41FFE7")]
  [DataRow("call [r12]", "41FF942400000000")]
  [DataRow("ret 16", "C21000")]
  [DataRow("syscall", "0F05")]
  [DataRow("nop", "90")]
  [DataRow("lea rax, [message]\nmessage: db 1", "488D050000000001")]
  [DataRow("lea r8, [rip + message]\nmessage: db 1", "4C8D050000000001")]
  [DataRow("mov eax, [message]\nmessage: dd 42", "8B05000000002A000000")]
  [DataRow("lea rax, [rip + FFh]", "488D05FF000000")]
  [DataRow("lea rax, [path]\npath: db 1", "488D050000000001")]
  [DataRow("mov rax, path\npath: db 1", "48B80A1040000000000001")]
  [DataRow("jmp done\nnop\ndone: syscall", "E901000000900F05")]
  [DataRow("jz8 done\nnop\ndone: ret", "740190C3")]
  [DataRow("call fn\njmp done\nfn: ret\ndone: nop", "E805000000E901000000C390")]
  [DataRow("dq 0x1122334455667788", "8877665544332211")]
  public void EncodingsMatchAmd64(string source, string hex)
  {
    var executable = Build(source);
    Assert.AreEqual(hex, Convert.ToHexString(Text(executable)));
  }

  [TestMethod]
  [DataRow("mov ax, 1")]
  [DataRow("mov eax, rax")]
  [DataRow("mov rax, xmm0")]
  [DataRow("push eax")]
  [DataRow("pop 1")]
  [DataRow("syscall eax")]
  [DataRow("mov r16, 1")]
  [DataRow("mov rax, [eax]")]
  [DataRow("mov rax, [rax + rcx]")]
  [DataRow("mov [rax], 1")]
  [DataRow("add [rax], 1")]
  [DataRow("call eax")]
  [DataRow("mov eax, 0x100000000")]
  [DataRow("mov rax, 0x10000000000000000")]
  [DataRow("add rax, 0x80000000")]
  [DataRow("lea rax, [rsp + 0x80000000]")]
  [DataRow("lea rax, [rsp - 0x8000000000000000]")]
  [DataRow("lea rax, [0xFFFFFFFFFFFFFFFF]")]
  [DataRow("dw 'Hi'")]
  [DataRow("ret 65536")]
  [DataRow("int 256")]
  [DataRow("jmp missing")]
  [DataRow("#IMPORT libc.so.6, puts\nret")]
  [DataRow("#IMPORT kernel32.dll, ExitProcess\nret")]
  public void UnsupportedOrInvalidFormsHaveDiagnostics(string source)
  {
    var error = Assert.ThrowsExactly<AssemblerException>(() => Build(source));
    Assert.IsFalse(string.IsNullOrWhiteSpace(error.Message));
  }

  [TestMethod]
  public void ElfHeadersDescribeLoadableExecutableAndNonExecutableStack()
  {
    var bytes = Build("mov eax, 60\nxor edi, edi\nsyscall");
    Assert.AreSequenceEqual(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 2, 1, 1 }, bytes[..7]);
    Assert.AreEqual((ushort)2, U16(bytes, 16));
    Assert.AreEqual((ushort)62, U16(bytes, 18));
    Assert.AreEqual((ushort)64, U16(bytes, 52));
    Assert.AreEqual((ushort)56, U16(bytes, 54));
    Assert.AreEqual((ushort)2, U16(bytes, 56));
    int programHeaders = checked((int)U64(bytes, 32));
    Assert.AreEqual(1u, U32(bytes, programHeaders));
    Assert.AreEqual(5u, U32(bytes, programHeaders + 4));
    ulong offset = U64(bytes, programHeaders + 8);
    ulong address = U64(bytes, programHeaders + 16);
    ulong size = U64(bytes, programHeaders + 32);
    ulong alignment = U64(bytes, programHeaders + 48);
    Assert.AreEqual(offset % alignment, address % alignment);
    Assert.AreEqual((ulong)bytes.Length, size);
    Assert.IsTrue(U64(bytes, 24) >= address && U64(bytes, 24) < address + size);
    Assert.AreEqual(0x6474E551u, U32(bytes, programHeaders + 56));
    Assert.AreEqual(6u, U32(bytes, programHeaders + 60));
    Assert.AreEqual((ushort)3, U16(bytes, 60));
    Assert.AreEqual((ushort)2, U16(bytes, 62));
  }

  [TestMethod]
  public void ShortBranchesAreRangeChecked()
  {
    Assert.ThrowsExactly<AssemblerException>(() => Build($"jz8 done\ndb '{new string('x', 129)}'\ndone: ret"));
    Assert.ThrowsExactly<AssemblerException>(() => Build($"done: nop\ndb '{new string('x', 129)}'\njnz8 done"));
  }

  [TestMethod]
  [DataRow("macos-x64")]
  [DataRow("macos-arm64")]
  [DataRow("stm32-arm")]
  [DataRow("linux-arm64")]
  [DataRow("windows-x64")]
  public void FuturePlatformTargetsAreExplicitlyUnsupported(string target)
  {
    var error = Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble([$"#TARGET {target}", "nop"]));
    Assert.Contains("not implemented", error.Message);
  }

  [TestMethod]
  public void TargetDirectivesAndExplicitTargetsMustAgree()
  {
    Assert.ThrowsExactly<AssemblerException>(() => new Assembler(CompilationTarget.WindowsX86).Assemble(["#TARGET linux-x64", "nop"]));
    Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(["#TARGET linux-x64", "#TARGET windows-x86", "nop"]));
    Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(["#TARGET unknown", "nop"]));
  }

  [TestMethod]
  public void WindowsTargetRejects64BitRegistersAndLinuxLibraries()
  {
    Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(["mov rax, 1"]));
    Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(["#IMPORT libc.so.6, puts", "ret"]));
  }

  [TestMethod]
  public void LinuxErrorsPreserveIncludedSourceLocations()
  {
    var source = new Parsing.SourceLine[]
    {
      new("#TARGET linux-x64", "main.zasm", 1),
      new("mov rax, [eax]", "bad.zh", 19),
    };
    var error = Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(source));
    Assert.Contains("bad.zh(19):", error.Message);
  }

  [TestMethod]
  public void CliSelectsTargetAndUsesExtensionlessDefaultOutput()
  {
    var directory = Directory.CreateTempSubdirectory("zero-elf-cli-");
    try
    {
      var source = Path.Combine(directory.FullName, "hello.zasm");
      File.WriteAllText(source, "#TARGET linux-x64\nmov eax, 60\nxor edi, edi\nsyscall");
      Assert.AreEqual(0, Program.Main([source]));
      Assert.IsTrue(File.Exists(Path.ChangeExtension(source, null)));
      Assert.IsFalse(File.Exists(Path.ChangeExtension(source, ".exe")));
      Assert.AreEqual((int)ErrorCode.AssemblyError, Program.Main([source, "--target", "windows-x86"]));
      Assert.AreEqual((int)ErrorCode.InvalidCommandLineArguments, Program.Main([source, "--target"]));
      Assert.AreEqual((int)ErrorCode.InvalidCommandLineArguments, Program.Main([source, "--target", "unknown"]));
      if (!CompilationTarget.LinuxX64.CanRunOnCurrentHost)
        Assert.AreEqual((int)ErrorCode.UnexpectedError, Program.Main(["run", source]));
      File.WriteAllText(source, "mov eax, 60\nxor edi, edi\nsyscall");
      Assert.AreEqual(0, Program.Main([source, "--target", "linux-x64"]));
    }
    finally
    {
      directory.Delete(recursive: true);
    }
  }

  private static byte[] Build(string source)
  {
    var result = new Assembler(CompilationTarget.LinuxX64).Assemble(source.Split('\n'));
    Assert.IsNotNull(result);
    return result;
  }

  private static byte[] Text(byte[] executable)
  {
    int header = checked((int)U64(executable, 40) + U16(executable, 58));
    int offset = checked((int)U64(executable, header + 24));
    int size = checked((int)U64(executable, header + 32));
    return executable[offset..(offset + size)];
  }

  private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
  private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
  private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
}
