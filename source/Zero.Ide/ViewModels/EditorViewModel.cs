// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using AvaloniaEdit.Document;
using Prism.Commands;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.ViewModels;

public class EditorViewModel : ViewModelBase
{
  private readonly ISettingsService? _settingsService;
  private TextDocument _document = new();
  private IEditorActions? _editorActions;
  private string _fileName = "Untitled";
  private int _revision;
  private string _savedText = string.Empty;
  private string _searchText = string.Empty;

  public EditorViewModel(ISettingsService? settingsService = null)
  {
    CmdUndo = CreateEditorCommand(actions => actions.CanUndo, actions => actions.Undo());
    CmdRedo = CreateEditorCommand(actions => actions.CanRedo, actions => actions.Redo());
    CmdCut = CreateEditorCommand(actions => actions.CanCut, actions => actions.Cut());
    CmdCopy = CreateEditorCommand(actions => actions.CanCopy, actions => actions.Copy());
    CmdPaste = CreateEditorCommand(actions => actions.CanPaste, actions => actions.Paste());
    CmdFindNext = CreateEditorCommand(actions => actions.CanSearch && !string.IsNullOrEmpty(SearchText), actions => actions.FindNext(SearchText));

    _settingsService = settingsService;

    _settingsService?.SettingsChanged += (_, _) => RaisePropertyChanged(nameof(Settings));
    _document.UndoStack.SizeLimit = 1000;
    _document.TextChanged += OnTextChanged;
  }

  public DelegateCommand CmdCopy { get; }
  public DelegateCommand CmdCut { get; }
  public DelegateCommand CmdFindNext { get; }
  public DelegateCommand CmdPaste { get; }
  public DelegateCommand CmdRedo { get; }

  public DelegateCommand CmdUndo { get; }

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

  public string SearchText
  {
    get => _searchText;
    set
    {
      if (SetProperty(ref _searchText, value ?? string.Empty)) CmdFindNext.RaiseCanExecuteChanged();
    }
  }

  public IdeSettings Settings => _settingsService?.Current ?? IdeSettings.Default;

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

  public void AttachEditorActions(IEditorActions actions)
  {
    ArgumentNullException.ThrowIfNull(actions);
    if (ReferenceEquals(_editorActions, actions))
      return;

    _editorActions?.StateChanged -= OnEditorStateChanged;
    _editorActions = actions;
    _editorActions.StateChanged += OnEditorStateChanged;

    RefreshEditorCommands();
  }

  public void DetachEditorActions(IEditorActions actions)
  {
    // A previously detached view must not disconnect a newer editor.
    if (!ReferenceEquals(_editorActions, actions))
      return;

    _editorActions.StateChanged -= OnEditorStateChanged;
    _editorActions = null;
    RefreshEditorCommands();
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

  public void TrimTrailingWhitespace()
  {
    // Edit in place so caret/selection anchors survive and cleanup is one undo step.
    Document.BeginUpdate();
    try
    {
      for (var index = Document.LineCount; index >= 1; index--)
      {
        var line = Document.GetLineByNumber(index);
        var end = line.EndOffset;
        var trimmedEnd = end;
        while (trimmedEnd > line.Offset && char.IsWhiteSpace(Document.GetCharAt(trimmedEnd - 1)))
          trimmedEnd--;

        if (trimmedEnd < end) Document.Remove(trimmedEnd, end - trimmedEnd);
      }
    }
    finally
    {
      Document.EndUpdate();
    }
  }

  private DelegateCommand CreateEditorCommand(Func<IEditorActions, bool> canExecute, Action<IEditorActions> execute) =>
    new(() =>
    {
      if (_editorActions is { } actions && canExecute(actions))
        execute(actions);
    }, () => _editorActions is { } actions && canExecute(actions));

  private void OnEditorStateChanged(object? sender, EventArgs e) => RefreshEditorCommands();

  private void OnTextChanged(object? sender, EventArgs e)
  {
    RaisePropertyChanged(nameof(Text));
    RaisePropertyChanged(nameof(IsModified));
  }

  private void RefreshEditorCommands()
  {
    CmdUndo.RaiseCanExecuteChanged();
    CmdRedo.RaiseCanExecuteChanged();
    CmdCut.RaiseCanExecuteChanged();
    CmdCopy.RaiseCanExecuteChanged();
    CmdPaste.RaiseCanExecuteChanged();
    CmdFindNext.RaiseCanExecuteChanged();
  }
}
