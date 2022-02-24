using System;
using System.ComponentModel;
using System.Diagnostics;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Collision;
using FarseerPhysics.Dynamics.Joints;

#if (XNA)
using Farseer.Xna.Framework.Content;
#endif

using System.Runtime.Serialization;



//TODO future this should not be under particles, but to fix it could break level load, need to check ..wyg files
namespace FarseerPhysics.Dynamics.Particles
{

    [Flags]
    public enum AttachPointFlags
    {
        None = 0,
        IsHeart = (1 << 1),
        IsTemporary = (1 << 2),  //created in response to strick or something.. means the clean command will remove this attach point .
        CollideConnected = (1 << 3),
        IsTouched =( 1 <<4),   //means has already been touched.  NOTE NOT USED.. might be left for legacy files
        IsDisabled = (1 << 5), // means can't be grabbed at pressent.
        IsHeart_CollideConnected = ( IsHeart | CollideConnected),
        SteeringControl =( 1 << 6),//TODO  maybe use this
        IsClaw = (1 << 7)

    }

    /// <summary>
    /// AttachPoint object can  connect to one other AttachPoint.   It will create a PoweredJoint between the two
    /// </summary>
    [DataContract(Name = "AttachPoint", Namespace = "http://ShadowPlay", IsReference = true)]
    public class AttachPoint : ReferencePoint
    {
        
        private PoweredJoint _joint;
        private AttachPoint _partnerAttachPt;  // the other attachpoint 
        private float _stretchBreakpoint;
        //private float _rotateBreakpoint;  not implemented in Farseer 3.2

        //public event EventHandler Attached;

        /// <summary>
        /// Fires when attach point is break/detached, either manually or by force.
        /// First param is the attach point this event listens to.
        /// Second param is the attach point PAIR of first param, BEFORE it break/detached. Because after detached, PAIR is always null.
        /// </summary>
        public Action<AttachPoint, AttachPoint> Detached;

        /// <summary>
        /// Called when attach point is release on command
        /// </summary>
        public Action Releasing;


        [DataMember]
        public AttachPointFlags Flags { get; set; }

        #region Publics & Properties

        /// <summary>
        /// Optional. But mandatory for controller attach point, especially when 
        /// spirit have multiple controller attach point.
        /// </summary>
        [DataMember]
        public string Name { get; set; }



        /// <summary>
        /// If this joint null then attach point currently not connected. 
        /// Not serialized to avoid issue in auto-deserialization order. 
        /// </summary>
        public PoweredJoint Joint
        {
            get { return _joint; }
            set { _joint = value; }
        }

        /// <summary>
        /// Get other end of attachpoint pair. Only valid if Joint property is not null
        /// (except when deserialized).
        /// </summary>
     //   [DataMember] don't save partner might cause issues 
        public AttachPoint Partner
        {
            get
            {
                if (_joint == null) return null;
                return _partnerAttachPt;
            }
            set { _partnerAttachPt = value; }      // for deserialize only, don't access
        }

        // For compatibility purposes, AttachPoint redefine parent again
        [DataMember]
        public new Body Parent
        {
            get { return _parent; }
            set { _parent = value; }    // for deserialize only, do not access
        }


        // This attach point cant be grabbed, it is a grabber.   For point inside of hands only.  Fingers can be grabbed , they 
        //will get extra points added .   
        [DataMember]
        public bool IsGrabber { get; set; }



        //TODO FUTURE ..either allow to  combine PartType flags and remove this  property or removePartType.Control type,
        //fix old levels.
        /// <summary>
        /// Pass input command to the ownerspirit when this item is held.
        /// Same as PartType.Control.   was needed since some spirit like Umbrella must use MainBody as handle, and cannot currently combine Parttype as bit flags
        /// </summary>
        [DataMember]
        public bool IsControl { get; set; }



        //TODO search this in targeting
        /// <summary>
        /// Targeting will target this as a vital place to stab.. used by heart.. A mild joint will apply some reaction
        /// </summary>
        [DataMember]
        public bool IsTarget { get; set; }


