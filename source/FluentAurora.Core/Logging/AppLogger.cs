using System.Runtime.CompilerServices;
using System.Text;
using FluentAurora.Core.Utilities;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace FluentAurora.Core.Logging;

public static class AppLogger
{
    private static readonly NLog.Logger _logger;
    private static readonly LoggingConfiguration _config;

    static AppLogger()
    {
        _config = new LoggingConfiguration();

        ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget("console")
        {
            Layout = @"[${longdate:format=HH\:mm\:ss.fff}][${level:uppercase=true:format=FirstCharacter}]${message}"
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
        consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule
        {
            Condition = "level == LogLevel.Fatal",
            ForegroundColor = ConsoleOutputColor.DarkRed
        });
        _config.AddTarget(consoleTarget);
        _config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);

        FileTarget fileTarget = new FileTarget("file")
        {
            FileName = PathResolver.GetFullPath("Logs", $"Log-${{shortdate}}.log"),
            Layout = @"[${longdate:format=HH\:mm\:ss.fff}][${level:uppercase=true:format=FirstCharacter}]${message}",
            KeepFileOpen = false,
            Encoding = Encoding.UTF8
        };
        _config.AddTarget(fileTarget);
        _config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);

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

    public static void Flush()
    {
        LogManager.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetTypeName<T>()
    {
        Type type = typeof(T);

        string typeName = type.Name;
        int backtickIndex = typeName.IndexOf('`');
        if (backtickIndex > 0)
        {
            typeName = typeName.Substring(0, backtickIndex);
        }

        if (type is not { IsNested: true, DeclaringType: not null })
        {
            return typeName;
        }
        string declaringName = type.DeclaringType.Name;
        int declaringBacktick = declaringName.IndexOf('`');
        if (declaringBacktick > 0)
        {
            declaringName = declaringName.Substring(0, declaringBacktick);
        }
        return $"{declaringName}.{typeName}";
    }

    public static void Trace(string message,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Trace($"[{context}] {message}");
    }

    public static void Debug(string message,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Debug($"[{context}] {message}");
    }

    public static void Info(string message,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Info($"[{context}] {message}");
    }

    public static void Warning(string message,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Warn($"[{context}] {message}");
    }

    public static void Error(string message,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Error($"[{context}] {message}");
    }

    public static void Fatal(string message,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Fatal($"[{context}] {message}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatContext(string? memberName, string? filePath, int lineNumber)
    {
        if (string.IsNullOrEmpty(memberName))
        {
            return "Unknown";
        }

        string fileName = filePath != null
            ? Path.GetFileNameWithoutExtension(filePath)
            : "Unknown";

        return lineNumber > 0
            ? $"{fileName}.{memberName}:{lineNumber}"
            : $"{fileName}.{memberName}";
    }

    public static void LogExceptionDetails(Exception ex, bool includeEnvironmentInfo = true,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        string context = FormatContext(memberName, filePath, lineNumber);
        _logger.Error($"[{context}] ===== Exception Report Start =====");
        _logger.Error($"[{context}] Timestamp (UTC): {DateTime.UtcNow:O}");

        LogExceptionWithDepth(ex, context);

        if (includeEnvironmentInfo)
        {
            _logger.Error($"[{context}] === System Information ===");
            _logger.Error($"[{context}] Machine Name: {Environment.MachineName}");
            _logger.Error($"[{context}] OS Version: {Environment.OSVersion}");
            _logger.Error($"[{context}] .NET Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            _logger.Error($"[{context}] Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            _logger.Error($"[{context}] Current Directory: {Environment.CurrentDirectory}");
        }

        _logger.Error($"[{context}] ===== Exception Report End =====");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Trace(message) instead, which auto-captures caller context.")]
    public static void Trace<T>(string message)
    {
        _logger.Trace($"[{GetTypeName<T>()}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Debug(message) instead, which auto-captures caller context.")]
    public static void Debug<T>(string message)
    {
        _logger.Debug($"[{GetTypeName<T>()}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Info(message) instead, which auto-captures caller context.")]
    public static void Info<T>(string message)
    {
        _logger.Info($"[{GetTypeName<T>()}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Warning(message) instead, which auto-captures caller context.")]
    public static void Warning<T>(string message)
    {
        _logger.Warn($"[{GetTypeName<T>()}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Error(message) instead, which auto-captures caller context.")]
    public static void Error<T>(string message)
    {
        _logger.Error($"[{GetTypeName<T>()}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Fatal(message) instead, which auto-captures caller context.")]
    public static void Fatal<T>(string message)
    {
        _logger.Fatal($"[{GetTypeName<T>()}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Trace(message) instead, which auto-captures caller context.")]
    public static void Trace<T>(string message, [CallerMemberName] string? methodName = null)
    {
        string className = GetTypeName<T>();
        string prefix = string.IsNullOrEmpty(methodName) ? className : $"{className}.{methodName}";
        _logger.Trace($"[{prefix}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Debug(message) instead, which auto-captures caller context.")]
    public static void Debug<T>(string message, [CallerMemberName] string? methodName = null)
    {
        string className = GetTypeName<T>();
        string prefix = string.IsNullOrEmpty(methodName) ? className : $"{className}.{methodName}";
        _logger.Debug($"[{prefix}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Info(message) instead, which auto-captures caller context.")]
    public static void Info<T>(string message, [CallerMemberName] string? methodName = null)
    {
        string className = GetTypeName<T>();
        string prefix = string.IsNullOrEmpty(methodName) ? className : $"{className}.{methodName}";
        _logger.Info($"[{prefix}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Warning(message) instead, which auto-captures caller context.")]
    public static void Warning<T>(string message, [CallerMemberName] string? methodName = null)
    {
        string className = GetTypeName<T>();
        string prefix = string.IsNullOrEmpty(methodName) ? className : $"{className}.{methodName}";
        _logger.Warn($"[{prefix}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Error(message) instead, which auto-captures caller context.")]
    public static void Error<T>(string message, [CallerMemberName] string? methodName = null)
    {
        string className = GetTypeName<T>();
        string prefix = string.IsNullOrEmpty(methodName) ? className : $"{className}.{methodName}";
        _logger.Error($"[{prefix}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.Fatal(message) instead, which auto-captures caller context.")]
    public static void Fatal<T>(string message, [CallerMemberName] string? methodName = null)
    {
        string className = GetTypeName<T>();
        string prefix = string.IsNullOrEmpty(methodName) ? className : $"{className}.{methodName}";
        _logger.Fatal($"[{prefix}] {message}");
    }

    [Obsolete("Does not work from static classes. Use AppLogger.LogExceptionDetails(ex) instead, which auto-captures caller context.")]
    public static void LogExceptionDetails<T>(Exception ex, bool includeEnvironmentInfo = true)
    {
        string className = GetTypeName<T>();
        _logger.Error($"[{className}] ===== Exception Report Start =====");
        _logger.Error($"[{className}] Timestamp (UTC): {DateTime.UtcNow:O}");

        LogExceptionWithDepth(ex, className);

        if (includeEnvironmentInfo)
        {
            _logger.Error($"[{className}] === System Information ===");
            _logger.Error($"[{className}] Machine Name: {Environment.MachineName}");
            _logger.Error($"[{className}] OS Version: {Environment.OSVersion}");
            _logger.Error($"[{className}] .NET Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            _logger.Error($"[{className}] Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            _logger.Error($"[{className}] Current Directory: {Environment.CurrentDirectory}");
        }

        _logger.Error($"[{className}] ===== Exception Report End =====");
    }

    private static void LogExceptionWithDepth(Exception ex, string? className = null, int depth = 0)
    {
        while (true)
        {
            string indent = new string(' ', depth * 2);
            string prefix = className != null ? $"[{className}] " : "";

            _logger.Error($"{prefix}{indent}Exception Level: {depth}");
            _logger.Error($"{prefix}{indent}Type: {ex.GetType().FullName}");
            _logger.Error($"{prefix}{indent}Message: {ex.Message}");
            _logger.Error($"{prefix}{indent}Source: {ex.Source}");
            _logger.Error($"{prefix}{indent}HResult: {ex.HResult}");
            if (ex.HelpLink != null)
            {
                _logger.Error($"{prefix}{indent}Help Link: {ex.HelpLink}");
            }

            if (ex.Data.Count > 0)
            {
                _logger.Error($"{prefix}{indent}Data:");
                foreach (object? key in ex.Data.Keys)
                {
                    _logger.Error($"{prefix}{indent}  {key}: {ex.Data[key]}");
                }
            }

            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                _logger.Error($"{prefix}{indent}StackTrace:");
                foreach (string line in ex.StackTrace.Split(Environment.NewLine))
                {
                    _logger.Error($"{prefix}{indent}  {line}");
                }
            }

            if (ex.TargetSite != null)
            {
                _logger.Error($"{prefix}{indent}TargetSite: {ex.TargetSite}");
            }

            if (ex.InnerException != null)
            {
                _logger.Error($"{prefix}{indent}--- Inner Exception ---");
                ex = ex.InnerException;
                depth = depth + 1;
                continue;
            }
            break;
        }
    }

    public static void Shutdown()
    {
        LogManager.Shutdown();
    }
}