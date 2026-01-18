// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Assembly;

/// <summary>Minimal PE32 Writer (no imports, reloc stripped).</summary>
public class PeWriter
{
  private const uint ImageBase = 0x00400000;
  private const uint SectionAlignment = 0x1000; // 4 KB
  private const uint FileAlignment = 0x0200;  // 512 bytes

  public byte[] BuildPe32Executable(byte[] code)
  {
    // Layout parameters
    const uint DosHeaderSize = 0x80; // We'll point e_lfanew to 0x80
    const uint PeSignatureSize = 4;
    const uint FileHeaderSize = 20;
    const uint OptionalHeaderSize = 0xE0; // 224 (PE32)
    const uint SectionHeaderSize = 40;
    const ushort NumberOfSections = 1;

    uint headersSizeUnaligned =
      DosHeaderSize +
      PeSignatureSize +
      FileHeaderSize +
      OptionalHeaderSize +
      (SectionHeaderSize * NumberOfSections);

    return [];
  }
}
