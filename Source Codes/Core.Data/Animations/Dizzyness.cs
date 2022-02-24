using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;

namespace Core.Data.Animations
{
    public class Dizzyness : LowFrequencyEffect
    {


        /// <summary>
        /// Creature  gets dizzy loses CanBalance state
        /// </summary>
        ///<param name="duration">duration in sec</param>


        public Dizzyness(Spirit sp, string name, float duration)
            : base(sp, name)
        {
            _duration = duration;

            Parent.CanBalance = false;  //KO'd

        }


        public override void Update(double dt)
        {

            if (ElapsedTime > Duration)
            {
                Parent.CanBalance = true;
            }

            base.Update(dt);


        }






    }
}
