using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;


namespace Core.Data.Animations
{
    public class JointFlex: Delay
    {
       float _biasFactor;
       int _jointIndex;

       double _startDelay;
       float _targetAngle; 

       /// <summary>
       /// Set bias on selected joints for a time.  
       /// </summary>
       /// <param name="spirit"></param>
       ///  <param name="startDelay"> delay before start of this.</param>
       /// <param name="duration">how long to keep this muscle flexed, this joint in this pos in Seconds</param>
       /// <param name="biasFactor">bias during effect, strenth</param>
       /// <param name="jointIndex"></param>
       ///  <param name="targetAngle"></param>

       public JointFlex(Spirit spirit, string name, double startDelay, double duration, float biasFactor, int jointIndex, float targetAngle)
            : base(spirit, name, duration)
        {
            _jointIndex = jointIndex;
            _biasFactor = biasFactor;
            _startDelay = startDelay;
            _targetAngle = targetAngle;
        }

        public override void Update(double dt)
        {
            base.Update(dt);

            if (ElapsedTime > _startDelay)
            {
                Parent.Joints[_jointIndex].BiasFactor = _biasFactor;
                Parent.TargetFilter.SetTarget(_jointIndex, _targetAngle);
            }      
        }
    }

}
