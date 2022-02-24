using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FarseerPhysics;

using Core.Data.Entity;



namespace Core.Data.Animations
{
    /// <summary>
    /// base class for an arm motion.  these usually consists of two  or more target pos based on a target 
    /// </summary>
    public class ArmMotion : Delay
    {
        protected int _dirfactor = 1;
        protected int _shoulderindex;
        protected int _elbowindex;
        protected int _wristindex;


        public ArmMotion(Spirit spirit, string name, bool leftArm, int idxShoulder, int idxElbow, int idxWrist)
            : base(spirit, name)
        {

            base.Left = leftArm;

            _shoulderindex = idxShoulder;
            _elbowindex = idxElbow;
            _wristindex = idxWrist;

            _dirfactor = (leftArm ? 1 : -1);
        }


        public override void Update(double dt)
        {
            base.Update(dt);
        }


    }
}
