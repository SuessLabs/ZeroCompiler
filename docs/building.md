# Build Zero

## Building the Native compiler

Run `.\build.bat` (or `.\build.ps1`) to build the solution and publish a
self-contained Native AOT compiler as both `publish\zcc.exe` and
`publish\zero.exe`, with the contents of `samples` copied into `publish\samples`
and the shared `.zh` headers copied into `publish\include`.
Headers retain their platform subfolders: `windows` contains working PE imports;
`linux` contains draft declarations/reference files for future ELF support.

Both names run the same compiler; the build copies the native executable rather
than compiling it twice. Both executables are included in release ZIPs.

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
the defaults; the compiler path must point to the managed `zcc.dll`.

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
and inspects/executes `hello-linux64-cli` on Ubuntu. See [Linux x64](linux64.md).

## Build version

The compiler and CLI share the numeric version in `source/Directory.Build.props`
(`0.0.4.6`). The IDE maintains its own version in `source/Zero.Ide/Version.props`
(starting at `0.1.0.0`). Both use `Major.Minor.Revision.Build`. Each version source
also has its own `VersionSuffix`; the release workflow clears both on `master`. The SDK
derives numeric assembly/file versions and the informational version from these
properties. `Zero.Version` and the CLI banner read the library's generated
informational version, including commit metadata when the SDK supplies it.

CI/CD can supply a complete version without modifying source files:

```powershell
.\build.bat -CompilerVersion 0.0.4.7-ci.123 -IdeVersion 0.1.0.1-ci.123

# Or publish a stable release:
.\build.ps1 -CompilerVersion 0.0.5.0 -IdeVersion 0.1.1.0
```

Please see [builds-AutoVersioning.md](builds-AutoVersioning.md) for more information on automatic-versioning on PRs.

The build script passes the independent versions to solution build and native
publish. `-Version` remains an alias for `-CompilerVersion` and does not override
the IDE version. Direct `dotnet` commands can use
`-p:CompilerVersion=0.0.4.7-ci.123 -p:IdeVersion=0.1.0.1-ci.123`.
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
.\publish\zcc.exe publish\samples\hello-console.zasm publish\hello-console.exe

# Run it
.\publish\hello-console.exe
```

The program prints `Hello from Zero!` followed by a newline and exits with code 0
(or code 1 if writing fails). It uses `#IMPORT` to call the Windows functions
`GetStdHandle`, `WriteFile`, and `ExitProcess`. The message is stored as numeric
ASCII bytes because `DB` currently accepts numeric operands. Run it from an
existing terminal to keep the output visible.
