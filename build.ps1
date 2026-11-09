[CmdletBinding()]
param (
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',

  [ValidateSet('win-x64', 'win-arm64')]
  [string]$Runtime = 'win-x64',

  [ValidateSet('Size', 'Speed')]
  [string]$OptimizationPreference = 'Size',

  [ValidateNotNullOrEmpty()]
  [string]$Version
)

$ErrorActionPreference = 'Stop'

$solution = Join-Path $PSScriptRoot 'source/Zero.slnx'
$versionArguments = @()
if ($PSBoundParameters.ContainsKey('Version')) {
  $versionArguments += "-p:Version=$Version"
}

# Use a single MSBuild worker and avoid depending on existing build servers.
dotnet build $solution --configuration $Configuration --disable-build-servers --maxcpucount:1 @versionArguments
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

# Validate all sources with the managed compiler before the slower native publish.
& (Join-Path $PSScriptRoot 'validate-samples.ps1') -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

# Native AOT compilation happens during publish, rather than dotnet build.
$cliProject = Join-Path $PSScriptRoot 'source/Zero.Cli/Zero.Cli.csproj'
$publishDirectory = Join-Path $PSScriptRoot 'publish'
dotnet publish $cliProject --configuration $Configuration --runtime $Runtime --self-contained true --output $publishDirectory --disable-build-servers --maxcpucount:1 "-p:OptimizationPreference=$OptimizationPreference" @versionArguments
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

# Publish a second command name for the same standalone Native AOT executable.
Copy-Item -LiteralPath (Join-Path $publishDirectory 'zcc.exe') -Destination (Join-Path $publishDirectory 'zero.exe') -Force

# Copy directory
$samplesDirectory = Join-Path $PSScriptRoot 'samples'
$publishedSamplesDirectory = Join-Path $publishDirectory 'samples'
New-Item -ItemType Directory -Path $publishedSamplesDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $samplesDirectory | Copy-Item -Destination $publishedSamplesDirectory -Recurse -Force

# Ship standard headers beside both compiler command names.
$includeDirectory = Join-Path $PSScriptRoot 'include'
$publishedIncludeDirectory = Join-Path $publishDirectory 'include'
New-Item -ItemType Directory -Path $publishedIncludeDirectory -Force | Out-Null
# Remove only the former flat copies of shipped Windows headers during migration.
Get-ChildItem -LiteralPath (Join-Path $includeDirectory 'windows') -Filter '*.zh' -File | ForEach-Object {
  $legacyHeader = Join-Path $publishedIncludeDirectory $_.Name
  if (Test-Path -LiteralPath $legacyHeader -PathType Leaf) {
    Remove-Item -LiteralPath $legacyHeader -Force
  }
}
Get-ChildItem -LiteralPath $includeDirectory | Copy-Item -Destination $publishedIncludeDirectory -Recurse -Force
exit 0
