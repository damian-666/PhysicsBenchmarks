/*
* Farseer Physics Engine based on Box2D.XNA port:
* Copyright (c) 2010 Ian Qvist
* 
* Box2D.XNA port of Box2D:
* Copyright (c) 2009 Brandon Furtwangler, Nathan Furtwangler
*
* Original source Box2D:
* Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using Farseer.Xna.Framework;
using System.ComponentModel;

namespace FarseerPhysics.Dynamics.Joints
{
    public enum JointType
    {
        Revolute,
        Prismatic,
        Distance,
        Pulley,
        Gear,
        Line,
        Weld,
        Friction,
        FixedMouse,
        FixedRevolute,
        FixedDistance,
        FixedLine,
        FixedPrismatic,
        MaxDistance,
        Angle,
        FixedAngle,
        FixedFriction
    }

    public enum LimitState
    {
        Inactive,
        AtLower,
        AtUpper,
        Equal,
    }


    public enum JointUse
    {
        Embedded,  //holds embedded item like bullet stuck inside
        Arm,
        Shoulder,
        Hip,    
    }

    internal struct Jacobian
    {
        public float AngularA;
        public float AngularB;
        public Vector2 LinearA;
        public Vector2 LinearB;

        public void SetZero()
        {
            LinearA = Vector2.Zero;
            AngularA = 0.0f;
            LinearB = Vector2.Zero;
            AngularB = 0.0f;
        }

        public void Set(Vector2 x1, float a1, Vector2 x2, float a2)
        {
            LinearA = x1;
            AngularA = a1;
            LinearB = x2;
            AngularB = a2;
        }

        public float Compute(Vector2 x1, float a1, Vector2 x2, float a2)
        {
            return Vector2.Dot(LinearA, x1) + AngularA * a1 + Vector2.Dot(LinearB, x2) + AngularB * a2;
        }
    }

    /// <summary>
    /// A joint edge is used to connect bodies and joints together
    /// in a joint graph where each body is a node and each joint
    /// is an edge. A joint edge belongs to a doubly linked list
    /// maintained in each attached body. Each joint has two joint
    /// nodes, one for each attached body.
    /// </summary>
    public sealed class JointEdge
    {
        /// <summary>
        /// The joint.
        /// </summary>
        public Joint Joint;

        /// <summary>
        /// The next joint edge in the body's joint list.
        /// </summary>
        public JointEdge Next;

        /// <summary>
        /// Provides quick access to the other body attached.
        /// </summary>
        public Body Other;

        /// <summary>
        /// The previous joint edge in the body's joint list.
        /// </summary>
        public JointEdge Prev;
    }

    [DataContract(Name = "Joint", Namespace = "http://ShadowPlay", IsReference = true)]
    [KnownType(typeof(RevoluteJoint))]
    [KnownType(typeof(AngleJoint))]
    [KnownType(typeof(FixedRevoluteJoint))]
    [KnownType(typeof(WeldJoint))]
    [KnownType(typeof(PoweredJoint))]
    public abstract class Joint : INotifyPropertyChanged  //shadowplay mod for viewing joint props
    {
        internal JointEdge EdgeA;
        internal JointEdge EdgeB;
        protected float InvIA;
        protected float InvIB;
        protected float InvMassA;
        protected float InvMassB;
        internal bool IslandFlag;
        protected Vector2 LocalCenterA, LocalCenterB;

        #region ShadowPlay Mods:
        protected float _jointError; //cache it so we can report it on Validate
        
 

        /// <summary>
        /// This allows  the Joint to have a lower setting that the global, allows us to relax individual joints.
        /// </summary>
        [DataMember]
        public int MaxVelocityIterations { get; set; }
        [DataMember]
        public int MaxPositionIterations { get; set; }
        #endregion

        protected Joint(Body body, Body bodyB)
        {
            Debug.Assert(body != bodyB);

            BodyA = body;
            EdgeA = new JointEdge();
            BodyB = bodyB;
            EdgeB = new JointEdge();

            //Connected bodies should not collide by default
            CollideConnected = false;
        }

        /// <summary>
        /// Constructor for fixed joint
        /// </summary>
        protected Joint(Body body)
        {
            BodyA = body;
            //Connected bodies should not collide by default
            CollideConnected = false;
            EdgeA = new JointEdge();
        }

        /// <summary>
        /// Gets or sets the type of the joint.
        /// </summary>
        /// <value>The type of the joint.</value>
        [DataMember(Order = 3)]
        public JointType JointType { get; set; }

        /// <summary>
        /// Get the first body attached to this joint.
        /// </summary>
        /// <value></value>
        [DataMember(Order = 0)]
        public Body BodyA { get; set; }

        /// <summary>
        /// Get the second body attached to this joint.
        /// </summary>
        /// <value></value>
        [DataMember(Order = 1)]
        public Body BodyB { get; set; }

        /// <summary>
        /// Get the anchor point on body1 in world coordinates.
        /// </summary>
        /// <value></value>
        public abstract Vector2 WorldAnchorA { get; }

        /// <summary>
        /// Get the anchor point on body2 in world coordinates.
        /// </summary>
        /// <value></value>
        [DataMember]  //TODO FUTURE remove this.. use only for FixedJoint like FixedRevoluteJoint NOTE i checked this.. cannot reload position of the Fixed revoluted joints, datamember wont work in virtual method
        public abstract Vector2 WorldAnchorB { get; set; }

        /// <summary>
        /// Set the user data pointer.
        /// </summary>
        /// <value>The data.</value>
        public object UserData { get; set; }

        [DataMember]
        public JointUse Usage { get; set; }

        /// <summary>
        /// Short-cut function to determine if either body is inactive.
        /// </summary>
        /// <value><c>true</c> if active; otherwise, <c>false</c>.</value>
        public bool Active
        {
            get { return BodyA != null && BodyA.Enabled && BodyB != null && BodyB.Enabled; }
        }



        public bool collideConnected { get; set; }  
        /// <summary>
        /// Set this flag to true if the attached bodies should collide.
        /// </summary>
        [DataMember(Order = 2)]
        public bool CollideConnected
        {

            get => collideConnected;

            set
            {
                if (value != collideConnected)

                {// FIX FOR AFTER TOGGLING THIS OFF AT RUNTIME ITS STILL CIN A COLLIDINATE STATE
                    value = collideConnected;

                    if (!_ondeserializing)
                    {

                        if (BodyA != null)
                            BodyA.ClearCollisionData();


                        if (BodyB != null)
                            BodyB.ClearCollisionData();
                    }
                }
            }
        }

        /// <summary>
        /// Fires when the joint is broken.
        /// </summary>
        public /*event*/ Action<Joint, float> Broke;

        /// <summary>
        /// Get the reaction force on body2 at the joint anchor in Newtons.
        /// </summary>
        /// <param name="inv_dt">The inv_dt.</param>
        /// <returns></returns>
        public abstract Vector2 GetReactionForce(float inv_dt);

        /// <summary>
        /// Get the reaction torque on body2 in N*m.
        /// </summary>
        /// <param name="inv_dt">The inv_dt.</param>
        /// <returns></returns>
        public abstract float GetReactionTorque(float inv_dt);


        
        // Shadowtool only dor prop sheets
        public float ReactionForce
        {
            get { return _jointError; }
        }

        //to see the amount of damping might be needed.
        public float ReactionTorque
        {
            get { return GetReactionTorque(1/ World.DT); }
        }



        protected void WakeBodies()
        {
            if (BodyA != null)
            {
                BodyA.Awake = true;
            }

            if (BodyB != null)
            {
                BodyB.Awake = true;
            }
        }

        /// <summary>
        /// Return true if the joint is a fixed type.
        /// </summary>
        public bool IsFixedType()
        {
            return JointType == JointType.FixedRevolute ||
                   JointType == JointType.FixedDistance ||
                   JointType == JointType.FixedPrismatic ||
                   JointType == JointType.FixedLine ||
                   JointType == JointType.FixedMouse ||
                   JointType == JointType.FixedAngle ||
                   JointType == JointType.FixedFriction;
        }

        internal abstract void InitVelocityConstraints(ref TimeStep step);

        internal virtual void Validate(float invDT)
        {
            if (Enabled == false)              
            {
                return;
            }

            _jointError = GetReactionForce(invDT).Length();

            if (Math.Abs(_jointError) <= _breakpoint)
                return;

            // check if allowed to break by event
            Break();// shadowplay mod.. factor out so it can be called from collision or other
            System.Diagnostics.Debug.WriteLine("Base Joint Broken: " + _jointError);
        }


        #region ShadowPlay Mods:

        //TODO replace wit a SoundEffect param
        public Nullable<bool> BreakQuietly = false;



        /// <summary>
        /// Common way to break joint programmatically if allowed
        /// </summary>
        /// <returns>True if broken </returns>
        public bool Break()
        {

            if (!Settings.IsJointBreakable)
                return false;

            if (Breaking != null && Breaking(this) == false)
                return false;
   
            // will only reach here if joint break
            Enabled = false;
            IsBroken = true;

            if (Broke != null)
            {
                Broke(this, _jointError);
            }

            return true;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            try
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
            catch (Exception ex)
            {
#if MONOTOUCH || SILVERLIGHT || UNIVERSAL
                System.Diagnostics.Debug.WriteLine("{0} \n{1}", ex.Message, ex.StackTrace);
#else
                System.Diagnostics.Trace.TraceError(ex.Message);
                System.Diagnostics.Trace.TraceError(ex.StackTrace);
#endif
            }
        }
        #endregion

        internal abstract void SolveVelocityConstraints(ref TimeStep step);

        /// <summary>
        /// Solves the position constraints.
        /// </summary>
        /// <returns>returns true if the position errors are within tolerance.</returns>
        internal abstract bool SolvePositionConstraints();


        #region ShadowPlay Mods:

        /// <summary>
        /// Flag to inform that we are in deserialization proses. Used by some 
        /// properties in derived class.
        /// Even though OnDeserialized and OnDeserializing methods can't be virtual, 
        /// those method always get called first at base class, then on derived class (if any).
        /// So state of this flag should be correct when used in derived class.
        /// </summary>
        protected bool _ondeserializing;

        private float _breakpoint = float.MaxValue;
        /// <summary>
        /// The Breakpoint simply indicates the maximum Value the JointError can be before it breaks.
        /// The default value is float.MaxValue
        /// </summary>
        [DataMember]
        public float Breakpoint
        {
            get { return _breakpoint; }
            set { _breakpoint = value; }
        }


        // method marked with OnDeserialized attribute cannot be virtual
        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            EdgeA = new JointEdge();
            EdgeB = new JointEdge();

            // temporary fix for loading old level saved from farseer 3.2 .
            // uncomment this after old level have been re-saved in new format.
            if (Breakpoint == 0)
                Breakpoint = float.MaxValue;

            _ondeserializing = false;
        }

        // method marked with OnDeserializing attribute cannot be virtual
        [OnDeserializing]
        public void OnDeserializing(StreamingContext sc)
        {
            _ondeserializing = true;
        }

        public virtual void ResetStateForTransferBetweenPhysicsWorld()
        {
            EdgeA = new JointEdge();
            EdgeB = new JointEdge();
            IslandFlag = false;
        }


        //stupidly the model is saved as worldAnchor.. it think..   dontknow if A or B
        /// <summary>
        /// given the body its attached to get the local point for this joint
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public Vector2 GetLocalAnchor( Body body)
        {
            //TODO check i think its always A.. if its in a bodies jointList  ( TODO check engine) 
            return body == BodyA ? body.GetLocalPoint(WorldAnchorA) 
                : body.GetLocalPoint(WorldAnchorB);
        }


        /// <summary>
        /// Fires when joint is about to break. Listener can return false to
        /// prevent break, with the exception on AttachPoint.Detach().
        /// </summary>
        public Func<Joint, bool > Breaking;

        private bool _enabled = true;
        /// <summary>
        /// Set if this joint is enabled or not.
        /// True = Will simulate the Joint constraint.
        /// False = Ignore Joint constraint.
        /// </summary>
        [DataMember]
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        private bool _isBroken = false;
        /// <summary>
        /// Check if this joint is broken.
        /// </summary>
        [DataMember(Order = 4)]     // should be deserialized after CollideConnected
        public bool IsBroken
        {
            get { return _isBroken; }
            set
            {
                _isBroken = value;

                // when joint is mark as broken, set it to collide connected bodies
                if (value == true)
                {
                    CollideConnected = true;
                }
            }
        }

        /// <summary>
        ///if true spirit body graph walker wont collect or traverse this.  Can be used to connect two graphs
        /// </summary>
        [DataMember]
        public bool SkipTraversal { get; set; }


        #endregion
    }
}