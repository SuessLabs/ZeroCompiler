// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Zero.Ide.Models;

namespace Zero.Ide.Services;

public interface ISettingsService
{
  event EventHandler? SettingsChanged;

  IdeSettings Current { get; }

  string FilePath { get; }

  string? LoadError { get; }

  void Save(IdeSettings settings);
}
