// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using Zero.Compilation;

namespace Zero.Ide.Services;

public sealed class ProgramRunner : IProgramRunner
{
  private const byte ElfClass64 = 2;
  private const byte ElfLittleEndian = 1;
  private const ushort ElfMachineX64 = 62;
  private const uint ElfSignature = 0x464C457F;
  private const int ExecutableHeaderLength = 20;
  private const ushort PeSignature = 0x5A4D;

  public void Run(string executablePath, string workingDirectory)
  {
    executablePath = Path.GetFullPath(executablePath);
    var target = ReadTarget(executablePath);

    if (!target.CanRunOnCurrentHost)
      throw new InvalidOperationException($"The build succeeded, but {target} cannot run on this host. Run it on a compatible operating system and architecture.");

    // Start the binary directly, including .bin artifacts; don't use file associations.
    using var process = Process.Start(new ProcessStartInfo(executablePath)
    {
      UseShellExecute = false,
      CreateNoWindow = false,
      WorkingDirectory = workingDirectory,
    }) ?? throw new InvalidOperationException("The compiled program could not be started.");
    // Disposing our process handle does not stop the child. Keep the IDE editable.
  }

  private static CompilationTarget ReadTarget(string executablePath)
  {
    using var stream = File.OpenRead(executablePath);
    if (stream.Length < ExecutableHeaderLength)
      throw new InvalidOperationException("The build output is not a supported executable.");

    Span<byte> header = stackalloc byte[ExecutableHeaderLength];
    stream.ReadExactly(header);

    if (BinaryPrimitives.ReadUInt16LittleEndian(header) == PeSignature)
    {
      stream.Position = 0;
      using var pe = new PEReader(stream);

      if (pe.PEHeaders.CoffHeader.Machine == Machine.I386)
        return CompilationTarget.WindowsX86;
    }
    else if (BinaryPrimitives.ReadUInt32LittleEndian(header) == ElfSignature
          && header[4] == ElfClass64 && header[5] == ElfLittleEndian
          && BinaryPrimitives.ReadUInt16LittleEndian(header[18..]) == ElfMachineX64)
    {
      return CompilationTarget.LinuxX64;
    }

    throw new InvalidOperationException("The build output must be a Windows x86 PE or Linux x64 ELF executable.");
  }
}
