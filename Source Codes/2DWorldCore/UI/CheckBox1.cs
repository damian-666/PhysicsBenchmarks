using FarseerPhysics.Collision;
using System;
using System.Collections.Generic;
using System.Text;

namespace _2DWorldCore.UI
{
    public class CheckBox1 : CheckBox
    {
        /// <summary>
        /// default style of checkbox w standard  win10 minimal style checkmark
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="name"></param>
        public CheckBox1(AABB bounds, string label,  string name = null) : base(bounds, label, name, "checkbox64OFF", "checkbox64ON")
        {
        }
    }
}
