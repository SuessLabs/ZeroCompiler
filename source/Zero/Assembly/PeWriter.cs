// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Zero.PE;

namespace Zero.Assembly;

/// <summary>Minimal PE32 Writer (no imports, reloc stripped).</summary>
public class PeWriter
{
  /// <summary>512 bytes.</summary>
  private const uint FileAlignment = 0x0200;

  /// <summary>Image base.</summary>
  private const uint ImageBase = 0x00400000;

  /// <summary>4 KB.</summary>
  private const uint SectionAlignment = 0x1000;

  public byte[] BuildPe32Executable(byte[] code)
  {
    // Layout parameters
    const uint DosHeaderSize = 0x80; // We'll point e_lfanew to 0x80
    const uint PeSignatureSize = 4;
    const uint FileHeaderSize = 20;
    const uint OptionalHeaderSize = 0xE0; // 224 (PE32)
    const uint SectionHeaderSize = 40;
    const ushort NumberOfSections = 1;

    // For now, force apps as console
    var subSystemType = SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_CUI;

    uint headersSizeUnaligned =
      DosHeaderSize +
      PeSignatureSize +
      FileHeaderSize +
      OptionalHeaderSize +
      (SectionHeaderSize * NumberOfSections);

    uint sizeOfHeaders = AlignUp(headersSizeUnaligned, FileAlignment);

    // Section: .text
    uint textVirtualAddress = AlignUp(SectionAlignment, SectionAlignment); // First section typically at 0x1000
    if (textVirtualAddress == 0)
      textVirtualAddress = SectionAlignment;

    uint textVirtualSize = (uint)code.Length;
    uint textRawSize = AlignUp(textVirtualSize, FileAlignment);
    uint textRawPtr = sizeOfHeaders;

    // SizeOfImage = lastSection(VirtAddr + aligned VirtSize)
    uint sizeOfImage = AlignUp(textVirtualAddress + AlignUp(textVirtualSize, SectionAlignment), SectionAlignment);

    // Build Binary
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);

    // --- DOS header (minimal) ---
    // e_magic 'MZ' + e_lfanew pointer at 0x3C to NT headers offset 0x80
    // Fill 0x80 bytes; set magic and e_lfanew
    var dos = new byte[DosHeaderSize];
    dos[0] = (byte)'M';
    dos[1] = (byte)'Z';

    // e_lfanew at 0x3C (DWORD) -> 0x80
    BitConverter.GetBytes((uint)DosHeaderSize).CopyTo(dos, 0x3C);
    bw.Write(dos);

    // --- PE Signature ---
    bw.Write(Encoding.ASCII.GetBytes("PE\0\0"));

    // --- COFF File Header - IMAGE_FILE_HEADER ---
    bw.Write((ushort)0x014c); // Machine = Intel 386
    bw.Write((ushort)NumberOfSections);
    bw.Write((uint)0);        // TimeDateStamp
    bw.Write((uint)0);        // PointerToSymbolTable
    bw.Write((uint)0);        // NumberOfSymbols
    bw.Write((ushort)OptionalHeaderSize);
    // Characteristics: EXECUTABLE_IMAGE | 32BIT_MACHINE | RELOCS_STRIPPED
    bw.Write((ushort)(0x0002 | 0x0100 | 0x0001));

    // --- Optional Header (PE32) - IMAGE_OPTIONAL_HEADER32 ---
    /*
    TODO (2026-01-17 DS) vNext: Use Structs to ensure nothing is missed.
    TODO: Calculate checksum and fill in data correctly

    var optHeader = new IMAGE_OPTIONAL_HEADER32();
    int headerSize = Marshal.SizeOf(typeof(IMAGE_OPTIONAL_HEADER32));
    IntPtr ptr = Marshal.AllocHGlobal(headerSize);
    try
    {
      Marshal.StructureToPtr(optHeader, ptr, false);    // Copy struct into unmanaged memory
      byte[] headerBytes = new byte[headerSize];        // Create byte array and copy
      Marshal.Copy(ptr, headerBytes, 0, headerSize);
      // use header bytes
    }
    finally
    {
      Marshal.FreeHGlobal(ptr);                         // free unmanaged memory
    }
    */

