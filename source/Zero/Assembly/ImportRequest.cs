// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Zero.Assembly;

public class ImportRequest
{
  public string Dll { get; set; } = "";

  public List<string> Functions { get; } = [];
}
