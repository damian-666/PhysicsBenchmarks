using System;
using System.Diagnostics;


namespace Timers
{


    /// <summary>
    ///  this class has static itesms for timing only one can be used at a time but 
   /// usage:  using new ( StopWatchTimer("100 iterations fucnctionX")){ funcgtionx;};
    /// </summary>

    public class StopWatchTimer : IDisposable
    {
        static long startTime = 0;

        static Stopwatch stopwatch = new Stopwatch();
  
        /// <summary>
        /// optional , name this measurement, output trace will contain name
        /// </summary>

        public string Name = null;

        /// <summary>
        /// optional categorize measurements to avoid to trace output pollution 
        /// </summary>
        public string Category = null;
 
        public StopWatchTimer(string name = "", string category = "")
        {

            Name = name;
            Category = category;
            //NOTE in windows timer resolution is bad by default, might affect measurement on things
            // like thread sleep, wait, etc, use  timeBeginPeriod(1) to get 1 ms rec otherwise expect  timer errors up to 16 ms errors
            // [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
            // public static extern uint timeBeginPeriod(uint uMilliseconds);
            startTime = stopwatch.ElapsedTicks;

        }


        static StopWatchTimer()
        {
            //we use one watch for all.. takes a start time to get elapsed when each timer is created
            stopwatch.Start();

            //NOTE in windows timer resolution is bad by default, might affect measurement on thigsn
            // liek thread sleep, wait, etc, use  timeBeginPeriod(1) to get 1 ms rec otherwise expect  timer errors up to 16 ms errors
            // [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
            // public static extern uint timeBeginPeriod(uint uMilliseconds);
            stopwatch.Start();
            startTime = stopwatch.ElapsedTicks;
        
        }


        public StopWatchTimer()
        {
            startTime = stopwatch.ElapsedTicks;
        }

        void Start()
        {
            startTime = stopwatch.ElapsedTicks;

        }

        static public int LastResultTicks = 0;

        public void Dispose()
        {
    
             LastResultTicks = (int)(stopwatch.ElapsedTicks - startTime);


            Trace.WriteLine(Name + " dt:" +LastResultTicks  + "ticks", Category);
            

        }

    }
}
