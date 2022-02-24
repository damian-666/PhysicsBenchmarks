using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics;
using FarseerPhysics.Dynamics;

namespace Core.Data.Animations
{

    public class WindGust : LowFrequencyEffect
    {
        double _minMagnitude;
        double _maxMagnitude;

        public double Speed;

       //TODO inner gust maybe..  look at blowing sand..
        public WindGust(Spirit sp, string name, double duration, double minMagnitude, double maxMagnitude, double frequency)
            : base(sp, name, duration, 1 / frequency)
        {
            _minMagnitude = minMagnitude;
            _maxMagnitude = maxMagnitude;
        }

        public override void Update(double dt)
        {
            base.Update(dt);

            if ( Double.IsInfinity( Period) )          
            {
                Speed = _minMagnitude;
            }

            double mag = _maxMagnitude - _minMagnitude;
            Speed = _minMagnitude + mag * Math.Cos(2 * Settings.Pi * ElapsedTime / Period);

        }
    }
}
