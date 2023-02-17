# codenow-net-logging
* .NET Core 3.1
* .NET 5 
* C# 9.0


# build package

`dotnet pack -c Release -o Packages`


# changelog
**2.1.0**
- Change 'LogLevel' to 'level' and 'Category' to 'class' as these are what CodeNow logging infrastructure indexes in log messages 

**2.0.0**
- Retarget from .NET Standard 2.1 to .NET Core 3.1 
    - This fixes issues with hosting setup helper, due to backward compatibility issues between the two runtimes
- Create new implementation of `CodeNowJsonLoggerProvider` 
    - Logs into console
    - Messages are formatted into JSON in single line
    - Scope data and messages are logged into `mdc` property
    - TraceId, SpanId and ParentSpanId are expected to be in scope from activity tracing
    - Arrays are flattened due to limitations in Loki
- Add `CodeNowLoggerExtensions.ConfigureCodeNowLogging` to simplify setup of logging in service
    - Outside development, replaces all logger providers with `CodeNowJsonLoggerProvider`
    - In development, keeps logger providers (like console logger with human-readable output)

**1.0.1**
- add extension for ILoggerFactory

**1.0.0**
- add stack trace into the logged message body
- propagate tracing headers into logged message
- add extension to register the logger in application
- add extension to register HttpContextAccessor 