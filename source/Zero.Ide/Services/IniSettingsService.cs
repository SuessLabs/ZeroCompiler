// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Zero.Ide.Models;

namespace Zero.Ide.Services;

public sealed class IniSettingsService : ISettingsService
{
  public IniSettingsService() : this(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zero", "Zero.Ide", "settings.ini"))
  {
  }

  public IniSettingsService(string filePath)
  {
    FilePath = Path.GetFullPath(filePath);
    try
    {
      var values = ReadValues();
      var defaults = IdeSettings.Default;

      Current = new IdeSettings
      {
        CompilerPath = Get(values, "Compiler", "Path", defaults.CompilerPath),
        FontFamily = ReadFontFamily(Get(values, "Editor", "FontFamily", defaults.FontFamily)),
        FontSize = ReadNumber(Get(values, "Editor", "FontSize", ""), defaults.FontSize, 8, 48),
        IndentationSize = ReadInteger(Get(values, "Editor", "IndentationSize", ""), defaults.IndentationSize),
        ConvertTabsToSpaces = ReadBoolean(Get(values, "Editor", "ConvertTabsToSpaces", ""), defaults.ConvertTabsToSpaces),
        ShowLineNumbers = ReadBoolean(Get(values, "Editor", "ShowLineNumbers", ""), defaults.ShowLineNumbers),
        WordWrap = ReadBoolean(Get(values, "Editor", "WordWrap", ""), defaults.WordWrap),
      };
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      Current = IdeSettings.Default;
      LoadError = $"Settings could not be loaded. Defaults are in use. {ex.Message}";
    }
  }

  public event EventHandler? SettingsChanged;

  public IdeSettings Current { get; private set; }

  public string FilePath { get; }

  public string? LoadError { get; private set; }

  public void Save(IdeSettings settings)
  {
    settings.Validate();
    // Re-read so settings owned by future versions or plugins are retained.
    var values = ReadValues();

    Set(values, "Compiler", "Path", settings.CompilerPath);
    Set(values, "Editor", "FontFamily", settings.FontFamily);
    Set(values, "Editor", "FontSize", settings.FontSize.ToString(CultureInfo.InvariantCulture));
    Set(values, "Editor", "IndentationSize", settings.IndentationSize.ToString(CultureInfo.InvariantCulture));
    Set(values, "Editor", "ConvertTabsToSpaces", settings.ConvertTabsToSpaces.ToString().ToLowerInvariant());
    Set(values, "Editor", "ShowLineNumbers", settings.ShowLineNumbers.ToString().ToLowerInvariant());
    Set(values, "Editor", "WordWrap", settings.WordWrap.ToString().ToLowerInvariant());

    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    var temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

    try
    {
      using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
          writer.WriteLine("; Zero IDE settings");
          foreach (var section in values)
          {
            if (section.Key.Length > 0)
              writer.WriteLine($"[{section.Key}]");

            foreach (var entry in section.Value)
              writer.WriteLine($"{entry.Key}={Encode(entry.Value)}");

            writer.WriteLine();
          }
        }

        stream.Flush(true);
      }

      File.Move(temporaryPath, FilePath, true);
    }
    finally
    {
      if (File.Exists(temporaryPath))
        File.Delete(temporaryPath);
    }

    Current = settings;
    LoadError = null;
    SettingsChanged?.Invoke(this, EventArgs.Empty);
  }

  private static string Encode(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

  private static string Get(Dictionary<string, Dictionary<string, string>> values, string section, string key, string fallback) =>
    values.TryGetValue(section, out var entries) && entries.TryGetValue(key, out var value) ? value : fallback;

  private static bool ReadBoolean(string text, bool fallback) => bool.TryParse(text, out var value) ? value : fallback;

  private static string ReadFontFamily(string text) => string.IsNullOrWhiteSpace(text) ? IdeSettings.Default.FontFamily : text;

  private static int ReadInteger(string text, int fallback) =>
    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 8 ? value : fallback;

  private static double ReadNumber(string text, double fallback, double minimum, double maximum) =>
    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
      && double.IsFinite(value) && value >= minimum && value <= maximum ? value : fallback;

  private static void Set(Dictionary<string, Dictionary<string, string>> values, string section, string key, string value)
  {
    if (!values.TryGetValue(section, out var entries))
    {
      entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      values.Add(section, entries);
    }

    entries[key] = value;
  }

  private Dictionary<string, Dictionary<string, string>> ReadValues()
  {
    var values = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    if (!File.Exists(FilePath))
      return values;

    var section = string.Empty;

    foreach (var sourceLine in File.ReadAllLines(FilePath))
    {
      var line = sourceLine.Trim();
      if (line.Length == 0 || line[0] is ';' or '#')
        continue;

      if (line.StartsWith('[') && line.EndsWith(']'))
      {
        section = line[1..^1].Trim();
        continue;
      }

      var separator = line.IndexOf('=');
      if (separator < 1)
        continue;

      var key = line[..separator].Trim();
      var value = line[(separator + 1)..].Trim();

      if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        value = value[1..^1].Replace("\"\"", "\"");

      Set(values, section, key, value);
    }

    return values;
  }
}
