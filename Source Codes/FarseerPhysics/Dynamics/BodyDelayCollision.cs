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

#define USE_MARK_LOCK
//#define  ACCESS_LAST_FRAME   enable function to access Body postion from last frame  ( save xform)


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.ComponentModel;

using Microsoft.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common.PhysicsLogic;
using FarseerPhysics.Controllers;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;



#region ShadowPlay Mods
using UndoRedoFramework;
using FarseerPhysics.Dynamics.Particles;
using System.Threading;
#endregion



namespace FarseerPhysics.Dynamics
{
    /// <summary>
    /// The body type.
    /// </summary>
    public enum BodyType
    {
        /// <summary>
        /// Zero velocity, may be manually moved. Note: even static bodies have mass.
        /// </summary>
        Static,
        /// <summary>
        /// Zero mass, non-zero velocity set by user, moved by solver
        /// </summary>
        Kinematic,
        /// <summary>
        /// Positive mass, non-zero velocity determined by forces, moved by solver
        /// </summary>
        Dynamic,
        // TODO_ERIN
        //b2_bulletBody,
    }

    [Flags]
    public enum BodyFlags
    {
        None = 0,
        Island = (1 << 0),
        Awake = (1 << 1),
        AutoSleep = (1 << 2),
        Bullet = (1 << 3),
        FixedRotation = (1 << 4),
        Enabled = (1 << 5),
        Toi = (1 << 6),
        IgnoreGravity = (1 << 7),

        //Shadowplay mod  use this flag to tell application body is used up.
        IsSpent = (1 << 8),
        LastBullet = (1 << 9)
    }

    /// <summary>
    /// A Rigid body.
    /// DataMember with Order 0-6 are similar to order in Body(World) constructor.  Since some of the model data is built of other this  in imported
    // Other DataMember must appears after Order 6, any number above 6 is ok.
    // Order 99 means must appear after Order 6, but without strict ordering.
    //  NOTE changing order does have unexpected consequences.. must be careful if needing to do this, better to avoid it
    /// </summary>
    [DataContract(Name = "Body", Namespace = "http://ShadowPlay", IsReference = true)]
    [KnownType(typeof(Particle))]
    public class Body : IEntity, INotifyPropertyChanged //, ICloneable
    {
        private static int _bodyIdCounter;
        internal float AngularVelocityInternal;

        #region ShadowplayMods
        //public Action AppearanceChanged;  using this way hard to collect changes..


        const float _maxStuckParticleSizeBlood = 0.018f;  //for blood make smaller untill ellpse is ready  TODO remove this after ellipse
        const float _maxStuckParticleSize = 0.022f;  //make bigger for dust.. not sure if oval is good for that  blood..

        private bool _isShowingDress2;

        //if anther thread addiing a mark effect just skip it. since this for peformance dont use sync. any partlice effect can be 
        //TODO future maybe for stab wounds , should use the lock or other syc       
#if !USE_MARK_LOCK
        volatile public bool _markCollectingSkipped = false;  
#endif
       

        public bool IsShowingDress2
        {
            get
            {
                return _isShowingDress2;
            }
            set
            {
                // only set to true if dress 2 available. 
                // so body that didn't have dress 2 won't suddenly disappear when dress 2 activated.
                if (DressingGeom2 == null)
                {
                    value = false;
                }

                if (_isShowingDress2 != value)
                {
                    _isShowingDress2 = value;
                    NotifyPropertyChanged("IsShowingDress2");
                }
            }
        }


        #endregion

        [DataMember]
        public int BodyId;// merged in from latest farseer.. just a unique id.. ( might not be unique after insertong body from one level to another)

        public ControllerFilter ControllerFilter = new ControllerFilter();

        // internal  BodyFlags Flags;   shadowplay mod below .. make external so client can use them
        public BodyFlags Flags;

        #region ShadowPlay Mods..
        internal protected Vector2 Force; //will be cleared after step
        private float _area;
        private float _perimeter;

        public const float MinAccel = 1.10f;  //TUNED for stuff resting on ground.. stuff wont budge anyways unless accel is more than this.
        public const float MinAccelSq = MinAccel * MinAccel;

        public Vector2 LinearVelocityPreviousFrame; //Note this is only valid after one frame.. on emitted we calculate via force
        public Vector2 Acceleration = Vector2.Zero;

        ///Flags containing metadata, special information so that bodies in different use can be treated differently in low level pnysics code

        public BodyInfo _info;
        public static bool NotCreateFixtureOnDeserialize = false;  //for loading items just for dress . 

        [DataMember]
        public BodyInfo Info
        {
            get { return _info; }
            set
            {
                _info = value;
            }
        }

#if !PRODUCTION
        public Vector2 LastTotalForce; //shadowplay mod..  this will not show gravity..
#endif

        //private Vector2 _averageVel;
        #endregion

        internal float InvI;
        public float InvMass;  //shadowPlay mod expose this
        internal Vector2 LinearVelocityInternal;
        public PhysicsLogicFilter PhysicsLogicFilter = new PhysicsLogicFilter();
        internal float SleepTime;
        internal Sweep Sweep; // the swept motion for CCD
        internal float Torque;
        internal World World;
        internal Transform Xf; // the body origin transform
        private BodyType _bodyType;
        private float _inertia;

        #region ShadowPlay Mods

        private UndoRedo<float> _mass;  // original: private float _mass;

#if ACCESS_LAST_FRAME   
       shadowplay Mod internal Transform XfLastFrame; // the body origin transform.. last frame .. could be used for onCollide, or put a Report before the Contacts are solved and applied to the body state
#endif
        #endregion

        public Body(World world)
            : this(world, null)
        {
        }

        public Body(World world, Object userData)
        {

            //TODO future.. le..
            //however force on blowing particles requires mass data which requires one fixture.. its approximated for clouds, using AABB on deserialize
            // having smaller class or pool of particles will be faster ( less allocation, garbage cllection
            if (!(this is Particle))
            {
                FixtureList = new List<Fixture>(32);
            }
            else
            {
                FixtureList = new List<Fixture>(1); //only one needed for  circle used for mass data of circular particle
            }

            BodyId = _bodyIdCounter++;

            World = world;
            UserData = userData;

            FixedRotation = false;
            IsBullet = false;
            SleepingAllowed = true;
            Awake = true;
            BodyType = BodyType.Static;
            Enabled = true;

            Xf.R.Set(0);

            world.AddBody(this);

            //shadowplay Mod.. the default for all entities 
            DragCoefficient = 0.4f;
        }

        /// <summary>
        /// Gets the total number revolutions the body has made.
        /// </summary>
        /// <value>The revolutions.</value>
        public float Revolutions
        {
            get { return Rotation / (float)Math.PI; }
        }

        /// <summary>
        /// Gets or sets the body type.
        /// </summary>
        /// <value>The type of body.</value>
        [DataMember(Order = 5)]
        public BodyType BodyType
        {
            get { return _bodyType; }
            set
            {
                if (_bodyType == value)
                    return;

                _bodyType = value;
                ResetMassData();

                if (_bodyType == BodyType.Static)
                {
                    LinearVelocityInternal = Vector2.Zero;
                    AngularVelocityInternal = 0.0f;
                }

                Awake = true;

                Force = Vector2.Zero;
                Torque = 0.0f;

                // Since the body type changed, we need to flag contacts for filtering.
                for (ContactEdge ce = ContactList; ce != null; ce = ce.Next)
                {
                    ce.Contact.FlagForFiltering();
                }
            }
        }

        /// <summary>
        /// Get or sets the linear velocity of the center of mass.
        /// </summary>
        /// <value>The linear velocity.</value>
        [DataMember(Order = 99)]
        public Vector2 LinearVelocity
        {
            set
            {
                if (_bodyType == BodyType.Static)
                {
                    return;
                }

                if (Vector2.Dot(value, value) > 0.0f)
                {
                    Awake = true;
                }

                LinearVelocityInternal = value;
            }
            get { return LinearVelocityInternal; }
        }

        /// <summary>
        /// Gets or sets the angular velocity. Radians/second.
        /// </summary>
        /// <value>The angular velocity.</value>
        [DataMember(Order = 99)]
        public float AngularVelocity
        {
            set
            {
                if (_bodyType == BodyType.Static)
                {
                    return;
                }

                if (value * value > 0.0f)
                {
                    Awake = true;
                }

                AngularVelocityInternal = value;
            }
            get { return AngularVelocityInternal; }
        }

        /// <summary>
        /// Gets or sets the linear damping.
        /// </summary>
        /// <value>The linear damping.</value>
        [DataMember]
        public float LinearDamping { get; set; }

