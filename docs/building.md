# Build Zero

For the separate CLI and IDE installers, see [Installer builds](../installer/README.md).
These use the shared `Zero/bin`, `Zero/include`, `Zero/samples`, `Zero/docs`, and
`Zero/ide` layout and leave the existing portable build below unchanged.

With PowerShell 7, run `./ud.ps1` to fetch pinned packaging tools into the
Git-ignored `tools/` folder. Run `./publish.ps1` to prepare those dependencies,
run `build.ps1` (including sample validation), and generate both installers in
`publish/`. Installer staging stays under `output/installers/`. Subsequent runs
reuse verified cached downloads. Neither script installs the generated packages.

## Building the Native compiler

Run `.\build.bat` (or `.\build.ps1`) to build the solution and publish a
self-contained Native AOT compiler as `publish\zero.exe`, with the contents of
`samples` copied into `publish\samples`
and the shared `.zh` headers copied into `publish\include`.
Headers retain their platform subfolders: `windows` contains working PE imports;
`linux` contains draft declarations/reference files for future ELF support.

The compiler command is `zero.exe` on Windows and `zero` on Linux/macOS.

Compiling requires the .NET 10 SDK and Visual Studio's **Desktop development with C++**
workload, including the Windows SDK. The published executable runs without an
installed .NET runtime.

The default is Release with size optimization. Native AOT removes JIT startup
work, while trimming removes unused code. Release also omits stack trace metadata
and debug symbols, and uses resource keys for runtime exception messages to save
space; compiler-authored error messages remain readable. The ordinary managed
build outputs remain under `output\Zero.Cli\Release`.

Use `.\build.bat -OptimizationPreference Speed` to favor execution speed over
size, or `-Runtime win-arm64` when building for Windows ARM64 with the corresponding
native toolchain. `-Configuration Debug` keeps debug symbols and stack traces.
Size and maximum execution speed are competing optimization goals.
Each build replaces the published compiler for the selected runtime in the same
`publish` folder.

## Sample validation

Every `build.ps1` / `build.bat` run compiles all `.zasm` files under `samples`,
including subfolders, after building the solution and before Native AOT publishing.
Linux samples declare `#TARGET linux-x64` and produce extensionless ELF64 output;
unmarked samples retain Windows x86 PE32 output with `.exe` extensions.
The build fails if any sample fails. Generated sample executables go to
`output\samples\Release` (or `Debug`), preserving subfolder paths. Samples are
compiled without launching them, so MessageBox samples require no interaction.
The validator supplies the repository's `include` directory to the managed
compiler with `-I`. Tests copy the shared headers beside their compiler output.
See [Using included files](Zero-Assembly-Include-Files.md) for syntax and search order.

After building the solution, run the validator separately with:

```powershell
.\validate-samples.ps1
.\validate-samples.ps1 -Configuration Debug
```

The validator reports every failing file and returns a nonzero exit code on
failure. `-SamplesDirectory`, `-OutputDirectory`, and `-CompilerPath` can override
the defaults; the compiler path must point to the managed `zero.dll`.

Unit tests also discover and compile every sample automatically. Sources are
embedded in the test assembly during build, so renamed/deleted samples do not
linger in discovery and new samples need no manually maintained test list:

```powershell
dotnet test source/Zero.Tests/Zero.Tests.csproj --configuration Release
```

These compilation tests run on any OS supported by .NET. Existing console-output
tests still execute selected samples on Windows to check their behavior.
Native Linux x86-64 runtime tests run under Linux/WSL and skip other hosts. The
PowerShell-only Linux CI workflow runs the tests, validates all sample formats,
and inspects/executes `linux64-cli-01-hello` on Ubuntu. See [Linux x64](linux64.md).

## IDE runtime targets

`Zero.Ide` builds for `win-x64`, `linux-x64`, `linux-arm64`, `linux-arm`,
`osx-x64` (Intel macOS), and `osx-arm64` (Apple Silicon macOS). Builds and
publishes reject other runtime identifiers. A normal build selects the SDK
host's runtime, which must be one of these six. Compiler/CLI targeting is
configured separately.

```powershell
dotnet build source/Zero.Ide/Zero.Ide.csproj -r win-x64
dotnet publish source/Zero.Ide/Zero.Ide.csproj -c Release -r linux-arm64 --self-contained true
dotnet publish source/Zero.Ide/Zero.Ide.csproj -c Release -r osx-arm64 --self-contained true
```

IDE build output is separated by runtime under
`output/Zero.Ide/{Configuration}/{RuntimeIdentifier}/`.

Linux x86 cannot currently be produced: .NET 10 supplies no `linux-x86`
runtime/app host, and the current SkiaSharp 2.88 native assets do not include
Linux x86. The project reports this explicitly rather than producing an
unrunnable package. Linux x64, Arm32, and Arm64 are supported runtime targets;
Linux desktop deployment also requires Avalonia's X11/native dependencies.

