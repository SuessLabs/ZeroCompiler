[CmdletBinding()]
param (
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [ValidateSet('Size', 'Speed')]
    [string]$OptimizationPreference = 'Size'
)

$ErrorActionPreference = 'Stop'

$solution = Join-Path $PSScriptRoot 'source/Zero.slnx'

# Use a single MSBuild worker and avoid depending on existing build servers.
dotnet build $solution --configuration $Configuration --disable-build-servers --maxcpucount:1
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Native AOT compilation happens during publish, rather than dotnet build.
$cliProject = Join-Path $PSScriptRoot 'source/Zero.Cli/Zero.Cli.csproj'
$publishDirectory = Join-Path $PSScriptRoot "output/zcc/$Runtime"
dotnet publish $cliProject --configuration $Configuration --runtime $Runtime --self-contained true --output $publishDirectory --disable-build-servers --maxcpucount:1 "-p:OptimizationPreference=$OptimizationPreference"
exit $LASTEXITCODE
