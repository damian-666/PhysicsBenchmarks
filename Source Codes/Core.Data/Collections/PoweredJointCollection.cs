using System.Collections.Generic;

using FarseerPhysics.Dynamics.Joints;

namespace Core.Data.Collections
{
    /// <summary>
    /// Observable collection for PoweredJoint
    /// </summary>
    public class PoweredJointCollection : ObservableCollectionUndoable<PoweredJoint>
    {
        /// <summary>
        /// To overcome the ReadOnly limitation, manual create the list
        /// </summary>
        public PoweredJointCollection()
            : base(new List<PoweredJoint>())
        {
        }

        public PoweredJointCollection(List<PoweredJoint> joints) :
            base(new List<PoweredJoint>(joints))
        {
        }

        public PoweredJointCollection(PoweredJointCollection collection) :
            base(new List<PoweredJoint>(collection))
        {
        }
    }
}
