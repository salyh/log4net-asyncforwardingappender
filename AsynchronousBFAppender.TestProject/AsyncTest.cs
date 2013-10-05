using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using log4net.Config;
using log4net;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace saly.l4n.AsynchronousBFAppender.TestProject
{
    [TestClass]
    public class AsyncTest
    {

        static AsyncTest(){
            log4net.Util.LogLog.InternalDebugging = true;
        }


        [TestMethod]
        public void AsyncTest1()
        {
            XmlConfigurator.Configure(new System.IO.FileInfo("log4netconfig_tc_async.xml"));
            ILog log = LogManager.GetLogger(GetType());

            Stopwatch sw = new Stopwatch();
            sw.Start();
            Console.WriteLine(DateTime.Now + " - START-METHOD");

            log.Debug("UNIT Test Debug Log");
            log.Info("UNIT Test Info Log");
            log.Error("UNIT Test Error Log");

            Console.WriteLine(DateTime.Now+ " - END-METHOD");
            sw.Stop();

            Assert.IsTrue(sw.Elapsed.TotalSeconds < 5);
            log4net.LogManager.Shutdown();

            Thread.Sleep(16*1000);

            Assert.IsTrue(BlockingConsoleAppender.count >= 3);

        }


        [TestMethod]
        public void FloddingTest1()
        {
            FileInfo logfile = new FileInfo("unittestlog.txt");
            logfile.Delete();

            FileInfo fi = new System.IO.FileInfo("log4netconfig_tc_flooding.xml");

            Assert.IsTrue(fi.Exists);

            XmlConfigurator.Configure(fi);
            ILog log = LogManager.GetLogger(GetType());

            for (int i = 0; i < 32; i++)
                log.Debug("UNIT Test Debug Log " + i);

            Thread.Sleep(6 * 1000);

            for (int i = 0; i < 200; i++)
            {
                log.Error("UNIT Flod Log " + i);
            }

            Thread.Sleep(6 * 1000);

            for (int i = 0; i < 34; i++)
                log.Error("UNIT Test Error Log " + i);

            Thread.Sleep(6 * 1000);
            log.Debug("UNIT Test Debug Log ");

            Thread.Sleep(10 * 1000);

            Assert.IsTrue(logfile.Exists);
            for (int i = 0; i < 34; i++)
                log.Error("UNIT end fi " + i);

            Thread.Sleep(10 * 1000);

            log4net.LogManager.Shutdown();

            string filecontent = File.ReadAllText(logfile.FullName);
            Assert.IsTrue(filecontent.Contains("[LOGFLODSTART]"));

        }


        [TestMethod]
        public void FloddingTest3()
        {
            FileInfo logfile = new FileInfo("unittestlog.txt");
            logfile.Delete();

            FileInfo fi = new System.IO.FileInfo("log4netconfig_tc_flooding_min.xml");

            Assert.IsTrue(fi.Exists);

            XmlConfigurator.Configure(fi);
            ILog log = LogManager.GetLogger(GetType());

            for (int i = 0; i < 32; i++){

                Thread.Sleep(50);
                log.Debug("UNIT Test Debug 1 " + i);
            }

            for (int i = 0; i < 32; i++)
            {

                Thread.Sleep(50);
                log.Debug("UNIT Test Debug 2 " + i);
            }

            Thread.Sleep(10 * 1000);

            for (int i = 0; i < 9; i++)
            {

                Thread.Sleep(50);
                log.Debug("UNIT Test Debug 3 " + i);
            }

            Assert.IsTrue(logfile.Exists);

            log4net.LogManager.Shutdown();

            string filecontent = File.ReadAllText(logfile.FullName);
            Assert.IsTrue(filecontent.Contains("UNIT Test Debug 1"));
            Assert.IsTrue(filecontent.Contains("UNIT Test Debug 3"));

            Assert.IsTrue(filecontent.Contains("[LOGFLODSTART]"));
            Assert.IsTrue(filecontent.Contains("[LOGFLODSTOP]"));
          
            Assert.IsFalse(filecontent.Contains("UNIT Test Debug 2"));

        }

        [TestMethod]
        public void FloddingTest2()
        {
            FileInfo logfile = new FileInfo("unittestlog.txt");
            logfile.Delete();

            FileInfo fi = new System.IO.FileInfo("log4netconfig_tc_flooding.xml");

            Assert.IsTrue(fi.Exists);

            XmlConfigurator.Configure(fi);
            ILog log = LogManager.GetLogger(GetType());

            for (int i = 0; i < 32; i++)
                log.Debug("UNIT Test Debug Log NonFlood " + i);

            Thread.Sleep(6 * 1000);

            for (int i = 0; i < 200; i++)
            {
                log.Error("UNIT Flod Log " + i);
            }

            Thread.Sleep(5*1000);

            for (int i = 0; i < 200; i++)
            {
                log.Error("UNIT Flod Log2 " + i);
            }

            Thread.Sleep(2 * 1000);

            for (int i = 0; i < 100; i++)
            {
                log.Error("must not be logged because of flooding stop" + i);
            }

            Thread.Sleep(10 * 1000);


            for (int i = 0; i < 80; i++)
            {
                log.Debug("blah");
            }

            Thread.Sleep(10 * 1000);


            for (int i = 0; i < 80; i++)
            {
                log.Debug("must be logged because of flooding stop");
            }
        
            Thread.Sleep(2*1000);

            Assert.IsTrue(logfile.Exists);

            log4net.LogManager.Shutdown();

            string filecontent = File.ReadAllText(logfile.FullName);
            Assert.IsTrue(filecontent.Contains("UNIT Test Debug Log NonFlood"));
            Assert.IsTrue(filecontent.Contains("UNIT Flod Log"));
           
            Assert.IsTrue(filecontent.Contains("[LOGFLODSTART]"));
            Assert.IsTrue(filecontent.Contains("[LOGFLODSTOP]"));
            Assert.IsTrue(filecontent.Contains("must be logged because of flooding stop"));          
            Assert.IsFalse(filecontent.Contains("must not be logged because of flooding stop"));

        }
    }
}
