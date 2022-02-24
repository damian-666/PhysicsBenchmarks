using Core.Data.Entity;


namespace Core.Data.Animations
{

    /// <summary>
    /// this will set a Specified Bias on select joint for a time..usually lower,  to use after squatting, raising hands, etc, or with the joint motor damping to absorb shocks for a brief period, then let the bias go back to restore the position
    /// </summary>
    public class SetBias : Effect
    {

        float _biasFactor;

        int[]_jointIndecies;

        /// <summary>
        /// Set bias on selected joints for a time.  
        /// </summary>
        /// <param name="spirit"></param>
        /// <param name="duration"></param>
        /// <param name="biasFactor"></param>
        /// <param name="jointIndecies">if null , will set all powered joints.</param>
        public SetBias(Spirit spirit, string name,  double duration, float biasFactor, int[] jointIndecies)
            : base(spirit, name, duration)
        {
            _jointIndecies = jointIndecies;
            _biasFactor = biasFactor;
        }

        public override void Update(double dt)
        {
            base.Update(dt);

            if (_jointIndecies == null)
            {
                Parent.ApplyJointBias(_biasFactor);
            }
            else
            {
                foreach (int jointindex in _jointIndecies)
                {
                    Parent.Joints[jointindex].BiasFactor = _biasFactor;
        
                }
            }
  
        }

        
    }
}

    
    