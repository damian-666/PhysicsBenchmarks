using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;


namespace Core.Data.Animations
{

    /// <summary>
    /// Low Frequency Effect  from audio effect LFE... like swoop , sweep, etc.  can repeat or fade out..
    /// This can change a  Spirits state  based on some function of time.
    /// </summary>
    public class LowFrequencyEffect : PeriodicEffect
    {
        /// <summary>
        /// If this is a nested LowFrequencyeffect, it will remove itself from this owner, not the spirit
        /// </summary>
        public LowFrequencyEffect ParentEffect;

        protected int FrameCount = 0;

        public Action<LowFrequencyEffect> OnCycleEvent;
        public Action<LowFrequencyEffect> OnUpdateCycle;

        private List<LowFrequencyEffect> _nestedLowFrequencyEffects;
        /// <summary>
        /// Collection of Low Frequency Effects , a LFE can contain others.
        /// </summary>
        public List<LowFrequencyEffect> LowFrequencyEffects  //TODO CLEANUP have the owner List<Effect> implement IEffectOwner has one method Remove(x)..
        {
            get
            {
                if (_nestedLowFrequencyEffects == null)
                {
                    _nestedLowFrequencyEffects = new List<LowFrequencyEffect>();
                }
                return _nestedLowFrequencyEffects;
            }
        }


        public LowFrequencyEffect(Spirit sp, string name)
            : base(sp, name)
        {
        }

      
        /// <summary>
        /// And effect that cycles on given period in seconds
        /// </summary>
        /// <param name="spirit">Parent spirit</param>
        /// <param name="name">Unique key</param>
        /// <param name="duration">time in seconds use  , double.PositiveInfinity for never ending </param>
        /// <param name="period">cycle time in Seconds</param>
        public LowFrequencyEffect(Spirit spirit, string name, double duration, double period)
            : base(spirit, name, duration, period)
        {
        }

    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dt"> Time step in sec ( time step of the physics engine) virtual time, can vary depending on loading</param>
        override public void Update(double dt)
        {
            base.Update(dt);

            FrameCount += 1;

            if (_nestedLowFrequencyEffects != null && _nestedLowFrequencyEffects.Any()) // don't copy empty lfe list
            {

                //TODO future cleanup.. why?   used by regrow..   see how parent handles it.. should be all the same.
                List<LowFrequencyEffect> copyLFE = new List<LowFrequencyEffect>(LowFrequencyEffects);  //some LEF will remove them selves from list after expiration, have to make a copy
                foreach (LowFrequencyEffect effect in copyLFE)
                {
                    effect.Update(dt);
                }
            }

            if (OnUpdateCycle != null)
                OnUpdateCycle(this);
        }


        override public void Finish()
        {
            if (ParentEffect != null)
            {
                ParentEffect.LowFrequencyEffects.Remove(this);   //TODO better remove through interface, see teh .. check standard ones.. iParent..  with  one method Remove( object)   LFE for nested, and Spirit, and level , in future..
            }

            base.Finish();

            OnUpdateCycle = null;
            OnCycleEvent = null;
        }


  
    }
}
