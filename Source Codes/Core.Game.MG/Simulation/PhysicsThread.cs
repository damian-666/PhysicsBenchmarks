////#define USETIMER
#define LOCKLESSTEST

using System;
using System.Windows;
using System.Threading;

using FarseerPhysics;
using FarseerPhysics.Dynamics;
using System.Threading.Tasks;
using System.Diagnostics;
using Core.Trace;




namespace Core.Game.MG.Simulation
{

   

    /// <summary>
    /// Multithread support for physics.
    /// </summary>
    public class PhysicsThread : IDisposable
    {

        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint timeBeginPeriod(uint uMilliseconds);

        #region Memvars

        private volatile bool _doExit;

        /// <summary>
        /// Manage access to physics data. Any thread get lock of this _accessEvent
        /// can access physics data safely. Call WaitOne() to wait, and block current thread untillg etting lock or timing out.
        /// Call Set() to release lock. 
        /// Initial value is no lock.
        /// </summary>
        private AutoResetEvent _accessEvent;

        /// <summary>
        /// Manage physics cycle. The purpose of this lock is to make physics thread
        /// not run continuously.
        /// Call Set() to allow physics run one cycle then stop. 
        /// WaitOne() will always be called on beginning of loop, waiting to execute 
        /// next physics update. 
        /// Initial value is locked, so physics thread won't execute until told to.
        /// </summary>
        private AutoResetEvent _cycleEvent;

        private Task _thread;

        private int ThreadID;
        private bool _isDisposed;

        //// to aid debugging lock, string of thread id locking it
        //private volatile string _lockID = "none";


        private int _cycleWaitTimeout;

        private Action<float> _updateCallback;
        private volatile float _updateParam;

        public int SlowMotionTime = 0;

        private Action _onBackgroundUpdate = null;

        private volatile bool _isRunning = true;


        //to prevent deadlock caused by the blocking a thread and Waiting, inreentrant function

        //set when the physics data is Locked by the background thread while updating physics
        public volatile bool IsLockedInBkGrnd = false;

        public static bool IsUsingTimer =
#if USETIMER
        true;
#else
        false;
#endif


#if LOCKLESSTEST
        static public bool Lockless =true;

#else
        static public  bool Lockless = false;
#endif
        #endregion

        public volatile Action OnBkUpdate = null;

        public  Action<object,TickEventArgs> OnPostPhysicUpdate = null;

#region Constructor

        public PhysicsThread()
        {
            Initialize();
        }

        private void Initialize()
        {
            _cycleEvent = new AutoResetEvent(false);//cycle can to be started when everything is read
            _accessEvent = new AutoResetEvent(true);//start Set or open its a lock to prevent read/write collision on physics data,

        
        }

#endregion

#region Public Methods

        /// <summary>
        /// Start the physics thread.
        /// </summary>
        public void Start()
        {
            // return if physics thread is already running or disposed
            if (_thread != null || _isDisposed == true)
                return;

            _thread = new Task(physicsThread);
            _thread.Start();
        }

        /// <summary>
        /// Signal the physics thread to run a cycle. This call is ignored if physics 
        /// still in the middle of process.
        /// </summary>
        /// <param name="timeout">Set timeout to -1 to wait indefinitely, or 0 
        /// to not wait.</param>
        public void RunOneCycle(int timeout)
        {
            if (_isDisposed == true)
                return;

            _cycleWaitTimeout = timeout;
            // free the lock, physics thread is free to run
            _cycleEvent.Set();
        }

        /// <summary>
        /// Use this to wait until the physics update finished / at the end of
        /// cycle, to safely access physics data. 
        /// NOTE 1: If previous WaitForAccess() is still in effect, do not call 
        /// this method before calling FinishedAccess(). Can cause deadlock.
        /// NOTE 2: If timeout parameter is not -1,  check for return value.
        /// If lock not acquired but still continue, this WaitForAccess is useless.
        /// </summary>
        /// <param name="timeout">
        /// time in milliseconds
        /// Set timeout to -1 to wait indefinitely, or 0 to not wait.
        /// </param>
        /// <returns>
        /// True if acquired lock for access. Checking return value is optional 
        /// for timeout = -1.
        /// </returns>
        public bool WaitForAccess(int timeout)
        {
            if (_isDisposed || !IsStarted())
            {
                Debug.WriteLine("wait for access for thread not started");
                //  we can use the timer instead of the thread..            
                return false;  
            }

            if (Thread.CurrentThread.ManagedThreadId == ThreadID)
                return true;

            //TODO remove thos checks..
            return _accessEvent.WaitOne(timeout);
        }

