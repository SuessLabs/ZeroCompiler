// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Zero.Compilation;
using Zero.Dbg;
using Zero.Parsing;

namespace Zero.Dbg.Tests;

[TestClass]
public class DebugMapTests
{
  [TestMethod]
  public void IncludeOnlyRootIsHashedEvenWithoutEmittedInstructions()
  {
    using var fixture = new DebugFixture("using \"helper.zh\"", "ret");
    Assert.HasCount(2, fixture.Map.Sources);
    File.AppendAllText(fixture.Source, "\n// changed root");
    Assert.ThrowsExactly<InvalidDataException>(() => fixture.Map.Validate(fixture.Executable));
  }

  [TestMethod]
  public void MapPreservesIncludedLocationsAndExcludesDataAndComments()
  {
    using var fixture = new DebugFixture("""
      jmp start
      message db 'data', 0
      /* comment
      ignored */
      using "helper.zh"
      start: call helper
      ret
      """, "// header\nhelper: mov eax, 42\nret");

    var map = DebugMap.Load(fixture.Executable + DebugMap.Extension);
    map.Validate(fixture.Executable);

    Assert.IsNull(map.FindInstruction(fixture.Source, 2));
    Assert.IsNull(map.FindInstruction(fixture.Source, 3));
    Assert.IsNull(map.FindInstruction(fixture.Source, 5));
    Assert.IsNotNull(map.FindInstruction(fixture.Source, 6));
    Assert.IsTrue(map.FindInstruction(fixture.Source, 6)!.IsCall);
    Assert.IsNotNull(map.FindInstruction(fixture.Header, 2));
    Assert.HasCount(5, map.Instructions);
    Assert.IsNotNull(map.FindInstruction(map.EntryRva));
  }

  [TestMethod]
  public void MapRejectsChangedSourceAndExecutable()
  {
    using var fixture = new DebugFixture("mov eax, 1\nret");
    var map = DebugMap.Load(fixture.Executable + DebugMap.Extension);
    File.AppendAllText(fixture.Source, "\n// changed");

    Assert.ThrowsExactly<InvalidDataException>(() => map.Validate(fixture.Executable));

    File.WriteAllText(fixture.Source, fixture.SourceText);
    File.AppendAllText(fixture.Executable, "changed");

    Assert.ThrowsExactly<InvalidDataException>(() => map.Validate(fixture.Executable));
  }

  [TestMethod]
  public void NormalBuildRemovesOldDebuggerMap()
  {
    using var fixture = new DebugFixture("ret");

    Assert.IsTrue(File.Exists(fixture.Executable + DebugMap.Extension));
    Assert.AreEqual(ErrorCode.None, new global::Zero.Zero().Compile(fixture.Source, fixture.Executable, SourceType.Assembly));
    Assert.IsFalse(File.Exists(fixture.Executable + DebugMap.Extension));
  }
}

internal sealed class DebugFixture : IDisposable
{
  private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("zero-dbg-");

  internal DebugFixture(string source, string? header = null)
  {
    Source = Path.Combine(_root.FullName, "program.zasm");
    Header = Path.Combine(_root.FullName, "helper.zh");
    Executable = Path.Combine(_root.FullName, "program.exe");
    SourceText = source;

    File.WriteAllText(Source, source);

    if (header is not null)
      File.WriteAllText(Header, header);

    var options = new CompilerOptions { EmitDebugInformation = true, WorkingDirectory = _root.FullName };
    Assert.AreEqual(ErrorCode.None, new global::Zero.Zero().Compile(Source, Executable, SourceType.Assembly, options));
    Map = DebugMap.Load(Executable + DebugMap.Extension);
  }

  internal string Executable { get; }

  internal string Header { get; }

  internal DebugMap Map { get; }

  internal string Source { get; }

  internal string SourceText { get; }

  public void Dispose() => _root.Delete(true);

  internal DebugLaunchOptions Options(params int[] lines)
  {
    var breakpoints = new SourceBreakpoint[lines.Length];

    for (int i = 0; i < lines.Length; i++)
      breakpoints[i] = new(Source, lines[i]);

    return new(Executable, _root.FullName, Map, breakpoints);
  }
}
