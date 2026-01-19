// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Zero.Core;

public static class Helpers
{
  /// <summary>Because I despise System.Linq so much, we're using logic from it.</summary>
  /// <typeparam name="TSource"></typeparam>
  /// <param name="source"></param>
  /// <param name="value"></param>
  /// <param name="comparer"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentNullException"></exception>
  public static bool Contains<TSource>(this IEnumerable<TSource> source, TSource value, IEqualityComparer<TSource>? comparer)
  {
    if (source is null)
      throw new ArgumentNullException();

    if (comparer is null)
    {
      if (typeof(TSource).IsValueType)
      {
        foreach (TSource element in source)
        {
          if (EqualityComparer<TSource>.Default.Equals(element, value))
            return true;
        }

        return false;
      }

      comparer = EqualityComparer<TSource>.Default;
    }

    foreach (TSource element in source)
    {
      if (comparer.Equals(element, value))
        return true;
    }

    return false;
  }
}
