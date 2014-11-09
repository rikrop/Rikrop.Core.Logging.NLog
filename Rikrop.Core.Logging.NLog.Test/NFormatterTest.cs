using System.Globalization;
using System.Linq;
using NLog;
using NUnit.Framework;

namespace Rikrop.Core.Logging.NLog.Test
{
    [TestFixture]
    public class NFormatterTest
    {
        [Test]
        [TestCase(300, 100)]
        [TestCase(1, 5)]
        public void ShouldCutMessageTextIfMaxLogSizeIsSet(int messageSize, int maxSize)
        {
            var logEventInfo = new LogEventInfo(LogLevel.Info, "test", "test")
                                   {
                                       Message = string.Join("", Enumerable.Range(0, messageSize).Select(o => "1"))
                                   };
            var formatter = new NFormatter(maxSize);
            var message = formatter.Format(logEventInfo);
            
            Assert.AreEqual(maxSize, message.Length);
        }
    }
}