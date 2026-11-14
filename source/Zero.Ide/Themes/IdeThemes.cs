// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Zero.Ide.Models;

namespace Zero.Ide.Themes;

public static class IdeThemes
{
  public static IReadOnlyList<ThemeChoice> Choices { get; } = Array.AsReadOnly<ThemeChoice>(
  [
    new(IdeTheme.System, "Use system setting"),
    new(IdeTheme.Light, "Light"),
    new(IdeTheme.Dark, "Dark"),
    new(IdeTheme.CoolSlate, "Cool Slate"),
  ]);

  public static ThemeVariant CoolSlateVariant { get; } = new("CoolSlate", ThemeVariant.Dark);

  public static ResourceDictionary CreateResources()
  {
    var resources = new ResourceDictionary();

    // Neutral Visual Studio-inspired surfaces, with a separate blue-gray Slate variant.
    resources.ThemeDictionaries[ThemeVariant.Light] = CreatePalette(
      "#F5F5F5", "#FFFFFF", "#EBEBEB", "#FFFFFF", "#1F1F1F", "#616161", "#D4D4D4", "#E2E7EF", "#CDE4FF", "#0078D4", "#ADD6FF", "#6E7681");

    resources.ThemeDictionaries[ThemeVariant.Dark] = CreatePalette(
      "#1F1F1F", "#252526", "#2B2B2B", "#1E1E1E", "#D4D4D4", "#A6A6A6", "#414141", "#37373D", "#094771", "#4DAAFC", "#264F78", "#858585");

    resources.ThemeDictionaries[CoolSlateVariant] = CreatePalette(
      "#242B35", "#2B3440", "#303C4A", "#202731", "#DFE7F0", "#A5B7CC", "#465568", "#39495D", "#365A7A", "#8BBDEB", "#3B556F", "#8B9EB4");

    return resources;
  }

  public static ThemeVariant GetVariant(IdeTheme theme) => theme switch
  {
    IdeTheme.Light => ThemeVariant.Light,
    IdeTheme.Dark => ThemeVariant.Dark,
    IdeTheme.CoolSlate => CoolSlateVariant,
    _ => ThemeVariant.Default,
  };

  public static bool IsDark(ThemeVariant variant) => variant == ThemeVariant.Dark || variant == CoolSlateVariant;

  private static ResourceDictionary CreatePalette(string window, string panel, string chrome, string editor,
    string text, string secondary, string border, string hover, string selected, string accent, string selection, string lineNumbers)
  {
    var resources = new ResourceDictionary();
    var colors = new Dictionary<string, string>
    {
      ["Window"] = window,
      ["Panel"] = panel,
      ["Chrome"] = chrome,
      ["Editor"] = editor,
      ["Text"] = text,
      ["Secondary"] = secondary,
      ["Border"] = border,
      ["Hover"] = hover,
      ["Selected"] = selected,
      ["Accent"] = accent,
      ["Selection"] = selection,
      ["LineNumbers"] = lineNumbers,
    };
    foreach (var (name, value) in colors)
    {
      var color = Color.Parse(value);
      resources[$"Ide{name}Color"] = color;
      resources[$"Ide{name}Brush"] = new SolidColorBrush(color);
    }

    // Fluent controls and their popup templates consume these dynamic resources.
    foreach (var key in new[]
    {
      "ButtonBackground",
      "ComboBoxBackground",
      "TextControlBackground",
      "MenuFlyoutPresenterBackground",
    })
      resources[key] = resources["IdePanelBrush"];

    foreach (var key in new[]
    {
      "ButtonBackgroundPointerOver",
      "ComboBoxBackgroundPointerOver",
      "MenuItemBackgroundPointerOver",
    })
      resources[key] = resources["IdeHoverBrush"];

    foreach (var key in new[]
    {
      "ButtonForeground",
      "ComboBoxForeground",
      "TextControlForeground",
      "MenuItemForeground",
    })
      resources[key] = resources["IdeTextBrush"];

    resources["TreeViewItemBackgroundSelected"] = resources["IdeSelectedBrush"];
    resources["TreeViewItemBackgroundSelectedPointerOver"] = resources["IdeSelectedBrush"];
    resources["SystemControlForegroundBaseLowBrush"] = resources["IdeBorderBrush"];

    return resources;
  }
}
