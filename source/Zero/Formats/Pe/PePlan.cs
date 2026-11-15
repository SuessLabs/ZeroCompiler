// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using static Zero.Formats.Pe.PeWriter;

namespace Zero.Formats.Pe;

public class PePlan
{
  public IReadOnlyDictionary<string, uint> ExternSymbolMap { get; set; } = default!;
  public uint FileAlignment { get; set; }
  public IDataBuilder IdataBytesBuilder { get; set; } = default!;
  public uint IdataRawPtr { get; set; }
  public uint IdataRawSize { get; set; }
  public uint IdataRva { get; set; }
  public uint IdataVirtSize { get; set; }
  public uint ImageBase { get; set; }
  public uint ImportDirectoryRva { get; set; }
  public uint ImportDirectorySize { get; set; }
  public uint SectionAlignment { get; set; }
  public uint SizeOfHeaders { get; set; }
  public uint SizeOfImage { get; set; }
  public uint TextRawPtr { get; set; }
  public uint TextRawSize { get; set; }

  /// <summary>Text Virtual Address.</summary>
  public uint TextRva { get; set; }

  /// <summary>Text Virtual Size.</summary>
  public uint TextVirtSize { get; set; }
}