        /// <summary>
        /// Inform that the caller thread has finished access to physics data.
        /// Call this after call to WaitForAccess(). 
        /// Required when previous call to WaitForAccess() return TRUE. 
        /// 
        /// Don't call if previous WaitForAccess() return FALSE.
        /// Don't call this without previous call to WaitForAccess(), can accidentally unlock other WaitForAccess() in different module.
        /// </summary>
        public void FinishedAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId == ThreadID)
                return;

            if (_isDisposed) //todo clean remove thsi test and 
                return;
    // this will allow others to access physics. careful when call this.
            _accessEvent.Set();
       
        }

#endregion
        /// <summary>
        /// Run physics update on behalf of caller thread.
        /// Calling this directly from external module will run non-multithreaded physics.
        /// </summary>
        public void UpdatePhysics()
        {
            try
            {

                DateTime startTime = new DateTime();
                DateTime endTime = new DateTime();

                if (EnableFPS)
                {
                    startTime = DateTime.Now;       // start performance measure
                }

                if (_updateCallback != null)
                {
                    // do physics update here
                    _updateCallback(_updateParam);

                }

                if (EnableFPS)
                {
                    endTime = DateTime.Now;         // end performance measure
                }

                // background update is excluded from time measurement now, 
                // for easy comparison with farseer diagnostic later.

                Action bgupdate = OnBackgroundUpdate;
                if (bgupdate != null)
                {
                    bgupdate();
                }


                if (EnableFPS)
                {
                    TimeSpan deltaTime = endTime - startTime;
                    UpdateCounter(deltaTime);
                }

                // elapsed time is used by simworld
                _elapsedTime += _updateParam;  //_updateParam is DT          
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("PhysicsThread: Exception: {0}", ex.Message));
                //   System.Diagnostics.Debug.WriteLine("Spirit Update stack" + ex.StackTrace);//shows in debugger output window..// todo why wont this show in tool trace window
#if !PRODUCTION
             //   IsRunning = false;   //allow user to save data, or will be stuck in this  loop with frozen UI    TODO revisite.. might be
                                     //listener giving issues from writing to UI on bk thread.. FIX
#endif
            }
        }

        public bool IsStarted()
        {
            return _thread != null && !_doExit;
        }

        static public TickEventArgs ticksArgs;
        static public long ticks = 0;



        /// <summary>
        /// Target frame in milliseconds, if >= 0 physics will run on loop to max the cpu, set to -1 to max out sim
        /// </summary>
        static public int  TargetFrameDT  = 1000/200;  // 200 is desired FPS   
  

        static bool IsRunningSlowly = false;



        // the physics thread

        //NOTE in monogame port we use a background thread instead of Dispatch one on the gameloop timer.  Not sure about the reentrancy on that but could
        //replace all this thread code.

        Stopwatch frameTimer=null;

        long lastElapsed = 0;


        private void physicsThread()
        {

            try
            {
                if (SimWorld.IsDirectX)
                {
                    timeBeginPeriod(1);  //THIS IS CRITICAL or Thread sleep and waitOne give all insconsistent results 1 is the fastest resolution but is system global and might affect scheduling and battery too much. this setting gives consistent enough frame rate from 200 to 100
                }

                ThreadID = Thread.CurrentThread.ManagedThreadId;

                if (frameTimer == null)
                {
                    frameTimer = new Stopwatch();
                    frameTimer.Start();
                }

                //NOTE MIGHT BE SIMPLER TO DO THE SYNC IN THE ENGINE SINCE THE TIMER HANDLERS ARE THERE.
                // The main loop for the physics thread
                while (!_doExit)
                {
                    // wait for the signal that the  Engines Timer has updated before execute, single cycle at a time.   

                    if (IsUsingTimer)
                    {
                        _cycleEvent.WaitOne();// for  monogame not using a timer
                    }

                    // wait if there are any other thread accessing physics data
#if !LOCKLESSTEST
                    if (_accessEvent.WaitOne(200) == true)  //NOTE infinite wait can result in hangs.  
#endif                                                         //here we dont want to wait zero which will cause unnecessary delay or spinning just to calling waitOne(0), want to resume as soon as access is given back, usually, draw code or editor
                    {

#if !LOCKLESSTEST
                        IsLockedInBkGrnd = true;
#endif
                        if (TargetFrameDT > 0)
                        {
                            // frameTimer.Restart();
                            lastElapsed = frameTimer.ElapsedMilliseconds;
                        }


                        OnBkUpdate?.Invoke(); //this used to becould be  dispatched to UI thread,but now we updaet plugin on bk thread. draw separate.. should effectively be sync as waiting for the result  

                        if (_isRunning)
                        {
                            UpdatePhysics();
                        }

                        // other thread can access physics data after this

#if !LOCKLESSTEST
                        _accessEvent.Set(); // Drawing and ui mabye need to get a lock
                                            //ui code is free to get a lock if it asks.  
                                            //its ok since we are blocking the physics, and using await to wait for the dipatch call

                        IsLockedInBkGrnd = false;


#endif

                        long dt;
                        if (TargetFrameDT > 0)
                        {
                            dt = frameTimer.ElapsedMilliseconds - lastElapsed;
                            IsRunningSlowly = dt > TargetFrameDT;

                            if (!IsRunningSlowly)
                            {
                                long diff = TargetFrameDT - dt;

                                if (diff > 0)
                                {
                                    //this will give us consistent framerate so long as    timeBeginPeriod(4) is called, default is aroudn 16 ms
                                    Thread.Sleep((int)diff);

                                 //   Trace.TimeExec.Logger.Trace("slept  in physi bk" + diff.ToString());
                                }

                            }
                        }


#if TIMERTESTS
                    lastElapsed = frameTimer.ElapsedMilliseconds;

                    Thread.Sleep((int)1);

                     dt = frameTimer.ElapsedMilliseconds - lastElapsed;

                    lastElapsed = frameTimer.ElapsedMilliseconds;


                    lastElapsed = frameTimer.ElapsedMilliseconds;

                    Task.Run(() => System.Threading.Thread.Sleep(10));
                    dt = frameTimer.ElapsedMilliseconds - lastElapsed;


                    Task.Delay(100);

                    dt = frameTimer.ElapsedMilliseconds - lastElapsed;


                    lastElapsed = frameTimer.ElapsedMilliseconds;

                    ManualResetEvent backgroundWakeEvent = new ManualResetEvent(false);
                 //   backgroundWakeEvent.Set();
                    backgroundWakeEvent.WaitOne(20);//none thes wprk well on low timer res they use timers  

                    dt = frameTimer.ElapsedMilliseconds - lastElapsed;//here we have a minimum wait of 16

                //to fix this pinvoke https://docs.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod

#endif

                        // Slow down the thread for slow motion,  todo add test ui for this
                        if (SlowMotionTime > 0)
                        {
                            Thread.Sleep(SlowMotionTime);

                        }

                        OnPostPhysicUpdate?.Invoke(null, ticksArgs);

                    }

#if !LOCKLESSTEST
                     else
                     {
                        Debug.WriteLine("physics Access wait timed out in physics update loop");
                     }

#endif


                } // end of do while


            }
            catch( Exception exc)
            {

                Debug.WriteLine("physics update exc " + exc.ToString());

            }

            finally 
            {
#if !LOCKLESSTEST
                if (IsLockedInBkGrnd)
                    _accessEvent.Set(); 
#endif
            }
        }
