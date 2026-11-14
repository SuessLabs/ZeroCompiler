// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia;
using Avalonia.Markup.Xaml;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;
using Zero.Ide.Services;
using Zero.Ide.Themes;
using Zero.Ide.ViewModels;
using Zero.Ide.Views;

namespace Zero.Ide;

public partial class App : PrismApplication
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
    Resources.MergedDictionaries.Add(IdeThemes.CreateResources());
    base.Initialize();
  }

  protected override AvaloniaObject CreateShell() => Container.Resolve<MainWindow>();

  protected override void RegisterTypes(IContainerRegistry containerRegistry)
  {
    // Services
    containerRegistry.RegisterSingleton<IEditorDialogService, EditorDialogService>();

    var settings = new IniSettingsService();
    containerRegistry.RegisterInstance<ISettingsService>(settings);
    RequestedThemeVariant = IdeThemes.GetVariant(settings.Current.Theme);
    settings.SettingsChanged += (_, _) => RequestedThemeVariant = IdeThemes.GetVariant(settings.Current.Theme);

    // ViewModels
    containerRegistry.RegisterSingleton<EditorViewModel>();
    containerRegistry.RegisterSingleton<MainWindowViewModel>();

    // Dialogs
    containerRegistry.RegisterDialog<EditorDialogView, EditorDialogViewModel>();
    containerRegistry.RegisterDialogWindow<EditorDialogWindow>(nameof(EditorDialogWindow));
    containerRegistry.RegisterDialog<AboutDialogView, AboutDialogViewModel>();
    containerRegistry.RegisterDialogWindow<AboutDialogWindow>(nameof(AboutDialogWindow));
    containerRegistry.RegisterDialog<OptionsDialogView, OptionsDialogViewModel>();
    containerRegistry.RegisterDialogWindow<OptionsDialogWindow>(nameof(OptionsDialogWindow));

    // Windows
    ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
    ViewModelLocationProvider.Register<EditorView, EditorViewModel>();
  }
}
