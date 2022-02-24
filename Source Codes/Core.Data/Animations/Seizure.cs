using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;

namespace Core.Data.Animations
{
    public class Seizure : LowFrequencyEffect
    {
        #region private members
        Random _randomGen;
        float _seizureDuration;
        float _magnitude;
        //float _fadefactor;  // linear function? 
        bool _fatal;
     
        //   TargetFilter _targetFilter;
        float _rigormortisTime = -1f;
        double _fadeFactor;

        #endregion

        /// <summary>
        /// Spasms, uses target filter and random jitters
        /// </summary>
        /// <param name="magnitude"></param>
        ///<param name="magnitude">part of an angle for max twitch</param>
        ///<param name="duration">duration in sec</param>
        ///<param name="fadefactor">magiture change by this during </param>
        ///<param name="fatal">after seizure enegy becomes zero</param>
        ///<param name="rigormortisTime">after this time after death body become stiff in sec -1 means stay limp</param>


        //TODO break into separate DeathThrows that calls Seizure..
        //TODO add decompose time?   break all joints..   bullet bones.. 

        public Seizure(Spirit sp, string name, float magnitude, float duration, float fadefactor, bool fatal, float rigormortisTime )
            : base(sp, name)
        {
            _fadeFactor = fadefactor;

            //  _randomGen = new Random(44);  //shows the hand going though leg 
            _randomGen = new Random();

            _rigormortisTime = rigormortisTime;

            if (_rigormortisTime > 0)
            {
                _duration = duration + _rigormortisTime;// +0.5;  //add a few cycles for unstick during stiffent
            }
            else
            {
                _duration = duration;
            }

            _seizureDuration = duration;

            _fatal = fatal;
            _magnitude = magnitude;
            //  _targetFilter = new TargetFilter();

           // LowFrequencyEffect nestedLEF = new UnStickSelf(10, 2, sp);
            //    nestedLEF.OnEffect += OnUnStickCycle;
            //       LowFrequencyEffects.Add(nestedLEF);  //every 10 frames , clear offset filter and turn  IsSelfCollide false to fix any self- intersection when spaz hits himself

            Parent.IsSelfCollide = true;  //spazzing figure will hit himself.  but it can get stuck to self if not bulletted..
           
            //TODO add bullet self.. to be sure..

        }


        //not used..
        //private void OnUnStickCycle()
        //{
        //    //TODO see to set a normall pose for this to work
        //    //anyways this is not used.. leaving all as bullets seems to prevents self-collide during seizure
        //    _offsetFilter.Clear();
        //    Parent.IsSelfCollide = false;
        //    //   Parent.JointBias = 0.5f;  //give it more strenth to unstick.. it wil be set back.

        //    //update joint Bias?

        //    if (ElapsedTime > _seizureDuration)
        //    {
        //        Parent.JointBias = 0.5f;
        //        Parent.JointSoftness = 0;
        //        Parent.IsSelfCollide = true;
        //    }
        //}

        public override void Update(double dt)
        {

            base.Update(dt);  //this will cause our OnUnStickCycle.. calls IsSelfCollide = false, every so often
            Parent.IsSelfCollide = true;  //consider partial 

            //TODO implement  tick, or twitch for just one angle or subset reusing some of this code
            double timeProgress  = ElapsedTime   / _seizureDuration;
            //TODO exponent  with fadefactor?

            if (timeProgress > ElapsedTime)
            {
                timeProgress = ElapsedTime;
            }

            double magnitude = _magnitude - _fadeFactor * (timeProgress * _magnitude);
            
            for (int i = 0; i < Parent.Joints.Count; i++)
            {
                // does this exceed joint limits, seems to 
                Parent.OffsetFilter1.SetOffset(i, (float)(_randomGen.NextDouble() * magnitude))/* * (float) fractionTimeLeft */;
                // if dies while walking will shake during walk in ghastly manner
            }

            if (ElapsedTime > _seizureDuration)
            {
                if (_fatal)
                {
                    // Parent.FallLimp();
                    Parent.Die();

                    if (ElapsedTime > Duration)
                    {
                        //after LFE exires , rigormortis ( it becomes a stiff, not a ragdoll) 
                        // dh comment out for now.leave as ragdoll .. too often a creature sticks in collided state and stows physics.
                           Parent.StiffenJoints();//TODO consider do this in several frames. with LEF.. make sure pose is set for unstuck..
                        //TODO after a time could be disabled joints.. or  even static ( stuck to ground.. for revisiting level).
                        //or make off-screen dead bodies static..  lots of dead bodies slows performance greatly           
                    }                  
                }
                else
                {
                    Parent.IsSelfCollide = false;
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            //  _spirit.Filters.Remove(_offsetFilter);
        }


    }
}
