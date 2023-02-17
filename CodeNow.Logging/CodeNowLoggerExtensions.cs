using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeNow.Logging
{
    public static class CodeNowLoggerExtensions
    {
        public static IServiceCollection AddCodeNowHttpContextAccessor(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            return services;
        }
        
        [Obsolete("Use ConfigureCodeNowLogging or AddCodeNowJson")]
        public static ILoggerFactory AddCodeNowLoggerProvider(this ILoggerFactory loggerFactory, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            
            loggerFactory.AddProvider(new CodeNowLoggerProvider(serviceProvider, configuration));
            return loggerFactory;
        }

        /// <summary>
        /// Adds provider for logging into console as JSON-formatted lines, as supported by CodeNow infrastructure.
        /// </summary>
        public static ILoggerFactory AddCodeNowJson(this ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            
            loggerFactory.AddProvider(new CodeNowJsonLoggerProvider());
            
            return loggerFactory;
        }
        
        /// <summary>
        /// Adds provider for logging into console as JSON-formatted lines, as supported by CodeNow infrastructure.
        /// </summary>
        public static ILoggingBuilder AddCodeNowJson(this ILoggingBuilder loggingBuilder)
        {
            if (loggingBuilder == null)
            {
                throw new ArgumentNullException(nameof(loggingBuilder));
            }
            
            loggingBuilder.AddProvider(new CodeNowJsonLoggerProvider());
            
            return loggingBuilder;
        }
        
        /// <summary>
        /// Sets up logging to be compatible with CodeNow infrastructure.
        /// </summary>
        /// <remarks>
        /// If running in development, use default ASP.NET logging.
        /// If running outside development, uses console logger with JSON-formatted messages.
        /// </remarks>
        /// <example>
        /// Use when configuring web host.
        /// <code>
        /// Host.CreateDefaultBuilder(args)
        ///     .ConfigureWebHostDefaults(webBuilder =>
        ///      {
        ///          webBuilder
        ///              .UseStartup&lt;Startup&gt;()
        ///              .ConfigureLogging(CodeNowLoggerExtensions.ConfigureLogging); // use here
        ///      });
        /// </code>
        /// </example>
        public static void ConfigureCodeNowLogging(WebHostBuilderContext webHostBuilderContext, ILoggingBuilder loggingBuilder)
        {
            if (!webHostBuilderContext.HostingEnvironment.IsDevelopment())
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddCodeNowJson();
            }
        }
    }
}