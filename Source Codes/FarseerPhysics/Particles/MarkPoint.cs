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
    /// <summary>
    /// A Visual marker on Body, has a view in a separate map, which moves with parent body.. used for scars, stains, dressings created or modified at runtime.
    /// </summary>
    [DataContract]
    public class MarkPoint : ReferencePoint
    {
        #region MemVars & Props


        /// <summary>
        /// Life span of this mark point in msec
        /// </summary>
        [DataMember]
        public float LifeSpan { get; set; }


        [DataMember]
        public double Age { get; set; }


        public override bool IsDead
        {
            get { return Age >= LifeSpan; }
        }

        public void Terminate()
        {
            LifeSpan = 0;  //will get removed on next update.
        }
 

        [DataMember]
        public BodyColor Color { get; set; }


        [DataMember]
        public float Radius { get; set; }


       //  [DataMember] //TODO should be here..
      //  public float EllipseXYRatio { get; set; }


        // no need for now, ObjectViewFactory deduce ZIndex from UseType
        ///// <summary>
        ///// ZIndex for this particular point, apart from Parent Body ZIndex.
        ///// </summary>
        //[DataMember]
        //public int ZIndex { get; set; }
        
        /// <summary>
        /// to differentiate mark point for specific use
        /// </summary>
        [DataMember]
        public MarkPointType UseType { get; set; }

        #endregion

        #region Ctor

        public MarkPoint(Body parent, Vector2 localPos)
            : base(parent, localPos)
        {
           _parent.VisibleMarks.Add(this);   //this is not thread safe.. there is a lock around the outer call
           Age = 0;
           UseType = MarkPointType.General;
        }

        #endregion

        #region Methods

        public virtual void Update(double dt)
        {
            if (IsDead)
                return;

            Age += dt * 1000;
        }


        public virtual MarkPoint Clone()
        {
            return MemberwiseClone() as MarkPoint;
        }


        #endregion
    }




    /// <summary>
    /// mark point for specific use,  flags, can be combined.
    /// </summary>
  
    [Flags]
    public enum  MarkPointType
    {
        General = 0,    // use circle shape
        Bruise = (1 << 2),     // will use ellipse shape
        Scar = (1 << 3),       // will use triangle shape
        Liquid = (1 << 4),// use flattened circle shape  // TODO  FUTURE  cut like this instead.. use a scar to normal + rect polygon.. easiest way... unless we do clipping and re-rasterize.
        Burn = (1 << 5)// for strike in high temp zone, hit by spark.. allows us to blacken edges of stuff.
    }


}

