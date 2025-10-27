using System.Text;
using FluentAurora.Core.Paths;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace FluentAurora.Core.Logging;

public static class Logger
{
    private static readonly NLog.Logger _logger;
    private static readonly LoggingConfiguration _config;
    private static readonly ColoredConsoleTarget _consoleTarget;
    private static readonly FileTarget _fileTarget;

    static Logger()
    {
        _config = new LoggingConfiguration();

        // Console target (colored)
        _consoleTarget = new ColoredConsoleTarget("console")
        {
            Layout = @"[${longdate:format=HH\:mm\:ss.fff}][${level:uppercase=true:format=FirstCharacter}] ${message}"
        };
        _consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule
        {
            Condition = "level == LogLevel.Warn",
            ForegroundColor = ConsoleOutputColor.Yellow
        });
        _consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule
        {
            Condition = "level == LogLevel.Error",
            ForegroundColor = ConsoleOutputColor.Red
        });

        // File target
        _fileTarget = new FileTarget("file")
        {
            FileName = PathResolver.LogFile,
            Layout = @"[${longdate:format=HH\:mm\:ss.fff}][${level:uppercase=true:format=FirstCharacter}] ${message}",
            KeepFileOpen = false,
            Encoding = Encoding.UTF8,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 7
        };

        _config.AddTarget(_consoleTarget);
        _config.AddTarget(_fileTarget);

        // Default Rules: All levels to both
        _config.AddRule(LogLevel.Trace, LogLevel.Fatal, _consoleTarget);
        _config.AddRule(LogLevel.Trace, LogLevel.Fatal, _fileTarget);

        LogManager.Configuration = _config;
        _logger = LogManager.GetCurrentClassLogger();
    }
    
    public static void SetLogLevel(LogLevel level)
    {
        IList<LoggingRule> rules = _config.LoggingRules;

        foreach (LoggingRule rule in rules)
        {
            rule.SetLoggingLevels(level, LogLevel.Fatal);
        }

        LogManager.ReconfigExistingLoggers();
        _logger.Info($"Logging level updated: {level}");
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