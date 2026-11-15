// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Zero.Assembly;
using Zero.PE;

namespace Zero.Formats.Pe;

/// <summary>Minimal PE32 Writer (no imports, reloc stripped).</summary>
public class PeWriter
{
  public const uint DosHeaderSize = 0x80;

  public const uint FileHeaderSize = 20;

  /// <summary>.text + .idata.</summary>
  public const ushort NumberOfSections = 2;

  /// <summary>224 (PE32).</summary>
  public const uint OptionalHeaderSize = 0xE0;

  public const uint PeSignatureSize = 4;

  public const uint SectionHeaderSize = 40;

  /// <summary>512 bytes.</summary>
  private const uint FileAlignment = 0x0200;

  /// <summary>Image base.</summary>
  private const uint ImageBase = 0x00400000;

  /// <summary>4 KB.</summary>
  private const uint SectionAlignment = 0x1000;

  public byte[] BuildPe32Executable(byte[] code, PePlan plan)
  {
    // For now, force apps as console
    var subSystemType = SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_CUI;

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
    // Characteristics: RELOCS_STRIPPED (0x0001) | EXECUTABLE_IMAGE (0x0002) | 32BIT_MACHINE (0x0100)
    bw.Write((ushort)(0x0001 | 0x0002 | 0x0100));

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
    bw.Write(AlignUp(plan.TextVirtSize, FileAlignment)); // SizeOfCode (file-aligned)
    bw.Write((uint)0);                // SizeOfInitializedData
    bw.Write((uint)0);                // SizeOfUninitializedData
    bw.Write((plan.TextRva));         // AddressOfEntryPoint (start of .text)
    bw.Write((plan.TextRva));         // BaseOfCode
    bw.Write((plan.TextRva));         // BaseOfData (no data section; set same as code)

    bw.Write(ImageBase);              // ImageBase
    bw.Write(SectionAlignment);       // SectionAlignment
    bw.Write(FileAlignment);          // FileAlignment
    bw.Write((ushort)4);              // MajorOperatingSystemVersion
    bw.Write((ushort)0);              // MinorOperatingSystemVersion
    bw.Write((ushort)0);              // MajorImageVersion
    bw.Write((ushort)0);              // MinorImageVersion
    bw.Write((ushort)4);              // MajorSubsystemVersion
    bw.Write((ushort)0);              // MinorSubsystemVersion
    bw.Write((uint)0);                // Win32VersionValue
    bw.Write(plan.SizeOfImage);       // SizeOfImage
    bw.Write(plan.SizeOfHeaders);     // SizeOfHeaders
    bw.Write((uint)0);                // CheckSum
    bw.Write((ushort)subSystemType);  // Subsystem = Windows CUI (3). Use 2 for GUI.
    bw.Write((ushort)0);              // DllCharacteristics
    bw.Write((uint)0x00100000);       // SizeOfStackReserve (1MB)
    bw.Write((uint)0x00001000);       // SizeOfStackCommit (4KB)
    bw.Write((uint)0x00100000);       // SizeOfHeapReserve (1MB)
    bw.Write((uint)0x00001000);       // SizeOfHeapCommit (4KB)
    bw.Write((uint)0);                // LoaderFlags (reserved)
    bw.Write((uint)16);               // NumberOfRvaAndSizes

    // --- Data Directories (16 * 8 bytes) all zero ---
    // 0: Export
    bw.Write((uint)0); bw.Write((uint)0);

    // 1: Import
    bw.Write(plan.ImportDirectoryRva);
    bw.Write(plan.ImportDirectorySize);

    // 2..15: zeros
    for (int i = 2; i < 16; i++)
    {
      bw.Write((uint)0); // RVA
      bw.Write((uint)0); // Size
    }

    // --- Section Header: .text - IMAGE_DATA_DIRECTORY ---
    bw.Write(FixedAscii8Bytes(".text"));
    bw.Write(plan.TextVirtSize);          // VirtualSize
    bw.Write(plan.TextRva);               // VirtualAddress
    bw.Write(plan.TextRawSize);           // SizeOfRawData
    bw.Write(plan.TextRawPtr);            // PointerToRawData
    bw.Write((uint)0);                    // PointerToRelocations
    bw.Write((uint)0);                    // PointerToLinenumbers
    bw.Write((ushort)0);                  // NumberOfRelocations
    bw.Write((ushort)0);                  // NumberOfLinenumbers
    bw.Write((uint)0x60000020);           // Characteristics: CODE | EXECUTE | READ

    // --- Section Header: .idata ---
    bw.Write(FixedAscii8Bytes(".idata"));
    bw.Write(plan.IdataVirtSize);
    bw.Write(plan.IdataRva);
    bw.Write(plan.IdataRawSize);
    bw.Write(plan.IdataRawPtr);
    bw.Write((uint)0); bw.Write((uint)0);
    bw.Write((ushort)0); bw.Write((ushort)0);
    bw.Write((uint)0x40000040); // INITIALIZED_DATA | READ

    // --- Pad to start of .text raw ---
    PadTo(bw, (int)plan.TextRawPtr);

    // --- Write .text payload ---
    bw.Write(code);
    // Pad to section raw size
    PadTo(bw, (int)(plan.TextRawPtr + plan.TextRawSize));

    // .idata payload
    PadTo(bw, (int)plan.IdataRawPtr);
    var idataBytes = plan.IdataBytesBuilder.Emit(plan.IdataRva);
    bw.Write(idataBytes);
    PadTo(bw, (int)(plan.IdataRawPtr + plan.IdataRawSize));

    bw.Flush();
    return ms.ToArray();
  }

