// Copyright Xeno Innovations, Inc. 2026
// See LICENSE in the repository root.
// Run: dotnet run --file samples/Windows/notepad/verify.cs -- [--compiler <zero.exe>]
// Requires Windows and .NET 10 SDK or later. No project or NuGet packages needed.
#:property TargetFramework=net10.0
#:property NuGetAudit=false
#:property UseSharedCompilation=false

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

return await Verify.RunAsync(args);

internal static class Verify
{
  public static async Task<int> RunAsync(string[] args)
  {
    try
    {
      string? compilerOption = null;
      for (int i = 0; i < args.Length; i++)
      {
        if (args[i] is "--help" or "-h")
        {
          Console.WriteLine("Usage: dotnet run --file samples/Windows/notepad/verify.cs -- [--compiler <zero.exe>]");
          Console.WriteLine("Windows-only source and file-I/O checks. Dialog selection and error popups are stubbed.");
          return 0;
        }

        Require(args[i] == "--compiler" && compilerOption is null && i + 1 < args.Length &&
          !args[i + 1].StartsWith("--", StringComparison.Ordinal),
          "Expected --compiler <zero.exe>. Use --help for usage.");
        compilerOption = args[++i];
      }

      Require(OperatingSystem.IsWindows(), "This runtime check requires Windows.");
      // CallerFilePath is the source location, independent of the SDK build cache or working directory.
      string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "../../.."));
      string compiler = Path.GetFullPath(compilerOption ?? Path.Combine(root, "publish/zero.exe"));
      Require(File.Exists(compiler), $"Compiler not found: {compiler}. Build it or pass --compiler.");
      string output = Path.Combine(root, "output/notepad-check");
      Directory.CreateDirectory(output);

      var stages = new[]
      {
        ("notepad-01-window", "zasm-04-02-notepad-create-the-window"),
        ("notepad-02-editor", "zasm-04-03-notepad-add-the-text-editor"),
        ("notepad-03-complete", "zasm-04-04-notepad-open-and-save-files"),
      };

      foreach (var (stem, chapter) in stages)
      {
        string source = Path.Combine(root, $"samples/Windows/notepad/{stem}.zasm");
        string listing = ReadText(Path.Combine(root, $"docs/zasm/{chapter}.md"));
        MatchCollection blocks = Regex.Matches(listing, @"```asm\n(.*?)```", RegexOptions.Singleline);
        Require(blocks.Count == 1 && blocks[0].Groups[1].Value == ReadText(source),
          $"Listing differs: {chapter}");
        await CompileAsync(compiler, source, Path.Combine(output, stem + ".exe"));
      }

      Console.WriteLine("PASS: All three complete chapter listings match their standalone sources and compile.");
      string sample = ReadText(Path.Combine(root, "samples/Windows/notepad/notepad-03-complete.zasm"));

      // Exercise the real file routines against a hidden multiline EDIT control.
      // Replace only interactive filename selection and error popups with deterministic stubs.
      int entry = Find(sample, "jmp start");
      sample = sample[..entry] + "jmp test_start" + sample[(entry + "jmp start".Length)..];
      int dialog = Find(sample, "choose_path:\n");
      int open = Find(sample, "open_document:\n", dialog);
      sample = sample[..dialog] + "choose_path:\n  mov eax, 1\n  ret\n\n" + sample[open..];
      sample = sample[..Find(sample, "show_error:\n")] + "show_error:\n  xor eax, eax\n  ret\n";
      var assembly = new StringBuilder(sample).AppendLine("#IMPORT msvcrt.dll, strcmp");

      var fixtures = new Dictionary<string, byte[]>
      {
        ["input"] = Encoding.ASCII.GetBytes("Loaded text\r\nSecond line"),
        ["empty"] = [],
        ["maximum"] = Enumerable.Repeat((byte)'A', 65535).ToArray(),
        ["oversized"] = Enumerable.Repeat((byte)'B', 65536).ToArray(),
        ["nul"] = Encoding.ASCII.GetBytes("one\0two"),
      };
      var paths = new Dictionary<string, string>();
      foreach (var (key, bytes) in fixtures)
      {
        paths[key] = Path.Combine(output, key + ".txt");
        File.WriteAllBytes(paths[key], bytes);
      }

