// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using AvaloniaEdit.Document;
using Prism.Commands;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.ViewModels;

public class EditorViewModel : ViewModelBase, IDisposable
{
  private readonly List<TextAnchor> _breakpoints = [];
  private readonly ISettingsService? _settingsService;
  private TextDocument _document = new();
  private IEditorActions? _editorActions;
  private IReadOnlySet<int>? _executableLines;
  private int _executionLine;
  private string _fileName = "Untitled";
  private bool _isDebugReadOnly;
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

    _settingsService?.SettingsChanged += OnSettingsChanged;
    _document.UndoStack.SizeLimit = 1000;
    _document.TextChanged += OnTextChanged;
    _document.Changing += OnDocumentChanging;
  }

  public int BreakpointRevision { get; private set; }
  public IReadOnlyList<TextAnchor> Breakpoints => _breakpoints;
  public int CaretLine { get; set; } = 1;
  public DelegateCommand CmdCopy { get; }
  public DelegateCommand CmdCut { get; }
  public DelegateCommand CmdFindNext { get; }
  public DelegateCommand CmdPaste { get; }
  public DelegateCommand CmdRedo { get; }
  public DelegateCommand CmdUndo { get; }
  public TextDocument Document => _document;
  public int ExecutionLine { get => _executionLine; set => SetProperty(ref _executionLine, value); }

  public string FileName
  {
    get => _fileName;
    private set => SetProperty(ref _fileName, value);
  }

  public bool IsDebugReadOnly { get => _isDebugReadOnly; set => SetProperty(ref _isDebugReadOnly, value); }
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

  public void Dispose()
  {
    _settingsService?.SettingsChanged -= OnSettingsChanged;
    _document.TextChanged -= OnTextChanged;
    _document.Changing -= OnDocumentChanging;

    if (_editorActions is not null)
      DetachEditorActions(_editorActions);
  }

  public bool HasBreakpoint(int line)
  {
    foreach (var anchor in _breakpoints)
      if (!anchor.IsDeleted && anchor.Line == line) return true;
    return false;
  }

  public bool IsBreakpointBound(int line) => _executableLines?.Contains(line) == true;

  public void Load(string text, string fileName)
  {
    _breakpoints.Clear();

    _executableLines = null;
    ExecutionLine = 0;

    _savedText = text;

    _document.TextChanged -= OnTextChanged;
    _document.Changing -= OnDocumentChanging;
    _document = new TextDocument(text);
    _document.UndoStack.SizeLimit = 1000;
    _document.TextChanged += OnTextChanged;
    _document.Changing += OnDocumentChanging;

    RaisePropertyChanged(nameof(Document));
    RaisePropertyChanged(nameof(Text));
    FileName = fileName;
    Revision++;

    RaisePropertyChanged(nameof(IsModified));
    RaisePropertyChanged(nameof(Breakpoints));
  }

  public void MarkSaved(string fileName, string savedText)
  {
    _savedText = savedText;
    FileName = fileName;

    RaisePropertyChanged(nameof(IsModified));
  }

  public void SetExecutableLines(IReadOnlySet<int>? lines)
  {
    _executableLines = lines;
    RaisePropertyChanged(nameof(Breakpoints));
  }

  public void ToggleBreakpoint(int line)
  {
    if (line < 1 || line > Document.LineCount) return;
    for (int i = 0; i < _breakpoints.Count; i++)
    {
      if (_breakpoints[i].IsDeleted || _breakpoints[i].Line != line)
        continue;

      _breakpoints.RemoveAt(i);
      BreakpointRevision++;

      RaisePropertyChanged(nameof(Breakpoints));
      RaisePropertyChanged(nameof(BreakpointRevision));
      return;
    }

    var anchor = Document.CreateAnchor(Document.GetLineByNumber(line).Offset);
    anchor.MovementType = AnchorMovementType.AfterInsertion;
    anchor.SurviveDeletion = true;

    _breakpoints.Add(anchor);
    BreakpointRevision++;

    RaisePropertyChanged(nameof(Breakpoints));
    RaisePropertyChanged(nameof(BreakpointRevision));
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

  private void OnDocumentChanging(object? sender, DocumentChangeEventArgs e)
  {
    if (e.RemovalLength == 0) return;
    for (int i = _breakpoints.Count - 1; i >= 0; i--)
    {
      var anchor = _breakpoints[i];
      if (anchor.IsDeleted)
      {
        _breakpoints.RemoveAt(i);
        continue;
      }

      var line = Document.GetLineByNumber(anchor.Line);
      if (line.Offset >= e.Offset && line.EndOffset <= e.Offset + e.RemovalLength)
        _breakpoints.RemoveAt(i);
    }
  }

  private void OnEditorStateChanged(object? sender, EventArgs e) =>
      RefreshEditorCommands();

  private void OnSettingsChanged(object? sender, EventArgs e) =>
    RaisePropertyChanged(nameof(Settings));

  private void OnTextChanged(object? sender, EventArgs e)
  {
    _breakpoints.RemoveAll(anchor => anchor.IsDeleted);
    BreakpointRevision++;
    _executableLines = null;

    RaisePropertyChanged(nameof(Breakpoints));
    RaisePropertyChanged(nameof(BreakpointRevision));
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
