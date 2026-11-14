// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia;
using Avalonia.Markup.Xaml;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;
using Zero.Ide.Views;

namespace Zero.Ide;

public partial class App : PrismApplication
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
    base.Initialize();
  }

  protected override AvaloniaObject CreateShell() => Container.Resolve<MainWindow>();

  protected override void RegisterTypes(IContainerRegistry containerRegistry)
  {
    // Services
    containerRegistry.RegisterSingleton<IEditorDialogService, EditorDialogService>();
    containerRegistry.RegisterInstance<ISettingsService>(new IniSettingsService());

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
