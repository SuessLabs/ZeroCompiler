// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lite.EventIpc;
using Zero.Ide.Events;

namespace Zero.Ide.Services;

/// <summary>Isolates Lite.EventIPC subscribers with bounded, ordered queues so extensions cannot stall UI or workflow transitions.</summary>
public sealed class IdeEventService : IIdeEventService
{
  /// <summary>The maximum number of pending notifications per subscription before the oldest pending notification is dropped.</summary>
  public const int SubscriptionBufferCapacity = 256;
  private readonly object _gate = new();
  private readonly EventAggregator _aggregator = new();
  private readonly HashSet<ISubscription> _subscriptions = [];
  private bool _disposed;

  /// <summary>Queues a notification through Lite.EventIPC without invoking subscriber code on the publisher's thread.</summary>
  /// <typeparam name="TEvent">The concrete notification type to publish.</typeparam>
  /// <param name="notification">The notification to enqueue; ignored after service disposal.</param>
  public void Publish<TEvent>(TEvent notification) where TEvent : IdeEvent
  {
    ArgumentNullException.ThrowIfNull(notification);
    lock (_gate)
    {
      if (!_disposed)
        _aggregator.Publish(notification);
    }
  }

  /// <summary>Registers a synchronous handler for ordered background delivery with exception isolation.</summary>
  /// <typeparam name="TEvent">The concrete notification type to receive.</typeparam>
  /// <param name="handler">The handler to invoke on the subscription's background worker.</param>
  /// <returns>A disposable token that detaches the subscription.</returns>
  public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IdeEvent
  {
    ArgumentNullException.ThrowIfNull(handler);

    return Subscribe<TEvent>(notification =>
    {
      handler(notification);
      return Task.CompletedTask;
    });
  }

  /// <summary>Registers an asynchronous handler with a bounded queue and one sequential background worker.</summary>
  /// <typeparam name="TEvent">The concrete notification type to receive.</typeparam>
  /// <param name="handler">The handler awaited for each notification before processing the next one.</param>
  /// <returns>A disposable token that detaches the subscription.</returns>
  public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IdeEvent
  {
    ArgumentNullException.ThrowIfNull(handler);
    lock (_gate)
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
      var subscription = new Subscription<TEvent>(handler, Remove);

      _subscriptions.Add(subscription); // Keep Lite.EventIPC's weak handler alive until explicit disposal.
      _aggregator.Subscribe<TEvent>(subscription.Enqueue);

      return subscription;
    }
  }

  /// <summary>Stops accepting notifications and detaches all subscriptions without waiting for active handlers.</summary>
  public void Dispose()
  {
    ISubscription[] subscriptions;
    lock (_gate)
    {
      if (_disposed)
        return;

      _disposed = true;
      subscriptions = new ISubscription[_subscriptions.Count];
      _subscriptions.CopyTo(subscriptions);
    }

    foreach (var subscription in subscriptions)
      subscription.Dispose();
  }

  /// <summary>Unsubscribes a handler from Lite.EventIPC and releases the service's strong reference under the publication lock.</summary>
  /// <param name="subscription">The subscription being disposed.</param>
  private void Remove(ISubscription subscription)
  {
    lock (_gate)
    {
      subscription.Detach(_aggregator);
      _subscriptions.Remove(subscription);
    }
  }

  /// <summary>Provides non-generic lifecycle operations for subscriptions retained by the event service.</summary>
  private interface ISubscription : IDisposable
  {
    /// <summary>Removes the subscription's enqueue handler from the underlying Lite.EventIPC aggregator.</summary>
    /// <param name="aggregator">The aggregator that owns the handler registration.</param>
    void Detach(IEventAggregator aggregator);
  }

  /// <summary>Delivers one event type through a bounded queue while isolating subscriber latency and exceptions.</summary>
  /// <typeparam name="TEvent">The concrete notification type handled by this subscription.</typeparam>
  private sealed class Subscription<TEvent> : ISubscription where TEvent : IdeEvent
  {
    private readonly Channel<TEvent> _queue = Channel.CreateBounded<TEvent>(new BoundedChannelOptions(SubscriptionBufferCapacity)
    {
      SingleReader = true,
      AllowSynchronousContinuations = false,
      FullMode = BoundedChannelFullMode.DropOldest,
    });

    private readonly Func<TEvent, Task> _handler;

    private readonly Action<ISubscription> _remove;

    private int _disposed;

    /// <summary>Initializes a subscription and starts its sequential background delivery worker.</summary>
    /// <param name="handler">The asynchronous handler invoked for each queued notification.</param>
    /// <param name="remove">The callback that detaches the subscription from its owning service.</param>
    public Subscription(Func<TEvent, Task> handler, Action<ISubscription> remove)
    {
      _handler = handler;
      _remove = remove;
      _ = Task.Run(ProcessAsync);
    }

    /// <summary>Queues a notification without blocking, dropping the oldest pending item when the buffer is full.</summary>
    /// <param name="notification">The notification to enqueue if the queue is still open.</param>
    public void Enqueue(TEvent notification) =>
      _queue.Writer.TryWrite(notification);

    /// <summary>Unregisters the enqueue handler from Lite.EventIPC.</summary>
    /// <param name="aggregator">The aggregator that owns this subscription's registration.</param>
    public void Detach(IEventAggregator aggregator) =>
      aggregator.Unsubscribe<TEvent>(Enqueue);

    /// <summary>Detaches the subscription and stops queued delivery without waiting for an active handler to finish.</summary>
    public void Dispose()
    {
      if (Interlocked.Exchange(ref _disposed, 1) != 0)
        return;

      _remove(this);
      _queue.Writer.TryComplete();
    }

    /// <summary>Processes queued notifications in order, tracing handler failures and stopping delivery after disposal.</summary>
    /// <returns>A task that completes when queued processing ends.</returns>
    private async Task ProcessAsync()
    {
      await foreach (var notification in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
      {
        if (Volatile.Read(ref _disposed) != 0)
          break;

        try
        {
          await _handler(notification).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          Trace.TraceError($"IDE event subscriber failed for {typeof(TEvent).Name}: {ex}");
        }
      }
    }
  }
}
