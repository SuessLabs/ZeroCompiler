// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Zero.Parsing;

namespace Zero.Tests.ZAsmTests;

/// <summary>Validate draft source declarations without pretending to emit ELF.</summary>
[TestClass]
public sealed class LinuxHeaderTests
{
  public static IEnumerable<object[]> GetLibcHeaders()
  {
    var names = typeof(LinuxHeaderTests).Assembly.GetManifestResourceNames();
    Array.Sort(names, StringComparer.Ordinal);
    bool found = false;
    foreach (var resource in names)
    {
      var name = resource.Replace('\\', '/');
      if (!name.StartsWith("Include/linux/libc/", StringComparison.Ordinal))
        continue;

      found = true;
      yield return [name["Include/".Length..]];
    }

    if (!found)
      throw new InvalidOperationException("No draft Linux libc headers were included in the test assembly.");
  }

  [TestMethod]
  [DynamicData(nameof(GetLibcHeaders))]
  public void DraftLibcHeadersContainOnlyGlibcImportDeclarations(string name)
  {
    var lines = new SourceLoader().Load(Path.Combine(AppContext.BaseDirectory, "include", name));
    var imports = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in lines)
    {
      var text = line.Text.Trim();
      if (text.Length == 0 || text.StartsWith(';'))
        continue;

      Assert.IsTrue(text.StartsWith("#IMPORT libc.so.6, ", StringComparison.Ordinal), $"Unexpected draft statement: {text}");
      var symbols = text["#IMPORT libc.so.6, ".Length..].Split(',');
      foreach (var symbol in symbols)
      {
        var function = symbol.Trim();
        Assert.IsTrue(Regex.IsMatch(function, "^[A-Za-z_][A-Za-z0-9_]*$"), $"Invalid export name: {function}");
        Assert.IsTrue(imports.Add(function), $"Duplicate declaration: {function}");
      }
    }

    Assert.IsNotEmpty(imports, $"No draft imports in {name}");
  }

  [TestMethod]
  [DataRow("x64.zh")]
  [DataRow("arm64.zh")]
  public void SyscallReferencesDoNotClaimExecutableSupport(string name)
  {
    var lines = new SourceLoader().Load(Path.Combine(AppContext.BaseDirectory, "include/linux/syscalls", name));
    Assert.IsTrue(lines.Count > 0);
    foreach (var line in lines)
      Assert.IsTrue(string.IsNullOrWhiteSpace(line.Text) || line.Text.TrimStart().StartsWith(';'));
  }
}
