namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Consumes bounded owned UTF8 decode characters without manufacturing an immutable private string.</summary>
public delegate T Utf8SpanConsumer<T>(ReadOnlySpan<char> text);
