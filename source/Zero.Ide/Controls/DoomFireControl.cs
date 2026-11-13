// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Zero.Ide.Controls;

/// <summary>A small, palette-based Doom fire animation, active only while attached.</summary>
public sealed class DoomFireControl : Control
{
  private const int FireHeight = 96;
  private const int FireWidth = 252;
  private static readonly int[] Palette = CreatePalette();
  private readonly byte[] _heat = new byte[FireWidth * FireHeight];
  private readonly int[] _pixels = new int[FireWidth * FireHeight];
  private readonly Random _random = new();
  private readonly DispatcherTimer _timer;
  private WriteableBitmap? _bitmap;

  public DoomFireControl()
  {
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
    _timer.Tick += OnTick;
    RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
  }

  public override void Render(DrawingContext context)
  {
    base.Render(context);
    if (_bitmap is not null)
      context.DrawImage(_bitmap, new Rect(Bounds.Size));
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);

    _bitmap = new WriteableBitmap(new PixelSize(FireWidth, FireHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    Array.Clear(_heat);
    // Pre-warm so the dialog opens with visible flames rather than an empty frame.
    for (var frame = 0; frame < FireHeight * 2; frame++)
      SpreadFire();

    UpdateBitmap();
    _timer.Start();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    _timer.Stop();
    _bitmap?.Dispose();
    _bitmap = null;
    base.OnDetachedFromVisualTree(e);
  }

  private static int[] CreatePalette()
  {
    // Original-style 37-step palette: black, red, orange, yellow, warm white.
    int[] colors =
    [
      0x101722, 0x230607, 0x370707, 0x470707, 0x570707, 0x670707,
      0x770707, 0x8F0707, 0x9F0707, 0xAF0F07, 0xBF1707, 0xC71F07,
      0xDF2707, 0xDF2F07, 0xDF3707, 0xDF3F07, 0xDF4707, 0xDF4F07,
      0xDF5707, 0xD75F07, 0xD7670F, 0xCF6F0F, 0xCF7717, 0xCF7F17,
      0xCF871F, 0xC78F1F, 0xC79727, 0xC79F2F, 0xBFA737, 0xBFAF3F,
      0xBFB747, 0xBFBF4F, 0xCFCF6F, 0xDFDF9F, 0xEFEFC7, 0xF7F7DF, 0xFFFFFF,
    ];

    for (var i = 0; i < colors.Length; i++)
      colors[i] = unchecked((int)0xFF000000) | colors[i];

    return colors;
  }

  private void OnTick(object? sender, EventArgs e)
  {
    SpreadFire();
    UpdateBitmap();
    InvalidateVisual();
  }

  private void SpreadFire()
  {
    Array.Fill(_heat, (byte)(Palette.Length - 1), FireWidth * (FireHeight - 1), FireWidth);
    // Propagate each source pixel upward with Doom-style random drift and cooling.
    for (var x = 0; x < FireWidth; x++)
    {
      for (var y = 1; y < FireHeight; y++)
      {
        var heat = _heat[(y * FireWidth) + x];
        var drift = _random.Next(4);
        var destinationX = (x - drift + 1 + FireWidth) % FireWidth;
        _heat[((y - 1) * FireWidth) + destinationX] = (byte)Math.Max(0, heat - (drift & 1));
      }
    }
  }

  private void UpdateBitmap()
  {
    if (_bitmap is null) return;
    for (var i = 0; i < _heat.Length; i++)
      _pixels[i] = Palette[_heat[i]];

    using var framebuffer = _bitmap.Lock();

    for (var y = 0; y < FireHeight; y++)
      Marshal.Copy(_pixels, y * FireWidth, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), FireWidth);
  }
}
