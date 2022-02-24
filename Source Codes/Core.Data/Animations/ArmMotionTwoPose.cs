using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FarseerPhysics.Dynamics.Joints;

using Core.Data.Animations;
using Core.Data.Entity;



namespace Core.Data.Animations
{
    public abstract class ArmMotionTwoPose : ArmMotion
    {
   //     public double ArmBias = 0.7;  //seems stupid to put these as properties.. just make it public so plugin can tune it.
        public double DrawBackTime = 0.15;  //time to draw arm in before extended punch
        public double ExtendTime = 0.065;//1.2;//for testing pointing. //  //time duration of throw punch
        public double RepeatDelay = 0.0;  //time after finished  to repeat punch when key held, for now always zero, immediately repeat


        public Action<bool> OnFirstFramePose;  //calls back to plugin..  bo
        public Action<bool> OnSecondFramePose;

        abstract protected void UpdateOnFirstPose(double dt);  //set the Target filter
        abstract protected void UpdateOnSecondPose(double dt);


        protected ArmMotionTwoPose(Spirit spirit, string name, bool leftArm, int idxShoulder, int idxElbow, int idxWrist)
            : base(spirit, name, leftArm, idxShoulder, idxElbow, idxWrist)
        {
        }


        protected ArmMotionTwoPose(Spirit spirit, string name, bool leftArm, int idxShoulder, int idxElbow, int idxWrist, double armBias, double firstFrameTime, double lastFrameTime)
            : base(spirit, name, leftArm, idxShoulder, idxElbow, idxWrist)
        {
            DrawBackTime = firstFrameTime;
            ExtendTime = lastFrameTime;
            RepeatDelay = armBias;
        }


        public override void Update(double dt)
        {
            base.Update(dt);

            if (ElapsedTime < DrawBackTime)
            {
                if (OnFirstFramePose != null)
                    OnFirstFramePose(Left);

                UpdateOnFirstPose(dt);
            }

            else if (ElapsedTime < DrawBackTime + ExtendTime)
            {
                if (OnSecondFramePose != null)
                    OnSecondFramePose(Left);

                UpdateOnSecondPose(dt);
            }

            else //TODO handle repeat here or in plugin
                Finish();
        }


    }
}
