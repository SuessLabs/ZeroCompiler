#Requires -Version 7.2
Set-StrictMode -Version Latest

$script:VersionPath = 'source/Directory.Build.props'
$script:PrefixPattern = [regex]'(<VersionPrefix\b[^>]*>)([^<]*)(</VersionPrefix>)'

function ConvertTo-VersionParts {
  param([Parameter(Mandatory)][string]$Value)

  if ($Value -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Expected Major.Minor.Revision.Build, received '$Value'."
  }
  $parts = foreach ($part in $Value.Split('.')) {
    $number = 0
    if (-not [int]::TryParse($part, [ref]$number) -or $number -gt 65534) {
      throw 'Each version component must be between 0 and 65534.'
    }
    $number
  }
  return $parts
}

function Get-PropsVersion {
  param([Parameter(Mandatory)][AllowEmptyString()][string]$Contents)

  $prefixes = $script:PrefixPattern.Matches($Contents)
  if ($prefixes.Count -ne 1) {
    throw 'Expected exactly one VersionPrefix in Directory.Build.props.'
  }
  $value = $prefixes[0].Groups[2].Value
  $null = ConvertTo-VersionParts $value
  return $value
}

function Get-NextVersion {
  param(
    [Parameter(Mandatory)][string]$Current,
    [Parameter(Mandatory)][ValidateSet('develop', 'master')][string]$Branch,
    [string[]]$Baselines = @()
  )

  [int[]]$parts = ConvertTo-VersionParts $Current
  foreach ($baseline in $Baselines) {
    [int[]]$candidate = ConvertTo-VersionParts $baseline
    if ($candidate[0] -ne $parts[0] -or $candidate[1] -ne $parts[1]) { continue }
    if ($candidate[2] -gt $parts[2] -or
      ($candidate[2] -eq $parts[2] -and $candidate[3] -gt $parts[3])) {
      $parts[2] = $candidate[2]
      $parts[3] = $candidate[3]
    }
  }
  if ($Branch -eq 'develop') {
    $parts[3]++
  } else {
    $parts[2]++
    $parts[3] = 0
  }
  $version = $parts -join '.'
  $null = ConvertTo-VersionParts $version
  return $version
}

function Set-PropsVersion {
  param(
    [Parameter(Mandatory)][string]$Contents,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][ValidateSet('develop', 'master')][string]$Branch
  )

  $null = Get-PropsVersion $Contents
  $null = ConvertTo-VersionParts $Version
  $updated = $script:PrefixPattern.Replace($Contents, [System.Text.RegularExpressions.MatchEvaluator]{
    param($match)
    $match.Groups[1].Value + $Version + $match.Groups[3].Value
  })
  if ($Branch -eq 'master') {
    $updated = [regex]::Replace($updated,
      '(<VersionSuffix\b[^>]*>)[^<]*(</VersionSuffix>)', '$1$2')
  }
  return $updated
}

function Invoke-VersionGit {
  param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string[]]$Arguments,
    [switch]$AllowFailure
  )

  $startInfo = [System.Diagnostics.ProcessStartInfo]::new('git')
  $startInfo.WorkingDirectory = $Directory
  $startInfo.UseShellExecute = $false
  $startInfo.CreateNoWindow = $true
  $startInfo.RedirectStandardOutput = $true
  $startInfo.RedirectStandardError = $true
  foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }

  $process = [System.Diagnostics.Process]::Start($startInfo)
  try {
    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $result = [pscustomobject]@{
      Output = $outputTask.GetAwaiter().GetResult().Trim()
      Error = $errorTask.GetAwaiter().GetResult().Trim()
      ExitCode = $process.ExitCode
    }
    if ($result.ExitCode -ne 0 -and -not $AllowFailure) {
      throw "git $($Arguments[0]) failed: $($result.Error) $($result.Output)"
    }
    return $result
  } finally {
    $process.Dispose()
  }
}

