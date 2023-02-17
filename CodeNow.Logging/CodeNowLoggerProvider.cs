using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeNow.Logging
{
    [Obsolete("Use CodeNowJsonLoggerProvider")]
    public class CodeNowLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly List<ILogger> _loggers = new();

        public CodeNowLoggerProvider(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var accessor = _serviceProvider.GetService<IHttpContextAccessor>();
            var logger = new CodeNowLogger(categoryName, accessor, _configuration);

            _loggers.Add(logger);

            return logger;
        }

        public void Dispose() => _loggers.Clear();
    }
}