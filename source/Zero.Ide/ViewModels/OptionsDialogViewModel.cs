// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Dialogs;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.ViewModels;

public class OptionsDialogViewModel : ViewModelBase, IDialogAware
{
  private readonly IEditorDialogService _dialogs;
  private readonly ISettingsService _settings;
  private string _compilerPath = string.Empty;
  private bool _convertTabsToSpaces;
  private string _errorMessage = string.Empty;
  private string _fontFamily = string.Empty;
  private double _fontSize;
  private int _indentationSize;
  private bool _isBrowsing;
  private OptionsCategory? _selectedCategory;
  private bool _showLineNumbers;
  private string _statusMessage = string.Empty;
  private bool _wordWrap;

  public OptionsDialogViewModel(ISettingsService settings, IEditorDialogService dialogs)
  {
    _settings = settings;
    _dialogs = dialogs;

    Categories =
    [
      new("compiler", "Compiler"),
      new("editor", "Editor")
      {
        Children = [new("appearance", "Appearance"), new("indentation", "Indentation")],
      },
    ];

    SelectedCategory = Categories[0];
  }

  public IReadOnlyList<OptionsCategory> Categories { get; }

  public DelegateCommand CmdApply => new DelegateCommand(
    () => Save(),
    () => !IsBrowsing)
    .ObservesProperty(() => IsBrowsing);

  public AsyncDelegateCommand CmdBrowseCompiler => new AsyncDelegateCommand(
    BrowseCompilerAsync,
    () => !IsBrowsing)
    .ObservesProperty(() => IsBrowsing);

  public DelegateCommand CmdCancel => new DelegateCommand(
    () => RequestClose.Invoke(ButtonResult.Cancel),
    () => !IsBrowsing)
    .ObservesProperty(() => IsBrowsing);

  public DelegateCommand CmdOk => new DelegateCommand(
    () =>
    {
      if (Save())
        RequestClose.Invoke(ButtonResult.OK);
    },
    () => !IsBrowsing)
    .ObservesProperty(() => IsBrowsing);

  public string CompilerPath { get => _compilerPath; set => SetProperty(ref _compilerPath, value); }

  public bool ConvertTabsToSpaces { get => _convertTabsToSpaces; set => SetProperty(ref _convertTabsToSpaces, value); }

  public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

  public string FontFamily { get => _fontFamily; set => SetProperty(ref _fontFamily, value); }

  public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }

  public int IndentationSize { get => _indentationSize; set => SetProperty(ref _indentationSize, value); }

  public bool IsBrowsing { get => _isBrowsing; private set => SetProperty(ref _isBrowsing, value); }

  public string PageTitle => SelectedCategory?.Title ?? "Options";

  public DialogCloseListener RequestClose { get; }

  public OptionsCategory? SelectedCategory
  {
    get => _selectedCategory;
    set
    {
      if (!SetProperty(ref _selectedCategory, value)) return;
      RaisePropertyChanged(nameof(PageTitle));
      RaisePropertyChanged(nameof(ShowCompiler));
      RaisePropertyChanged(nameof(ShowAppearance));
      RaisePropertyChanged(nameof(ShowIndentation));
    }
  }

  public string SettingsFilePath => _settings.FilePath;
  public bool ShowAppearance => SelectedCategory?.Key is "editor" or "appearance";
  public bool ShowCompiler => SelectedCategory?.Key == "compiler";
  public bool ShowIndentation => SelectedCategory?.Key is "editor" or "indentation";
  public bool ShowLineNumbers { get => _showLineNumbers; set => SetProperty(ref _showLineNumbers, value); }
  public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
  public bool WordWrap { get => _wordWrap; set => SetProperty(ref _wordWrap, value); }

  public bool CanCloseDialog() => !IsBrowsing;

  public void OnDialogClosed()
  {
  }

  public void OnDialogOpened(IDialogParameters parameters)
  {
    var settings = _settings.Current;
    CompilerPath = settings.CompilerPath;
    FontFamily = settings.FontFamily;
    FontSize = settings.FontSize;
    IndentationSize = settings.IndentationSize;
    ConvertTabsToSpaces = settings.ConvertTabsToSpaces;
    ShowLineNumbers = settings.ShowLineNumbers;
    WordWrap = settings.WordWrap;
    SelectedCategory = Categories[0];
    ErrorMessage = _settings.LoadError ?? string.Empty;
    StatusMessage = string.Empty;
  }

  private async Task BrowseCompilerAsync()
  {
    IsBrowsing = true;
    ErrorMessage = string.Empty;

    try
    {
      var path = await _dialogs.BrowseCompilerAsync();
      if (path is not null)
        CompilerPath = path;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsBrowsing = false;
    }
  }

  private bool Save()
  {
    ErrorMessage = string.Empty;
    StatusMessage = string.Empty;
    try
    {
      _settings.Save(new IdeSettings
      {
        CompilerPath = CompilerPath.Trim(),
        FontFamily = FontFamily.Trim(),
        FontSize = FontSize,
        IndentationSize = IndentationSize,
        ConvertTabsToSpaces = ConvertTabsToSpaces,
        ShowLineNumbers = ShowLineNumbers,
        WordWrap = WordWrap,
      });

      StatusMessage = "Settings saved.";
      return true;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      return false;
    }
  }
}
