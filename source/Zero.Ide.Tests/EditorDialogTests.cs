// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Moq;
using Prism.Dialogs;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;
using Zero.Ide.Views;

namespace Zero.Ide.Tests;

[TestClass]
public class EditorDialogTests
{
  [TestMethod]
  [DataRow(ButtonResult.Yes, SaveChangesChoice.Save)]
  [DataRow(ButtonResult.No, SaveChangesChoice.Discard)]
  [DataRow(ButtonResult.Cancel, SaveChangesChoice.Cancel)]
  [DataRow(ButtonResult.None, SaveChangesChoice.Cancel)]
  public async Task ConfirmationMapsPrismResultsAsync(ButtonResult result, SaveChangesChoice expected)
  {
    var prism = new Mock<IDialogService>();
    DialogCallback? callback = null;
    IDialogParameters? parameters = null;
    prism.Setup(d => d.ShowDialog(nameof(EditorDialogView), It.IsAny<IDialogParameters>(), It.IsAny<DialogCallback>()))
      .Callback<string, IDialogParameters, DialogCallback>((_, p, c) =>
      {
        parameters = p;
        callback = c;
      });
    var service = new EditorDialogService(prism.Object);
    var pending = service.ConfirmSaveAsync("sample.zasm");
    Assert.IsFalse(pending.IsCompleted);
    Assert.IsNotNull(parameters);
    Assert.IsTrue(parameters.GetValue<bool>("confirmSave"));
    Assert.AreEqual("Save changes to sample.zasm?", parameters.GetValue<string>("message"));
    Assert.AreEqual(nameof(EditorDialogWindow), parameters.GetValue<string>(KnownDialogParameters.WindowName));
    Assert.IsNotNull(callback);
    await callback.Value.Invoke(new DialogResult(result));
    Assert.AreEqual(expected, await pending);
  }

  [TestMethod]
  public async Task MessagesUsePrismAndWaitForDismissalAsync()
  {
    var prism = new Mock<IDialogService>();
    DialogCallback? callback = null;
    prism.Setup(d => d.ShowDialog(nameof(EditorDialogView), It.IsAny<IDialogParameters>(), It.IsAny<DialogCallback>()))
      .Callback<string, IDialogParameters, DialogCallback>((_, p, c) =>
      {
        Assert.AreEqual("About", p.GetValue<string>("title"));
        Assert.AreEqual("Zero IDE", p.GetValue<string>("message"));
        Assert.IsFalse(p.GetValue<bool>("confirmSave"));
        callback = c;
      });
    var service = new EditorDialogService(prism.Object);
    var pending = service.ShowMessageAsync("About", "Zero IDE");
    Assert.IsFalse(pending.IsCompleted);
    Assert.IsNotNull(callback);
    await callback.Value.Invoke(new DialogResult(ButtonResult.OK));
    await pending;
  }

  [TestMethod]
  public async Task PrismDialogErrorsPropagateToCallerAsync()
  {
    var prism = new Mock<IDialogService>();
    DialogCallback? callback = null;
    prism.Setup(d => d.ShowDialog(It.IsAny<string>(), It.IsAny<IDialogParameters>(), It.IsAny<DialogCallback>()))
      .Callback<string, IDialogParameters, DialogCallback>((_, _, c) => callback = c);
    var service = new EditorDialogService(prism.Object);
    var pending = service.ConfirmSaveAsync("sample.zasm");
    Assert.IsNotNull(callback);
    await callback.Value.Invoke(new IOException("Dialog failed"));
    await Assert.ThrowsExactlyAsync<IOException>(async () => await pending);
  }

  [TestMethod]
  [DataRow("save", ButtonResult.Yes)]
  [DataRow("discard", ButtonResult.No)]
  [DataRow("cancel", ButtonResult.Cancel)]
  [DataRow("ok", ButtonResult.OK)]
  public void DialogCommandsReturnResultsThroughPrism(string action, ButtonResult expected)
  {
    var viewModel = new EditorDialogViewModel();
    IDialogResult? result = null;
    DialogUtilities.InitializeListener(viewModel, (IDialogResult r) => result = r);
    var command = action switch
    {
      "save" => viewModel.CmdSave,
      "discard" => viewModel.CmdDiscard,
      "cancel" => viewModel.CmdCancel,
      _ => viewModel.CmdOk,
    };
    command.Execute();
    Assert.IsNotNull(result);
    Assert.AreEqual(expected, result.Result);
  }

  [TestMethod]
  public void DialogParametersSetContentAndResetConfirmationMode()
  {
    var viewModel = new EditorDialogViewModel();
    viewModel.OnDialogOpened(new DialogParameters
    {
      { "title", "Unsaved changes" },
      { "message", "Save changes?" },
      { "confirmSave", true },
    });
    Assert.AreEqual("Unsaved changes", viewModel.Title);
    Assert.AreEqual("Save changes?", viewModel.Message);
    Assert.IsTrue(viewModel.IsSaveConfirmation);
    Assert.IsTrue(viewModel.CanCloseDialog());
    viewModel.OnDialogOpened(new DialogParameters
    {
      { "title", "About" },
      { "message", "Zero IDE" },
      { "confirmSave", false },
    });
    Assert.IsFalse(viewModel.IsSaveConfirmation);
    Assert.AreEqual("About", viewModel.Title);
  }
}
