using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;

namespace Core.Data.Animations
{
    //TODO use this inside the reachpickup.. if needed..
    public class Stoop : Effect
    {
       
        //TODO FUTURE.. move stoop code and all the joint index  to here if needed.. or keep in StraightJaborGrab if practical.. add relax at end to parent.

        public Stoop(Spirit spirit, string name, bool bleft)
            : base(spirit, name)  //too store the joint indeces
        {
 
        }

    }
}
