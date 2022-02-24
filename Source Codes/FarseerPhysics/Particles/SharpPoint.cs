using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Factories;
using FarseerPhysics.Common;



namespace FarseerPhysics.Dynamics.Particles
{

    //TODO classu should be dropped , cuts could be calculated from ridid geom acute points on collision, and the geom hardness might be used to determine joint damage or not
    //designer should not have to do this.

    [DataContract]
    //  [DataContract(Name = "SharpPoint", Namespace = "http://ShadowPlay", IsReference = true)] //TODOCONTRACTNAMING
    public class SharpPoint : ReferencePoint
    {
        #region MemVars & Props
        
        /// <summary>
        /// Allows only some points to cut 
        /// </summary>
        [DataMember]
        public float BreakGroupID
        {
            get;
            set;
        }

    /// <summary>
        /// If not zero, will inject poison on contact with joint.
        /// </summary>
        [DataMember]
        public float PoisonInjection { get; set; }



        #endregion


        #region Ctor

        public SharpPoint(Body parent, Vector2 localPos)
            : base(parent, localPos)
        {
            _parent.SharpPoints.Add(this);           
        }

        #endregion


        #region Methods

        #endregion
    }
}
