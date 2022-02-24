
using Core.Game.MG;
using Core.Game.MG.Graphics;
using Core.Game.MG.Simulation;

using MGCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Threading.Tasks;


namespace _2DWorldCore
{
    /// <summary>
    /// Legacy Game loop apapted from plugable gameloop timer.
    /// allows switching of the UI face.. as in Sandbox vs Game and level select .
    /// Manages our producer  / consumer Simulations and Display list l  
    /// The physics engine runs on a separate background , gets a lock and updates the physics Bodies display list 
    /// the draw code might be in sync with the screen,, it updates the camera, then
   /// , it draw the current state of the physics bodies, either by geting a lock to the physics or the physics Bodies display list
   ///  Every physics update, Engine gets  a lock and copies body state  to a display list .  
   ///  the engine uptionally uses a timer or just update the loop as fast as possible, Timers cary risk because fine timer resolutions requires processing, in windows must be set per process.
    /// When a new frame is ready a lock is taken an
    /// TODO implement dropped frames if we are behind?  
    /// dont draw every frame if getting behind?  if more frame are calculated between draw requests, in 30 fps graphics, it will definitly not draw every frame.
    /// better just indicate we have a new frame available, copy to the display list and return, not blocking  the UI thread waiting for physcis lock.
    /// </summary>

    public class Engine
    {

        #region MemVars & Props

        // Subsystem
        private SimWorld simworld;

        // current game code
        private IGameCode _activeGameCode;

        // game timing
        protected FrameTimer _engineTimer;


        public static bool IsBackgroundThread = true;


        public static bool IsGameBKThread = true;


        private double _timerInterval = 1 / 200f; //5 millisec

        /// <summary>
        /// Physics engine timer loop interval in millisec
        /// </summary>
        public double TimerInterval
        {
            get => _timerInterval;
            set
            {
                _timerInterval = value;
                _engineTimer.Interval = _timerInterval;

            }
        }

        /// This event will always fire on every tick. Might run parallel with physics.
        /// </summary>
        public event EventHandler<TickEventArgs> Updated;

        #endregion


        #region Constructor




        public Engine(GraphicsDevice gr )
        {
            //todo clean maybe

            //TODO pull in Game.Core file or ref Nez, then

            simworld = new SimWorld( IsBackgroundThread);


           
            Graphics.InitGraphics(gr);
            
      
            PhysicsThread.EnableFPS = true;

                // Initiate the Game Loop
            InitGameLoop();

            if (IsBackgroundThread)
            {
                simworld.StartThread();
            }   
          }

        public void Update(GameTime gameTime)
        {
            SyncUpdate(gameTime);
        }


        /// <summary>
        /// A syncronous update
        /// </summary>
        private void SyncUpdate( GameTime tick)
        {
            try
            {//TODO if using this must get a lock so Draw doesnt access phsyics concurrently MG_GRAPHICS



               // TickEventArgs e = new TickEventArgs(this.tick++, 16, 0, 60); // TODO see if needed goes it cause garbage;

                TickEventArgs e = new TickEventArgs(tick.ElapsedGameTime.Ticks,  
                   (int) tick.ElapsedGameTime.TotalMilliseconds,(int) tick.ElapsedGameTime.TotalSeconds, 
                   (int) (1.0 / tick.ElapsedGameTime.TotalSeconds)
                   ); // TODO    used TargetElapsed time for see if needed goes it cause garbage;
                                                                        //then pass from the game loop with sleep info if we use it .. TODO maybe  use the monogame model, isrunningslowly maybe and some TargetFPS and stuff
           
              //   simworld.Update(this, e);

                //TODO can we get a dispatcher fropm netstandard, can inject it..  better to rearchitect with updatephysics and AI on bk and add an OnDraw for UI and mabye playing  sounds..TODO see MONOGAME forums

                if (simworld.PhysicsThread.IsRunning)
                {
                    _activeGameCode?.PreUpdatePhysics(); //update entities, plugin update

                }

                simworld.PhysicsThread.UpdatePhysics();


                //TODO do not have to await this.  check.

                if (_activeGameCode != null)
                {
       
                    _activeGameCode.Update(this, e);

                    if (Updated != null)
                    {
                        Updated(this, e);
                    }

                    simworld.PostUpdate(this, e);

                }

                return;
            }

            catch (Exception exc)
            {
                Debug.WriteLine("ex in SyncUpdate " + exc.Message);
            }
        }

    



