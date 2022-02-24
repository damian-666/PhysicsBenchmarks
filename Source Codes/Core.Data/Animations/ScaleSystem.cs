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
    /// An LFE that can scale (resize) spirit Body pieces, become bigger or smaller in size. 
    /// Scaling is done incrementally.
    /// </summary>
    public class ScaleSystem : LowFrequencyEffect
    {
        protected int _frameCountPerCycle;

        protected float _minScale;

        protected float _maxScale;

        protected float _incrementScale;

        protected bool _firstTime = true;


        /// <summary>
        /// exectute scale 1 per N cycles..  so 1 is fastest, but physics might not have time to solve joints enough in one update.
        /// 
        /// </summary>
        public int FrameCountPerCycle
        {
            set { _frameCountPerCycle = value; }
            get { return _frameCountPerCycle; }
        }


        private Dictionary<Body, float> _scalingPartScaleMap;
        public Dictionary<Body, float> ScalingPartScaleMap
        {
            get { return _scalingPartScaleMap; }
        }


        /// <summary>
        /// TODO document.. can this be reused easily..
        /// having to supply  Dictionary<Body, float> partsToScale,
        /// does this Diction  means for each part, the target scale? 
        /// </summary>
        /// <param name="sp">parent spirit</param>
        /// <param name="name">naem of this lfe</param>
        /// <param name="partsToScale">
        /// List of regrow bodies with its associated scale.
        /// Each Body have its own associated scale. Normal Body will start with associated scale = 1.
        /// Currently associated scale for a Body might be different with actual physic scale of Body itself.
        /// </param>
        /// <param name="minScale">minimum scale value. body with scale smaller than this will no longer be processed. </param>
        /// <param name="maxScale">maximum scale value. body with scale larger than this will no longer be processed. </param>
        /// <param name="incrementScale">amount of incremental scale per cycle.</param>
        /// <param name="frameCountPerCycle">amount of frame update per cycle. </param>
        public ScaleSystem(Spirit sp, string name, IDictionary<Body, float> partsToScale,
            float minScale, float maxScale, float incrementScale, int frameCountPerCycle)
            : base(sp, name)
        {
            if (frameCountPerCycle < 1)
            {
                throw new ArgumentException("frameCountPer Cycle must be >= 1");
            }

            _incrementScale = incrementScale;
            _minScale = minScale;
            _maxScale = maxScale;

            _frameCountPerCycle = frameCountPerCycle;
            _scalingPartScaleMap = new Dictionary<Body, float>(partsToScale);

            //TODO consider scaling before adding to physics..
            SetScaledBodiesNonCollidable();
        }


        protected void DoUpdate()
        {
            int remainder = FrameCount % _frameCountPerCycle;
            if (remainder == 0)
            {
                // set growing parts to non self collide with spirit.
                foreach (KeyValuePair<Body, float> pair in ScalingPartScaleMap)
                {
                    pair.Key.CollisionGroup = Parent.CollisionGroupId;
                }

                //TODO future .. we might set this after growth  is > 20% to prevent  self collide, joint  limits are usually assuming full length..
                //but need  to be sure full limb is outside of main body before allowoing self collide 
                //  foreach (Body b in _regrowingParts)
                //  {
                //     b.CollisionGroup = 0;
                //   }

                ScaleBodyParts();

                // when ScalingPartScaleMap empty, this LFE will finish itself
                if (ScalingPartScaleMap.Count <= 0)
                {
                    Finish();
                }
            }
        }


        /// <summary>
        /// Scale the size of existing bodies in _scalingPartScaleMap.
        /// Scale repeatly until target scale achieved, then Body will be removed from _scalingPartScaleMap.
        /// </summary>
        protected virtual void ScaleBodyParts()
        {
            if (Parent.EnergyLevel < Spirit.MinEnergyForRegen)  //pause growth if not enough energy..
                return;

            List<Body> forRemoval = new List<Body>();

            // need to set value back to dict, using _regrowingParts will throw collection modified exception
            List<Body> scaledBodies = new List<Body>(_scalingPartScaleMap.Keys);

            foreach (Body b in scaledBodies)
            {
  
                float growSize = _scalingPartScaleMap[b];
                Vector2 scaleMultiplierForBodyAndDress = GetGrowScale(forRemoval, b, ref growSize);

                // Scale body.
                // Note that regrowingBodies must not contain duplicate for this to work.
                b.ScaleLocal(scaleMultiplierForBodyAndDress);  //this will clear collision data
                b.Awake = true; // in case growing when spirt standing still and forced sleep 
                                // for small body, make it only 3 kg weight.. so that mass ratio with main body is not so extreme.
                                //extreme mass ration resutls in wild orbit like behavior for joined bodies, and breakpoints are not even reached.

                if (b.IsNotCollideable == false)//TODO PROBABLY SHOUULD DISABLE WHEN REPLACING.. SHIRINKING .. ENABLE WHEN REGROW..
                {
                    b.UpdateAABB();
                   // b.CheckToCreateProxies();
                }

                b.Enabled = false;//hack.. not sure if its needced.. we should use enabeld instead of IsNotCollidabel. the ahck that cause many isues 
                b.Enabled = true;
                //TODO FUTURE check.. for the shrink .. we  want even the long leg replacement for missing leg to have its density reduced
                //TEST.. break whole leg with Pinned creature... regrow.. see if (invisible long replacement  leg ) gravity torque moves the system weird.

                float massMainBody = Parent.MainBody.Mass;
                const float targetMassRatio = 1f / 8; // 1/8 the mass was shown to be stable in tool experiment with litte foot anchored
                //  about 3 kg per leg piece whn body is  24

                float targetMass = massMainBody * targetMassRatio;

                if (_incrementScale < 0)
                {
                    if (growSize > 0.5)
                    {
                        b.Density = (targetMass / b.Area) * ((1f - growSize)/* + 0.5f*/);     // ranged from 0.1 - 0.4  x target mass
                    }
                }
                //else if (_incrementScale > 0)
                //{
                if (growSize < 0.25f)  // visually determined.. afher this scale the leg usually  is stable.
                {
                    b.Density = targetMass / b.Area;  // setting Mass directly is dangerous because it does not update the Interta moment, used to figure effect of torques .
                }
                else
                {
                    // other than small bodies, use normal density
                    b.Density = Parent.MainBody.Density;
                }
                //}


                // DEBUG: this will keep body scale match with spirit scale (growSize).
                if (b.DressScale.X != growSize)
                {
                    //throw new Exception("Body scale didn't match with growSize.");
                    System.Diagnostics.Debug.WriteLine("Mismatch. Body scale: " + b.DressScale.X.ToString() + ", growSize: " + growSize.ToString() + ".");
                    //return false;
                    //continue;
                }
                // update view, use cached op to prevent thread owner exception.
                // delay view replacement by 1 frame to avoid using initial position of new scaled body,
                // this will hide body view at first frame right after scaling.
                // to see actual body position after scaling, set delay to 0.
                Parent.Level.CacheUpdateEntityView(b, 1);

             //   Parent.Level.CacheUpdateEntityView(b, 0);

                // put back value to map, growsize is modified in this loop
                _scalingPartScaleMap[b] = growSize;
            }

            //TODO test.. we see issue with regrow leg penetrating ground, this is done on a CreateFixture so might work
            World.Instance.Flags |= WorldFlags.NewFixture;


            // remove bodies that no longer need grow
            foreach (Body b in forRemoval)
            {
                _scalingPartScaleMap.Remove(b);
            }

            // udpate spirit
            Parent.UpdateTotalAndCenterMass();
            Parent.UpdateAABB();
        }


        protected virtual Vector2 GetGrowScale(List<Body> forRemoval, Body b, ref float growSize)
        {
            float nextScale = 1f + _incrementScale;

            Vector2 scaleMultiplierForBodyAndDress = new Vector2(nextScale, nextScale);
            growSize *= nextScale;

            if (growSize < _minScale || growSize > _maxScale)
            {
                forRemoval.Add(b);
            }

            return scaleMultiplierForBodyAndDress;
        }


        protected void SetScaledBodiesNonCollidable()
        {
            foreach (KeyValuePair<Body, float> pair in _scalingPartScaleMap)
            {
                pair.Key.IsNotCollideable = true;
            }
        }



        /// <summary>
        /// Body might have been physically scaled previously. We can see this from its DressScale.
        /// Use this actual body scale as scale value in map.
        /// </summary>
        protected void ApplyBodyScaleToMap()
        {
            // need to set value back to dict, iterating _regrowingParts will throw collection modified exception
            List<Body> regrowingBodies = new List<Body>(_scalingPartScaleMap.Keys);
            foreach (Body b in regrowingBodies)
            {
                _scalingPartScaleMap[b] = b.DressScale.X;
            }
        }


    }
}
