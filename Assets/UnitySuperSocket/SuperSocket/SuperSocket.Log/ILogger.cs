// Decompiled with JetBrains decompiler
// Type: Microsoft.Extensions.Logging.ILogger
// Assembly: Microsoft.Extensions.Logging.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60
// MVID: 919927C8-44AD-43AA-A822-ADB49B409D8B
// Assembly location: F:\workspace\main\Unicorn\u3dclient\Assets\Packages\Microsoft.Extensions.Logging.Abstractions.9.0.8\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll
// XML documentation location: F:\workspace\main\Unicorn\u3dclient\Assets\Packages\Microsoft.Extensions.Logging.Abstractions.9.0.8\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.xml

using System;

#nullable enable
namespace SuperSocket.Log
{
  /// <summary>Represents a type used to perform logging.</summary>
  /// <remarks>Aggregates most logging patterns to a single method.</remarks>
  public interface ILogger
  {
    /// <summary>Writes a log entry.</summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">Id of the event.</param>
    /// <param name="state">The entry to be written. Can be also an object.</param>
    /// <param name="exception">The exception related to this entry.</param>
    /// <param name="formatter">Function to create a <see cref="T:System.String" /> message of the <paramref name="state" /> and <paramref name="exception" />.</param>
    /// <typeparam name="TState">The type of the object to be written.</typeparam>
    void Log(
      string message,
      LogLevel logLevel,
      EventId eventId,
      Exception? exception
     );

    /// <summary>
    /// Checks if the given <paramref name="logLevel" /> is enabled.
    /// </summary>
    /// <param name="logLevel">Level to be checked.</param>
    /// <returns><see langword="true" /> if enabled.</returns>
    bool IsEnabled(LogLevel logLevel);

    /// <summary>Begins a logical operation scope.</summary>
    /// <param name="state">The identifier for the scope.</param>
    /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
    /// <returns>An <see cref="T:System.IDisposable" /> that ends the logical operation scope on dispose.</returns>
    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
  }
}
