using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;


/// <summary>
/// See AvgTimer uses high res timer, similar usage and better than this .. static class but could be used
/// 
/// TODO with NETCORE THIS WHOLE THING MIGHT BE REDUNDANT, USE SYSTEM.DEBUG CLASS
///  SEE GAME CODE W TIMERS, THIS THING MIGHT ONLY SIMPLIFY GETTING TIMES BETWEEN CALLS, 
/// AND NOW THE iDE CAN DO THIS SO MUCH BE SIMPLE OR STANDARD BY NOW
/// </summary>


namespace Core.Trace
{


    /// <summary>
    /// This class is used to log the execution time for the code that executes within its scope. 
    /// Methods also  used for general purpose login of output messages to console, listener, or   if TRACE is defined
    /// </summary>
    //NOTEs.. in the wpf tool, stopwatch can be used.   suppossed to be more accurate?
    //getting to 1/100 millisec.  for timing is best to take average and use units test.
    //TODO more this accessible
    //note per  MONOGAME TIMING issues on their github discussion SEE README ABOUT SYSTEM TIMER RESOLUTION IN WINDOWS AND ITS POWER SAVING, GLOBALY ACCORSS APPS
    //AFFECTS IN ORDER OF  30M SDELAYS, OVER ONE FRAME
    ///NOT SURE ABOUT LINUX/OR OSX OR ANDROID, NON ARE REALTIME OS

