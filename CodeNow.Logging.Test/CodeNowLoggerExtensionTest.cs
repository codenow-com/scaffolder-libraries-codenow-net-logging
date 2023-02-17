using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeNow.Logging
{
    public class CodeNowLoggerExtensionTest
    {
        private class LoggingBuilder : ILoggingBuilder
        {
            public LoggingBuilder(IServiceCollection services)
            {
                Services = services;
            }

            public IServiceCollection Services { get; }
        }

        private class TestHostingEnvironment : IWebHostEnvironment
        {
            public string? EnvironmentName { get; set; }
            public string? ApplicationName { get; set; }
            public string WebRootPath { get; set; } = "";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string? ContentRootPath { get; set; }
            public IFileProvider? ContentRootFileProvider { get; set; }
        }

        private class FakeLoggerProvider : ILoggerProvider
        {
            public void Dispose()
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                throw new System.NotSupportedException("Not needed for test.");
            }
        }
        
        [Fact]
        public void Configuring_CodeNow_logging_replaces_all_providers_with_CodeNowJsonLogger()
        {
            ILoggingBuilder loggingBuilder = new LoggingBuilder(new ServiceCollection());
            loggingBuilder.AddProvider(new FakeLoggerProvider()); // will be removed by configuring CodeNow logging
            
            var webHostBuilderContext = new WebHostBuilderContext
            {
                HostingEnvironment = new TestHostingEnvironment()
                {
                    EnvironmentName = Environments.Production
                }
            };
            CodeNowLoggerExtensions.ConfigureCodeNowLogging(webHostBuilderContext, loggingBuilder);

            var registeredProviders = 
                loggingBuilder.Services.Where(x=>x.ServiceType == typeof(ILoggerProvider)).ToList();

            var provider = Assert.Single(registeredProviders);
            Assert.IsType<CodeNowJsonLoggerProvider>(provider.ImplementationInstance);
        }
        
        [Fact]
        public void Configuring_CodeNow_logging_keeps_providers_in_development()
        {
            ILoggingBuilder loggingBuilder = new LoggingBuilder(new ServiceCollection());
            loggingBuilder.AddProvider(new FakeLoggerProvider());
            
            var webHostBuilderContext = new WebHostBuilderContext
            {
                HostingEnvironment = new TestHostingEnvironment()
                {
                    EnvironmentName = Environments.Development
                }
            };
            CodeNowLoggerExtensions.ConfigureCodeNowLogging(webHostBuilderContext, loggingBuilder);

            var registeredProviders = 
                loggingBuilder.Services.Where(x=>x.ServiceType == typeof(ILoggerProvider)).ToList();

            var provider = Assert.Single(registeredProviders);
            Assert.IsType<FakeLoggerProvider>(provider.ImplementationInstance);
        }
    }
}