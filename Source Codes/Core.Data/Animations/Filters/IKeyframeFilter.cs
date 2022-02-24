using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;


namespace Core.Data.Animations
{
    /// <summary>
    /// Interface class for Keyframe Filters
    /// </summary>
    public interface IKeyframeFilter
    {
        void Reset(int angleCount);
        void Clear();
        void Update(int index, ref float angle);
    }
}
