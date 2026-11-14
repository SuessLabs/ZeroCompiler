// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Zero.Ide.Services;

public sealed record IdeUpdate(string Version, string AssetName, Uri InstallerUri, long Size, string? Sha256, Uri? ChecksumsUri);

public sealed record DownloadedUpdate(string Path, string Sha256);

public interface IUpdateService
{
  bool IsSupported { get; }
  string CurrentVersion { get; }
  Task<IdeUpdate?> CheckAsync();
  Task<DownloadedUpdate> DownloadAsync(IdeUpdate update);
  void LaunchInstaller(DownloadedUpdate update);
}