function Update-ZeroVersion {
  param(
    [Parameter(Mandatory)][ValidateSet('develop', 'master')][string]$Branch,
    [Parameter(Mandatory)][ValidatePattern('^[1-9]\d*$')][string]$PullRequest,
    [string]$Directory = (Get-Location).Path
  )

  $Directory = (Resolve-Path -LiteralPath $Directory).Path
  function Get-GitText {
    param([string[]]$GitArguments)
    (Invoke-VersionGit -Directory $Directory -Arguments $GitArguments).Output
  }

  # Only operate in a clean, disposable Actions checkout. Preserve user edits.
  if (Get-GitText @('status', '--porcelain')) { throw 'The checkout must be clean.' }
  $marker = "Zero-Version-PR: ${Branch}#$PullRequest"
  $remoteRef = "refs/remotes/origin/$Branch"
  $propsPath = Join-Path $Directory $script:VersionPath
  $null = Get-GitText @('config', 'user.name', 'github-actions[bot]')
  $null = Get-GitText @('config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com')

  for ($attempt = 0; $attempt -lt 12; $attempt++) {
    $null = Get-GitText @('fetch', 'origin', "+refs/heads/${Branch}:$remoteRef", '--tags')
    $previous = Get-GitText @('log', $remoteRef, '--format=%H', '--max-count=1', "--grep=^${marker}$")
    if ($previous) {
      $version = Get-PropsVersion (Get-GitText @('show', "${previous}:$script:VersionPath"))
      if ($Branch -eq 'master' -and
        (Get-GitText @('rev-parse', "refs/tags/v${version}^{commit}")) -ne $previous) {
        throw "Release tag v$version does not point to the version commit."
      }
      $null = Get-GitText @('switch', '--detach', $previous)
      return [pscustomobject]@{ version = $version; tag = "v$version"; commit = $previous; reused = $true }
    }

    $null = Get-GitText @('switch', '--detach', $remoteRef)
    $contents = [System.IO.File]::ReadAllText($propsPath)
    [string[]]$baselines = @(
      (Get-GitText @('tag', '--list', 'v*')).Split("`n") |
        Where-Object { $_ -match '^v\d+\.\d+\.\d+\.\d+$' } |
        ForEach-Object { $_.Substring(1) }
    )
    # Incoming merges can restore stale version metadata from another branch.
    $lastBump = Get-GitText @('log', $remoteRef, '--first-parent', '--format=%H', '--max-count=1',
      "--grep=^Zero-Version-PR: ${Branch}#[0-9][0-9]*$")
    if ($lastBump) {
      $baselines += Get-PropsVersion (Get-GitText @('show', "${lastBump}:$script:VersionPath"))
    }
    $version = Get-NextVersion -Current (Get-PropsVersion $contents) -Branch $Branch -Baselines $baselines
    [System.IO.File]::WriteAllText($propsPath,
      (Set-PropsVersion -Contents $contents -Version $version -Branch $Branch))
    $null = Get-GitText @('add', '--', $script:VersionPath)
    $null = Get-GitText @('commit', '-m', "chore: bump version to v$version", '-m', $marker)
    $commit = Get-GitText @('rev-parse', 'HEAD')
    $tag = "v$version"
    $refspecs = @("HEAD:refs/heads/$Branch")
    if ($Branch -eq 'master') {
      # Publish the tag and version commit together, or neither of them.
      $null = Get-GitText @('tag', '-a', $tag, '-m', "Zero Compiler $tag")
      $refspecs += "refs/tags/$tag"
    }
    $push = Invoke-VersionGit -Directory $Directory -Arguments (@('push', '--atomic', 'origin') + $refspecs) -AllowFailure
    if ($push.ExitCode -eq 0) {
      return [pscustomobject]@{ version = $version; tag = $tag; commit = $commit; reused = $false }
    }
    if ($Branch -eq 'master') { $null = Get-GitText @('tag', '-d', $tag) }
    if ($attempt -eq 11) { throw "Unable to push version after 12 attempts: $($push.Error)" }
    # Recalculate after competing merges rather than overwriting their commits.
    Start-Sleep -Milliseconds ([Math]::Min(500 * ($attempt + 1), 5000))
  }
}

Export-ModuleMember -Function ConvertTo-VersionParts, Get-PropsVersion, Get-NextVersion, Set-PropsVersion, Invoke-VersionGit, Update-ZeroVersion
