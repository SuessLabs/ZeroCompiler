// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Prism.Commands;
using Prism.Container.DryIoc;
using Prism.Ioc;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class UpdateTests
{
  private const string Asset = "Zero.IDE-0.1.0.12-win-x64-Setup.exe";
  private const string ReleaseRoot = "https://github.com/SuessLabs/ZeroCompiler/releases/download/v0.0.4.12/";
  private static readonly byte[] Payload = Encoding.UTF8.GetBytes("installer test bytes");
  private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Payload));

  [TestMethod]
  public void ContainerResolvesUpdateService()
  {
    var container = new DryIocContainerExtension();
    container.RegisterSingleton<IUpdateService, UpdateService>();
    Assert.IsInstanceOfType<UpdateService>(container.Resolve<IUpdateService>());
    container.RegisterInstance<IEditorDialogService>(Mock.Of<IEditorDialogService>());
    container.RegisterSingleton<EditorViewModel>();
    container.RegisterSingleton<MainWindowViewModel>();
    Assert.IsTrue(container.Resolve<MainWindowViewModel>().CmdCheckForUpdates.CanExecute());
  }

  [TestMethod]
  [DataRow("0.1.0.9", true)]
  [DataRow("0.1.0.12", false)]
  [DataRow("0.2.0.0", false)]
  [DataRow("0.1.0.12-preview+commit", true)]
  public async Task ChecksIdeAssetVersionInsteadOfCompilerReleaseTagAsync(string current, bool available)
  {
    using var client = ClientFor(_ => JsonResponse(Release()));
    var update = await new UpdateService(client, current).CheckAsync();
    Assert.AreEqual(available, update is not null);
    if (update is not null) Assert.AreEqual("0.1.0.12", update.Version);
  }

  [TestMethod]
  [DataRow(true, false, ReleaseRoot + Asset)]
  [DataRow(false, true, ReleaseRoot + Asset)]
  [DataRow(false, false, "https://example.com/" + Asset)]
  public async Task RejectsUnstableReleasesAndUntrustedAssetUrlsAsync(bool draft, bool prerelease, string uri)
  {
    using var client = ClientFor(_ => JsonResponse(Release(draft: draft, prerelease: prerelease, uri: uri)));
    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new UpdateService(client, "0.0.0.0").CheckAsync());
  }

  [TestMethod]
  public async Task VerifiesChecksumFileAndRemovesCorruptDownloadsAsync()
  {
    var directory = Path.Combine(Path.GetTempPath(), "zero-updates-" + Guid.NewGuid().ToString("N"));
    try
    {
      var corrupt = false;
      using var client = ClientFor(request => request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS", StringComparison.Ordinal)
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Hash + "  " + Asset + "\n") }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(corrupt ? new byte[Payload.Length] : Payload) });
      var service = new UpdateService(client, "0.0.0.0", directory);
      var update = Update() with { Sha256 = null };
      var download = await service.DownloadAsync(update);
      CollectionAssert.AreEqual(Payload, await File.ReadAllBytesAsync(download.Path));
      Assert.AreEqual(Hash, download.Sha256);
      corrupt = true;
      await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.DownloadAsync(update));
      Assert.HasCount(1, Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
  }

  [TestMethod]
  public async Task MissingChecksumNeverDownloadsExecutableAsync()
  {
    using var client = ClientFor(_ => throw new AssertFailedException("Installer must not be downloaded without a checksum."));
    await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new UpdateService(client, "0.0.0.0").DownloadAsync(Update() with { Sha256 = null, ChecksumsUri = null }));
  }

  [TestMethod]
  public void InstallerHandoffWaitsForIdeAndPreservesCustomRoot()
  {
    var root = Path.Combine(Path.GetTempPath(), "custom Zero folder");
    var start = UpdateService.CreateInstallerStartInfo(Path.Combine(root, "update.exe"), Path.Combine(root, "ide"), 1234);
    Assert.IsTrue(start.UseShellExecute);
    Assert.AreEqual("runas", start.Verb);
    Assert.AreEqual($"/WAITPID=1234 /D={root}", start.Arguments);
  }

  [TestMethod]
  public async Task DecliningUpdateNeverDownloadsOrClosesAsync()
  {
    var updates = CreateUpdates();
    var dialogs = CreateDialogs();
    dialogs.Setup(d => d.ConfirmUpdateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
    var shell = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, updates: updates.Object);
    await ExecuteAsync(shell, shell.CmdCheckForUpdates);
    updates.Verify(s => s.DownloadAsync(It.IsAny<IdeUpdate>()), Times.Never);
    dialogs.Verify(d => d.CloseWindow(), Times.Never);
  }

  [TestMethod]
  public async Task CancelingUnsavedChangesPreventsInstallerLaunchAsync()
  {
    var updates = CreateUpdates();
    var dialogs = CreateDialogs();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Cancel);
    var editor = new EditorViewModel { Text = "unsaved work" };
    var shell = new MainWindowViewModel(editor, dialogs.Object, updates: updates.Object);
    await ExecuteAsync(shell, shell.CmdCheckForUpdates);
    updates.Verify(s => s.LaunchInstaller(It.IsAny<DownloadedUpdate>()), Times.Never);
    dialogs.Verify(d => d.CloseWindow(), Times.Never);
    Assert.IsTrue(editor.IsModified);
  }

  [TestMethod]
  public async Task AcceptedUpdateLaunchesThenAllowsWindowToCloseWhileCommandIsBusyAsync()
  {
    var updates = CreateUpdates();
    var dialogs = CreateDialogs();
    var shell = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, updates: updates.Object);
    Task<bool>? canClose = null;
    dialogs.Setup(d => d.CloseWindow()).Callback(() =>
    {
      updates.Verify(s => s.LaunchInstaller(It.IsAny<DownloadedUpdate>()), Times.Once);
      canClose = shell.CanCloseAsync();
    });
    await ExecuteAsync(shell, shell.CmdCheckForUpdates);
    Assert.IsNotNull(canClose);
    Assert.IsTrue(await canClose);
    Assert.IsFalse(shell.IsBusy);
  }

  [TestMethod]
  public async Task DeclinedElevationKeepsIdeOpenAsync()
  {
    var updates = CreateUpdates();
    updates.Setup(s => s.LaunchInstaller(It.IsAny<DownloadedUpdate>())).Throws(new Win32Exception(1223));
    var dialogs = CreateDialogs();
    var shell = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, updates: updates.Object);
    await ExecuteAsync(shell, shell.CmdCheckForUpdates);
    Assert.AreEqual("Update canceled", shell.Status);
    dialogs.Verify(d => d.CloseWindow(), Times.Never);
  }

  [TestMethod]
  public async Task DownloadFailureKeepsIdeOpenAndReportsErrorAsync()
  {
    var updates = CreateUpdates();
    updates.Setup(s => s.DownloadAsync(It.IsAny<IdeUpdate>())).ThrowsAsync(new InvalidDataException("Checksum failed"));
    var dialogs = CreateDialogs();
    var shell = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, updates: updates.Object);
    await ExecuteAsync(shell, shell.CmdCheckForUpdates);
    updates.Verify(s => s.LaunchInstaller(It.IsAny<DownloadedUpdate>()), Times.Never);
    dialogs.Verify(d => d.CloseWindow(), Times.Never);
    dialogs.Verify(d => d.ShowMessageAsync("Unable to update Zero IDE", "Checksum failed"), Times.Once);
  }

  private static IdeUpdate Update() => new("0.1.0.12", Asset, new Uri(ReleaseRoot + Asset), Payload.Length, Hash, new Uri(ReleaseRoot + "SHA256SUMS"));

  private static object Release(bool draft = false, bool prerelease = false, string uri = ReleaseRoot + Asset) => new
  {
    draft, prerelease, tag_name = "v99.0.0.0",
    assets = new object[]
    {
      new { name = Asset, state = "uploaded", size = Payload.Length, browser_download_url = uri, digest = "sha256:" + Hash },
      new { name = "SHA256SUMS", state = "uploaded", size = 512, browser_download_url = ReleaseRoot + "SHA256SUMS" },
    },
  };

  private static HttpResponseMessage JsonResponse(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
  private static HttpClient ClientFor(Func<HttpRequestMessage, HttpResponseMessage> handler) => new(new Handler(handler));

  private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
  }

  private static Mock<IUpdateService> CreateUpdates()
  {
    var service = new Mock<IUpdateService>();
    service.SetupGet(s => s.IsSupported).Returns(true);
    service.SetupGet(s => s.CurrentVersion).Returns("0.1.0.9");
    service.Setup(s => s.CheckAsync()).ReturnsAsync(Update());
    service.Setup(s => s.DownloadAsync(It.IsAny<IdeUpdate>())).ReturnsAsync(new DownloadedUpdate("test.exe", Hash));
    return service;
  }

  private static Mock<IEditorDialogService> CreateDialogs()
  {
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmUpdateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
    dialogs.Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
    return dialogs;
  }

  private static async Task ExecuteAsync(MainWindowViewModel shell, DelegateCommand command)
  {
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    shell.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(shell.IsBusy) && !shell.IsBusy) completed.TrySetResult();
    };
    command.Execute();
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
  }
}