#region Performance counter

        private volatile float _averageUpdateTime;
        private volatile int _updatePerSec;
        private volatile int _upsAveragingInterval = 3;    // default
        private volatile int _updateCounter = 0;
        private DateTime _lastCounterUpdate = DateTime.Now;
        private TimeSpan _deltaTimeAccum = TimeSpan.Zero;
        private TimeSpan _timeSinceLastUpdate;
        private double _elapsedTime = 0;  // time since the beginning of the simulation.

        /// <summary>
        /// Update average update time for each frame and the number Physics updates per second 
        /// </summary>
        private void UpdateCounter(TimeSpan deltaTime)
        {
            _deltaTimeAccum += deltaTime;
            _updateCounter++;

            // update performance counter
            _timeSinceLastUpdate = DateTime.Now - _lastCounterUpdate;
            if (_timeSinceLastUpdate.Seconds >= _upsAveragingInterval)
            {
                if (_updateCounter != 0)
                {
                    _averageUpdateTime = (float)
                        (_deltaTimeAccum.TotalMilliseconds / _updateCounter);
                }
                else
                {
                    _averageUpdateTime = 0;
                }

                _updatePerSec = _updateCounter / _timeSinceLastUpdate.Seconds;
                _updateCounter = 0;

                World.Instance.UpdatePerSecond = _updatePerSec;  // measured time, public so that body emitter can access it to attempt at load balancing

                _deltaTimeAccum = TimeSpan.Zero;
                _lastCounterUpdate = DateTime.Now;
            }
        }

        /// <summary>
        /// Return time spent on physics update (in milisecond), 
        /// averaged by amount of update per second.
        /// </summary>
        public float AvgUpdateTime
        {
            get { return _averageUpdateTime; }
        }
        /// <summary>
        /// Return averaged amount of update per second.
        /// When specific interval elapsed, this value will be updated using: Num of update divided by num of second elapsed.
        /// Note: this value might get zero when using debugger break (halt).
        /// </summary>
        public int UpdatePerSecond
        {
            get { return _updatePerSec; }
        }
        /// <summary>
        /// Interval (in seconds) before updating UpdatePerSecond. 
        /// Default is 3, which means UpdatePerSecond is updated and averaged every 3 seconds.
        /// </summary>
        public int UPSAveragingInterval
        {
            get { return _upsAveragingInterval; }
        }
        /// <summary>
        /// Return the Elapsed Time since the beginning of the simulation.
        /// Virtual Elapsed Time, updated every physics  cycle.
        /// </summary>
        public double ElapsedTime
        {
            get { return _elapsedTime; }
        }

        public static bool EnableFPS = false;

