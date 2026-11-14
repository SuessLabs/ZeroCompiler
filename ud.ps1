#Requires -Version 7.0
[CmdletBinding()]
param([switch]$Force)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'installer/dependencies.ps1')

$toolsDirectory = Join-Path $PSScriptRoot 'tools'
$downloads = Join-Path $toolsDirectory 'downloads'

function Install-ZeroTool($Tool)
{
  New-Item -ItemType Directory $downloads, $Tool.Directory -Force | Out-Null
  $archive = Join-Path $downloads $Tool.Archive
  $verified = !$Force -and (Test-Path -LiteralPath $archive -PathType Leaf) -and
    ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ieq $Tool.Sha256)

  if (!$verified)
  {
    $temporary = Join-Path $downloads "$([guid]::NewGuid().ToString('N')).download"
    try
    {
      foreach ($url in $Tool.Urls)
      {
        try
        {
          Write-Host "Downloading $($Tool.Name) $($Tool.Version): $url"
          Invoke-WebRequest -Uri $url -OutFile $temporary -TimeoutSec 45
          if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ine $Tool.Sha256)
          {
            throw 'The downloaded archive does not match the pinned SHA-256.'
          }

          Move-Item -LiteralPath $temporary -Destination $archive -Force
          $verified = $true
          break
        }
        catch { Write-Warning "Could not use ${url}: $($_.Exception.Message)" }
      }

      if (!$verified) {
        throw "Could not download a verified $($Tool.Name) archive. Retry ./ud.ps1 when the download source is available." }
    }
    finally
    {
      if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force }
    }
  }
  else
  {
    Write-Host "Using verified cached archive: $archive"
  }

  if ($Force -or !(Test-Path -LiteralPath $Tool.Executable -PathType Leaf))
  {
    if ($archive.EndsWith('.zip'))
    {
      Expand-Archive -LiteralPath $archive -DestinationPath $Tool.Directory -Force
    }
    else
    {
      & tar -xzf $archive -C $Tool.Directory
      if ($LASTEXITCODE -ne 0) {
        throw "Could not extract $archive" }
    }
  }

  if (!(Test-Path -LiteralPath $Tool.Executable -PathType Leaf)) {
    throw "Tool executable missing after extraction: $($Tool.Executable)" }

  $versionFlag = if ($Tool.Name -eq 'nsis') { '/VERSION' } else { '--version' }

  $reportedVersion = & $Tool.Executable $versionFlag

  if ($LASTEXITCODE -ne 0) {
    throw "$($Tool.Name) could not start." }

  Write-Host ($reportedVersion -join [Environment]::NewLine)
  Write-Host "Ready: $($Tool.Executable)"
}

if (!(Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue))
{
  throw 'Install the .NET 10 SDK before running ud.ps1. SDKs and native development toolchains are system prerequisites.'
}

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or !($sdks -match '^10\.')) {
  throw 'Install the .NET 10 SDK before running ud.ps1.' }

if ($IsWindows)
{
  Install-ZeroTool (Get-ZeroToolDefinition 'nsis')
}
elseif ($IsLinux)
{
  Install-ZeroTool (Get-ZeroToolDefinition 'nfpm')
}
elseif ($IsMacOS)
{
  foreach ($command in @('pkgbuild', 'productbuild', 'clang'))
  {
    if (!(Get-Command $command -CommandType Application -ErrorAction SilentlyContinue))
    {
      throw "Missing $command. Install Apple's command line tools with xcode-select --install."
    }
  }
  Write-Host 'macOS uses the installed Apple packaging tools; no additional packaging download is required.'
}
else
{
  throw 'Unsupported operating system.'
}

Write-Host 'Packaging dependencies are ready. NuGet dependencies are restored by build.ps1.'
