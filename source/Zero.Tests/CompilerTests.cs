// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Zero.Tests;

[TestClass]
public sealed class CompilerTests
{
  [TestMethod]
  public void VersionMatchesSharedBuildMetadata()
  {
    var expected = typeof(CompilerTests).Assembly
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    Assert.IsFalse(string.IsNullOrWhiteSpace(Zero.Version));
    Assert.AreEqual(expected, Zero.Version);
  }
}
