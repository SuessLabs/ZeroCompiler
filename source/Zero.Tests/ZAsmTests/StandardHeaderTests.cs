// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using Zero.Assembly;
using Zero.Parsing;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class StandardHeaderTests
{
  public static IEnumerable<object[]> GetHeaders()
  {
    var resources = typeof(StandardHeaderTests).Assembly.GetManifestResourceNames();
    Array.Sort(resources, StringComparer.Ordinal);
    bool found = false;
    foreach (var name in resources)
    {
      if (!name.StartsWith("Include/", StringComparison.Ordinal))
        continue;

      found = true;
      yield return [name["Include/".Length..]];
    }

    if (!found)
      throw new InvalidOperationException("No standard .zh headers were included in the test assembly.");
  }

  [TestMethod]
  [DynamicData(nameof(GetHeaders))]
  public void EveryHeaderCompilesAndImportsExistingExports(string headerName)
  {
    var assembler = CompileHeaders([headerName]);

    // Verify the target's actual export spellings without loading 32-bit DLLs
    // into the test process or calling any OS APIs declared in the headers.
    if (!OperatingSystem.IsWindows())
      return;

    var systemDirectory = Environment.GetFolderPath(Environment.Is64BitOperatingSystem
      ? Environment.SpecialFolder.SystemX86
      : Environment.SpecialFolder.System);

    foreach (var import in assembler.Imports)
    {
      var libraryPath = Path.Combine(systemDirectory, import.Dll);
      Assert.IsTrue(File.Exists(libraryPath), $"Missing target DLL: {libraryPath}");

      var exports = ReadExportNames(libraryPath);

      foreach (var function in import.Functions)
        Assert.IsTrue(exports.Contains(function), $"{headerName}: {import.Dll} does not export '{function}' with this spelling.");
    }
  }

  [TestMethod]
  public void AllHeadersCanBeCombined()
  {
    var headers = new List<string>();
    foreach (var row in GetHeaders())
      headers.Add((string)row[0]);

    _ = CompileHeaders(headers);
  }

  private static Assembler CompileHeaders(IEnumerable<string> headerNames)
  {
    var source = new List<SourceLine>();
    foreach (var header in headerNames)
      source.AddRange(new SourceLoader().Load(Path.Combine(AppContext.BaseDirectory, "include", header)));

    source.Add(new SourceLine("#IMPORT kernel32.dll, ExitProcess", "<header test>", 1));
    source.Add(new SourceLine("push 0", "<header test>", 2));
    source.Add(new SourceLine("call [ExitProcess]", "<header test>", 3));
    var assembler = new Assembler();
    var bytes = assembler.Assemble(source);
    Assert.IsNotNull(bytes);
    Assert.IsNotEmpty(bytes);

    using var executable = new PEReader(new MemoryStream(bytes));
    Assert.AreEqual(Machine.I386, executable.PEHeaders.CoffHeader.Machine);
    Assert.IsNotNull(executable.PEHeaders.PEHeader);
    Assert.IsTrue(executable.PEHeaders.PEHeader.ImportTableDirectory.Size > 0);

    return assembler;
  }

  private static HashSet<string> ReadExportNames(string libraryPath)
  {
    using var stream = File.OpenRead(libraryPath);
    using var library = new PEReader(stream);
    Assert.AreEqual(Machine.I386, library.PEHeaders.CoffHeader.Machine, $"Expected x86 DLL: {libraryPath}");
    Assert.IsNotNull(library.PEHeaders.PEHeader);

    var directoryRva = library.PEHeaders.PEHeader.ExportTableDirectory.RelativeVirtualAddress;
    Assert.IsTrue(directoryRva > 0, $"No exports: {libraryPath}");

    var directory = library.GetSectionData(directoryRva).GetReader();
    directory.Offset = 24; // IMAGE_EXPORT_DIRECTORY.NumberOfNames
    int count = directory.ReadInt32();
    directory.Offset = 32; // IMAGE_EXPORT_DIRECTORY.AddressOfNames

    var names = library.GetSectionData(directory.ReadInt32()).GetReader();
    var exports = new HashSet<string>(StringComparer.Ordinal);

    for (int i = 0; i < count; i++)
    {
      var reader = library.GetSectionData(names.ReadInt32()).GetReader();
      var name = new StringBuilder();
      byte character;

      while ((character = reader.ReadByte()) != 0)
        name.Append((char)character);

      exports.Add(name.ToString());
    }

    return exports;
  }
}
