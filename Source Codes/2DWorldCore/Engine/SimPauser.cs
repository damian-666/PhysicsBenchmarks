using FarseerPhysics.Dynamics;
using System;
using System.Diagnostics;

namespace _2DWorldCore
{
    ///<summary>
    ///a class to lock the simulation while loading stuff into physics,which runs on the background thread, to avoid thread conflict. 
    ///loading is done on the UI thread.  Usage is using ( SimPause xx = new SimPauser)
    /// </summary>
    internal class SimPauser : IDisposable
    {
        private bool isRunning;

      //  static private volatile bool physicsLocked = false;

        public SimPauser()
        {

            //this makes the physics engine not do anything
            isRunning = ShadowFactory.Engine.World.PhysicsThread.IsRunning;

            //both methods in case not using the locks

            //TODO ty getting this to work
        //    Body.NotCreateFixtureOnDeserialize = true;

            //   ShadowFactory.Engine.EngineTimer.Stop(); //if Timer ever gets used, maybe needed


            //incase this isi called in the middle of an update.
            //need a handler that 3 can call, now there is view and this on the UI thread, and physics 


            if (!ShadowFactory.Engine.World.PhysicsThread.WaitForAccess(500))
            {
                Debug.WriteLine("SimPauser timed out waiting for physics access"); 
            }; //as soon as physics thread is done an update , get the lock so it cant continue

            ShadowFactory.Engine.World.PhysicsThread.IsRunning = false;  
          

            //now isrunning is false , physics loop will not do anythign, safe to release it .

//unless it timed out should be improssilbe for racecondition, and loading files which affects the broadphase and stuff can be done


        }
        public void Dispose()
        {
            //when using {} goes out of scope this is called to set it back.
            ShadowFactory.Engine.World.PhysicsThread.FinishedAccess();

            ShadowFactory.Engine.World.PhysicsThread.IsRunning = isRunning;
          //  ShadowFactory.Engine.EngineTimer.Start();

        }


    }
}
