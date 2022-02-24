using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics;
using FarseerPhysics.Dynamics;

namespace Core.Data.Animations
{
    class Glow : LowFrequencyEffect
    {
        double _minMagnitude;
        double _maxMagnitude;

        //snap jaw
        public Glow(Spirit sp, string name, BodyColor glowColor , double duration, double minMagnitude, double maxMagnitude, double frequency)
            : base(sp, name, duration, 1 / frequency)
        {
            _minMagnitude = minMagnitude;
            _maxMagnitude = maxMagnitude;
            sp.GlowColor = new BodyColor (glowColor);
        }

        public override void Update(double dt)
        {
            base.Update(dt);

            double mag = _maxMagnitude - _minMagnitude;
            Parent.GlowBrightness = _minMagnitude + mag * Math.Cos(2 * Settings.Pi * ElapsedTime / Period);

            if (ElapsedTime > Duration)
            {
                Parent.GlowColor = null;
                Parent.GlowBrightness = 0;
            }
        }
    }

    /// <summary>
    /// glow just one body.. could be used on callback when particle collide with ballon panel to show heat..
    /// </summary>
      class GlowBody : Glow
    {
    
        Body _bodyToGlow;
        //snap jaw
        public GlowBody(Spirit sp, Body b, string name, BodyColor glowColor, double duration, double minMagnitude, double maxMagnitude, double frequency)
            : base( sp,  name,  glowColor ,  duration,  minMagnitude,  maxMagnitude,  frequency)
        {
           _bodyToGlow = b;
        }

        public override void Update(double dt)
        {
            base.Update(dt);        
        }
    }


}