        //NOTES timer issues 
        /// best not to use windows timer, or Monogame timing for physics loop those can be  subject to Windows timer resolution which on labtop can be 0.5ms to 30ms
        /// will not allow to maximize a system CPU.  
        /// 
        /// TODO try this way on silverlight maybe. and in tool for units tests
        /// 
        ///TODO measure plugins time, might be able to run those in parallel
        ///but for now improvements shold be made in physics, its the bottleneck.


        private void InitGameLoop()
        {
            //test for stability

            // _engineTimer.OnTick += new EventHandler<TickEventArgs>(OnUpdate);
            // this one run physics sequentially. physics will run on Timers threah which is background for  PhysicsTimer and for the dispatcher one is foreground.
            //  _engineTimer.OnTick += new EventHandler<TickEventArgs>(OnUpdateNP);

            if (PhysicsThread.IsUsingTimer)
            {
                _engineTimer = new FrameTimer();
                _engineTimer.Interval = _timerInterval;
                _engineTimer.OnTick += new EventHandler<TickEventArgs>(OnTimerUpdate);
                _engineTimer.Start();
            }
            else
            {
                simworld.PhysicsThread.OnBkUpdate += OnBKUpdate;
                simworld.PhysicsThread.OnPostPhysicUpdate = simworld.PostUpdate;
            }
        }


        #endregion
        #region Methods

        //NOTES timer issues 
        /// best not to use windows timer for physics loop
        ///TODO measure plugins time, might be able to run those in parallel
        ///but for now improvements shold be made in physics, its the bottleneck.
        /// <summary> this timer handler desinged for Timerbackground thread,

        ///  It gets access to physics, marks it as Locked, updates views, dispatches and waits for calls like the plugin updates,  on the UI thread and then
        ///  Signals the Physics thread that is can run.
        ///  NOTES        ///  Still subject to Windows timer resolution which on labtop can be 0.5ms to 30ms
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTimerUpdate(object sender, TickEventArgs e)
        {
            if (_isPaused)
                return;
            

            // physics thread should only null when app is in the process of termination, todo then remove this
            if (simworld.PhysicsThread == null)
            {
                return;
            }

            if (simworld.PhysicsThread.IsLockedInBkGrnd)  // to prevent reentrancy if timer is not on the UI thread. TODO che
            {

                //_world.PhysicsThread.RunOneCycle(-1); //does a Set on the _cycleEvent, set timeout to infinity

               // Debug.WriteLine("timer reentered");
                return;
            }

            //locks are to prevent reading and writing to the Bodies position and velocity at the same time
            //for monogame its the producer / consumer model.   with the physics runnig at 60 to 120 hz , the graphics 
            //are syncd with the monitor for UWP and at 60

            // Code block below contains code that must _not_ run parallel with physics, must be on the UI thread
            // so this block will execute after the end of physics cycle.

            // Try getting lock without waiting. if lock obtained, it's 
            // most likely that physics process from previous timer tick has ended.
            // If lock not obtained, just continue rendering on this main (UI) thread,
            // and update other codes that can run parallel with physics.

            //timer is on a background thread thread for UWP monograme that must be vsync
            //it can drive the physics thread faster.  Iin monogame the UI thread blocks and sleeps for that its game update is not called faster than ui refresh rate

            //problem with NOT WAITING HERE  IST THAT PHYSICS CAN GET DONE AND MUST THEN WAIT UNTIL
            //THE NEXT TIMER UPDATE BEFORE DRAWING ITS FRAME , set from 0 to 2000 
            if (simworld.PhysicsThread.WaitForAccess(2000) == true)  //lock physics so we can READ the body properties it writes to //_accessEvent.WaitOne(timeout);
            {
                simworld.PhysicsThread.IsLockedInBkGrnd = true;

                //TODO just update Draw to get a lcok and draw , dont bother with this.
                // rearchitgect  plugins to run on bk and have a draw method.   every one.  its way better then many might can be batched
                try  //needed becasue plugins and called, and code might be in progress
                {
                    //    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                    //     () =>
                    {   // 1. world update: contains sensor update on UI thread to draw it

                        simworld.Update(this, e);

                        // 2. gamecode that need to be executed non-parallel.
                        // some entities are updated through this, should run only if physics enabled.
                        if (_activeGameCode != null && simworld.PhysicsThread.IsRunning)
                        {
                            _activeGameCode.PreUpdatePhysics();// plugin's Update Physics and Update AI called in here

                            // 3. Copy body properties to Views for the UI.  in WPF/SL update canvas attached properties
                            if (simworld.PhysicsThread.IsRunning)
                            {
                                ///   Graphics2.Instance.Presentation.Update();//TODO, should check if this frame has not been copied to graphics, we may do 2 -4 physics frames per graphics
                                simworld.PhysicsThread.UpdatePhysics();
                            }
                        }

                    }
                    //);
                }

                catch (Exception exc)
                {
                    Debug.WriteLine(exc);
                }

                finally {
                    simworld.PhysicsThread.FinishedAccess();

                    simworld.PhysicsThread.IsLockedInBkGrnd = false; 

                  }

            }

            if (IsGameBKThread)
            { 
               
                if (_activeGameCode != null)
                {                
                    _activeGameCode.Update(this, e);
                  
                }
        }

            // 2. other update, can run parallel with physics.
            // this update usually not related to physics, can always execute
            // regardless of physics state.
            if (Updated != null)
            {
                Updated(this, e);
            }

            // 3. ??  can run parallel with physics..  
            simworld.PostUpdate(this, e);
        }




