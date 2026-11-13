// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Zero.Formats.Elf;

/// <summary>Represents the 32-bit ELF (Executable and Linkable Format) file header (52 bytes).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Elf32Header
{
  /// <summary>Magic number and file identification info (16 bytes).</summary>
  public ElfIdent Ident;

  /// <summary>Object file type (2 bytes).</summary>
  public ushort Type;

  /// <summary>Target instruction set architecture (2 bytes).</summary>
  public ushort Machine;

  /// <summary>Object file format version (4 bytes).</summary>
  public uint Version;

  /// <summary>Entry point virtual address (4 bytes).</summary>
  public uint Entry;

  /// <summary>Program header table file offset in bytes (4 bytes).</summary>
  public uint PhOff;

  /// <summary>Section header table file offset in bytes (4 bytes).</summary>
  public uint ShOff;

  /// <summary>Processor-specific flags (4 bytes).</summary>
  public uint Flags;

  /// <summary>ELF header size in bytes (2 bytes).</summary>
  public ushort EhSize;

  /// <summary>Program header table entry size in bytes (2 bytes).</summary>
  public ushort PhEntSize;

  /// <summary>Program header table entry count (2 bytes).</summary>
  public ushort PhNum;

  /// <summary>Section header table entry size in bytes (2 bytes).</summary>
  public ushort ShEntSize;

  /// <summary>Section header table entry count (2 bytes).</summary>
  public ushort ShNum;

  /// <summary>Section header string table index (2 bytes).</summary>
  public ushort ShStrNdx;
}
