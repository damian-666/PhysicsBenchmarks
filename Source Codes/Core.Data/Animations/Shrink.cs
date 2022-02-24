//#define SHOWSHRINKPROCESS


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Farseer.Xna.Framework;
using FarseerPhysics;                                                  
using FarseerPhysics.Dynamics;

using Core.Data.Entity;


namespace Core.Data.Animations
{
    /// <summary>
    /// An LFE that can scale down (resize to become smaller) any spirit Body piece incrementally.
    /// </summary>
    public class Shrink : ScaleSystem
    {
        public Shrink(Spirit sp, string name, IDictionary<Body, float> partsToScale, float minScale, float maxScale, float incrementScale, int frameCountPerCycle)
            : base(sp, name, partsToScale, minScale, maxScale, incrementScale, frameCountPerCycle)
        {
            foreach (KeyValuePair<Body, float> pair in ScalingPartScaleMap)
            {
                pair.Key.IsVisible = false;
            }
        }


        public override void Update(double dt)
        {
            base.Update(dt);

            if (_firstTime == true)
            {
                _firstTime = false;

                // need to call this again because Plugin.Loaded() will restore collidable state
                SetScaledBodiesNonCollidable();

#if SHOWSHRINKPROCESS
                // to see shrink process visually.
                foreach (KeyValuePair<Body, float> pair in ScalingPartScaleMap)
                {
                    pair.Key.IsVisible = true;
                }
#endif

            }

            DoUpdate();
        }


        protected override Vector2 GetGrowScale(List<Body> forRemoval, Body b, ref float growSize)
        {
            float nextScale = 1f + _incrementScale;     // can be set for quicker shrink

            Vector2 scaleMultiplierForBodyAndDress = new Vector2(nextScale, nextScale);
            growSize *= nextScale;

            if (growSize < _minScale || growSize > _maxScale)
            {
                forRemoval.Add(b);
                b.IsVisible = true;     // make it visible and collidable again (for shrink case).
                b.IsNotCollideable = false;
            }

            return scaleMultiplierForBodyAndDress;
        }


    }
}
