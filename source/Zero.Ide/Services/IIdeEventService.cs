// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Zero.Ide.Events;

namespace Zero.Ide.Services;

/// <summary>Publishes local Lite.EventIPC notifications without executing subscriber code on the publisher's thread.</summary>
public interface IIdeEventService : IDisposable
{
  /// <summary>Queues a notification for each subscriber without waiting for handlers; notifications after disposal are ignored.</summary>
  /// <typeparam name="TEvent">The concrete notification type to publish.</typeparam>
  /// <param name="notification">The notification to deliver to matching subscriptions.</param>
  void Publish<TEvent>(TEvent notification) where TEvent : IdeEvent;

  /// <summary>Subscribes a background handler; dispose the returned token to detach it. Marshal UI updates onto Avalonia's dispatcher.</summary>
  /// <typeparam name="TEvent">The concrete notification type to receive.</typeparam>
  /// <param name="handler">The handler invoked sequentially for this subscription.</param>
  /// <returns>A token that detaches the handler and stops queued delivery when disposed.</returns>
  IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IdeEvent;

  /// <summary>Subscribes an awaited background handler with per-subscription ordering and exception isolation.</summary>
  /// <typeparam name="TEvent">The concrete notification type to receive.</typeparam>
  /// <param name="handler">The asynchronous handler awaited before processing the next notification.</param>
  /// <returns>A token that detaches the handler and stops queued delivery when disposed.</returns>
  IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IdeEvent;
}
