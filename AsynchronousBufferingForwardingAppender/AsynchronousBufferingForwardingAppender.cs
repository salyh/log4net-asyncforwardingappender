#region MIT License

//The MIT License (MIT)
//Copyright (c) 2013 Hendrik Saly
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
//to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
//and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
//WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using log4net.Appender;
using log4net.Core;
using log4net.Util;

namespace saly.l4n
{
    /*
      
    Generic log4net asynchronous forwarding appender (optionally with floddingprevention/throttling)
    
    This generic forwarding appender has the following features:

    1) Async logging in a manner that the ILog.Debug()/../Fatal() call will not
    block (for example to cope with the case that the underlying ADO Appender does currently slow
    inserts into database because of database locks)
    
    2) Flooding detection/prevention: If a log4net enabled application
    gets wild and issue, for example, 10000 logging events per minute you maybe
    want to suppress further messages. In this case a message is issued that any further log messages
    will be discarded, and if the flooding is over the appender
    automatically resume to normal logging behavior.  Sometimes this feature is
    also called message throttling.
    
    3) All the capabilities of the BufferingForwardingAppender

    The appender is always async, flooding prevention can be configured
    with two parameters
      <maxEventsPeriod value="100"/> <!-- set to 0 to disable flood protection -->
      <periodInSeconds value="5"/>

    Note regarding async behavior: If one of the attached appenders will
    block for a while this cause that logmessage delivery to the other (successive)
    appenders will also be delayed - but although its async from the
    perspective of the log4net enabled application which is using this
    appender.
    
    Dependencies: .Net 4, log4net Full Profile >= 1.2.11 
    Versioning: a.b.c.d (a,b,c refers to the log4net version compatible/compiled with (for example 1.2.12); d is the version of this appender)
      
    Confuguration:
    <appender name="AsynchronousBufferingForwardingAppender" type="saly.l4n.AsynchronousBufferingForwardingAppender,saly.l4n.AsynchronousBufferingForwardingAppender">
      <bufferSize value="..." />
      <lossy value="..." />
      ...
      <maxEventsPeriod value="100"/>
      <periodInSeconds value="5"/>
      ... 
  
      <appender-ref ref="FileAppender" />
      <appender-ref ref="..." />
      <appender-ref ref="..." />
    </appender>
      
      
    Author: Hendrik Saly <hendrik.saly(at)gmx.de>  
    WWW: https://github.com/salyh/log4net-asyncforwardingappender 
     
    
    */


    public class AsynchronousBufferingForwardingAppender : BufferingForwardingAppender
    {
        private readonly List<Task> _activeTasks = new List<Task>();
        private readonly Object _lock = new Object();
        private volatile bool _closeRequested;
        private int _countCurrentPeriod;
        private int _countLastPeriod;
        private bool _floodMessageSend;
        private bool _isFlodding;
        private Stopwatch _watch;

        //flodding detection is disabled by default
        private int c_MaxEventsPeriod; //0 means disabled
        private int c_PeriodInSeconds = 5;

        //TODO
        //throw exception if parameters are invalid

        public int MaxEventsPeriod
        {
            get { return c_MaxEventsPeriod; }

            set { c_MaxEventsPeriod = value; }
        }

        public int PeriodInSeconds
        {
            get { return c_PeriodInSeconds; }

            set { c_PeriodInSeconds = value; }
        }

        public override void ActivateOptions()
        {
            if (c_MaxEventsPeriod > 0)
            {
                _countLastPeriod = 0;
                _countCurrentPeriod = 0;
                _isFlodding = false;
                _watch = new Stopwatch();
                _watch.Start();
            }

            base.ActivateOptions();
        }


        protected override void SendBuffer(LoggingEvent[] events)
        {
            if (events == null || events.Length == 0)
            {
                return;
            }


            if (_closeRequested)
            {
#if DEBUG
                LogLog.Warn(GetType(), DateTime.Now + ": " + "Close was requested, will not accept new messages");
                
#endif

                return;
            }

            Task task = Task.Factory.StartNew(() => _SendBuffer(events));
            _activeTasks.Add(task);
            task.ContinueWith(t => { _activeTasks.Remove(t); });
        }

