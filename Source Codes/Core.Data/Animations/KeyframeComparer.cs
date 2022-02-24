using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Data.Animations
{

    /// <summary>
    /// Keyframe Comparer used for Keyframes list sort, sorted by Keyframe's time
    /// </summary>
    public class KeyframeComparer : IComparer<Keyframe>
    {

        const double EPSILON = 0.000000001;

        public int Compare(Keyframe left, Keyframe right)
        {
            return left.Time < right.Time ? -1 : Math.Abs(left.Time - right.Time) < EPSILON ? 0 : 1;
        }
    }

}
