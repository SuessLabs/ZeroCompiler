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
# Windows x64: install NSIS 3.11+ and put makensis on PATH, or pass its full path.
./installer/build-installers.ps1 -MakeNsis 'C:/Program Files (x86)/NSIS/makensis.exe'

# Debian/Ubuntu and RPM distributions: install nFPM and the Native AOT toolchain.
./installer/build-installers.ps1 -Runtime linux-x64
./installer/build-installers.ps1 -Runtime linux-arm64

# macOS: install Xcode command line tools (pkgbuild, productbuild, clang).
./installer/build-installers.ps1 -Runtime osx-arm64
./installer/build-installers.ps1 -Runtime osx-x64

# Override independent versions, or publish payloads without invoking packaging tools.
./installer/build-installers.ps1 -CompilerVersion 0.0.5.0 -IdeVersion 0.1.1.0 -StageOnly
```

Artifacts and SHA256SUMS appear in `output/installers/<runtime>/`. `staging.json`
records the exact payload directories and versions. Existing `build.ps1` and its
portable publish output are unchanged. Build and test before packaging releases.

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
testing and, where applicable, signing/notarization. No installer checks for or
downloads updates; users run a newer installer or use their package manager.
