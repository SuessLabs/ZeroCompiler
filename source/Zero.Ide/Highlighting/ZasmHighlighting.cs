// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Zero.Assembly;

namespace Zero.Ide.Highlighting;

public static class ZasmHighlighting
{
  private static readonly Lazy<IHighlightingDefinition> Dark = new(() => Load(true));
  private static readonly Lazy<IHighlightingDefinition> Light = new(() => Load(false));

  public static IHighlightingDefinition Create(bool darkTheme) => darkTheme ? Dark.Value : Light.Value;

  private static IHighlightingDefinition Load(bool darkTheme)
  {
    using var stream = typeof(ZasmHighlighting).Assembly.GetManifestResourceStream("Zero.Ide.Highlighting.Zasm.xshd")
      ?? throw new InvalidOperationException("The ZASM highlighting resource is missing.");

    var syntax = XDocument.Load(stream);
    XNamespace ns = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";

    var instructions = syntax.Descendants(ns + "Keywords").Single(e => (string?)e.Attribute("color") == "Instruction");
    var words = instructions.Elements(ns + "Word").Select(e => e.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var field in typeof(Mnemonic).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
      if (!field.IsLiteral || field.FieldType != typeof(string))
        continue;

      var word = (string)field.GetRawConstantValue()!;

      if (word is "DB" or "DW" or "DD" or "DQ" || !words.Add(word))
        continue;

      instructions.Add(new XElement(ns + "Word", word));
    }

    if (!darkTheme)
    {
      var colors = new Dictionary<string, string>
      {
        ["Comment"] = "#39712D",
        ["String"] = "#9C2F16",
        ["Directive"] = "#7A238C",
        ["Instruction"] = "#0000AA",
        ["Register"] = "#005A70",
        ["Number"] = "#14664E",
        ["Label"] = "#795E26",
        ["Data"] = "#006F63",
        ["Target"] = "#795E26",
      };

      foreach (var color in syntax.Root!.Elements(ns + "Color"))
        color.SetAttributeValue("foreground", colors[(string)color.Attribute("name")!]);
    }

    using var reader = syntax.CreateReader();
    return HighlightingLoader.Load(reader, HighlightingManager.Instance);
  }
}
