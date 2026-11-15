// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Zero.Dbg.WinPe;

// All layouts below are the Windows x64 ABI. The debuggee's register context is x86/WOW64.
internal static partial class Native
{
  internal const uint Continue = 0x00010002;
  internal const uint NotHandled = 0x80010001;
  internal const uint Breakpoint = 0x80000003;
  internal const uint SingleStep = 0x80000004;
  internal const uint WowBreakpoint = 0x4000001F;
  internal const uint WowSingleStep = 0x4000001E;
  internal const uint TrapFlag = 0x100;

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool CloseHandle(IntPtr handle);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool ContinueDebugEvent(uint processId, uint threadId, uint status);

  [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool CreateProcess(string application, IntPtr commandLine, IntPtr processAttributes,
    IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags,
    IntPtr environment, string directory, ref StartupInfo startupInfo, out ProcessInformation information);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool DebugBreakProcess(IntPtr process);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, nuint size, out nuint read);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  internal static partial uint ResumeThread(IntPtr thread);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  internal static partial uint SuspendThread(IntPtr thread);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool TerminateProcess(IntPtr process, uint exitCode);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool WaitForDebugEventEx(out DebugEvent debugEvent, uint milliseconds);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  internal static partial uint WaitForSingleObject(IntPtr handle, uint milliseconds);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool Wow64GetThreadContext(IntPtr thread, ref Context context);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool Wow64SetThreadContext(IntPtr thread, ref Context context);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static partial bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);

  [StructLayout(LayoutKind.Explicit, Size = 716)]
  internal struct Context
  {
    [FieldOffset(0)] internal uint Flags;
    [FieldOffset(156)] internal uint Edi;
    [FieldOffset(160)] internal uint Esi;
    [FieldOffset(164)] internal uint Ebx;
    [FieldOffset(168)] internal uint Edx;
    [FieldOffset(172)] internal uint Ecx;
    [FieldOffset(176)] internal uint Eax;
    [FieldOffset(180)] internal uint Ebp;
    [FieldOffset(184)] internal uint Eip;
    [FieldOffset(192)] internal uint EFlags;
    [FieldOffset(196)] internal uint Esp;
  }

  [StructLayout(LayoutKind.Explicit, Size = 176)]
  internal struct DebugEvent
  {
    [FieldOffset(0)] internal uint Code;
    [FieldOffset(4)] internal uint ProcessId;
    [FieldOffset(8)] internal uint ThreadId;
    [FieldOffset(16)] internal uint ExceptionCode;
    [FieldOffset(32)] internal IntPtr ExceptionAddress;
    [FieldOffset(168)] internal uint FirstChance;
    [FieldOffset(16)] internal uint ExitCode;
    [FieldOffset(16)] internal IntPtr File;
    [FieldOffset(24)] internal IntPtr Process;
    [FieldOffset(32)] internal IntPtr InitialThread;
    [FieldOffset(40)] internal IntPtr ImageBase;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct ProcessInformation
  {
    internal IntPtr Process, Thread;
    internal uint ProcessId, ThreadId;
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  internal struct StartupInfo
  {
    internal uint Size;
    internal IntPtr Reserved, Desktop, Title;
    internal uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
    internal ushort ShowWindow, ReservedSize;
    internal IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError;
  }
}
