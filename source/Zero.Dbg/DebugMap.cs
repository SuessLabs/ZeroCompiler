// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zero.Dbg;

public sealed class DebugInstruction
{
  public string FilePath { get; set; } = "";

  public bool IsCall { get; set; }

  public int Line { get; set; }

  public uint Rva { get; set; }

  public int Size { get; set; }
}

/// <summary>Versioned source locations for one exact executable; addresses are module-relative.</summary>
public sealed class DebugMap
{
  /// <summary>Suffix appended to an executable path for its JSON debugger source-map sidecar, such as program.exe.zdbg.json.</summary>
  public const string Extension = ".zdbg.json";

  public uint EntryRva { get; set; }

  public string ExecutableHash { get; set; } = "";

  public List<DebugInstruction> Instructions { get; set; } = [];

  public ulong PreferredImageBase { get; set; }

  public List<DebugSource> Sources { get; set; } = [];

  public string Target { get; set; } = "";

  public int Version { get; set; } = 1;

  public static string HashFile(string path)
  {
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
  }

  public static DebugMap Load(string path) =>
    JsonSerializer.Deserialize(File.ReadAllText(path), DebugJsonContext.Default.DebugMap)
      ?? throw new InvalidDataException("The debugger map is empty.");

  public DebugInstruction? FindInstruction(string filePath, int line)
  {
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    var path = Path.GetFullPath(filePath);

    foreach (var instruction in Instructions)
      if (instruction.Line == line && string.Equals(instruction.FilePath, path, comparison))
        return instruction;

    return null;
  }

  public DebugInstruction? FindInstruction(uint rva)
  {
    // Instructions are emitted in address order. Return only instruction starts.
    int low = 0;
    int high = Instructions.Count - 1;

    while (low <= high)
    {
      int middle = low + ((high - low) / 2);
      var instruction = Instructions[middle];

      if (instruction.Rva == rva)
        return instruction;

      if (instruction.Rva < rva)
        low = middle + 1;
      else
        high = middle - 1;
    }

    return null;
  }

  public void Save(string path) => File.WriteAllText(path,
    JsonSerializer.Serialize(this, DebugJsonContext.Default.DebugMap));

  public void Validate(string executablePath)
  {
    if (Version != 1) throw new InvalidDataException($"Unsupported debugger map version: {Version}.");

    if (!string.Equals(ExecutableHash, HashFile(executablePath), StringComparison.Ordinal))
      throw new InvalidDataException("The executable does not match its debugger map. Rebuild before debugging.");

    foreach (var source in Sources)
    {
      if (!File.Exists(source.FilePath) ||
          !string.Equals(source.Hash, HashFile(source.FilePath), StringComparison.Ordinal))
      {
        throw new InvalidDataException($"Source changed or is missing: {source.FilePath}. Rebuild before debugging.");
      }
    }

    uint previousEnd = 0;

    foreach (var instruction in Instructions)
    {
      if (instruction.Size <= 0 || instruction.Line <= 0 || instruction.Rva < previousEnd)
        throw new InvalidDataException("The debugger map has invalid instruction ranges.");

      previousEnd = checked(instruction.Rva + (uint)instruction.Size);
    }
  }
}

public sealed class DebugSource
{
  public string FilePath { get; set; } = "";

  public string Hash { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DebugMap))]
internal partial class DebugJsonContext : JsonSerializerContext;
