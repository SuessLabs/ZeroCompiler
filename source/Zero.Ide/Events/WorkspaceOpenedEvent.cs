// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Ide.Events;

/// <summary>Signals that the IDE selected a folder workspace and initialized its explorer root; children load lazily.</summary>
/// <param name="RootPath">The fully qualified path of the selected workspace folder shown as the explorer root.</param>
public sealed record WorkspaceOpenedEvent(string RootPath) : IdeEvent;
