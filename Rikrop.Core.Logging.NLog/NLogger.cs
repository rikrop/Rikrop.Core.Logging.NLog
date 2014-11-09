using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Rikrop.Core.Exceptions;
using Rikrop.Core.Framework.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Rikrop.Core.Logging.NLog
{
    public class NLogger : ILogger
    {
        static NLogger()
        {
            LogManager.Configuration = new LoggingConfiguration();
        }

        private static readonly string AppDomainName = AppDomain.CurrentDomain.FriendlyName.Split('\\').Last();

        private const int MaxEventLogItemSize = 0x7ffe; //32766

        private readonly NFormatter _formatter;
        private readonly Logger _logWriter;

        private readonly string _name;
        private readonly string _sourcename;

        public string Name
        {
            get { return _name; }
        }

        private NLogger(NFormatter formatter, string sourcename)
        {
            Contract.Requires<ArgumentNullException>(formatter != null);

            _name = Guid.NewGuid().ToString();

            _formatter = formatter;
            _sourcename = sourcename;

            _logWriter = LogManager.GetLogger(_name);
        }

        ~NLogger()
        {
            var configuration = LogManager.Configuration;

            if (configuration == null)
            {
                return;
            }

            var crules = configuration.LoggingRules;

            if (crules != null)
            {
                var rules = crules
                    .Where(_ => _.LoggerNamePattern == _name)
                    .ToArray();

                foreach (var rule in rules)
                {
                    crules.Remove(rule);
                }
            }

            var targets = configuration.AllTargets.Where(_ => _.Name == _name)
                .ToArray();

            foreach (var target in targets)
            {
                configuration.RemoveTarget(target.Name);
            }

            //LogManager.Configuration = configuration;//check it
        }

        public void Log<TRecord>(TRecord record) where TRecord : ILogRecord
        {
            LogAsError(record.Message);
        }

        public static NLogger CreateFileTarget(string filename)
        {
            var logger = new NLogger(new NFormatter(), AppDomainName);

            logger.AddFileTarget(filename);

            return logger;
        }

        public static NLogger CreateFileTarget(FileTarget filename)
        {
            var logger = new NLogger(new NFormatter(), AppDomainName);

            logger.AddFileTarget(filename);

            return logger;
        }

        /// <summary>
        /// ѕо умолчанию будет писать в лог Application из-под источника Application
        /// </summary>
        /// <returns></returns>
        public static NLogger CreateEventLogTarget()
        {
            return CreateEventLogTarget("Application", "Application");
        }

        /// <summary>
        /// “ребует админские права, если лог или источник не существуют.
        /// </summary>
        /// <param name="logName"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static NLogger CreateEventLogTarget(string logName, string source)
        {
            var logger = new NLogger(new NFormatter(MaxEventLogItemSize), source);

            logger.AddEventLogTarget(logName);

            return logger;
        }

        public static NLogger CreateConsoleTarget()
        {
            var logger = new NLogger(new NFormatter(), AppDomainName);

            logger.AddConsoleTarget();

            return logger;
        }

        public void AddFileTarget(string filename)
        {
            var config = LogManager.Configuration;

            var fileTarget = new FileTarget
            {
                FileName = filename,
                Layout = "${message}",
                Header = "====${logger}====",
                Footer = "================="
            };
            config.AddTarget("file", fileTarget);

            var rule = new LoggingRule(_name, LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;
        }

        public void AddFileTarget(FileTarget fileTarget)
        {
            var config = LogManager.Configuration;

            config.AddTarget("file", fileTarget);

            var rule = new LoggingRule(_name, LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;
        }

        public void AddConsoleTarget()
        {
            var config = LogManager.Configuration;

            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            var rule = new LoggingRule(_name, LogLevel.Debug, consoleTarget);
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;
        }

        [Obsolete]
        public void AddEventLogTarget()
        {
            AddEventLogTarget("Application");
        }

        public void AddEventLogTarget(string logname)
        {
            if (!string.IsNullOrWhiteSpace(logname))
            {
                CheckEventLogExists(logname);
            }

            var config = LogManager.Configuration;

            var eventLog = new EventLogTarget
            {
                Source = _sourcename,
                Layout = "${message}",
                Log = logname,
                Name = _name,
            };
            config.AddTarget("EventLog", eventLog);

            var rule = new LoggingRule(_name, LogLevel.Info, eventLog);
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;
        }

        public void LogAsError(Exception exception)
        {
            LogAs(exception, LogLevel.Error);
        }

        public void LogAsError(string message)
        {
            Debug.WriteLine(message);
            WriteLog(message, LogLevel.Error);
        }

        public void LogAsInfo(string message)
        {
            Debug.WriteLine(message);
            WriteLog(message, LogLevel.Info);
        }

        public void LogAsWarning(Exception exception)
        {
            LogAs(exception, LogLevel.Warn);
        }

        public void LogAsWarning(string message)
        {
            Debug.WriteLine(message);
            WriteLog(message, LogLevel.Warn);
        }


        public void LogInnerExceptionsAsError(Exception innerException, int level = 1)
        {
            if (level > 5)
            {
                return;
            }

            if (innerException.InnerException != null)
            {
                LogInnerExceptionsAsError(innerException.InnerException, ++level);
            }

            LogAsError(innerException);
        }

        private void CheckEventLogExists(string logname)
        {
            if (logname.ToLower() == "application")
            {
                return;
            }

            if (EventLog.SourceExists(_sourcename) && !EventLog.Exists(logname) && !String.IsNullOrWhiteSpace(logname))
            {
                EventLog.DeleteEventSource(_sourcename);
                EventLog.CreateEventSource(_sourcename, logname);
            }

            if (!EventLog.SourceExists(_sourcename))
            {
                EventLog.CreateEventSource(_sourcename, logname);
            }
        }

        private void WriteLog(string message,
                              LogLevel level,
                              IDictionary extendedProperties = null)
        {
            var logEntry = new LogEventInfo
            {
                Message = message,
                Level = level,
                LoggerName = _name,
            };

            if (extendedProperties != null)
            {
                var enumerator = extendedProperties.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    logEntry.Properties.Add(enumerator.Entry.Key, enumerator.Entry.Value);
                }
            }

            logEntry.Message = _formatter.Format(logEntry);

            if (_logWriter != null)
            {
                _logWriter.Log(logEntry);
            }
        }

        private void LogAs(Exception exception,
                           LogLevel logLevel)
        {
            exception.Data.Add("ExceptionStackTrace", exception.StackTrace);

            WriteLog(exception.UnwindAll(), logLevel, exception.Data);

            Debug.WriteLine(exception);
        }
    }
}