// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using HostArchitecture = System.Runtime.InteropServices.Architecture;

namespace Zero.Compilation;

public enum TargetPlatform { Windows, Linux, MacOS, Stm32 }
public enum TargetArchitecture { X86, X64, Arm, Arm64 }
public enum ExecutableFormat { Pe, Elf, MachO }

/// <summary>Platform, architecture, and file format are independent target dimensions.</summary>
public sealed record CompilationTarget(TargetPlatform Platform, TargetArchitecture Architecture, ExecutableFormat Format)
{
  public static CompilationTarget WindowsX86 { get; } = new(TargetPlatform.Windows, TargetArchitecture.X86, ExecutableFormat.Pe);
  public static CompilationTarget LinuxX64 { get; } = new(TargetPlatform.Linux, TargetArchitecture.X64, ExecutableFormat.Elf);

  public static CompilationTarget Parse(string name) => name.ToLowerInvariant() switch
  {
    "windows-x86" => WindowsX86,
    "linux-x64" => LinuxX64,
    "windows-x64" => new(TargetPlatform.Windows, TargetArchitecture.X64, ExecutableFormat.Pe),
    "linux-arm64" => new(TargetPlatform.Linux, TargetArchitecture.Arm64, ExecutableFormat.Elf),
    "macos-x64" => new(TargetPlatform.MacOS, TargetArchitecture.X64, ExecutableFormat.MachO),
    "macos-arm64" => new(TargetPlatform.MacOS, TargetArchitecture.Arm64, ExecutableFormat.MachO),
    "stm32-arm" => new(TargetPlatform.Stm32, TargetArchitecture.Arm, ExecutableFormat.Elf),
    _ => throw new ArgumentException($"Unknown target '{name}'. Supported targets: windows-x86, linux-x64."),
  };

  public bool CanRunOnCurrentHost => this == WindowsX86
    ? OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture is HostArchitecture.X64 or HostArchitecture.X86 or HostArchitecture.Arm64
    : this == LinuxX64 && OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == HostArchitecture.X64;

  public override string ToString() => $"{Platform.ToString().ToLowerInvariant()}-{Architecture.ToString().ToLowerInvariant()}";
}
