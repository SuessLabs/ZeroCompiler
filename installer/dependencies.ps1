# Shared tool locations; downloading is performed only by ud.ps1.
function Get-ZeroHostRuntime
{
  $architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
  $platform = if ($IsWindows) { 'win' } elseif ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'osx' } else { throw 'Unsupported operating system.' }
  return "$platform-$architecture"
}

function Get-ZeroToolDefinition([ValidateSet('nsis', 'nfpm')][string]$Name)
{
  $manifest = Get-Content (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json
  $dependency = $manifest.$Name
  $directory = Join-Path (Split-Path $PSScriptRoot -Parent) "tools/$Name/$($dependency.version)"
  if ($Name -eq 'nsis')
  {
    return [pscustomobject]@{
      Name = $Name; Version = $dependency.version; Directory = $directory
      Executable = Join-Path $directory $dependency.executable
      Archive = $dependency.archive; Sha256 = $dependency.sha256; Urls = @($dependency.urls)
    }
  }

  $runtime = Get-ZeroHostRuntime
  $artifact = $dependency.artifacts.$runtime
  if (!$artifact) { throw "No local $Name tool is configured for $runtime." }
  $directory = Join-Path $directory $runtime
  
  return [pscustomobject]@{
    Name = $Name; Version = $dependency.version; Directory = $directory
    Executable = Join-Path $directory 'nfpm'
    Archive = $artifact.archive; Sha256 = $artifact.sha256
    Urls = @("https://github.com/goreleaser/nfpm/releases/download/v$($dependency.version)/$($artifact.archive)")
  }
}
