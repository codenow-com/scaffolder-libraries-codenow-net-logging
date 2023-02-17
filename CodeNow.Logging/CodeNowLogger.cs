using System;
using System.Collections.Generic;
using System.Linq;
using CodeNow.Logging.Structs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CodeNow.Logging
{
        public class CodeNowLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly IHttpContextAccessor _httpContextAccessor;
            private readonly IConfiguration _configuration;

            public CodeNowLogger(string categoryName, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
            {
                _categoryName = categoryName;
                _httpContextAccessor = httpContextAccessor;
                _configuration = configuration;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId,
                TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }
                // message
                var str = formatter(state, exception);
                // append stack trace if there is exception
                if (exception != null)
                    str = $"{str}\n {exception.Source}\n {exception.Message}\n  {exception.StackTrace}";  

                // get tracing ids 
                var traceId = string.Empty;
                var spanId = string.Empty;
                var parentSpanId = string.Empty;
                
                if(_httpContextAccessor != null && _httpContextAccessor.HttpContext != null)
                    traceId = _httpContextAccessor.HttpContext.Request.Headers["x-b3-traceid"].ToString();
                if(_httpContextAccessor != null && _httpContextAccessor.HttpContext != null)
                    spanId = _httpContextAccessor.HttpContext.Request.Headers["x-b3-spanid"].ToString();
                if(_httpContextAccessor != null && _httpContextAccessor.HttpContext != null)
                    parentSpanId = _httpContextAccessor.HttpContext.Request.Headers["x-b3-parentspanid"].ToString();

                MdcStruct mdc = null;
                
                if(!string.IsNullOrEmpty(traceId))
                {
                    mdc = new MdcStruct {SpanId = spanId, TraceId = traceId, ParentSpanId = parentSpanId};
                }
                
                // create and format log message into the json
                var serialized = JsonConvert.SerializeObject(new LogStruct
                {
                    TimeStamp = DateTime.Now.ToString("o"),
                    Level = $"{logLevel}".ToLowerInvariant(),
                    Logger = $"{_categoryName}[{eventId.Id}]",
                    Mdc = mdc,
                    Message = $"{str}"
                },
                    new JsonSerializerSettings()
                {
                    Formatting = (Newtonsoft.Json.Formatting) Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                
                Console.WriteLine(serialized);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                try
                {
                    // get logLevel for category from settings and default LogLevel
                    var configuredLogLevels = _configuration.GetSection("Logging:LogLevel").GetChildren();
                    var defaultLogLevel = configuredLogLevels.FirstOrDefault(ll => ll.Key == "Default")?.Value;
                    var categoryLogLevel = configuredLogLevels.FirstOrDefault(ll => ll.Key == _categoryName)?.Value;


                    // if logLevel for category isn't configured we use default LogLevel
                    var configuredLogLevel =
                        !string.IsNullOrWhiteSpace(categoryLogLevel) ? categoryLogLevel : defaultLogLevel;

                    // parse enum value from configured string
                    var configuredLogLevelEnumValue = Enum.Parse<LogLevel>(configuredLogLevel, true);

                    // disable logging output if configured LogLevel == None 
                    if (configuredLogLevelEnumValue == LogLevel.None) return false;

                    // enable logging output if current LogLevel equals or is higher tha configured LogLevel
                    return (int) logLevel >= (int) configuredLogLevelEnumValue;
                }
                catch
                {
                    return true;
                }
            }

            public IDisposable BeginScope<TState>(TState state) => default;
        }
}