        private void OnBKUpdate()
        {
            try
            {//TODO if using this must get a lock so Draw doesnt access phsyics concurrently MG_GRAPHICS


                   PhysicsThread.ticksArgs = new TickEventArgs(PhysicsThread.ticks++, 16, 0, 60); // TODO see if needed goes it cause garbage;

                    simworld.Update(this, PhysicsThread.ticksArgs);

                     if (simworld.PhysicsThread.IsRunning)
                    {
                        _activeGameCode?.PreUpdatePhysics(); //update entities, plugin update

                       //  Graphics.Instance.Presentation.Update();//copy the body position data to the wpf view object while this is locked.

                    }

                if (_activeGameCode != null)
                {
                     _activeGameCode.Update(this, PhysicsThread.ticksArgs);               
                }


                if (Updated != null)
                {
                    Updated(this, PhysicsThread.ticksArgs);
                }

                return;
            }

            catch (Exception exc)
            {
                Debug.WriteLine("ex in OnBKUpdate " + exc.Message);
            }

        }


        /// <summary>
        /// Performing single update to PhysicsSimulator using thread-safe mechanism.
        /// Stepping through single update at a time.
        /// </summary>
        public void SingleStepPhysicsUpdate()
        {
            // this update ignores whatever state of _world.PhysicsThread.IsRunning currently

            if (simworld.PhysicsThread.WaitForAccess(1000))
            { //
                simworld.PhysicsThread.IsLockedInBkGrnd = true;
                if (_activeGameCode != null)
                {
                    //  entities are updated through this.             
                    _activeGameCode.PreUpdatePhysics();
                }

                // DEBUG TEST: update view here
                //TODO plugins;  //was used in Tool
              
             //   Graphics.Instance.Presentation.Update();

                simworld.PhysicsThread.UpdatePhysics();
                simworld.PhysicsThread.FinishedAccess();

                simworld.PhysicsThread.IsLockedInBkGrnd = false;

            }
        }




        /// <summary>
        /// todo, erase if not needed for uwp.  Might be useful to restart all after exceptions for for leak clearing
        /// </summary>
        public void Shutdown()
        {
            Updated = null;
            ActiveGameCode = null;
            _engineTimer.Stop();
            simworld.ShutDown();


          //TODo clea any display list state.
        }


#endregion


#region Properties


        private bool _isPaused = false;
        /// <summary>
        /// Set the engine paused will pause rendering and physics
        /// </summary>
        public bool IsPausd
        {
            get => _isPaused;
            set => _isPaused = value;
        }



        //TODO maybe gut this , erase it..see the refs.. if can erase..

        /// <summary>
        /// Model of the Simulation
        /// </summary>
        public SimWorld World => simworld;


   
        public FrameTimer EngineTimer => _engineTimer;


        /// <summary>
        /// Get or set current active game code
        /// </summary>
        public IGameCode ActiveGameCode
        {
            get => _activeGameCode;
            set
            {

                if (_activeGameCode == value)
                    return;
                // stop timer first, to prevent update to active game code
                //with new loop timer isnt used.. TODO..if we support the switching gamecode

                bool isRunning = true;
       
  				if (PhysicsThread.IsUsingTimer)
            	{
					_engineTimer.Stop();
               	} 


                // if previous code exist
                if (_activeGameCode != null)
                {
                    // terminate previous code, this will also clean world & view
                    _activeGameCode.Terminate();
                }

                // set new game code
                _activeGameCode = value;

                if (_activeGameCode != null)
                {
                    _activeGameCode.Start();
                }


                if (PhysicsThread.IsUsingTimer && isRunning)
				{
                // start the timer again
                _engineTimer.Start();
				}

            }
        }


#endregion


    }
}
