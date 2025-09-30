using System;
namespace SuperSocket.Log{
 /// <summary>
    /// Unity实现的ILogger，将日志输出到Unity的Debug系统
    /// </summary>
    public class UnityLogger : SuperSocket.Log.ILogger
    {
        private readonly string _categoryName;

        public UnityLogger(string categoryName = "ELEXNetwork")
        {
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log(string message, LogLevel logLevel, EventId eventId, Exception exception)
        {
            if (!IsEnabled(logLevel))
                return;

            var logMessage = $"[{_categoryName}] {message}";

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Information:
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogInfo(logMessage);
                    #endif
                    break;
                case LogLevel.Warning:
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogInfo(logMessage);
                    #endif
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogInfo(logMessage);
                    #endif
                    break;
                default:
                    #if ENABLE_SUPERSOCKET_LOG
                    NetLogUtil.LogInfo(logMessage);
                    #endif
                    break;
            }

            if (exception != null)
            {
                #if ENABLE_SUPERSOCKET_LOG
                NetLogUtil.LogInfo($"[{_categoryName}] Exception: {exception}");
                #endif
            }
        }
    }
   
}