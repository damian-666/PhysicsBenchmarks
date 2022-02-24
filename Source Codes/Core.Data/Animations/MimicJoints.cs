using System;
using System.Collections.Generic;
using System.Text;


using System.Diagnostics;

using Core.Data.Entity;


namespace Core.Data.Animations
{

    /// <summary>
    /// Specify arrays of joint indices so that one set will track the other set. , infinite duration, same life as spirit
    /// </summary>
    public class MimicJoints: Effect
    {

        int[] _srcJoints;
        int[] _destJoints;


        /// <summary>
        ///Specify arrays of joint indices so that one set will track the other set. , infinite duration, same life as spirit
        /// </summary>
        /// <param name="spirit"></param>
        /// <param name="name"></param>
        /// <param name="srcJoints"></param>
        /// <param name="targetJoints"></param>
        public MimicJoints(Spirit spirit, string name, int[] srcJoints, int[] targetJoints)
            : base(spirit, name, -1)
        {
            _srcJoints = srcJoints;
            _destJoints = targetJoints;

            Debug.Assert(srcJoints.Length == targetJoints.Length);
        }


        public override void Update(double dt)
        {
  
            if (OnUpdate != null)
                OnUpdate();

            if (OnUpdateEffect != null)
                OnUpdateEffect(this);


            if (Parent.ActiveBehavior.Keyframes.Count > 0 && Parent.ActiveBehavior.Keyframes[0].Angles.Count == Parent.Joints.Count)
            {
                Parent.TargetFilter.Clear();
                return;
            }


            for (int i = 0; i < _destJoints.Length; i++)
            {
                Parent.TargetFilter.SetTarget( _destJoints[i] , Parent.Joints[_srcJoints[i] ].TargetAngle);
            }
        }



    }
}
