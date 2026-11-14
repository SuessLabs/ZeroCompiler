// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;

namespace Zero.Ide.Services;

public interface IEditorActions
{
  event EventHandler? StateChanged;

  bool CanCopy { get; }
  bool CanCut { get; }
  bool CanPaste { get; }
  bool CanRedo { get; }
  bool CanSearch { get; }
  bool CanUndo { get; }

  void Copy();

  void Cut();

  void FindNext(string text);

  void Paste();

  void Redo();

  void Undo();
}
