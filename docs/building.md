# Build Zero

## Building the Native compiler

Run `.\build.bat` (or `.\build.ps1`) to build the solution and publish a
self-contained Native AOT compiler to `publish\zcc.exe`, with the contents of
`docs\samples` copied into `publish\samples`.
This requires the .NET 10 SDK and Visual Studio's **Desktop development with C++**
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

## Build version

The numeric version in `source/Directory.Build.props` uses
`Major.Minor.Revision.Build` (initially `0.0.2.10`). `VersionSuffix=alpha1`
adds a development suffix; the release workflow clears it on `master`. The SDK
derives numeric assembly/file versions and the informational version from these
properties. `Zero.Version` and the CLI banner read the library's generated
informational version, including commit metadata when the SDK supplies it.

CI/CD can supply a complete version without modifying source files:

```powershell
.\build.bat -Version 0.0.2.11-ci.123

# Or publish a stable release:
.\build.ps1 -Version 0.0.3.0
```

Please see [builds-AutoVersioning.md](builds-AutoVersioning.md) for more information on automatic-versioning on PRs.

The build script passes the version to both solution build and native publish.
Direct `dotnet` commands can use `-p:Version=0.0.2.11-ci.123`, or
`-p:VersionPrefix=0.0.2.11 -p:VersionSuffix=ci.123`. Pass the same properties when
building, testing, and publishing in separate CI steps. The public `Zero.Version`
member is now a static property rather than a compile-time constant.

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
