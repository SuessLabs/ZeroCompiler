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

# Native AOT compilation happens during publish, rather than dotnet build.
$cliProject = Join-Path $PSScriptRoot 'source/Zero.Cli/Zero.Cli.csproj'
$publishDirectory = Join-Path $PSScriptRoot 'publish'
dotnet publish $cliProject --configuration $Configuration --runtime $Runtime --self-contained true --output $publishDirectory --disable-build-servers --maxcpucount:1 "-p:OptimizationPreference=$OptimizationPreference" @versionArguments
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

# Copy directory
$samplesDirectory = Join-Path $PSScriptRoot 'samples'
$publishedSamplesDirectory = Join-Path $publishDirectory 'samples'
New-Item -ItemType Directory -Path $publishedSamplesDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $samplesDirectory | Copy-Item -Destination $publishedSamplesDirectory -Recurse -Force
exit 0
