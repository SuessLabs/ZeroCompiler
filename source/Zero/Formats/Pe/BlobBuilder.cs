// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace Zero.Formats.Pe;

public class BlobBuilder
{
  private readonly MemoryStream _ms = new();
  public int Length => (int)_ms.Length;
  public int OffsetBase { get; set; } = 0;
  public int Position => (int)_ms.Position;

  public void AddAsciiZ(string s)
  {
    var bytes = Encoding.ASCII.GetBytes(s);
    Write(bytes);
    Write(new byte[] { 0 });
  }

  public void AddBytes(byte[] b) => Write(b);

  public void AddDword(uint v) => Write(BitConverter.GetBytes(v));

  public void AddWord(ushort v) => Write(BitConverter.GetBytes(v));

  public byte[] ToArray() => _ms.ToArray();

  public void Write(byte[] b) => _ms.Write(b, 0, b.Length);
}
