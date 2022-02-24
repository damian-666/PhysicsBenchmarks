using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Data.Animations
{
    /// <summary>
    /// Filter to Limit angles of spirit bones
    /// </summary>
    public class LimitFilter : IKeyframeFilter
    {
        #region MemVars & Props

        Dictionary<int, float> _lowerMap;
        Dictionary<int, float> _upperMap;

        #endregion


        #region Ctor

        public LimitFilter()
        {
            _lowerMap = new Dictionary<int, float>();
            _upperMap = new Dictionary<int, float>();
        }

        #endregion


        #region Public Methods

        public void Reset(int angleCount)
        {
            Clear();
        }

        public void Clear()
        {
            _upperMap.Clear();
            _lowerMap.Clear();
        }

        public void Update(int index, ref float angle)
        {
            if (_lowerMap.ContainsKey(index))
                if (angle <= _lowerMap[index])
                    angle = _lowerMap[index];
           
            if (_upperMap.ContainsKey(index))
                if (angle >= _upperMap[index])
                    angle = _upperMap[index];
        }

        public void SetLimitUpper(int index, float targetAngle)
        {
            if (_upperMap.ContainsKey(index))
                _upperMap[index] = targetAngle;
            else
            {
                _upperMap.Add(index, targetAngle);
            }
        }

        public void SetLimitLower(int index, float targetAngle)
        {
            if (_lowerMap.ContainsKey(index))
                _lowerMap[index] = targetAngle;
            else
                _lowerMap.Add(index, targetAngle);
        }

        #endregion

    }
}
