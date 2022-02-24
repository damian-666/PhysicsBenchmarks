using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;


namespace Core.Data.Animations
{


    //chagne to TargetAngleFilter   todo are we using this

    /// <summary>
    /// Filter to Set the Target Angle directly
    /// </summary>
    public class TargetFilter : IKeyframeFilter
    {
        #region MemVars & Props

        private Dictionary<int, float> _targetAngles;

        /// <summary>
        /// Temporary unavailable, until SA find an elegant way to use this instead of savinf Spirit property
        /// Remember: No Dependency to Spirit ever
        /// </summary>
       // private Dictionary<int, float> _targetSteps;    

        #endregion


        #region Ctor

        public TargetFilter()
        {
            _targetAngles = new Dictionary<int, float>();
         //   _targetSteps = new Dictionary<int, float>();
        }

        #endregion


        #region Public Methods

        public void Reset(int angleCount)
        {
            // No Reset needed for this kind of filter
        }

        public void Clear()
        {
            _targetAngles.Clear();

        
           // _targetSteps.Clear();
        }

        public void Update(int index, ref float angle)
        {
            if (_targetAngles.ContainsKey(index))
            {
                angle = _targetAngles[index];
            }

            //TODO_SA: Come on Suhendra, add a stepper to smoothly increase or decrease the current angle into target value
            //         So, the angle change won't be jumpy, think an elegant way to do this
        }

        public void SetTarget(int index, float targetAngle)
        {
            if (float.IsNaN(targetAngle))
            {
                return;
            }

            if (_targetAngles.ContainsKey(index))
                _targetAngles[index] = targetAngle;
            else
            {
                _targetAngles.Add(index, targetAngle);
            }
                
        }

        #endregion
    }
}
