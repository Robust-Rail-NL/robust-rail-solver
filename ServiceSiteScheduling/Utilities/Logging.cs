#nullable enable

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace ServiceSiteScheduling.Utilities
{
    public static class Logging
    {
        private static readonly ILoggerFactory factory = LoggerFactory.Create(builder =>
            builder.AddFilter(null, LogLevel.Warning).AddConsole()
        );

        public static ILogger GetLogger([CallerFilePath] string filePath = "")
        {
            return factory.CreateLogger(Path.GetFileName(filePath));
        }
    }
}
