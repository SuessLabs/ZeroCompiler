[CmdletBinding()]
param (
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$solution = Join-Path $PSScriptRoot 'source/Zero.slnx'

# Use a single MSBuild worker and avoid depending on existing build servers.
dotnet build $solution --configuration $Configuration --disable-build-servers --maxcpucount:1
exit $LASTEXITCODE
