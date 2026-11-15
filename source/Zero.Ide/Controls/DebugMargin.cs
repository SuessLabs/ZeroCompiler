// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Controls;

internal sealed class DebugMargin(Func<EditorViewModel?> getModel) : AbstractMargin
{
  public static readonly StyledProperty<IBrush?> GutterBrushProperty =
    AvaloniaProperty.Register<DebugMargin, IBrush?>(nameof(GutterBrush));

  static DebugMargin() => AffectsRender<DebugMargin>(GutterBrushProperty);

  public IBrush? GutterBrush
  {
    get => GetValue(GutterBrushProperty);
    set => SetValue(GutterBrushProperty, value);
  }

  public override void Render(DrawingContext context)
  {
    // Keep the entire breakpoint hit area visible, including lines without a marker.
    context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    if (GutterBrush is { } gutter)
    {
      using (context.PushOpacity(0.28))
        context.FillRectangle(gutter, new Rect(Bounds.Size));
    }

    if (TextView is not { VisualLinesValid: true } view || getModel() is not { } model)
      return;

    foreach (var line in view.VisualLines)
    {
      int number = line.FirstDocumentLine.LineNumber;
      double y = line.VisualTop - view.VerticalOffset + line.TextLines[0].Height / 2;

      if (model.HasBreakpoint(number))
      {
        context.DrawEllipse(
          model.IsBreakpointBound(number) ? Brushes.IndianRed : null,
          new Pen(Brushes.IndianRed, 2),
          new Point(8, y),
          5,
          5);
      }

      if (model.ExecutionLine >= number && model.ExecutionLine <= line.LastDocumentLine.LineNumber)
      {
        var pen = new Pen(Brushes.Gold, 2);
        context.DrawLine(pen, new Point(17, y), new Point(27, y));
        context.DrawLine(pen, new Point(22, y - 5), new Point(27, y));
        context.DrawLine(pen, new Point(22, y + 5), new Point(27, y));
      }
    }
  }

  protected override Size MeasureOverride(Size availableSize) => new(30, 0);

  protected override void OnPointerPressed(PointerPressedEventArgs e)
  {
    base.OnPointerPressed(e);

    if (TextView is not { VisualLinesValid: true } view || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
      return;

    var line = view.GetVisualLineFromVisualTop(e.GetPosition(this).Y + view.VerticalOffset);

    if (line is null)
      return;

    getModel()?.ToggleBreakpoint(line.FirstDocumentLine.LineNumber);
    TextArea?.Focus();
    e.Handled = true;
  }
}

internal sealed class ExecutionLineRenderer(Func<EditorViewModel?> getModel) : IBackgroundRenderer
{
  private static readonly IBrush Background = new SolidColorBrush(Color.FromArgb(65, 255, 200, 0));
  public KnownLayer Layer => KnownLayer.Background;

  public void Draw(TextView textView, DrawingContext context)
  {
    int number = getModel()?.ExecutionLine ?? 0;
    if (number == 0 || !textView.VisualLinesValid)
      return;

    foreach (var line in textView.VisualLines)
    {
      if (number >= line.FirstDocumentLine.LineNumber && number <= line.LastDocumentLine.LineNumber)
        context.FillRectangle(
          Background,
          new Rect(0, line.VisualTop - textView.VerticalOffset, textView.Bounds.Width, line.Height));
    }
  }
}
