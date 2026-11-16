// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;

namespace Zero.Ide.Events;

/// <summary>An immutable notification; subscribers observe work without controlling its outcome.</summary>
public abstract record IdeEvent
{
  /// <summary>Gets the UTC time at which this notification was created.</summary>
  public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
