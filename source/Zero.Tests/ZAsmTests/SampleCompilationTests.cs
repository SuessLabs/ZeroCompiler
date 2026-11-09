// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using Zero.Assembly;
using Zero.Parsing;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class SampleCompilationTests
{
  private const string ResourcePrefix = "Samples/";

  public static IEnumerable<object[]> GetSamples()
  {
    var resources = typeof(SampleCompilationTests).Assembly.GetManifestResourceNames();
    Array.Sort(resources, StringComparer.Ordinal);
    bool found = false;

    foreach (var resource in resources)
    {
      if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        continue;

      found = true;
      yield return [resource[ResourcePrefix.Length..]];
    }

    if (!found)
      throw new InvalidOperationException("No .zasm samples were included in the test assembly.");
  }

  [TestMethod]
  [DynamicData(nameof(GetSamples))]
  public void EverySampleCompilesToAnExecutable(string sampleName)
  {
    // Embed sources at build time so removed/renamed output files cannot enter
    // discovery, and new samples (including subfolders) need no manual test list.
    var sourcePath = Path.Combine(AppContext.BaseDirectory, "samples", sampleName);
    var source = new SourceLoader().Load(sourcePath);
    var bytes = new Assembler().Assemble(source);

    Assert.IsNotNull(bytes, $"Compilation failed: {sampleName}");
    Assert.IsNotEmpty(bytes, $"Empty executable: {sampleName}");

    using var executable = new PEReader(new MemoryStream(bytes));
    Assert.IsNotNull(executable.PEHeaders.PEHeader, $"Invalid PE header: {sampleName}");
    Assert.IsTrue(executable.PEHeaders.PEHeader.AddressOfEntryPoint > 0, $"Missing entry point: {sampleName}");
    Assert.IsTrue(executable.PEHeaders.SectionHeaders.Length > 0, $"Missing sections: {sampleName}");
  }
}
