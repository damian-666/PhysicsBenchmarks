using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Collections;
using FarseerPhysics.Dynamics.Joints;
using System.Runtime.Serialization;

namespace Core.Data.Collections
{
    /// <summary>
    /// Joint Collection
    /// </summary>
    [KnownType(typeof(RevoluteJoint))]
    [KnownType(typeof(FixedRevoluteJoint))]
    [KnownType(typeof(PoweredJoint))]
    [KnownType(typeof(AngleJoint))]
    [KnownType(typeof(DistanceJoint))]
    [KnownType(typeof(PrismaticJoint))]
    [KnownType(typeof(LineJoint))]
    [KnownType(typeof(WeldJoint))]    //    TODOWELDJOINT uncommment , new levels with weld work , old ones need fixing.
	[KnownType(typeof(Joint))]
 
     public class JointCollection : ObservableCollectionUndoable<Joint>
    {
        public JointCollection():
            base(new List<Joint>())
        {
        }
    }
}
