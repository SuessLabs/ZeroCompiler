// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Zero.Ide.Services;

public sealed class UpdateService : IUpdateService
{
  private const string AssetPrefix = "/SuessLabs/ZeroCompiler/releases/download/";
  private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };
  private static readonly Regex InstallerName = new(@"^Zero\.IDE-(\d+\.\d+\.\d+(?:\.\d+)?)-win-x64-Setup\.exe$", RegexOptions.CultureInvariant);
  private readonly HttpClient _client;
  private readonly string _downloadDirectory;

  public UpdateService() : this(Client, typeof(App).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
    ?? typeof(App).Assembly.GetName().Version!.ToString()) { }

  public UpdateService(HttpClient client, string currentVersion, string? downloadDirectory = null)
  {
    _client = client;
    CurrentVersion = currentVersion;
    _downloadDirectory = downloadDirectory ?? Path.Combine(Path.GetTempPath(), "SuessLabs", "Zero", "Updates");
  }

  public bool IsSupported => OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64;
  public string CurrentVersion { get; }

  public async Task<IdeUpdate?> CheckAsync()
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/SuessLabs/ZeroCompiler/releases/latest");
    request.Headers.UserAgent.ParseAdd("Zero-IDE-Update-Check");
    request.Headers.Accept.ParseAdd("application/vnd.github+json");
    using var response = await _client.SendAsync(request, timeout.Token);
    response.EnsureSuccessStatusCode();
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
    var release = document.RootElement;
    if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
      throw new InvalidOperationException("No stable update release is available.");

    IdeUpdate? latest = null;
    Uri? checksums = null;
    foreach (var asset in release.GetProperty("assets").EnumerateArray())
    {
      if (asset.GetProperty("state").GetString() != "uploaded") continue;
      var name = asset.GetProperty("name").GetString() ?? "";
      var size = asset.GetProperty("size").GetInt64();
      if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri) || !IsReleaseAsset(uri)) continue;
      if (Uri.UnescapeDataString(uri.Segments[^1]) != name) continue;
      if (name == "SHA256SUMS" && size is > 0 and <= 1048576) checksums = uri;
      var match = InstallerName.Match(name);
      if (!match.Success || size is <= 0 or > 1073741824 || !TryVersion(match.Groups[1].Value, out var version)) continue;
      if (latest is not null && version <= ParseVersion(latest.Version)) continue;
      string? hash = null;
      if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String &&
          Regex.IsMatch(digest.GetString()!, "^sha256:[a-fA-F0-9]{64}$")) hash = digest.GetString()![7..];
      latest = new IdeUpdate(match.Groups[1].Value, name, uri, size, hash, null);
    }

    if (latest is null) throw new InvalidOperationException("The latest release does not include a Windows IDE installer yet.");
    var current = ParseVersion(CurrentVersion.Split('+')[0].Split('-')[0]);
    if (ParseVersion(latest.Version) < current || (ParseVersion(latest.Version) == current && !CurrentVersion.Contains('-'))) return null;
    // The checksum file must belong to the same tagged release as the installer.
    if (checksums is not null && new Uri(checksums, ".") != new Uri(latest.InstallerUri, ".")) checksums = null;
    return latest with { ChecksumsUri = checksums };
  }

  public async Task<DownloadedUpdate> DownloadAsync(IdeUpdate update)
  {
    if (!InstallerName.IsMatch(update.AssetName) || !IsReleaseAsset(update.InstallerUri) ||
        Uri.UnescapeDataString(update.InstallerUri.Segments[^1]) != update.AssetName || update.Size is <= 0 or > 1073741824)
      throw new InvalidDataException("The update installer metadata is invalid.");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var expected = update.Sha256;
    if (expected is null)
    {
      if (update.ChecksumsUri is null || !IsReleaseAsset(update.ChecksumsUri) ||
          new Uri(update.ChecksumsUri, ".") != new Uri(update.InstallerUri, "."))
        throw new InvalidDataException("This release has no SHA-256 checksum for the installer.");
      using var checksumTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      var sums = await _client.GetStringAsync(update.ChecksumsUri, checksumTimeout.Token);
      var pattern = @"^([a-fA-F0-9]{64})[ \t]+\*?" + Regex.Escape(update.AssetName) + "$";
      int matches = 0;

      foreach (var line in sums.TrimStart('\uFEFF').Split('\n'))
      {
        var match = Regex.Match(line.TrimEnd('\r'), pattern);
        if (!match.Success)
          continue;

        matches++;
        expected = match.Groups[1].Value;
      }

      if (matches != 1)
        throw new InvalidDataException("The release checksum file does not identify this installer uniquely.");
    }

    if (expected is null || !Regex.IsMatch(expected, "^[a-fA-F0-9]{64}$"))
      throw new InvalidDataException("The installer checksum is invalid.");

    var directory = Directory.CreateDirectory(Path.Combine(_downloadDirectory, Guid.NewGuid().ToString("N")));
    var path = Path.Combine(directory.FullName, update.AssetName);
    try
    {
      using var response = await _client.GetAsync(update.InstallerUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
      response.EnsureSuccessStatusCode();

      await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
      await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))

      using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
      {
        var buffer = new byte[81920];
        long length = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, timeout.Token)) != 0)
        {
          length += read;
          if (length > update.Size)
            throw new InvalidDataException("The update download exceeds its published size.");

          hash.AppendData(buffer, 0, read);
          await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }

        if (length != update.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(expected, StringComparison.OrdinalIgnoreCase))
          throw new InvalidDataException("The installer download failed SHA-256 verification. It will not be run.");
      }

      return new DownloadedUpdate(path, expected);
    }
    catch
    {
      File.Delete(path);
      directory.Delete();
      throw;
    }
  }

  public void LaunchInstaller(DownloadedUpdate update)
  {
    if (!IsSupported)
      throw new PlatformNotSupportedException("IDE updates currently require Windows x64.");

    using (var stream = File.OpenRead(update.Path))
      if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("The downloaded installer changed after verification. Check for updates again.");

    using var process = Process.Start(CreateInstallerStartInfo(update.Path, AppContext.BaseDirectory, Environment.ProcessId))
      ?? throw new InvalidOperationException("The update installer could not be started.");
  }

  internal static ProcessStartInfo CreateInstallerStartInfo(string path, string ideDirectory, int processId)
  {
    var arguments = $"/WAITPID={processId}";
    var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ideDirectory));
    if (Path.GetFileName(directory).Equals("ide", StringComparison.OrdinalIgnoreCase))
      arguments += $" /D={Path.GetDirectoryName(directory)}"; // NSIS /D must be unquoted and last.

    return new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true, Verb = "runas", Arguments = arguments };
  }

  private static bool IsReleaseAsset(Uri uri) => uri.Scheme == "https" && uri.Host == "github.com" && uri.IsDefaultPort &&
    uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith(AssetPrefix, StringComparison.Ordinal);

  private static Version ParseVersion(string value) => TryVersion(value, out var version) ? version : throw new InvalidDataException("Invalid IDE version.");

  private static bool TryVersion(string value, out Version version)
  {
    version = new Version();
    if (!Regex.IsMatch(value, @"^\d+\.\d+\.\d+(\.\d+)?$"))
      return false;

    return Version.TryParse(value.Split('.').Length == 3 ? value + ".0" : value, out version!);
  }
}
