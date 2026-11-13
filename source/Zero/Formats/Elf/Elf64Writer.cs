// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Formats.Elf;

/// <summary>Static Linux AMD64 ET_EXEC image: RX text and a non-executable stack.</summary>
internal sealed class Elf64Writer : IExecutableWriter
{
  private const ushort Amd64Machine = 62;
  private const byte Class64 = 2;
  private const byte CurrentVersion = 1;
  private const ushort ElfHeaderSize = 64;
  private const ulong EmptySegmentSize = 0;
  private const ushort ExecutableType = 2;
  private const uint GnuStackSegmentType = 0x6474E551;
  private const ulong ImageBase = 0x400000;
  private const byte LittleEndianEncoding = 1;
  private const ulong LoadSegmentAlignment = 0x1000;
  private const uint LoadSegmentType = 1;
  private const ulong NoFileOffset = 0;
  private const uint NoProcessorFlags = 0;
  private const ulong NoSectionEntrySize = 0;
  private const ulong NoSectionFlags = 0;
  private const uint NoSectionInfo = 0;
  private const uint NoSectionLink = 0;
  private const ulong NoVirtualAddress = 0;
  private const uint ProgramBitsSectionType = 1;
  private const ushort ProgramHeaderCount = 2;
  private const ushort ProgramHeaderEntrySize = 56;
  private const ulong SectionAllocated = 2;
  private const ulong SectionExecutable = 4;
  private const ushort SectionHeaderCount = 3;
  private const ushort SectionHeaderEntrySize = 64;
  private const ushort SectionNameTableIndex = 2;
  private const uint SectionNameTableNameOffset = 7;
  private const uint SegmentExecute = 1;
  private const uint SegmentRead = 4;
  private const uint SegmentWrite = 2;
  private const ulong StackAlignment = 16;
  private const ulong StringTableAlignment = 1;
  private const uint StringTableSectionType = 3;
  private const byte SystemVAbi = 0;
  private const int TableAlignment = 8;
  private const ulong TextAlignment = 16;
  private const int TextOffset = 0x1000;
  private const uint TextSectionNameOffset = 1;

  public ExecutableLayout Plan(AssemblyProgram program)
  {
    if (program.Imports.Count > 0)
      throw new AssemblerException(program.FirstImportLine ?? 1, "ELF64 shared-library imports are not implemented; use Linux syscalls instead of #IMPORT");

    return new ExecutableLayout(ImageBase + TextOffset);
  }

  public byte[] Write(byte[] code, ExecutableLayout layout)
  {
    var names = Encoding.ASCII.GetBytes("\0.text\0.shstrtab\0");
    int namesOffset = AlignTable(TextOffset + code.Length);
    int sectionsOffset = AlignTable(namesOffset + names.Length);
    int fileSize = checked(sectionsOffset + SectionHeaderCount * SectionHeaderEntrySize);

    var header = new Elf64Header
    {
      Ident = new ElfIdent(Class64, LittleEndianEncoding, CurrentVersion, SystemVAbi),
      Type = ExecutableType,
      Machine = Amd64Machine,
      Version = CurrentVersion,
      Entry = layout.CodeAddress,
      PhOff = ElfHeaderSize,
      ShOff = (ulong)sectionsOffset,
      Flags = NoProcessorFlags,
      EhSize = ElfHeaderSize,
      PhEntSize = ProgramHeaderEntrySize,
      PhNum = ProgramHeaderCount,
      ShEntSize = SectionHeaderEntrySize,
      ShNum = SectionHeaderCount,
      ShStrNdx = SectionNameTableIndex,
    };

    using var stream = new MemoryStream(new byte[fileSize], writable: true);
    using var writer = new BinaryWriter(stream);

    WriteHeader(writer, header);

    WriteProgramHeader(writer, LoadSegmentType, SegmentRead | SegmentExecute, NoFileOffset, ImageBase, (ulong)fileSize, LoadSegmentAlignment);
    WriteProgramHeader(writer, GnuStackSegmentType, SegmentRead | SegmentWrite, NoFileOffset, NoVirtualAddress, EmptySegmentSize, StackAlignment);

    stream.Position = TextOffset;
    writer.Write(code);
    stream.Position = namesOffset;
    writer.Write(names);
    stream.Position = sectionsOffset + SectionHeaderEntrySize; // Null section stays zero.

    WriteSectionHeader(writer, TextSectionNameOffset, ProgramBitsSectionType, SectionAllocated | SectionExecutable, layout.CodeAddress, TextOffset, code.Length, TextAlignment);
    WriteSectionHeader(writer, SectionNameTableNameOffset, StringTableSectionType, NoSectionFlags, NoVirtualAddress, namesOffset, names.Length, StringTableAlignment);
    return stream.ToArray();
  }

  private static int AlignTable(int offset) => checked(offset + TableAlignment - 1) & -TableAlignment;

  // Explicit field serialization preserves little-endian output without runtime marshalling.
  private static void WriteHeader(BinaryWriter writer, Elf64Header header)
  {
    WriteIdentification(writer, header.Ident);

    writer.Write(header.Type);
    writer.Write(header.Machine);
    writer.Write(header.Version);
    writer.Write(header.Entry);
    writer.Write(header.PhOff);
    writer.Write(header.ShOff);
    writer.Write(header.Flags);
    writer.Write(header.EhSize);
    writer.Write(header.PhEntSize);
    writer.Write(header.PhNum);
    writer.Write(header.ShEntSize);
    writer.Write(header.ShNum);
    writer.Write(header.ShStrNdx);
  }

  private static void WriteIdentification(BinaryWriter writer, ElfIdent ident)
  {
    writer.Write(ident.Magic0);
    writer.Write(ident.Magic1);
    writer.Write(ident.Magic2);
    writer.Write(ident.Magic3);
    writer.Write(ident.Class);
    writer.Write(ident.DataEncoding);
    writer.Write(ident.Version);
    writer.Write(ident.OsAbi);
    writer.Write(ident.AbiVersion);
    writer.Write(ident.Padding0);
    writer.Write(ident.Padding1);
    writer.Write(ident.Padding2);
    writer.Write(ident.Padding3);
    writer.Write(ident.Padding4);
    writer.Write(ident.Padding5);
    writer.Write(ident.Padding6);
  }

  private static void WriteProgramHeader(BinaryWriter writer, uint type, uint flags, ulong offset, ulong address, ulong size, ulong alignment)
  {
    writer.Write(type);
    writer.Write(flags);
    writer.Write(offset);
    writer.Write(address);
    writer.Write(address);
    writer.Write(size);
    writer.Write(size);
    writer.Write(alignment);
  }

  private static void WriteSectionHeader(BinaryWriter writer, uint name, uint type, ulong flags, ulong address, int offset, int size, ulong alignment)
  {
    writer.Write(name);
    writer.Write(type);
    writer.Write(flags);
    writer.Write(address);
    writer.Write((ulong)offset);
    writer.Write((ulong)size);
    writer.Write(NoSectionLink);
    writer.Write(NoSectionInfo);
    writer.Write(alignment);
    writer.Write(NoSectionEntrySize);
  }
}
