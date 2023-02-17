using System;
using System.IO;
using ApprovalTests;
using ApprovalTests.Reporters;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeNow.Logging
{
    [UseReporter(typeof(RiderReporter))]
    public class CodeNowLoggerProviderTest
    {
        private static string UseLogger(Action<ILogger> useLogger, LoggerFilterOptions? loggerFilterOptions = null)
        {
            string loggedText;
            using MemoryStream stream = new MemoryStream();
            {
                using StreamWriter writeStream = new StreamWriter(stream, leaveOpen: true);

                var provider = new[]
                {
                    new CodeNowJsonLoggerProvider(writeStream)
                };
                LoggerFactory factory = new LoggerFactory(provider, loggerFilterOptions ?? new LoggerFilterOptions());

                var logger = factory.CreateLogger("test-category");

                useLogger(logger);
            }

            stream.Seek(0, SeekOrigin.Begin);

            {
                using StreamReader readStream = new StreamReader(stream, leaveOpen: true);
                loggedText = readStream.ReadToEnd();
            }
            return loggedText;
        }

        [Fact]
        public void Logs_message_as_json()
        {
            var loggedText = UseLogger(logger => logger.LogInformation("test-message"));
            Approvals.Verify(loggedText);
        }

        [Fact]
        public void Logs_various_messages()
        {
            var loggedText = UseLogger(logger =>
            {
                logger.LogError(new Exception("test-exception"), "test-error-message {ErrorData}", "Error data");
                logger.LogDebug(new EventId(987, "event-name"), "test-debug-message {DebugData}", 111);
                logger.LogTrace("test-trace-message {TraceUri} {TraceList}", new Uri("https://test-uri.cz"),
                    new LoggedData("item1", "item2"));
            });
            Approvals.Verify(loggedText);
        }

        [Fact]
        public void Logs_trace_context_into_mdc()
        {
            var loggedText = UseLogger(logger =>
            {
                // emulates usage of scope as done by ASP.NET Core
                using var _ = logger.BeginScope(
                    "TraceId:{TraceId} SpanId:{SpanId} ParentSpanId:{ParentSpanId}",
                    "0af7651916cd43dd8448eb211c80319c",
                    "b7ad6b7169203331",
                    "c5ad6b716920222f");
                logger.LogInformation("test-message");
            });
            Approvals.Verify(loggedText);
        }
        
        private class LoggedData
        {
            public string? ValueA { get; }
            public string? ValueB { get; }

            public LoggedData(string? valueA = null, string? valueB = null)
            {
                ValueA = valueA;
                ValueB = valueB;
            }

            public override string ToString()
            {
                return $"[{ValueA};{ValueB}]";
            }
        };

        [Fact]
        public void Logs_multiple_nested_scopes()
        {
            var loggedText = UseLogger(logger =>
            {
                // emulates usage of scope as done by ASP.NET Core
                using var a = logger.BeginScope("Scope1:{ScopeVal1}", 1);
                using var b = logger.BeginScope("Scope2:{ScopeVal2}", "II");
                using var c = logger.BeginScope("Scope3:{ScopeVal3}", new LoggedData("scope3"));
                logger.LogInformation("test-message");
            });
            Approvals.Verify(loggedText);
        }

        [Fact]
        public void Same_scope_value_is_latest()
        {
            var loggedText = UseLogger(logger =>
            {
                // emulates usage of scope as done by ASP.NET Core
                using var a = logger.BeginScope("Scope1:{ScopeVal}", 1);
                using var b = logger.BeginScope("Scope2:{ScopeVal}", "II");
                using var c = logger.BeginScope("Scope3:{ScopeVal}", new LoggedData("scope3A", "scope3B"));
                logger.LogInformation("test-message");
            });
            Approvals.Verify(loggedText);
        }

        [Fact]
        public void Is_enabled_works()
        {
            var logLevelFilter = new LoggerFilterOptions { MinLevel = LogLevel.Information };
            UseLogger(logger =>
            {
                Assert.False(logger.IsEnabled(LogLevel.Trace));
                Assert.False(logger.IsEnabled(LogLevel.Debug));
                Assert.True(logger.IsEnabled(LogLevel.Information));
                Assert.True(logger.IsEnabled(LogLevel.Warning));
                Assert.True(logger.IsEnabled(LogLevel.Error));
                Assert.True(logger.IsEnabled(LogLevel.Critical));
            }, logLevelFilter);

            var categoryFilter = new LoggerFilterOptions
            {
                Rules =
                {
                    new LoggerFilterRule(null, "test-category", LogLevel.Warning, (_, _, _) => true)
                }
            };
            UseLogger(logger =>
                {
                    Assert.False(logger.IsEnabled(LogLevel.Trace));
                    Assert.False(logger.IsEnabled(LogLevel.Debug));
                    Assert.False(logger.IsEnabled(LogLevel.Information));
                    Assert.True(logger.IsEnabled(LogLevel.Warning));
                    Assert.True(logger.IsEnabled(LogLevel.Error));
                    Assert.True(logger.IsEnabled(LogLevel.Critical));
                },
                categoryFilter);
        }
    }
}