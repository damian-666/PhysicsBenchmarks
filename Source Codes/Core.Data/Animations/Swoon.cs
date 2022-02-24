using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;

namespace Core.Data.Animations
{
    public class Swoon : LowFrequencyEffect
    {

        //gets reduced temporarily.
        float _originalEnergyLevel;

        bool _recovering = false;


        private float MassFactorSq;



        /// <summary>
        /// Creature  faints
        /// </summary>
        ///<param name="duration">duration in sec</param>


        private Swoon(Spirit sp, string name,  float duration )
            : base(sp, name)
        {
            _duration = duration;
            _originalEnergyLevel = Parent.EnergyLevel;  //TODO better have separate StoredEnergy..
            Parent.CollectEyeJoints();
            Parent.UpdateSpiritAbilities();
            
        }
        
        public Swoon(Spirit sp, string name,  float duration, float energyLevel, float massFactorSq )
            : this(sp, name, duration )
        {
           Parent.EnergyLevel = energyLevel;
           MassFactorSq = massFactorSq;

        }


        public override void Update(double dt)
        {

            base.Update(dt);

            if (!_recovering && ElapsedTime >  Duration - Math.Max(0.5 , Duration * .2))  //80 % of sworn  done... or at least .5 sec...start getting up
            {
                StartToRecover();  //TODO check this for long swoon.. play getting up behavior ?
             }

            if (ElapsedTime > Duration)
            {
                Parent.EnergyLevel = _originalEnergyLevel;
            }

        }


        // raise bias gradually for wakeup..
        private void StartToRecover()
        {

      
            Parent.EnergyLevel =  Math.Min( _originalEnergyLevel,  30f * MassFactorSq);

            Parent.UpdateSpiritAbilities();  //TODO REVIEW...this might override another LFE that affects softness  firstUpdateSpiritAbilities, then plugin, then lfes, then animate .. ISSUE sometimes moving hands seems to move a dead or unconscious screature..

            _recovering = true;
        
        }

    

    }
}
