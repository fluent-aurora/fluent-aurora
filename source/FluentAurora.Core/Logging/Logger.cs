using System.Text;
using FluentAurora.Core.Paths;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace FluentAurora.Core.Logging;

public static class Logger
{
    private static readonly NLog.Logger _logger;

    static Logger()
    {
        LoggingConfiguration config = new LoggingConfiguration();
        // Console target (colored)
        ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget("console")
        {
            Layout = @"[${longdate:format=HH\:mm\:ss.fff}][${level:uppercase=true:format=FirstCharacter}] ${message}"
        };
        consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule
        {
            Condition = "level == LogLevel.Warn",
            ForegroundColor = ConsoleOutputColor.Yellow
        });
        consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule
        {
            Condition = "level == LogLevel.Error",
            ForegroundColor = ConsoleOutputColor.Red
        });

        config.AddTarget(consoleTarget);
        config.AddRuleForAllLevels(consoleTarget);

        // File target
        FileTarget fileTarget = new FileTarget("file")
        {
            FileName = PathResolver.LogFile,
            Layout = @"[${longdate:format=HH\:mm\:ss.fff}][${level:uppercase=true:format=FirstCharacter}] ${message}",
            KeepFileOpen = false,
            Encoding = Encoding.UTF8,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 7
        };

        config.AddTarget(fileTarget);
        config.AddRuleForAllLevels(fileTarget);
        LogManager.Configuration = config;

        _logger = LogManager.GetCurrentClassLogger();
    }

    public static void Trace(string message) => _logger.Trace(message);
    public static void Debug(string message) => _logger.Debug(message);
    public static void Info(string message) => _logger.Info(message);
    public static void Warning(string message) => _logger.Warn(message);
    public static void Error(string message) => _logger.Error(message);

    public static void Shutdown()
    {
        LogManager.Shutdown();
    }
}