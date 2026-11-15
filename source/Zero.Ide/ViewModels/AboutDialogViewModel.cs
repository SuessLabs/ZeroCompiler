// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Prism.Commands;
using Prism.Dialogs;

namespace Zero.Ide.ViewModels;

public class AboutDialogViewModel : ViewModelBase, IDialogAware
{
  public AboutDialogViewModel()
  {
    CmdClose = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
  }

  public DelegateCommand CmdClose { get; }

  public string CompilerVersion { get; } = global::Zero.Zero.Version.Split('+')[0];

  public string Copyright { get; } = typeof(App).Assembly
    .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

  public string IdeVersion { get; } = typeof(App).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
    ?? typeof(App).Assembly.GetName().Version?.ToString() ?? "Unknown";

  public DialogCloseListener RequestClose { get; }

  public bool CanCloseDialog() => true;

  public void OnDialogClosed()
  {
  }

  public void OnDialogOpened(IDialogParameters parameters)
  {
  }
}
