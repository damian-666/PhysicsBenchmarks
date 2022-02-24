
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

//#define  ACCESS_LAST_FRAME   enable function to access Body postion from last frame  ( save xform) //TODO ERASE  //PostSolve, ONcollide, etc.. is fixd now..


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.ComponentModel;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
//using FarseerPhysics.Common.PhysicsLogic; //shadowplay mod
using FarseerPhysics.Controllers;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;


#region ShadowPlay Mods
using UndoRedoFramework;
using FarseerPhysics.Dynamics.Particles;
using Core.Data.Collections;
using FarseerPhysics.Factories;
using Core.Data.Interfaces;
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

        //Shadowplay mods   non persistent flags
        IsSpent = (1 << 8),//use this to tell application body is used up.
        LastBullet = (1 << 9),  //last ammo in gun, don't waste it
        IsInUse = (1 << 10),
        IsStaticForSleep = (1 << 11),  // used for items forced to sleep as static due to joints and piling never getting stable.. airship, etc.
        IsSharpWeaponHeldByAI = (1 << 12), // used for tunneling check on blocking two joined object.
        IsSharpWeaponHeldByPC = (1 << 13),
        Magnetized = (1 << 14), // is  under the influence of a magnetic field ( stuck to magnet) 
        StopToBlockWind = (1 << 15), //blocking ray will stop at this.  used for bodies that produce wind fields,    when set  will  fail to block external winds but its probably moving and doesnt matter,  used by rocket engine, gun  might use it
        DontBreakJointOnBlocking = (1 << 16), // ray between joints wont cause breakage with this, used for toe straps and other grips can could break hands
        InWaterAdjustment = (1 << 17) //    for items like swords that are not stable in water..TODO HACK FIX.. consider removing this state

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
    public class Body : NotifyPropertyBase, IEntity   //, ICloneable
    {
        private static int _bodyIdCounter;
        internal float AngularVelocityInternal;

        #region ShadowplayMods

        const float _maxStuckParticleSizeBlood = 0.018f;  //for blood make smaller until ellipse is ready  TODO remove this after ellipse
        const float _maxStuckParticleSize = 0.022f;  //make bigger for dust.. not sure if oval is good for that  blood..

        private bool _isShowingDress2;

        //if anther thread adding a mark effect just skip it. since this for performance don't use sync. any particle effect can be 
        //TODO OPTIMIZE possible optimization future maybe for stab wounds , should use the lock or other syc       


        public bool IsShowingDress2
        {
            get
            {
                return _isShowingDress2;
            }
            set
            {

                if (string.IsNullOrEmpty(DressingGeom2))
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
        public int BodyId;// merged in from latest farseer.. just a unique id.. ( might not be unique after insert body from one level to another)

        public ControllerFilter ControllerFilter = new ControllerFilter();

        // internal  BodyFlags Flags;   shadowplay mod below .. make external so client can use them
        public BodyFlags Flags;

        #region ShadowPlay Mods..
        internal protected Vector2 Force; //will be cleared after step
        private float _area;
        private float _perimeter;

        public const float MinAccel = 1.10f;  //TUNED for stuff resting on ground in Wind.. stuff wont budge anyways unless accel is more than this.
        public const float MinAccelSq = MinAccel * MinAccel;

        public Vector2 LinearVelocityPreviousFrame; //Note this is only valid after one frame.. on emitted we calculate via force
        public Vector2 Acceleration = Vector2.Zero;

        ///Flags containing metadata, special information so that bodies in different use can be treated differently in low level pnysics code

        public BodyInfo _info;
        public static bool NotCreateFixtureOnDeserialize = false;  //bad HACK reallly and mabye not appropriate.. optimized for nto calling syncronize fixture but it mayh have those... originally for loading items just for dress originally, for those to be drawn but not ready to spawn.. to avoid putting fixtures too early before spawning and moving.. an optimization for visual items that dont update the tree like particle. or without expecitnve a world or to do anytig with fixtures .  //TODO hack.. why is it a body then.. i guess it needs one to track the  views.  all bodies are maped to views.  TODO.. justkeep a list of views, with references to Bodys as members , ( or null).. no more Dummy bodies needed 

        [DataMember]
        public BodyInfo Info
        {
            get { return _info; }
            set
            {
                if (_info != value)
                {
                    _info = value;
                    NotifyPropertyChanged("Info");
                }
            }
        }

        /// <summary>
        /// TotalContactForceOnThis, added up, can be considered pressure, from other Bodies.  doesn't consider other forces, friction, tangential force, gravity,  wind.   From last frame since it is used by joints.  Was from last frame, post solve everything, joints, collisions , and TOI.
        /// </summary>
        public float TotalContactForce
        {
            get
            {
                return _totalContactForceOnThis;
            }
            set
            {
                _totalContactForceOnThis = value;
                NotifyPropertyChanged("TotalContactForceOnThis");
            }
        }

        protected float _totalContactForceOnThis; //shadowplay mod..   desire is to shows total contact forces after solving.  attempt to determine pressure on a body.. TODO add joints..


        //private Vector2 _averageVel;
        #endregion

        internal float InvI;
        public float InvMass;  //shadowPlay mod expose this
        internal Vector2 LinearVelocityInternal;
        ///   public PhysicsLogicFilter PhysicsLogicFilter = new PhysicsLogicFilter();
        internal float SleepTime;
        internal Sweep Sweep; // the swept motion for CCD
        internal float Torque;
        public World World;
        public Transform Xf; // the body origin transform  //shadowPlay mod expose this
        private BodyType _bodyType;
        private float _inertia;

        #region ShadowPlay Mods

        private UndoRedo<float> _mass;  // original: private float _mass;

#if ACCESS_LAST_FRAME
       shadowplay Mod internal Transform XfLastFrame; // the body origin transform.. last frame .. could be used for onCollide, or put a Report before the Contacts are solved and applied to the body state
#endif
        #endregion

//TREEVIEW TEST.. should not be needed we pass a IEntity colleciton to the as the itemsource, but it makes the treeview work TODO CLEAN    see  tree items datatemplate tool mainwindow
        public string Name { get => "Part:" + PartType.ToString(); }//too lazy to make a wpf converter if thats whats needed
        public string PluginName { get => ""; }
        public string Type { get => typeof(Body).Name; }

        public Body(World world)
            : this(world, null)
        {
        }



        //shadowplay Mod
        public const float DefaultDrag = 0.4f;
        public Body(World world, Object userData)
        {

            //TODO future.. 
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
            //   UserData = userData;  shadowplay mod, not used

            FixedRotation = false;
            IsBullet = false;
            SleepingAllowed = true;
            Awake = true;
            BodyType = BodyType.Static;
            Enabled = true;

            Xf.R.Set(0);

            world.AddBody(this);

            DragCoefficient = DefaultDrag; //shadowplay Mod
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

                if (!NotCreateFixtureOnDeserialize)
                {

                    // Since the body type changed, we need to flag contacts for filtering.
                    for (ContactEdge ce = ContactList; ce != null; ce = ce.Next)
                    {
                        ce.Contact.FlagForFiltering();
                    }
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
        ///  used to determine how much pressure a body is under when squeezed.   also size, restitutoin, strenght ( # of atoms or parts to be compressed, should be a factor)
        /// </summary>
        //     public float LinearPressureY {get;set;}   //the minimum of the  sum all positive impulses  ( cm pos should be considered tho)  as in the solver, and the sum of all negative impluses..
        // {
        ///      get { return _linearVelocityInternalAbs; }
        // }

        //     public float Torsion {get;set;}   //the minimum of the  sum all CCW torques, and the sum of all CW torques
        // {
        ///      get { return _linearVelocityInternalAbs; }
        // }





        //   public Vector2 Jerk { get; set; }  //rate of change of accel.. better not use props for release.
        // {

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
        [DataMember(Name = "Active", Order = 6)]  //TODO get rid of this order stuff.. add to broad phase after load, consider use BodyTemplate like velcro 
        public bool Enabled
        {
            set
            {
                if (value == Enabled)
                {
                    return;
                }

                try
                {



                    FirePropertyChanging();
                    //TODO FIX FUTURE .. SEE HOW EDGE or LOOP WORKS..IMPORTANT  ..  this should probably be be done after all the model is  loaded.
                    // all that setting of order is awful.Order = 6), best to redo serialization like velcro phyiscs or aeither, with proxy
                    //getting ghost fixtures , etc.  on emit pickaxe or bullet..on preload..or even on load..check levle 3b emitt pixaxe 3 meter to right of guy.
                    if (value)
                    {
                        Flags |= BodyFlags.Enabled;


                        if (!(IsNotCollideable || FixtureList == null || World == null))//shadowplay mods
                        {
                            // Create all proxies.
                            BroadPhase broadPhase = World.ContactManager.BroadPhase;
                            for (int i = 0; i < FixtureList.Count; i++)
                            {
                                FixtureList[i].CreateProxies(broadPhase, ref Xf);
                            }
                        }
                        else
                        {
                            return;
                        }

                        // Contacts are created the next time step.
                    }
                    else
                    {
                        Flags &= ~BodyFlags.Enabled;


                        if (!(IsNotCollideable || FixtureList == null || World == null))
                        {
                            DestroyCollisionDataAndFixtureProxies();
                        }
                    }

                        FirePropertyChanged();

                }
                catch (Exception exc)
                {
                    Debug.WriteLine("errro setting the Enabled" + exc.ToString());  //TODO PHONE happens in windows phone 
                }


            }
            get { return (Flags & BodyFlags.Enabled) == BodyFlags.Enabled; }
        }

        private void DestroyCollisionDataAndFixtureProxies()
        {
            BroadPhase broadPhase = World.ContactManager.BroadPhase;

            if (FixtureList != null)
            {
                for (int i = 0; i < FixtureList.Count; i++)
                {
                    FixtureList[i].DestroyProxies(broadPhase);
                }
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
                NotifyPropertyChanged("FixedRotation");
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
                if (_ondeserializing && value == null)
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
        public object UserData { get; set; } // shadowplay mod , not used

        /// <summary>
        /// Get the world body origin position.
        /// </summary>
        /// <returns>Return the world position of the body's origin.</returns>
        [DataMember(Order = 7)]
        public Vector2 Position
        {
            get { return Xf.Position; }

            set
            {
                if (_ondeserializing)
                {
                    SetTransformIgnoreContacts(ref value, Rotation);
                }
                else
                {
                    SetTransform(ref value, Rotation);
                }
            }
        }


        /// <summary>
        /// Get the angle in radians.
        /// </summary>
        /// <returns>Return the current world rotation angle in radians.</returns>
        [DataMember(Order = 8)]   //TODO remove this serialization tag...but then we have to resave old levels..anyways Angle will override any wound values  since this order is after.  ( I verified this) 
        public float Rotation
        {
            get { return Sweep.A; }
            set
            {
                if (_ondeserializing)
                {
                    SetTransformIgnoreContacts(ref Xf.Position, value);
                }
                else
                {
                    SetTransform(ref Xf.Position, value);
                }
      
            }
        }


        /// <summary>
        /// Rotation from 0 to 2pi radians, 0 is vert
        /// </summary>
        //[DataMember(Order = 9)]   TODO put this back, fix unwinding in SetTarget again, retest namiad.wyg..
        public float Angle
        {
            get { return MathUtils.WrapAnglePositive(Rotation); }   //shadowplay mod.. moved from MathHelper to MathUtis  ( in Math.cs)
            set { Rotation = value; }
        }

        [Category("Collision")]
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

                if (
                    //!_ondeserializing&&
                    _bodyType != BodyType.Dynamic)
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


                if (Mass != value)
                {
                    _mass.ValueUndoable = value;
                    if (_mass.ValueUndoable <= 0.0f)
                    {
                        _mass.ValueUndoable = 1.0f;
                    }

                    InvMass = 1.0f / _mass.ValueUndoable;

                    NotifyPropertyChanged("Mass");
                }

                //    MassOrig = value;  //TODO JOINTSTRENGTHEN.. TODO USE OR ERASE..
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

        //shadowplay mod  JOINTSTRENGTHEN
        //to use for restoration if we need to tweak the mass so that joints wont have errors under huge load imbalances.   Should not cause a problem
        //if the model is saved, since we are not even changing Mass property, just the cached 1/Mass, besides, Mass is overwritten by Density, and
        //We usually use the Density as the model.   Undoable properties were meant for A/B tuning , but the property sheet I believe does not make 
        //use of it.  Note, we change farseer to not use the Mass for gravity, use this...TODO check buoyancy..
        //   public float MassOrig;

        /// <summary>
        /// Used in the case of large joint error , due to being low in mass , but connected to a body high in mass.  Alwyas a problems with rope joints under strain, 
        /// The RopeJoint is used in the simple case of a rope being stretch in a straight line, but no use around corners,  in a balloon holding a heady load in winds, or in 
        /// a strained collision, we see errors  .. for example when head is press on ground by body, the eyes pop out, due to their low mass and some some reason, being part of the joint graph.
        /// The solutin is to raise the mass of the bodies attached.. This should not cause a visible issue since the bodies are usually not moving, being compressed, and gravity will not use this 
        /// adjusted value.
        /// </summary>
        /// <param name="fakemass"></param>
        //   public void SetTempMassForJoints ( float fakemass)  //temporary increase to make a joint solid, 
        //   {
        //      //  InvMass = 1 / fakemass;  //does  not need undoable, back door it.. , , or the effect of gravity.. just InvMass
        //      //  //get it back to 1/ MassOrig ASAP

        //        MassOrig = Mass;
        //        Mass = fakemass;
        //     }



        void _mass_UndoRedoChanged(object sender, UndoRedoChangedType type, float oldState, float newState)
        {

            InvMass = 1.0f / Mass;  //TODO mass undo   do Intertia..
                                    // InvI = ??   TODO ajsut this.. should be proportinal to mass..I = M R2
                                    //  InvI  = InvI / oldState/ newState;  TODO..FIX..

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
            fixture.ClearOnCollisionListeners(); //clear listeners  //shadowplay mod  

#if DEBUG
            // You tried to remove a fixture that not present in the fixturelist.
            Debug.Assert(FixtureList.Contains(fixture));
#endif

            ClearCollisionData(fixture);

            if ((Flags & BodyFlags.Enabled) == BodyFlags.Enabled&& World != null)
            {
                BroadPhase broadPhase = World.ContactManager.BroadPhase;
                fixture.DestroyProxies(broadPhase);
            }

            FixtureList.Remove(fixture);
            fixture.Destroy();
            fixture.Body = null;

            ResetMassData();
        }


        public void ClearCollisionData()
        {
            FixtureList?.ForEach(x =>this.ClearCollisionData(x));

        } 

        private void ClearCollisionData(Fixture fixture)
        {
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


            if (!IsNotCollideable && Enabled && World != null) //   shadowplay mods
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


            if (Body.NotCreateFixtureOnDeserialize)
                return;

            if (World == null)
                return;

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
            ApplyForce(ref force, ref Sweep.C);
        }

        /// <summary>
        /// Applies a force at the center of mass.
        /// </summary>
        /// <param name="force">The force.</param>
        public void ApplyForce(Vector2 force)
        {
            ApplyForce(ref force, ref Sweep.C);
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

            float accelSq = force.LengthSquared() / (Mass * Mass);   //TODO JOINTSTRENGTHEN use origMass?
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
            if (dVel != impulse * InvMass)
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
                // quick hack to fix when _inertia < 0.   TODO CODE REVIEW quick fix Needed for Breakable body.. probably due to winding of verts wrong way.
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

            NotifyPropsOnShapeChanged();
        }

        private void NotifyPropsOnShapeChanged()
        {
            NotifyPropertyChanged("Mass");
            NotifyPropertyChanged("Area");
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
        /// Like GetLocalPoint,  except using last frames postion and rotation.  This could be  useful OnCollided.. since object is moved already by solver.  NOTE TODO ERASE.. Collided and Collision are there.  
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <returns></returns>
        public Vector2 GetLocalPointPreviousFrame(ref Vector2 worldPoint)
        {
            return new Vector2((worldPoint.X - XfLastFrame.Position.X) * XfLastFrame.R.Col1.X + (worldPoint.Y - XfLastFrame.Position.Y) * XfLastFrame.R.Col1.Y,
                               (worldPoint.X - XfLastFrame.Position.X) * XfLastFrame.R.Col2.X + (worldPoint.Y - XfLastFrame.Position.Y) * XfLastFrame.R.Col2.Y);
        }

#endif


        /// <summary>
        /// push the verts away from CM of body so that feet bottoms and such do not appear to touch water.
        /// NOTE  .. splashes and bubbles can come from sync issues ass well..  CONCURRENCY.   also this maybe  importance for airdrag moving up boats in waves....so that a large wave can lift a stationary large vessel ..  water is moving up fast tho..so should not be an issue..
        /// </summary>
        /// <param name="point"> vertex location</param>
        /// <param name="expansion">expansion in meters</param>
        ///  <param name="body">Body body</param>
        /// <param name="i"></param>
        /// <returns></returns>
        public Vector2 ExpandVertexToFattenRegion(Vector2 point, float expansion, int i)
        {
            Debug.Assert(Normals != null && Normals.Count != 0);
            return point - expansion * Normals[i];
        }

        //TODO should expand General Vertices .. remove all those strokes.  This will solve the slop issue.
        //check carefully the wind.. and offsets there.    this way we dont have to do this
        //will have to be a flag so that old levels still work... ones that have strokes on everything..
        public Vertices GetExpandedGeneralVertices(float expansion)
        {

            //TODO for cuts we want to use the cut face ..
            //TODO   for .. expand away from center of mass..    this might not be good with sharp corners.
            //see shapecode.    ShapeFactors    might be a way  with dot prodocut no square roolt
            CalculateNormals(); //this is done right after cut..so we have no normals yet.. until its affected by wind.
            Vertices vertices = new Vertices();

            //TODO  add a scale around center.. this is the best way to expand the polygon.. factor out with Body scale..
            for (int i = 0; i < GeneralVertices.Count; ++i)  //TODO OPTIMIZE  can be done in parallel using parallelization lib in loop
            {
                vertices.Add(GeneralVertices[i] + expansion * Normals[i]);
            }

            return vertices;
        }


        public void CalculateNormals()
        {

            if (Normals == null)
            {
                Normals = new Vertices(GeneralVertices.Count);
            }
            Vertices vertices = GeneralVertices;
            for (int i = 0; i < vertices.Count; ++i)
            {
                int i1 = i;
                int i2 = i + 1 < vertices.Count ? i + 1 : 0;
                Vector2 edge = vertices[i2] - vertices[i1];

                Vector2 normal;
                normal = new Vector2(edge.Y, -edge.X);  // or cross product -1, edge
                normal.Normalize();
                Normals.Add(normal);
            }
        }
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

            if (World == null)
                return;

            Transform xf1 = new Transform();
            float c = (float)Math.Cos(Sweep.A0), s = (float)Math.Sin(Sweep.A0);
            xf1.R.Col1.X = c;
            xf1.R.Col2.X = -s;
            xf1.R.Col1.Y = s;
            xf1.R.Col2.Y = c;

            xf1.Position.X = Sweep.C0.X - (xf1.R.Col1.X * Sweep.LocalCenter.X + xf1.R.Col2.X * Sweep.LocalCenter.Y);
            xf1.Position.Y = Sweep.C0.Y - (xf1.R.Col1.Y * Sweep.LocalCenter.X + xf1.R.Col2.Y * Sweep.LocalCenter.Y);

            BroadPhase broadPhase = World.ContactManager.BroadPhase;


            if (FixtureList == null)
                RebuildFixtures();


            if (FixtureList == null)
            {
                Debug.WriteLine(" CANT rebuild fixtures");
                return;
            }


            for (int i = 0; i < FixtureList.Count; i++)
            {
                #region ShadowPlay Mods
#if  PRODUCTION //in tool, we need to be able to select the body.  This logic is per body also, keep it in case fixture select of internal triangles is desired.
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


        public event OnCollisionEventHandler OnCollision
        {
            add
            {
                if (FixtureList != null)
                {
                    for (int i = 0; i < FixtureList.Count; i++)
                    {
                        FixtureList[i].OnCollision += value;
                    }
                }
            }
            remove
            {
                if (FixtureList != null)
                {
                    for (int i = 0; i < FixtureList.Count; i++)
                    {
                        FixtureList[i].OnCollision -= value;
                    }
                }
            }
        }


        public event AfterCollisionEventHandler AfterCollision
        {
            add
            {
                if (FixtureList != null)
                {
                    for (int i = 0; i < FixtureList.Count; i++)
                    {
                        FixtureList[i].AfterCollision += value;
                    }
                }
            }
            remove
            {
                if (FixtureList != null)
                {
                    for (int i = 0; i < FixtureList.Count; i++)
                    {
                        FixtureList[i].AfterCollision -= value;
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
        ///  hitpoint in world, direction, power
        /// </summary>
        public Action<Vector2, Vector2, float> OnLaserStrike;

        /// <summary>
        /// General Vertices Store the outer Edge of  the polygon for collision detection for this 
        /// Rigid body.  Its described in Local (Body ) coordinates, relative to the first point used to define it
        /// If it is  a concave polygon,  it is decomposed into fixtures, each of which is a triangle.
        /// </summary>
        //[DataMember (Order =1)]  fixing will break all old level;
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
                return _gradientBrush;
            }
            set
            {
                _gradientBrush = value;
                NotifyPropertyChanged("GradientBrush");
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



        public static PartType SetPartDir(PartType pt, bool left)
        {

            pt &= ~(left ? PartType.Right : PartType.Left);
            pt |= (left ? PartType.Left : PartType.Right);

            return pt;
        }

        public static PartType UnSetPartDir(PartType pt, bool left)
        {
            pt &= ~(left ? PartType.Left : PartType.Right);
            return pt;
        }


        public void SetPartDir(bool left)
        {
            PartType = SetPartDir(PartType, left);
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
                //      return;  HACK TODO REMOVE THIS modification should check it and fix the "syncing issue"

                _collisionGroup = value;

                if (FixtureList != null)
                {
                    foreach (Fixture f in FixtureList)
                    {
                        if (f.CollisionFilter != null)
                        {
                            f.CollisionFilter.CollisionGroup = value;
                        }
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


        //TODO CODE REVIEW FUTURE CollidesWith  clean this out, resave levels.    only thing this dones its not update tree..  its a hack and sucks anyways
        //just set collides with none.. IsNotCollidable in production
        private bool _isNotCollideable = false;

        [Category("Collision")]
        [DataMember]
        /// <summary>
        ///  Will not collide with anything or update broad phase (for performance in production build) .  For tool bodies are 
        ///  stin the treeu will to allow selection .
        /// </summary>
        public bool IsNotCollideable
        {
            get { return _isNotCollideable; }

            set
            {

             //   Debug.Assert(value != false && value != _isNotCollideable);

                //NTE . can't just set this to false repeatedly  well get errro in CheckToCreateProxies puts tree in unstable state.   in level6.. level 1.. using this property cause weird ression bad proxy.. quiety and weird error result in motion..   fix regrssion..isnotcollidabel is a hack and optimiztion..the setter cant be toggled..so only set it to true if the cloud not moving , not to false..test that i can bust cloude level 6 and stil lbreak in level 1..tis ok just for first release that is like the original ported..not too many other chagnes form silverlight shipping is wise..best fix woud be to either elimate the isnot collidable and dont use body as particle or finishy it so it can be toggled..or set in a loop..just chekc it its really chang e and if nedds to rebuild its cache..
                //   if ( value != _isNotCollideable )  //TODO bc of serialization im afriad to just fix this now
                _isNotCollideable = value;

                if (FixtureList == null)
                    return;

                foreach (Fixture f in FixtureList)
                {
                    f.CollisionFilter.CollidesWith = (value == true ? Category.None : Category.All);
                }

                if (value == false)
                {
                   CheckToCreateProxies();
                }


#if REVISIT  //it should be like this but things get broken, reverting.. issue is we abuse the enging and used 
// body for way to much stuff.. particles that dont collide could be in another collection entirely
//since we have wind and everything is physical it made sense to collect everythign as bodies evnet if not having fixtures

if (_isNotCollideable != value)
                {

                    FirePropertyChanging();

                    _isNotCollideable = value;

                    if (this._ondeserializing && Body.NotCreateFixtureOnDeserialize)
                        return;// dont do anything w fixtures now

                    if (this is Particle)
                        return;

                    if (value == false)
                    {


                        DestroyCollisionDataAndFixtureProxies();///we might be using AABB fixture to select in tool, safest to wipe clean
                        DestroyAllFixtures();
    

                        RebuildFixtures();  //TODO if the verts have changed..
                        CheckToCreateProxies();

                           foreach (Fixture f in FixtureList)
                        {
                            f.CollisionFilter.CollidesWith = Category.All;
                        }

                }
                else
                    {



//here we would clear all complex fixtures and replace with an AABB thing just to sleect in tool
                        DestroyCollisionDataAndFixtureProxies();

                        if (GeneralVertices != null && GeneralVertices.IsConvex() )//we have no need for fixtures if we arent filling a concave body
                        {
                            DestroyAllFixtures();
                        }


#if !PRODUCTION
					    if (GeneralVertices != null)
						{

	                        UpdateAABB();

	                        //make a fixture just for tool to select or we lose control of the item
	                        CreateRectFixtureFromAABB( (this.Info & BodyInfo.Cloud) == 0 ? -0.2f: 0 );

						}

#endif


                        foreach (Fixture f in FixtureList)
                        {
                            f.CollisionFilter.CollidesWith = Category.None;
                        }



                    }

                    FirePropertyChanged();

                }
#endif




            }


        }

        public void CheckToCreateProxies()
        {

            if ((Flags & BodyFlags.Enabled) == BodyFlags.Enabled)
            {
                BroadPhase broadPhase = World.ContactManager.BroadPhase;

                if (FixtureList == null)
                {
                    if (RebuildFixtures() == false)
                        return;
                }

                foreach (Fixture f in FixtureList)
                {
                    if (f.ProxyCount == 0)
                        f.CreateProxies(broadPhase, ref Xf);
                }
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


        //This overhead of unused stuff might affect particle creation speed, mem use..
        //if not placed in tree.  consider derived Body from Particle, owning, interfacing, or best, using particle pool.
        internal List<AttachPoint> _attachPoints;
        [DataMember]  //TODO future.. this should have been undoable collection..  if fixed consider the serialization..  or put that in a view model.  
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



        //TODO simarly convert these AttachPoints  to UndoableCollection like entities is in, then allow copy paste 

        //NOTE TODO the serialization should maybe use BodyTemplate, not sure if serializing the joint graph is ideal
        //Body Template or maybe BodyData is seen in Velco, Farseer demos, it is just the persistant parts of body, easier to load fixtures , then put to  the broadphase later
        //Body Data as separate would make switching Physics engine to bullet sharp easier
        // cons, this whole thing is based on the physics engine, and we can serialize the body with the joints collected as a graph using standard serialization


        internal List<Emitter> _emitterPoints;

#if !UNIVERSAL
        /// <summary>
        /// Legacy, use Emitters that is observable
        /// </summary>
        [Browsable(false)]   //hide from property inspector to remove clutter, access via standard Emitters
#endif

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
            set { _emitterPoints = value; } //required for deserialization only, rather have this readonly
        }

        ObservableCollectionUndoable<Emitter> _emittersUndoable;

        /// <summary>
        /// Undoable Observable Collection of Emitter references, can be BodyEmitters or LaserEmitter points
        /// Readonly
        /// </summary>
        public ObservableCollectionUndoable<Emitter> Emitters
        {
            get
            {
                if (_emittersUndoable == null)
                {
                    _emittersUndoable = new ObservableCollectionUndoable<Emitter>(EmitterPoints);
                }

                return _emittersUndoable;
            }
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



        static public  bool MarkPoints = true;

        /// <summary>
        /// AttachParticleAsVisualMark this is added when a particle strikes an object and sticks to it,  thread safe using locks.. called from background thread collider in winddrag
        /// </summary>
        /// <param name="particle"></param>
        /// <param name="worldpos">position in which this mark should appear. in world coord.</param>
        public MarkPoint AttachParticleAsVisualMark(Particle particle, Vector2 worldpos, Vector2 normal)
        {

            if (!MarkPoints)
                return null;

            MarkPoint markpoint;

            try
            {
                worldpos += normal * EdgeStrokeThickness / 2.0f;  // sometimes stroke is used for snow or moss or dirt to make collision appear closer to feet.  
                //attach particle at edge including stroke             
                //Vector2 localpos = GetLocalPoint(particle.WorldCenter);
                Vector2 localpos = GetLocalPoint(worldpos);

                markpoint = GetMarkPoint(localpos);
                

                // for some strange reason direction have to be perpendicular (90 degree)
                Vector2 normalRotated90 = new Vector2(-normal.Y, normal.X); // graphic coord
                markpoint.Direction = GetLocalVector(ref normalRotated90);

                float randomlifeSpan = MathUtils.RandomNumber(2000, 8000);

                if ((Info & BodyInfo.Solid) != 0)
                {
                    randomlifeSpan += 15000;  //keep it dirty
                }

                if ((particle.Info & BodyInfo.Fire) != 0)
                {
                    randomlifeSpan += 85000;  //burns last long or foreer usually burns joints and such.  TODO figure temp ( blackend and smoking, vs light burns by partilces)
                }//TODO add Bullet



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
                return null;
            }

            return markpoint;
        }





        private MarkPoint GetMarkPoint(Vector2 localpos)
        {
            MarkPoint markpoint = new MarkPoint(this, localpos);
            return markpoint;
        }



        public const string BulletTemporaryTag = "bullet";

        /// <summary>
        ///Use a joint and attack point to stick a bullet in this body  (Note.. also can use visual marks) 
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

            //   depth += 0.02f;  // seems like the collision tolerance.. now using ray intesect point on shape
            //     float depth  = MathHelper.Max(contactImpulse, 16f);   TODO relate to this impulse.. TODO check with cutter.. either cut of embed.. or prevent cutting on thin items
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
            //but for swords..?  dont know..   make sure impulse is correct  TODO .. remove post solv.
            bool insideOneOfFixture = IsInsideAFixture(contactIndented);

            if (!insideOneOfFixture) //try with less indent
            {
                indentationVec = penetrationVec * penentrationDepth / 2;
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
            strikingBody.LinearVelocity = LinearVelocity;// make it stop relative to us.

            Vector2 relVelocity = strikingBody.LinearVelocityPreviousFrame   //using rel  last frame beause  at point blank range vel will be zero, emitter will set this..updated everyframe
                - LinearVelocity;  //always go to our body at rest intertial reference frame ( never mind rotation).. this fight could be in a fast spaceship.


            ApplyLinearImpulse(relVelocity * strikingBody.Mass * World.DT, embeddedWorldPosition);//absorb its energy, since we stop it in one frame take that whole impulse

            //TODO if make body only.. make to rub// remove bullet..
            //strikingBody.IsNotCollideable = true;  // TODO future optimisatino.. .. make this not collidable except when other bullet penetrates so they dont collide with each other.
            //TODO search other body parts..

            strikingBody.CollisionGroup = spiritCollideID; //this is so unstickHeadFromArms can workin plugin.
            strikingBody.Position = embeddedWorldPosition;

            AttachPoint atc = new AttachPoint(this, localpos);  // dont need to add to collection ..unless body can expell them
            atc.Name = BulletTemporaryTag;  //TODO future.. could be a flag in addition to IsTemporary
            atc.Flags |= (AttachPointFlags.IsTemporary);

            ///bullet are 170 grams,, massive                
            if (PartType != PartType.MainBody)
            {
                atc.StretchBreakpoint = 500;  // can be rubbed off .. or pulled off maybe.
            }

            atc.Detached += OnTemporaryAttachPtDetached;



            if (strikingBody is Particle) //TODO SHOTGUN
            {
                (strikingBody as Particle).LifeSpan = float.MaxValue;
                //      AttachParticleAsVisualMark( )
            }


            //   TODO place a mark instead.. like an emitter.  of the pellet..  because there would be too  many of these..  .. 
            if (strikingBody.AttachPoints.Count == 0)
            {
                AttachPoint atcp = new AttachPoint(strikingBody, strikingBody.LocalCenter);
                strikingBody.AttachPoints.Add(atcp);

            }

            atc.Attach(strikingBody.AttachPoints[0]);// ignoring contacts..

            if (atc.Joint != null)
            {
                atc.Joint.Usage = JointUse.Embedded;
            }

            AttachPoint newGrab = new AttachPoint(strikingBody, strikingBody.LocalCenter);  // this is so creature can pull off a bullet I think.
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
            AttachPoints.ForEach(x => x.Detached -= OnTemporaryAttachPtDetached);
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
        /// Is world point inside one of the fixtures.
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
                    // random black an blue 
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


        [Category("VIEW")]
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

        [Category("VIEW")]
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

                if (_edgeStrokeColor != value)
                {
                    _edgeStrokeColor = value;
                    //NotifyPropertyChanged("EdgeStrokeColor");
                }
            }
        }


        private UndoRedo<T> CreateUndoableMember<T>(NotifyUndoRedoMemberChangedEventHandler<T> handler)
        {
            UndoRedo<T> d = new UndoRedo<T>();
            d.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<T>(handler);
            return d;
        }

        /// <summary>
        /// This value is use for bouyancy instead of the Density if nonzero.. lets say an object is pourous with trapped air, this will be less dense.
        /// Alos needed for separate tuning of object in water or on land.
        /// </summary>
        [DataMember]
        public float SubmersionDensity { get; set; }


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

        /// <summary>
        /// Restitution bounciness.   if 1 max.. elastic collision. 0 non elastic.
        /// </summary>
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
        /// help determine how thick a body can be cut through , in one frame, by a given laser or a bullet.   ( or penetrated into)
        /// There are different  standard hardness scales with  hardend steel being 50 to 100, or Diamond 10, Talc is 1
        /// we will use 1 = soft like a cotton, 20 hard like steel.  10 ( is average, as in bone) 
        /// on load will set 0s to 10..for default
        /// </summary>
        [DataMember]
        public float Hardness { get; set; }

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



        [Category("VIEW")]

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


        [Category("VIEW")]
        /// <summary>  
        /// Xaml shapes that used on visual layer as alternate 'dress' for this Body.  
        ///  for dress2 to be used for simple animation like heart beat.. switch back and forth.
        /// </summary>  
        [DataMember]
        public string DressingGeom2 { get; set; }
        [Category("VIEW")]
        /// <summary>
        /// Scaling for dress, 2 to be used for simple animation like heart beat
        /// </summary>
        [DataMember]
        public Vector2 DressScale2 { get; set; }

       
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
        public static readonly float MinBulletImpulseForBoneShellPenetration = 3f; //these are normal impulses]


        /// <summary>
        /// Fixture cache to be used as temporary fixture list when serializing,
        /// as FixtureList will be nulled temporarily.
        /// </summary>
        private List<Fixture> _savedFixture;


        private bool _bIsSaveFixture = false; 
        
        [Category("Collision")]
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
        private bool _ondeserializing;




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


        //TODO FUTURE this should all be done outside the model..  decouple from World,  load the model then add all to physics, a nightmare.
        //later farseer might have cleaned this up also
        // executed first when deserialize
        [OnDeserializing]
        public void OnDeserializing(StreamingContext sc)
        {
            // warning: all props will still  be null here


            // allocate default fixture list. when FixtureList property is being 
            // deserialized and filled with non-null value, this default fixture
            // will be overwritten.

            if (!NotCreateFixtureOnDeserialize)
            {
                World = World.Instance;
                FixtureList = new List<Fixture>(32);
            }
            else
            {
                ContactList = null;
            }

            // to prevent null _mass when ResetMassData is called from some property
            _mass = new UndoRedo<float>();
            _ondeserializing = true;

     
        }

        // executed last when deserialize
        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            //TODO, DONT 
            _bodyIdCounter++;  //avoid dublicates in level.. TODO script a fix for existing levels, its needs to be fixed

            //  Rebuild from general verts if fixturelist was not deserialized

            //  this is an optimization for clouds that loaded as IsNotCollideable, They may  collisions using query in AABB in CloudBurst plugin  
            // dont even allocate for fixtures just use a AABB rectangle to approximate it.

            //TODO Refactore out using AABB fixture..  //NOTE this might not be worthy optimization..   slowness is caused much more by rendering.
            //undless there are increable about of cloudes.. but .. they are not near anything.. Farseer can pile many hundred objects..
            //add level.. CollideClouds.. turn on during bullet , laser , or bomb..
            //ditch the cloud detect code in plugin..

            //on IsNotCollidable, set.. make sure FixtureList is created.
            //Add Using AABB for fixtures.  ( update it every X updates maybe on cloud burst) 


            if (!NotCreateFixtureOnDeserialize)
            {
                if (UsingAABBforCollisionFixture())
                //TODO Hack , remove CLEAN  should just have added a collideable fixture when verst change, in plugin replace one.
                {

                    //TODO  try ... 
                    if (GeneralVertices == null || GeneralVertices.Count == 0)
                    {
                        _ondeserializing = false;
                        Debug.WriteLine("GeneralVertices is empty null and UsingAABBforCollisionFixture, should not be");
                        return;
                    }

                    CreateRectFixtureFromAABB();
                }
                else
                {

                    if ((FixtureList == null || FixtureList.Count == 0) && World != null)
                    {
                        RebuildFixtures();
                    }
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


            _ondeserializing = false;

            _mass.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<float>(_mass_UndoRedoChanged);
        }

        private void CreateRectFixtureFromAABB()
        {
            AABB aabb = ComputeBodySpaceAABB();
            PolygonShape shape = new PolygonShape(aabb.VerticesCounterclockwise, Density);
            CreateFixture(shape, null);  //just to set the mass data.   need some to be a valid object   .  Also for selecting in tool, and colliding in game       
        }

        private void CreateRectFixtureFromAABB(float scale)
        {
            AABB aabb = ComputeBodySpaceAABB();
            aabb =aabb.Expand(scale, scale);
            PolygonShape shape = new PolygonShape(aabb.VerticesCounterclockwise, Density);
            CreateFixture(shape, null);  //just to set the mass data.   need some to be a valid object   .  Also for selecting in tool, and colliding in game       
        }

        /// <summary>
        /// Rebuild body fixtures from existing GeneralVertices.
        /// Warning: This function is locked during callbacks.
        /// Warning: density is needed in order to avoid contact solver crash
        /// </summary>
        /// <param name="density">The Density of the fixtures</param>
        public bool  RebuildFixtures()
        {
            if (GeneralVertices == null || GeneralVertices.Count == 0)
                return false;

    
            if (UsingAABBforCollisionFixture())
            {
                UpdateAABB();
            }

            if (NeverUsingFixtures())//shadow play mod
            {
                return false;
            }


            DestroyCollisionDataAndFixtureProxies();///we might be using AABB fixture to select in tool, safest to wipe clean



            if (FixtureList == null)
            {
                FixtureList = new List<Fixture>();
            }


            DestroyAllFixtures();

            if (IsCollisionSpline && SplineViewVertices != null && SplineViewVertices.Count > 0)
            {
                PathManager.PartitionFixturesFromVerts(this, SplineViewVertices);
            }
            else
            if (Info == BodyInfo.UseEdgeShape)
            {
                CreateFixtureUsingEdgeShape(GeneralVertices);//not stable with bullet items, on level anyways
            }
            else if (Info == BodyInfo.UseLoopShape)
            {
                CreateFixtureUsingLoopShape(GeneralVertices);
            }
            else
            {
                // Assuming Density is not 0 now i should always be set in Body
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
            }

            if (!UsingAABBforCollisionFixture())  //already did
            {
                UpdateAABB();
            }

            return true;
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


        //TODO in tool its being used only for picking.. todo clean here we use one aabb simple fixture for selection in non prod build..should be generalized to use AABB fixture or something
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


            if (UsingAABBforCollisionFixture())
            {
                CreateRectFixtureFromAABB();
            }
            else
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
                Debug.WriteLine("Error CreateFixtureFromConvexVerts" + ex.Message);
                throw ex;
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
                // Force the vertices to counter clockwise, so that normals will point outward 
                if (vertices.IsCounterClockWise() == false)
                    vertices.ForceCounterClockWise();

                // Decompose our vertices
                List<Vertices> listverts = EarclipDecomposer.ConvexPartition(vertices); //3.3ms physics update  yndrd-swordfightbalancescriptroughgroundsections.wyg
                                                                                        //this seems the best for ground and for giving the fewes triangles.


                // List<Vertices> listverts = CDTDecomposer.ConvexPartition(vertices);//8.3 ms  todo WATER we want to avoid sharp triangles... this is a precursor to voronoi.. check if breakable things like clouds or water can decompose to this, check MIConvexHull  miconvexhull.codeplex.com to add mort vers and get to that.. also for ground textures..




                //   List<Vertices> listverts = BayazitDecomposer.ConvexPartition(vertices);  3.9ms
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

         //   if ( GeneralVertices ==null && this is Particle)
         //   {
         //       (this as Particle).CurrentParticleSize;
         //         
        //    }.


            if (  GeneralVertices != null 
                && ( IsSaveFixture == false) || (FixtureList == null || FixtureList.Count == 0)) // optimization  will be faster than fixture way since verts are revisited.. should  do this in all cases.   each fixture is repeating information, they are not tristrips or similar.  now used only for clouds
            {
                ComputeAABBFromGeneralVerts(out AABB, ref Xf);
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


        /// <summary>
        // AABB using only the general verts not each fixture.  works for polygon bodies ( all that we use) 
        /// </summary>
        public void UpdateAABBForPolygon()
        {
            AABB.Reset();
            ComputeAABBFromGeneralVerts(out AABB, ref Xf);  //TODO optimize  will be faster than fixture way since verts are revisited.. should  do this in all cases.   each fixture is repeating information.  now used only for clouds
        }

        //this was adapted from farseer polygon shape

        public AABB ComputeBodySpaceAABB()
        {
            return ComputeBodySpaceAABB(GeneralVertices);

        }

        public AABB ComputeBodySpaceAABB(Vertices verts)
        {
            AABB aabb;
            aabb.LowerBound = aabb.UpperBound = verts[0];

            for (int i = 1; i < verts.Count; ++i)
            {
                aabb.LowerBound = Vector2.Min(aabb.LowerBound, verts[i]);
                aabb.UpperBound = Vector2.Max(aabb.UpperBound, verts[i]);
            }
            return aabb;
        }


        //note tested
        public AABB ComputeWorldAABB(Vertices verts)  //TODO OPTIMIZATOIN consider this for all AABB update on body and sprit.. less xforms.
        {
            AABB aabb = ComputeBodySpaceAABB(verts);
            aabb.LowerBound = MathUtils.Multiply(ref Xf, aabb.LowerBound);
            aabb.UpperBound = MathUtils.Multiply(ref Xf, aabb.UpperBound);
            return aabb;
        }


        //TODO use local like above...?OPTIMIZATOIN
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

            Vector2 r = new Vector2(Settings.PolygonRadius, Settings.PolygonRadius); //just a little buffer used for farseer CCD.  using this to slightly expand aabb with slop.  this comes from  the shape polygon shape code.
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


        public void JitterVertices(float factor, bool rebuildFixtures)
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

            tessDirty = true;

            if (rebuildFixtures)
            {
                RebuildFixtures();  //this can be costly for concave items..       
            }
        }


        //TODO add a y2 componet for curving..  cloud can get head, claws..looking during deform.. 
        public void Skew(float factor, bool rebuildFixtures)
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


            tessDirty = true;

            if (rebuildFixtures)
            {
                RebuildFixtures();  //this can be costly for concave items..        
            }
        }

#if !XNA  //TODO fix this
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




        }

#endif

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
        /// helps in breaking clouds which are drawn.. ideally these would be point clouds,   voronoi tesselated once on impace or on creation ..a pool of clouds could exist..
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


        //TODO test with gun and preload.. this was a myseterious bug with bad fixture.. preloading is not necessary for performance we found, for machine gun..
        bool NeverUsingFixtures()
        {

            return false;
#if PRODUCTION
        
            //TODO check clouds are using aaBB fixtures to burst?
       //     return (
           //      IsNotCollideable &&
        //           ((Info & BodyInfo.Cloud) != 0));

#endif
            //     //    || IsStatic      //parts of ground are shapes used for texture display, saved as static body.  need fixtures only to be selectable in tool.   
            //they can never be explosed and collide durng gameplay
      
         // Mass and Moment of intertia wont update from density....should not be a problem tho.
            //NOTE this is all a shared module now remove PRODUCTION from these.. 
            //possible optimization for run time.. now not needed.. we are always using fixture at some point.. sometimes collidable is shut off though.
            //for selection in tool, always need a fixture.

      
            //     IsNotCollideable &&
            //        (   (Info & BodyInfo.Cloud) != 0
            //     //    || IsStatic      //parts of ground are shapes used for texture display, saved as static body.  need fixtures only to be selectable in tool.   
    
           // return false;

        }

        bool UsingAABBforCollisionFixture() //TODO add a seperate flag to be used by cloud., generalize this
        {
            //TODO NOTE  .. Mass and Moment of intertia won't update from density....should not be a problem tho.
            return ((Info & BodyInfo.Cloud) != 0);  //TODO add a seperate flag to be used by cloud..  but no way to we want  detailed cloud to use all those fixtures
        }



        //TODO break out a scale around center ( or X pt) .. useful for other stuff, like setting the visuall geom so things touch.


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

            //TODO  one liner collection all reference points and then scale, use interface
            //one of these was +=  even on

            //TODO could be dangerous .. being added from another thread..
            SharpPoints.ForEach(x => ScaleRefPoint(x, ref scale));
            EmitterPoints.ForEach(x => ScaleRefPoint(x, ref scale));

            //_markCollectingSkipped = true;// TODO fix  this is still not safe.. control can already be inside the add mark method.
            //I think  this is safe.. these are added from the physics threads.. ( controller)
            // while scale  occurs in the UI thread.. while physics is locked.
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

                ClearCollisionData(f);

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



        /*  new one..   TODO FUTURE.. usingwith joints that hhave interface to the body and achors.. the way by putting those in the base class wiht new caused issues in regrow and / or serialization
            /// <summary>
            /// Scale the local anchor of all joints connected to this body. Scaling is applied to local coordinate.
            /// </summary>
            public void ScaleJointList(JointEdge je, Vector2 scale)
            {
                // for connected joints, we only need to translate the local coordinate
                while (je != null)
                {
                    PoweredJoint pj = je.Joint as PoweredJoint;  //TODO pull out interest BodyA, BodyB , LocalAnchor, ..
                    ScaleJoint(je, scale);
                    je = je.Next;
                }
            }

            private void ScaleJoint(JointEdge je, Vector2 scale)
            {
                Joint j = je.Joint as Joint;
                if (j != null)
                {
                    if (j.BodyA == this)
                    {
                        j.LocalAnchorA *= scale;
                    }
                    else if (j.BodyB == this)
                    {
                        j.LocalAnchorB *= scale;
                    }
                }
            }*/





        /// <summary>
        /// Scale the local anchor of all joints connected to this body. Scaling is applied to local coordinate.
        /// </summary>
        public void ScaleJointList(JointEdge je, Vector2 scale)
        {
            // for connected joints, we only need to translate the local coordinate
            while (je != null)
            {
                PoweredJoint pj = je.Joint as PoweredJoint;  //TODO pull out interface to BodyA, BodyB , LocalAnchor, See code above.. i tried but then regen got broken.. other serialiization issues.. aborted that. TODO future

                if (pj != null)
                {
                    if (pj.BodyA == this)
                    {
                        pj.LocalAnchorA *= scale;
                    }
                    else if (pj.BodyB == this)
                    {
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


                SliderJoint sj = je.Joint as SliderJoint;
                if (sj != null)
                {
                    if (sj.BodyA == this)
                    {
                        sj.LocalAnchorA *= scale;
                    }
                    else if (sj.BodyB == this)
                    {
                        sj.LocalAnchorB *= scale;
                    }
                }



                DistanceJoint dj = je.Joint as DistanceJoint;
                if (dj != null)
                {
                    if (dj.BodyA == this)
                    {
                        dj.LocalAnchorA *= scale;
                    }
                    else if (sj.BodyB == this)
                    {
                        dj.LocalAnchorB *= scale;
                    }
                }

                je = je.Next;
            }
        }



        /// <summary>
        /// uses joint graph ( active physics to find number of joints directly on this body.
        /// </summary>
        /// <param name="ignoreTemporary">dont count joints mared temporary , as used fro Bullets stuck inside</param>
        /// <returns></returns>
        public int GetNumJointsConnected(bool ignoreTemporary)
        {
            JointEdge je = JointList;
            int num = 0;
            while (je != null)
            {
                JointEdge je0 = je;
                je = je.Next;
                Joint joint = je0.Joint;

                if (!(ignoreTemporary && joint.Usage == JointUse.Embedded))
                    num++;
            }
            return num;
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
        /// Reset some state when transferring body between physics world.
        /// </summary>
        public void ResetStateForTransferBetweenPhysicsWorld()
        {
            World = World.Instance;
            // when traveler body transfered between level, it has the  
            // invalid jointlist from the previous physics world, reset it
            JointList = null;
            ContactList = null;

            // this is to fix bug on ghost spirit (passing through level).
            // all new fixtures must get proper collision group id.
            FixtureList = null;

            //this is needed , since proxies use the world, and the broad phases is in there
            RebuildFixtures();

            if (FixtureList != null)
            {
                SynchronizeFixtures();   // not sure if needed, doesnt hurt.
            }

            // body emitters need to get new world reference, else will throw dynamic tree leaf exception
            foreach (Emitter em in EmitterPoints)
            {
                BodyEmitter be = em as BodyEmitter;
                if (be != null)
                {
                    be.World = World.Instance;
                }
            }
        }

#region IEntity Members


        /// <summary>
        /// This is called on one thread after physics is fished a frame , locked and waiting.
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


            if (!IsStatic && !FixedRotation)
            { 
                NotifyPropertyChanged("Angle");  //was in sync transfrom,seems wasteful
                NotifyPropertyChanged("Rotation"); //dont need to see this
            }
        }

        public void UpdateThreadSafe(double dt)
        {

       
        }



        private void UpdateVisibleMarks(double dt)
        {
            if (_visibleMarks != null && _visibleMarks.Count != 0)
            {
                List<MarkPoint> deadMarks = new List<MarkPoint>();

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

   
#endregion

        //TODO remove this.. only accounts for rotation not position.   cm body is usually not 0,0 in local space..

        /// <summary>
        /// Returns an angle in radians from ranging for -pi to pi  from  vector relative to the body, can be used for Target angle on a joint anchored to body
        /// </summary>
        /// <param name="targetVec"></param>
        /// <returns>angle to body</returns>
        public float AngleToBody(Vector2 targetVec)
        {
            targetVec = GetLocalVector(ref targetVec);
            //if our positive Y was up like normal Euclidean plane we are taught with.. then it would be Atan2(targetVec.Y, targetVec.X);
            float theta = (float)Math.Atan2(/*neg needed since our Y is flipped*/ -targetVec.Y, targetVec.X);
            return theta;
        }


        public float AngleToBodyCMLocal(Vector2 position)
        {
            Vector2 targetVec = position - LocalCenter;
            float theta = (float)Math.Atan2(/*neg needed since our Y is flipped*/ -targetVec.Y, targetVec.X); // see GeomUtils.GetAngleFromVectorCartesian  in case of quandrant III  issues
            return theta;
        }


        /// <summary>
        /// Returns a position angle in radians ( counterclockwise)  from a vector to the body,  can be used for Target angleon a joint anchored to body
        /// becareful  of transition from  0 to 360.  0 to 2pi.   3 oclock is zero.
        /// </summary>
        /// <param name="targetVec"></param>
        /// <returns>angle to body</returns>
        public float PositiveAngleToBody(Vector2 targetVec)
        {
            float theta = AngleToBody(targetVec);
            if (theta < 0)//make angle positive
            {
                theta += (float)(2 * Math.PI);
            }
            return theta;
        }

        public Body cloneOrg = null;

        //future .. might speed up cloud generation on UI thread.. or bullets.  reading XAML is slow.
        public object Clone()
        {
            // not a deep clone ..object references are shared.  i think its ok in case of body.  //body color is the only one thats a class, so if not changing color, fine.
            object clone = MemberwiseClone();

            if (cloneOrg == null)
                cloneOrg = this;

            return clone;
            //   clone.GeneralVertices??  do we need a deep clone here ie copy array to new array of verts?
        }

        public object DeepCloneVisible()
        {

            object clone = Clone();

            if (GeneralVertices != null)
            {
               ( clone as Body).GeneralVertices = new Vertices(GeneralVertices);
            }

            (clone as Body).Xf = new Transform(ref Xf.Position, ref Xf.R);

            (clone as Body).VisibleMarks = new List<MarkPoint>(VisibleMarks);

       //later     clone.EmitterPoints = new List<Emitter>();


            if (Color.A > 0 && FixtureList != null )
            {
                (clone as Body).FixtureList = new List<Fixture>(FixtureList);
                //TODOO if needed clone each fixture in case growing or scaling..
            }  
            
            return clone;
        }

        //TODO future .. should probably use above instead or  MemberwiseClone   
        /// <summary>
        /// Copy some of the properties , one by one. This is used by breakable body .  Vertices are not cloned.
        /// </summary>
        /// <param name="source"></param>
        public void CopyPropertiesFrom(Body source)// bool ignoreContacts = false)
        {


            SetTransformIgnoreContacts(ref source.Xf.Position, source.Rotation);  //used on cutting.. for teleporting bodies ignoreing contracts on this step... 


            //  Position = source.Position;
            //  Rotation = source.Rotation;
            LinearVelocity = source.LinearVelocity;
            AngularVelocity = source.AngularVelocity;
            Color = source.Color;
            EdgeStrokeThickness = source.EdgeStrokeThickness;
            EdgeStrokeColor = source.EdgeStrokeColor;
            FixedRotation = source.FixedRotation;
            Restitution = source.Restitution;
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
            Acidity = source.Acidity;

            DressingGeom = source.DressingGeom;
            DressingGeom2 = source.DressingGeom2;
            IsShowingDress2 = source.IsShowingDress2;

            DressScale = source.DressScale;
            DressScale2 = source.DressScale2;

            //TODO particle properties.. consolidate with other copy for spawn..
        }


        public bool IsSharpWeapon
        {
            get
            {
                return (((PartType & PartType.Weapon) != 0 && SharpPoints.Count > 0));
            }
        }


        public bool IsWeapon
        {
            get
            {
                return (

                    (PartType & PartType.Weapon) != 0
                || (Info & BodyInfo.ShootsProjectile) != 0
               //|| SharpPoints.Count > 0 // some stone has sharp point but not weapon, sharp point is meant to allow sword to deflect.. (TODO remove this hack, dont use sharp point for that)
               || (Info & BodyInfo.Bullet) != 0);

            }
        }

        public bool IsGun
        {
            get
            {
                return ((Info & BodyInfo.ShootsProjectile) != 0);
            }
        }

        /// <summary>
        /// AI can detect its foot on sight.. Nourishment indicates what it really is.. on taste, touch to mouth
        /// </summary>
        public bool IsFoodInAppearance
        {
            get
            {
                return ((Info & BodyInfo.Food) != 0 || PartType == PartType.Food);
            }
        }




#region IEntity Members

        public bool WasSpawned
        {
            get { return (Info & BodyInfo.WasSpawned) != 0; }
        }

        /// <summary>
        /// Uniquie birth id for this level.  combine with level id we have universal ID
        /// </summary>
        public int ID
        {
            get
            {
                return BodyId;
            }
        }
        /// <summary>
        /// Immediate mode draw, ViewModel takes care of this
        /// </summary>
        /// <param name="dt"></param>
        public void Draw(double dt) { }

        /// <summary>
        ///
        /// </summary>
        ViewModel viewModel = null;


        [Category("VIEWUNUSED")]

        [DataMember]
        public ViewModel ViewModel
        {
            get { return viewModel; }

            set { viewModel = value; FirePropertyChanged(); }

        }

        public Transform Transform => Xf;


#endregion


        /// <summary>
        /// returns PartType.Left or Right flags, if set.
        /// </summary>
        /// <param name="left"></param>
        /// <returns></returns>
        public static PartType GetPartTypeLeftRight(bool left)
        {
            return left ? PartType.Left : PartType.Right;
        }

        public static PartType GetPartTypeFlag(bool left, PartType pt)
        {
            return pt | GetPartTypeLeftRight(left);
        }

        /// <summary>
        /// test if bit set, more readable .
        /// </summary>
        /// <param name="bf"></param>
        /// <returns></returns>
        public bool IsInfoFlagged(BodyInfo bf)
        {
            return ((Info & bf) != 0);
        }

        public void UnflagInfo(BodyInfo bf)
        {
            Info &= ~bf;
        }



        #region VIEWINGSTUFF  /// we dont use maingtain views because its simple maintain a list of bodies to immediate draw... ll pbyscal bodies have only one looki
        public string BodyColorRGBA
        {
            get
            {
                return Color.ToString();
            }
        }

        private bool isSplineFit = false;

        [Category("VIEW")]
        [DataMember]
        int DrawOrder { get; set; }



        private int numSplineVerts = 200;
        [Category("VIEW")]
        [DataMember(Order = 2)]//after GeneralVertices
        public int NumSplineVerts
        {
            set
            {
                if (value == numSplineVerts)
                    return;

                numSplineVerts = value;

                FirePropertyChanged();

                GenSplinePath();
            }

            get => numSplineVerts;

        }


        bool isCollisionSpline = false;

        [Category("VIEW")]

        [DataMember(Order = 3)]
        public bool IsCollisionSpline
        {
            get => isCollisionSpline;
            set
            {

                if (value == isCollisionSpline)
                    return;

                isCollisionSpline = value;

                if (value == true)
                {
                    GenSplinePath();
                }
                else
                { 
                    ClearSplineFixtures();
                }


                if (!_ondeserializing && IsCollisionSpline)
                {
                    if (isSplineFit == false)
                        isSplineFit = true;
                }

                FirePropertyChanged("IsSplineFit");
                FirePropertyChanged();
            }
        }

        /// <summary>
        /// Fit a Catmull Rom spline through the verts
        /// </summary>
        [DataMember(Order = 99)]
        [Category("VIEW")]
        public bool IsSplineFit
        {
            get => isSplineFit;
            set
            {
                if (isSplineFit == value  && !_ondeserializing)
                    return;

                isSplineFit = value;

                if (value)
                {
                    GenSplinePath();
                }
                else
                {
                    if(IsCollisionSpline)
                    {
                        IsCollisionSpline = false;
                    }

                }

                FirePropertyChanged();
            }
        }

        List<Vector2> splineViewVertices;

        public List<Vector2> SplineViewVertices
        {
            get {

                if (isSplineFit && splineViewVertices == null)
                {
                    GenSplinePath();
                }

                return splineViewVertices; 
            }

            set
            {
                if (value == splineViewVertices)
                    return;

                splineViewVertices = value;
                FirePropertyChanged();
            }

        }


        
        

        public IEnumerable<IEntity>  Entities => default(IEnumerable<IEntity>);
        

      
        byte[] IEntity.Thumbnail => _isShowingDress2 ? Thumnail2:Thumnail1;



        #region legacy graphics 

        //data for xforming xml dress canvas to images for display in MG_GRAPHICS
        /// <summary>
        /// png  encoded thumbnail for the dress canvas
        /// </summary>
        [DataMember]
        public byte[] Thumnail1 { get; set; }//todo fix spelling and resave duh

        /// <summary>
        /// png encoded thumbnail for the dress2 canvas 
        /// </summary>
        [DataMember]
        public byte[] Thumnail2 { get; set; }//todo fix spelling and resave duh




        /// <summary>
        /// offset in body space from body center to body origin..the 0,0 isnt alwaysthe cm.   when we cut, and clip and image the body CM changes so using it as a ref pt isnt good
        /// xfrom to world vect and draw image there. 
        /// </summary>
        [DataMember]
        public Vector2 OffsetToBodyOrigin { get; set; }


        /// <summary>
        /// offset in body space from body center to body origin..the 0,0 isnt alwaysthe cm.   when we cut, and clip and image the body CM changes so using it as a ref pt isnt good
        /// xfrom to world vect and draw image there. 
        /// </summary>
        [DataMember]
        public Vector2 OffsetToBodyOrigin2 { get; set; }


        /// <summary>
        /// the original scale from pixel to body space  dress1
        /// </summary>
        [DataMember]
        public Vector2 TexelScale { get; set; }



        /// <summary>
        /// the original scale from pixel to body space  dress2
        /// </summary>

        [DataMember]
        public Vector2 TexelScale2 { get; set; }


     
        #endregion
        string IEntity.Name => this.PartType.ToString();

        string IEntity.PluginName => "";

        public void GenSplinePath()
        {
            if (GeneralVertices == null)

                return;

            
                Path splinePath = new Path(GeneralVertices);
                splinePath.Closed = true;

                SplineViewVertices = PathManager.ConvertPathToViewingPolygon(splinePath, NumSplineVerts);
            
            
            if (isCollisionSpline && !(_ondeserializing && Body.NotCreateFixtureOnDeserialize))
            {
                DestroyCollisionDataAndFixtureProxies();
                PathManager.PartitionFixturesFromVerts(this, SplineViewVertices);
                CheckToCreateProxies();
            }
         
        }

        private void ClearSplineFixtures()
        {
            if (!(_ondeserializing && Body.NotCreateFixtureOnDeserialize))
            {
                DestroyAllFixtures(); //will get made on RebuildFixtures when needed later
                this.RebuildFixtures();
            }
        }


        private List<Vertices> tesselatedVerts;

        private bool tessDirty = true;


        public bool TesselationDirty { set => tessDirty = value; get => tessDirty || TesselatedVerts.Count == 0; }
       

        /// <summary>
        /// aa cache for when draw code fills concave shapes fom general verts where there are no fixutres or different looking fixtures
        /// </summary>

         public List<Vertices> TesselatedVerts {

            get
            {

                if (tesselatedVerts == null)
                {
                    tesselatedVerts = new List<Vertices>();

                }

                return tesselatedVerts;
            }
                set{ 

                tesselatedVerts = value;
                tessDirty = false;

                }
            
            }



    }



    //NOTE , CODE REVIEW.     not worth changing tho now. this should  be a struct to save on GC .   except in rope,  its rarely shared.
    // for particles, the exiting object RGB values should  be changed, if particles are pooled also the data should probably be a long, not 3 bytes.  like .net.  then using bitmask.
    //however.  have to see how often object sharing is used, i think not often enough to save space.  maybe on ropes 
    /// <summary>
    /// Will give the same color to all Fixtures on the same Body.
    /// </summary>
    [DataContract(Name = "BodyColor", Namespace = "http://ShadowPlay")]
    public class BodyColor : IEquatable<BodyColor> //TODO should be a struct... to be normal.. not i think persistance will be ok, wont break anything.. this does allow a collor to be shared easily on particles or bodies...all change..
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

        public BodyColor(byte r, byte g, byte b)
        {
            SetRGBA(r, g, b, 255);
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


        public bool Equals(BodyColor other)
        {
            return (R == other.R && G == other.G && B == other.B && A == other.A);
        }



        //  Color brushes = System.Drawing.ColorTranslator.FromHtml("#" + hexValue);

        //TODO refactor all repeat crap from plugins and effects  ( that can be named..) and place here..
        public static BodyColor Yellow = new BodyColor(255, 255, 0, 255);
        public static BodyColor Red = new BodyColor(255, 0, 0, 255);
        public static BodyColor White = new BodyColor(255, 255, 255, 255);

        public static BodyColor Black = new BodyColor(0,0,0, 255);

        public static BodyColor NeonGreen = new BodyColor(57, 255, 20, 255);
        public static BodyColor FluorescentGreen = new BodyColor(187, 227, 10, 255);


        public static BodyColor Copper = new BodyColor(200, 117, 51, 255);
        public static BodyColor Brass = new BodyColor(181, 166, 66, 255);
        public static BodyColor GunmetalBlue = new BodyColor(43, 59, 68);



        public static BodyColor DepressiaGrey = new BodyColor(138, 143, 146);
        public static BodyColor ItalianNude = new BodyColor(221, 170, 108);

        public static BodyColor Rtskin = new BodyColor(198, 126, 77);
        public static BodyColor Alcasmodustsur = new BodyColor(170, 105, 56);
        public static BodyColor AlcasmodustsurDarker = new BodyColor(168, 103, 57);




        public static BodyColor CoolCopper = new BodyColor(0xd9, 0x87, 0x19);//217,

        public static BodyColor Iron = new BodyColor(168, 103, 57);
        public static BodyColor Lead = new BodyColor(0x4c, 0x4c, 0x4c);
        public static BodyColor Steel = new BodyColor(0x66, 0x66, 0x66);
        public static BodyColor Aluminum = new BodyColor(0x99, 0x99, 0x99);

        public static BodyColor Silver = new BodyColor(0xcc, 0xcc, 0xcc);

        public static BodyColor Transparent = new BodyColor(0, 0, 0, 0);


        /*
          Copper      = Color::RGB.new(0xb8, 0x73, 0x33)


          Magnesium   = Color::RGB.new(0xb3, 0xb3, 0xb3)
          Mercury     = Color::RGB.new(0xe6, 0xe6, 0xe6)
          Nickel      = Color::RGB.new(0x80, 0x80, 0x80)
          PolySilicon = Color::RGB.new(0x60, 0x00, 0x00)
          Poly        = PolySilicon
          Silver = Color::RGB.new(0xcc, 0xcc, 0xcc)
          Tin         = Color::RGB.new(0x7f, 0x7f, 0x7f)
          Tungsten    = Color::RGB.new(0x33, 0x33, 0x33)

            */


        public override string ToString()
        {

            // return   String.Format("#{0:X}{0:X}{0:X}{0:X}", R, G, , A);
            /// string hexColor = "#" + red.ToString("X") + green.ToString("X") + blue.ToString("X");

            //   string color = string.Format("#{0:X}{1:X}{2:X}"

            return string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", A, R, G, B); //fuck so many answers online
        }

    }

    #endregion


    /// <summary>
    /// Type of body piece that participates in Spirit system.   Typically there is one value ,  sometimes with  modifier bit ( Left, Upper) .  For the PartType to show in Tools standard property page, an enum must match or it will be blank
    /// </summary>
    [Flags]
    public enum PartType
    {
        None = 0,//also assumed,   ground is none for wind block checking
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

        Wing = (1 << 17),

        LeftWing = (Left | Wing),
        RightWing = (Right | Wing),
        LeftWingTip = (Left | Wing | Toe),
        RightWingTip = (Right | Wing | Toe),

        //note Part bits should be reserved just for organisms, basic uses of special parts in system
        //may need fin, tail, reuse legs arms for fins.  wings might be single purpose so added for bird

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

        LeftMiddleToe = (Left | Middle | Toe),
        RightMiddleToe = (Right | Middle | Toe),

        LeftHand = (Left | Hand),
        RightHand = (Right | Hand),

        LeftUpperHand = (Left | Upper | Hand),
        RightUpperHand = (Right | Upper | Hand),


        LeftLowerHand = (Left | Lower | Hand),
        RightLowerHand = (Right | Lower | Hand),

        LeftShinBone = (Left | Shin),
        LeftThighBone = (Left | Thigh),

        RightShinBone = (Right | Shin),
        RightThighBone = (Right | Thigh),

        UpperArm = (Upper | Arm),
        LowerArm = (Lower | Arm),

        LeftUpperArm = (Left | UpperArm),
        LeftLowerArm = (Left | LowerArm),

        RightUpperArm = (Right | UpperArm),
        RightLowerArm = (Right | LowerArm),


        Control = (1 << 18),   // when holding a part marked like this arm will have no power, the spirit will provide the power.. used for certain doors , handles and such.

        Weapon = (1 << 19),   //TODO parttype is for a parts role in a system.. this shod be BodyInfo as well
        Food = (1 << 20),      //TODO might start using Body info for this.. 
        Container = (1 << 21),
        Rope = (1 << 22),  //segment of a rope
        Stone = (1 << 23),
        Handhold = (1 << 24), //for climbing.. TODO clean this.. change to Handhold, bit..  
        Rock = (1 << 25), //  small  stone that can be tossed
        Door = (1 << 26),
        Cloud = (1 << 27),  //TODO use body info for this.. since cloud is a spirit and has a main body..NOTE.. note use.. free for uther use..

        Hinge = (1 << 28),  //usually hinge part used to contect two pieces using two jionts 
        Latch = (1 << 29), //used to lock a door


        LiquidOxygen = (1 << 30),//TODO remove ths in favor of parts with mixable flags
        RocketFuel = (1 << 31), //for a block or blocks used to show a fluid level ( see airship)


        //  Branch = (Arm | Plant),
        //    Root = (Foot | Plant),

        //   Roof = (1<<32),
        //TODO clean out unused.. cant be above 32 or dotn know what value bits it might add.... Ground = ( 1<<35) 
        Stalagtite = (Upper | Rock),
        Stalagmite = (Lower | Rock),
    }







    //TODO combine with above , remove Body info .. used only now for    PlayerCharacter..
    // maybe filter PlayerCharacter out of prop page UI combo box..

    /// <summary>
    /// Additional Metadata tag describing properties of a Body as an individual physical object
    /// </summary>
    /// 
    [Flags]
    public enum BodyInfo
    {
        None = 0,
        PlayerCharacter = (1 << 1),

        /// <summary>
        //AI or eyes can see though this body.  migth be used if theres dress, otherwise check alpha on fill
        /// </summary>
        SeeThrough = (1 << 2),
        InMargin = (1 << 3),//body lives in level  margin, wont be cleaned..
        Cloud = (1 << 4),//body will use a rectangular fixture to simply mass data setting..  alsoo verts can jitter , be degenerate polygon and require special tesselation...
        Debris = (1 << 5),//future, migth land on ground and become collidable..  todo, remove,  just make it collidabel we can afford this..
        UsePointToHandleForDragEdge = (1 << 6),//for kris or other curvy weapon, use a vector from shart point to handle edge for the wind face
        IncreaseDensityOnGround = (1 << 7), //for low density like leave,  .. TODO erase and implenet crushing,,, currently use by tree only.. attach handlers and increase density when lying on ground, so that stepping doesn not create contraint solver issues.

        DebugThis = (1 << 8),  //to mark body of interest for debugging.
        Food = (1 << 9),  //   means visible as food , will be hunted.    TODO REMOVE redundant with NutriationValue on body
        AirContainer = (1 << 10),  //   might  use condition  ( like urn or hull some oject of greater mass ).   use now for two reasons.  
                                   //1. Boats with hulls that can float if both ends are above water 2. rule: Dont step over stuff inside it when pushin it. ( not is present game levels).. 


        /// <summary>
        /// WIP material info
        //Magnetic = ( 1 <<10)
        // Glue
        //Oxidant  this + that = gunpowder
        //Combustible
        //HIghexplosive gunpowder ignite this..
        //Sometimes = (1 << 11),   //object is there sometimes.. 

        /// </summary>
        Liquid = (1 << 12),
        Solid = (1 << 13),

        UseSingleDragEdgePanel = (1 << 14),  //  for balloon  panel, rope, or bone in system  , use only one pt on panel, not two.  for saving cycles.  unset on broken..
        Surfboard = (1 << 15),
        SurfboardAirContainer = (Surfboard | AirContainer),
        SurfboardAirContainerClip = (Surfboard | AirContainer | ClipDressToGeom),

        KeepVisible = (1 << 16),   //for this body dont remove the view if out of viewport. 
        ShootsProjectile = (1 << 17),//for gun.. so that it can be aim and treated as special weapon by 

        ClipDressToGeom = (1 << 18),
        ClipDressIsGuarded = (ClipDressToGeom | IsGuarded),
        //UseSingleDragEdgePanel


        /// <summary>
        /// //to indicate not to save with level .. and so that clean will remove it.
        /// </summary>
        WasSpawned = (1 << 19),

        /// <summary>
        /// used on emitter. don't apply properties from emitter to spawned body.  TODO well then consider calling it ApplyEmitterProperties then 
        /// </summary>
        SpawnOnly = (1 << 20),

        PlayerCharacterSpawed = (SpawnOnly | PlayerCharacter),//we make this so that the prop page can show it it cant hanle mulple flags now
        PlayerCharacterSpawedSingleEdgeDrag = (SpawnOnly | PlayerCharacter | UseSingleDragEdgePanel),

        NotReuseableDress = (1 << 21),  //a mark dont copy this dress when repeating texture on a rope as in rope bridge
        NotReuseableDressUseSingleDragEdgePanel = (NotReuseableDress | UseSingleDragEdgePanel),  //just so it can show in drag panel..
        Bullet = (1 << 22),
      
        UseEdgeShape = (1 << 23),
        UseLoopShape = (1 << 24),   // chain shape in box2d they have issues...

        IsGuarded = (1 << 25),  // AI will react if this is moved ..  //TODO HACK  CLEAN bad shit..this is used in like level 1 only itgs not a general features. just name objects , give each ai have some level staved.. brain state.
        Building = (1 << 26),  // used for structural parts of houses and buildings, will block wind but not be affected by wind.
                               //   Flags   IsStaticForSleep will be used on Building.. high bombs can move them   // used for items forced to sleep as static due to joints and piling never getting stable.. airship, etc.

        SkipAirOnPanel = (1 << 27),  //  for closed balloon or rope only.. don't apply wind or blocking at all when joined, let the adjacent piece do it.  Not used, puts a strain.
        Magnetic = (1 << 28),  // sticks to magnet spirit ( plugin called Magnet), objects nearby be magnetized and stuck
        Fire = (1 << 29),  // so that temperature does evaporate it.  also will burn stuff
        Bubble = (1 << 30)

    }

    //TODO add a material.   ropesegment and chain segment
    //TODO add a hardness
#endregion

}