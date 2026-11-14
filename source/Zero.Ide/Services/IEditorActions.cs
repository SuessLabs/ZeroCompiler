// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;

namespace Zero.Ide.Services;

public interface IEditorActions
{
  event EventHandler? StateChanged;

  bool CanUndo { get; }
  bool CanRedo { get; }
  bool CanCut { get; }
  bool CanCopy { get; }
  bool CanPaste { get; }

  void Undo();
  void Redo();
  void Cut();
  void Copy();
  void Paste();
}
