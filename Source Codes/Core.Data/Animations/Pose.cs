using System;
using System.Diagnostics;

using Core.Data.Entity;


namespace Core.Data.Animations
{
    /// <summary>
    /// Maintain target angle on selected joints for a duration.  
    /// </summary>
    public class Pose : Delay
    {
        private float _dirFactor;   // left or right. normally left is 1.
        private int[] _jointIndices;
        private float[] _targetAngles;     // target angle for each joint, in radian
        /// <summary>
        /// Maintain target angle on selected joints for a duration.   Uses target filter
        /// </summary>
        /// <param name="spirit"></param>
        /// <param name="name"></param>
        /// <param name="duration"></param>
        /// <param name="jointIndices"></param>
        /// <param name="targetAngles">target angle for each joint, in radian</param>
        /// <param name="dirFactor">commonly filled with GetDirFactor(isLeft) or GetDirFactorFacing().</param>
        public Pose(Spirit spirit, string name, double duration, int[] jointIndices, float[] targetAngles, float dirFactor)
            : base(spirit, name, duration)
        {
            // number of index on joint and target angle must match
            if (jointIndices.Length != targetAngles.Length)
                throw new ArgumentException("Number of item between joint array and target angle array didn't match.");

            _jointIndices = jointIndices;
            _targetAngles = targetAngles;

            _dirFactor = dirFactor;
        }


        public override void Update(double dt)
        {
            base.Update(dt);

            Debug.Assert(_jointIndices.Length == _targetAngles.Length);

            int max = _targetAngles.Length;
            for (int i = 0; i < max; i++)
            {
                Parent.TargetFilter.SetTarget(_jointIndices[i], _targetAngles[i] * _dirFactor);
            }
        }


    }
}

    
    