// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Prism.Commands;
using Prism.Dialogs;

namespace Zero.Ide.ViewModels;

public class EditorDialogViewModel : ViewModelBase, IDialogAware
{
  private string _title = string.Empty;
  private string _message = string.Empty;
  private bool _isSaveConfirmation;

  public EditorDialogViewModel()
  {
    CmdSave = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Yes));
    CmdDiscard = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.No));
    CmdCancel = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
    CmdOk = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
  }

  public DelegateCommand CmdCancel { get; }
  public DelegateCommand CmdDiscard { get; }
  public DelegateCommand CmdOk { get; }
  public DelegateCommand CmdSave { get; }

  public bool IsSaveConfirmation
  {
    get => _isSaveConfirmation;
    private set => SetProperty(ref _isSaveConfirmation, value);
  }

  public string Message
  {
    get => _message;
    private set => SetProperty(ref _message, value);
  }

  // Prism initializes this listener when it opens the dialog.
  public DialogCloseListener RequestClose { get; }

  public string Title
  {
    get => _title;
    private set => SetProperty(ref _title, value);
  }

  public bool CanCloseDialog() => true;

  public void OnDialogClosed()
  {
  }

  public void OnDialogOpened(IDialogParameters parameters)
  {
    Title = parameters.GetValue<string>("title");
    Message = parameters.GetValue<string>("message");
    IsSaveConfirmation = parameters.GetValue<bool>("confirmSave");
  }
}
