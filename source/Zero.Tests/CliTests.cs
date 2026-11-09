// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Zero.Cli;

namespace Zero.Tests;

[TestClass]
public sealed class CliTests
{
  private DirectoryInfo _workspace = null!;

  [TestCleanup]
  public void Cleanup()
  {
    _workspace?.Delete(recursive: true);
  }

  [TestMethod]
  public void CompileOnlyDoesNotExecuteProgram()
  {
    var source = WriteExitProgram(37);

    Assert.AreEqual(0, Program.Main([source]));
    Assert.IsTrue(File.Exists(Path.ChangeExtension(source, ".exe")));
  }

  [TestMethod]
  public void FailedCompilationDoesNotExecuteStaleOutput()
  {
    var source = WriteExitProgram(37);
    var output = Path.ChangeExtension(source, ".exe");
    Assert.AreEqual(0, Program.Main([source]));

    var original = File.ReadAllBytes(output);
    File.WriteAllText(source, "invalid_instruction");

    Assert.AreEqual((int)ErrorCode.AssemblyError, Program.Main(["run", source]));
    Assert.AreSequenceEqual(original, File.ReadAllBytes(output));
  }

  [TestMethod]
  [DataRow(false)]
  [DataRow(true)]
  public void IncludeDirectoriesWorkForCompileAndRun(bool run)
  {
    var source = WriteExitProgram(37);
    File.WriteAllText(source, "using \"exit.zh\"\npush 37\ncall [ExitProcess]");

    var headers = Directory.CreateDirectory(Path.Combine(_workspace.FullName, "my headers")).FullName;
    File.WriteAllText(Path.Combine(headers, "exit.zh"), "#IMPORT kernel32.dll, ExitProcess");

    var missing = Path.Combine(_workspace.FullName, "missing");

    string[] arguments = run
      ? ["run", "-I", missing, source, "--include", headers]
      : ["-I", missing, source, "--include", headers];

    Assert.AreEqual(run ? 37 : 0, Program.Main(arguments));
    Assert.IsTrue(File.Exists(Path.ChangeExtension(source, ".exe")));
  }

  [TestInitialize]
  public void Initialize()
  {
    if (!OperatingSystem.IsWindows())
      Assert.Inconclusive("The compiler currently generates Windows executables.");

    _workspace = Directory.CreateTempSubdirectory("zero cli test-");
  }

  [TestMethod]
  public void InvalidRunArgumentsReturnFailure()
  {
    Assert.AreEqual((int)ErrorCode.InvalidCommandLineArguments, Program.Main(["run"]));
    Assert.AreEqual((int)ErrorCode.InvalidCommandLineArguments, Program.Main(["run", "source", "output", "extra"]));
  }

  [TestMethod]
  public void MissingIncludeDirectoryArgumentReturnsFailure()
  {
    var source = WriteExitProgram(37);

    Assert.AreEqual((int)ErrorCode.InvalidCommandLineArguments, Program.Main([source, "-I"]));
    Assert.AreEqual((int)ErrorCode.InvalidCommandLineArguments, Program.Main(["run", source, "--include", "-I"]));
    Assert.IsFalse(File.Exists(Path.ChangeExtension(source, ".exe")));
  }

  [TestMethod]
  public void MissingIncludePreventsRunningStaleOutput()
  {
    var source = WriteExitProgram(37);
    Assert.AreEqual(0, Program.Main([source]));
    File.WriteAllText(source, "using \"missing-file.zh\"");

    Assert.AreEqual((int)ErrorCode.AssemblyError, Program.Main(["run", source]));
  }

  [TestMethod]
  public void MissingSourceReturnsFailure()
  {
    var source = Path.Combine(_workspace.FullName, "missing.zasm");

    Assert.AreEqual((int)ErrorCode.SourceFileNotFound, Program.Main(["run", source]));
    Assert.IsFalse(File.Exists(Path.ChangeExtension(source, ".exe")));
  }

  [TestMethod]
  public void OutputWriteFailureReturnsFailure()
  {
    var source = WriteExitProgram(37);
    var output = Path.Combine(_workspace.FullName, "missing directory", "output.exe");

    Assert.AreEqual((int)ErrorCode.UnexpectedError, Program.Main(["run", source, output]));
  }

  [TestMethod]
  public void RunCompilesAndReturnsProgramExitCode()
  {
    var source = WriteExitProgram(37);
    var output = Path.Combine(_workspace.FullName, "custom output.exe");

    Assert.AreEqual(37, Program.Main(["run", source, output]));
    Assert.IsTrue(File.Exists(output));
  }

  [TestMethod]
  public void RunUsesDefaultOutputPath()
  {
    var source = WriteExitProgram(23);

    Assert.AreEqual(23, Program.Main(["run", source]));
    Assert.IsTrue(File.Exists(Path.ChangeExtension(source, ".exe")));
  }

  private string WriteExitProgram(int exitCode)
  {
    var source = Path.Combine(_workspace.FullName, "exit program.zasm");
    File.WriteAllText(source, $"#IMPORT kernel32.dll, ExitProcess\n  push {exitCode}\n  call [IAT_ExitProcess]\n");
    return source;
  }
}
