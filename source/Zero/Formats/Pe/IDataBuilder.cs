// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Zero.Formats.Pe;

public class IDataBuilder
{
  public BlobBuilder Desc { get; set; } = default!;
  public BlobBuilder IAT { get; set; } = default!;
  public List<(string dll, string func, uint iatRva)> IatEntries { get; set; } = default!;
  public BlobBuilder ILT { get; set; } = default!;
  public uint ImportDirectoryRva { get; set; }
  public uint ImportDirectorySize { get; set; }
  public BlobBuilder Names { get; set; } = default!;
  public uint TotalSize { get; set; }

  public byte[] Emit(uint idataRva)
  {
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);
    bw.Write(Desc.ToArray());
    bw.Write(ILT.ToArray());
    bw.Write(IAT.ToArray());
    bw.Write(Names.ToArray());
    bw.Flush();
    return ms.ToArray();
  }
}
