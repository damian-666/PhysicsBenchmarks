using System;
using System.Collections.Generic;
using System.ComponentModel;
using Core.Data.Interfaces;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Dynamics;
using Farseer.Xna.Framework;
using FarseerPhysics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Common.PolygonManipulation;

using UndoRedoFramework;


using Core.Game.MG;

namespace Core.Game.MG.Simulation

    //TODO this might not be used... GAme has Engine , and tool using a wpf based timer.  MG_GRAPHICS  erase file if not used
{
    /// <summary>
    /// This cross platfrom class handle all Physics World and Presentation using  monogame
    /// </summary>
    /// 
    public class Simulation    
    {
        #region MemVars

        public delegate void UpdateEventHandler(object sender, TickEventArgs e);

        /// <summary>
        /// This event will always fire on every tick.
        /// </summary>
        public event UpdateEventHandler OnUpdate;

        /// <summary>
        /// Fires before every physics update. This event is fired from main tick
        /// update, but synchronized with physics cycle. Any method executed inside
        /// this event can safely access physics without thread guard.
        /// </summary>
        public event Action OnPreUpdatePhysics;



        private SimWorld _simWorld;

        //note , needs a dispatcher timer , this will be the wrong thread if we used this.. 
        //TODO GRAPHICS_MG
        private FrameTimer _graphicsUpdateTimer;
        private double _currentTime = 0;



        private AmbienceController _ambience = null;

        public AmbienceController AmbienceController
        {
           get { return _ambience; }
        }

        /// <summary>
        /// Most Recent physics Simulation instantiated.  For now only one is instantiated during the game and tool 
        /// </summary>
          public static  Simulation Instance { get; private set; }


        /// <summary>
        /// This sets the Target FPS for graphics update.   May also be used by moving camera timer. graphics must wait for physics , so actual FPS will vary.
        /// </summary>
   
        public int GraphicsMaxFPS
        {
            get { return (int)Math.Round(1.0f / _graphicsUpdateTimer.Interval); }
            set
            {
                _graphicsUpdateTimer.Interval = (1.0 / value);
            }
        }

      

        #endregion


        #region Ctor/Dtor

        public Simulation()
        {
            _simWorld = new SimWorld();

            _graphicsUpdateTimer = new FrameTimer();

            _graphicsUpdateTimer.Interval = 1.0f / GraphicsMaxFPS;

            _graphicsUpdateTimer.OnTick += new EventHandler<TickEventArgs>(_simulationTimer_OnTick);

            _ambience = new AmbienceController();


        }

        #endregion


        #region Methods

        public void Run()
        {
            _graphicsUpdateTimer.Start();
        }


        /// <summary>
        /// This executes on main thread, but physics will run on separate thread.
        /// </summary>
        private void _simulationTimer_OnTick(object sender, TickEventArgs e)
        {
            // try getting lock to update view and others. if lock obtained, it's 
            // most likely that physics process from previous timer tick have ended.
            // if lock not obtained, just continue rendering on this main (UI) thread.         
            if (   _simWorld.PhysicsThread.WaitForAccess(0)  )
            {         

                _simWorld.PhysicsThread.IsLockedInBkGrnd = true;
                _simWorld.Update(this, e);

                if (_currentTime != _simWorld.PhysicsThread.ElapsedTime)// only call this update if physics actually did an update .
                {
                    _currentTime = _simWorld.PhysicsThread.ElapsedTime;

                    // spirit update here ?
                    if (OnPreUpdatePhysics != null)
                    {             
                        OnPreUpdatePhysics();
                    }

                     _ambience.Update(_currentTime, World.PlanetRotationPeriod);
                }



                //TODO udate presentations
                //TODO MGRAPHIC
                //TODO update camera
                     // graphic & camera update
           //     foreach (Presentation view in Graphics.Instance.ViewportPresentationMap.Values)
           //     {
           //         view.Update();
           //         view.Camera.Update(e.Tick);
           //     }



                _simWorld.Sensor.ResetRayViews(); // used to be in Postupdate, caused threading, reentrancy, crash issues with wind rays.
                _simWorld.PhysicsThread.FinishedAccess();
                _simWorld.PhysicsThread.IsLockedInBkGrnd = false;

                // tell physics to begin one cycle. function will return immediately.
                // update will run on separate thread. 
                _simWorld.PhysicsThread.RunOneCycle(-1);     
             }


            // this update usually not related to physics, can always execute //TODO NOTE  when grips are on its using general verteces.  GetWorldPoint.. should probably be done while 
            //physics is locked.. 
            // regardless of physics state.   
            if (OnUpdate != null)
            {
                OnUpdate(this, e);
            }
            
            _simWorld.PostUpdate(this, e);
        }



        /// <summary>
        /// Call this after level closed or before loading a new level. 
        /// Don't call after a level loaded.
        /// </summary>
        public void Reset(bool start)
        {
            _simWorld.Reset(start);

           //TODO clear display lists of rays and stuff from last level

        }

        public void SingleStepPhysicsUpdate()
        {
            SingleStepPhysicsUpdate(false);
        }

        /// <summary>
        /// Performing single update to PhysicsSimulator using thread-safe mechanism.
        /// Stepping through single update at a time.
        /// </summary>
        public void SingleStepPhysicsUpdate(bool ignorePhysicsEnabled)
        {
            _simWorld.SingleStepPhysicsUpdate(ignorePhysicsEnabled);
        }

        #endregion



        #region Properties

        /// <summary>
        /// Model of the Simulation
        /// </summary>
        public SimWorld World
        {
            get { return _simWorld; }
        }

   

        /// <summary>
        /// Physics simulation is running.  Goes to false when physics is Paused.
        /// 
        /// </summary>
        public bool PhysicsRunning
        {
            get { return _simWorld.PhysicsThread.IsRunning && _simWorld.PhysicsThread.IsStarted(); }
            set { _simWorld.PhysicsThread.IsRunning = value; }
        }

        #endregion
    }
}
