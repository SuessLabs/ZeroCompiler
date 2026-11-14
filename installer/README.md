# Zero installers

Two products share one installation root:

```text
Zero/
  bin/zero.exe       # zero on Linux/macOS
  include/
  samples/
  docs/
  ide/Zero.Ide.exe   # Zero.Ide on Linux/macOS, with all IDE DLLs here
```

The CLI package owns `bin`, `include`, `samples`, and `docs`. The IDE package
owns `ide` and its launcher. The IDE discovers `../bin/zero` automatically for
new or blank settings; explicit compiler selections remain authoritative. Includes resolve
from `../include` when the compiler lives in `bin`.

## Build

Run from the repository root with PowerShell 7, the .NET 10 SDK, and the platform's
Native AOT prerequisites. Build on the target OS. Each run publishes self-contained
CLI and IDE payloads into a fresh staging directory; the IDE needs no installed
.NET runtime. Versions default to the projects' independent version properties.
Installer builds require three- or four-component numeric release versions.

```powershell
# Download verified, pinned packaging dependencies into the Git-ignored tools folder.
./ud.ps1

# Build, validate samples, and generate both installers (also runs ud.ps1).
./publish.ps1

# Windows packaging only: automatically uses tools/nsis/<version>/.../makensis.exe.
./installer/build-installers.ps1

# Linux: ud.ps1 downloads nFPM for the host. Install the Native AOT toolchain first.
./publish.ps1 -Runtime linux-x64
./publish.ps1 -Runtime linux-arm64

# macOS: install Xcode command line tools (pkgbuild, productbuild, clang).
./publish.ps1 -Runtime osx-arm64
./publish.ps1 -Runtime osx-x64

# Override independent versions, or publish payloads without invoking packaging tools.
./installer/build-installers.ps1 -CompilerVersion 0.0.5.0 -IdeVersion 0.1.1.0 -StageOnly
```

Installers and their `SHA256SUMS` appear directly in `publish/`, alongside the
portable compiler output. Staged payloads remain under `output/installers/<runtime>/`;
`staging.json` there records their paths and versions. Both directories are ignored
by Git. The installer checksum file covers installer packages, not the portable
compiler executables. Standalone packaging can override `-Destination`.

`publish.ps1` defaults to the host runtime and forwards `-Configuration`, `-Runtime`,
`-OptimizationPreference`, `-CompilerVersion` (alias `-Version`), and `-IdeVersion`.
It stops on dependency/build/packaging failure and reuses the compiler just built by
`build.ps1`, avoiding a second Native AOT publish. It does not upload releases or
install the generated packages. Run unit tests separately before releasing.

`ud.ps1` downloads only packaging tools for the host: NSIS on Windows and nFPM on
Linux. Versions, source URLs, and SHA-256 hashes are pinned in
`installer/dependencies.json`; update that file when upgrading a tool. Archives are
cached in `tools/downloads`, with extracted executables under `tools/nsis` or
`tools/nfpm`. Subsequent runs verify and reuse cached archives without downloading;
`./ud.ps1 -Force` downloads and extracts them again. Builds use explicit local tool
paths without changing PATH or installing NSIS globally. Custom tools remain
available through packaging's `-MakeNsis` and `-Nfpm` parameters.

PowerShell 7, the .NET 10 SDK, and native toolchains remain system prerequisites:
Visual Studio C++/Windows SDK on Windows, clang and development libraries on Linux,
and Xcode command line tools on macOS. Apple packaging tools are used in place;
they are not copied into `tools`. NuGet packages are restored normally by the build.

## Windows / NSIS

The CLI setup installs to `C:\Program Files\SuessLabs\Zero` by default and offers
a custom destination and an unchecked **Add Zero CLI to the system PATH** option.
Both products use the same root; installers reuse it on upgrades and reject a
different root while either product remains installed. To relocate, uninstall
the IDE and then CLI first. Administrator access is required.

The IDE setup embeds the actual CLI setup and invokes it silently, passing its
selected root and PATH option. CLI and IDE appear separately in Installed Apps.
The CLI installer refuses downgrades; the IDE installer likewise refuses IDE
downgrades. Reinstalling the same version repairs packaged files. A newer CLI
already installed is retained. PATH updates preserve unexpanded entries, avoid
duplicates, and remove an entry only if Zero added it. Reopen terminals afterward.
PowerShell is used for PATH operations, without any downloaded NSIS plug-ins.

The Windows setup wizard has an optional **Check for updates** page. Its button
queries the latest stable GitHub release at `SuessLabs/ZeroCompiler`, compares the
matching Windows x64 installer asset against this package's version, and offers
to open the release download page. CLI and IDE versions are compared independently.
Checking never downloads or runs another installer. Click Next to use the bundled
version, even when a newer one exists. Offline errors, API rate limits, and missing
assets do not block installation. Silent setup (including the IDE's embedded CLI)
skips the page and makes no update request. No background updater is installed.
Release automation publishes both installers and SHA256SUMS alongside the portable
ZIP. Existing releases containing only ZIP files report no matching installer.
Run `./installer/tests/test-update-check.ps1` to test without accessing GitHub.

Uninstall the IDE first, then CLI. IDE uninstall leaves CLI intact. CLI uninstall
is blocked while the IDE is registered. Uninstall removes only files listed in
the built payload, preserving extra user files. Do not edit installed samples;
copy them to a writable workspace. Settings remain in the user's application-data
directory. Upgrades can leave obsolete files from previous versions; uninstall
before upgrading if dependency filenames change.

```powershell
# Silent setup; /D must be last and is intentionally unquoted in NSIS syntax.
./Zero.Cli-0.0.5.0-win-x64-Setup.exe /S /ADDPATH=1 /D=C:\Tools\Zero
```

## Linux / nFPM

Produces CLI and IDE `.deb` and `.rpm` packages. Files live under
`/opt/suesslabs/zero`, with `/usr/bin/zero` linking to `bin/zero`. This avoids
editing shell profiles. The IDE adds a desktop entry and depends on a compatible
CLI package. Install both downloaded files in one transaction using `apt install
./zero-*.deb` or `dnf install ./zero-*.rpm`; native packages do not embed or run
another installer. Package locations are fixed. The Avalonia runtime additionally
needs a working desktop/X11 environment; verify dependencies on each supported
distribution before releasing.

## macOS / Apple packaging tools

Produces a CLI `.pkg` and an IDE `.pkg` containing both component packages with
stable identifiers. Payloads live under `/Library/SuessLabs/Zero`. A Finder launcher
is installed at `/Applications/Zero IDE.app`; the real executable and its DLLs
remain under `Zero/ide`. `/usr/local/bin/zero` links to the compiler. Installation
locations are fixed. Apple packages do not provide an uninstaller; package receipts
are not a dependency manager. Keep the CLI when removing the IDE.

These are unsigned development packages. Public distribution still needs platform
testing and, where applicable, signing/notarization. Updates remain user initiated:
use the Windows setup check, run a newer installer, or use the package manager.