        /// <summary>
        /// Gets or sets the angular damping.
        /// </summary>
        /// <value>The angular damping.</value>
        [DataMember]
        public float AngularDamping { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this body should be included in the CCD solver.
        /// </summary>
        /// <value><c>true</c> if this instance is included in CCD; otherwise, <c>false</c>.</value>
        [DataMember(Order = 2)]
        public bool IsBullet
        {
            set
            {
                if (value)
                {
                    Flags |= BodyFlags.Bullet;
                }
                else
                {
                    Flags &= ~BodyFlags.Bullet;
                }
            }
            get { return (Flags & BodyFlags.Bullet) == BodyFlags.Bullet; }
        }

        /// <summary>
        /// You can disable sleeping on this body. If you disable sleeping, the
        /// body will be woken.
        /// </summary>
        /// <value><c>true</c> if sleeping is allowed; otherwise, <c>false</c>.</value>
        [DataMember(Order = 3)]
        public bool SleepingAllowed
        {
            set
            {
                if (value)
                {
                    Flags |= BodyFlags.AutoSleep;
                }
                else
                {
                    Flags &= ~BodyFlags.AutoSleep;
                    Awake = true;
                }
            }
            get { return (Flags & BodyFlags.AutoSleep) == BodyFlags.AutoSleep; }
        }

        /// <summary>
        /// Set the sleep state of the body. A sleeping body has very
        /// low CPU cost.
        /// </summary>
        /// <value><c>true</c> if awake; otherwise, <c>false</c>.</value>
        [DataMember(Order = 4)]
        public bool Awake
        {
            set
            {
                if (value)
                {
                    if ((Flags & BodyFlags.Awake) == 0)
                    {
                        Flags |= BodyFlags.Awake;
                        SleepTime = 0.0f;

                        #region ShadowPlay Mods
#if !PRODUCTION
                        NotifyPropertyChanged("Awake");
#endif
                        #endregion

                        if ((Info & BodyInfo.DebugThis) != 0)
                        {
                            Debug.WriteLine("wokeup");
                        }
                    }
                }
                else
                {
                    Flags &= ~BodyFlags.Awake;
                    SleepTime = 0.0f;
                    LinearVelocityInternal = Vector2.Zero;
                    AngularVelocityInternal = 0.0f;
                    Force = Vector2.Zero;
                    Torque = 0.0f;

                    #region ShadowPlay Mods
#if !PRODUCTION
                    NotifyPropertyChanged("Awake");
#endif
                    #endregion
                }
            }
            get { return (BodyType != Dynamics.BodyType.Static) && (Flags & BodyFlags.Awake) == BodyFlags.Awake; }  //shadowplay mod merged from 87727  
        }

        /// <summary>
        /// Set the active state of the body. An inactive body is not
        /// simulated and cannot be collided with or woken up.
        /// If you pass a flag of true, all fixtures will be added to the
        /// broad-phase.
        /// If you pass a flag of false, all fixtures will be removed from
        /// the broad-phase and all contacts will be destroyed.
        /// Fixtures and joints are otherwise unaffected. You may continue
        /// to create/destroy fixtures and joints on inactive bodies.
        /// Fixtures on an inactive body are implicitly inactive and will
        /// not participate in collisions, ray-casts, or queries.
        /// Joints connected to an inactive body are implicitly inactive.
        /// An inactive body is still owned by a b2World object and remains
        /// in the body list.
        /// </summary>
        /// <value><c>true</c> if active; otherwise, <c>false</c>.</value>
        [DataMember (Name="Active")]  //TODO get rigth of this order stuff.. add to broad phase after level load.
        public bool Enabled
        {
            set
            {
                if (value == Enabled)
                    return;

                if (_isDeserializing)
                    return;

                SetCollision(value);
            }
            get { return (Flags & BodyFlags.Enabled) == BodyFlags.Enabled; }
        }

        private void SetCollision(bool value)
        {
            if (value)
            {
                // Contacts are created the next time step.
                EnableCollision();  //shadowplay mod.. extracted method                  
            }
            else
            {
                DisableCollsion();
            }
        }

        private void DisableCollsion()
        {
            Flags &= ~BodyFlags.Enabled;

            // Destroy all proxies.
            BroadPhase broadPhase = World.ContactManager.BroadPhase;

            for (int i = 0; i < FixtureList.Count; i++)
            {
                FixtureList[i].DestroyProxies(broadPhase);
            }

            // Destroy the attached contacts.
            ContactEdge ce = ContactList;
            while (ce != null)
            {
                ContactEdge ce0 = ce;
                ce = ce.Next;
                World.ContactManager.Destroy(ce0.Contact);
            }
            ContactList = null;
        }

        private void EnableCollision()
        {
            Flags |= BodyFlags.Enabled;
            if (!(IsNotCollideable || FixtureList == null))//shadowplay mods
            {
                // Create all proxies.
                BroadPhase broadPhase = World.ContactManager.BroadPhase;
                for (int i = 0; i < FixtureList.Count; i++)
                {
                    FixtureList[i].CreateProxies(broadPhase, ref Xf);
                }
                return;
            }
            else
            {
                return;
            }
        }



        public void AddToBroadPhase()
        {

        }

        /// <summary>
        /// Set this body to have fixed rotation. This causes the mass
        /// to be reset.
        /// </summary>
        /// <value><c>true</c> if it has fixed rotation; otherwise, <c>false</c>.</value>
        [DataMember(Order = 1)]
        public bool FixedRotation
        {
            set
            {
                if (value)
                {
                    Flags |= BodyFlags.FixedRotation;
                }
                else
                {
                    Flags &= ~BodyFlags.FixedRotation;
                }

                ResetMassData();
            }
            get { return (Flags & BodyFlags.FixedRotation) == BodyFlags.FixedRotation; }
        }

        private List<Fixture> _fixtureList;
        /// <summary>
        /// Gets all the fixtures attached to this body.
        /// </summary>
        /// <value>The fixture list.</value>
        [DataMember(Order = 0)]
        public List<Fixture> FixtureList
        {
            get { return _fixtureList; }

            //internal set{}
            // for deserialization only, do not access
            set
            {
                // when in deserialization, must not assign null value
                if (_isDeserializing && value == null)
                    return;

                _fixtureList = value;
            }
        }

        // No need to serialize JointList, will be rebuilt when added into World
        /// <summary>
        /// Get the list of all joints attached to this body.
        /// </summary>
        /// <value>The joint list.</value>
        public JointEdge JointList { get; internal set; }

        /// <summary>
        /// Get the list of all contacts attached to this body.
        /// Warning: this list changes during the time step and you may
        /// miss some collisions if you don't use ContactListener.
        /// </summary>
        /// <value>The contact list.</value>
        public ContactEdge ContactList { get; internal set; }

        /// <summary>
        /// Set the user data. Use this to store your application specific data.
        /// </summary>
        /// <value>The user data.</value>
        public object UserData { get; set; }

        /// <summary>
        /// Get the world body origin position.
        /// </summary>
        /// <returns>Return the world position of the body's origin.</returns>
        [DataMember(Order = 7)]
        public Vector2 Position
        {
            get { return Xf.Position; }
            set { SetTransform(ref value, Rotation); }
        }


        /// <summary>
        /// Get the angle in radians.
        /// </summary>
        /// <returns>Return the current world rotation angle in radians.</returns>
        [DataMember(Order = 8)]   //TODO remove this serialization tag...but then we have to resave old levels..anyways Angle will override any wound values  since this order is after.  ( I verified this) 
        public float Rotation
        {
            get { return Sweep.A; }
            set { SetTransform(ref Xf.Position, value); }
        }


        /// <summary>
        /// Rotation from 0 to 2pi radians, 0 is vert
        /// </summary>
        //[DataMember(Order = 9)]   TODO put this back, fix unwinding in SetTarget again, retest namiad.wyg..
        public float Angle
        {
            get { return MathHelper.WrapAnglePositive(Rotation); }
            set { Rotation = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this body is static.
        /// </summary>
        /// <value><c>true</c> if this instance is static; otherwise, <c>false</c>.</value>
        [DataMember]
        public bool IsStatic
        {
            get { return _bodyType == BodyType.Static; }
            set
            {
                if (value)
                    BodyType = BodyType.Static;
                else
                    BodyType = BodyType.Dynamic;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this body ignores gravity.
        /// </summary>
        /// <value><c>true</c> if  it ignores gravity; otherwise, <c>false</c>.</value>
        [DataMember]
        public bool IgnoreGravity
        {
            get { return (Flags & BodyFlags.IgnoreGravity) == BodyFlags.IgnoreGravity; }
            set
            {
                if (value)
                    Flags |= BodyFlags.IgnoreGravity;
                else
                    Flags &= ~BodyFlags.IgnoreGravity;
            }
        }

        /// <summary>
        /// Get the world position of the center of mass.
        /// </summary>
        /// <value>The world position.</value>
        public Vector2 WorldCenter
        {
            get { return Sweep.C; }
        }

        /// <summary>
        /// Get the local position of the center of mass.
        /// </summary>
        /// <value>The local position.</value>
        [DataMember(Order = 99)]
        public Vector2 LocalCenter
        {
            get { return Sweep.LocalCenter; }
            set
            {
                if (_bodyType != BodyType.Dynamic)
                {
                    return;
                }

                // Move center of mass.
                Vector2 oldCenter = Sweep.C;
                Sweep.LocalCenter = value;
                Sweep.C0 = Sweep.C = MathUtils.Multiply(ref Xf, ref Sweep.LocalCenter);

                // Update center of mass velocity.
                Vector2 a = Sweep.C - oldCenter;
                LinearVelocityInternal += new Vector2(-AngularVelocityInternal * a.Y, AngularVelocityInternal * a.X);
            }
        }

        /// <summary>
        /// Gets or sets the mass. Usually in kilograms (kg).
        /// </summary>
        /// <value>The mass.</value>
        [DataMember(Order = 99)]
        public float Mass
        {
            #region ShadowPlay Mods

            get
            {
                if (_mass == null)
                {
                    _mass = CreateUndoableMember<float>(_mass_UndoRedoChanged);
                }
                return _mass.ValueUndoable;
            }
            set
            {
                if (_bodyType != BodyType.Dynamic)
                {
                    return;
                }

                if (_mass == null)
                {
                    _mass = CreateUndoableMember<float>(_mass_UndoRedoChanged);
                }

                _mass.ValueUndoable = value;
                if (_mass.ValueUndoable <= 0.0f)
                {
                    _mass.ValueUndoable = 1.0f;
                }

                InvMass = 1.0f / _mass.ValueUndoable;

                NotifyPropertyChanged("Mass");
            }


            #endregion

#if ORIGINAL_FARSEER_3_2
            get
            {
                return _mass; 
            }
            set
            {
                if (_bodyType != BodyType.Dynamic)
                {
                    return;
                }

                _mass = value;
                if (_mass <= 0.0f)
                {
                    _mass = 1.0f;
                }

                InvMass = 1.0f/_mass;

                NotifyPropertyChanged("Mass");
            }
#endif
        }




        void _mass_UndoRedoChanged(object sender, UndoRedoChangedType type, float oldState, float newState)
        {
            InvMass = 1.0f / Mass;
            NotifyPropertyChanged("Mass");
        }


        //ShadowPlay Mods  added.. so we can set Density , due to mass ratio..
        /// <summary>
        /// The area of all the fixtures added up.  Note won't work if density is zero..
        /// </summary>
        public float Area
        {
            get { return _area; }
        }


        /// <summary>
        /// Like perimeter ,except aviod Square root .. will be use to see if a face is a significant to the whole perim on small objects in wind.
        /// </summary>
        public float PerimeterOfEdges
        {
            get
            {
                if (_perimeter == 0)
                {
                    CalculatePerimeter();
                }
                return _perimeter;
            }
        }

        private void CalculatePerimeter()
        {
            _perimeter = 0;

            Vertices vertices = GeneralVertices;

            if (vertices == null) //for circle its 2pi 2 but we dont need it.
                return;

            for (int i = 0; i < vertices.Count; ++i)
            {
                int i1 = i;
                int i2 = i + 1 < vertices.Count ? i + 1 : 0;
                Vector2 edge = vertices[i2] - vertices[i1];
                _perimeter += edge.Length();  //TODO future consider approx lenght without sq root?  test speed of square root 
            }
        }



        /// <summary>
        /// Get or set the rotational inertia of the body about the local origin. usually in kg-m^2.
        /// </summary>
        /// <value>The inertia.</value>
        public float Inertia
        {
            get { return _inertia + Mass * Vector2.Dot(Sweep.LocalCenter, Sweep.LocalCenter); }
            set
            {
                if (_bodyType != BodyType.Dynamic)
                {
                    return;
                }

                if (value > 0.0f && (Flags & BodyFlags.FixedRotation) == 0)
                {
                    _inertia = value - Mass * Vector2.Dot(LocalCenter, LocalCenter);
                    Debug.Assert(_inertia > 0.0f);
                    InvI = 1.0f / _inertia;
                }
            }
        }

        #region IDisposable Members

    

        #endregion

        /// <summary>
        /// Resets the dynamics of this body.
        /// Sets torque, force and linear/angular velocity to 0
        /// </summary>
        public void ResetDynamics()
        {
            Torque = 0;
            AngularVelocityInternal = 0;
            Force = Vector2.Zero;
            LinearVelocityInternal = Vector2.Zero;
        }

        /// <summary>
        /// Creates a fixture and attach it to this body.
        /// If the density is non-zero, this function automatically updates the mass of the body.
        /// Contacts are not created until the next time step.
        /// Warning: This function is locked during callbacks.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <returns></returns>
        public Fixture CreateFixture(Shape shape)
        {
            return new Fixture(this, shape);
        }

        /// <summary>
        /// Creates a fixture and attach it to this body.
        /// If the density is non-zero, this function automatically updates the mass of the body.
        /// Contacts are not created until the next time step.
        /// Warning: This function is locked during callbacks.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="userData">Application specific data</param>
        /// <returns></returns>
        public Fixture CreateFixture(Shape shape, Object userData)
        {
            return new Fixture(this, shape, userData);
        }

        /// <summary>
        /// Destroy a fixture. This removes the fixture from the broad-phase and
        /// destroys all contacts associated with this fixture. This will
        /// automatically adjust the mass of the body if the body is dynamic and the
        /// fixture has positive density.
        /// All fixtures attached to a body are implicitly destroyed when the body is destroyed.
        /// Warning: This function is locked during callbacks.
        /// </summary>
        /// <param name="fixture">The fixture to be removed.</param>
        public void DestroyFixture(Fixture fixture)
        {
            Debug.Assert(fixture.Body == this);

            // Remove the fixture from this body's singly linked list.
            Debug.Assert(FixtureList.Count > 0);

#if DEBUG
            // You tried to remove a fixture that not present in the fixturelist.
            Debug.Assert(FixtureList.Contains(fixture));
#endif

            // Destroy any contacts associated with the fixture.
            ContactEdge edge = ContactList;
            while (edge != null)
            {
                Contact c = edge.Contact;
                edge = edge.Next;

                Fixture fixtureA = c.FixtureA;
                Fixture fixtureB = c.FixtureB;

                if (fixture == fixtureA || fixture == fixtureB)
                {
                    // This destroys the contact and removes it from
                    // this body's contact list.
                    World.ContactManager.Destroy(c);
                }
            }

            if ((Flags & BodyFlags.Enabled) == BodyFlags.Enabled)
            {
                BroadPhase broadPhase = World.ContactManager.BroadPhase;
                fixture.DestroyProxies(broadPhase);
            }

            FixtureList.Remove(fixture);
            fixture.Destroy();
            fixture.Body = null;

            ResetMassData();
        }

        /// <summary>
        /// Set the position of the body's origin and rotation.
        /// This breaks any contacts and wakes the other bodies.
        /// Manipulating a body's transform may cause non-physical behavior.
        /// </summary>
        /// <param name="position">The world position of the body's local origin.</param>
        /// <param name="rotation">The world rotation in radians.</param>
        public void SetTransform(ref Vector2 position, float rotation)
        {
            SetTransformIgnoreContacts(ref position, rotation);

            if (!IsNotCollideable) //   shadowplay mods
            {
                World.ContactManager.FindNewContacts();
            }
        }

        /// <summary>
        /// Set the position of the body's origin and rotation.
        /// This breaks any contacts and wakes the other bodies.
        /// Manipulating a body's transform may cause non-physical behavior.
        /// </summary>
        /// <param name="position">The world position of the body's local origin.</param>
        /// <param name="rotation">The world rotation in radians.</param>
        public void SetTransform(Vector2 position, float rotation)
        {
            SetTransform(ref position, rotation);
        }

        /// <summary>
        /// For teleporting a body without considering new contacts immediately.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="angle">The angle.</param>
        public void SetTransformIgnoreContacts(ref Vector2 position, float angle)
        {
            Xf.R.Set(angle);
            Xf.Position = position;

            Sweep.C0 =
                Sweep.C =
                new Vector2(Xf.Position.X + Xf.R.Col1.X * Sweep.LocalCenter.X + Xf.R.Col2.X * Sweep.LocalCenter.Y,
                            Xf.Position.Y + Xf.R.Col1.Y * Sweep.LocalCenter.X + Xf.R.Col2.Y * Sweep.LocalCenter.Y);
            Sweep.A0 = Sweep.A = angle;

            BroadPhase broadPhase = World.ContactManager.BroadPhase;

            if (FixtureList == null)//shadowplay mod
                return;

            for (int i = 0; i < FixtureList.Count; i++)
            {
                FixtureList[i].Synchronize(broadPhase, ref Xf, ref Xf);
            }

        }

        /// <summary>
        /// Get the body transform for the body's origin.
        /// </summary>
        /// <param name="transform">The transform of the body's origin.</param>
        public void GetTransform(out Transform transform)
        {
            transform = Xf;
        }

        /// <summary>
        /// Apply a force at a world point. If the force is not
        /// applied at the center of mass, it will generate a torque and
        /// affect the angular velocity. This wakes up the body.
        /// </summary>
        /// <param name="force">The world force vector, usually in Newtons (N).</param>
        /// <param name="point">The world position of the point of application.</param>
        public void ApplyForce(Vector2 force, Vector2 point)
        {
            ApplyForce(ref force, ref point);
        }

        /// <summary>
        /// Applies a force at the center of mass.
        /// </summary>
        /// <param name="force">The force.</param>
        public void ApplyForce(ref Vector2 force)
        {
            ApplyForce(ref force, ref Xf.Position);
        }

        /// <summary>
        /// Applies a force at the center of mass.
        /// </summary>
        /// <param name="force">The force.</param>
        public void ApplyForce(Vector2 force)
        {
            ApplyForce(ref force, ref Xf.Position);
        }

        /// <summary>
        /// Apply a force at a world point. If the force is not
        /// applied at the center of mass, it will generate a torque and
        /// affect the angular velocity. This wakes up the body.
        /// </summary>
        /// <param name="force">The world force vector, usually in Newtons (N).</param>
        /// <param name="point">The world position of the point of application.</param>
        public void ApplyForce(ref Vector2 force, ref Vector2 point)
        {
            if (_bodyType != BodyType.Dynamic)//shadow play mod
                return;

            float accelSq = force.LengthSquared() / (Mass * Mass);
            float accel = force.Length() / (Mass);

            //if ((Info & BodyInfo.DebugThis) != 0)
            //{
            //    Debug.WriteLine(accelSq.ToString() + "  accelSq " + accelSq.ToString());
            //}

            if (accelSq < MinAccelSq) //TODO tune MinAccelSq   TODO do Torque separately..
            {
                //if ((Info & BodyInfo.DebugThis) != 0)
                //{
                // //   Debug.WriteLine( " too small accelSq " + accelSq.ToString());
                // //   Debug.WriteLine("MinAccelSq " + MinAccelSq.ToString()  );
                // //   Debug.WriteLine(accel.ToString() + " accel  " + accel.ToString());
                ////    Debug.WriteLine( "MinAccel " + MinAccel.ToString());           
                //    return; //don't wake if not going to budge it.// TODO check torque separately.. 
                //}
                return;
            }

            if (Awake == false)
            {
                Awake = true;
            }

            Force += force;
            Torque += (point.X - Sweep.C.X) * force.Y - (point.Y - Sweep.C.Y) * force.X;

        }





        /// <summary>
        ///  check that impulse doesnt not exceed that which would cause a full stop in N  frames.
        /// </summary>
        /// <param name="force"></param>
        public void ApplyLinearDragImpulse(ref Vector2 impulse)
        {
            const float nFrames = 1f;

            if (_bodyType != BodyType.Dynamic)
                return;

            Vector2 dVelImpulse = impulse * InvMass;

            if (dVelImpulse.LengthSquared() < MinAccelSq)     // shadowplay Mod, used for wind only..  if not going to budge it visibly let it rest
                return;   //don't even  wake

            //TODO consider moving this to wind drag , and use for both Force 
            Vector2 dVel = new Vector2(
            Math.Sign(impulse.X) * Math.Min(Math.Abs(impulse.X * InvMass), Math.Abs(LinearVelocityInternal.X * InvMass / nFrames)),
            Math.Sign(impulse.Y) * Math.Min(Math.Abs(impulse.Y * InvMass), Math.Abs(LinearVelocityInternal.Y * InvMass / nFrames)));

#if DEBUG
            if (dVel != impulse* InvMass)
            {
             //   Debug.WriteLine("dead stop in Drag, impulse clipped");
            }
#endif
            LinearVelocityInternal += dVel;

        }
        //shadowplay mod, added.. '

        //TODO this seems to not work.. somehow clouds still dont move all taht the same speed due to this.
        // will have to revisit laster .  Then test flag and other extreme air density levels..
        /// <summary>
        ///clip force to that which would bring body to  force field speed in N frames..  (  used in winddrag for stablity in case of wind drag large thin bodies, where coefficient > 1
        /// </summary>
        /// <param name="force"></param>
        /// <param name="dt"></param>
        /// <param name="relAirVelocity"></param>
        /// <returns>limited force</returns>
        public Vector2 LimitAirForceField(ref Vector2 force, float dt, Vector2 relAirVelocity)
        {
            const float nFrames = 2f;
            Vector2 impulse = force * dt;

            Vector2 dVelImpulse = impulse * InvMass;  //rate of change of velocity due to this impluse
            //find out the change in Vel, don't let it go over input vel.

            //find out the max change in vel to bring this to relAirVelocity in nFrames.. avoid errors such as coming to dead stop too fast and going backward..
            Vector2 dVelLimited = new Vector2(Math.Sign(impulse.X) * Math.Min(Math.Abs(dVelImpulse.X), Math.Abs(relAirVelocity.X * InvMass / nFrames)),
             Math.Sign(impulse.Y) * Math.Min(Math.Abs(dVelImpulse.Y), Math.Abs(relAirVelocity.Y * InvMass / nFrames)));

            Vector2 dVelImpulseClipped = dVelLimited * Mass;

            Vector2 forceClipped = dVelImpulseClipped / dt;
#if DEBUG
            const float tol = 0.0001f;

            if ((forceClipped - force).LengthSquared() > tol)
            {
             //   Debug.WriteLine(" force clipped");
            }
#endif

            return forceClipped;
        }


        /// <summary>
        /// Apply a torque. This affects the angular velocity
        /// without affecting the linear velocity of the center of mass.
        /// This wakes up the body.
        /// </summary>
        /// <param name="torque">The torque about the z-axis (out of the screen), usually in N-m.</param>
        public void ApplyTorque(float torque)
        {
            if (_bodyType == BodyType.Dynamic)
            {
                if (Awake == false)
                {
                    Awake = true;
                }

                Torque += torque;
            }
        }

        /// <summary>
        /// Apply an impulse at a point. This immediately modifies the velocity.
        /// This wakes up the body.
        /// </summary>
        /// <param name="impulse">The world impulse vector, usually in N-seconds or kg-m/s.</param>
        public void ApplyLinearImpulse(Vector2 impulse)
        {
            ApplyLinearImpulse(ref impulse);
        }

        /// <summary>
        /// Apply an impulse at a point. This immediately modifies the velocity.
        /// It also modifies the angular velocity if the point of application
        /// is not at the center of mass.
        /// This wakes up the body.
        /// </summary>
        /// <param name="impulse">The world impulse vector, usually in N-seconds or kg-m/s.</param>
        /// <param name="point">The world position of the point of application.</param>
        public void ApplyLinearImpulse(Vector2 impulse, Vector2 point)
        {
            ApplyLinearImpulse(ref impulse, ref point);
        }



        /// <summary>
        /// just check that force doesnt not exceed that which would cause a full stop in one frame.
        /// </summary>
        /// <param name="force"></param>
        //     public void ApplyDragForce(ref Vector2 force)
        //      {
        //          Vector2 dVel = force  * (float)_dt; //impulse is force //TODO ( dT / invMass?   check units of  accel and impulse definition wiki
        //           ApplyLinearDragImpulse(ref dVel);   everey frame vel = step.dt*(b.InvMass*b.Force.Y);    or  impulse is  InvMass * impulse;
        //        }

        /// <summary>
        /// Apply an impulse at a point. This immediately modifies the velocity.
        /// This wakes up the body.
        /// </summary>
        /// <param name="impulse">The world impulse vector, usually in N-seconds or kg-m/s.</param>
        public void ApplyLinearImpulse(ref Vector2 impulse)
        {
            if (_bodyType != BodyType.Dynamic)
                return;

            if ((impulse * InvMass).LengthSquared() < MinAccelSq)     // shadowplay Mod, used for wind only..  if not going to budge it visibly let it rest
                return;   //don't even  wake it up.

            if (Awake == false)
            {
                Awake = true;
            }

            LinearVelocityInternal += InvMass * impulse;
        }

        /// <summary>
        /// Apply an impulse at a point. This immediately modifies the velocity.
        /// It also modifies the angular velocity if the point of application
        /// is not at the center of mass.
        /// This wakes up the body.
        /// </summary>
        /// <param name="impulse">The world impulse vector, usually in N-seconds or kg-m/s.</param>
        /// <param name="point">The world position of the point of application.</param>
        public void ApplyLinearImpulse(ref Vector2 impulse, ref Vector2 point)
        {
            if (_bodyType != BodyType.Dynamic)
                return;

            if (Awake == false)
                Awake = true;

            LinearVelocityInternal += InvMass * impulse;
            AngularVelocityInternal += InvI * ((point.X - Sweep.C.X) * impulse.Y - (point.Y - Sweep.C.Y) * impulse.X);
        }

        /// <summary>
        /// Apply an angular impulse.
        /// </summary>
        /// <param name="impulse">The angular impulse in units of kg*m*m/s.</param>
        public void ApplyAngularImpulse(float impulse)
        {
            if (_bodyType != BodyType.Dynamic)
            {
                return;
            }

            if (Awake == false)
            {
                Awake = true;
            }

            AngularVelocityInternal += InvI * impulse;
        }

        /// <summary>
        /// This resets the mass properties to the sum of the mass properties of the fixtures.
        /// This normally does not need to be called unless you called SetMassData to override
        /// the mass and you later want to reset the mass.
        /// </summary>
        public void ResetMassData()
        {
            float m = Mass;
            // Compute mass data from shapes. Each shape has its own density.
            _mass.ValueUndoable = 0.0f;
            InvMass = 0.0f;
            _inertia = 0.0f;
            InvI = 0.0f;
            Sweep.LocalCenter = Vector2.Zero;

            _area = 0.0f; //shadowPlay Mod;

            // Kinematic bodies have zero mass.
            if (BodyType == BodyType.Kinematic)
            {
                Sweep.C0 = Sweep.C = Xf.Position;
                NotifyPropertyChanged("Mass");
                return;
            }

            Debug.Assert(BodyType == BodyType.Dynamic || BodyType == BodyType.Static);

            // Accumulate mass over all fixtures.
            Vector2 center = Vector2.Zero;

            if (FixtureList != null)
            {
                foreach (Fixture f in FixtureList)
                {

                    if (f.Shape._density == 0)
                    {
                        continue;
                    }

                    f.Shape.ComputeProperties();

                    MassData massData = f.Shape.MassData;
                    _mass.ValueUndoable = _mass.ValueUndoable + massData.Mass;
                    center += massData.Mass * massData.Centroid;
                    _inertia += massData.Inertia;

                    _area += f.Shape.MassData.Area;//shadowPlay Mod;
                }
            }

            //Static bodies only have mass, they don't have other properties. A little hacky tho...
            if (BodyType == BodyType.Static)
            {
                Sweep.C0 = Sweep.C = Xf.Position;
                NotifyPropertyChanged("Mass");
                return;
            }

            // Compute center of mass.
            if (_mass.ValueUndoable > 0.0f)
            {
                InvMass = 1.0f / _mass.ValueUndoable;
                center *= InvMass;
            }
            else
            {
                // Force all dynamic bodies to have a positive mass.
                _mass.ValueUndoable = 1.0f;
                InvMass = 1.0f;
            }

            if (_inertia > 0.0f && (Flags & BodyFlags.FixedRotation) == 0)
            {
                // Center the inertia about the center of mass.
                _inertia -= _mass.ValueUndoable * Vector2.Dot(center, center);

                #region Shadowplay Mod
                // quick hack to fix when _inertia < 0.   TODO CODE REVIEW Needed for Breakable body.. probably  due to winding of verts or something
                // if only commented out Assert(), will also throw assertion later in ContactSolver.InitializeVelocityConstraints
                if (_inertia < 0.0f)
                {
                    _inertia *= -1.0f;
                }
                #endregion


                Debug.Assert(_inertia > 0.0f);
                InvI = 1.0f / _inertia;
            }
            else
            {
                _inertia = 0.0f;
                InvI = 0.0f;
            }

            // Move center of mass.
            Vector2 oldCenter = Sweep.C;
            Sweep.LocalCenter = center;
            Sweep.C0 = Sweep.C = MathUtils.Multiply(ref Xf, ref Sweep.LocalCenter);

            // Update center of mass velocity.
            Vector2 a = Sweep.C - oldCenter;
            LinearVelocityInternal += new Vector2(-AngularVelocityInternal * a.Y, AngularVelocityInternal * a.X);
            NotifyPropertyChanged("Mass");
        }

        /// <summary>
        /// Get the world coordinates of a point given the local coordinates.
        /// </summary>
        /// <param name="localPoint">A point on the body measured relative the the body's origin.</param>
        /// <returns>The same point expressed in world coordinates.</returns>
        public Vector2 GetWorldPoint(ref Vector2 localPoint)
        {
            return new Vector2(Xf.Position.X + Xf.R.Col1.X * localPoint.X + Xf.R.Col2.X * localPoint.Y,
                               Xf.Position.Y + Xf.R.Col1.Y * localPoint.X + Xf.R.Col2.Y * localPoint.Y);
        }

        /// <summary>
        /// Get the world coordinates of a point given the local coordinates.
        /// </summary>
        /// <param name="localPoint">A point on the body measured relative the the body's origin.</param>
        /// <returns>The same point expressed in world coordinates.</returns>
        public Vector2 GetWorldPoint(Vector2 localPoint)
        {
            return GetWorldPoint(ref localPoint);
        }

        /// <summary>
        /// Get the world coordinates of a vector given the local coordinates.
        /// Note that the vector only takes the rotation into account, not the position.
        /// </summary>
        /// <param name="localVector">A vector fixed in the body.</param>
        /// <returns>The same vector expressed in world coordinates.</returns>
        public Vector2 GetWorldVector(ref Vector2 localVector)
        {
            return new Vector2(Xf.R.Col1.X * localVector.X + Xf.R.Col2.X * localVector.Y,
                               Xf.R.Col1.Y * localVector.X + Xf.R.Col2.Y * localVector.Y);
        }

        /// <summary>
        /// Get the world coordinates of a vector given the local coordinates.
        /// </summary>
        /// <param name="localVector">A vector fixed in the body.</param>
        /// <returns>The same vector expressed in world coordinates.</returns>
        public Vector2 GetWorldVector(Vector2 localVector)
        {
            return GetWorldVector(ref localVector);
        }

        /// <summary>
        /// Gets a local point relative to the body's origin given a world point.
        /// shadow play mod, fix comment.. it does take position into account.
        /// </summary>
        /// <param name="worldPoint">A point in world coordinates. Input param, the ref is supposed to pass faster on some platfroms</param>
        /// <returns>The corresponding local point relative to the body's CS origin, or Position is the origin in WCS</returns>
        public Vector2 GetLocalPoint(ref Vector2 worldPoint)
        {
            return new Vector2((worldPoint.X - Xf.Position.X) * Xf.R.Col1.X + (worldPoint.Y - Xf.Position.Y) * Xf.R.Col1.Y,
                               (worldPoint.X - Xf.Position.X) * Xf.R.Col2.X + (worldPoint.Y - Xf.Position.Y) * Xf.R.Col2.Y);
        }

        /// <summary>
        /// Gets a local point relative to the body's origin given a world point.
        /// </summary>
        /// <param name="worldPoint">A point in world coordinates.</param>
        /// <returns>The corresponding local point relative to the body's origin.</returns>
        public Vector2 GetLocalPoint(Vector2 worldPoint)
        {
            return GetLocalPoint(ref worldPoint);
        }


        #region ShadowPlay Mods
#if ACCESS_LAST_FRAME   //currently not used
        /// <summary>
        /// Like GetLocalPoint,  except using last frames postion and rotation.  This could be  useful OnCollided.. since object is moved already by solver.
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <returns></returns>
        public Vector2 GetLocalPointPreviousFrame(ref Vector2 worldPoint)
        {
            return new Vector2((worldPoint.X - XfLastFrame.Position.X) * XfLastFrame.R.Col1.X + (worldPoint.Y - XfLastFrame.Position.Y) * XfLastFrame.R.Col1.Y,
                               (worldPoint.X - XfLastFrame.Position.X) * XfLastFrame.R.Col2.X + (worldPoint.Y - XfLastFrame.Position.Y) * XfLastFrame.R.Col2.Y);
        }

#endif
        #endregion


        /// <summary>
        /// Gets a local vector given a world vector.
        /// Note that the vector only takes the rotation into account, not the position.
        /// </summary>
        /// <param name="worldVector">A vector in world coordinates.</param>
        /// <returns>The corresponding local vector.</returns>
        public Vector2 GetLocalVector(ref Vector2 worldVector)
        {
            return new Vector2(worldVector.X * Xf.R.Col1.X + worldVector.Y * Xf.R.Col1.Y,
                               worldVector.X * Xf.R.Col2.X + worldVector.Y * Xf.R.Col2.Y);
        }

        /// <summary>
        /// Gets a local vector given a world vector.
        /// Note that the vector only takes the rotation into account, not the position.
        /// </summary>
        /// <param name="worldVector">A vector in world coordinates.</param>
        /// <returns>The corresponding local vector.</returns>
        public Vector2 GetLocalVector(Vector2 worldVector)
        {
            return GetLocalVector(ref worldVector);
        }

        /// <summary>
        /// Get the world linear velocity of a world point attached to this body.
        /// </summary>
        /// <param name="worldPoint">A point in world coordinates.</param>
        /// <returns>The world velocity of a point.</returns>
        public Vector2 GetLinearVelocityFromWorldPoint(Vector2 worldPoint)
        {
            return GetLinearVelocityFromWorldPoint(ref worldPoint);
        }

        /// <summary>
        /// Get the world linear velocity of a world point attached to this body.
        /// </summary>
        /// <param name="worldPoint">A point in world coordinates.</param>
        /// <returns>The world velocity of a point.</returns>
        public Vector2 GetLinearVelocityFromWorldPoint(ref Vector2 worldPoint)
        {
            return LinearVelocityInternal +
                   new Vector2(-AngularVelocityInternal * (worldPoint.Y - Sweep.C.Y),
                               AngularVelocityInternal * (worldPoint.X - Sweep.C.X));
        }

        /// <summary>
        /// Get the world velocity of a local point.
        /// </summary>
        /// <param name="localPoint">A point in local coordinates.</param>
        /// <returns>The world velocity of a point.</returns>
        public Vector2 GetLinearVelocityFromLocalPoint(Vector2 localPoint)
        {
            return GetLinearVelocityFromLocalPoint(ref localPoint);
        }

        /// <summary>
        /// Get the world velocity of a local point.
        /// </summary>
        /// <param name="localPoint">A point in local coordinates.</param>
        /// <returns>The world velocity of a point.</returns>
        public Vector2 GetLinearVelocityFromLocalPoint(ref Vector2 localPoint)
        {
            return GetLinearVelocityFromWorldPoint(GetWorldPoint(ref localPoint));
        }

        internal void SynchronizeFixtures()
        {
            Transform xf1 = new Transform();
            float c = (float)Math.Cos(Sweep.A0), s = (float)Math.Sin(Sweep.A0);
            xf1.R.Col1.X = c;
            xf1.R.Col2.X = -s;
            xf1.R.Col1.Y = s;
            xf1.R.Col2.Y = c;

            xf1.Position.X = Sweep.C0.X - (xf1.R.Col1.X * Sweep.LocalCenter.X + xf1.R.Col2.X * Sweep.LocalCenter.Y);
            xf1.Position.Y = Sweep.C0.Y - (xf1.R.Col1.Y * Sweep.LocalCenter.X + xf1.R.Col2.Y * Sweep.LocalCenter.Y);

            BroadPhase broadPhase = World.ContactManager.BroadPhase;

            for (int i = 0; i < FixtureList.Count; i++)
            {
                #region ShadowPlay Mods
#if SILVERLIGHT || PRODUCTION //in tool, we need to be able to select the body.  This logic is per body also, keep it in case fixture select of internal triangles is desired.
                if (FixtureList[i].CollisionFilter.CollidesWith != Category.None) //dh  there is no need to update the tree when not colliding with anything
#endif
                #endregion
                {
                    FixtureList[i].Synchronize(broadPhase, ref xf1, ref Xf);
                }
            }
        }

        internal void SynchronizeTransform()
        {
            Xf.R.Set(Sweep.A);

            float vx = Xf.R.Col1.X * Sweep.LocalCenter.X + Xf.R.Col2.X * Sweep.LocalCenter.Y;
            float vy = Xf.R.Col1.Y * Sweep.LocalCenter.X + Xf.R.Col2.Y * Sweep.LocalCenter.Y;

            Xf.Position.X = Sweep.C.X - vx;
            Xf.Position.Y = Sweep.C.Y - vy;

            NotifyPropertyChanged("Angle");
            //    NotifyPropertyChanged("Rotation");  dont need to see this

        }

        /// <summary>
        /// This is used to prevent connected bodies from colliding.
        /// It may lie, depending on the collideConnected flag.
        /// </summary>
        /// <param name="other">The other body.</param>
        /// <returns></returns>
        internal bool ShouldCollide(Body other)
        {
            // At least one body should be dynamic.
            if (_bodyType != BodyType.Dynamic && other._bodyType != BodyType.Dynamic)
            {
                return false;
            }

            // Does a joint prevent collision?
            for (JointEdge jn = JointList; jn != null; jn = jn.Next)
            {
                if (jn.Other == other)
                {
                    if (jn.Joint.CollideConnected == false)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal void Advance(float alpha)
        {
            // Advance to the new safe time.
            Sweep.Advance(alpha);
            Sweep.C = Sweep.C0;
            Sweep.A = Sweep.A0;
            SynchronizeTransform();
        }


        #region ShadowPlay Mods

        //merged from 94324 

        public event OnCollisionEventHandler OnCollision
        {
            add
            {
                if (FixtureList != null) //ShadowPlay mod on merge.. check for null 
                {
                    for (int i = 0; i < FixtureList.Count; i++)
                    {
                        FixtureList[i].OnCollision += value;
                    }
                }
            }
            remove
            {
                if (FixtureList != null) //ShadowPlay mod.. check for null 
                {
                    for (int i = 0; i < FixtureList.Count; i++)
                    {
                        FixtureList[i].OnCollision -= value;
                    }
                }
            }
        }

        public event OnSeparationEventHandler OnSeparation
        {
            add
            {
                for (int i = 0; i < FixtureList.Count; i++)
                {
                    FixtureList[i].OnSeparation += value;
                }
            }
            remove
            {
                for (int i = 0; i < FixtureList.Count; i++)
                {
                    FixtureList[i].OnSeparation -= value;
                }
            }
        }



        /// <summary>
        /// General Vertices Store the outer Edge of  the polygon for collision detection for this 
        /// Rigid body.  Its descriped in Local (Body ) coodinates, relative to the first point used to define it
        /// If it is  a concave polygon,  it is decomposed into fixtures, each of which is a triangle.
        /// </summary>
        [DataMember]
        public Vertices GeneralVertices { get; set; }


        public Vertices Normals = null;




        /// <summary>
        /// ZIndex value for ObjectView generated from this Body. 
        /// When created, ObjectView must read z-index from here.
        /// Default is 0, normally this value shouldn't be changed. 
        /// Only set if a body view need to be explicitly placed in front of other bodies view.
        /// </summary>
        [DataMember]
        public int ZIndex { get; set; }


        internal bool _isVisible = true;
        /// <summary>
        /// View will check for this property on each update to turn on/off visibility.
        /// Default is true (visible). No need to serialize.
        /// </summary>
        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }


        internal BodyColor _color;
        /// <summary>
        /// Color for this body. Should be shared among all fixture of this body.
        /// Default is opague white (255,255,255,255). This property should never null.
        /// </summary>
        [DataMember]
        public BodyColor Color
        {
            get
            {
                if (_color == null)
                {
                    _color = new BodyColor(255, 255, 255, 255);
                }
                return _color;
            }
            set
            {
                _color = value;
                //NotifyPropertyChanged("Color");
            }
        }


        internal BodyGradientBrush _gradientBrush;
        /// <summary>
        /// Brush for this body. Only used by body view. 
        /// Can be NULL, which in that case View will use Body.Color instead.
        /// </summary>
        //[DataMember]
        public BodyGradientBrush GradientBrush
        {
            get
            {
                //if (_gradientBrush == null)
                //{
                //    _gradientBrush = new BodyBrush();
                //}
                return _gradientBrush;
            }
            set
            {
                _gradientBrush = value;
                //NotifyPropertyChanged("Brush");
            }
        }


        internal PartType _partType = PartType.None;
        /// <summary>
        /// Optional data used in Spirit system. Default is None.
        /// </summary>
        [DataMember]
        public PartType PartType
        {
            get { return _partType; }
            set { _partType = value; }
        }

        private short _collisionGroup;
        /// <summary>
        /// Copied from Fixture, applies CollisionGroup to all Fixtures of a body.
        /// Defaults to 0
        /// 
        /// If Settings.UseFPECollisionCategories is set to false:
        /// Collision groups allow a certain group of objects to never collide (negative)
        /// or always collide (positive). Zero means no collision group. Non-zero group
        /// filtering always wins against the mask bits.
        /// 
        /// If Settings.UseFPECollisionCategories is set to true:
        /// If 2 fixtures are in the same collision group, they will not collide.
        /// </summary>
        [DataMember]//set last in case fixtures are saved.. for bodies in spirit systems, it will be reset after based on self collide of sprit
        public short CollisionGroup
        {
            set
            {
                //   if (_collisionGroup == value)   //theres has been syncing issue ,   clouds collidinge with each other, safe not to check this
                //in case fixtures were regenerated..
                //      return;

                _collisionGroup = value;

                if (FixtureList != null)
                {
                    foreach (Fixture f in FixtureList)
                    {
                        f.CollisionFilter.CollisionGroup = value;
                    }
                }
                NotifyPropertyChanged("CollisionGroup");
            }
            get { return _collisionGroup; }
        }


        /*  //TODO CODE REVIEW FUTURE CollidesWith. issue is it breaks old level since the default must be 1.   dont see a way in WCF to change it.
     //using DontCollide instead
          
       private Category _collidesWith = Category.All;
       //copied from Fixture..
       /// <summary>
       /// Defaults to Category.All
       /// The collision mask bits. This states the categories that this
       /// fixture would accept for collision.
       /// Use Settings.UseFPECollisionCategories to change the behavior.
       /// </summary>
       /// 
   
        [DataMember(Order = 99)]
       public Category CollidesWith
       {
           get { return _collidesWith; }

           set
           {
     
               if (_collidesWith == value)
                   return;

               _collidesWith = value;
                
               foreach (Fixture f in FixtureList)
               {
                   f.CollisionFilter.CollidesWith = value;
               }
 
           }
       }
       */


        //TODO CODE REVIEW FUTURE CollidesWith  clean this out, resave levels.
        private bool _isNotCollideable = false;

        [DataMember]
        /// <summary>
        ///  Will not collide with anything or update broad phase (for performance in production build) .  For tool it will to allow selection .
        /// </summary>
        public bool IsNotCollideable
        {
            get { return _isNotCollideable; }

            set
            {

                _isNotCollideable = value;

                if (FixtureList == null)
                    return;

                //  RebuildFixtures();  //TODO if the verts have changed..

                foreach (Fixture f in FixtureList)
                {
                    f.CollisionFilter.CollidesWith = (value == true ? Category.None : Category.All);
                }

#if SILVERLIGHT || PRODUCTION   //in here this body hasnt even been placed in tree.
                //tested poke eyes out..  need to land on ground in play
                if (value == false && ((Flags & BodyFlags.Enabled) == BodyFlags.Enabled))
                {
                    BroadPhase broadPhase = World.ContactManager.BroadPhase;
                    foreach (Fixture f in FixtureList)
                    {
                        if (f.ProxyCount == 0)
                            f.CreateProxies(broadPhase, ref Xf);
                    }
                }
#endif
            }
        }

        /// <summary>
        /// Optional data used in Spirit system. Default is false.  used to indicate middle legs for 4 leg creature (ie: middle right
        /// </summary>
        [DataMember]
        public bool IsMiddlePart
        {
            get
            {
                return (PartType & PartType.Middle) == PartType.Middle;
            }
            set
            {
                PartType |= PartType.Middle;
            }
        }


        //This overhead of unused stuff might affect particle creation speed, 
        //if not placed in tree.  consider derived Body from Particle
        internal List<AttachPoint> _attachPoints;
        [DataMember]
        public List<AttachPoint> AttachPoints
        {
            get
            {
                if (_attachPoints == null)
                {
                    _attachPoints = new List<AttachPoint>();
                }
                return _attachPoints;
            }
            set { _attachPoints = value; }   // for deserialize only, don't access
        }

        internal List<Emitter> _emitterPoints;
        [DataMember]
        public List<Emitter> EmitterPoints
        {
            get
            {
                if (_emitterPoints == null)
                {
                    _emitterPoints = new List<Emitter>();
                }
                return _emitterPoints;
            }
            set { _emitterPoints = value; } // for deserialization only, don't access
        }

        internal List<SharpPoint> _sharpPoints;
        [DataMember]
        public List<SharpPoint> SharpPoints
        {
            get
            {
                if (_sharpPoints == null)
                {
                    _sharpPoints = new List<SharpPoint>();
                }
                return _sharpPoints;
            }
            set { _sharpPoints = value; } // for deserialization only, do not access
        }


        internal List<MarkPoint> _visibleMarks;
        //[DataMember]      // for scar mark this should be serializable later.  but for snow particle shouldn't.
        public List<MarkPoint> VisibleMarks
        {
            get
            {
                if (_visibleMarks == null)
                {
                    _visibleMarks = new List<MarkPoint>();
                }
                return _visibleMarks;
            }
            set { _visibleMarks = value; } // for deserialization only, do not access
        }


#if USE_MARK_LOCK
        private object _visualMarkLock = new object();
#endif
        /// <summary>
        /// AttachParticleAsVisualMark this is added when a particle strikes an object and sticks to it, not thread safe
        /// </summary>
        /// <param name="particle"></param>
        /// <param name="worldpos">position in which this mark should appear. in world coord.</param>
        public MarkPoint AttachParticleAsVisualMark(Particle particle, Vector2 worldpos, Vector2 normal)
        {
            MarkPoint markpoint;

            try
            {

#if !USE_MARK_LOCK
                if (_markCollectingSkipped == true)  // extra check here just do nothing , skip it.. we have enough blood and snow.
                    return;       
#endif
                worldpos += normal * EdgeStrokeThickness / 2.0f;  // sometimes stroke is used for snow or moss or dirt to make collision appear closer to feet.  
                //attach particle at edge including stroke             
                //Vector2 localpos = GetLocalPoint(particle.WorldCenter);
                Vector2 localpos = GetLocalPoint(worldpos);

#if !USE_MARK_LOCK
                if (_markCollectingSkipped == true)  // just do nothing , skip it.. we have enough blood and snow.
                    return;       //TODO clean this .. it can still crash if two thread in MarkPoint contructor.. 
#else

                lock (_visualMarkLock)
#endif   // critcal section here
                {
                    markpoint = new MarkPoint(this, localpos);
                }

#if !USE_MARK_LOCK
               _markCollectingSkipped = false;
#endif
                // for some strange reason direction have to be perpendicular (90 degree)
                Vector2 normalRotated90 = new Vector2(-normal.Y, normal.X); // graphic coord
                markpoint.Direction = GetLocalVector(ref normalRotated90);

                float randomlifeSpan = MathUtils.RandomNumber(2000, 8000);

                if ((Info & BodyInfo.Solid) != 0)
                {
                    randomlifeSpan += 15000;  //keep it dirty
                }

                markpoint.Color = particle.Color;
                //markpoint.ZIndex = Int16.MinValue;

                float maxSize = _maxStuckParticleSize;

                if ((particle.Info & BodyInfo.Liquid) != 0)
                {
                    maxSize = _maxStuckParticleSizeBlood;  //TODO make like ellipse
                    markpoint.LifeSpan = randomlifeSpan;
                }
                else
                {
                    markpoint.LifeSpan += particle.LifeSpan + randomlifeSpan;// make snow an dust last longer
                    markpoint.Age = particle.Age;
                }

                markpoint.Radius = Math.Min(particle.ParticleSize, maxSize);
            }

            catch (Exception exc)
            {
                Debug.WriteLine(" exception in AttachParticleAsVisualMark " + exc.ToString());

#if !USE_MARK_LOCK
               _markCollectingSkipped = false;
#endif  //?? need to unlock?   ah were out of memroy gonna die anyways.
                return null;
            }
            //   finally()
            //   {
            //   }

            return markpoint;
        }




        public const string BulletTemporary = "bullet";

        /// <summary>
        ///Use a joint and attack point to stick a bullet in this body
        /// </summary>
        /// <param name="contactWorldPosition"></param>
        /// <param name="contactWorldNormal"></param>
        /// <param name="contactImpulse"></param>
        /// <param name="isSharpObject">true if contact with sharp object. false otherwise.</param>
        public bool AttachProjectileInsideBody(Vector2 contactWorldPosition, Vector2 contactWorldNormal, float contactImpulse, Body strikingBody, short spiritCollideID, out Vector2 bleedOriginLocation)
        {
            bleedOriginLocation = contactWorldPosition;

            if (Area < 5 * strikingBody.Area)
                return false;

            if ((PartType & (PartType.Hand | PartType.Foot | PartType.Toe)) != 0)  //these parts have too much transparency..
                return false;
  
         //   depth += 0.02f;  // seems like the collisino tolerance.. now using ray intesect point on shape
         //     float depth  = MathHelper.Max(contactImpulse, 16f);   TODO rellate to this impulse.. TODO check with cutter.. either cut of embed.. or prevent cutting on thin items
            //Vector2 penetrationVec = -contactWorldNormal;
            Vector2 penetrationVec = strikingBody.LinearVelocity;
            penetrationVec.Normalize();

            float penentrationDepth = GetPenetrationDepth(PartType);  // should not break bone.. for arms
      
            Vector2 indentationVec = penetrationVec * penentrationDepth;
            Vector2 contactIndented = contactWorldPosition + indentationVec;

            float penentrationDepthBlood = Math.Max(penentrationDepth / 2f, 0.01f);//not too close to bullet will move it if collidable.   just below skin..

            bleedOriginLocation = contactWorldPosition + penetrationVec * penentrationDepthBlood; 

            //then... we could break joint before strike..  let bullet plow through..  at lest for bullet
            //if attach.. then  leave linear vel as is
            //but for swords..?  dont know..   make sure impulse is corrrect  TODO .. remove post solv.
            bool insideOneOfFixture = IsInsideAFixture(contactIndented);

            if (!insideOneOfFixture) //try with less indent
            {
                 indentationVec = penetrationVec * penentrationDepth/2;
                 contactIndented = contactWorldPosition + indentationVec;
                 insideOneOfFixture = IsInsideAFixture(contactIndented);
            }

             if (!insideOneOfFixture)  
                return false;

            Vector2 embeddedWorldPosition = contactWorldPosition;
            embeddedWorldPosition += indentationVec;// this is for appearance.. collision is usually inside dress.. except on upper arm..

            embeddedWorldPosition -= penetrationVec * 0.007f;  //move it back out so bullet wont appear to cut bone on arm.   if moved out prior it wont be inside fixture.

            //Vector2 direction = GetLocalVector(ref contactWorldNormal);
            //For view in  graphic coord,  direction has to be perpendicular to reported normal (90 degree).. 
            Vector2 normalRotated90 = new Vector2(-contactWorldNormal.Y, contactWorldNormal.X);

            Vector2 localpos = GetLocalPoint(ref embeddedWorldPosition);

            strikingBody.ZIndex = ZIndex + 2;
            strikingBody.LinearVelocity = LinearVelocity;// make it stop..    

            //TODO if make body only.. make to rub// remove bullet..
            //strikingBody.IsNotCollideable = true;  // TODO future optimisatino.. .. make this not collidable except when other bullet penetrates so they dont collide with each other.
            //TODO search other body parts..

            strikingBody.CollisionGroup = spiritCollideID; //this is so unstickHeadFromArms can workin plugin.
            strikingBody.Position = embeddedWorldPosition;
  
            AttachPoint atc = new AttachPoint(this, localpos);  // dont need to add to collection ..unless body can expell them
            atc.Name = BulletTemporary;  //TODO future.. could be a flag in addition to IsTemporary
            atc.Flags |= (AttachPointFlags.IsTemporary);

            ///bullet are 170 grams,, massive                
            if (PartType != PartType.MainBody)
            {
                atc.StretchBreakpoint = 500;  // can be rubbed off .. or pulled off maybe.
            }

            atc.Detached += OnTemporaryAttachPtDetached;
            atc.Attach(strikingBody.AttachPoints[0]);// ignoreing contacts..

            AttachPoint newGrab = new AttachPoint(strikingBody, strikingBody.LocalCenter);
            strikingBody.AttachPoints.Add(newGrab);

            //TODO is not colliding with due to joint .. so need to play  the effect.. test on board.
            //PhysicsSounds     PlayCollisionSoundEffect(Body body, Body otherBody, Contact contact, ContactConstraint impulse)
             return true;
        }

        void OnTemporaryAttachPtDetached(AttachPoint atc, AttachPoint atcPartner)
        {
            AttachPoints.Remove(atc);  //so it cant be grabbed
        }


        public void ReleaseListeners()
        {
            AttachPoints.ForEach( x => x.Detached -= OnTemporaryAttachPtDetached);
        }

        float GetPenetrationDepth(PartType pt)
        {
             float depth = PartType == PartType.MainBody ? 0.05f : 0.002f;   // 1/2 cm.. bullet enter just inside skin.. to bone. collide has a little buffer..

            if (PartType == PartType.Head)
            { 
                depth = 0.055f;  //deeper for  structures?
            }
            
            if (PartType == PartType.None)
           {   //TODO//Density or hardness.
                depth = 0.02f;  //deeper for  structures?
            }

            depth *= MathUtils.RandomNumber(0.8f, 1.2f);  //vary a bit.
            //TODO shoot ground? particles up..
            return depth;
        }

        /// <summary>
        /// can return null. check for null value.
        /// </summary>
        /// <param name="contactWorldPosition"></param>
        /// <param name="contactWorldNormal"></param>
        /// <param name="contactImpulse"></param>
        /// <param name="isSharpObject">true if contact with sharp object. false otherwise.</param>
        /// <returns>can return null. check for null value.</returns>
        public MarkPoint AttachContactPointAsVisualMark(Vector2 contactWorldPosition, Vector2 contactWorldNormal, float contactImpulse, bool isSharpObject, Body strikingBody)
        {
            try
            {
                //TODO factor out commonality  between bruise and scar...    
                MarkPointType useType = MarkPointType.General;

                // prevent wound mark too big
                float impulse = MathHelper.Min(contactImpulse, 6f);
                float markRadius = 0.007f * (impulse - MinImpulseForMarkCreation);     // size of scar/bruise, larger impulse gives larger scar

                BodyColor color = GetMarkCharacteristics(isSharpObject, ref useType, ref markRadius);

                float circleFlattenFactor = 0.5f;  // TODO same as in current objectview fact, shoujdl be a property of Markpoint, dont have time to redo it.
                //TODo move the flattening factor to MarkPoint.. we need it .. want bruise to go just under skin
                float indentation = isSharpObject ? 0.01f : 0.006f + (markRadius * circleFlattenFactor);

                Vector2 indentationVec = contactWorldNormal * indentation;
                Vector2 contactIndented = contactWorldPosition - indentationVec;

                // if indented position is  floating (not inside Body shape), then abort mark point creation
                //TODO see why sometimes floating collisions happen with both blood and marks

                //if we are using ray for collide.. using the new frame. and place with new body.. xform, so it shoud be correct.. frame just after reaction from collision is close
                // ACCESS_LAST_FRAME   bool insideOneOfFixture = IsInsideAFixture(ref contactWorldPosition, true);   //using last frames position.. have to use this for everthing then  ACCESS_LAST_FRAME//TODO if passing false have to uncomment last frame thing
                bool insideOneOfFixture = IsInsideAFixture(contactIndented);  //TODO if passing false have to uncomment last frame thing  

                if (!insideOneOfFixture)
                    return null;

                Vector2 visualContactWorldPosition = contactWorldPosition;

                if (!isSharpObject)
                {
                    visualContactWorldPosition -= indentationVec;// this is for appearance.. collision is usually inside dress.. except on upper arm..
                }

                //Vector2 direction = GetLocalVector(ref contactWorldNormal);
                //For view in  graphic coord,  direction has to be perpendicular to reported normal (90 degree).. 
                Vector2 normalRotated90 = new Vector2(-contactWorldNormal.Y, contactWorldNormal.X);
                Vector2 direction = GetLocalVector(ref normalRotated90);

                MarkPoint markpoint;
                Vector2 localpos = GetLocalPoint(ref visualContactWorldPosition);

                markpoint = new MarkPoint(this, localpos);
                markpoint.LifeSpan = float.MaxValue;
                markpoint.Radius = markRadius;
                markpoint.Color = color;
                markpoint.UseType = useType;
                markpoint.Direction = direction;
                //markpoint.ZIndex = Int16.MaxValue;
                return markpoint;
            }

            catch (Exception exc)
            {
                Debug.WriteLine(" exception in AttachHitSharpPointAsVisualMark " + exc.ToString());
                return null;
            }
        }


#if ACCESS_LAST_FRAME   
        /// <summary>
        /// Is world ponit inside one of the fixtures.
        /// </summary>
        /// <param name="contactWorldPosition"></param>
        /// <param name="useLastFrame"></param>
        /// <returns></returns>
        private bool IsInsideAFixture(ref Vector2 contactWorldPosition, bool useLastFrame)
        {
            bool insideOneOfFixture = false;
            foreach (Fixture f in FixtureList)
            {
                // check at least inside 1 of fixture
                if  ( useLastFrame  ? f.TestPointLastFrame(ref contactWorldPosition): f.TestPoint(ref contactWorldPosition))
                {
                    insideOneOfFixture = true;
                    break;
                }
            }
            return insideOneOfFixture;
        }
#endif


        /// <summary>
        /// Is world ponit inside one of the fixtures.
        /// </summary>
        /// <param name="contactWorldPosition"></param>
        /// <param name="useLastFrame"></param>
        /// <returns></returns>
        private bool IsInsideAFixture(Vector2 contactWorldPosition)
        {
            bool insideOneOfFixture = false;
            foreach (Fixture f in FixtureList)
            {
                // check at least inside 1 of fixture
                if (f.TestPoint(ref contactWorldPosition))
                {
                    insideOneOfFixture = true;
                    break;
                }
            }
            return insideOneOfFixture;
        }

        private BodyColor GetMarkCharacteristics(bool isSharpObject, ref MarkPointType useType, ref float markSize)
        {
            BodyColor markColor = new BodyColor();
            if (isSharpObject)
            {
                useType = MarkPointType.Scar;
                markSize *= 0.5f;
                markColor = new BodyColor(81, 0, 14, 255);    // dark red for scars
            }
            else
            {
                useType = MarkPointType.Bruise;
                // sharpPointMarkSize *= 0.58   // bruise.. test  to appear on boxing test

                float sizeFactor = (PartType == PartType.Head || PartType == PartType.Jaw) ? 0.25f : 0.5f;
                markSize *= sizeFactor;
                // color = new BodyColor(39, 26, 70, 255);    // purple for bruise, picked with tool
                //  color = new BodyColor(47, 11, 69, 255);  //darker one..
                // random blak an blue 
                markColor = new BodyColor((byte)(int)MathUtils.RandomNumber(40, 60), 20, (byte)(int)MathUtils.RandomNumber(30, 69), (byte)(int)MathUtils.RandomNumber(180, 240));//not fully opaque
            }

            return markColor;
        }


        internal BodySoundEffectParams _soundEffect;
        [DataMember]
        public BodySoundEffectParams SoundEffect
        {
            get { return _soundEffect; }
            set
            {
                if (_soundEffect != value)
                {
                    _soundEffect = value;
                    NotifyPropertyChanged("SoundEffect");
                }

            }
        }


        internal float _edgeStrokeThickness;
        [DataMember]
        public float EdgeStrokeThickness
        {
            get { return _edgeStrokeThickness; }
            set
            {
                _edgeStrokeThickness = value;
                //NotifyPropertyChanged("EdgeStrokeThickness");
            }
        }

        internal BodyColor _edgeStrokeColor;
        [DataMember]
        public BodyColor EdgeStrokeColor
        {
            get
            {
                if (_edgeStrokeColor == null)
                {
                    _edgeStrokeColor = new BodyColor(255, 255, 255, 255);
                }
                return _edgeStrokeColor;
            }
            set
            {
                _edgeStrokeColor = value;
                //NotifyPropertyChanged("EdgeStrokeColor");
            }
        }


        private UndoRedo<T> CreateUndoableMember<T>(NotifyUndoRedoMemberChangedEventHandler<T> handler)
        {
            UndoRedo<T> d = new UndoRedo<T>();
            d.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<T>(handler);

            return d;
        }


        private UndoRedo<float> _density;
        [DataMember]   //TODO  fixture density info this should be After  IsSaveFixture)..but if i change it breaks allot of old files.. still magically save fixture with differnt density seems to work
        public float Density
        {
            get
            {
                if (_density == null)
                {
                    _density = CreateUndoableMember<float>(_density_UndoRedoChanged);
                }
                return _density.Value;
            }
            set
            {
                if (_density == null)
                {
                    _density = CreateUndoableMember<float>(_density_UndoRedoChanged);
                }

                if (_density.ValueUndoable != value)
                {
                    _density.ValueUndoable = value;

                    if (_density.ValueUndoable < 0.0001f)
                    {
                        _density.ValueUndoable = 0.0001f;
                    }

                    ResetDensity(value);

                    //    if (!IsSaveFixture)//TODO fixture density info
                    //    {
                    //      ResetDensity(value);
                    //   }
                    //   else
                    //       ResetMassData();
                }
            }
        }

        void ResetDensity(float value)
        {
            if (FixtureList != null)
            {
                foreach (Fixture f in FixtureList)
                    f.Density = _density.ValueUndoable;
            }

            ResetMassData();
            NotifyPropertyChanged("Density");
        }

        void _density_UndoRedoChanged(object sender, UndoRedoChangedType type, float oldState, float newState)
        {
            ResetDensity(_density.Value);
        }

        private UndoRedo<float> _restitution;
        [DataMember]
        public float Restitution
        {
            get
            {
                if (_restitution == null)
                {
                    _restitution = CreateUndoableMember<float>(_restitution_UndoRedoChanged);
                }
                return _restitution.Value;
            }
            set
            {
                if (_restitution == null)
                {
                    _restitution = CreateUndoableMember<float>(_restitution_UndoRedoChanged);
                }

                if (_restitution.ValueUndoable != value)
                {
                    _restitution.ValueUndoable = value;

                    ResetRestitution(value);
                }
            }
        }

        void ResetRestitution(float value)
        {
            if (FixtureList != null)
            {
                foreach (Fixture f in FixtureList)
                    f.Restitution = value;
            }
            NotifyPropertyChanged("Restitution");
        }

        void _restitution_UndoRedoChanged(object sender, UndoRedoChangedType type, float oldState, float newState)
        {
            ResetRestitution(_restitution.Value);
        }

        private UndoRedo<float> _friction = null;
        [DataMember]
        public float Friction
        {
            get
            {
                if (_friction == null)
                {
                    _friction = CreateUndoableMember<float>(_friction_UndoRedoChanged);
                }
                return _friction.ValueUndoable;
            }

            set
            {
                if (_friction == null)
                {
                    _friction = CreateUndoableMember<float>(_friction_UndoRedoChanged);
                }

                if (_friction.ValueUndoable != value)
                {
                    _friction.ValueUndoable = value;

                    ResetFriction(value);
                }
            }
        }

        private void ResetFriction(float friction)
        {
            if (FixtureList != null)
            {
                foreach (Fixture f in FixtureList)
                    f.Friction = friction;
            }
            NotifyPropertyChanged("Friction");
        }

        void _friction_UndoRedoChanged(object sender, UndoRedoChangedType type, float oldState, float newState)
        {
            ResetFriction(_friction.Value);
        }


        /// <summary>
        /// The friction coefficient when the item is moving , used as as a function of relative  velocity  ( will be divided by 
        /// </summary>
        /// if null then just use normal friction  (allows use of zero) 
        [DataMember]
        public Nullable<float> SlidingFriction { get; set; }

        public Nullable<float> GetSlidingFriction(Body otherBody)
        {
            if (SlidingFriction == null)
                return Friction;

            //TODO FUTURE add contact points, use rel vel or impulse at that point..
            //hint: search   relavite velocity   in contactsolver..     // Solve normal constraints
            //     if (c.PointCount == 1)
            //      ContactConstraintPoint ccp = c.Points[0];

            // Relative velocity at contact
            float relVelSq = (LinearVelocity - otherBody.LinearVelocity).LengthSquared();
            //  Debug.WriteLine("relVelSq" + relVelSq.ToString());
            if (relVelSq > 1.2f)
            {
                return SlidingFriction;
            }
            else
                return Friction;
        }



#if ORIGINAL_FARSEER_3_2               
        private float _density = 1.0f;  // Must be non null, so RebuildFixtures() won't crash and burn
        [DataMember]
        public float Density
        {
            get
            {
                return _density;
            }
            set
            {
                if (_density != value)
                {
                    _density = value;

                    if (_density < 0.0001f)
                    {
                        _density = 0.0001f;
                    }

                    NotifyPropertyChanged("Density");

                    if (FixtureList != null)
                    {
                        foreach (Fixture f in FixtureList)
                            f.Density = _density;
                    }

                    ResetMassData();
                }
            }
        }

        private float _restitution;
        [DataMember]
        public float Restitution
        {
            get
            {
                return _restitution;
            }
            set
            {
                if (_restitution != value)
                {
                    _restitution = value;

                    if (FixtureList != null)
                    {
                        foreach (Fixture f in FixtureList)
                            f.Restitution = value;
                    }
                    NotifyPropertyChanged("Restitution");
                }
            }
        }

        private float _friction;
        [DataMember]
        public float Friction
        {
            get
            {
                return _friction;
            }

            set
            {
                if (_friction != value)
                {
                    _friction = value;

                    if (FixtureList != null)
                    {
                        foreach (Fixture f in FixtureList)
                            f.Friction = value;
                    }
                    NotifyPropertyChanged("Friction");
                }
            }
        }
#endif


        /// <summary>
        /// Cached world AABB of this body, should come from combined aabb of all 
        /// fixtures in FixtureList. Use world coordinate. 
        /// Update this through UpdateAABB(). 
        /// Note: fixture with IsSensor=true is not included.
        /// </summary>
        public AABB AABB;  

        /// <summary>  
        /// Xaml shapes that used on visual layer as 'dress' for this Body.  
        /// When loaded, it will become a Canvas object, which then stored on GeneralObjectView.Content.
        /// </summary>  
        [DataMember]
        public string DressingGeom { get; set; }

        /// <summary>
        /// Scaling for dress. 
        /// Primarily for Shadowplay, because it can't rewrite DressingGeom string.
        /// This property will be updated when Body is scaled using ScaleLocal().
        /// When creating dress view for body, code should automatically scale dress view using this value.
        /// Shadowtool should reset this value to 1 if it rewrites DressingGeom string.
        /// </summary>
        [DataMember]
        public Vector2 DressScale { get; set; }


        public float DressScaleX
        {
            get { return DressScale.X; }
            set { DressScale = new Vector2(value, DressScale.Y); }
        }

        public float DressScaleY
        {
            get { return DressScale.Y; }
            set
            { DressScale = new Vector2(DressScale.X, value); }
        }


        /// <summary>  
        /// Xaml shapes that used on visual layer as alternate 'dress' for this Body.  
        ///  for dress2 to be used for simple animation like heart beat.. switch back and forth.
        /// </summary>  
        [DataMember]
        public string DressingGeom2 { get; set; }

        /// <summary>
        /// Scaling for dress, 2 to be used for simple animation like heart beat
        /// </summary>
        [DataMember]
        public Vector2 DressScale2 { get; set; }

        /// <summary>
        /// Dress offset, currently unused.
        /// </summary>
        //[DataMember]
        //public Vector2 DressOffset { get; set; }


        /// <summary>
        /// adds to Energy.. if negative.. causes illness or drunkenness
        /// </summary>
        [DataMember]
        public float Nourishment { get; set; }

        [DataMember]
        public float Acidity { get; set; } // -3 very acidic, can melt stuff    10 basic.. like pH


        //   http://en.wikipedia.org/wiki/Drag_(physics).   dimensionless factor how much air and drag affects particle
        // also affected by size and vel  square..  //set to zero and object wont be affected by wind
        [DataMember]
        public float DragCoefficient { get; set; }

        /// <summary>
        /// If true, the wind flow will treat this body as a particle, skip the verices..
        /// </summary>
        [DataMember]
        public bool ApplyDragAsParticle { get; set; }

        //   [DataMember]
        //   public float Temperature { get; set; } // degrees K


        //for faster stuff at high altitudes, dont cast rays to see if wind is blocked
        [DataMember]
        public bool SkipWindBlockCheck { get; set; }


        /// <summary>
        /// Minimum value for collision impulse that can create bruise/scar mark on Body.
        /// from testing, flying sword gives about 2-3 impulse unit (Ns), walking above sword can get 4-7 Ns,
        /// and swordsman stab using sword can give about 5-19 Ns to yndrd.
        /// </summary>
        //TODO move all this special stuff to Creature..
        public static readonly float MinImpulseForMarkCreation = 1f;
        public static readonly float MinBulletImpulseForJointBreak = 13f;
        public static readonly float MinBulletImpulseForBoneShellPenetration = 11f; //these are normal impulses


        /// <summary>
        /// Fixture cache to be used as temporary fixture list when serializing,
        /// as FixtureList will be nulled temporarily.
        /// </summary>
        private List<Fixture> _savedFixture;


        private bool _bIsSaveFixture = false;
        /// <summary>
        /// If TRUE, FixtureList will be serialized.
        /// Otherwise FixtureList will be nulled (moved to _savedFixture temporarily) when serialize.
        /// </summary>
        [DataMember]  //NOTE should  save this before density..  tried putting order = before density but messes up clouds, everything.. somehow  it works on ynrd.. but somehow didnt work on binary simple pickaxe
        public bool IsSaveFixture
        {
            get { return _bIsSaveFixture; }
            set
            {
                if (_bIsSaveFixture != value)
                {
                    _bIsSaveFixture = value;
                }
            }
        }


        /// <summary>
        /// Flag to inform that we are in deserialization proses.
        /// </summary>
        private bool _isDeserializing;


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
#if MONOTOUCH || SILVERLIGHT
                System.Diagnostics.Debug.WriteLine("{0} \n{1}", ex.Message, ex.StackTrace);
#else
                System.Diagnostics.Trace.TraceError(ex.Message);
                System.Diagnostics.Trace.TraceError(ex.StackTrace);
#endif
            }
        }


        [OnSerializing]
        public void OnSerializing(StreamingContext sc)
        {
            // comment out block below if we need to keep fixture list always serialized
            if (GeneralVertices != null && !_bIsSaveFixture)
            {
                _savedFixture = FixtureList;

                //comment this out causes fixtures to be saved in the file for all bodies
                //result in aprox 5 x files size, but with 5 x faster load time
                FixtureList = null;
            }
        }

        [OnSerialized]
        public void OnSerialized(StreamingContext sc)
        {
            if (GeneralVertices != null && _savedFixture != null && !_bIsSaveFixture)
            {
                FixtureList = _savedFixture;
                _savedFixture = null;
            }
        }

        // executed first when deserialize
        [OnDeserializing]
        public void OnDeserializing(StreamingContext sc)
        {
            // warning: all props will still null here
            World = World.RecentInstance;
            // allocate default fixture list. when FixtureList property is being 
            // deserialized and filled with non-null value, this default fixture
            // will be overwritten.

            if (!NeverUsingFixtures())
            {
                FixtureList = new List<Fixture>(32);
            }

            // to prevent null _mass when ResetMassData is called from some property
            _mass = new UndoRedo<float>();
            _isDeserializing = true;

#if USE_MARK_LOCK
            _visualMarkLock = new object();
#endif

        }

        // executed last when deserialize
        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            //  Rebuild from general verts if fixturelist was not deserialized

            //  this is an optimization for clouds that loaded as IsNotCollideable, They may  collisions using query in AABB in CloudBurst plugin  
            // dont even allocate for fixtures just use a AABB rectangle to approximate it.
            if (NotUsingFixtures())
            {
                AABB aabb = GetBodySpaceAABB();
                PolygonShape shape = new PolygonShape(aabb.VerticesCounterclockwise, Density);       
                //     CircleShape circleShape = new CircleShape(radius, Density);  /             
                CreateFixture(shape, null);  //just to set the mass data.   need some to be a valid object          
            }
            else
            {
                if (FixtureList == null || FixtureList.Count <= 0)
                {
                    RebuildFixtures();
                }
            }

            // to fix old level that have empty DressScale. 
            if (DressScale == Vector2.Zero)
            {
                DressScale = new Vector2(1.0f, 1.0f);
            }

            if (DressScale2 == Vector2.Zero)
            {
                DressScale2 = new Vector2(1.0f, 1.0f);
            }

            _isVisible = true;  // default is visible

            //#if !SILVERLIGHT     ///TODO todo  ERASE .. old attemp at unwinding in model.. 
            //            // this will clamp rotation value to less than 2pi, but might be better suited for tools UI.
            //            // because spirit & body will jumpy, and cloud will rotating, when first loading level.
            //            Rotation %= MathHelper.Pi * 2;
            //#endif

            _isDeserializing = false;
        }

        /// <summary>
        /// Rebuild body fixtures from existing GeneralVertices.
        /// Warning: This function is locked during callbacks.
        /// Warning: density is needed in order to avoid contact solver crash
        /// </summary>
        /// <param name="density">The Density of the fixtures</param>
        public void RebuildFixtures()
        {
            if (GeneralVertices == null)
                return;

            SetCollision(Enabled);

            if (NeverUsingFixtures())//shadow play mod
            {
                UpdateAABB();  
                return;
            }

            if (FixtureList == null)
                FixtureList = new List<Fixture>();

            DestroyAllFixtures();

            if (Info == BodyInfo.UseEdgeShape)
            {
                CreateFixtureUsingEdgeShape(GeneralVertices);
            }
            else if (Info == BodyInfo.UseLoopShape)
            {
                CreateFixtureUsingLoopShape(GeneralVertices);
            }
            else
            {
                // Assuming Density is not 0
                CreateFixtures(GeneralVertices, Density);
            }


            ResetMassData();

            foreach (Fixture f in FixtureList)
            {

                f.CollisionFilter.CollisionGroup = CollisionGroup;

                if (IsNotCollideable)
                {
                    f.CollisionFilter.CollidesWith = Category.None;
                }

                //TODO CODE REVIEW FUTURE CollidesWith.
                //  f.CollisionFilter.CollidesWith = CollidesWith;
            }

            UpdateAABB();
        }


        /// <summary>
        /// clear contents of FixtureList.
        /// </summary>
        public void DestroyAllFixtures()
        {
            if (FixtureList.Count > 0)
            {
                List<Fixture> tempFixtures = new List<Fixture>(FixtureList);
                foreach (Fixture f in tempFixtures)
                {
                    DestroyFixture(f);
                }
                FixtureList.Clear();
                tempFixtures.Clear();
            }
        }


        //TODO in tool its being used only for picking..
        public bool IsUsingFixtures()
        {
            return ((Info & BodyInfo.Cloud) != 0 && IsNotCollideable);
        }


        public void CreateFixtureUsingLoopShape(Vertices vertices)
        {
            LoopShape shape = new LoopShape(vertices);
            CreateFixture(shape);
        }


        public void CreateFixtureUsingEdgeShape(Vertices vertices)
        {
            for (int i = 0; i < vertices.Count - 2; i++)
            {
                EdgeShape shape = new EdgeShape(vertices[i], vertices[i + 1]);

                if (i > 0)
                {
                    shape.Vertex0 = vertices[i - 1];
                }

                if (i < vertices.Count - 3)
                {
                    shape.Vertex3 = vertices[i + 2];
                }

                CreateFixture(shape);
            }
        }


        /// <summary>
        /// Create one or more fixtures from a given vertices. 
        /// Vertices can be concave or convex.
        /// Warning: This function is locked during callbacks.
        /// </summary>
        public void CreateFixtures(Vertices vertices, float density)
        {
            vertices.ForceCounterClockWise();
            if (vertices.IsConvex() && vertices.Count <= Settings.MaxPolygonVertices)
            {
                CreateFixtureFromConvexVerts(vertices, density);
            }
            else
            {
                CreateFixturesFromConcaveVerts(vertices, density);
            }
        }

        /// <summary>
        /// Create a fixture from a given convex vertices. 
        /// </summary>
        private void CreateFixtureFromConvexVerts(Vertices vertices, float density)
        {
            try
            {
                PolygonShape polygonShape = new PolygonShape(vertices, density);
                CreateFixture(polygonShape, density);
            }
            catch (Exception ex)
            {
                // any error when creating fixture will be skipped
                Debug.WriteLine("Error CreateFixtureFromConvexVerts" + ex.Message);
            }
        }

        /// <summary>
        /// Create one or more fixtures from a given concave vertices. 
        /// Note: this method uses recursive call.
        /// </summary>
        private void CreateFixturesFromConcaveVerts(Vertices vertices, float density)
        {

            try
            {
                // Force the vertices to counter clockwise, so that normals will point outward i believe.  -dh
                if (vertices.IsCounterClockWise() == false)
                    vertices.ForceCounterClockWise();

                // Decompose our vertices
                List<Vertices> listverts = EarclipDecomposer.ConvexPartition(vertices); //3.3ms physics update  yndrd-swordfightbalancescriptroughgroundsections.wyg

                //   List<Vertices> listverts = BayazitDecomposer.ConvexPartition(vertices);  3.9ms
                //   List<Vertices> listverts = CDTDecomposer.ConvexPartition(vertices);//8.3 ms
                //   List<Vertices> listverts = SeidelDecomposer.ConvexPartition(new Vertices(vertices), 0.001f); // crashes tried  .1 .01, .0001
                //   List<Vertices> listverts = FlipcodeDecomposer.ConvexPartition(vertices);  //10.3ms
                if (listverts.Count <= 0)
                    return;

                // If decompose succeed, iterate for each decomposed convex poly
                foreach (Vertices verts in listverts)
                {
                    if (verts.Count == 0)
                        continue;

                    // Recheck again if the decomposed part is really is convex
                    if (verts.IsConvex())
                    {
                        // Create our convex fixture
                        CreateFixtureFromConvexVerts(verts, density);
                    }
                    else
                    {
                        // This decomposed part is not yet convex, then run the decomposer again
                        CreateFixturesFromConcaveVerts(verts, density);
                    }
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine("Error in CreateFixturesFromConcaveVerts: " + exc.Message.ToString());
            }
        }

        /// <summary>
        /// Update Body.AABB .
        /// </summary>
        public void UpdateAABB()
        {
            AABB.Reset();

            if (FixtureList == null || FixtureList.Count == 0 )
            {
                ComputeAABBFromGeneralVerts(out AABB, ref Xf);  //TODO optimize  will be faster than fixture way since verts are revisited.. should  do this in all cases.   each fixture is repeating information.  now used only for clouds
                return;
            }

            AABB aabb;
            bool init = false;
            foreach (Fixture f in FixtureList)
            {
                // usually sensor fixture is skipped from aabb. if required, 
                // can be combined with aabb from sensor fixture later.
                if (f.IsSensor)
                    continue;

                f.Shape.ComputeAABB(out aabb, ref Xf, 0);
                if (init == false)
                {
                    AABB.Copy(aabb);
                    init = true;
                }
                else AABB.Combine(ref aabb);
            }
        }



     //this was adapted from farseer polygon shape
        
        public AABB GetBodySpaceAABB()
        {
            AABB aabb;
            aabb.LowerBound = aabb.UpperBound = GeneralVertices[0];

            for (int i = 1; i < GeneralVertices.Count; ++i)
            {
               aabb.LowerBound = Vector2.Min(  aabb.LowerBound, GeneralVertices[i]);
               aabb.UpperBound  = Vector2.Max( aabb.UpperBound, GeneralVertices[i]);
            }
            return aabb;

        }
        
        public void ComputeAABBFromGeneralVerts(out AABB aabb, ref Transform transform)
        {
            Vector2 lower = MathUtils.Multiply(ref transform, GeneralVertices[0]);
            //   Vector2 lower = GeneralVertices[0];
            Vector2 upper = lower;
            for (int i = 1; i < GeneralVertices.Count; ++i)
            {
                Vector2 v = MathUtils.Multiply(ref transform, GeneralVertices[i]);    //TODO OPTIMIZATION see below  and.. could optimize in farseer... for a not rotateable body such as cloud, can this in body space , then transform,
                lower = Vector2.Min(lower, v);
                upper = Vector2.Max(upper, v);
            }

            Vector2 r = new Vector2(Settings.PolygonRadius, Settings.PolygonRadius); //just a little buffer used for farseer CCD
            aabb.LowerBound = lower - r;
            aabb.UpperBound = upper + r;

            //if (FixedRotation && Angle == 0)
            //{
            //            public void ComputeBodySpaceBounds(  AABB aabb)
            //            Vector2 v = MathUtils.Multiply(ref transform, GeneralVertices[i]);    //TODO OPTIMIZATION  and.. could optimize in farseer... for a not rotateable body such as cloud, can this in body space , then transform,
            //}
        }


        //TODO... try  first vert on with max Y from edge and remove it..
        // for remve maxY edge...  can be used to shrink cloud..
        public void RemoveExtremeVerts(float factor)
        {
            ///     Debug.Assert(GeneralVertices != null);
            //       double width = AABB.Width;
        }


        public void JitterVertices(float factor)
        {
            Debug.Assert(GeneralVertices != null);
            double width = AABB.Width;

            float variation = AABB.Width * factor / 2;

            int max = GeneralVertices.Count;

            for (int i = 0; i < max; i++)
            {
                Vector2 vertex = GeneralVertices[i];
                vertex.X += MathUtils.RandomNumber(-variation, variation);
                vertex.Y += MathUtils.RandomNumber(-variation, variation);
                GeneralVertices[i] = vertex;

            }

            RebuildFixtures();  //this can be costly for concave items..       

        }


        //TODO add a y2 componet for curving..  cloud can get head, claws..looking during deform.. 
        public void Skew(float factor)
        {
            Vector2 localAABBLowerBound = AABB.LowerBound - AABB.Center;

            if (GeneralVertices != null)
            {
                int max = GeneralVertices.Count;
                for (int i = 0; i < max; i++)
                {
                    Vector2 vertex = GeneralVertices[i];
                    vertex.X += (vertex.Y - LocalCenter.Y) * -1f * factor; // Because out Y is upside down, we reverse from vertex.Y - lowY into lowY - vertex.Y
                    GeneralVertices[i] = vertex;
                }
            }

            RebuildFixtures();  //this can be costly for concave items..        

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="verticalAxisLocalX">vertical axis X position, in local coordinate</param>
        public void MirrorHorizontal(float verticalAxisLocalX)
        {
            // NOTE, TODO FOR LATER: This should go in 2 step:
            // 1. Mirror all vertices using local center as axis.
            // 2. Mirror body position based on external axis. 
            //    For spirit system, called from Spirit.ApplyMirror(), external axis = spirit WorldCenter.  
            //    For individual body, called from MainWindow.btnApplySS_MirrorHorz(), external axis = body WorldCenter.  

            int max;
            Vertices vs;

            // new shape
            if (GeneralVertices != null)
            {
                max = GeneralVertices.Count;
                for (int i = 0; i < max; i++)
                {

#if FUTUREREVISIT
                    GeneralVertices[i] = Vector2.MirrorHorizontal(GeneralVertices[i], Position.X);
#else
                    GeneralVertices[i] = Vector2.MirrorHorizontal(GeneralVertices[i], verticalAxisLocalX);
#endif
                }
            }

            Position = Vector2.MirrorHorizontal(Position, verticalAxisLocalX);

            // reverse rotation too
            Rotation *= -1f;


#if FUTUREREVISIT 
            RebuildFixtures();   // mirror fixtures should not be needed.  I did test reshape after mirro on Zero, its seems ok..
#else
            if (FixtureList != null)
            {
                foreach (Fixture f in FixtureList)
                {
                    switch (f.ShapeType)
                    {
                        default:
                            continue;

                        case ShapeType.Polygon:
                            PolygonShape poly = f.Shape as PolygonShape;
                            if (poly == null)
                            {
                                continue;
                            }

                            vs = new Vertices(poly.Vertices);
                            max = vs.Count;
                            for (int i = 0; i < max; i++)
                            {
                                vs[i] = Vector2.MirrorHorizontal(vs[i], verticalAxisLocalX);
                            }

                            vs.Reverse();
                            poly.Set(vs);

                            // Hopefully fixture proxies will refresh its aabb 
                            // based on new vertices.
                            break;

                        case ShapeType.Circle:
                            CircleShape circ = f.Shape as CircleShape;
                            if (circ == null)
                            {
                                continue;
                            }

                            circ.Position = Vector2.MirrorHorizontal(circ.Position, verticalAxisLocalX);
                            break;
                    }
                }
            }


            // need to recalculate mass when shape change
            ResetMassData();
            // required if mirrored per-body.  if mirror comes from spirit, they always update all bodies aabb, so probably UpdateAABB() got called twice 
            UpdateAABB();
#endif


            //TODO future .. theres a way way to treat each as a collection  of ref points  Treat List<AttachPoint> as IEnumerable<ReferencePoint>.. even here is repeat code.
            //flip local coords,  body need to be near 0,0

            AttachPoints.ForEach(x => x.MirrorHorizontal(verticalAxisLocalX));
            EmitterPoints.ForEach(x => x.MirrorHorizontal(verticalAxisLocalX));
            SharpPoints.ForEach(x => x.MirrorHorizontal(verticalAxisLocalX));


            //   MarkPoints.ForEach(x => Vector2.MirrorHorizontal(x.LocalPosition, verticalAxisLocalX));

            //  foreach (AttachPoint ap in AttachPoints)
            // {
            //      MirrorConnectedJoints(verticalAxisLocalX, ap);
            //  }

        }



        //needs to be near 0,0
        //   private static Vector2 MirrorOnYAxis(float verticalAxisLocalX,RefencePoint pt)
        //     {
        //       return Vector2.MirrorHorizontal(pt.LocalPosition, verticalAxisLocalX);
        //    }



        /*   we never do this at run time.   its impossible in the planiverse..  mabye for spawning would be usefull.
        private static void MirrorConnectedJoints(float verticalAxisLocalX, AttachPoint ap)
        {
            // if currently connected
            if (ap.Joint != null)
            {
                // only apply mirror to own anchor. anchor on external body should be ignored.
                if (ap.Parent == ap.Joint.BodyA)
                {
                    ap.Joint.LocalAnchorA = Vector2.MirrorHorizontal(ap.Joint.LocalAnchorA, verticalAxisLocalX);
                }
                else if (ap.Parent == ap.Joint.BodyB)
                {
                    ap.Joint.LocalAnchorB = Vector2.MirrorHorizontal(ap.Joint.LocalAnchorB, verticalAxisLocalX);
                }
            }
        }*/



        /// <summary>
        /// 
        /// </summary>
        /// <param name="minimumEdgeSquaredSize"></param>
        /// <param name="hasOnlyShortFaces"></param>
        public void RemoveSmallFacesOnPolygonalCapsule(float minimumEdgeSquaredSize, out bool hasOnlyShortFaces)
        {
            hasOnlyShortFaces = false;
            try
            {

                Debug.Assert(GeneralVertices != null);//  this should never happen if coded correctly,, thats what assert is for

                if (GeneralVertices.Count <= 4)
                    return;

                if (minimumEdgeSquaredSize <= 0)
                    return;

                Vertices vertices = GeneralVertices;

                FarseerPhysics.Common.HashSet<Vector2> simplerSet = GetVertsThatAreOnALongEdge(minimumEdgeSquaredSize, vertices);

                if (simplerSet.Count < 4)  //this can happen on tiny regrowing parts.. TODO .. should just remove this part.
                {
                    hasOnlyShortFaces = true;   // for now well just have called remove small regrowing bits.
                    return;
                }

                //   if (simplerSet.Count > 4)/// on legs  the first verts to the capsule part is on a long edge so its not removed.    eend up with a shape with 6 pionts.. pointy edges.
                //    {
                //so just do another pass.. the points are not on an ed
                //should not be needed.. dont know why legs allow certain face to pass..
                //    }

                GeneralVertices.Clear();
                GeneralVertices.AddRange(simplerSet);  // order must not be disturbed.. 
                RebuildFixtures();    //this cause AABB to get changed, and possible threading issues since it is AABB is read by graphics thread.. 
                //TODO if this freeze happens again.. consider.. variant of RebuildFixtures thatn does not update AABB .. or just edit the fixture verts
                // this include call to ResetMassData()
                //  we dont want to update view  or set dirty

                return;
            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in RemoveSmallFacesOnPolygonalCapsule" + exc.ToString());
            }

        }

        private static FarseerPhysics.Common.HashSet<Vector2> GetVertsThatAreOnALongEdge(float minimumEdgeSquaredSize, Vertices vertices)
        {
            FarseerPhysics.Common.HashSet<Vector2> simplerSet = new FarseerPhysics.Common.HashSet<Vector2>();  // not sure if structs can go in a hash set..  seems to work tho
            //HashSet<int> vertsOnLongFaces = new HashSet<int>();


            for (int i = 0; i < vertices.Count; ++i)                //code was adapted from loop in    public void Set(Vertices vertices) on polygon shape
            {
                int i1 = i;
                int i2 = i + 1 < vertices.Count ? i + 1 : 0;
                Vector2 edge = vertices[i2] - vertices[i1];

                float edgeLenSq = edge.LengthSquared();

                if (edgeLenSq > minimumEdgeSquaredSize)   //TODO just as in wind drag .. should use the perimter to and some percentag to see if the face is long..
                {
                    simplerSet.CheckAdd(vertices[i2]); //mabye not be optimal, but if only on each body  in spirit on breaking.
                    simplerSet.CheckAdd(vertices[i1]);  // add both verts.. if edge is short , we can safely remove both verts 
                }
            }
            return simplerSet;
        }


        //never is using fixtures from begin in end of lifespan.. so its not selectable.  Fixtures are created on deserialization otherwidse
        bool NeverUsingFixtures()
        {
#if SILVERLIGHT  || PRODUCTION //TODO NOTE  .. Mass and Moment of intertia wont update from density....should not be a problem tho.
            return NotCreateFixtureOnDeserialize
                || (IsNotCollideable &&
                   ((Info & BodyInfo.Cloud) != 0
                  //    || IsStatic      //parts of ground are shapes used for texture.  need fixtures only to be selectable in tool
                    )

                );
#else
            return NotCreateFixtureOnDeserialize;
#endif
        }

        bool NotUsingFixtures()
        {
            //TODO NOTE  .. Mass and Moment of intertia wont update from density....should not be a problem tho.
            return (IsNotCollideable && (Info & BodyInfo.Cloud) != 0);  //TODO 
        }


        /// <summary>
        /// Scale this body and all its contents. Scaling is applied to local coordinate.
        /// All scaling uses world (0,0) as center.  NOTE only scale under 10% or so is safe.  to scale more, repeat scaling after 
        /// running phyiscs a few updates at least.
        /// Reason  is that Body Position   needs to be moved,  we are relying on physics engine and interation to 
        /// keep the joint graph together and position and rotate the connected bodies..  so a sudden scale will teleport and joint constraint might not be able to be solved from that distance. result in jump or pop or explode.
        /// </summary>
        public void ScaleLocal(Vector2 scale)
        {
            if (GeneralVertices != null)
            {
                int max = GeneralVertices.Count;
                for (int i = 0; i < max; i++)
                {
                    GeneralVertices[i] *= scale;
                }
            }

            float scaleScalar = scale.X;  // used for sizes.

            //TODO break into function scale fixture..
            // Generate fixture only when it is collidable
            if (FixtureList != null && !NeverUsingFixtures())
            {
             //   RebuildFixtures(); //expensive .. better scale all the fixture
                scale = ScaleFixtures(scale);
            }

            // need to recalculate mass when shape change
            ResetMassData();

            // attachpoint only need to update local position. 
            foreach (AttachPoint ap in AttachPoints)
            {
                ap.LocalPosition *= scale;

                // if currently connected
                /*         if (ap.Joint != null)   //this code makes sense but causes a held item to drift when hand still growing..
                         {
                             // avoid 2 attachpoint applying multiplier twice to the same joint.
                             if (ap.Parent == ap.Joint.BodyA)// ap.Parent is this?
                             {
                                 ap.Joint.LocalAnchorA *= scale;
                             }
                             else if (ap.Parent == ap.Joint.BodyB)
                             {
                                 ap.Joint.LocalAnchorB *= scale;
                             }
                         }
                 */
            }

            //TODO  one linefer collectio all referece points and then scale
            //one of these was +=  even on

            //TODO could be dangerous .. being added from another thread..
            SharpPoints.ForEach(x => ScaleRefPoint(x, ref scale));
            EmitterPoints.ForEach(x => ScaleRefPoint(x, ref scale));

            //_markCollectingSkipped = true;// TODO fix  this is still not safe.. control can already be insde the add mark method.
            //I think  this is safe.. these are added from the physics threads.. ( controller)
            // while sclae occurs in the UI thread.. while physics is locked.
            VisibleMarks.ForEach(x => ScaleRefPoint(x, ref scale));
            //_markCollectingSkipped = false;
            // this cause troubles on regen when joint list is not yet collected.
            //NOTE  even JointList is determined and  this is done  before adding to physics ( when the jointEdges are set up) .. the issue is that Position of each Body rel to its joined neigbors would need to be adjusted.
            //to do that needs lot of math.. consider sin/ cose each joint angle, rotation , local CS .. etc.
            //so for now.. only small scales work at a time.                    
            ScaleJointList(JointList, scale);
            // set new dress scale here
            DressScale *= scale;
            DressScale2 *= scale;

        }

        private Vector2 ScaleFixtures(Vector2 scale)
        {
            foreach (Fixture f in FixtureList)
            {
                switch (f.ShapeType)
                {
                
                    case ShapeType.Polygon:
                        PolygonShape poly = f.Shape as PolygonShape;

                        if (poly == null)
                            continue;

                        Vertices vs = new Vertices(poly.Vertices);
                        int max = vs.Count;
                        for (int i = 0; i < max; i++)
                        {
                            vs[i] *= scale;
                        }

                        poly.Set(vs);

                        // Hopefully fixture proxies will refresh its aabb 
                        // based on new vertices.?TODO
                        break;

                    case ShapeType.Circle:
                        CircleShape circ = f.Shape as CircleShape;
                        if (circ == null)
                        {
                            continue;
                        }

                        circ.Radius *= Math.Min(scale.X, scale.Y);
                        circ.Position *= scale;
                        break;

                    default:
                        continue;
                }
            }
            return scale;
        }


        void ScaleRefPoint(ReferencePoint rp, ref Vector2 scale)
        {
            rp.LocalPosition *= scale;
        }

        /// <summary>
        /// Scale the local anchor of all joints connected to this body. Scaling is applied to local coordinate.
        /// </summary>
        public void ScaleJointList(JointEdge je, Vector2 scale)
        {
            // for connected joints, we only need to translate the local coordinate
            while (je != null)
            {
                PoweredJoint pj = je.Joint as PoweredJoint;

                if (pj != null)
                {
                    if (pj.BodyA == this)
                    {
                        //pj.LocalAnchorA = ScaleJointLocalAnchorAndEmitter(scale, pj.WorldAnchorA, pj.LocalAnchorA);
                        pj.LocalAnchorA *= scale;
                    }
                    else if (pj.BodyB == this)
                    {
                        //pj.LocalAnchorB = ScaleJointLocalAnchorAndEmitter(scale, pj.WorldAnchorB, pj.LocalAnchorB);
                        pj.LocalAnchorB *= scale;
                    }
                }

                WeldJoint wj = je.Joint as WeldJoint;
                if (wj != null)
                {
                    if (wj.BodyA == this)
                    {
                        wj.LocalAnchorA *= scale;
                    }
                    else if (wj.BodyB == this)
                    {
                        wj.LocalAnchorB *= scale;
                    }
                }

                je = je.Next;
            }
        }


        //private Vector2 ScaleJointLocalAnchorAndEmitter(Vector2 scale, Vector2 jointWorldAnchor, Vector2 jointLocalAnchor)
        //{
        //    //TODO fix future
        //    //   pj.SensorSize *= scaleScalar;  //will this prevent scale being applied twice?
        //    //this so that tiny legs cant be so easily nicked off                  
        //    foreach (Emitter em in EmitterPoints)
        //    {
        //        //TODO why cant we just scale all the emitter LocalPosition on this body outside this loop

        //        // check closest emitter to joint
        //        float tocenter = em.LocalPosition.Length();
        //        float tojoint = (em.WorldPosition - jointWorldAnchor).Length();
        //        if (tojoint < tocenter)
        //        {
        //            em.LocalPosition = jointLocalAnchor * scale;
        //        }
        //    }

        //    return jointLocalAnchor * scale;
        //}


        /// <summary>
        /// Reset some state when transfering body between physics world.
        /// </summary>
        public void ResetStateForTransferBetweenPhysicsWorld()
        {
            // if reference to previous physics World not removed, it might still in memory
            World = World.RecentInstance;
            // when traveler body transfered between level, it might still have 
            // invalid jointlist from previous physics.
            // if not reset, it will cause infinite loop on GraphWalker.WalkGraph2() .
            JointList = null;

            ContactList = null;

            // this is to fix bug on ghost spirit (passing through level).
            // all new fixtures must get proper collision group id.
            FixtureList = null;
            RebuildFixtures();

            // if desired to stop moving movement on new level 
            // comment out if preferred to maintain velocity  from previous level.
            //ResetDynamics();

            // body emitters need to get new world reference, else will throw dynamic tree leaf exception
            foreach (Emitter em in EmitterPoints)
            {
                BodyEmitter be = em as BodyEmitter;
                if (be != null)
                {
                    be.World = World.RecentInstance;
                }
            }
        }

        #region IEntity Members


        /// <summary>
        /// This is called on UI thread after physics is fished a frame , locked and waiting.
        /// </summary>
        /// <param name="dt"></param>
        public virtual void Update(double dt)
        {

            CalculateAcceleration();
#if ACCESS_LAST_FRAME   
            XfLastFrame = Xf;  //  keep a copy of last frame.    when collision handlers PostSolve or OnCollided , it will have already changed position for this body
#endif

            if (_emitterPoints != null)
            {
                _emitterPoints.ForEach(emitter => { emitter.Update(dt); });
            }

            UpdateVisibleMarks(dt);
        }

        private void UpdateVisibleMarks(double dt)
        {
            if (_visibleMarks != null && _visibleMarks.Count != 0)
            {
                List<MarkPoint> deadMarks = new List<MarkPoint>();

                //_visibleMarks.ForEach(point => { point.Update(dt); });
                foreach (MarkPoint point in _visibleMarks)
                {
                    point.Update(dt);
                    if (point.IsDead)
                    {
                        deadMarks.Add(point);
                    }
                }

                deadMarks.ForEach(point => { _visibleMarks.Remove(point); });
            }
        }

        protected void CalculateAcceleration()
        {
            Acceleration = LinearVelocity - LinearVelocityPreviousFrame;
            LinearVelocityPreviousFrame = LinearVelocity;
        }


        public AABB EntityAABB
        {
            get { return AABB; }
        }

        public void OnDeserialized()
        {
        }

        #endregion


        /// <summary>
        /// Returns an angle from ranging for -pi to pi  from  vector relative to the body, can be used for Target angleon a joint anchored to body
        /// </summary>
        /// <param name="targetVec"></param>
        /// <returns>angle to body</returns>
        public float AngleToBody(Vector2 targetVec)
        {
            targetVec = GetLocalVector(ref targetVec);
            //TODO UPSIDEDOWNWORLD
            //if our positive Y was up like normal Euclidean plane we are taught with.. then it would be Atan2(targetVec.Y, targetVec.X);
            float theta = (float)Math.Atan2(/*neg needed since our Y is flipped*/ -targetVec.Y, targetVec.X);
            return theta;
        }

        //future .. might speed up cloud generation on UI thread.. or bullets.  reading XAML is slow.
        public object Clone()
        {
            // not a deep clone ..object references are shared.  i think its ok in case of body.  //body color is the only one thats a class, so if not changing color, fine.
            Body clone = (Body)MemberwiseClone();
            return clone;
            //   clone.GeneralVertices??  do we need a deep clone here ie copy array to new array of verts?
        }

        //TODO future .. should probalbly use above instead or  MemberwiseClone   
        /// <summary>
        /// Copy some of the properties , one by one. This is used by breakable body .  Vertices are not cloned.
        /// </summary>
        /// <param name="source"></param>
        public void CopyPropertiesFrom(Body source)
        {
            Position = source.Position;
            Rotation = source.Rotation;
            LinearVelocity = source.LinearVelocity;
            AngularVelocity = source.AngularVelocity;
            Color = source.Color;
            FixedRotation = source.FixedRotation;
            Info = source.Info;
            IgnoreGravity = source.IgnoreGravity;
            SlidingFriction = source.SlidingFriction;
            Friction = source.Friction;
            ZIndex = source.ZIndex;
            Density = source.Density;
            DragCoefficient = source.DragCoefficient;
            ApplyDragAsParticle = source.ApplyDragAsParticle;
            CollisionGroup = source.CollisionGroup;
            SkipWindBlockCheck = source.SkipWindBlockCheck;
            IsNotCollideable = source.IsNotCollideable;

            //TODO particle properties.. cosolidate with other copy for spawn..

        }


        public bool IsSharpWeapon()
        {
            return (((PartType & PartType.Weapon) != 0 && SharpPoints.Count > 0));
        }


        public bool IsWeapon()
        {
            return (((PartType & PartType.Weapon) != 0)
                || (Info & BodyInfo.ShootsProjectile) != 0
                //|| SharpPoints.Count > 0  // some stone has sharp point but not weapon, sharp point is meant to allow sword to deflect.. (TODO remove this hack, dont use sharp point for that)
                );
        }






        #region IEntity Members


        public bool WasSpawned
        {
            get { return (Info & BodyInfo.WasSpawned) != 0; }
        }

        #endregion


        public static PartType GetPartTypeLeftRight(bool left)
        {
            return left ? PartType.Left : PartType.Right;
        }

        public static PartType GetPartTypeFlag(bool left, PartType pt)
        {
            return pt | GetPartTypeLeftRight(left);
        }
    }


    /// <summary>
    /// Will give the same color to all Fixtures on the same Body.
    /// </summary>
    [DataContract(Name = "BodyColor", Namespace = "http://ShadowPlay")]
    public class BodyColor
    {
        public BodyColor() { }

        /// <summary>
        /// Model color , with Alpha .. Alpha 255 means opaque, 0 means transparent.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        public BodyColor(byte r, byte g, byte b, byte a)
        {
            SetRGBA(r, g, b, a);
        }

        public BodyColor(BodyColor color)
        {
            SetRGBA(color.R, color.G, color.B, color.A);
        }

        public void SetRGBA(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        [DataMember]
        public byte R;

        [DataMember]
        public byte G;

        [DataMember]
        public byte B;

        [DataMember]
        public byte A;
    }



    /// <summary>
    /// Type of body piece that participate in Spirit system.   Typically there is one value ,  sometimes with  modifier bit ( Left, Upper) .  For the PartType to show in Tools standard property page, an enum must match or it will be blank
    /// </summary>
    [Flags]
    public enum PartType
    {
        None = 0,
        MainBody = (1 << 2),  //For a creature spirit ( body system) , this is the nexus, usually were the most joints connect
        Left = (1 << 3),
        Right = (1 << 4),
        Middle = (1 << 5),
        Upper = (1 << 6),
        Lower = (1 << 7),

        Thorax = (Middle | MainBody),    //Upper Main Body is spirit main body
        Abdomen = (Lower | MainBody),//Todo fix sprit tool to not loosen spine joints

        Head = (1 << 8),
        Jaw = (1 << 9),

        Neck = (1 << 10),
        UpperNeck = (Upper | Neck),
        LowerNeck = (Lower | Neck),

        Hand = (1 << 11),
        Arm = (1 << 12),
        Foot = (1 << 13),
        Toe = (1 << 14),  //or Hoof , animals walk on toes

        Eye = (1 << 15),

        Leg = (1 << 16),
        Shin = (Lower | Leg),
        Thigh = (Upper | Leg),

        LowerJaw = (Lower | Jaw),
        Both = (Left | Right),

        LeftEye = (Left | Eye),
        RightEye = (Right | Eye),

        LeftFoot = (Left | Foot),
        RightFoot = (Right | Foot),

        LeftToe = (Left | Toe),
        RightToe = (Right | Toe),

        LeftHand = (Left | Hand),
        RightHand = (Right | Hand),

        LeftShinBone = (Left | Shin),
        LeftThighBone = (Left | Thigh),

        RightShinBone = (Right | Shin),
        RightThighBone = (Right | Thigh),

        LeftUpperArm = (Left | Upper | Arm),
        LeftLowerArm = (Left | Lower | Arm),

        RightUpperArm = (Right | Upper | Arm),
        RightLowerArm = (Right | Lower | Arm),

        Control = (1 << 18),   // obsolete soon ?  better use AttachPoint.IsControl property.

        Weapon = (1 << 19),   //TODO parttype is for a parts role in a system.. this shod be BodyInfo as well
        Food = (1 << 20),      //TODO might start using Body info for this.. 
        Container = (1 << 21),
        Rope = (1 << 22),  //segment of a rope
        Stone = (1 << 23),
        RockHandhold = (1 << 24), //for climbing.. TODO clean this.. change to Handhold, bit..  
        Rock = (1 << 25), //  small  stone that can be tossed
        Door = (1 << 26),
        Cloud = (1 << 27),  //TODO use body info for this.. since cloud is a spirit and has a main body..

        Hinge = (1 << 28),  //usually hinge part used to contect two pieces using two jionts 
        Latch = (1 << 29), //used to lock a door

        LiquidOxygen = (1 << 30),
        RocketFuel = (1 << 31), //for a block or blocks used to show a fluid level ( see airship)

        //  xxx = (1 << 17),   //   free BIT.. 

        //   Roof = (1<<32),
        //TODO clean out unused.. cant be above 32 or dotn know what value bits it might add.... Ground = ( 1<<35) 
        Stalagtite = (Upper | Rock),
        Stalagmite = (Lower | Rock),
        Surfboard  //ununsed.. deprecated.. for old files only..   this is not bit, can appear as a rock

    }




    //TODO combine with above , remove Body info .. used only now for    PlayerCharacter..
    // maybe filter PlayerCharacter out of prop page UI combo box..

    /// <summary>
    /// Additional Metadata tag about a body or its parent, used so that bodies can be given special treatment 
    /// use when PartType cannot be used..
    /// </summary>
    /// 
    [Flags]
    public enum BodyInfo
    {
        None = 0,
        PlayerCharacter = (1 << 1),
        SeeThrough = (1 << 2),//AI or eyes can see though this body.  migth be used if theres dress, otherwise check alpha on fill
        InMargin = (1 << 3),//body lives in level  margin, wont be cleaned..
        Cloud = (1 << 4),//body will use a cicular fixture to simply mass data setting..
        Debris = (1 << 5),//future, migth land on ground and become collidable
        UsePointToHandleForDragEdge = (1 << 6),//for kris or other curvy weapon, use a vector from shart point to handle edge for the wind face
        IncreaseDensityOnGround = (1 << 7), //for low density like leave, attach handlers and increase density when lying on ground, so that stepping doesn not create contraint solver issues.

        DebugThis = (1 << 8),  //to mark body of interest for debugging.
        Food = (1 << 9),  //   means visible as food , will be hunted.  
        Container = (1 << 10),  //   might contion  ( like url or hull some oject of greater mass 

        //  Magnetic = ( 1 <<10)
        //Steel = ( 1 <<10)   // stick more to better steel? check magnitism law  sword.. and spear..
        // Glue
        //Oxidant  this + that = gunpowder
        //Combustible
        //HIghexplosive gunpowder ignite this..

        Sometimes = (1 << 11),   //object is there sometimes..
        Liquid = (1 << 12),
        Solid = (1 << 13),

        UseSingleDragEdgePanel = (1 << 14),  //  for balloon or bone  only cast one blocking ray.. ingnore self
        Surfboard = (1 << 15),

        KeepVisible = (1 << 16),   //for this body dont remove the view if out of viewport. 
        ShootsProjectile = (1 << 17),//for gun.. so that it can be aim and treated as special weapon by 

        CollideAll = (1 << 18),
        WasSpawned = (1 << 19),   //to indicate not to save with level .. or clean will remove it.

        SpawnOnly = (1 << 20),  // used on emitter. dont apply properties from emitter to spawned body
        UseSingleDragEdgePanelCollideAll = (CollideAll | UseSingleDragEdgePanel),

        PlayerCharacterSpawed = (SpawnOnly | PlayerCharacter),

        NotReuseableDress = (1 << 21),  //a mark dont copy this dress when repeating testure.
        NotReuseableDressUseSingleDragEdgePanel = (NotReuseableDress | UseSingleDragEdgePanel),  //just so it can show in drag panel..

        Bullet = (1 << 22),

        //Spark = (1 << 14),      // flame in ballon

        UseEdgeShape = (1 << 23),
        UseLoopShape = (1 << 24),   // chain shape in box2d
  

    }


    //TODO add a material.   ropesegment and chain segment
    //TODO add a hardness

   #endregion



}