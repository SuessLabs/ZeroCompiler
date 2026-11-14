param(
  [Parameter(Mandatory)][string]$Cli,
  [Parameter(Mandatory)][string]$Ide,
  [Parameter(Mandatory)][string]$CompilerVersion,
  [Parameter(Mandatory)][string]$IdeVersion,
  [Parameter(Mandatory)][string]$Runtime,
  [Parameter(Mandatory)][string]$Destination
)

$ErrorActionPreference = 'Stop'
$architecture = if ($Runtime -eq 'linux-arm64') { 'arm64' } else { 'amd64' }
$root = '/opt/suesslabs/zero'

foreach ($product in @('cli', 'ide'))
{
  $payload = if ($product -eq 'cli') { $Cli } else { $Ide }
  $version = if ($product -eq 'cli') { $CompilerVersion } else { $IdeVersion }

  $contents = @(Get-ChildItem $payload -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($payload, $_.FullName).Replace('\', '/')
    $mode = if ($relative -eq 'bin/zero' -or $relative -eq 'ide/Zero.Ide' -or $relative.EndsWith('.so')) { 493 } else { 420 }
    @{ src = $_.FullName; dst = "$root/$relative"; file_info = @{ mode = $mode } }
  })

  $dependencies = @('libc6', 'libgcc-s1', 'libstdc++6', 'zlib1g')

  if ($product -eq 'cli')
  {
    $contents += @{ src = "$root/bin/zero"; dst = '/usr/bin/zero'; type = 'symlink' }
  }
  else
  {
    $dependencies += @("zero-cli (>= $CompilerVersion)", 'libx11-6', 'libice6', 'libsm6', 'libfontconfig1')

    $desktop = Join-Path (Split-Path $Ide -Parent) 'zero-ide.desktop'
    @"
[Desktop Entry]
Type=Application
Name=Zero IDE
Exec=$root/ide/Zero.Ide %f
Terminal=false
Categories=Development;IDE;
"@ | Set-Content $desktop -Encoding utf8NoBOM
    $contents += @{ src = $desktop; dst = '/usr/share/applications/zero-ide.desktop' }
  }

  $rpmDependencies = @('glibc', 'libgcc', 'libstdc++', 'zlib')

  if ($product -eq 'ide')
  {
    $rpmDependencies += @("zero-cli >= $CompilerVersion", 'libX11', 'libICE', 'libSM', 'fontconfig')
  }

  $config = @{
    name = "zero-$product"; arch = $architecture; platform = 'linux'; version = $version; version_schema = 'none'
    maintainer = 'SuessLabs'; description = "Zero $product"; license = 'Apache-2.0'
    contents = $contents
    overrides = @{ deb = @{ depends = $dependencies }; rpm = @{ depends = $rpmDependencies } }
  }

  $configPath = Join-Path (Split-Path $payload -Parent) "$product.nfpm.json"
  $config | ConvertTo-Json -Depth 12 | Set-Content $configPath -Encoding utf8NoBOM

  foreach ($format in @('deb', 'rpm'))
  {
    & nfpm package --config $configPath --packager $format --target (Join-Path $Destination "zero-$product-$version-$Runtime.$format")

    if ($LASTEXITCODE -ne 0)
    {
      throw "nFPM $format packaging failed"
    }
  }
}
