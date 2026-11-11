// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Formats.Elf;

/// <summary>Static Linux AMD64 ET_EXEC image: RX text and a non-executable stack.</summary>
internal sealed class Elf64Writer : IExecutableWriter
{
  private const ulong ImageBase = 0x400000;
  private const int TextOffset = 0x1000;

  public ExecutableLayout Plan(AssemblyProgram program)
  {
    if (program.Imports.Count > 0)
      throw new AssemblerException(program.FirstImportLine ?? 1, "ELF64 shared-library imports are not implemented; use Linux syscalls instead of #IMPORT");
    return new ExecutableLayout(ImageBase + TextOffset);
  }

  public byte[] Write(byte[] code, ExecutableLayout layout)
  {
    var names = Encoding.ASCII.GetBytes("\0.text\0.shstrtab\0");
    int namesOffset = (TextOffset + code.Length + 7) & ~7;
    int sectionsOffset = (namesOffset + names.Length + 7) & ~7;
    int fileSize = checked(sectionsOffset + 3 * 64);
    using var stream = new MemoryStream(new byte[fileSize], writable: true);
    using var writer = new BinaryWriter(stream);
    writer.Write(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    writer.Write((ushort)2); // ET_EXEC
    writer.Write((ushort)62); // EM_X86_64
    writer.Write(1u);
    writer.Write(layout.CodeAddress);
    writer.Write(64UL); // Program header table.
    writer.Write((ulong)sectionsOffset);
    writer.Write(0u);
    writer.Write((ushort)64);
    writer.Write((ushort)56);
    writer.Write((ushort)2);
    writer.Write((ushort)64);
    writer.Write((ushort)3);
    writer.Write((ushort)2); // Section-name string table index.
    ProgramHeader(1, 5, 0, ImageBase, (ulong)fileSize, 0x1000); // PT_LOAD, PF_R | PF_X.
    ProgramHeader(0x6474E551, 6, 0, 0, 0, 16); // PT_GNU_STACK, PF_R | PF_W.
    stream.Position = TextOffset;
    writer.Write(code);
    stream.Position = namesOffset;
    writer.Write(names);
    stream.Position = sectionsOffset + 64; // Null section stays zero.
    SectionHeader(1, 1, 6, layout.CodeAddress, TextOffset, code.Length, 16); // .text
    SectionHeader(7, 3, 0, 0, namesOffset, names.Length, 1); // .shstrtab
    return stream.ToArray();

    void ProgramHeader(uint type, uint flags, ulong offset, ulong address, ulong size, ulong alignment)
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

    void SectionHeader(uint name, uint type, ulong flags, ulong address, int offset, int size, ulong alignment)
    {
      writer.Write(name);
      writer.Write(type);
      writer.Write(flags);
      writer.Write(address);
      writer.Write((ulong)offset);
      writer.Write((ulong)size);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(alignment);
      writer.Write(0UL);
    }
  }
}
