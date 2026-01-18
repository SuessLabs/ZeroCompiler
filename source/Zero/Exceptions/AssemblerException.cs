// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;

namespace Zero.Exceptions;

public class AssemblerException(string message)
  : Exception(message);
