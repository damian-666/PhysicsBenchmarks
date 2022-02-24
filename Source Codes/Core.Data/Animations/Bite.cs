using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics;

namespace Core.Data.Animations
{
    public class Bite: LowFrequencyEffect
    {

        double _magnitude;
        int _jawJointIndex;
        protected float _origAngle; 

        //snap jaw
        public Bite(Spirit sp, string name, int jawJointIndex, double duration, double magnitude, double frequency)
            : base(sp, name, duration, 1 / frequency)
        {
            _magnitude = magnitude;
            _jawJointIndex = jawJointIndex;
            _origAngle = Parent.Joints[_jawJointIndex].TargetAngle;
        }

        public override void Update(double dt)
        {
            base.Update(dt);

            float target = (float)(_origAngle + _magnitude * Math.Sin(2* Settings.Pi * ElapsedTime / Period));
            Parent.TargetFilter.SetTarget(_jawJointIndex,target );

            Parent.Joints[_jawJointIndex].TargetAngle = target;

            if (ElapsedTime > Duration)
            {
                //TODO use TargetFilter2 for effect, plugin uses TargetFilter
                Parent.TargetFilter.SetTarget(_jawJointIndex, _origAngle);
                Parent.Joints[_jawJointIndex].TargetAngle = _origAngle;
            }
        }

    
    }
}