  public PePlan PlanLayout(int codeSize, IReadOnlyList<ImportRequest> imports)
  {
    // TODO (2026-01-18 DS): Pull size from structs
    // Headers
    uint headersSizeUnaligned =
      DosHeaderSize +
      PeSignatureSize +
      FileHeaderSize +
      OptionalHeaderSize +
      (SectionHeaderSize * NumberOfSections);

    uint sizeOfHeaders = AlignUp(headersSizeUnaligned, FileAlignment);

    // Section: .text
    uint textRva = SectionAlignment; // First section RVA (0x1000)
    ////uint textVirtualAddress = AlignUp(SectionAlignment, SectionAlignment); // First section typically at 0x1000
    ////if (textVirtualAddress == 0)
    ////  textVirtualAddress = SectionAlignment;

    uint textVirtualSize = (uint)codeSize;
    uint textRawSize = AlignUp(textVirtualSize, FileAlignment);
    uint textRawPtr = sizeOfHeaders;

    ////// SizeOfImage = lastSection(VirtAddr + aligned VirtSize)
    ////uint sizeOfImage = AlignUp(textRva + AlignUp(textVirtualSize, SectionAlignment), SectionAlignment);

    // .idata section RVA/Raw follows .text
    uint idataRva = AlignUp(textRva + AlignUp(textVirtualSize, SectionAlignment), SectionAlignment);
    uint idataRawPtr = AlignUp(textRawPtr + textRawSize, FileAlignment);

    // Build .idata content in-memory to compute sizes and symbol addresses
    var id = BuildIdataLayout(imports, idataRva);
    uint idataVirtSize = id.TotalSize;
    uint idataRawSize = AlignUp(idataVirtSize, FileAlignment);
    uint sizeOfImage = AlignUp(idataRva + AlignUp(idataVirtSize, SectionAlignment), SectionAlignment);

    // Extern symbol map (absolute addresses)
    var externs = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    var plainNames = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    var ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var (dll, func, iatRva) in id.IatEntries)
    {
      // Import Address Table and without "IAT"
      uint abs = ImageBase + iatRva;
      externs["IAT_" + func] = abs;
      externs[$"IAT_{SanitizeDll(dll)}_{func}"] = abs;
      externs[$"__imp_{func}"] = abs; // common alias

      if (!plainNames.TryAdd(func, abs))
        ambiguousNames.Add(func);
    }

