using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using System.Diagnostics;

namespace Core.Data.Animations
{
    /// <summary>
    /// Filter to add offset to angle
    /// </summary>
    public class OffsetFilter : IKeyframeFilter
    {
        #region MemVars & Props


        private Keyframe _offsetKeyframe;

        /// <summary>
        /// Values will be used to set the offset of each angle, heavily used by Script
        /// Never set the angle using this property directy
        /// </summary>
        public List<float> Values
        {
            get { return _offsetKeyframe.Angles; }
        }

        #endregion


        #region Ctor

        public OffsetFilter()
        {
            _offsetKeyframe = new Keyframe();
        }

        public OffsetFilter(int angleCount)
        {
            Reset(angleCount);
        }

        #endregion


        #region Public Methods

        public void Reset(int angleCount)
        {
            _offsetKeyframe = new Keyframe(angleCount);
        }

        public void Clear()
        {
            for (int i = 0; i < Values.Count; i++)
            {
                _offsetKeyframe.Angles[i] = 0;
            }
        }

        /// <summary>
        /// Update the reference angle given by the parameter
        /// </summary>
        /// <param name="index">Angle Index to the Joint index</param>
        /// <param name="angle">Angle Reference</param>
        public void Update(int index, ref float angle)
        {
            angle += _offsetKeyframe.Angles[index];
        }

        public void SetOffset(int index, float value)
        {
            if (float.IsNaN(value))
                return;
            
            Debug.Assert(index > -1 && index < _offsetKeyframe.Angles.Count);
        
            _offsetKeyframe.Angles[index] = value;

        }

        /// <summary>
        /// Safer to use.. all filters are cleared each frame, and we might have one angle to say aling foot with ground , then another to make it hop.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void AddOffset(int index, float value)
        {
            if (float.IsNaN(value))
              return;

            Debug.Assert(index > -1 && index < _offsetKeyframe.Angles.Count);
    
            _offsetKeyframe.Angles[index] += value;
            
        }


        public void AddOffsets(OffsetFilter infilter)
        {

            for (int i = 0; i < infilter.Values.Count; i++)
            {
                if (float.IsNaN(infilter.Values[i]))
                {
                   continue;
                }     
         
                Values[i] +=  infilter.Values[i];
            }


        }




        #endregion
    }
}