        /// <summary>
        /// Angle is in radian.
        /// during pickup or hold  its using the attach point direction like a sword
        /// but during throw or thrust  , wrist angle   is turned by this angle.    to correct the aim.
        ///  for spear this would be 90 degree , for gun  more like 30 ( old pirate 1600s style handgun)
        ///  Angle is positive Counterclockwise as usual.   Less assume handle always is turned down.
        ///  So a positive value means a Right (west ) designed gun .  
        ///  If this is too confusing well add a  Direction.East / west. enum.    West aiming weapons can we used upside down to shoot east.
        /// </summary>
        [DataMember]
        public float HandleAngle { get; set; }



        [DataMember]
        public bool IsClimbHandle { get; set; }

        /// <summary>
        /// This is the minimum size a hand must be to grab this..  
        /// </summary>
        [DataMember]
        public float  HandleWidth { get; set; }



        public float ReactionForce
        {
            get
            {
                if (_joint != null && _partnerAttachPt != null)
                {
                    return _joint.ReactionForce;
                }
                else return 0;
            }
        }



        float _jointSoftness = 0.0f;
        /// <summary>
        /// Sets the softness of the temporary joint, 1 is the max , very loose
        /// </summary>
        [DataMember]
        public float JointSoftness
        {
            get { return _jointSoftness; }
            set
            {
                _jointSoftness = value;
                if (_joint != null && _partnerAttachPt != null)
                {
                    _joint.Softness = MathHelper.Max(_jointSoftness, _partnerAttachPt.JointSoftness);
                }
            }
        }

        /// <summary>
        /// This measures how far attachpoint joint can stretch before break. Value
        /// only applied to joint when it's available. Because one joint connect
        /// 2 attachpoints, only the lowest value from both will be applied.
        /// </summary>
        [DataMember]
        public float StretchBreakpoint
        {
            get { return _stretchBreakpoint; }
            set
            {
                _stretchBreakpoint = value;
                if (_joint != null && _partnerAttachPt != null)
                {
                    _joint.Breakpoint =
                        MathHelper.Min(_stretchBreakpoint, _partnerAttachPt._stretchBreakpoint);
                }
            }
        }


        /* not implemented in farseer 3.2
        /// <summary>
        /// This measures how far attachpoint joint can rotate before break. Value
        /// only applied to joint when it's available. Because one joint connect
        /// 2 attachpoints, only the lowest value from both will be applied.
        /// </summary>
        [DataMember]
        public float RotateBreakpoint
        {
            get { return _rotateBreakpoint; }
            set
            {
                _rotateBreakpoint = value;
                if (_joint != null && _pair != null)
                {
                    _joint.AngleJoint.Breakpoint =
                        MathHelper.Min(_rotateBreakpoint, _pair._rotateBreakpoint);
                }
            }
        }*/

        #endregion


        #region Constructor

        /// <summary>
        /// Position is relative to parent (geom or body local coordinate)
        /// </summary>
        public AttachPoint(Body parent, Vector2 localPos)
            : base(parent, localPos)
        {
            _parent.AttachPoints.Add(this);
            //_parent.Updated += ParentUpdateHandler;

            _stretchBreakpoint = float.MaxValue;

            // _rotateBreakpoint = float.MaxValue;
        }

        // note: Body.AttachPoints is currently the first property that get 
        // deserialized on Body. This should be noted when using OnDeserialized 
        // tag and accessing parent Body.
        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
        }

        #endregion


        #region Methods

        public void Update()
        {
            NotifyPropertyChanged("ReactionForce");
        }


