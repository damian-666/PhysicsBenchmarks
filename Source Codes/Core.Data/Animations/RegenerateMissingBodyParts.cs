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
    /// An LFE that can scale up (resize to become bigger) any spirit Body piece incrementally.
    /// </summary>
    public class RegenerateMissingBodyParts : ScaleSystem
    {

        private bool _regrowingAFullLeg = false;

        /// <summary>
        /// means one or more thigh is broken.. whole leg missing
        /// </summary>
        public bool IsRegrowingAFullLeg
        {
            get { return _regrowingAFullLeg; }
        }

        private bool _regrowingArms = true;
        public bool IsRegrowingArms
        {
            get { return _regrowingArms; }
        }


        public RegenerateMissingBodyParts(Spirit sp, string name, IDictionary<Body, float> regenParts,
            float minScale, float maxScale, float incrementScale, int frameCountPerCycle)
            : base(sp, name, regenParts, minScale, maxScale, incrementScale, frameCountPerCycle)
        {
            if (frameCountPerCycle < 1)
            {
                throw new ArgumentException("frameCountPer Cycle must be >= 1");
            }

            _regrowingArms = ScalingPartScaleMap.Any(x => (x.Key.PartType & PartType.Arm) != 0);
            _regrowingAFullLeg = ScalingPartScaleMap.Any(x => (x.Key.PartType & PartType.Thigh) != 0);

            // hide all regrow parts
            foreach (KeyValuePair<Body, float> pair in ScalingPartScaleMap)
            {
                pair.Key.IsVisible = false;
            }


            //TUNING .. tried. -0.06.. stable but slow 
            const float shrinkScalePerUpdate = -0.06f; // TUNING NOTES.. dh tried  -0.2 on leg  not stable ..  if shrinking to much..( "tunneling" or displacing bodies and joint anchors too far in one cycle, can result in instability
            //causes chaos.. if tuning too little  ( flicker of partially grown items lasts longer while replacements shrink ) 

            //DH after build 669.. tuned it to -0.07 from .1... near gettting tiny one elg could get unstable..
            const int frameCountPerNestedShrinkCycle = 3;   //notes  tried 1 .. faster but physics need aother frame solve joints closer.

          

            //NOTES TODO FUTURE   (ISSUE partially regrown item hidden during shrink of invisible replacement parts)
            //1.  FIX 1could drop this increment shrink and transform bodies to "spirit CS" and shrink in one step.  figure - mainbody.wcs  ( rotation might need more sin cosine math..

                //   a)  if fixture and fixture wsa separate from deseralize.. might be easy..  orgination doesn matter..
            // leg or arem just must be straight..
            //2.  could not allow regen until all prior bodies and finished growing... maybe  accelerated them on break of something during grow. ( skip additive regrowth and just grow faster)
            // TODO notes.. regrowth  like this should suck energy..

            // instead of 'teleport' scaling down to extreme small value quickly, we scale it down on multiple step using nested Shrink LFE.
            // some bodies need to shrink to minimum scale.
            // while some others only need to shrink partialy to previous grow value (continue previous grow).

            Dictionary<Body, float> growingPartStartNew = new Dictionary<Body, float>();
            foreach (KeyValuePair<Body, float> pair in ScalingPartScaleMap)
            {
                if (pair.Value == 1f)
                {
                    growingPartStartNew.Add(pair.Key, pair.Value);
                }
                else
                {
                    // each Body from previous LFE will run on its own Shrink LFE. 
                    // this is because each Body might have different scaling. 
                    Dictionary<Body, float> growingPartPrevLFE = new Dictionary<Body, float>();
                    growingPartPrevLFE.Add(pair.Key, 1f);

                    // shrink for grow after grow need short frame cycle
                    AddNestedShrinkLFE(name + "ShrinkPartial", growingPartPrevLFE, pair.Value, shrinkScalePerUpdate, frameCountPerNestedShrinkCycle);
                }
            }

            if (growingPartStartNew.Any())
            {
                AddNestedShrinkLFE(name + "ShrinkFull", growingPartStartNew, minScale, shrinkScalePerUpdate, FrameCountPerCycle);
            }
        }


        /// <summary>
        /// This is to shrink replacement joints gradually.   
        ///  if shrinking to much..( "tunneling" or displacing bodies and joint anchors too far in one cycle, can result in instability
        /// </summary>
        /// <param name="name"></param>
        /// <param name="shrinkingParts"></param>
        /// <param name="minScale"></param>
        /// <param name="frameCountPerCycle"></param>
        private void AddNestedShrinkLFE(string name, IDictionary<Body, float> shrinkingParts, float minScale, float shrinkScalePerCycle, int frameCountPerCycle)
        {
            Shrink shrink = new Shrink(Parent, name, shrinkingParts, minScale, _maxScale, shrinkScalePerCycle, frameCountPerCycle);
            shrink.ParentEffect = this;
            LowFrequencyEffects.Add(shrink);
        }


        // this is to appear (simulate cell additive growth..  otherwise is stays small so long then grows too fast).  
        // note: currently only support maxScale = 1.  fix later if really needed.  
        protected override Vector2 GetGrowScale(List<Body> forRemoval, Body b, ref float growSize)
        {
            //TODO FUTURE can we do a quick regrow if  base:GetGrowScale if another replace pending?
            // then just wait till its finished .. skip the partial regrow.. start new regrow, partial shrinking, etc.. flicker

            float incrementScale = _incrementScale;

            if (growSize > 0.5f)  //  after hard is .4 grown keep if short and freaky for longer  
            {
                incrementScale = _incrementScale / 2.5f;
            }

            // this is to appear (simulate cell additive growth..  otherwise is stays small so long then grows too fast).  
            float nextScale = 1f + ((1f - growSize) * incrementScale);

            // nextscale can reach 1 because rounding of small increment  
            // now afterscaled will never goes above 1,  
            // better check for grow size,   
            // after 0.9 the increment is becoming very small, very slow regrow  
            // after 0.97 regrow increment is rarely significant  
            if (growSize > 0.9f)
            {
                // make sure next scale will result to growSize=1.  
                nextScale = 1.0f / growSize;
                forRemoval.Add(b);
            }

            Vector2 scaleMultiplierForBodyAndDress = new Vector2(nextScale, nextScale);
            growSize *= nextScale;     // when afterScaled > 1, growSize should be 1 (or close to 1) here  

            return scaleMultiplierForBodyAndDress;
        }


        public override void Update(double dt)
        {
            base.Update(dt);

            // for now we don't allow regrow if nested shrink lfe still running
            if (IsNestedScaleRunning())
            {
                return;
            }

            if (_firstTime == true)
            {
                _firstTime = false;

         

                // restore visibility
                foreach (KeyValuePair<Body, float> pair in ScalingPartScaleMap)
                {
                    pair.Key.IsVisible = true;

                }

                // because now we use shrink lfe before grow, we need to sync that initial growsize, or else it will explode.
                ApplyBodyScaleToMap();
            }

            DoUpdate();
        }


        /// <summary>
        /// Check if other nested scaling lfe (such as shrink) is running.
        /// </summary>
        /// <returns></returns>
        public bool IsNestedScaleRunning()
        {
            return LowFrequencyEffects.OfType<ScaleSystem>().Any();
        }


        /// <summary>
        /// Check if nested shrink lfe contains specific body.  ( used to prevent grabing with shrinking inviso hand)
        /// </summary>
        /// <returns></returns>
        public bool DoesNestedShrinkContain(Body body)
        {
            return LowFrequencyEffects.OfType<Shrink>().Any(x => ( x.ScalingPartScaleMap.ContainsKey(body)));      
        }

    }
}