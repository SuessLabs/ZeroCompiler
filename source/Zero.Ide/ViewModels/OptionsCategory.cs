// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Zero.Ide.ViewModels;

public sealed record OptionsCategory(string Key, string Title)
{
  public IReadOnlyList<OptionsCategory> Children { get; init; } = [];
}
