param([Parameter(Mandatory)][ValidateSet('Add', 'Remove')][string]$Action)
$ErrorActionPreference = 'Stop'

try
{
  $bin = $PSScriptRoot.TrimEnd('\')
  $environmentKey = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SYSTEM\CurrentControlSet\Control\Session Manager\Environment', $true)
  $registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
  $owner = $registry.CreateSubKey('Software\SuessLabs\Zero\Cli')

  try
  {
    # Read unexpanded values, preserving %VARIABLE% references and registry value type.
    $path = [string]$environmentKey.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)

    $kind = if ($environmentKey.GetValueNames() -contains 'Path')
    {
      $environmentKey.GetValueKind('Path')
    }
    else
    {
      [Microsoft.Win32.RegistryValueKind]::ExpandString
    }

    $entries = @($path.Split(';'))
    $matches = @($entries | Where-Object { $_.Trim().Trim('"').TrimEnd('\') -ieq $bin })
    $owned = [string]$owner.GetValue('AddedPath', '')

    if ($Action -eq 'Add' -and $matches.Count -eq 0)
    {
      $updated = if ($path.Length -eq 0 -or $path.EndsWith(';')) { "$path$bin" } else { "$path;$bin" }
      if ($updated.Length -ge 32767) {
        throw 'The resulting system PATH would exceed the Windows limit.' }

      $environmentKey.SetValue('Path', $updated, $kind)
      $owner.SetValue('AddedPath', $bin)
    }
    elseif ($Action -eq 'Remove' -and $owned -ieq $bin)
    {
      $updated = ($entries | Where-Object { $_.Trim().Trim('"').TrimEnd('\') -ine $owned }) -join ';'
      $environmentKey.SetValue('Path', $updated, $kind)
      $owner.DeleteValue('AddedPath', $false)
    }
  }
  finally
  {
    $environmentKey.Dispose(); $owner.Dispose(); $registry.Dispose()
  }

  Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ZeroEnvironment {
  [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wparam, string lparam, uint flags, uint timeout, out UIntPtr result);
}
'@
  $result = [UIntPtr]::Zero
  [void][ZeroEnvironment]::SendMessageTimeout([IntPtr]0xffff, 0x1a, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$result)
}
catch
{
  Write-Error $_; exit 1
}
