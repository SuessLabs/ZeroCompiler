// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Parsing;

/// <summary>A source line and its location before include expansion.</summary>
public sealed record SourceLine(string Text, string FilePath, int LineNumber);