#endregion

        /// <summary>
        /// Completely turn off and close this physics thread. After this call, 
        /// this physics thread should never be used again.
        /// </summary>
        public void Dispose()     //TODO  this is not release what dispose is for ( removing unmanaged resources) , consider rename to Unload or Release
        {
            if (_isDisposed == true)
                return;

            ShutDown();

            _isDisposed = true;
        }


        public void Exit() { _doExit = true; }
      

        public void ShutDown()
        {
            // signal the physics thread to exit, free the lock, 
            // so it will execute once, then exit.
            _doExit = true;
            _cycleEvent.Set();


            //TODO MG_GRAPHICS.. its Wait hangs not sure why
            // if physics thread have been started, wait until it finished
            if (_thread != null)
            {
                _thread.Wait();
            }

            _cycleEvent.Dispose();
            _accessEvent.Dispose();

            UpdateCallback = null;
            OnBackgroundUpdate = null;
        }


#region Properties

        /// <summary>
        /// The Physics thread function is always looping, if IsRunning is set, 
        /// UpdatePhysics() are called , otherwise it does not get called.
        /// Doesn't have effect if UpdatePhysics() is called externally.
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                if (value != _isRunning)
                {
                    _isRunning = value;
                }
            }
        }


        /// <summary>
        /// Parameter to use when calling physics World.Step(float dt)
        /// </summary>
        public float UpdateParam
        {
            get { return _updateParam; }
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException("value");
                _updateParam = value;
            }
        }

        /// <summary>
        /// Fill this with instance of physics World.Step(float dt)
        /// </summary>
        public Action<float> UpdateCallback
        {
            set { _updateCallback = value; }
        }

        /// <summary>
        /// Fires after physics update but on the background thread, not on the UI Thread
        /// Careful using this event, make sure you don't access UI thread,
        /// If you do access thread,  use a dispatcher, OnUpdate and OnPrePhysicsUpdate instead.
        /// </summary>
        public Action OnBackgroundUpdate
        {
            get { return _onBackgroundUpdate; }
            set { _onBackgroundUpdate = value; }
        }

#endregion
    }
}
