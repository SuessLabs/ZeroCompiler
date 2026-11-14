#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../windows/Check-ZeroUpdate.ps1')

function Assert-Status($Release, $Product, $Current, $Expected, $Latest = '')
{
  $actual = Get-ZeroInstallerUpdate $Release $Product $Current
  if ($actual.Status -ne $Expected -or $actual.LatestVersion -ne $Latest)
  {
    throw "Expected $Expected/$Latest, got $($actual.Status)/$($actual.LatestVersion)"
  }
}
function New-Asset([string]$Name) { return @{ name = $Name; state = 'uploaded'; size = 1024 } }
$release = @{ draft = $false; prerelease = $false; tag_name = 'v99.0.0'; assets = @(
  (New-Asset 'Zero.Cli-0.0.4.9-win-x64-Setup.exe'),
  (New-Asset 'Zero.Cli-0.0.4.12-win-x64-Setup.exe'),
  (New-Asset 'Zero.IDE-0.1.0.6-win-x64-Setup.exe'),
  (New-Asset 'Zero.Cli-99.0.0-win-arm64-Setup.exe'),
  (New-Asset 'Zero.Cli-99.0.0-preview-win-x64-Setup.exe'),
  (New-Asset 'Zero.Cli-99999999999999999999.0.0-win-x64-Setup.exe')
) }
Assert-Status $release Cli '0.0.4.9' Available '0.0.4.12'
Assert-Status $release Ide '0.1.0.5' Available '0.1.0.6'
Assert-Status $release Cli '0.0.4.12' Current '0.0.4.12'
Assert-Status $release Cli '0.0.5.0' Current '0.0.4.12'
Assert-Status @{ draft = $false; prerelease = $false; assets = @((New-Asset 'Zero.Cli-1.2.3-win-x64-Setup.exe')) } Cli '1.2.3.0' Current '1.2.3'
$release.prerelease = $true
Assert-Status $release Cli '0.0.0.0' Missing
$release.prerelease = $false
$release.draft = $true
Assert-Status $release Cli '0.0.0.0' Missing
$release.draft = $false
$release.assets = @((New-Asset 'Zero.IDE-0.1.0.6-win-x64-Setup.exe'))
Assert-Status $release Cli '0.0.0.0' Missing
$release.assets = @(@{ name = 'Zero.Cli-9.0.0-win-x64-Setup.exe'; state = 'new'; size = 0 })
Assert-Status $release Cli '0.0.0.0' Missing

# Simulate offline/rate-limited GitHub and ensure setup receives a usable fallback.
function Invoke-RestMethod { throw 'Simulated offline request.' }
$resultFile = Join-Path ([IO.Path]::GetTempPath()) ("zero-update-test-$([guid]::NewGuid().ToString('N')).ini")
try
{
  Invoke-ZeroInstallerUpdateCheck Cli '1.2.3.4' $resultFile
  if ((Get-Content $resultFile) -notcontains 'Status=Unavailable') { throw 'Offline fallback failed.' }
  function Invoke-RestMethod { return @{ draft = $false; prerelease = $false; assets = @((New-Asset 'Zero.IDE-1.2.3.5-win-x64-Setup.exe')) } }
  Invoke-ZeroInstallerUpdateCheck Ide '1.2.3.4' $resultFile
  if ((Get-Content $resultFile) -notcontains 'Status=Available' -or (Get-Content $resultFile) -notcontains 'LatestVersion=1.2.3.5') { throw 'Result serialization failed.' }
}
finally { if (Test-Path -LiteralPath $resultFile) { Remove-Item -LiteralPath $resultFile } }
Write-Host 'Installer update checks passed (version ordering, independent products, stable assets, missing assets, offline fallback, and result serialization).'
