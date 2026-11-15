// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Text;
using Zero.Ide.Services;

var destination = Path.GetFullPath(args.Length > 0 ? args[0] : "docs/images/ide-execution");
Directory.CreateDirectory(destination);
foreach (var mode in Enum.GetValues<EditorExecutionMode>())
{
  var path = Path.Combine(destination, $"{mode.ToString().ToLowerInvariant()}.dot");
  File.WriteAllText(path, EditorExecutionCoordinator.ExportStateDiagram(mode), new UTF8Encoding(false));
  Console.WriteLine(path);
}
