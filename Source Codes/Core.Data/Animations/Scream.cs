using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics;

namespace Core.Data.Animations
{
    public class Scream : Bite
    {
        double _magnitude;
        int _jawJointIndex;

        int _neck1JointIndex;  //towards body
        int _neck2JointIndex;

        Random _rand = new Random();
        public bool StraightenNeck = false;


        //snap jaw
        public Scream(Spirit sp, string name, string soundKey, int jawJointIndex, int neck1JointIndex, int neck2JointIndex)
            : base(sp, name, jawJointIndex, 1, 2 , 8)
        {
            _jawJointIndex = jawJointIndex;
            _origAngle = Parent.Joints[_jawJointIndex].TargetAngle;

            _neck1JointIndex = neck1JointIndex;
            _neck2JointIndex= neck2JointIndex;

            if (_rand.Next(2) == 0)
            { // by default is random  50% chance
                StraightenNeck = true;
            }

            UserData = soundKey;
                                    
            //TODO play sound.  until scream end.
            //on head chop stop it.
        }


        public Scream(Spirit sp, string name, int jawJointIndex, double duration, double magnitude, double frequency)
            : base(sp, name, jawJointIndex, duration,magnitude,frequency)
        {
            _magnitude = magnitude;
            _jawJointIndex = jawJointIndex;
            _origAngle = Parent.Joints[_jawJointIndex].TargetAngle;
        }

        public override void Update(double dt)
        {
            base.Update(dt);

            float jawOpenAngle = -0.9f; ;

            if (StraightenNeck)
            {
                Parent.TargetFilter.SetTarget(_neck1JointIndex, 0);
                Parent.TargetFilter.SetTarget(_neck2JointIndex, 0);         
            }

            Parent.TargetFilter.SetTarget(_jawJointIndex, jawOpenAngle);  //open mouth all the way 
            Parent.Joints[_jawJointIndex].TargetAngle = jawOpenAngle;

            if (Parent.Head == null ||  Parent.CannotDoAnything())
            {     
                Finish();          
            }

        }

        public override void Finish()
        {
            Parent.TargetFilter.SetTarget(_jawJointIndex, _origAngle);
            Parent.Joints[_jawJointIndex].TargetAngle = _origAngle;
            base.Finish();
        }


    }
}
