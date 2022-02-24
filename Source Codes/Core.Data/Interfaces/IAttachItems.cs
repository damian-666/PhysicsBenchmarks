using Core.Data.Entity;
using FarseerPhysics.Dynamics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Data.Plugins
{
    public interface IAttachItems
    {


        /// <param name="objectAPType">Type of external object attach point.</param>
         bool Attach(Sensor sensor, PartType spiritAPType, PartType objectAPType, float reachDistance);
    }
}
