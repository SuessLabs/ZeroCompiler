// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Zero.Assembly;
using Zero.Compilation;
using Zero.Exceptions;
using Zero.Parsing;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class IncludeTests
{
  private CompilerOptions _options = null!;
  private DirectoryInfo _workspace = null!;

  [TestMethod]
  public void AbsoluteIncludesAndNormalizedDuplicatesExpandOncePerLoad()
  {
    var header = Write("external/file.zh", "nop");
    Write("first/parent.zh", $"using \"{header}\"");
    var main = Write("source/main.zasm", $"using \"{header}\"\nusing \"{Path.GetDirectoryName(header)}/./file.zh\"\nusing \"parent.zh\"");
    var loader = new SourceLoader();

    Assert.HasCount(1, loader.Load(main, _options));
    Assert.HasCount(1, loader.Load(main, _options));
  }

  [TestMethod]
  [DataRow("invalid_instruction")]
  [DataRow("push missing_symbol")]
  [DataRow("#IMPORT kernel32.dll, ExitProcess\nExitProcess: ret")]
  public void AssemblyErrorsInHeadersKeepOriginalLocation(string instruction)
  {
    Write("first/outer.zh", "nop\nnop\nusing \"inner.zh\"");
    var inner = Write("first/inner.zh", "; comment\n" + instruction);
    var main = Write("source/main.zasm", "using \"outer.zh\"");
    var lines = new SourceLoader().Load(main, _options);

    var error = Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(lines));

    var lineNumber = instruction.StartsWith("#IMPORT", StringComparison.Ordinal) ? 3 : 2;
    Assert.Contains($"{inner}({lineNumber}):", error.Message);
  }

  [TestCleanup]
  public void Cleanup() => _workspace.Delete(recursive: true);

  [TestMethod]
  public void CycleReportsChainAndRequestingLocation()
  {
    var main = Write("work/main.zasm", "using \"a.zh\"");
    var a = Write("work/a.zh", "using \"b.zh\"");
    var b = Write("work/b.zh", "; comment\nusing \"a.zh\"");

    var error = Assert.ThrowsExactly<AssemblerException>(() => new SourceLoader().Load(main, _options));

    Assert.Contains($"{b}(2)", error.Message);
    Assert.Contains($"{main} -> {a} -> {b} -> {a}", error.Message);
  }

  [TestInitialize]
  public void Initialize()
  {
    _workspace = Directory.CreateTempSubdirectory("zero-includes-");
    _options = new CompilerOptions
    {
      WorkingDirectory = Path.Combine(_workspace.FullName, "work"),
      CompilerDirectory = Path.Combine(_workspace.FullName, "compiler"),
    };

    Directory.CreateDirectory(_options.WorkingDirectory);
    _options.IncludeDirectories.Add(Path.Combine(_workspace.FullName, "first"));
    _options.IncludeDirectories.Add(Path.Combine(_workspace.FullName, "second"));
  }

  [TestMethod]
  public void LabelsResolveAcrossIncludeBoundaries()
  {
    Write("first/code.zh", "helper: push message\nret");
    var main = Write("source/main.zasm", "jmp start\nusing \"code.zh\"\nmessage db 'Hello', 0\nstart: call helper\nret");

    Assert.IsNotNull(new Assembler().Assemble(new SourceLoader().Load(main, _options)));
  }

  [TestMethod]
  public void MainFileErrorsAfterIncludesKeepOriginalLocation()
  {
    Write("first/file.zh", "nop\nnop\nnop");
    var main = Write("source/main.zasm", "using \"file.zh\"\npush missing_symbol");
    var lines = new SourceLoader().Load(main, _options);

    var error = Assert.ThrowsExactly<AssemblerException>(() => new Assembler().Assemble(lines));

    Assert.Contains($"{main}(2):", error.Message);
  }

  [TestMethod]
  [DataRow("using")]
  [DataRow("using file.zh")]
  [DataRow("using \"\"")]
  [DataRow("using \"file.zh")]
  [DataRow("using \"file.zh\" extra")]
  [DataRow("using \"file.zh\", \"other.zh\"")]
  public void MalformedDirectiveReportsOriginalLocation(string directive)
  {
    var main = Write("source/main.zasm", "; comment\n" + directive);

    var error = Assert.ThrowsExactly<AssemblerException>(() => new SourceLoader().Load(main, _options));

    Assert.Contains($"{main}(2):", error.Message);
  }

  [TestMethod]
  public void MissingIncludeReportsEverySearchLocation()
  {
    var main = Write("source/main.zasm", "; comment\nusing \"sub/missing.zh\"");
    var error = Assert.ThrowsExactly<AssemblerException>(() => new SourceLoader().Load(main, _options));

    Assert.Contains($"{main}(2)", error.Message);

    foreach (var root in new[] { "work", "first", "second", "compiler/include" })
      Assert.Contains(Path.GetFullPath(Path.Combine(_workspace.FullName, root, "sub/missing.zh")), error.Message);
  }

  [TestMethod]
  public void NestedIncludesUseSameRootsAndRelativeIncludePathsUseWorkingDirectory()
  {
    _options.IncludeDirectories.Clear();
    _options.IncludeDirectories.Add("../first");
    Write("first/sub/parent.zh", "using \"child.zh\"");
    Write("first/sub/child.zh", "invalid_instruction");

    var child = Write("work/child.zh", "nop");
    var main = Write("source/main.zasm", "using \"sub/parent.zh\"");
    var lines = new SourceLoader().Load(main, _options);

    Assert.HasCount(1, lines);
    Assert.AreEqual(child, lines[0].FilePath);
    Assert.AreEqual("nop", lines[0].Text);
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(1)]
  [DataRow(2)]
  [DataRow(3)]
  public void SearchOrderPreservesSubfolders(int firstAvailable)
  {
    var roots = new[] { "work", "first", "second", "compiler/include" };
    for (int i = firstAvailable; i < roots.Length; i++)
      Write($"{roots[i]}/sub/file.zh", $"db {i}");
    var main = Write("source/main.zasm", "using \"sub/file.zh\"");

    var lines = new SourceLoader().Load(main, _options);

    Assert.HasCount(1, lines);
    Assert.AreEqual($"db {firstAvailable}", lines[0].Text);
    Assert.AreEqual(Path.GetFullPath(Path.Combine(_workspace.FullName, roots[firstAvailable], "sub/file.zh")), lines[0].FilePath);
    Assert.AreEqual(1, lines[0].LineNumber);
  }

  [TestMethod]
  public void UsingRespectsCommentsQuotedPathsAndOrdinarySourceStrings()
  {
    Write("first/folder with spaces/file;name.zh", "nop");
    var main = Write("source/main.zasm", "  USING \"folder with spaces/file;name.zh\" ; trailing comment\n; using \"missing.zh\"\ntext db 'using \"missing.zh\"', 0\nusing_label: ret");

    var lines = new SourceLoader().Load(main, _options);

    Assert.HasCount(4, lines);
    Assert.AreEqual("nop", lines[0].Text);
    Assert.IsNotNull(new Assembler().Assemble(lines));
  }

  private string Write(string relativePath, string content)
  {
    var path = Path.GetFullPath(Path.Combine(_workspace.FullName, relativePath));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
    return path;
  }
}