## IDE settings

The editor uses Dock.Avalonia document tabs and a dockable **Solution Explorer**.
Open multiple `.zasm` or `.zsrc` files with **File > Open** (Ctrl+O), or create a
new tab with Ctrl+N. Each document has its own text and undo history.
Opening an already-open path selects its existing tab. Tabs can be moved, split,
or floated using their docking context menu or drag handles.

**Save** (Ctrl+S) saves the active tab. **Save All** (Ctrl+Alt+S) saves all dirty
tabs; **Save As** remains Ctrl+Shift+S. Dirty tabs display one trailing `*`.
Middle-clicking a tab, using its close button, or pressing Ctrl+W closes it.
Unsaved changes offer **Save**, **Close Without Saving**, and **Cancel**. Canceled
or failed saves leave the tab open. Exiting the IDE or installing an update checks
all dirty documents, including floating windows.
Save, close, and build shortcuts also work when a native floating editor has focus.

Use **File > Open Folder** or **Open Folder** in Solution Explorer to open a
folder workspace. The tree loads directories on expansion; double-click a source
file to open it, and use Refresh to pick up filesystem changes. Closing the tool
does not close documents. **View > Solutions Explorer** (right after Edit) restores
the tool on the left, including when it has been floated or moved. `.zproj` and
XML solution formats are reserved for a future phase; no project parsing is done yet.

Build and execution commands act on the document active when the command starts.
The compiler currently supports `.zasm`; `.zsrc` files can be opened, edited, and
saved, but are not yet supported by the compiler.

The toolbar provides icon buttons for New, Open, Save, Undo, Redo, and Build and
Execute. Hover over a button for its name and shortcut. The build configuration
selector offers Debug and Release. Play or F5 builds and launches the current
file after successful compilation. **Build > Build** (Ctrl+B) compiles without
launching. Ctrl+Shift+B performs Clean and Build without launching.
The centered status shows `Idle`,
`Building...`, or `Build failure`. Failures remain visible until a successful
build or clean. Diagnostics appear on failure and can be reopened through
**Build > Build Output**.
The search box on the right searches the current file: press Enter to select the
next case-insensitive literal match, wrapping to the start of the document.
The editor highlights the caret's current line using the selected color theme.

Use **Tools > Options** to select the compiler executable and configure editor
appearance and indentation. The tree groups Compiler, Editor > Appearance, and
Editor > Indentation settings. Apply saves and updates the editor immediately;
OK also closes the dialog. Cancel discards edits made since the last Apply.

Under **Editor > Code Cleanup**, enable **Trim Trailing Whitespace** to remove trailing
whitespace from every line on Save (including Ctrl+S), Save As, and save-before-close.
This is off by default. Cleanup preserves indentation and line endings, updates
the editor, and can be undone as one edit. Canceling the save picker leaves the
document unchanged.

Choose the IDE color theme under **Editor > Appearance**, or switch immediately
using **Tools > Themes**. Light and Dark use Visual Studio-inspired neutral colors;
Cool Slate uses a dark blue-gray palette. **Use system setting** is the default
and follows operating system light/dark changes until you choose another theme.
The selection applies to the editor, syntax highlighting, menus, and dialogs.

Settings are stored per user in `Zero/Zero.Ide/settings.ini` beneath .NET's
`ApplicationData` folder. On Windows this is
`%APPDATA%\Zero\Zero.Ide\settings.ini`. The full path is shown in the dialog.
Settings load at startup; a missing file uses the editor defaults. Invalid
numeric or Boolean entries fall back individually. Saves preserve unknown keys
and replace the INI file only after writing the new contents successfully.

```ini
[Compiler]
Path="C:\Tools\Zero\zero.exe"

[Appearance]
Theme="System"

[Editor]
FontFamily="Cascadia Code, Consolas, Menlo, monospace"
FontSize="14"
IndentationSize="2"
ConvertTabsToSpaces="true"
ShowLineNumbers="true"
WordWrap="false"

[CodeCleanup]
TrimTrailingWhitespaceOnSave="false"
```

Set the compiler path to the Zero CLI executable under **Tools > Options > Compiler**
before building. Builds save the current file first (including enabled cleanup);
canceling the save cancels the build. The compiler runs asynchronously with the
source folder as its working directory, preserving relative include resolution.
Play and F5 launch the compiled binary directly with the source folder as its
working directory; the IDE remains editable while the program runs. Windows x86
output requires Windows; Linux x64 output requires an x64 Linux host. An incompatible
target or launch error displays `Run failure`, preserving the successful build
and its diagnostics. Build-only and Clean and Build commands never execute output.

