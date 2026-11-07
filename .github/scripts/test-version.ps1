#Requires -Version 7.2
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Versioning.psm1') -Force

$script:TestsPassed = 0
function Assert-Equal {
  param($Actual, $Expected)
  if ($Actual -cne $Expected) { throw "Expected '$Expected', received '$Actual'." }
}
function Assert-Throws {
  param([scriptblock]$Action, [string]$MessagePattern = '.')
  try { $null = & $Action } catch {
    if ($_.Exception.Message -match $MessagePattern) { return }
    throw
  }
  throw 'Expected an exception.'
}
function Invoke-Test {
  param([string]$Name, [scriptblock]$Action)
  & $Action
  $script:TestsPassed++
  Write-Host "PASS: $Name"
}

$props = @'
<Project>
  <PropertyGroup>
    <VersionPrefix Condition="'$(VersionPrefix)' == ''">0.0.2.10</VersionPrefix>
    <VersionSuffix Condition="'$(VersionSuffix)' == ''">alpha1</VersionSuffix>
  </PropertyGroup>
</Project>
'@

Invoke-Test 'Develop increments Build; master increments Revision and resets Build' {
  Assert-Equal (Get-NextVersion '0.0.2.10' 'develop') '0.0.2.11'
  Assert-Equal (Get-NextVersion '0.0.2.10' 'master') '0.0.3.0'
}

Invoke-Test 'Stale branch versions catch up to tags and previous bumps' {
  Assert-Equal (Get-NextVersion '0.0.2.10' 'develop' @('0.0.3.0')) '0.0.3.1'
  Assert-Equal (Get-NextVersion '0.0.2.10' 'master' @('0.0.3.0')) '0.0.4.0'
  Assert-Equal (Get-NextVersion '0.0.2.10' 'develop' @('0.0.2.12')) '0.0.2.13'
  Assert-Equal (Get-NextVersion '0.1.0.0' 'develop' @('0.0.99.99')) '0.1.0.1'
}

Invoke-Test 'Invalid versions, overflow, and unsupported branches fail' {
  foreach ($value in @('0.0.2', 'v0.0.2.10', '0.0.-1.0', '0.0.2.65535')) {
    Assert-Throws { ConvertTo-VersionParts $value }
  }
  Assert-Throws { Get-NextVersion '0.0.2.65534' 'develop' }
  Assert-Throws { Get-NextVersion '0.0.65534.1' 'master' }
  Assert-Throws { Get-NextVersion '0.0.2.10' 'feature/test' }
}

Invoke-Test 'Props edits preserve formatting and clear the suffix only for master' {
  Assert-Equal (Set-PropsVersion $props '0.0.2.11' 'develop') ($props.Replace('0.0.2.10', '0.0.2.11'))
  $master = Set-PropsVersion $props '0.0.3.0' 'master'
  Assert-Equal (Get-PropsVersion $master) '0.0.3.0'
  Assert-Equal ($master.Contains('alpha1')) $false
  Assert-Throws { Get-PropsVersion '<Project />' }
  Assert-Throws { Get-PropsVersion ($props + $props) }
}

