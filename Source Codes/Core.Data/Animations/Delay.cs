using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;


namespace Core.Data.Animations
{




    //TODO do a Delay with a template,  acts on a class like a body..  will define what Userdata is..  too many casts in all this.
   // public class Delay<T> : Effect

    public class Delay : Effect
    {

        //TODO move stuff from effect into Delay?   cleanup what derives from Effect?  lEF..
        //TODO Delay<T>: Effect<T>


        protected Delay(Spirit sp, string name)
            : base(sp, name)
        {
        }

        /// <summary>
        /// Effect with a unique name, can optionally replace existing delay.  The on end effect of the prior will never get called
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="name"></param> 
        /// <param name="replaceExisitng"></param>
        /// <summary>
        public Delay(Spirit sp, string name, double duration, bool replaceExisting)
            : base(sp, name, duration, replaceExisting)
        {
        }


        /// <summary>
        /// Effect with a finite duration in Seconds.  For a temporary condition.  Use OnUpdate or OnUpdateEffect, CanEndEffect,  OnEndEffect or derive a class.
        /// </summary>
        /// <param name="sp">parent spirit</param>
        /// <param name="name">unique name</param>
        /// <param name="duration">Duration in seconds</param>
        public Delay(Spirit sp, string name, double duration)
            : base(sp, name, duration)
        {

        }

     
    }
}