    // Plain names point to the same IAT slots. Preserve all legacy aliases and
    // require DLL-qualified names when multiple libraries export the same name.
    foreach (var (name, address) in plainNames)
    {
      if (!ambiguousNames.Contains(name))
        externs.TryAdd(name, address);
    }

    return new PePlan
    {
      ImageBase = ImageBase,
      SectionAlignment = SectionAlignment,
      FileAlignment = FileAlignment,
      SizeOfHeaders = sizeOfHeaders,
      SizeOfImage = sizeOfImage,
      TextRva = textRva,
      TextVirtSize = textVirtualSize,
      TextRawPtr = textRawPtr,
      TextRawSize = textRawSize,
      IdataRva = idataRva,
      IdataRawPtr = idataRawPtr,
      IdataVirtSize = idataVirtSize,
      IdataRawSize = idataRawSize,
      ImportDirectoryRva = id.ImportDirectoryRva,
      ImportDirectorySize = id.ImportDirectorySize,
      IdataBytesBuilder = id,
      ExternSymbolMap = externs,
    };
  }

  private static uint AlignUp(uint value, uint align) =>
    (value + align - 1) / align * align;

  private static byte[] FixedAscii8Bytes(string name)
  {
    var arr = new byte[8];
    var bytes = Encoding.ASCII.GetBytes(name);
    Array.Copy(bytes, arr, Math.Min(8, bytes.Length));
    return arr;
  }

  private static void PadTo(BinaryWriter bw, int position)
  {
    int cur = (int)bw.BaseStream.Position;
    if (position < cur)
      return;

    int need = position - cur;
    if (need > 0)
      bw.Write(new byte[need]);
  }

  private static string SanitizeDll(string dll) =>
    dll.Replace('.', '_').Replace('-', '_');

  /// <summary>Section .idata (import data) builder.</summary>
  /// <param name="imports">Imports requested.</param>
  /// <param name="idataRvaBase">IData Virtual Address Base.</param>
  /// <returns>IDataBuilder.</returns>
  private IDataBuilder BuildIdataLayout(IReadOnlyList<ImportRequest> imports, uint idataRvaBase)
  {
    // Build memory image of:
    //   [Import Descriptors ...] + null desc
    //   [ILT arrays ...]
    //   [IAT arrays ...]
    //   [DLL name strings ...]
    //   [Hint/Name structures ...]
    //
    // We'll layout in simple sequence and remember RVAs.

    ////var descs = new List<ImportDescLayout>();
    var nameBlob = new BlobBuilder();
    var thunkBlob = new BlobBuilder();
    var iatBlob = new BlobBuilder();

    // Assign bases before recording any RVAs in descriptors or thunk entries.
    int descriptorSize = (imports.Count + 1) * 20;
    int thunkSize = 0;
    foreach (var req in imports)
      thunkSize += (req.Functions.Count + 1) * 4;

    thunkBlob.OffsetBase = descriptorSize;
    iatBlob.OffsetBase = descriptorSize + thunkSize;
    nameBlob.OffsetBase = descriptorSize + (2 * thunkSize);

    // Helper to add DLL name and return RVA
    uint AddDllName(string dll)
    {
      uint rva = 0; int ofs = nameBlob.Position;
      nameBlob.AddAsciiZ(dll);
      rva = idataRvaBase + (uint)nameBlob.OffsetBase + (uint)ofs;
      return rva;
    }

    // Helper to add Hint/Name and return RVA
    uint AddHintName(string func)
    {
      // IMAGE_IMPORT_BY_NAME starts on a two-byte boundary.
      if ((nameBlob.Position & 1) != 0)
        nameBlob.AddBytes(new byte[] { 0 });

      int ofs = nameBlob.Position;
      nameBlob.AddWord(0);         // Hint=0
      nameBlob.AddAsciiZ(func);
      return idataRvaBase + (uint)nameBlob.OffsetBase + (uint)ofs;
    }

    // First, reserve space for descriptors (we'll fill later).
    // We don't know counts yet; we’ll build thunks and names first and then produce descriptors.
    // Collect per-DLL function name RVAs and thunk positions.
    var iatEntries = new List<(string dll, string func, uint iatRva)>();

    // Build ILT/IAT for each DLL
    var perDll = new List<(ImportRequest req, uint iltRva, uint iatRva, List<uint> nameRvas, uint nameRvaDll)>();
    foreach (var req in imports)
    {
      var nameRvaDll = AddDllName(req.Dll);
      var nameRvas = new List<uint>(req.Functions.Count);

      // For each function, add name and then emit one ILT entry and one IAT entry (RVA to name)
      int iltStartOfs = thunkBlob.Position;
      int iatStartOfs = iatBlob.Position;

      foreach (var f in req.Functions)
      {
        var rvaName = AddHintName(f);
        nameRvas.Add(rvaName);

        // ILT entry (IMAGE_THUNK_DATA) = RVA to Hint/Name
        thunkBlob.AddDword(rvaName);
        // IAT entry (initially same as ILT value)
        iatBlob.AddDword(rvaName);
      }

      // null terminators
      thunkBlob.AddDword(0);
      iatBlob.AddDword(0);

      uint iltRva = idataRvaBase + (uint)thunkBlob.OffsetBase + (uint)iltStartOfs;
      uint iatRva = idataRvaBase + (uint)iatBlob.OffsetBase + (uint)iatStartOfs;

      // Record IAT addresses for extern map
      for (int i = 0; i < req.Functions.Count; i++)
      {
        uint entryRva = iatRva + (uint)(i * 4);
        iatEntries.Add((req.Dll, req.Functions[i], entryRva));
      }

      perDll.Add((req, iltRva, iatRva, nameRvas, nameRvaDll));
    }

    // Now build Import Descriptors
    var descBlob = new BlobBuilder();
    foreach (var dll in perDll)
    {
      // typedef struct {
      //   DWORD OriginalFirstThunk; DWORD TimeDateStamp; DWORD ForwarderChain;
      //   DWORD Name; DWORD FirstThunk;
      // } IMAGE_IMPORT_DESCRIPTOR;
      descBlob.AddDword(dll.iltRva);
      descBlob.AddDword(0);
      descBlob.AddDword(0);
      descBlob.AddDword(dll.nameRvaDll);
      descBlob.AddDword(dll.iatRva);
    }

    // Null descriptor
    descBlob.AddBytes(new byte[20]);

    // Stitch the .idata in order: [descs][ILTs][IATs][Names]
    // Set their base offsets
    descBlob.OffsetBase = 0;
    thunkBlob.OffsetBase = descBlob.Length;
    iatBlob.OffsetBase = descBlob.Length + thunkBlob.Length;
    nameBlob.OffsetBase = descBlob.Length + thunkBlob.Length + iatBlob.Length;

    uint importDirectoryRva = idataRvaBase + (uint)descBlob.OffsetBase; // start of descriptors
    uint importDirectorySize = (uint)descBlob.Length; // spec-wise OK; many linkers set dir size to full idata—either works

    uint totalSize = (uint)(descBlob.Length + thunkBlob.Length + iatBlob.Length + nameBlob.Length);

    return new IDataBuilder
    {
      Desc = descBlob,
      ILT = thunkBlob,
      IAT = iatBlob,
      Names = nameBlob,
      ImportDirectoryRva = importDirectoryRva,
      ImportDirectorySize = importDirectorySize,
      TotalSize = totalSize,
      IatEntries = iatEntries
    };
  }
}