Invoke-Test 'Local Git: concurrent merges, retries, tags, stale metadata, and reruns' {
  $temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
  $root = Join-Path $temporaryParent "zero version test-$([guid]::NewGuid().ToString('N'))"
  $null = New-Item -ItemType Directory -Path $root
  $remote = Join-Path $root 'origin.git'
  $seed = Join-Path $root 'seed'
  $allProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
  $entryPoint = Join-Path $PSScriptRoot 'update-version.ps1'
  $powershell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })

  function Get-TestGit {
    param([string]$Directory, [string[]]$Arguments)
    (Invoke-VersionGit -Directory $Directory -Arguments $Arguments).Output
  }
  function New-TestClone {
    param([string]$Name, [string]$Branch)
    $directory = Join-Path $root $Name
    $null = Get-TestGit $root @('clone', '--branch', $Branch, $remote, $directory)
    return $directory
  }
  function Start-VersionProcess {
    param([string]$Directory, [string]$Branch, [int]$PullRequest)
    $info = [System.Diagnostics.ProcessStartInfo]::new($powershell)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in @('-NoLogo', '-NoProfile', '-File', $entryPoint,
      '-TargetBranch', $Branch, '-PullRequest', [string]$PullRequest, '-Directory', $Directory)) {
      $info.ArgumentList.Add($argument)
    }
    $null = $info.Environment.Remove('GITHUB_OUTPUT')
    $process = [System.Diagnostics.Process]::Start($info)
    $allProcesses.Add($process)
    return [pscustomobject]@{
      Process = $process
      OutputTask = $process.StandardOutput.ReadToEndAsync()
      ErrorTask = $process.StandardError.ReadToEndAsync()
    }
  }
  function Receive-VersionProcess {
    param($Run)
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    while (-not $Run.Process.WaitForExit(1000)) {
      if ([DateTime]::UtcNow -gt $deadline) { throw 'Version test process timed out.' }
    }
    $stdout = $Run.OutputTask.GetAwaiter().GetResult()
    $stderr = $Run.ErrorTask.GetAwaiter().GetResult()
    if ($Run.Process.ExitCode -ne 0) { throw "Version process failed: $stderr $stdout" }
    return $stdout | ConvertFrom-Json
  }

  try {
    $null = Get-TestGit $root @('init', '--bare', $remote)
    $null = New-Item -ItemType Directory -Path $seed
    $null = Get-TestGit $seed @('init', '--initial-branch=master')
    $null = Get-TestGit $seed @('config', 'user.name', 'Version test')
    $null = Get-TestGit $seed @('config', 'user.email', 'version-test@example.invalid')
    $null = New-Item -ItemType Directory -Path (Join-Path $seed 'source')
    [System.IO.File]::WriteAllText((Join-Path $seed 'source/Directory.Build.props'), $props)
    $null = Get-TestGit $seed @('add', '.')
    $null = Get-TestGit $seed @('commit', '-m', 'Initial version')
    $null = Get-TestGit $seed @('branch', 'develop')
    $null = Get-TestGit $seed @('remote', 'add', 'origin', $remote)
    $null = Get-TestGit $seed @('push', 'origin', 'master', 'develop')

    # Independent PowerShell processes exercise actual competing Git pushes.
    $runners = @(1..4 | ForEach-Object { New-TestClone "develop-$_" 'develop' })
    $runs = @(for ($i = 0; $i -lt 4; $i++) { Start-VersionProcess $runners[$i] 'develop' ($i + 1) })
    $results = @($runs | ForEach-Object { Receive-VersionProcess $_ })
    Assert-Equal (($results.version | Sort-Object) -join ',') '0.0.2.11,0.0.2.12,0.0.2.13,0.0.2.14'
    $rerun = Update-ZeroVersion -Branch develop -PullRequest 1 -Directory $runners[0]
    Assert-Equal $rerun.reused $true
    Assert-Equal (Get-TestGit $runners[0] @('rev-parse', 'HEAD')) $rerun.commit
    Assert-Equal (Get-TestGit $runners[0] @('tag', '--list')) ''
    Assert-Equal (Get-PropsVersion (Get-TestGit $runners[0] @('show', 'origin/develop:source/Directory.Build.props'))) '0.0.2.14'

    $masterRunner = New-TestClone 'master-runner' 'master'
    $release = Update-ZeroVersion -Branch master -PullRequest 5 -Directory $masterRunner
    Assert-Equal $release.version '0.0.3.0'
    Assert-Equal (Get-TestGit $root @('--git-dir', $remote, 'rev-parse', "$($release.tag)^{commit}")) $release.commit
    Assert-Equal (Get-TestGit $root @('--git-dir', $remote, 'rev-parse', 'master')) $release.commit
    $masterProps = [System.IO.File]::ReadAllText((Join-Path $masterRunner 'source/Directory.Build.props'))
    Assert-Equal ($masterProps.Contains('alpha1')) $false

    $masterRunners = @(6..7 | ForEach-Object { New-TestClone "master-$_" 'master' })
    $masterRuns = @(for ($i = 0; $i -lt 2; $i++) { Start-VersionProcess $masterRunners[$i] 'master' ($i + 6) })
    $masterResults = @($masterRuns | ForEach-Object { Receive-VersionProcess $_ })
    Assert-Equal (($masterResults.version | Sort-Object) -join ',') '0.0.4.0,0.0.5.0'
    $releaseRerun = Update-ZeroVersion -Branch master -PullRequest 5 -Directory $masterRunner
    Assert-Equal $releaseRerun.reused $true
    Assert-Equal $releaseRerun.commit $release.commit
    Assert-Equal (Get-TestGit $masterRunner @('rev-parse', 'HEAD')) $release.commit

    $afterRelease = Update-ZeroVersion -Branch develop -PullRequest 8 -Directory $runners[0]
    Assert-Equal $afterRelease.version '0.0.5.1'
    $null = Get-TestGit $seed @('fetch', 'origin', 'develop')
    $null = Get-TestGit $seed @('switch', '--detach', 'origin/develop')
    [System.IO.File]::WriteAllText((Join-Path $seed 'source/Directory.Build.props'), $props)
    [System.IO.File]::WriteAllText((Join-Path $seed 'merged-feature.txt'), 'Keep this feature')
    $null = Get-TestGit $seed @('add', '.')
    $null = Get-TestGit $seed @('commit', '-m', 'Merge feature with stale version metadata')
    $null = Get-TestGit $seed @('push', 'origin', 'HEAD:develop')
    $afterStaleMerge = Update-ZeroVersion -Branch develop -PullRequest 9 -Directory $runners[0]
    Assert-Equal $afterStaleMerge.version '0.0.5.2'
    Assert-Equal ([System.IO.File]::ReadAllText((Join-Path $runners[0] 'merged-feature.txt'))) 'Keep this feature'
    [System.IO.File]::WriteAllText((Join-Path $runners[0] 'untracked.txt'), 'Preserve user changes')
    Assert-Throws { Update-ZeroVersion -Branch develop -PullRequest 10 -Directory $runners[0] } 'clean'
  } finally {
    foreach ($process in $allProcesses) {
      if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
      $process.Dispose()
    }
    # Verify the resolved target remains the disposable fixture before deletion.
    $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
    Assert-Equal ([System.IO.Path]::GetDirectoryName($resolvedRoot)) $temporaryParent
    if (-not [System.IO.Path]::GetFileName($resolvedRoot).StartsWith('zero version test-')) {
      throw 'Refusing to delete a directory outside the test fixture.'
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
  }
}

Write-Host "All $script:TestsPassed version automation tests passed."