    bw.Write((ushort)0x10B);          // Magic PE32
    bw.Write((byte)6);                // MajorLinkerVersion
    bw.Write((byte)0);                // MinorLinkerVersion
    bw.Write((uint)AlignUp(textVirtualSize, FileAlignment)); // SizeOfCode (file-aligned)
    bw.Write((uint)0);                // SizeOfInitializedData
    bw.Write((uint)0);                // SizeOfUninitializedData
    bw.Write((uint)(textVirtualAddress)); // AddressOfEntryPoint (start of .text)
    bw.Write((uint)(textVirtualAddress)); // BaseOfCode
    bw.Write((uint)(textVirtualAddress)); // BaseOfData (no data section; set same as code)
    bw.Write((uint)ImageBase);        // ImageBase
    bw.Write((uint)SectionAlignment); // SectionAlignment
    bw.Write((uint)FileAlignment);    // FileAlignment
    bw.Write((ushort)4);              // MajorOperatingSystemVersion
    bw.Write((ushort)0);              // MinorOperatingSystemVersion
    bw.Write((ushort)0);              // MajorImageVersion
    bw.Write((ushort)0);              // MinorImageVersion
    bw.Write((ushort)4);              // MajorSubsystemVersion
    bw.Write((ushort)0);              // MinorSubsystemVersion
    bw.Write((uint)0);                // Win32VersionValue
    bw.Write((uint)sizeOfImage);      // SizeOfImage
    bw.Write((uint)sizeOfHeaders);    // SizeOfHeaders
    bw.Write((uint)0);                // CheckSum
    bw.Write((ushort)subSystemType);  // Subsystem = Windows CUI (3). Use 2 for GUI.
    bw.Write((ushort)0);              // DllCharacteristics
    bw.Write((uint)0x00100000);       // SizeOfStackReserve (1MB)
    bw.Write((uint)0x00001000);       // SizeOfStackCommit (4KB)
    bw.Write((uint)0x00100000);       // SizeOfHeapReserve (1MB)
    bw.Write((uint)0x00001000);       // SizeOfHeapCommit (4KB)
    bw.Write((uint)16);               // NumberOfRvaAndSizes

    // --- Data Directories (16 * 8 bytes) all zero ---
    for (int i = 0; i < 16; i++)
    {
      bw.Write((uint)0); // RVA
      bw.Write((uint)0); // Size
    }

    // --- Section Header: .text - IMAGE_DATA_DIRECTORY ---
    var name = Encoding.ASCII.GetBytes(".text\0\0\0");
    if (name.Length != 8) throw new InvalidOperationException();
    bw.Write(name);
    bw.Write((uint)textVirtualSize);      // VirtualSize
    bw.Write((uint)textVirtualAddress);   // VirtualAddress
    bw.Write((uint)textRawSize);          // SizeOfRawData
    bw.Write((uint)textRawPtr);           // PointerToRawData
    bw.Write((uint)0);                    // PointerToRelocations
    bw.Write((uint)0);                    // PointerToLinenumbers
    bw.Write((ushort)0);                  // NumberOfRelocations
    bw.Write((ushort)0);                  // NumberOfLinenumbers
                                          // Characteristics: CODE | EXECUTE | READ
    bw.Write((uint)0x60000020);

    // --- Pad to start of section raw data ---
    PadTo(bw, (int)textRawPtr);

    // --- Write .text payload ---
    bw.Write(code);
    // Pad to section raw size
    PadTo(bw, (int)(textRawPtr + textRawSize));

    bw.Flush();
    return ms.ToArray();
  }

  private static uint AlignUp(uint value, uint align) =>
    (value + align - 1) / align * align;

  private static void PadTo(BinaryWriter bw, int position)
  {
    int cur = (int)bw.BaseStream.Position;
    if (position < cur)
      return;

    int need = position - cur;
    if (need > 0)
      bw.Write(new byte[need]);
  }
}
