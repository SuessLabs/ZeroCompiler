# Compatible with the Windows PowerShell 5.1 shipped with Windows.
[CmdletBinding()]
param(
  [ValidateSet('Cli', 'Ide')][string]$Product,
  [string]$CurrentVersion,
  [string]$ResultPath
)

function ConvertTo-ZeroUpdateVersion([string]$Value)
{
  if ($Value -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw 'Invalid installer version.' }

  if ($Value.Split('.').Count -eq 3) {
    $Value += '.0' }

  return [version]::Parse($Value)
}

function Get-ZeroInstallerUpdate($Release, [string]$Product, [string]$CurrentVersion)
{
  $current = ConvertTo-ZeroUpdateVersion $CurrentVersion
  if ($Product -notin @('Cli', 'Ide')) {
    throw 'Unknown installer product.' }

  if ($Release.draft -ne $false -or $Release.prerelease -ne $false)
  {
    return [pscustomobject]@{ Status = 'Missing'; LatestVersion = '' }
  }

  $assetProduct = if ($Product -eq 'Cli') { 'Cli' } else { 'IDE' }
  $pattern = '^Zero\.' + $assetProduct + '-(\d+\.\d+\.\d+(?:\.\d+)?)-win-x64-Setup\.exe$'
  $candidates = @(
    foreach ($asset in $Release.assets)
    {
      if ($asset.state -eq 'uploaded' -and $asset.size -gt 0 -and $asset.name -cmatch $pattern)
      {
        $text = $Matches[1]
        try
        {
          [pscustomobject]@{ Version = ConvertTo-ZeroUpdateVersion $text; Text = $text }
        }
        catch
        {
          continue
        }
      }
    }
  )

  if ($candidates.Count -eq 0) {
    return [pscustomobject]@{ Status = 'Missing'; LatestVersion = '' } }

  $latest = $candidates | Sort-Object Version -Descending | Select-Object -First 1
  $status = if ($latest.Version -gt $current) { 'Available' } else { 'Current' }
  return [pscustomobject]@{ Status = $status; LatestVersion = $latest.Text }
}

function Invoke-ZeroInstallerUpdateCheck([string]$Product, [string]$CurrentVersion, [string]$ResultPath)
{
  $result = [pscustomobject]@{ Status = 'Unavailable'; LatestVersion = '' }

  try
  {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/SuessLabs/ZeroCompiler/releases/latest' -Headers @{
      Accept = 'application/vnd.github+json'
      'User-Agent' = 'Zero-Installer-Update-Check'
    } -TimeoutSec 10 -ErrorAction Stop
    # Use asset versions: CLI and IDE are versioned independently of the release tag.
    $result = Get-ZeroInstallerUpdate $release $Product $CurrentVersion
  }
  catch { } # Offline, missing/private repository, and rate limits never block setup.

  @('[Update]', "Status=$($result.Status)", "LatestVersion=$($result.LatestVersion)") |
    Set-Content -LiteralPath $ResultPath -Encoding ascii -ErrorAction Stop
}

if ($MyInvocation.InvocationName -ne '.')
{
  $ErrorActionPreference = 'Stop'
  Invoke-ZeroInstallerUpdateCheck $Product $CurrentVersion $ResultPath
}