        protected override void OnClose()
        {
            _closeRequested = true;

            Task[] tasks = _activeTasks.ToArray();

#if DEBUG
            LogLog.Debug(GetType(), DateTime.Now + ": " + "OnClose(), wait for " + tasks.Length + " tasks to complete");
#endif

            Task.WaitAll(tasks);

#if DEBUG
            LogLog.Debug(GetType(), DateTime.Now + ": " + "OnClose() completes");
#endif

            base.OnClose();
        }


        protected void _SendBuffer(LoggingEvent[] events)
        {
            //queue here
            lock (_lock)
            {
                #region flooding control

                if (c_MaxEventsPeriod > 0)
                {
                    //flodding detection enabled
#if DEBUG
                    LogLog.Debug(GetType(), DateTime.Now + ": " + "SendBuffer with length " + events.Length);
#endif

                    double elapsed = _watch.Elapsed.TotalSeconds;

                    if (elapsed > 0)
                    {
                        if (elapsed >= c_PeriodInSeconds)
                        {
                            //period end, check flodding


                            if (_countCurrentPeriod > c_MaxEventsPeriod)
                            {
                                //flodding
                                _isFlodding = true;
#if DEBUG
                                LogLog.Debug(GetType(), DateTime.Now + ": " + "Flodding start");
#endif
                            }
                            else
                            {
                                //not flodding

                                if (_isFlodding)
                                {
                                    //reset flooding

                                    const string floddingMessage = "[LOGFLODSTOP] Flooding stop detected, resume to normal logging operation";


                                    LogLog.Warn(GetType(), floddingMessage);

                                    var floddingErrorLoggingEvent = new LoggingEvent(GetType(), events[0].Repository, "AsynchronousBufferingForwardingAppender.FloodingDetection", Level.Warn, floddingMessage, null); //null = Message, Exception            

                                    base.SendBuffer(new[]
                                                    {
                                                        floddingErrorLoggingEvent
                                                    });
                                }

#if DEBUG
                                LogLog.Debug(GetType(), DateTime.Now + ": " + "Flodding end");
#endif

                                _isFlodding = false;
                                _floodMessageSend = false;
                            }

#if DEBUG
                            LogLog.Debug(GetType(), DateTime.Now + ": " + "Period end");
#endif
                            _countLastPeriod = _countCurrentPeriod;
                            _countCurrentPeriod = 0;
                            _watch.Restart();
                        }
                        else
                        {
                            //period not ended, just count
                            _countCurrentPeriod += events.Length;

#if DEBUG
                            LogLog.Debug(GetType(), DateTime.Now + " Period not ended ("+elapsed+" sec.), _countCurrentPeriod: " + _countCurrentPeriod);
#endif
                        }


                        if (_isFlodding)
                        {
                            //emit warning message
                            if (!_floodMessageSend)
                            {
                                string floddingMessage = "[LOGFLODSTART] Log flodding detected! Messages will be discarded! Received " + _countLastPeriod + " messages within " + c_PeriodInSeconds + " secs.";


                                LogLog.Error(GetType(), floddingMessage);

                                var floddingErrorLoggingEvent = new LoggingEvent(GetType(), events[0].Repository, "AsynchronousBufferingForwardingAppender.FloodingDetection", Level.Fatal, floddingMessage, null); //null = Message, Exception            

                                base.SendBuffer(new[]
                                                {
                                                    floddingErrorLoggingEvent
                                                });

                                _floodMessageSend = true;
                            }

#if DEBUG
                            LogLog.Debug(GetType(), DateTime.Now + ": "+events.Length + " msg supressed because of flooding");
#endif

                            return;
                        } //endif isflodding
                    } //endif elapsed >0
                }

                #endregion

                base.SendBuffer(events);
            }
        }
    }
}