Artifacts are written beneath the source folder in
`.zeroide/<Debug|Release>/<source-file>/`. Windows PE executables use
`<source-name>.exe`; Linux ELF executables use `<source-name>` with no extension.
The builder inspects the generated executable to select its final name, including
when cross-compiling. On Linux, the compiler's executable permissions are preserved.
Switching targets replaces the previous artifact, and Clean also removes legacy
`.bin` artifacts for that source and configuration.
Debug/Release currently separate output folders; the CLI does not yet
provide configuration-specific optimization or debug-symbol flags. Clean removes
only that file's artifact for the selected configuration. The `.zeroide` folders
are ignored by Git.

## IDE updates

Choose **Help > Check for Updates** to check the latest stable GitHub release.
On Windows x64, the IDE compares the IDE setup asset's version with its own
assembly version, independently of the compiler release tag. If a newer installer
is available, choose **Update** to download the combined IDE + CLI setup. The IDE
verifies the published SHA-256 digest (or the release's `SHA256SUMS`) and file size
before starting it. Missing installers, network failures, and checksum failures
leave the IDE open; releases containing only ZIP files cannot be installed this way.

After downloading, unsaved work receives the normal Save / Don't Save / Cancel
prompt. Canceling that prompt, canceling Save As, or declining administrator
permission leaves the IDE open and does not install anything. Once setup starts,
the IDE closes and setup waits for its process to exit before showing the wizard.
The existing shared installation directory is preserved, and the IDE setup runs
the bundled CLI installer as usual. Follow setup to finish the upgrade, then reopen
the IDE. Other running IDE instances must also be closed before replacing files.

Updates run only when requested from the menu. Linux/macOS users should update
through their installer or package manager. Downloaded Windows setup files are
kept in the user's temporary `SuessLabs/Zero/Updates` folder. The IDE never asks
for GitHub credentials and only fetches published assets from `SuessLabs/ZeroCompiler`.

## Build version

Edit all three product versions in `source/Version.props`:

| Property | Projects | Version at centralization |
| --- | --- | --- |
| `CompilerVersion` | `Zero`, `Zero.Cli`, and compiler tests | `0.2.0.100` |
| `IdeVersion` | `Zero.Ide` and IDE tests | `0.2.0.15` |
| `DebuggerVersion` | `Zero.Dbg`, `Zero.Dbg.WinPe`, debugger tests and AOT smoke project | `0.2.0.3` |

All use `Major.Minor.Revision.Build`. `Directory.Build.props` imports the central
file, which selects the appropriate `VersionPrefix` for each project. The SDK
derives numeric assembly/file versions and the informational version from it.
An optional SDK `VersionSuffix` applies to all products; the release workflow
clears any suffix declared in the central file on `master`.
`Zero.Version` and the CLI banner read the library's generated
informational version, including commit metadata when the SDK supplies it.

CI/CD can supply a complete version without modifying source files:

```powershell
.\build.bat -CompilerVersion 0.2.0.101-ci.123 -IdeVersion 0.2.0.16-ci.123 -DebuggerVersion 0.2.0.4-ci.123

# Or publish a stable release:
.\build.ps1 -CompilerVersion 0.2.1.0 -IdeVersion 0.2.1.0 -DebuggerVersion 0.2.1.0
```

Please see [builds-AutoVersioning.md](builds-AutoVersioning.md) for more information on automatic-versioning on PRs.

The build script passes the independent versions to solution build and native
publish. `-Version` remains an alias for `-CompilerVersion` and does not override
the IDE or debugger version. `publish.ps1` accepts the same three version
parameters. Direct `dotnet` commands can use
`-p:CompilerVersion=0.2.0.101-ci.123 -p:IdeVersion=0.2.0.16-ci.123 -p:DebuggerVersion=0.2.0.4-ci.123`.
The standard SDK `-p:Version=...` or `VersionPrefix`/`VersionSuffix` properties
remain available when intentionally applying the same override to every project.
Pass the same properties when
building, testing, and publishing in separate CI steps. The public `Zero.Version`
member is now a static property rather than a compile-time constant.
Versions increment only on merged PRs into `develop` or `master`; local builds
do not change them. The IDE About dialog reads the editor and compiler assembly
versions independently.

## Running Samples

### Console Assembly Sample

From a PowerShell terminal in the repository root, build the compiler, compile
the Windows x86 assembly sample, and run the resulting executable:

```powershell
# Compile Zero Assembly (ZASM)
.\build.bat
.\publish\zero.exe publish\samples\Windows\win-cli-03-console-write.zasm publish\win-cli-03-console-write.exe

# Run it
.\publish\win-cli-03-console-write.exe
```

The program prints `Hello from Zero!` followed by a newline and exits with code 0
(or code 1 if writing fails). It uses `#IMPORT` to call the Windows functions
`GetStdHandle`, `WriteFile`, and `ExitProcess`. The message is stored as numeric
ASCII bytes because `DB` currently accepts numeric operands. Run it from an
existing terminal to keep the output visible.
