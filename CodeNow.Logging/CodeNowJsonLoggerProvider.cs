using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CodeNow.Logging
{
    /// <summary>
    /// Logging provider which logs messages into console as JSON-formatted lines.
    /// This is supported in CodeNow infrastructure. 
    /// </summary>
    public sealed class CodeNowJsonLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly TextWriter _writer;
        private IExternalScopeProvider? _scopeProvider;

        /// <summary>
        /// Creates new instance.
        /// </summary>
        /// <param name="writer">Where to write logs. Logs to console if null.</param>
        public CodeNowJsonLoggerProvider(TextWriter? writer = null)
        {
            _writer = writer ?? Console.Out;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
        
        /// <inheritdoc />
        void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }
        
        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName)
        {
            return new CodeNowJsonLogger(_writer, _scopeProvider, categoryName);
        }

        private sealed class CodeNowJsonLogger : ILogger
        {
            private readonly TextWriter _textWriter;
            private readonly IExternalScopeProvider? _scopeProvider;
            private readonly string _category;

            private class LogEntry<TState>
            {
                public LogEntry(LogLevel logLevel, string category, EventId eventId, TState? state, Exception? exception, Func<TState?, Exception?, string> formatter)
                {
                    Level = logLevel;
                    Category = category;
                    EventId = eventId;
                    State = state;
                    Exception = exception;
                    Formatter = formatter;
                }

                public LogLevel Level { get; }
                public string Category { get; }
                public EventId EventId { get; }
                public TState? State{ get; }
                public Exception? Exception{ get; }
                public Func<TState?, Exception?, string> Formatter{ get; }
            }

            public CodeNowJsonLogger(TextWriter textWriter, IExternalScopeProvider? scopeProvider,
                string category)
            {
                _textWriter = textWriter;
                _scopeProvider = scopeProvider;
                _category = category;
            }
            
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState? state, Exception? exception, Func<TState?, Exception?, string> formatter)
            {
                var logEntry = new LogEntry<TState>(logLevel, _category, eventId, state, exception, formatter);
                Write(logEntry, _scopeProvider, _textWriter);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                // filtering done by logger factory
                return true;
            }

            /// <summary>
            /// An empty scope without any logic
            /// </summary>
            private class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new();

                private NullScope()
                {
                }

                /// <inheritdoc />
                public void Dispose()
                {
                }
            }
            
            public IDisposable BeginScope<TState>(TState state)
            {
                // scope received through IExternalScopeProvider
                return NullScope.Instance;
            }

            private void Write<TState>(
                in LogEntry<TState> logEntry,
                IExternalScopeProvider? scopeProvider,
                TextWriter textWriter)
            {
                string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
                if (logEntry.Exception == null && message == null)
                {
                    return;
                }

                LogLevel logLevel = logEntry.Level;
                string category = logEntry.Category;
                int eventId = logEntry.EventId.Id;
                Exception? exception = logEntry.Exception;
                using (var output = new MemoryStream())
                {
                    var jsonWriterOptions = new JsonWriterOptions
                    {
                        Indented = false
                    };
                    using (var writer = new Utf8JsonWriter(output, jsonWriterOptions))
                    {
                        writer.WriteStartObject();
                        
                        writer.WriteNumber(nameof(logEntry.EventId), eventId);
                        // use 'level' and 'class', because CodeNow uses Java's logging scheme
                        writer.WriteString("level", GetLogLevelString(logLevel));
                        writer.WriteString("class", category);
                        writer.WriteString("Message", message);

                        if (exception != null)
                        {
                            string exceptionMessage = exception.ToString();
                            if (!jsonWriterOptions.Indented)
                            {
                                exceptionMessage = exceptionMessage.Replace(Environment.NewLine, " ");
                            }

                            writer.WriteString(nameof(Exception), exceptionMessage);
                        }

                        WriteScopeAndStateInformation(writer, logEntry.State, scopeProvider);

                        writer.WriteEndObject();
                        writer.Flush();
                    }

                    output.Seek(0, SeekOrigin.Begin);
                    textWriter.Write(Encoding.UTF8.GetString(output.GetBuffer()));
                }

                textWriter.Write(Environment.NewLine);
            }

            private static string GetLogLevelString(LogLevel logLevel)
            {
                return logLevel switch
                {
                    LogLevel.Trace => "Trace",
                    LogLevel.Debug => "Debug",
                    LogLevel.Information => "Information",
                    LogLevel.Warning => "Warning",
                    LogLevel.Error => "Error",
                    LogLevel.Critical => "Critical",
                    _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
                };
            }

            private void WriteScopeAndStateInformation(
                Utf8JsonWriter writer, 
                object? logEntryState,
                IExternalScopeProvider? scopeProvider)
            {
                var state = new
                    { mergedScopeValues = new Dictionary<string, object>(), scopeMessages = new List<string>() };

                scopeProvider?.ForEachScope((scope, loopState) =>
                {
                    loopState.scopeMessages.Add(scope.ToString());
                    if (scope is IReadOnlyCollection<KeyValuePair<string, object>> scopeValues)
                    {
                        foreach (var scopeValue in scopeValues)
                        {
                            loopState.mergedScopeValues[scopeValue.Key] = scopeValue.Value;
                        }
                    }
                }, state);

                if (logEntryState != null)
                {
                    state.scopeMessages.Add(logEntryState.ToString());

                    if (logEntryState is IReadOnlyCollection<KeyValuePair<string, object>> stateProperties)
                    {
                        foreach (KeyValuePair<string, object> item in stateProperties)
                        {
                            state.mergedScopeValues[item.Key] = item.Value;
                        }
                    }
                }

                writer.WriteStartObject("mdc");
                foreach (var scopeValue in state.mergedScopeValues)
                {
                    WriteItem(writer, scopeValue);
                }

                foreach (var (scopeMessage, index) in state.scopeMessages.Select((message, index) =>
                             (x: message, i: index)))
                {
                    WriteItem(writer, new KeyValuePair<string, object>($"Scope_Message_{index}", scopeMessage));
                }

                writer.WriteEndObject();
            }

            private void WriteItem(Utf8JsonWriter writer, KeyValuePair<string, object> item)
            {
                var key = item.Key;
                switch (item.Value)
                {
                    case bool boolValue:
                        writer.WriteBoolean(key, boolValue);
                        break;
                    case byte byteValue:
                        writer.WriteNumber(key, byteValue);
                        break;
                    case sbyte sbyteValue:
                        writer.WriteNumber(key, sbyteValue);
                        break;
                    case char charValue:
#if NETCOREAPP
                    writer.WriteString(key, System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref charValue, 1));
#else
                        writer.WriteString(key, charValue.ToString());
#endif
                        break;
                    case decimal decimalValue:
                        writer.WriteNumber(key, decimalValue);
                        break;
                    case double doubleValue:
                        writer.WriteNumber(key, doubleValue);
                        break;
                    case float floatValue:
                        writer.WriteNumber(key, floatValue);
                        break;
                    case int intValue:
                        writer.WriteNumber(key, intValue);
                        break;
                    case uint uintValue:
                        writer.WriteNumber(key, uintValue);
                        break;
                    case long longValue:
                        writer.WriteNumber(key, longValue);
                        break;
                    case ulong ulongValue:
                        writer.WriteNumber(key, ulongValue);
                        break;
                    case short shortValue:
                        writer.WriteNumber(key, shortValue);
                        break;
                    case ushort ushortValue:
                        writer.WriteNumber(key, ushortValue);
                        break;
                    case null:
                        writer.WriteNull(key);
                        break;
                    default:
                        writer.WriteString(key, ToInvariantString(item.Value));
                        break;
                }
            }
            
            private static string ToInvariantString(object obj) => Convert.ToString(obj, CultureInfo.InvariantCulture) ?? throw new Exception("Error when converting object to string.");
        }
    }
}