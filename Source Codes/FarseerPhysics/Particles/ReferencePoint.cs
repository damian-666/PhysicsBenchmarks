using System;
using System.ComponentModel;
using System.Diagnostics;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Collision;
using Core.Data.Interfaces;

#if (XNA)
using Farseer.Xna.Framework.Content;
#endif

using System.Runtime.Serialization;//shadowplay mod




namespace FarseerPhysics.Dynamics.Particles
{
    [DataContract(Name = "ReferencePoint", Namespace = "http://ShadowPlay", IsReference = true)]
    [KnownType(typeof(AttachPoint))]
    [KnownType(typeof(SharpPoint))]
    [KnownType(typeof(MarkPoint))]
    public class ReferencePoint : NotifyPropertyBase
    {
        public ReferencePoint(Body parent, Vector2 localPos)
        {
            _parent = parent;
            _localPosition = localPos;
            Direction = new Vector2(0.0f, -1f);
            IsDead = false;
        }

        protected Body _parent;
        protected Vector2 _localPosition;

        [DataMember(Order = 1)]
        public Vector2 LocalPosition
        {
            get { return _localPosition; }
            set { _localPosition = value; }
        }

        /// <summary>
        /// World position of this reference point.
        /// </summary>
        public Vector2 WorldPosition
        {
            get { return _parent.GetWorldPoint(ref _localPosition); }
            set
            {
                // update local position
                _localPosition = _parent.GetLocalPoint(value);
            }
        }

        public Vector2 _direction;
        //TODO future.. should be perpendicular to end for grips like a normal
        //for sword, will point away from tip.    Hand ( wrist angle) will  try to align to this on grab..
        //could be set by code finding biggest edge nearby on Shape.. then set to that..
 

        //TODO use this for emitters.  now  used for attachpoint normal and maybe sharp point
        /// <summary>
        /// A vector indicating direction outward from the point.   may not be normalized
        /// </summary>
        [DataMember]
        public Vector2 Direction
        {
            get
            {
                return _direction;
            }
            set
            {
                _direction = value;
                NotifyPropertyChanged("Direction");
                NotifyPropertyChanged("DirectionX");
                NotifyPropertyChanged("DirectionY");
                NotifyPropertyChanged("Angle");
            }
        }


        public Vector2 WorldDirection
        {
            get
            {
                return Parent.GetWorldVector(_direction);
            }

        }



        // For easier get set on Property Page
        public float DirectionX
        {
            get { return Direction.X; }
            set {
                NotifyPropertyChanged("Direction");
                NotifyPropertyChanged("DirectionX");
                Direction = new Vector2(value, Direction.Y); }
        }


        // For easier get set on Property Page
        public float DirectionY
        {
            get { return Direction.Y; }
            set {
                NotifyPropertyChanged("Direction");
                NotifyPropertyChanged("DirectionY");
                Direction = new Vector2(Direction.X, value); }
        }


        /// <summary>
        /// Get rotation angle in WCS in Radians. Also used by shadowtool binding (non-searchable, check on xaml).
        /// </summary>
        public float Angle
        {       
            get
            {
                Vector2 dir = Parent.GetWorldVector(Direction);
                return (float)Math.Atan2((float)dir.Y, (float)dir.X);
            }
        }


        /// <summary>
        /// On Shadowtool this will inform AttachPointController to remove this AttachPoint from MapGripToAttachPoints.
        /// </summary>
        public bool IsDisposed { get; set; }

        [DataMember]
        public Body Parent
        {
            get { return _parent; }
            set { _parent = value; }
        }


        /// <summary>
        /// if true this ReferencePoint should no longer usable.
        /// Derived class should implement code to check IsDead.
        /// </summary>
        public virtual bool IsDead { get; protected set; }

      
#if !XNA
        public void MirrorHorizontal(float verticalAxisLocalX)
        {
            LocalPosition = Vector2.MirrorHorizontal(LocalPosition, verticalAxisLocalX);
            Direction = Vector2.MirrorHorizontal(Direction, verticalAxisLocalX);
        }
#endif






    }


}
