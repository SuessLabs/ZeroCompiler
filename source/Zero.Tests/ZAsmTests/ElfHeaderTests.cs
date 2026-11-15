// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Zero.Formats.Elf;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class ElfHeaderTests
{
  [TestMethod]
  public void IdentificationIsInlineAndPreservesBothHeaderLayouts()
  {
    Assert.AreEqual(16, Marshal.SizeOf<ElfIdent>());
    Assert.AreEqual(52, Marshal.SizeOf<Elf32Header>());
    Assert.AreEqual(64, Marshal.SizeOf<Elf64Header>());
    Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<ElfIdent>());
    Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<Elf32Header>());
    Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<Elf64Header>());
    Assert.AreEqual(4, Marshal.OffsetOf<ElfIdent>(nameof(ElfIdent.Class)).ToInt32());
    Assert.AreEqual(8, Marshal.OffsetOf<ElfIdent>(nameof(ElfIdent.AbiVersion)).ToInt32());
    Assert.AreEqual(15, Marshal.OffsetOf<ElfIdent>(nameof(ElfIdent.Padding6)).ToInt32());
    Assert.AreEqual(16, Marshal.OffsetOf<Elf32Header>(nameof(Elf32Header.Type)).ToInt32());
    Assert.AreEqual(16, Marshal.OffsetOf<Elf64Header>(nameof(Elf64Header.Type)).ToInt32());
  }

  [TestMethod]
  [DataRow((byte)1, (byte)2)]
  [DataRow((byte)2, (byte)1)]
  public void ConstructorInitializesMagicMetadataAndZeroPadding(byte fileClass, byte encoding)
  {
    var ident = new ElfIdent(fileClass, encoding, version: 1, osAbi: 3);
    Assert.AreEqual((byte)0x7F, ident.Magic0);
    Assert.AreEqual((byte)'E', ident.Magic1);
    Assert.AreEqual((byte)'L', ident.Magic2);
    Assert.AreEqual((byte)'F', ident.Magic3);
    Assert.AreEqual(fileClass, ident.Class);
    Assert.AreEqual(encoding, ident.DataEncoding);
    Assert.AreEqual((byte)1, ident.Version);
    Assert.AreEqual((byte)3, ident.OsAbi);
    Assert.AreEqual((byte)0, ident.AbiVersion);
    Assert.AreSequenceEqual(new byte[7], new[] { ident.Padding0, ident.Padding1, ident.Padding2,
      ident.Padding3, ident.Padding4, ident.Padding5, ident.Padding6 });
  }
}
