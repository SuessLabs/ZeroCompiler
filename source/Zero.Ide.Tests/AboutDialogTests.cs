// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Prism.Dialogs;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;
using Zero.Ide.Views;

namespace Zero.Ide.Tests;

[TestClass]
public class AboutDialogTests
{
  [TestMethod]
  public async Task AboutUsesDedicatedModalDialogAndWaitsForCloseAsync()
  {
    var prism = new Mock<IDialogService>();
    DialogCallback? callback = null;
    prism.Setup(d => d.ShowDialog(nameof(AboutDialogView), It.IsAny<IDialogParameters>(), It.IsAny<DialogCallback>()))
      .Callback<string, IDialogParameters, DialogCallback>((_, parameters, close) =>
      {
        Assert.AreEqual(nameof(AboutDialogWindow), parameters.GetValue<string>(KnownDialogParameters.WindowName));
        Assert.IsFalse(parameters.GetValue<bool>(KnownDialogParameters.ShowNonModal));
        callback = close;
      });
    var service = new EditorDialogService(prism.Object);
    var pending = service.ShowAboutAsync();
    Assert.IsFalse(pending.IsCompleted);
    Assert.IsNotNull(callback);
    await callback.Value.Invoke(new DialogResult(ButtonResult.OK));
    await pending;
  }

  [TestMethod]
  public void VersionsComeFromTheirRespectiveAssemblies()
  {
    var about = new AboutDialogViewModel();
    var ideVersion = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    var compilerVersion = typeof(global::Zero.Zero).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    Assert.AreEqual(ideVersion, about.IdeVersion);
    Assert.AreEqual(compilerVersion, about.CompilerVersion);
  }

  [TestMethod]
  public void CloseCommandUsesPrismDialogLifecycle()
  {
    var about = new AboutDialogViewModel();
    IDialogResult? result = null;
    DialogUtilities.InitializeListener(about, (IDialogResult value) => result = value);
    about.CmdClose.Execute();
    Assert.IsNotNull(result);
    Assert.AreEqual(ButtonResult.OK, result.Result);
    Assert.IsTrue(about.CanCloseDialog());
  }
}
