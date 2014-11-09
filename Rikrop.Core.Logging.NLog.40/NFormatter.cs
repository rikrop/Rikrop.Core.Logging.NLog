using System;
using System.Collections.Generic;
using System.Text;
using NLog;

namespace Rikrop.Core.Logging.NLog
{
    public class NFormatter
    {
        private readonly int? _maxLogSize;

        public NFormatter()
        {
            
        }

        public NFormatter(int maxLogSize)
        {
            _maxLogSize = maxLogSize;
        }

        public string Format(LogEventInfo log)
        {
            if (log == null)
            {
                return null;
            }

            var stringBuilder = Format(log, log.Message);
            
            return stringBuilder.ToString();
        }

        private void AppendExtendedProperties(StringBuilder sb, IEnumerable<KeyValuePair<object, object>> properties)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var property in properties)
            {
                sb.AppendLine().AppendFormat("{0}: {1}", property.Key, property.Value);
            }
        }

        private string GetStackTrace()
        {
            var stackTrace = Environment.StackTrace;

            return stackTrace;
        }

        private StringBuilder Format(LogEventInfo log, string message)
        {
            var stringBuilder = new StringBuilder()
                .AppendFormat("Timestamp: {0}", DateTime.Now)
                .AppendLine();
            
            int indexOfStartMessage = stringBuilder.Length;

            stringBuilder
                .AppendFormat("Message: {0}", message)
                .AppendLine();
            
            int indexOfEndMessage = stringBuilder.Length - indexOfStartMessage;

            stringBuilder
                .AppendFormat("Severity: {0}", log.Level)
                .AppendLine();

            AppendExtendedProperties(stringBuilder, log.Properties);

            if (log.Level == LogLevel.Error || log.Level == LogLevel.Warn)
            {
                stringBuilder
                    .AppendLine()
                    .AppendLine()
                    .AppendFormat("EnvironmentStackTrace: {0}", GetStackTrace());
            }

            CutMessage(stringBuilder, message, indexOfEndMessage);

            return stringBuilder;
        }

        private void CutMessage(StringBuilder stringBuilder, string sourceMessage, int indexOfEndMessage)
        {
            if (_maxLogSize.HasValue && stringBuilder.Length > _maxLogSize.Value)
            {
                var messageSizeToCut = stringBuilder.Length - _maxLogSize.Value;
                if(sourceMessage.Length > messageSizeToCut)
                {
                    //Если исходное сообщение больше того, что надо вырезать - тогда вырезаем часть сообщения 
                    int messageCutStartIndex = indexOfEndMessage - messageSizeToCut;
                    stringBuilder.Remove(messageCutStartIndex, messageSizeToCut);
                }
                else
                {
                    //Если вырезать кусочек из сообщения не получается, урезаем с конца
                    stringBuilder.Remove(_maxLogSize.Value, messageSizeToCut);
                }
            }
        }
    }
}