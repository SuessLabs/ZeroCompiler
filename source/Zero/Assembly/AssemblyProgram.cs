// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Zero.Assembly;

/// <summary>Format-independent parser output, with section-relative label offsets.</summary>
internal sealed class AssemblyProgram
{
  public List<AsmLine> Lines { get; } = [];
  public List<ImportRequest> Imports { get; } = [];
  public Dictionary<string, int> Labels { get; } = new(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, int> LabelLines { get; } = new(StringComparer.OrdinalIgnoreCase);
  public int CodeSize { get; set; }
  public int? FirstImportLine { get; set; }
}
