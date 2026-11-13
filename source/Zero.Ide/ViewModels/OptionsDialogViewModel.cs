// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Prism.Commands;
using Prism.Dialogs;

namespace Zero.Ide.ViewModels;

public class OptionsDialogViewModel : ViewModelBase, IDialogAware
{
  public OptionsDialogViewModel()
  {
  }

  public DelegateCommand CmdClose => new(() => RequestClose.Invoke(ButtonResult.OK));

  public DialogCloseListener RequestClose { get; }

  public bool CanCloseDialog() => true;

  public void OnDialogClosed()
  {
  }

  public void OnDialogOpened(IDialogParameters parameters)
  {
  }
}
