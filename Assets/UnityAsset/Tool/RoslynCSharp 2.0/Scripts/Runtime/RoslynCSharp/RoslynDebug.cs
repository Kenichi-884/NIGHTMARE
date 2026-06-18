using System;

namespace RoslynCSharp
{
    /// <summary>
    /// The log level used by the runtime.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Nothing should be logged to the Unity console.
        /// Not recommended for most users as it makes it easy to miss critical messages if a problem occurs.
        /// </summary>
        None = 0,
        /// <summary>
        /// Only errors should be logged to the Unity console.
        /// </summary>
        Errors = 1,
        /// <summary>
        /// Only Warnings and Errors should be logged to the Unity console.
        /// This is the default option.
        /// </summary>
        Warnings = 2,
        /// <summary>
        /// Messages, Warnings and Errors should be logged to the Unity console.
        /// </summary>
        Messages = 3,
        /// <summary>
        /// Same as Messages.
        /// Messages, Warnings and Errors should be logged to the Unity console.
        /// </summary>
        All = Messages,
    }

    /// <summary>
    /// Overrides Unity Debug utility and implements log filtering to allow the user to suppress logs that are not relevant.
    /// </summary>
    internal static class Debug
    {
        // Type
        private interface IDebugProvider
        {
            void Log(string message);
            void LogWarning(string message);
            void LogError(string message);
            void LogException(Exception e);
        }

        private sealed class UnityEngineDebugProvider : IDebugProvider
        {
            public void Log(string message) => UnityEngine.Debug.Log(message);
            public void LogWarning(string message) => UnityEngine.Debug.LogWarning(message);
            public void LogError(string message) => UnityEngine.Debug.LogError(message);
            public void LogException(Exception e) => UnityEngine.Debug.LogException(e);
        }

        private sealed class DiagnosticsDebugProvider : IDebugProvider
        {
            public void Log(string message) => System.Diagnostics.Debug.WriteLine(message);
            public void LogWarning(string message) => System.Diagnostics.Debug.WriteLine("[WARNING]: " + message);
            public void LogError(string message) => System.Diagnostics.Debug.WriteLine("[ERROR]: " + message);
            public void LogException(Exception e) => System.Diagnostics.Debug.WriteLine("[EXCEPTION]: " + e);
        }

        // Event
        /// <summary>
        /// Called when a log message is received, even if it is hidden by the current log level.
        /// </summary>
        public static event Action OnLog;

        // Private
        private static IDebugProvider provider = new UnityEngineDebugProvider();

        // Properties
        /// <summary>
        /// The current log level used to filter log messages.
        /// </summary>
        public static LogLevel LogLevel { get; set; } = LogLevel.Warnings;

        // Methods
        /// <summary>
        /// Causes all logged messages to be reported in the Unity console window and log.
        /// </summary>
        public static void UseUnityLogger()
        {
            provider = new UnityEngineDebugProvider();
        }

        /// <summary>
        /// Causes all logged messages to be reported using <see cref="System.Diagnostics.Debug"/>, useful for testing purposes.
        /// </summary>
        public static void UseDiagnosticsLogger()
        {
            provider = new DiagnosticsDebugProvider();
        }

        /// <summary>
        /// Log a message.
        /// </summary>
        /// <param name="msg">The message to log</param>
        public static void Log(string msg)
        {
            // Trigger log
            OnLog?.Invoke();

            if (LogLevel >= LogLevel.Messages)
                provider?.Log(msg);
        }

        /// <summary>
        /// Log a message with string formatting.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format args</param>
        public static void LogFormat(string format, params object[] args)
        {
            // Trigger log
            OnLog?.Invoke();

            if (LogLevel >= LogLevel.Messages)
                provider?.Log(string.Format(format, args));
        }

        /// <summary>
        /// Log a warning.
        /// </summary>
        /// <param name="msg">The message to log</param>
        public static void LogWarning(string msg)
        {
            // Trigger log
            OnLog?.Invoke();

            if (LogLevel >= LogLevel.Warnings)
                provider?.LogWarning(msg);
        }

        /// <summary>
        /// Log a warning with string formatting.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format args</param>
        public static void LogWarningFormat(string format, params object[] args)
        {
            // Trigger log
            OnLog?.Invoke();

            if (LogLevel >= LogLevel.Warnings)
                provider?.LogWarning(string.Format(format, args));
        }

        /// <summary>
        /// Log an error.
        /// </summary>
        /// <param name="msg">The message to log</param>
        public static void LogError(string msg)
        {
            // Trigger log
            OnLog?.Invoke();

            if (LogLevel >= LogLevel.Errors)
                provider?.LogError(msg);
        }

        /// <summary>
        /// Log an error with string formatting.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format args</param>
        public static void LogErrorFormat(string format, params object[] args)
        {
            // Trigger log
            OnLog?.Invoke();

            if (LogLevel >= LogLevel.Errors)
                provider?.LogError(string.Format(format, args));
        }

        /// <summary>
        /// Log an exception.
        /// </summary>
        /// <param name="e">The exception to log</param>
        public static void LogException(Exception e)
        {
            // Trigger log
            OnLog?.Invoke();

            if (e != null && LogLevel >= LogLevel.Errors)
                provider?.LogException(e);
        }
    }
}
