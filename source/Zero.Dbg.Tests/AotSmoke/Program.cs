// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Zero.Compilation;
using Zero.Dbg;
using Zero.Dbg.WinPe;

if (!WinPeDebugSession.IsSupported) throw new PlatformNotSupportedException("Run the AOT smoke check on Windows x64.");
var directory = Directory.CreateTempSubdirectory("zero-dbg-aot-");
try
{
  var source = Path.Combine(directory.FullName, "program.zasm");
  var executable = Path.Combine(directory.FullName, "program.exe");
  File.WriteAllText(source, "#IMPORT kernel32.dll, ExitProcess\nmov eax, 42\npush 0\ncall [ExitProcess]");
  var compiler = new Zero.Zero();
  if (compiler.Compile(source, executable, Zero.SourceType.Assembly, new CompilerOptions { EmitDebugInformation = true }) != Zero.ErrorCode.None)
    throw new InvalidOperationException("Smoke compilation failed.");
  var map = DebugMap.Load(executable + DebugMap.Extension);
  await using var session = new WinPeDebugSession();
  var events = Channel.CreateUnbounded<DebugSnapshot>();
  session.Changed += (_, snapshot) => events.Writer.TryWrite(snapshot);
  await session.LaunchAsync(new(executable, directory.FullName, map, [new(source, 3)])).WaitAsync(TimeSpan.FromSeconds(10));
  await ReadAsync(DebugState.Paused);
  session.StepOver();
  var paused = await ReadAsync(DebugState.Paused);
  bool found = false;
  foreach (var register in paused.Registers!)
    if (register.Name == "EAX" && register.Value == 42) found = true;
  if (!found || paused.Location?.Line != 3) throw new InvalidOperationException("AOT register/source check failed.");
  session.Continue();
  var exited = await ReadAsync(DebugState.Exited);
  if (exited.ExitCode != 0) throw new InvalidOperationException("Smoke debuggee failed.");
  Console.WriteLine("AOT debugger smoke check passed.");

  async Task<DebugSnapshot> ReadAsync(DebugState state)
  {
    while (true)
    {
      var snapshot = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
      if (snapshot.State == DebugState.Faulted) throw new InvalidOperationException(snapshot.Reason);
      if (snapshot.State == state) return snapshot;
    }
  }
}
finally
{
  directory.Delete(true);
}