        /// <summary>
        /// Connect this AttachPoint to another AttachPoint. Joint result will 
        /// be automatically inserted into physics.
        /// </summary>
        public bool Attach(AttachPoint other)
        {
            return Attach(other, 0);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <param name="targetAngleCorrection">Correction to TargetAngle in radians.</param>
        /// <returns></returns>
        public bool Attach(AttachPoint other, float targetAngleCorrection)
        {
            if (other == null || other._parent == null ||
                _joint != null || other._joint != null) return false;

            _partnerAttachPt = other;
            other._partnerAttachPt = this;

            CreateTemporaryAttachJoint(targetAngleCorrection);

            //  insert AttachPoint.Joint into physics
            if (_joint != null)
            {
                World.Instance.AddJoint(_joint);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Disconnect AttachPoint manually.
        /// </summary>
        public void Detach()
        {
            // just perform break joint normally. this will in turn call OnJointBreak 
            // listener on this and pair.
            if (Releasing != null)
            {
                Releasing();
            }

            if (Partner != null && Partner.Releasing != null)
            {
                Partner.Releasing();
            } 

            if (_joint != null && _joint.Breaking != null)
            {
                // we ignore both joint.IsBreakable and result from joint.Breaking here. 
                // we assume that when detach, joint must always break.
                // connected attach point must always handle _joint.Breaking event (not null).
                _joint.Breaking(_joint);
            }  
   
        }


        /// <summary>
        /// Create the joint when Attach.
        /// </summary>
        /// <param name="targetAngleCorrection">Correction to TargetAngle in radians.</param>
        private void CreateTemporaryAttachJoint(float targetAngleCorrection)
        {
            if (_partnerAttachPt == null)
                return;


            //TODO   consider making  this as simple as creating a joint in the tool.  sometimes it grabs 180 deg off..


            // record the angle difference between 2 object, then set that as angle joint TargetAngle.
            // angle correction from caller also added here.
            float curTAngle = (_partnerAttachPt._parent.Rotation - this._parent.Rotation) + targetAngleCorrection;

            // create the connection joint
            _joint = new PoweredJoint(this._parent, _partnerAttachPt._parent, this.WorldPosition);

            // attached bodies are notcollided by default with parent, since its usually inside it
            //..  this flags is set in joints such are on heart ( they are  in body empy space to hold blade.. so as not to allow object like a sword to rotate into the fixture to which it is attached.
            _joint.CollideConnected = ((Flags & AttachPointFlags.CollideConnected ) != 0);

            _joint.IsTemporary = true;  //so the spirit walker won't collect this joint while in Tool

            _joint.LocalAnchorA = this._localPosition;
            _joint.LocalAnchorB = _partnerAttachPt._localPosition;

            // this set the target angle from current angle difference, so when 
            // attaching object, the object will not rotated wildly
            _joint.TargetAngle = curTAngle;

            // apply breakpoint from attachpoint
            _joint.Breakpoint =
                MathHelper.Min(_stretchBreakpoint, _partnerAttachPt._stretchBreakpoint);


            _joint.Softness = MathHelper.Max(_jointSoftness, _partnerAttachPt.JointSoftness);


            _joint.BiasFactor = 0.8f;  //otherwise sword flops around..

            //not implemented in new farseer.. 
            //  _joint.AngleJoint.Breakpoint =
            //     MathHelper.Min(_rotateBreakpoint, _pair._rotateBreakpoint);

            // update others to use the same joint (no duplicates)
            _partnerAttachPt._joint = this._joint;


            // handle joint broken for both
            _joint.Breaking += OnJointBreaking;
            _joint.Breaking += _partnerAttachPt.OnJointBreaking;
        }

        /// <summary>
        /// This will be called after attach point detached normally or 
        /// when joint is about to break by force.
        /// </summary>
        private bool OnJointBreaking(Joint joint)
        {
            if (_joint == null || _joint != joint)
                return true;


            // dispose joint from physics. check first, in case pair had
            // already dispose it.
            // no need for physics lock, because this either called from spirit safe update
            // or physics update (joint break event).
            World physics = World.Instance;

            if (physics.JointList.Contains(_joint)
                // &&   !physics.JointRemoveList.Contains(_joint) // API checks already if removed twice.
             )
            {
                physics.RemoveJoint(_joint);
            }

            _joint.Breaking -= OnJointBreaking;
            _joint.Enabled = false;
            _joint.IsBroken = true;
            _joint = null;

            // fire ondetach event
            if (Detached != null)
            {
                Detached(this, _partnerAttachPt);
            }

            // null this last, after detached event
            _partnerAttachPt = null;

            return true;
        }

        #endregion




    }
}