      paths["saved"] = Path.Combine(output, "saved.txt");
      paths["empty_saved"] = Path.Combine(output, "empty-saved.txt");
      paths["bad"] = Path.Combine(output, "directory-that-does-not-exist", "bad.txt");
      Require(!Directory.Exists(Path.GetDirectoryName(paths["bad"])),
        "The failed-write fixture directory must not exist: " + Path.GetDirectoryName(paths["bad"]));
      foreach (var (key, path) in paths)
      {
        // Numeric UTF-8 bytes also handle apostrophes in the workspace path safely.
        string bytes = string.Join(", ", Encoding.UTF8.GetBytes(path.Replace('\\', '/')).Select(b => b.ToString()));
        assembly.AppendLine($"test_path_{key} db {bytes}, 0");
      }

      assembly.AppendLine("test_initial db 'Initial text', 13, 10, 'Second line', 0");
      assembly.AppendLine("test_revised db 'Revised text', 13, 10, 'Second line', 0");
      assembly.AppendLine("test_loaded db 'Loaded text', 13, 10, 'Second line', 0");
      assembly.AppendLine("test_parent_class db 'STATIC', 0");
      assembly.AppendLine(Setup);
      assembly.Append(Text("initial")).Append(Pending("saved"))
        .AppendLine("  call save_as_document").Append(Check(1));
      assembly.Append(Text("revised")).AppendLine("""
          mov eax, 1
          lea edx, [esi + 4]
          call store_dword
          call save_document
        """).Append(Check(1));
      assembly.AppendLine("""
          mov eax, [esi + 4]
          test eax, eax
          jnz test_fail
        """);
      assembly.Append(Pending("input")).AppendLine("  call open_document").Append(Check(1));
      assembly.AppendLine("""
          push 65536
          lea eax, [esi + 528]
          push eax
          mov eax, [esi]
          push eax
          call [GetWindowTextA]
          push test_loaded
          lea eax, [esi + 528]
          push eax
          call [strcmp]
          add esp, 8
        """).Append(Check(0));

      foreach (string key in new[] { "maximum", "oversized", "nul", "empty" })
      {
        bool rejected = key is "oversized" or "nul";
        assembly.AppendLine("""
            xor eax, eax
            lea edx, [esi + 4]
            call store_dword
          """).Append(Pending(key)).AppendLine("  call open_document").Append(Check(rejected ? 0 : 1));
        if (rejected)
        {
          // Rejected reads must retain the previously loaded 65535-byte document.
          assembly.AppendLine("""
              mov eax, [esi]
              push eax
              call [GetWindowTextLengthA]
            """).Append(Check(65535));
        }
      }

      assembly.Append(Pending("empty_saved")).AppendLine("  call save_as_document").Append(Check(1));
      assembly.Append(Pending("bad")).AppendLine("""
          mov eax, 1
          lea edx, [esi + 4]
          call store_dword
          call write_pending
        """).Append(Check(0));
      assembly.AppendLine("  mov eax, [esi + 4]").Append(Check(1));
      assembly.AppendLine("""
          push ebx
          call [DestroyWindow]
          push esi
          call [GlobalFree]
          push 0
          call [ExitProcess]
        test_fail:
          push 99
          call [ExitProcess]
        """);

      // Give each assertion a distinct exit code and print its assembly context on failure.
      var branches = new List<string>();
      string harness = assembly.ToString().ReplaceLineEndings("\n");
      string diagnosticSource = harness;
      harness = Regex.Replace(harness, @"(jz|jnz) test_fail\b", match =>
      {
        int contextStart = Math.Max(0, match.Index - 180);
        branches.Add(diagnosticSource[contextStart..(match.Index + match.Length)]);
        return $"{match.Groups[1].Value} test_failure_{branches.Count}";
      });
      for (int i = 1; i <= branches.Count; i++)
        harness += $"\ntest_failure_{i}:\n  push {i}\n  call [ExitProcess]\n";

