// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Ide.ViewModels;

public class EditorViewModel : ViewModelBase
{
  private string _fileName = "Untitled";
  private int _revision;
  private string _savedText = string.Empty;
  private string _text = string.Empty;

  public string FileName
  {
    get => _fileName;
    private set => SetProperty(ref _fileName, value);
  }

  public bool IsModified => Text != _savedText;

  // A new document must start with a fresh undo history.
  public int Revision
  {
    get => _revision;
    private set => SetProperty(ref _revision, value);
  }

  public string Text
  {
    get => _text;
    set
    {
      if (SetProperty(ref _text, value ?? string.Empty))
        RaisePropertyChanged(nameof(IsModified));
    }
  }

  public void Load(string text, string fileName)
  {
    _savedText = text;
    Text = text;
    FileName = fileName;
    Revision++;

    RaisePropertyChanged(nameof(IsModified));
  }

  public void MarkSaved(string fileName, string savedText)
  {
    _savedText = savedText;
    FileName = fileName;

    RaisePropertyChanged(nameof(IsModified));
  }
}