    public class TimeExec
         : IDisposable
    {

        //what sucks here is..doing a new on the class is something we 
        //don't want to even do in production..  Anyways it rare to do full production builds..
        // do  for now we will just stub out everthing


#if TRACE
        // Default messages.

        private const string DefaultExecutionContext = "UN-NAMED";
        private const string DefaultLogMessage = "dT [{0}]: [{1}] ms";


        // Configuration keys.
        private const string MessageKey = "LogExec.Message";
        private const string MilestoneMessageKey = "LogExec.MilestoneMessage";

        // Logged instance.
        // private static readonly ILog Logger = LogManager.GetCurrentClassLogger(); //if WPF

        // Member variables.
        public  string ExecutionContext;
        public  string MilestoneLogMessage;
        public  string LogMessage;
        public string Category;
        private readonly bool _infoOnly;
        private readonly TimeSpan _warnAbove;
        private readonly TimeSpan _errorAbove;
        private readonly TimeSpan _fatalAbove;

        private DateTime startTime = new DateTime();

        private TimeSpan _deltaTimeAccum = TimeSpan.Zero;    //todo for resume .. if needed.. subtract this.. 

        private TimeSpan ElapsedSecs = TimeSpan.Zero;  // time since the beginning of the simulation.


        Logger logger = new Logger();


#endif

        /// <summary>
        /// in debug Writelin works, it the default, but for release builds need to use something custom.
        /// There is no Trace in Silverlight
        /// </summary>
        private static Action<int, string, string> outputListener = null;


#if DEBUG
        private static bool once = false;
#endif



        [Conditional("TRACE")]
        public static void SetOutputListener(Action<int, string, string> traceLogger )
        {
            outputListener += traceLogger;
        }


        [Conditional("TRACE")]
        public static void RemoveOutputListener(Action<int, string, string> traceLogger)
        {
            outputListener -= traceLogger;
        }



        /// <summary>
        /// Runtime switch for trace functionality. 
        /// </summary>
        public static bool TraceEnabled { get; set; } = true;


        /// <summary>
        /// Stub, must define TRACE to get the trace code compiled, use the release with TRACE build configuration
        /// </summary>
        public TimeExec()
        {
        }




        /// <summary>
        /// Constructor that accepts an execution context. Example using (
        /// </summary>
        /// <param name="executionContext">The execution context is used to print in the log file.</param>
        /// <param name="startImmediately">Starts the timer immediately (default). If false is passed, then you must call the Start method.</param>
        /// <param name="category">Category so listener can filter these</param>
        public TimeExec(string executionContext, string category="", bool startImmediately = true)
        {
            if (!TraceEnabled)
                return;

#if TRACE
            ExecutionContext = string.IsNullOrEmpty(executionContext) ? DefaultExecutionContext : executionContext;
  
            LogMessage = DefaultLogMessage;
            Category = category;

            _infoOnly = true;
            _warnAbove = TimeSpan.Zero;
            _errorAbove = TimeSpan.Zero;
            _fatalAbove = TimeSpan.Zero;

            if (startImmediately)
            {
                startTime = DateTime.Now;
            }
#endif
        }
 


        //   const TimeSpanwarnAbove = 100 * TimeSpan.TicksPerMillisecs ;

        /// <summary>
        /// Constructor that accepts an execution context.
        /// </summary>
        /// <param name="executionContext">The execution context is used to print in the log file.</param>
        /// <param name="startImmediately">Starts the timer immediately. If false is passed, then you must call the Start method.</param>
        /// <param name="infoOnly">Logs messages only using the Info type</param>
        /// <param name="warnAbove">Logs messages using the Warning type if the elapsed time goes above this threshold</param>
        /// <param name="errorAbove">Logs messages using the Error type if the elapsed time goes above this threshold</param>
        /// <param name="fatalAbove">Logs messages using the Fatal type if the elapsed time goes above this threshold</param>
        public TimeExec(string executionContext, bool startImmediately, TimeSpan warnAbove, TimeSpan errorAbove, TimeSpan fatalAbove, bool infoOnly = true)
        {

#if TRACE
            if (!TraceEnabled)
                return;

            // cannot use       [Conditional("Trace")] on an object constructor
            ExecutionContext = string.IsNullOrEmpty(executionContext) ? DefaultExecutionContext : executionContext;
    
            LogMessage =    DefaultLogMessage;

            _warnAbove = warnAbove;
            _errorAbove = errorAbove;
            _fatalAbove = fatalAbove;

            if (startImmediately)
            {
                // _stopwatch.Start();
                startTime = DateTime.Now;
            }
#endif
        }

        /// <summary>
        /// Returns the current execution time.
        /// </summary>
     //   public long ExecutionTime
     //   {
     //       get
    //        {
    //            return _stopwatch.ToFileTime();
    //        }
      //  }

        /// <summary>
        /// Pauses the LogExec timer used to calculate the execution time.
        /// </summary>
        //  public void Pause()
        //  {
        //    if (_stopwatch.IsRunning)
        //    {
        //     _stopwatch.Stop();
        //    }
        //  }

        /// <summary>
        /// Resumes the paused LogExec timer if it is already paused.
        /// </summary>
        //   public void Resume()
        //   {
        //TOOD   keep a span to subtract if we need this..
        //     if (!_stopwatch.IsRunning)
        //     {
        //       _stopwatch.Start();
        //     }
        ///    }

        /// <summary>
        /// Starts the LogExec timer if it is not already running.
        /// </summary>
        [Conditional("TRACE")]
        public void Start()
        {
            #if TRACE
            startTime = DateTime.Now;
#endif
        }


        /// <summary>
        /// Stops the LogExec timer if it is running.
        /// </summary>


        [Conditional("TRACE")]
        public void Stop()
        {
    #if TRACE
            if (!TraceEnabled)
                return;

            Eval();
            DoLog();
#endif

        }


        ///   <summary>
        ///  Logs the effort in milliseconds since the beginning of the execution timer.Includes execution time for all previous milestones (if any).
        ///  </summary>
        [Conditional("TRACE")]
        public void EvalTimeAndLog()
        {

#if TRACE
            Eval();
            DoLog();
#endif
        }

        /// <summary>
        /// Stops the timer and logs the execution time using the configured logger.
        /// </summary>
        /// 
 
        public void Dispose()
        {
#if TRACE
            if (!TraceEnabled)
                return;

            EvalTimeAndLog();

            GC.SuppressFinalize(this);
#endif
        }

        [Conditional("TRACE")]
        public void Eval()
        {
#if TRACE
            ElapsedSecs = DateTime.Now - startTime;
#endif
        }


        /// <summary>
        /// When Trace is defined ,allows debugging messages to be displayed, in release builds.  For SL in the Debug Window, for the tool,its allow in the trace output pane
        /// </summary>
        public  class Logger
        {

#if TRACE
            public  void Info(string info)
            {
                Trace(4, "Info", info);  //TODO make better use of the level.. the filters in the build setting might filter 
            }

            public  void Info(string info, string category)
            {
                Trace(4, category, info);  //TODO make better use of the level.. the filters in the build setting might filter 
            }

            public  void Warn(string info)
            {
                Trace(3, "Warning", info);
            }

            public void Error(string info)
            {
                Trace(2, "Error", info);
            }


            public void Fatal(string info)
            {
                Trace(0, "Warning", info);
            }

       


#endif
            [Conditional("TRACE")]   
            public static void Trace(int level, string category, string message)
            {
#if TRACE
                if (!TraceEnabled)
                    return;

                //TODO.. add a logger file. and a window on it.. before and after the session.  if This tracer needs to go 
                //in a Non universal module, then ok.  its here so most modules have access.  Another please is core game, the apps, since they
                //need to handle the output

                if (TimeExec.outputListener == null)
                {
                    Debug.WriteLine(message); 
                }  
                else
                {
                
                    TimeExec.outputListener(level, category, message+"\n"); }
 #endif
             }



            public static void WarnOnce(string message, string category = "TODO")
            {
#if TRACE
                TraceOnce(3, category, message);
#endif
            }


            static private HashSet<string> usedWarnings   = new HashSet<string>();

            [Conditional("TRACE")]
            public static void TraceOnce( int level, string  category, string message)
            {
#if TRACE
                if (!TraceEnabled)
                    return;


                if (usedWarnings.Contains(message))
                    return;
                else
                    usedWarnings.Add(message);


                //TODO.. add a logger file. and a window on it.. before and after the session.  if This tracer needs to go 
                //in a Non universal module, then ok.  its here so most modules have access.  Another please is core game, the apps, since they
                //need to handle the output


                if (TimeExec.outputListener == null)
                {
                    Debug.WriteLine(message);
                }
                else
                {

                    TimeExec.outputListener(level, category, message + "\n");
                }
#endif
            }




            [Conditional("TRACE")]
            public static void Trace(string message)   //NOTE probably  not thread safe..   other trace output in system is not
            {
#if TRACE
                if (!TraceEnabled)
                    return;

                if (TimeExec.outputListener == null)
                {

                    Debug.WriteLine(message);

#if DEBUG
                    if ( !once && outputListener == null )
                      {
                              Debug.WriteLine( "Core.Trace.TimeExec.outputListener is not set, needed for tracing in release");
                              once= true;
                      }
#endif
                }
                else
                {
                    TimeExec.outputListener(4, "Info", message);
                }
#endif

            }
        }

#if (! UNIVERSAL)
        public void ObjectDump(object obj)
        {
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
            {
                string name = descriptor.Name;
                object value = descriptor.GetValue(obj);
                Console.WriteLine("{0}={1}", name, value);
            }
        }
#endif


        [Conditional("TRACE")]
        private void DoLog()
        {
 
#if TRACE
            if (!TraceEnabled)
                return;

            if (LogMessage == null)
                LogMessage = "";

            if (_infoOnly || ElapsedSecs <= _warnAbove)
            {

                if (string.IsNullOrEmpty(Category))
                { 
                    logger.Info(string.Format(LogMessage, ExecutionContext, ElapsedSecs.TotalMilliseconds));
                }
                else
                {
                    logger.Info(string.Format(LogMessage, ExecutionContext, ElapsedSecs.TotalMilliseconds), Category);
                }
            }
            else if (ElapsedSecs <= _errorAbove)
            {
                logger.Warn(string.Format(LogMessage, ExecutionContext, ElapsedSecs.TotalMilliseconds));
            }
            else if (ElapsedSecs <= _fatalAbove)
            {
                logger.Error(string.Format(LogMessage, ExecutionContext, ElapsedSecs.TotalMilliseconds));
            }
            else
            {
                logger.Fatal(string.Format(LogMessage, ExecutionContext, ElapsedSecs.TotalMilliseconds));

            }

#endif
        }


    }
}
