// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Avalonia.Themes.Fluent;
using Dock.Model.Core;
using Moq;
using Zero.Dbg;
using Zero.Ide.Docking;
using Zero.Ide.Services;
using Zero.Ide.Themes;
using Zero.Ide.ViewModels;
using Zero.Ide.Views;

namespace Zero.Ide.Tests;

[TestClass]
[DoNotParallelize]
public class DebuggerDockRenderingTests
{
  [TestMethod]
  [DataRow(false)]
  [DataRow(true)]
  public async Task DebuggerRendersAfterMovingLeftAndIntoDocumentDockAsync(bool dark)
  {
    using var session = HeadlessUnitTestSession.StartNew(typeof(DockingTestApp));
    await session.Dispatch(() =>
    {
      Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
      var debug = new Mock<IDebugSession>();
      var shell = new MainWindowViewModel(new EditorViewModel(), new Mock<IEditorDialogService>().Object, debugSession: debug.Object);
      var window = new Window
      {
        Width = 1200,
        Height = 800,
        Content = new DockControl { Layout = shell.Layout, Factory = shell.DockFactory, InitializeFactory = false, InitializeLayout = false },
      };

      try
      {
        window.Show();
        debug.Raise(s => s.Changed += null, debug.Object,
          new DebugSnapshot(DebugState.Paused, "Breakpoint", Registers: [new("EAX", 42)]));

        AssertDebuggerVisible(window, shell);

        shell.DockFactory.MoveDockable((IDock)shell.Debugger.Owner!, (IDock)shell.SolutionExplorer.Owner!, shell.Debugger, shell.SolutionExplorer);
        AssertDebuggerVisible(window, shell);

        shell.DockFactory.DockAsDocument(shell.Debugger);
        AssertDebuggerVisible(window, shell);

        for (var index = 0; index < 3; index++)
        {
          shell.DockFactory.Activate(shell.ActiveDocument!);
          window.UpdateLayout();
          Dispatcher.UIThread.RunJobs();
          var editor = window.GetVisualDescendants().OfType<EditorView>().Single(view => view.IsEffectivelyVisible);
          Assert.AreSame(shell.Editor, editor.DataContext);
          Assert.AreSame(shell.Editor.Document, editor.Editor.Document);
          shell.DockFactory.SetActiveDockable(shell.Debugger);
          AssertDebuggerVisible(window, shell);
        }

        debug.Raise(s => s.Changed += null, debug.Object, new DebugSnapshot(DebugState.Exited, "Exited"));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.AreNotSame(shell.Debugger, ((IDock)shell.ActiveDocument!.Owner!).ActiveDockable);
        Assert.IsFalse(window.GetVisualDescendants().OfType<DebuggerView>().Any(view => view.IsEffectivelyVisible));

        debug.Raise(s => s.Changed += null, debug.Object,
          new DebugSnapshot(DebugState.Paused, "Breakpoint", Registers: [new("EAX", 42)]));

        AssertDebuggerVisible(window, shell);
        shell.DockFactory.MoveDockable((IDock)shell.Debugger.Owner!, (IDock)shell.SolutionExplorer.Owner!, shell.Debugger, shell.SolutionExplorer);
        AssertDebuggerVisible(window, shell);
      }
      finally
      {
        window.Close();
      }
    }, CancellationToken.None);
  }

  [TestMethod]
  public async Task FloatingDebuggerRendersAcrossDebugSessionsAsync()
  {
    using var session = HeadlessUnitTestSession.StartNew(typeof(DockingTestApp));
    await session.Dispatch(() =>
    {
      var debug = new Mock<IDebugSession>();
      var shell = new MainWindowViewModel(new EditorViewModel(), new Mock<IEditorDialogService>().Object, debugSession: debug.Object);

      var window = new Window
      {
        Width = 1200,
        Height = 800,
        Content = new DockControl { Layout = shell.Layout, Factory = shell.DockFactory, InitializeFactory = false, InitializeLayout = false },
      };

      try
      {
        window.Show();
        debug.Raise(s => s.Changed += null, debug.Object,
          new DebugSnapshot(DebugState.Paused, "Breakpoint", Registers: [new("EAX", 42)]));

        AssertDebuggerVisible(window, shell);

        shell.DockFactory.FloatDockable(shell.Debugger);
        var host = (Window)shell.DockFactory.FindRoot(shell.Debugger, _ => true)!.Window!.Host!;

        AssertDebuggerVisible(host, shell);

        debug.Raise(s => s.Changed += null, debug.Object, new DebugSnapshot(DebugState.Exited, "Exited"));
        Assert.IsFalse(host.IsVisible);

        debug.Raise(s => s.Changed += null, debug.Object,
          new DebugSnapshot(DebugState.Paused, "Breakpoint", Registers: [new("EAX", 42)]));

        Assert.IsTrue(host.IsVisible);
        AssertDebuggerVisible(host, shell);

        debug.Raise(s => s.Changed += null, debug.Object, new DebugSnapshot(DebugState.Exited, "Exited"));

        var closed = false;
        host.Closed += (_, _) => closed = true;
        shell.DockFactory.CloseFloatingWindows();

        Assert.IsFalse(closed, "Unapproved application close must not destroy floating windows.");

        shell.DockFactory.ApproveApplicationClose();
        window.Close();
        shell.DockFactory.CloseFloatingWindows();

        Assert.IsTrue(closed, "Hidden floating debugger windows must close when the IDE exits.");
      }
      finally
      {
        shell.DockFactory.ApproveApplicationClose();

        foreach (var floating in shell.Layout.Windows?.ToArray() ?? [])
          floating.Exit();

        window.Close();
      }
    }, CancellationToken.None);
  }

  private static void AssertDebuggerVisible(Window window, MainWindowViewModel shell)
  {
    window.UpdateLayout();
    Dispatcher.UIThread.RunJobs();
    window.UpdateLayout();
    var view = window.GetVisualDescendants().OfType<DebuggerView>().Single(view => view.IsEffectivelyVisible);

    Assert.AreSame(shell.Debugger, view.DataContext);

    var tree = view.FindControl<TreeView>("ValuesTree")!;

    Assert.AreSame(shell.Debugger.Values, tree.ItemsSource);
    Assert.Contains(text => text.Text == "0x0000002A", view.GetVisualDescendants().OfType<SelectableTextBlock>());
  }

  public sealed class DockingTestApp : Application
  {
    public override void Initialize()
    {
      RequestedThemeVariant = ThemeVariant.Light;
      Styles.Add(new FluentTheme());
      Styles.Add(new DockFluentTheme());

      Resources.MergedDictionaries.Add(IdeThemes.CreateResources());

      Styles.Add(new StyleInclude(new Uri("avares://Zero.Ide/")) { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
      Styles.Add(new StyleInclude(new Uri("avares://Zero.Ide/")) { Source = new Uri("avares://Zero.Ide/Themes/IdeStyles.axaml") });
      Styles.Add(new StyleInclude(new Uri("avares://Zero.Ide/")) { Source = new Uri("avares://Zero.Ide/Themes/DockDocumentStyles.axaml") });

      DataTemplates.Add(new DockViewLocator());
    }
  }
}
