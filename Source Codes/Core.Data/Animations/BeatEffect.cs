using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;

namespace Core.Data.Animations
{


    /// <summary>
     /// calls back on specifed beats ,  a square wave measure in cycles.
    /// </summary>
    public class BeatEffect: LowFrequencyEffect
    {           
        private int _frameCountCycle;
        private int _frameCycleDuration;
        public Action OnOffCycle;
     //   private bool  _orgIsSelfCollide;//this is if  we apply strenght for weak figure, for now assum use during self collide true..

/// <summary>
///  with  some strength in joints , then Self Collide on, then off to unstick whin self collide is on in a jointed system.
    
/// </summary>
/// <param name="frameCountCycle"> how often in frames to apply the effect, need to be 1 or more</param>
        /// <param name="frameCycleDuration">how long in frames to apply the effect need to be less than frameCountCycle</param>
        public BeatEffect( int frameCountCycle, int frameCycleDuration, Spirit sp, string name)
            : base(sp, name)
        {
            if (frameCountCycle < 1 || frameCycleDuration > frameCountCycle)
            {
                throw new ArgumentException(" frameCountCycle must be >= 1 step and  frameCycleDuration < frameCountCycle");
            }

            _frameCountCycle = frameCountCycle;
            _frameCycleDuration = frameCycleDuration;
        }

        public override void Update(double dt)
        {
            base.Update(dt);
            int remainder = FrameCount %  _frameCountCycle;

            if (remainder < _frameCycleDuration)
            {
                if (OnCycleEvent != null)
                    OnCycleEvent(this);
            }
            else if (remainder == _frameCycleDuration)  //just pass to off cycle
            {
                if (OnOffCycle != null)
                    OnOffCycle();
            }      
        }
    }
  
}
