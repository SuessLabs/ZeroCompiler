[CmdletBinding()]
param(
  [ValidateSet('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
  [string]$Runtime = 'win-x64',
  [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
  [string]$CompilerVersion,
  [string]$IdeVersion,
  [string]$MakeNsis = 'makensis',
  [switch]$StageOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $repo "output/installers/$Runtime"
# Fresh staging avoids shipping files left over from a different runtime or version.
$work = Join-Path $destination ([guid]::NewGuid().ToString('N'))
$cli = Join-Path $work 'cli'
$ide = Join-Path $work 'ide'
$versions = @()
if ($CompilerVersion) { $versions += "-p:CompilerVersion=$CompilerVersion" }
if ($IdeVersion) { $versions += "-p:IdeVersion=$IdeVersion" }
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
  & $Command @Arguments
  if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
}
function Read-Version([string]$Project) {
  $result = & dotnet msbuild (Join-Path $repo "source/$Project/$Project.csproj") -nologo -getProperty:Version @versions
  if ($LASTEXITCODE -ne 0) { throw "Cannot read $Project version" }
  return ($result -join '').Trim()
}
function Copy-Content([string]$Folder, [string]$Target) {
  foreach ($file in Get-ChildItem -LiteralPath $Folder -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($Folder, $file.FullName)
    # Never package local IDE build products or documentation build/cache folders.
    if ($relative -match '(^|[\\/])(\.zeroide|\.git|_site|backup|obj|bin)([\\/]|$)' -or
        $file.Extension -in @('.exe', '.dll', '.pdb', '.bin', '.o', '.csv')) { continue }
    $targetPath = Join-Path $Target $relative
    New-Item -ItemType Directory (Split-Path $targetPath -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $targetPath
  }
}
$CompilerVersion = Read-Version 'Zero.Cli'
$IdeVersion = Read-Version 'Zero.Ide'
# Numeric Windows versions are also used for safe upgrade comparisons.
foreach ($version in @($CompilerVersion, $IdeVersion)) {
  if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw 'Installer versions must have three or four numeric components. Pass stable CompilerVersion and IdeVersion values.'
  }
}
$windows = $Runtime.StartsWith('win-')
if (($windows -and !$IsWindows) -or ($Runtime.StartsWith('linux-') -and !$IsLinux) -or ($Runtime.StartsWith('osx-') -and !$IsMacOS)) {
  throw 'Build installers on the target operating system (Native AOT requires a native toolchain).'
}
$rawCli = Join-Path $work 'publish-cli'
Invoke-Checked 'dotnet' (@('publish', (Join-Path $repo 'source/Zero.Cli/Zero.Cli.csproj'), '-c', $Configuration,
  '-r', $Runtime, '--self-contained', 'true', '-o', $rawCli, '--disable-build-servers', '--maxcpucount:1') + $versions)
New-Item -ItemType Directory -Path "$cli/bin", "$cli/docs", "$ide/ide" -Force | Out-Null
$extension = if ($windows) { '.exe' } else { '' }
Copy-Item -LiteralPath "$rawCli/zcc$extension" -Destination "$cli/bin/zero$extension"
foreach ($folder in @('include', 'samples')) {
  Copy-Content (Join-Path $repo $folder) (Join-Path $cli $folder)
}
# Ship source documentation and its images, without site build/cache folders.
Copy-Content (Join-Path $repo 'docs') "$cli/docs/compiler"
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination "$cli/docs/LICENSE.txt"
Copy-Item -LiteralPath (Join-Path $repo 'readme.md') -Destination "$cli/docs/readme.md"
Invoke-Checked 'dotnet' (@('publish', (Join-Path $repo 'source/Zero.Ide/Zero.Ide.csproj'), '-c', $Configuration,
  '-r', $Runtime, '--self-contained', 'true', '-o', "$ide/ide", '--disable-build-servers', '--maxcpucount:1') + $versions)
if ($windows) { Copy-Item "$PSScriptRoot/windows/Set-ZeroPath.ps1" "$cli/bin/Set-ZeroPath.ps1" }
@{ Runtime = $Runtime; CompilerVersion = $CompilerVersion; IdeVersion = $IdeVersion; Cli = $cli; Ide = $ide } |
  ConvertTo-Json | Set-Content "$destination/staging.json" -Encoding utf8
if ($StageOnly) { Write-Output "Staged payloads: $work"; return }

if ($windows) {
  # Uninstall only packaged files. Preserve user-created files and the other product.
  foreach ($component in @('cli', 'ide')) {
    $payload = if ($component -eq 'cli') { $cli } else { $ide }
    $commands = @(Get-ChildItem $payload -File -Recurse | ForEach-Object {
      $relative = [IO.Path]::GetRelativePath($payload, $_.FullName).Replace('/', '\').Replace('$', '$$')
      'Delete "$INSTDIR\' + $relative + '"'
    })
    $commands += @(Get-ChildItem $payload -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
      'RMDir "$INSTDIR\' + [IO.Path]::GetRelativePath($payload, $_.FullName).Replace('/', '\').Replace('$', '$$') + '"'
    })
    $commands | Set-Content "$work/uninstall-$component.nsh" -Encoding utf8
  }
  $cliSetup = Join-Path $destination "Zero.Cli-$CompilerVersion-$Runtime-Setup.exe"
  $ideSetup = Join-Path $destination "Zero.IDE-$IdeVersion-$Runtime-Setup.exe"
  $common = @('/V3', "/DCLI_VERSION=$CompilerVersion", "/DIDE_VERSION=$IdeVersion", "/DSTAGE=$work")
  Invoke-Checked $MakeNsis ($common + @("/DOUTPUT=$cliSetup", "$PSScriptRoot/windows/cli.nsi"))
  Invoke-Checked $MakeNsis ($common + @("/DCLI_SETUP=$cliSetup", "/DOUTPUT=$ideSetup", "$PSScriptRoot/windows/ide.nsi"))
} elseif ($IsLinux) {
  & "$PSScriptRoot/linux/package.ps1" -Cli $cli -Ide $ide -CompilerVersion $CompilerVersion -IdeVersion $IdeVersion -Runtime $Runtime -Destination $destination
} else {
  & "$PSScriptRoot/macos/package.ps1" -Cli $cli -Ide $ide -CompilerVersion $CompilerVersion -IdeVersion $IdeVersion -Destination $destination
}
Get-ChildItem $destination -File | Where-Object Extension -in @('.exe', '.deb', '.rpm', '.pkg') | ForEach-Object {
  $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
  "$hash  $($_.Name)"
} | Set-Content "$destination/SHA256SUMS" -Encoding utf8
Write-Output "Installers: $destination"
