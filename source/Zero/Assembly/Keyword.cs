// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Assembly;

/// <summary>Directives (section, data, text, global import/extern).</summary>
public class Keyword
{
  public const string Target = "#TARGET";

  /// <summary>Import external library keyword for section ".idata".</summary>
  /// <remarks>FASM: <![CDATA[
  ///   section '.idata' import data readable writeable
  ///   library kernel32,'KERNEL32.DLL', user32,'USER32.DLL'
  /// ]]>
  /// </remarks>
  public const string ImportLib = "#IMPORT";
}
