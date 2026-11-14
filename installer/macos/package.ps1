param(
  [Parameter(Mandatory)][string]$Cli,
  [Parameter(Mandatory)][string]$Ide,
  [Parameter(Mandatory)][string]$CompilerVersion,
  [Parameter(Mandatory)][string]$IdeVersion,
  [Parameter(Mandatory)][string]$Destination
)

$ErrorActionPreference = 'Stop'
$work = Split-Path $Cli -Parent
$root = '/Library/SuessLabs/Zero'
$parts = $IdeVersion.Split('.')
$bundleBuild = [int]$parts[2]

if ($parts.Count -gt 3)
{
  $bundleBuild = $bundleBuild * 10000 + [int]$parts[3]
}

$bundleVersion = "$($parts[0]).$($parts[1]).$bundleBuild"
$packages = Join-Path $work 'packages'
New-Item -ItemType Directory $packages -Force | Out-Null

foreach ($product in @('cli', 'ide'))
{
  $payload = if ($product -eq 'cli') { $Cli } else { $Ide }
  $version = if ($product -eq 'cli') { $CompilerVersion } else { $IdeVersion }
  $packageRoot = Join-Path $work "pkg-$product"

  New-Item -ItemType Directory "$packageRoot$root" -Force | Out-Null
  Copy-Item "$payload/*" "$packageRoot$root" -Recurse

  if ($product -eq 'cli')
  {
    & chmod 755 "$packageRoot$root/bin/zero"
    New-Item -ItemType Directory "$packageRoot/usr/local/bin" -Force | Out-Null
    & ln -s "$root/bin/zero" "$packageRoot/usr/local/bin/zero"
  }
  else
  {
    & chmod 755 "$packageRoot$root/ide/Zero.Ide"

    # A small Finder launcher keeps the entire .NET publish output together in Zero/ide.
    $app = "$packageRoot/Applications/Zero IDE.app/Contents"

    New-Item -ItemType Directory "$app/MacOS" -Force | Out-Null
    "#!/bin/sh`nexec /Library/SuessLabs/Zero/ide/Zero.Ide `"`$@`"`n" |
      Set-Content "$app/MacOS/Zero.Ide" -Encoding utf8NoBOM -NoNewline
    & chmod 755 "$app/MacOS/Zero.Ide"
    @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.suesslabs.zero.ide.launcher</string>
<key>CFBundleName</key><string>Zero IDE</string>
<key>CFBundleExecutable</key><string>Zero.Ide</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>$bundleVersion</string>
</dict></plist>
"@ | Set-Content "$app/Info.plist" -Encoding utf8NoBOM
  }
  # Disable bundle relocation: compiler discovery depends on a stable shared root.
  '<?xml version="1.0" encoding="UTF-8"?><plist version="1.0"><array/></plist>' |
    Set-Content "$work/component.plist" -Encoding utf8NoBOM
  & pkgbuild --root $packageRoot --identifier "com.suesslabs.zero.$product" --version $version --install-location / --component-plist "$work/component.plist" "$packages/$product.pkg"
  if ($LASTEXITCODE -ne 0) { throw 'pkgbuild failed' }
}

Copy-Item "$packages/cli.pkg" "$Destination/Zero.Cli-$CompilerVersion.pkg"
@"
<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="1">
  <title>Zero IDE $IdeVersion</title>
  <options customize="never" require-scripts="false"/>
  <domains enable_localSystem="true" enable_currentUserHome="false" enable_anywhere="false"/>
  <choices-outline><line choice="cli"/><line choice="ide"/></choices-outline>
  <choice id="cli" visible="false"><pkg-ref id="com.suesslabs.zero.cli"/></choice>
  <choice id="ide" visible="false"><pkg-ref id="com.suesslabs.zero.ide"/></choice>
  <pkg-ref id="com.suesslabs.zero.cli" version="$CompilerVersion">cli.pkg</pkg-ref>
  <pkg-ref id="com.suesslabs.zero.ide" version="$IdeVersion">ide.pkg</pkg-ref>
</installer-gui-script>
"@ | Set-Content "$work/distribution.xml" -Encoding utf8NoBOM
& productbuild --distribution "$work/distribution.xml" --package-path $packages "$Destination/Zero.IDE-$IdeVersion.pkg"

if ($LASTEXITCODE -ne 0)
{
  throw 'productbuild failed'
}
