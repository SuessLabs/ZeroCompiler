// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Zero.Formats.Elf;

/// <summary>The 16-byte ELF identification block shared by ELF32 and ELF64.</summary>
/// <remarks>Byte fields keep the block inline and independent of byte order.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ElfIdent
{
  private const byte MagicPrefix = 0x7F;
  private const byte CurrentVersion = 1;
  private const byte SystemVAbi = 0;
  private const byte DefaultAbiVersion = 0;

  /// <summary>First magic byte: 0x7F.</summary>k
  public byte Magic0;

  /// <summary>Second magic byte: E.</summary>
  public byte Magic1;

  /// <summary>Third magic byte: L.</summary>
  public byte Magic2;

  /// <summary>Fourth magic byte: F.</summary>
  public byte Magic3;

  /// <summary>File class: 1 for ELF32, 2 for ELF64.</summary>
  public byte Class;

  /// <summary>Data encoding: 1 for little-endian, 2 for big-endian.</summary>
  public byte DataEncoding;

  /// <summary>Identification format version, currently 1.</summary>
  public byte Version;

  /// <summary>OS/ABI identifier, such as 0 for System V.</summary>
  public byte OsAbi;

  /// <summary>Version of the selected OS/ABI.</summary>
  public byte AbiVersion;

  // Reserved padding bytes must remain zero.
  public byte Padding0;

  public byte Padding1;
  public byte Padding2;
  public byte Padding3;
  public byte Padding4;
  public byte Padding5;
  public byte Padding6;

  /// <summary>Initialize the ELF magic, identification fields, and zero padding.</summary>
  public ElfIdent(byte fileClass, byte dataEncoding, byte version = CurrentVersion, byte osAbi = SystemVAbi, byte abiVersion = DefaultAbiVersion)
  {
    this = default;
    Magic0 = MagicPrefix;
    Magic1 = (byte)'E';
    Magic2 = (byte)'L';
    Magic3 = (byte)'F';
    Class = fileClass;
    DataEncoding = dataEncoding;
    Version = version;
    OsAbi = osAbi;
    AbiVersion = abiVersion;
  }
}