      string harnessSource = Path.Combine(output, "runtime-check.zasm");
      string harnessExecutable = Path.ChangeExtension(harnessSource, ".exe");
      File.WriteAllText(harnessSource, harness, new UTF8Encoding(false));
      await CompileAsync(compiler, harnessSource, harnessExecutable);
      var run = await ExecuteAsync(harnessExecutable, [], TimeSpan.FromSeconds(20));
      string context = run.ExitCode > 0 && run.ExitCode <= branches.Count ? branches[run.ExitCode - 1] : "";
      Require(run.ExitCode == 0, $"Runtime check failed with exit code {run.ExitCode}:\n{context}\n{run.Output}");
      Require(File.ReadAllBytes(paths["saved"]).SequenceEqual(Encoding.ASCII.GetBytes("Revised text\r\nSecond line")),
        "Saved text bytes differ from the expected document.");
      Require(new FileInfo(paths["empty_saved"]).Length == 0, "The saved empty file is not empty.");
      Console.WriteLine("PASS: Save As, Save current path, Open, exact text bytes, empty files, 65535-byte boundary, oversized/NUL rejection, failed-write dirty state, and stack balance.");
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine("FAIL: " + ex.Message);
      return 1;
    }
  }

  private static string SourcePath([CallerFilePath] string path = "") => path;

  private static string ReadText(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");

  private static void Require(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  private static int Find(string text, string marker, int start = 0)
  {
    int index = text.IndexOf(marker, start, StringComparison.Ordinal);
    Require(index >= 0, "Cannot build runtime harness: missing " + marker.Trim());
    return index;
  }

  private static async Task CompileAsync(string compiler, string source, string output)
  {
    var result = await ExecuteAsync(compiler, [source, output], TimeSpan.FromSeconds(60));
    Require(result.ExitCode == 0, $"Compilation failed for {source}:\n{result.Output}");
  }

  private static async Task<(int ExitCode, string Output)> ExecuteAsync(string file, string[] arguments, TimeSpan timeout)
  {
    var start = new ProcessStartInfo(file)
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true,
    };
    foreach (string argument in arguments)
      start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start " + file);
    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
    Task<string> stderr = process.StandardError.ReadToEndAsync();
    using var cancellation = new CancellationTokenSource(timeout);
    try
    {
      await process.WaitForExitAsync(cancellation.Token);
    }
    catch (OperationCanceledException)
    {
      if (!process.HasExited)
        process.Kill(entireProcessTree: true);
      await process.WaitForExitAsync();
      throw new TimeoutException($"{Path.GetFileName(file)} exceeded {timeout.TotalSeconds} seconds.");
    }

    return (process.ExitCode, await stdout + await stderr);
  }

  private static string Pending(string key) => $"""
      push test_path_{key}
      lea eax, [esi + 268]
      push eax
      call [strcpy]
      add esp, 8
    """ + "\n";

  private static string Text(string key) => $"""
      push test_{key}
      mov eax, [esi]
      push eax
      call [SetWindowTextA]
      test eax, eax
      jz test_fail
    """ + "\n";

  private static string Check(int result) => $"""
      cmp eax, {result}
      jnz test_fail
      cmp esp, ebp
      jnz test_fail
    """ + "\n";

  private const string Setup = """
    test_start:
      lea ebp, [esp]
      push 66080
      push 0x40
      call [GlobalAlloc]
      test eax, eax
      jz test_fail
      push eax
      pop esi
      push 0
      push 0
      push 0
      push 0
      push 100
      push 100
      push 0
      push 0
      push 0x80000000
      push empty_text
      push test_parent_class
      push 0
      call [CreateWindowExA]
      test eax, eax
      jz test_fail
      push eax
      pop ebx
      push 0
      push 0
      push 201
      push ebx
      push 100
      push 100
      push 0
      push 0
      push 0x40B110C4
      push empty_text
      push edit_class
      push 0
      call [CreateWindowExA]
      test eax, eax
      jz test_fail
      lea edx, [esi]
      call store_dword
      push 0
      push 65535
      push 0xC5
      mov eax, [esi]
      push eax
      call [SendMessageA]
    """;
}
