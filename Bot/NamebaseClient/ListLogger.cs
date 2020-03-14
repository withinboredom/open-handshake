using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Bot.NamebaseClient
{
    public class ListLogger : ILogger
    {
        public static ObservableCollection<LogLine> Stream { get; } = new ObservableCollection<LogLine>();
        public LogLevel Level { get; set; } = LogLevel.Error;

        public struct LogLine
        {
            public LogLevel Level { get; set; }
            public string Content { get; set; }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if(IsEnabled(logLevel))
            {
                try
                {
                    Stream.Add(new LogLine
                    {
                        Level = logLevel,
                        Content = formatter.Invoke(state, exception)
                    });
                }
                catch
                {
                    // not much to do...
                }
                //File.AppendAllText("log.txt", $"{formatter(state, exception)}\r\n");
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return (int) logLevel >= (int) Level;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}
