// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using AvaloniaEdit.Document;

namespace Zero.Ide.ViewModels;

public class EditorViewModel : ViewModelBase
{
  private TextDocument _document = new();
  private string _fileName = "Untitled";
  private int _revision;
  private string _savedText = string.Empty;

  public EditorViewModel()
  {
    _document.UndoStack.SizeLimit = 1000;
    _document.TextChanged += OnTextChanged;
  }

  public TextDocument Document => _document;

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
    get => Document.Text;
    set
    {
      var text = value ?? string.Empty;
      if (Document.Text != text)
        Document.Text = text;
    }
  }

  public void Load(string text, string fileName)
  {
    _savedText = text;
    _document.TextChanged -= OnTextChanged;
    _document = new TextDocument(text);
    _document.UndoStack.SizeLimit = 1000;
    _document.TextChanged += OnTextChanged;
    RaisePropertyChanged(nameof(Document));
    RaisePropertyChanged(nameof(Text));
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

  private void OnTextChanged(object? sender, EventArgs e)
  {
    RaisePropertyChanged(nameof(Text));
    RaisePropertyChanged(nameof(IsModified));
  }
}
