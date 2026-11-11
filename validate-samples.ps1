[CmdletBinding()]
param (
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',

  [ValidateNotNullOrEmpty()]
  [string]$SamplesDirectory,
  [ValidateNotNullOrEmpty()]
  [string]$OutputDirectory,
  [ValidateNotNullOrEmpty()]
  [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'

# Resolve script-relative defaults after parameter binding for Windows PowerShell 5.1.
if (-not $PSBoundParameters.ContainsKey('SamplesDirectory')) {
  $SamplesDirectory = Join-Path $PSScriptRoot 'samples'
}

if (-not $PSBoundParameters.ContainsKey('OutputDirectory')) {
  $OutputDirectory = Join-Path $PSScriptRoot "output/samples/$Configuration"
}

if (-not $PSBoundParameters.ContainsKey('CompilerPath')) {
  $CompilerPath = Join-Path $PSScriptRoot "output/Zero.Cli/$Configuration/zcc.dll"
}

function Invoke-SampleCompiler {
  param ([string[]]$CompilerArguments)

  # Capture native stderr without stopping at the first bad sample in PowerShell 5.1.
  $ErrorActionPreference = 'Continue'
  $PSNativeCommandUseErrorActionPreference = $false
  $compilerOutput = & dotnet @CompilerArguments 2>&1
  return [pscustomobject]@{
    Output = $compilerOutput
    ExitCode = $LASTEXITCODE
  }
}

try {
  $null = Get-Command dotnet -CommandType Application -ErrorAction Stop
  if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw "Compiler not found: $CompilerPath. Build the solution first or run build.ps1."
  }

  if (-not (Test-Path -LiteralPath $SamplesDirectory -PathType Container)) {
    throw "Samples directory not found: $SamplesDirectory"
  }

  $samplesRoot = (Resolve-Path -LiteralPath $SamplesDirectory).Path
  $compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
  $outputRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
  $samples = @(Get-ChildItem -LiteralPath $samplesRoot -Filter '*.zasm' -File -Recurse | Sort-Object FullName)

  if ($samples.Count -eq 0) {
    throw "No .zasm samples found in $samplesRoot"
  }

  $failures = 0
  foreach ($sample in $samples) {
    $relativePath = $sample.FullName.Substring($samplesRoot.Length).TrimStart(
      [IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

    # Samples declare their target explicitly; unmarked samples retain Windows defaults.
    $linuxTarget = Select-String -LiteralPath $sample.FullName -Pattern '^\s*#TARGET\s+linux-x64\s*(;.*)?$' -Quiet
    $outputExtension = if ($linuxTarget) { $null } else { '.exe' }
    $outputPath = Join-Path $outputRoot ([IO.Path]::ChangeExtension($relativePath, $outputExtension))
    try {
      New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null

      # Remove only this generated file so a stale EXE cannot make validation pass.
      if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
        Remove-Item -LiteralPath $outputPath -Force
      }

      $result = Invoke-SampleCompiler @($compiler, $sample.FullName, $outputPath, '-I', (Join-Path $PSScriptRoot 'include'))
      if ($result.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "Compiler exit code: $($result.ExitCode). $($result.Output -join [Environment]::NewLine)"
      }

      $stream = [IO.File]::OpenRead($outputPath)
      try {
        if ($stream.Length -eq 0) { throw 'Compiler produced an empty executable.' }
        $signature = New-Object byte[] 4
        if ($stream.Read($signature, 0, 4) -ne 4) { throw 'Executable header is truncated.' }
        if ($linuxTarget) {
          if ($signature[0] -ne 0x7F -or $signature[1] -ne 0x45 -or $signature[2] -ne 0x4C -or $signature[3] -ne 0x46) {
            throw 'Expected an ELF executable.'
          }
        } elseif ($signature[0] -ne 0x4D -or $signature[1] -ne 0x5A) {
          throw 'Expected a PE executable.'
        }
      } finally {
        $stream.Dispose()
      }
      Write-Host "PASS: $relativePath"
    } catch {
      $failures++
      Write-Host "FAIL: $relativePath - $($_.Exception.Message)"
    }
  }

  Write-Host "Samples validated: $($samples.Count - $failures) passed, $failures failed."
  if ($failures -gt 0) { exit 1 }

  exit 0
} catch {
  Write-Error $_ -ErrorAction Continue
  exit 1
}
