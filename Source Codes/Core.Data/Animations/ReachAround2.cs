using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Dynamics;

using Core.Data.Animations;
using Core.Data.Entity;



namespace Core.Data.Animations
{

    // summary todo:
    // - check if target blocked (done from yndrd plugin).
    // - execute punch jab on first pose, rotate it 45-90 degree from actual target. ideally keep cast ray until found not blocked, but that will be costly.
    // - bend elbow on second pose, set elbow angle target to the actual target.
    // - for stab down, just swing shoulder down to target, no need to change elbow.


    
    /// <summary>
    /// Could have shared base class with ChopDown or StabAround later.
    /// </summary>
    public class ReachAroundGrab : ArmMotionTwoPose
    {
        private AttachPoint _pickupTarget;


        public ReachAroundGrab(AttachPoint pickupTarget, Spirit spirit, string name, bool leftArm, int idxShoulder, int idxElbow, int idxWrist)
            : base(spirit, name, leftArm, idxShoulder, idxElbow, idxWrist)
        {
            //copied from yndrd punch stab
      //      ArmBias = 0.5; not now used, set in plugin
            DrawBackTime = 0.15;
            ExtendTime = 0.065;
            RepeatDelay = 0;

            // pickup target must be supplied by caller 
            _pickupTarget = pickupTarget;
        }


        // this should applicable for both front and rear grabber
        protected override void UpdateOnFirstPose(double dt)
        {
            Debug.WriteLine("First pose");

            // execute normal punch stab here, but rotate 90 deg from actual target

            // 
        }


        protected override void UpdateOnSecondPose(double dt)
        {
            Debug.WriteLine("Second pose");
        }

    }
}
