using System;
using System.Net;
using System.Windows;
using System.Windows.Input;
using Core.Data.Entity;

namespace Core.Data.Animations
{
    public class PeriodicEffect: Effect
    {
        protected double _period = 0;

        public PeriodicEffect(Spirit sp, string name, double duration, double period)
        : base(sp,  name, duration)
        {
             _period = period;
        } 
       
        public PeriodicEffect(Spirit sp, string name  )
        : base(sp,  name)  {}

        /// <summary>
        /// Cycle time in seconds.
        /// </summary>
        public double Period
        {
            get   {    return _period;    }
        }

    }
}
