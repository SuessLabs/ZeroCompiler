// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class ImportSymbolTests
{
  [TestMethod]
  [DataRow("ExitProcess")]
  [DataRow("exitprocess")]
  [DataRow("IAT_ExitProcess")]
  [DataRow("__imp_ExitProcess")]
  [DataRow("IAT_kernel32_dll_ExitProcess")]
  public void PlainAndLegacyImportNamesProduceIdenticalExecutables(string symbol)
  {
    var expected = Assemble("#IMPORT kernel32.dll, ExitProcess\npush 0\ncall [IAT_ExitProcess]");
    var actual = Assemble($"#IMPORT kernel32.dll, ExitProcess\npush 0\ncall [{symbol}]");
    CollectionAssert.AreEqual(expected, actual);
  }

  [TestMethod]
  public void RepeatedImportsStillHaveAnUnambiguousPlainName()
  {
    var expected = Assemble("#IMPORT kernel32.dll, ExitProcess\npush 0\ncall [ExitProcess]");
    var actual = Assemble("#IMPORT kernel32.dll, ExitProcess\n#IMPORT kernel32.dll, ExitProcess\npush 0\ncall [ExitProcess]");
    CollectionAssert.AreEqual(expected, actual);
  }

  [TestMethod]
  public void AmbiguousPlainNamesRequireDllQualifiedAliases()
  {
    const string imports = "#IMPORT first.dll, Shared\n#IMPORT second.dll, Shared\n";
    Assert.ThrowsExactly<AssemblerException>(() => Assemble(imports + "call [Shared]"));
    Assert.IsNotNull(Assemble(imports + "call [IAT_first_dll_Shared]\ncall [IAT_second_dll_Shared]"));
  }

  [TestMethod]
  public void LocalLabelsCannotSilentlyReplaceAnImportedFunctionName()
  {
    var error = Assert.ThrowsExactly<AssemblerException>(() => Assemble("#IMPORT kernel32.dll, ExitProcess\nExitProcess: ret"));
    StringAssert.Contains(error.Message, "conflicts with an imported symbol");
  }

  [TestMethod]
  public void PlainImportAndLocalProcedureCallsExecuteTogether()
  {
    if (!OperatingSystem.IsWindows())
      Assert.Inconclusive("The assembler produces Windows x86 executables.");

    var bytes = Assemble("""
      #IMPORT kernel32.dll, ExitProcess
        call local_procedure
        push eax
        call [ExitProcess]
      local_procedure:
        mov eax, 37
        ret
      """);
    var executable = Path.Combine(Path.GetTempPath(), $"zero-import-{Guid.NewGuid():N}.exe");
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
        Assert.Fail("The import alias test timed out.");
      }

      Assert.AreEqual(37, process.ExitCode);
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
}
