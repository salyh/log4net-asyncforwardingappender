using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net.Appender;
using System.Threading;
using log4net.Core;

namespace saly.l4n.AsynchronousBFAppender.TestProject
{

    

    public class BlockingConsoleAppender : ConsoleAppender
    {
        public static int count = 0;

        override protected void Append(LoggingEvent loggingEvent)
        {
            Console.WriteLine(DateTime.Now+" BCA-START-BLOCK T:"+Thread.CurrentThread.Name);
            Thread.Sleep(5 * 1000);

            base.Append(loggingEvent);
            Console.WriteLine(DateTime.Now + " BCA-END-BLOCK T:" + Thread.CurrentThread.Name);
            count++;
        }
    }
}
