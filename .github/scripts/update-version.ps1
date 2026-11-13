#Requires -Version 7.2
[CmdletBinding()]
param(
  [string]$TargetBranch = $env:TARGET_BRANCH,
  [string]$PullRequest = $env:PR_NUMBER,
  [string]$Directory = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force

try {
  $result = Update-ZeroVersion -Branch $TargetBranch -PullRequest $PullRequest -Directory $Directory
  if ($env:GITHUB_OUTPUT) {
    [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT,
      "version=$($result.version)`nide_version=$($result.ideVersion)`ntag=$($result.tag)`ncommit=$($result.commit)`n")
  }
  $result | ConvertTo-Json -Compress
} catch {
  Write-Error $_ -ErrorAction Continue
  exit 1
}
