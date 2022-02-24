//#define TEST_REGROW_ZERO_FORCE  // test regrow with joint disabled and zero gravity, so no external force applied
#define  CIRCLESENSOR //  using sensors to detect items like other spirits, sharps , etc.. not using AABB queuy for moving objects.. for clouds not moving or moving slowly , using sensor
//define REDUCEVERTSONBONEBREAKING  //make collision faster.. less faces.   especially with bulleted   bones are overlapping rounded so stuff is less likes to get stuck inside when it separates
#define USINGSELFCOLLIDE

using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.Serialization;



using Farseer.Xna.Framework;
using FarseerPhysics;
using FarseerPhysics.Common;
using FarseerPhysics.Factories;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics.Contacts;

using Core.Data.Animations;
using Core.Data.Collections;
using Core.Data.Interfaces;



using Core.Data.Geometry;
using Core.Data.Plugins;
using Core.Data.Input;

using CryptoObfuscatorHelper;
using System.Diagnostics;
using Core.Data;



using UndoRedoFramework;
using System.Collections.Generic;
using System.Linq;

namespace Core.Data.Entity
{
    //TODO FUTURE derive a Legged spirit from this as a base class..    consider rename spirit BodySystem
    // may need a lighter simpler spirit class for stuff like clouds and tools




    /// <summary>
    /// An Entity comprised of a System of Bodies connected by a collection Joints, powered or welds.  It can be a device, planet or creature, or other.  
    /// It has a collection of behaviors so it can be animated, using Keyframes and linear interpolation.  Each behavior is just a collection of keyframes mapped to input keys to activate them
    //// Each Spirit can have one Plugin with JIT compilation to extend its functionalty and give it custom environment detection and provide
    /// </summary>
    ///<remarks>
    /// Custom user input enhancements other than that providedby the basic control key map framework.   It can be viewed  a complex State machine.  and with callbacks to optional Plugins, it can response to the environement.  There are hooks for canStopCurrentBehavior , and OnBehaviorChanging
    /// The client can say no and delay or tak over there.. examples are going from left to right, if soming is in the way , or it would put the system in an undesired state, this can be avoided and corrected
    /// The spirit has some features that may be unused, but are common on living things , hydralic devices, and even planets..  This are some things that are specific to living creatures in here..
    /// blood emitters  ( used for hydraulic leaks on machines).. this spirit was orginally used for the biped main.
    /// character , and there are a few non-general things that doesnt really belong here.. the simplest  spirit has two bodies, one joint.  The only way to give a body a plugin 
    /// for custom updates per frame outside the basic core, is to make a two bodies and click with the spirit tool on the main ( nexus body).. i think its possible to delete the second one then..
    /// for some there is a little non collidable body that is transparent.. ( machine gun that is mounted and needs no trigger)  TODO.. in tool.. allow better management of Spirits.  allow one body spirits.
    /// make sure Body entties in spirit are moved out of the Level loose Entities collection. and vice versa on joint delete.
    /// TODO FUTURE would be convenient to make the behaviors separate and shared.. to save memory .. reference by a string key..Good to have a Mirror reference, so that left and right are not copied data.
    /// now  the physics structure ,  of the character is directly embedded with the body , the appearance, and the Keyframes.  Would be nice to have linked dress, and/or linked behavior sets.
    /// so if adding a behavior, have to add to all the variants of the creature, as in different color, size AIs.. when they are all the same.
    //  would be ok if the blue bideds had dress separate from the Spirit and its Behaviors.  its conceivable that a set of behaviors might applyh to two different Body systems, but really we might only need this 
    /// since the dress is embedded.  Scaling of creatures and dress , alos use the same exacle behaviors which are slowed or speed using Spirit.SizeFactor, the logic of which is in the plugin, which can accomodate 
    /// many "phenotypes".  Plugins are separate, and a Spirit can be given a different plugin. 
    /// Behaviors should be a linked and shared file, acting only on the angles, as resouces of difference scales can be reused with small size.
    ///</remarks>
    //[ObfuscationAttribute(Exclude = true, ApplyToMembers= false)]  //class names are excluded by a general rule
    [DataContract(Name = "Spirit", Namespace = "http://ShadowPlay", IsReference = true)]
    public class Spirit : NotifyPropertyBase, IEntity, ICloneable
    {

        //there are some hacks ( specific stuff that should have ben generalized or done in plugin)  , it's not perfect, but on hindsight its a very robust class for dynamic motion synthesis and realtime design and tuning for locomotion,
        //AI work , and basic simulation stuff.    Even after reading 
        #region Events


        /// <summary>
        /// Called from the button under the params prop sheet so they can be taken all at once as a set
        /// </summary>
        public Action OnSpiritParamsUpdate = null;
        public void UpdateSpiritParams()
        {
            OnSpiritParamsUpdate?.Invoke();
        }


        public delegate void OnUpdateEventHandler(Spirit sender, double elapsedTime);
        public delegate void OnStopEventHandler(Spirit sender);
        public event OnStopEventHandler OnStop;
        public static event Action<Spirit, Exception, string> OnSpiritException = null;

        public Func<bool> CanEndRegenerateDelay;//if caller returns false, Delay will keep polling until its true.



        public OnCollisionEventHandler OnSensorCircleCollision;

        // sensor view is unused in shadowplay
#if !SILVERLIGHT
        /// <summary>
        /// This event is triggered when break sensor is about to be destroyed.
        /// Use this to properly remove view of previous sensor.
        /// </summary>
        public Action<Spirit> SensorDestroying;
#endif

        /// <summary>
        /// when dying , allow plugin to do death throws.   param is max seizure , so to drop dead , pass zero
        /// </summary>
        public Action<Spirit, float> OnKilled;

        /// <summary>
        /// Event when Died, energy level went to zero..
        /// </summary>
        public Action<Spirit> Died;

        /// <summary>
        /// send when Poison injected into joint..
        /// </summary>
        public Action<float> Poisoned;

        /// <summary>
        /// fires when a joint was broken, after the body set is updated
        /// </summary>
        public Action<Joint> JointBreaking;

        /// <summary>
        /// Event when something strikes head
        /// </summary>
        public event Action<float, Body, Body> OnHeadCollision;  //TODO .. should just be handled in plugin.. not all spirits had head.   too much state in spirits.  each cloud is a spirit, since they change shape , break specially and such.. and its too much memory used

        /// <summary>
        /// Event when non-static body touches foot.  ifleft
        /// </summary>
        //public event Action<Body, bool> OnFootCollision;  //not currently used

        public Action OnUnstickSelfCycle;

        public Action<float> OnAvgSpeedXUpdate;  //just the horizontal component

        public Action<float> OnAvgSpeedUpdate;

        /// <summary>
        /// Event when food strikes head
        /// </summary>
        public Action<Body> OnHeadFoodCollision;



        ///  Fired when Active behavior is requested to change by key stroke or whatever.  If delegrate returns false, it is not changed.   Client has poll user desires,  change the state when it can.
        /// </summary>
        public Func<Behavior, bool> ActiveBehaviorChangingKeyDown;

        /// <summary>
        /// Fired when Active behavior is requested to stop ( now by key up).  If deletage says you can't ( as in, it will fall, dont) then it will continue.
        /// Example.. walking on 2 legs.  Stop is requested just after front foot comes down, rear returns behind it, and body is unstable and leaning forward.  At this point in cycle its unsafe to stop in most conditions, ( as sensed by the plugin) , so it will be told to continue,  walk another step.
        /// If plugin interfered here, is must take the job of changing the state ( stopping).. the spirit is a simple state machine.  this prevents a state change at a bad time.
        /// then the client must take over on automatic.  These are to avoid state changes , then changing back by plugin or system state conflicts like that ( hacks) 
        /// </summary>
        public Func<Behavior, bool> ActiveBehaviorStoppingOnKeyUp;

        public Action<Body> BodyLeavingSystem;

        #endregion
        #region MemVars


        public string Type { get => typeof(Spirit).Name; }


        static string RegrowKey = "regrow";

        private static short _newSpiritCollisionGroup = 0;

        protected int _keyStartIndex;
        protected int _keyEndIndex;
        protected int _animTime = -1;
        protected SpiritPlay _spiritPlay = SpiritPlay.Repeat;

        //allows spirit to resume on play.. cleared by Stop
        protected bool _isPaused = false;

        // Interpolation MemVars for inter behavior transition
        protected Behavior _behavior1;
        protected Behavior _behavior2;
        protected double _duration = 0.3f; // in ms

        // spirit center mass related
        protected float _totalMass;
        public AABB AABB;

        public Spirit AuxParent = null;  //we we are an auxillialry spirit it can set its ref here

        public Dictionary<GameKey, List<Behavior>> _mapGamekeyToBehaviors;

        /// <summary>
        /// Lookup , given a GameKey , get all the behaviors associated with it. They will be played in list order.. on press key or Play( gey)   example.. GameKey.left initWalkLeft, WalkLeft 
        /// </summary>
        public Dictionary<GameKey, List<Behavior>> MapGamekeyToBehaviors
        {
            get { return _mapGamekeyToBehaviors; }
        }

        public bool IsRegrowing
        {
            get { return Effects.Contains(RegrowKey); }
        }


        /// <summary>
        /// Flag to inform that we are in deserialization proses. 
        /// Used by some property setter.
        /// </summary>
        private bool _ondeserializing;

        /// <summary>
        /// Flag to inform that we are in serialization proses. 
        /// Used by some property getter.
        /// </summary>
        private bool _onserializing;





        /// <summary>
        /// This is set by Level after all spirit deserialized. 
        /// </summary>
        private Level _level;




        public Level Level
        {
            get
            {
                return _level;
            }
        }

        /// <summary>
        /// sets the level and all its childen.. this a circular reference is possible,  dont use a setter it will loop for ever.  Its tightly coupled because they are always going to be in a level.
        /// </summary>
        /// <param name="level"></param>
        public void SetParentLevel(Level level)
        {
            _level = level;
            AuxiliarySpirits.ForEach(x => x._level = level);  //important for copy paste from one level to another
        }

        private int _mindUpdate;

        /// <summary>
        /// Number of bodies in spirit when loaded. Set when deserialized.
        /// Currently only used by RegenerateParts.
        /// </summary>
        private int _bodyCountWhenLoaded;

        //since plugins dont know about each other , these can be used to set data around on complex system.
        //now used by airship  balloon flame force..
        public float UserParam1;

        public bool IsCharging = false;  //is running

        /// <summary>
        /// Delay period (in second) to use before grow.
        /// Use 5s for debugging.
        /// </summary>
#if TEST_REGROW_ZERO_FORCE
        public const double GROW_DELAY = 0.1f;
#else
        public const double GROW_DELAY = 1;
#endif

        public const string grabDelay = "GrabAfterDropDelay";
        #endregion


        #region Properties

        //amount of energy given at level load in production build to main character ..TODO future .. should not be here, should be in level or biped
        public const float PlayerStartEnergy = 200;

        public static short CloudCollideId = -23674;//TODO get rid of magic num.. use statis simworld something..




        #region Spirit as Particle


        /// <summary>
        /// if  spirit leaves this area will delete itself.
        /// </summary>
        [DataMember]
        public AABB WorldAABBExistenceLimits;

        private bool _wasEmitted = false;
        /// <summary>
        /// Is this Spirit was emitter from an emitter
        /// </summary>
        [DataMember]
        public bool WasSpawned
        {
            get { return _wasEmitted; }
            set { _wasEmitted = value; }
        }



        public double _age = 0f;
        /// <summary>
        /// The Age of the Spirit when it was emitted
        /// </summary>
        public double Age
        {
            get { return _age; }
        }

        private double _lifeSpan = 20000f;

        /// <summary>
        /// The LifeSpan of the Spirit  in milliseconds when it was emitted
        /// </summary>
        public double LifeSpan
        {
            get { return _lifeSpan; }
            set { _lifeSpan = value; }
        }


        public bool IsCarnivore { get; set; }

        /// <summary>
        /// 1 for normal size, more for giant 2x..etc.
        /// </summary>
        public float SizeFactor;

        /// <summary>
        /// Check if the Spirit is dead if  it was emitted
        /// </summary>
        public bool IsExpired
        {
            get
            {
                //never expire these
                if (!WasSpawned || (MainBody.Info & BodyInfo.SpawnOnly) != 0)
                    return false;

                // Particle is killed  when spirit's AABB not overlap with WorldAABB , goes outside of world
                //TODO remove anything that goes outside of both  worlld and margin.. it can get stuck in next level.
                return (Age >= LifeSpan || !IsWithinWorldAABB());
            }
        }

        #endregion



        //TODO bind existing  tool sliders to this for tuning instead of global thing.  now applied in teh tool plugin since the tuning might apply without having to select the spirit uisng SimWorld.ParamA

        public float _paramA;
        /// <summary>
        /// Parameters for this instance set by tuning or by emitters or plugin, can be anthing, vortex speed, etc
        /// </summary>
        [DataMember]
        public float ParamA
        {
            get { return _paramA; }
            set
            {
                if (_paramA != value)
                {
                    _paramA = value;
                    NotifyPropertyChanged("ParamA");
                }
            }
        }

        public float _paramB;
        /// <summary>
        /// Parameters for this instance set by tuning or by emitters or plugin, can be anthing, vortex speed, etc
        /// </summary>
        [DataMember]
        public float ParamB
        {
            get { return _paramB; }
            set
            {
                if (_paramB != value)
                {
                    _paramB = value;
                    NotifyPropertyChanged("ParamB");
                }
            }
        }



        object _userData;

        [DataMember]   //this can be used for anything, typicall a struct defined in the plugin   TODO put a prop sheet like Body sound effects has.   This shoild replace all code that has if Level = 12 ... do this type waves, etc.
        //this is better than putting all this level specific stuff in the class.
        public object UserData
        {
            get { return _userData; }
            set
            {
                if (_userData != value)
                {
                    _userData = value;
                    NotifyPropertyChanged("UserData");
                }
            }
        }


        internal object _params;
        /// <summary>
        /// Data for use by plugins, in Datastructs marked with DataContract and DataMember that they can define
        /// Editable in Property sheet
        /// </summary>
        public object Params
        {
            get { return _params; }
            set
            {
                _params = value;
                NotifyPropertyChanged(nameof(Params));
            }
        }


        ///  string used to embed param types declared by the plugin script code so no tool core changes are needed to introduce a class or struct
        /// <summary>
        /// </summary>
        [DataMember]
        public string ParamStorage { get; set; }



        public bool PlaceJointBreakHanders { get; set; }


        private double _currentTime = 0;

        // used by Update and CurrentTime.
        private double _lastTime;

        /// <summary>
        /// This is to inform Update that CurrentTime has changed.
        /// Used to sync CurrentTime with physics thread, to prevent physics thread exception.
        /// </summary>
        private bool _needUpdateCurrentTime;

        private World _world;

        List<Body> _eyes = new List<Body>();
        List<Body> _headParts = new List<Body>();
        List<Body> _headAndNeckParts = new List<Body>();



        private Dictionary<AttachPoint, Body> mapatcPtToStruckBodies;

        /// <summary>
        /// map of stuff like bullets stuck in- side body.   so on regen it can put them back
        /// </summary>
        public Dictionary<AttachPoint, Body> MapAttachPtToStuckBodies
        {
            get
            {
                if (mapatcPtToStruckBodies == null)
                {
                    mapatcPtToStruckBodies = new Dictionary<AttachPoint, Body>();
                }
                return mapatcPtToStruckBodies;
            }
        }


        public World World  // set after loaded.. used for collision event like breakable body..
        {
            get { return _world; }

            set
            {

                if (_world != value)
                {

                    if (_world != null) //spirit is probably travelling to another level
                    {
                        _world.ContactManager.PostSolve -= PostSolve;
                    }

                    if (value != null)
                    {

                        // Careful, PostSolve will call all of listeners on everything.. TODO FUTURE remove this .. use the HandleBodyCollision in powered joint 
                        _world = value;
                        _world.ContactManager.PostSolve += PostSolve;// a proplem with this is all bodies have already been advanced in position and rotation after collision has solved              
                        //        _world.ContactManager.PreSolve += PreSolve;  // This could work but Farseer note says OldManifold likely will not have accurate impulse info
                        //  TODO must  to use the Before collision in PoweredJoint.. dont have to look up any bodies .    then pass it to here.    
                        //better attack BeforeCollisoin to head and torso.. or all limbs to check for bullet strikes or cuts before they happen.

                        //TODO .. OPTIMIZATION there is an event for after the thing is solved... with many spirit this can get complicated.. 2
                    }
                }
            }
        }

        public bool IsWalking { get; set; }//can be set by plugin

        [DataMember]
        public bool AddHeldItemsToCM { get; set; }//can be set by plugin



        /// <summary>
        /// Defaults to 1.  Can be used by plugin to slow or speed up current behavior without affecting the saved / tuned model.. dont have to worry about setting it back, as legacy plugins do
        /// This is multiplied with the current behaviors TimeDilateFactor
        /// </summary>
        public double TimeDilateAdjustFactor { get; set; }





        public bool IsThrustingLeft { get; set; }
        public bool IsThrustingRight { get; set; }

        public float TotalMass
        {
            get
            {
                //   UpdateTotalMass();  is  updated every frame on UpdateTotalAndCenterMass
                return _totalMass;
            }
        }


        //Animation stuff

        #region Keyframe Animation


        /// <summary>
        /// when true dont use the Key command mapd to the behavior.. for now used internally.
        /// </summary>
        public bool BlockMappedCommand;

        [DataMember]
        /// <summary>
        /// if true, wont revert to the start pose if letting go of a key that is mapped to a Keyframe sequence.  new, and legacy and default is false.  
        /// //especially important for bipeds, they will proably fall over if letting go at the wrong time.. REVIEW.. out first use of this spirit was biped definitely dont want to have it fall over on release key
        /// but for quadruped or more stable systems,  set to true.
        /// </summary>
        public bool PauseOnReleaseKey { get; set; }




        /// <summary>
        /// Current Time in the time line of active behavior
        /// </summary>
        [DataMember]
        public double CurrentTime
        {
            get { return _currentTime; }
            set
            {
                if (_currentTime != value)
                {
                    _lastTime = _currentTime;
                    _currentTime = value;

                    // Only update, if animation tool's interpolation enabled
                    if (IsAnimating == false && EnableDesignTimeInterpolation)
                    {
                        //This is unsafe, may cause assert.  will be updated on next update.
                        ////caution , dt can be backwards time if moving slider back.. but dont think any scripts are using it now..
                        //Update(_currentTime - lastTime, true);

                        // just raise flag, executing Update here can cause thread access exception
                        _needUpdateCurrentTime = true;
                    }

                    NotifyPropertyChanged("CurrentTime");
                }
            }
        }


        //NOTE currently no UI hooked up to this.. used to be, but i changed it to IsInterpolate.. which is more useful so we can see what keyframes angles are are exactly.
        //without having to place time exactly over the Key Frame (KF) 
        private bool _enableDesignTimeInterpolation = true;
        [DataMember]
        public bool EnableDesignTimeInterpolation
        {
            get { return _enableDesignTimeInterpolation; }
            set
            {
                if (_enableDesignTimeInterpolation != value)
                {
                    _enableDesignTimeInterpolation = value;
                    NotifyPropertyChanged("EnableDesignTimeInterpolation");
                }
            }
        }

        private List<IKeyframeFilter> _filters = new List<IKeyframeFilter>();
        /// <summary>
        /// Internal Filter collection for general Reset, and Update
        /// </summary>
        public List<IKeyframeFilter> Filters
        {
            get { return _filters; }
        }

        private EffectCollection _effects;
        /// <summary>
        /// Collection of Low Frequency Effects , like seizure , twitch, dance, bob head, blink, wave  etc, can affect spirit state, uses existing Filters above
        /// </summary>
        public EffectCollection Effects
        {
            get
            {
                if (_effects == null)
                {
                    _effects = new EffectCollection();
                }
                return _effects;
            }
        }

        /// <summary>
        /// Spirit is dead, not possible to animate..
        /// </summary>
        public bool IsDead
        {
            get { return (_isDead); }
        }

        /// <summary>
        /// Action has been paused by Pause method.  Play method will resume.
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return _isPaused;
            }
        }


        #endregion

        public bool CanPickupProjectileWeapon { get; set; }

        protected PoweredJointCollection _pwdjoints;

        [DataMember]
        public PoweredJointCollection Joints
        {
            get { return _pwdjoints; }
            set { _pwdjoints = value; }    // for serialization only, do not access

            // Note: _currentAngles might need to be updated when number of joints changed
        }

        protected JointCollection _fixedJoints;
        /// <summary>
        /// Fixed Joints collection such as Welds.  These may be traversed by Joint Graph but not powered or controlled
        /// </summary>
        [DataMember]    // for copy paste serialization
        public JointCollection FixedJoints
        {
            get
            {
                if (_fixedJoints == null)
                {
                    _fixedJoints = new JointCollection();
                }
                return _fixedJoints;

            }
            set { _fixedJoints = value; } // for serialization.. is needed   should not be allowed to change it, just emply and clear
        }

        protected List<Body> _bodies = null;
        /// <summary>
        /// List of Bodies for this Spirit.
        /// Because basically we use graph structure for Spirit, both Joints and 
        /// Bodies are required, even though we able to obtain Bodies from Joints. 
        /// Bodies should be independent from Joints.
        /// </summary>
        [DataMember]
        public List<Body> Bodies
        {
            get { return _bodies; }
            set { _bodies = value; }
        }

        //a cache of our bodies , can be searched faster than list.
        public FarseerPhysics.Common.HashSet<Body> _bodySet;


        //TODO redundant with Bodies. Consider removing Bodes somehow and do something on deserialized.  but i think this may  require resaving all levles.
        /// <summary>
        /// A set of Current bodies currently connected to spirit, can be searched faster than Bodies, is a set which cannot have dublicates
        /// Included since legacy files already serialized Bodies.. 
        /// </summary>
        public FarseerPhysics.Common.HashSet<Body> BodySet
        {
            get
            {
                if (_bodySet == null)
                {
                    _bodySet = new FarseerPhysics.Common.HashSet<Body>();
                }
                return _bodySet;
            }
        }

        protected List<Body> _bodiesOrig = null;

        /// <summary>
        /// List of original Bodies associated with this spirit.  
        /// </summary>

        public List<Body> BodiesOrig
        {
            get { return _bodiesOrig; }
        }




        public GameKey _gameKeyState;


        /// <summary>
        /// State of game key input for this spirit. Read-only, not  serializable state, 
        /// reflects state of command input, AI issued current order, keyboard or controller   
        ///  Other non-ActiveSpirit object can receive this KeyState through Control-attachpoint mechanism.  This is a way to command non player characters.
        ///  Only one keyboard is supported now each spirit is can  have commands routed to it.  so in theory, a two player , one keyboard game could be made
        /// NOTE issue  can happen with nested items under control with 
        /// spirit. such as dual machine gun, balloon on ship..  system wide key state methods available but discouraged

        /// </summary>
        public GameKey GameKeyState
        {
            get { return _gameKeyState; }
        }

        /// <summary>
        /// copy the state bits to this spirits input state, Can be used to override, script or control handled object
        /// </summary>
        /// <param name="controllerKeys"></param>
        public void CopyCmdStateFrom(GameKey controllerKeys)
        {
            _gameKeyState = controllerKeys;
        }


        protected OffsetFilter _offsetFilter1 = null;
        /// <summary>
        /// Get Offset Filter from Cache if available, otherwise, create new one
        /// </summary>
         public OffsetFilter OffsetFilter1
        {
            get
            {
                CheckToCreateFilters();
                return _offsetFilter1;
            }
        }


        protected OffsetFilter _offsetFilter2 = null;
        /// <summary>
        /// second Offset Filter, will be added to the  first one.
        /// </summary>
        public OffsetFilter OffsetFilter2
        {
            get
            {
                CheckToCreateFilters();
                return _offsetFilter2;
            }
        }

        protected OffsetFilter _offsetFilter3 = null;
        /// <summary>
        /// third  Offset Filter, will be added to the  first one.
        /// </summary>
        public OffsetFilter OffsetFilter3
        {
            get
            {
                CheckToCreateFilters();
                return _offsetFilter3;
            }
        }


        protected TargetFilter _targetFilter = null;

 
         public TargetFilter TargetFilter
        {
            get
            {
                CheckToCreateFilters();
                return _targetFilter;
            }
        }

        protected TargetFilter _targetFilterEx = null;

        public TargetFilter TargetFilterExclusive
        {
            get
            {
                CheckToCreateFilters();
                return _targetFilterEx;
            }
        }


        protected LimitFilter _limitFilter = null;

        public LimitFilter LimitFilter
        {
            get
            {
                CheckToCreateFilters();
                return _limitFilter;
            }
        }


        protected Behavior _activeBehavior = null;
        /// <summary>
        /// Currently Active Behavior.   This spirit is a delicate State Machine. Transitions between Behaviors are determined by if the Behaviors are part of a sequence of two, meaning two mapped to one key
        /// and one is marked IsFirstTimeExec.  The Play method determines _spiritPlay mode which is usually SpiritPlay.Repeat;  the Current state is the ActiveBehavior .  The Pose, or all the joint angles,  is determined by this,  and the CurrentTime, and the Filters, and IsInterpolating and    (ActiveBehavior.TimeDilateFactor * TimeDilateAdjustFactor
        /// </summary>


        [DataMember]  //if change.. see if levels break..usually its set to some default.. put a name  Name = ActiveBehavior
        public Behavior ActiveBehavior
        {
            get
            {
                if (_activeBehavior == null)
                {
                    _activeBehavior = new Behavior();
                }

                return _activeBehavior;
            }
            set
            {
                //only notify if it is really changing...
                if (value == null || value == _activeBehavior)   //null can get set from combo box binding during edit name, dont allow it.
                    return;

                _activeBehavior = value;

                NotifyPropertyChanged("ActiveBehavior");
            }
        }



        protected BehaviorCollection _behaviors;
        [DataMember]
        public BehaviorCollection Behaviors
        {
            get { return _behaviors; }
            set { _behaviors = value; }     // for deserialization only, do not access  .
        }


        bool _bIsAnimating = false;
        [DataMember]
        public bool IsAnimating
        {
            get { return _bIsAnimating; }
            set
            {
                _bIsAnimating = value;

                if (_bIsAnimating == true)
                    _isPaused = false;

                NotifyPropertyChanged("IsAnimating");
            }
        }


        bool _canBalance = true;
        public bool CanBalance
        {
            get { return _canBalance; }
            set
            {
                _canBalance = value;
                NotifyPropertyChanged("CanBalance");
            }
        }


        string _name = "NoName";
        [DataMember]
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                NotifyPropertyChanged("Name");
            }
        }


        [DataMember]
        public string SpiritFilename
        { get; set; }


        bool _bIsAutoReverse = true;
        [DataMember]
        public bool IsAutoReverse
        {
            get { return _bIsAutoReverse; }
            set
            {
                _bIsAutoReverse = value;
                NotifyPropertyChanged("IsAutoReverse");
            }
        }


        bool _bIsInterpolate = true;

        /// <summary>
        /// Interpolates  poses between the keyframes based on CurrentTime, currently using linear interpolation.  Can be shut off in tool to see the values of a keyframe.  
        ///  Not interpoling might have some uses.. A movement might make very quick transitions based on bias , between several poses with a spane of time between. 
        ///  system uses the first pose to the right of the time.  With interpoliaton, we have to use a set of two frames to key a pose for a duration.
        /// </summary>
        [DataMember]
        public bool IsInterpolate
        {
            get { return _bIsInterpolate; }
            set
            {
                _bIsInterpolate = value;
                NotifyPropertyChanged("IsInterpolate");
            }
        }


        bool isSplineInterpolate = false;
        /// <summary>
        /// Use fit spline to get smoother motion  //TODO not implememented
        /// </summary>
        public bool IsSplineInterpolate
        {
            get { return isSplineInterpolate; }
            set
            {
                isSplineInterpolate = value;
                FirePropertyChanged();
            }
        }


        double _maxTimeLine = 3;  //3 sec default since most moves are quick
        [DataMember]
        public double MaxTimeLine
        {
            get { return _maxTimeLine; }
            set
            {
                _maxTimeLine = value;
                NotifyPropertyChanged("MaxTimeLine");
            }
        }

        protected Vector2 _worldCenter;


        protected Vector2 _worldCenterPrev;


        /// <summary>
        /// Next Behavior to be executed. Might use internal list in the future.
        /// </summary>
        /// <returns></returns>
        public Behavior NextBehavior { get; set; }

        /// <summary>
        /// Behavior that saved before fall. This should be resumed after fall.
        /// </summary>
        public Behavior BehaviorBeforeFall { get; set; }


        private string _pluginScript = "";

        /// <summary>
        /// Plugin Script only used by Tool only for Plugin code design
        /// </summary>

        //Never save in file,  its not encrypted , just  a cache
        public string PluginScript
        {
            get { return _pluginScript; }
            set
            {
                _pluginScript = value;
                NotifyPropertyChanged("PluginScript");
            }
        }

        public bool IsScriptDirty
        {
            get;
            set;
        }

        //eyes can  converge on this point.
        public Vector2 PositionLookingAt
        {
            get;
            set;
        }


        private string _pluginName = "";


        /// <summary>
        /// Name of plugin class..  Script will be store in PluginName.cs
        /// </summary>
        [DataMember]
        public string PluginName
        {

            get { return _pluginName; }
            set
            {
                _pluginName = value;
                NotifyPropertyChanged("PluginName");
            }
        }

        private IPlugin<Spirit> _plugin = null;

        /// <summary>
        /// Each Spirit can have one Plugin  to extend its functionalty and give it custom environement detection and take custom user input.  
        /// </summary>
        public IPlugin<Spirit> Plugin
        {
            get { return _plugin; }
            set
            {
                if (_plugin != value)
                {
                    _plugin = value;
                }
            }
        }


        /// <summary>
        /// Inform if we are currently executing automated behavior, as opposed
        /// to keyed (input-based) behavior.
        /// </summary>
        public bool IsExecutingFirstCycleAutoBehavior { get; set; }



        ///Area to detect bodies for pickup, spirts to interact with,  etc.
        public float BodyProximityDetectWidth { get; set; }
        public float BodyProximityDetectHeight { get; set; }



        private Fixture sensorFixture;
        /// <summary>
        /// This sensor is used for contact detection between this spirit and external 
        /// body. Default is NULL, will be non-null if SensorRadius > 0. By default,
        /// sensor will be attached to MainBody. Sensor will be rebuilt everytime
        /// sensor radius changed or deserialized.
        /// </summary>
        public Fixture SensorFixture
        {
            get { return sensorFixture; }

            set { sensorFixture = value; }
        }


        private float _sensorRadius;
        /// <summary>
        /// Get or set the radius of Sensor fixture. This also create and destroy 
        /// Sensor. Default is 0. Set to 0 or below to destroy sensor and also 
        /// trigger sensor view cleanup.
        /// </summary>
        [DataMember]
        public float SensorRadius
        {
            get { return _sensorRadius; }
            set
            {
                _sensorRadius = value;

                if (_ondeserializing == true)
                    return;

                if (_sensorRadius > 0)
                {
                    ResizeSensor();
                }
                else
                {
                    DisposeSensor();
                }

                FirePropertyChanged();
            }
        }



        private float _sensorAspectRatio = 1f;
        /// <summary>
        /// Get or set the  Aspect Ratio, or Width to Height of Sensor rectangle Sensor fixture. 
        /// Default is 1, a  square.   Width is the radius x 2, and Height is determend by Width / the value of this AspectRatio v
        /// trigger sensor view cleanup.   Zero is invalid.   Not currently supported for circle shaped sensor
        /// </summary>
        [DataMember]
        public float SensorAspectRatio
        {
            get { return _sensorAspectRatio; }
            set
            {

                if (value <= 0)
                {
                    value = 1f;
                }

                _sensorAspectRatio = value;

                if (_ondeserializing)
                    return;

                if (_sensorRadius > 0)
                {
                    ResizeSensor();
                }

                FirePropertyChanged();
            }
        }



        /// <summary>
        /// Maximum number of shapes involved in the AABB sensor.
        /// Defaults to 300
        /// </summary>
        public int MaxShapes;

        protected Fixture[] _shapesDetected;

        /// <summary>
        /// External body that collide with our sensor. Doesn't include Bodies from our Spirit.
        /// </summary>
        public FarseerPhysics.Common.HashSet<Body> BodiesInSensor;


#if CIRCLESENSOR

        /// <summary>
        /// External body that collide with our circle sensor just around spirit to wake up collision of internal parts.
        /// </summary>
        public FarseerPhysics.Common.HashSet<Body> BodySetInCircleSensor;
#endif


        //TODO probably  query the level for these if there are few.. dont use huge broad phase query to find potential spirit threats

        /// <summary>
        /// Other spirits that have its MainBody collides with our sensor. Doesn't include our Spirit.
        /// </summary>
        public List<Spirit> SpiritsInSensor;

        /// <summary>
        /// All sharp points that have parent Body inside our sensor. 
        /// Doesn't include SharpPoint in our Spirit. Includes sharp points in HeldSharpPoint.
        /// Further distance measurement is required to determine sharp point inclusion in sensor.
        /// </summary>
        public List<SharpPoint> SharpPointsInSensor;

        /// <summary>
        /// All attach points that have parent Body inside our sensor. 
        /// Doesn't include AttachPoint in our Spirit.
        /// Further distance measurement is required to determine attach point inclusion in sensor.
        /// </summary>
        public List<AttachPoint> AttachPointsInSensor;


        private float _energyLevel;

        [DataMember]
        public float EnergyLevel
        {
            get { return _energyLevel; }
            set
            {
                _energyLevel = value;
                NotifyPropertyChanged("EnergyLevel");
            }
        }

        //TODO use this.. show this in energy bar.
        //allow temporary tired ness.
        // if power consumtion too high  reduce.
        //have a Quiescent power use rate if which exceeded causes tiredness

        private float _storedEnergy;
        [DataMember]
        public float StoredEnergy
        {
            get { return _storedEnergy; }
            set
            {
                _storedEnergy = value;
                NotifyPropertyChanged("StoredEnergy");
            }
        }

        /// <summary>
        /// Toggle if to put blood emitters on joints automatically..
        /// </summary>
        [DataMember]
        public bool CanBleedOnBreak { get; set; }



        private Body _mainBody;
        [DataMember]
        public Body MainBody
        {
            get
            {
                // saving spirit should not cause MainBody to change.
                if (_onserializing == false)  //NOTE this could casue issue with creating new spirit, then saving..
                {
                    // if no main body yet, generate one by finding the  body has biggest joint count, the nexus
                    if (_mainBody == null)
                    {
                        _mainBody = GraphWalker.FindOrDetermineAndMarkMainBody(_bodies);
                    }
                }
                return _mainBody;
            }
            set
            {
                _mainBody = value;
            }
        }

        private Body _head;


        /// <summary>
        /// Spirit head. Set null when head severed.
        /// </summary>
        public Body Head
        {
            get
            {
                if (_head == null)
                {
                    _head = GetBodyWithPartType(PartType.Head);
                }
                return _head;
            }
            set
            {
                _head = value;
            }

        }



        private Body _lowerJaw;

        /// <summary>
        /// Spirit LowerJaw. 
        /// </summary>
        public Body LowerJaw
        {
            get
            {
                if (_lowerJaw == null)
                {
                    _lowerJaw = GetBodyWithPartType(PartType.LowerJaw, false, true);
                }
                return _lowerJaw;
            }
            set
            {
                _lowerJaw = value;
            }
        }


        /// <summary>
        /// Mind / AI for spirit. Default is null.
        /// </summary>
        [DataMember]
        public Mind Mind { get; set; }


        /// <summary>
        /// Create or destroy spirit.Mind. For use by property grid.
        /// </summary>
        public bool IsMinded
        {
            get { return (Mind != null); }
            set
            {
                if (value == true)
                {
                    Mind = new Mind(this);
                }
                else
                {
                    Mind = null;
                }

                NotifyPropertyChanged("IsMinded");
            }
        }


        /// <summary>
        /// if true, calculate the TotalForce on each body in the spirit.   Also handle damage.. for now if minded will bruise, later devices should be able ot take damage, it should go to the plugin
        /// </summary>
        public bool DoPostSolve { get; set; }


        private short _groupID;
        public short CollisionGroupId
        {
            get
            {
                if (_groupID == 0)
                {
                    _groupID = GetNextSpiritCollisionGroup();
                }

                return _groupID;
            }

            set
            {
                _groupID = value;
                foreach (Body b in Bodies)
                {
                    b.CollisionGroup = _groupID;
                }
            }
        }

        private bool _isSelfCollide = false;
        /// <summary>
        /// Determine if collision will happen between spirit pieces. Default is false.
        /// </summary>
        [DataMember]
        public bool IsSelfCollide
        {
            get { return _isSelfCollide; }
            set
            {
                //    if (_isSelfCollide == value)  //dont do this.. since after setting this , individual parts might be set  differently
                //  return;

                _isSelfCollide = value;

                if (_isSelfCollide)
                {
                    // reset collision group of all bodies
                    foreach (Body b in Bodies)
                    {
                        b.CollisionGroup = 0;
                    }
                }
                else
                {
                    // disable collision between spirit pieces by setting all fixtures
                    // under the same collision group.  

                    // bodies
                    foreach (Body b in Bodies)
                    {
                        b.CollisionGroup = CollisionGroupId;
                        /*
                        foreach (PartType pt in AllwaysSelfCollideParts)
                        {
                            if (b.PartType == pt)
                            {
                                b.CollisionGroup = 0;
                                break;
                            }
                        }*/
                    }

                    NotifyPropertyChanged("IsSelfCollide");
                }
            }
        }

        private bool _isCallingPlugin = true;

        [DataMember]
        public bool IsCallingPlugin
        {
            get { return _isCallingPlugin; }
            set
            {
                _isCallingPlugin = value;
                NotifyPropertyChanged("IsCallingPlugin");


                if (IsCallingPlugin == false)
                {
                    ClearFilterValues();
                }
            }
        }


        public void ClearFilterValues()
        {
            if (Filters == null)
                return;

            Filters.ForEach(x => x.Clear());
        }



        //TODO move  this to creature.    
        /// <summary>
        /// Tribe name.. optional  metadata might govern how spirit react to each other.. warring.. friend , etc.
        /// </summary>
        [DataMember]
        public string Tribe { get; set; }


        //TODO move  this to creature.
        /// <summary>
        /// Indicate which direction spirit is facing right now.
        /// Set in script by InitLeft / InitRight animation. Required by stab / punch animation.
        /// </summary>
        public bool IsFacingLeft { get; set; }

        /// <summary>
        /// Another spirit that being held and will be given control to.
        /// Keyboard input will first be directed to this held spirit, and
        /// holder can still handle the unused keys.
        /// </summary>
        public Spirit HeldSpiritUnderControlLeft { get; set; }
        public Spirit HeldSpiritUnderControlRight { get; set; }



        /// <summary>
        /// Another spirit that being held and not given control too.  
        /// As in another bipeds finger, friend or foe.. or some device held by a grip that does not give you control.
        /// </summary>
        public Spirit HeldSpiritLeft { get; set; }
        public Spirit HeldSpiritRight { get; set; }


        /// <summary>
        /// Current held grip on weapon or other device
        /// </summary>
        public AttachPoint HeldGripLeft;
        public AttachPoint HeldGripRight;

        /// <summary>
        /// List of sharp points on weapon currently attached to spirit.
        /// This helps Mind to ignore sharp point in our own held weapons. 
        /// Updated internally by UpdateHeldItemsLists().
        /// </summary>
        public List<SharpPoint> HeldSharpPoints { get; set; }

        /// <summary>
        /// List of Bodies held, so  sensor rays can ignore them..
        /// Updated internally by UpdateHeldItemsLists().
        /// </summary>
        public List<Body> HeldBodies { get; set; }


        public Body HeldBodyLeft { get; set; }
        public Body HeldBodyRight { get; set; }
        public bool HoldingRope { get; set; }
        public bool HoldingClimbHandle { get; set; }
        public bool HoldingClimbHandleLeft { get; set; }
        public bool HoldingClimbHandleRight { get; set; }


        public bool HoldingLeftRightInputDevice { get; set; }
        public bool HoldingGun { get; set; }


        /// <summary>
        ///  Tells the view the spirit would like to glow with this color, ie after eating something.
        /// </summary>
        public BodyColor GlowColor { get; set; }


        /// <summary>
        ///  collor of liquid comes from breaking joints.. Red by default.
        /// </summary>
        [DataMember]
        public BodyColor BloodColor { get; set; }




        protected List<Spirit> _auxiliarySpirits;
        /// <summary>
        /// list of spirit connected to it.. for complex items like ballon ship
        /// </summary>
        [DataMember]
        public List<Spirit> AuxiliarySpirits
        {
            get
            {
                if (_auxiliarySpirits == null)
                {
                    _auxiliarySpirits = new List<Spirit>();
                }
                return _auxiliarySpirits;
            }

            set { _auxiliarySpirits = value; } // needed for serialization.. In standard object models,   should not be allowed to change it, just emply and clear
        }

        protected JointCollection _auxiliarySpiritJoints;
        /// <summary>
        /// Joints that used to connect auxiliary spirit.  Must be marked as SkipTraversal.
        /// NOTE: these joints no necessarily connected to this main spirit, can be between 2 aux spirit.
        /// </summary>
        [DataMember]
        public JointCollection AuxiliarySpiritJoints
        {
            get
            {
                if (_auxiliarySpiritJoints == null)
                {
                    _auxiliarySpiritJoints = new JointCollection();
                }
                return _auxiliarySpiritJoints;
            }

            // for serialization.. set is needed .  In standard object models,   should not be allowed to change it, just empty and clear
            set { _auxiliarySpiritJoints = value; }
        }



        /// <summary>
        ///  Glow level, between zero and 1
        /// </summary>
        public double GlowBrightness { get; set; }


    

        //to set uniform Softness and JointBias on all powered joints in system
        private float _jointsoftness = float.NaN;  //TODO REVIEW..  

        //similar to opposite of Bias .. using softness lets us keep Bias untouched, its a measure of strength, but also of damping.. softness does resist movement and works with dampers..see Box2d documentation
        public float JointSoftness
        {
            // joints might have individual values , this only reflects last set..
            get { return _jointsoftness; }  //toDO    RETURN NAN if they are not all same..
            set
            {
                if (_jointsoftness != value)
                {
                    _jointsoftness = value;
                    ApplyJointSoftness(_jointsoftness);
                    NotifyPropertyChanged("JointSoftness");
                }
            }
        }

        private float _jointBias = 0;
        public float JointBias
        {
            // joints might have individual values , this only reflects last set for all
            get { return _jointBias; } //TODO .. if not all the same return NaN.. 
            set
            {
                if (_jointBias != value)
                {
                    _jointBias = value;
                    ApplyJointBias(_jointBias);
                    NotifyPropertyChanged("JointBias");
                }
            }
        }


        private bool _isWeak = false;
        public bool IsWeak
        {
            get { return _isWeak; }
            set
            {
                if (_isWeak != value)
                {
                    _isWeak = value;
                    NotifyPropertyChanged("IsWeak");
                }
            }
        }


        private bool _isTired = false;
        public bool IsTired
        {
            get { return _isTired; }

            set
            {
                if (_isTired != value)
                {
                    _isTired = value;
                    NotifyPropertyChanged("IsTired");
                }
            }
        }


        #endregion



        #region Constructor

        public Spirit()
        {
            _pwdjoints = new PoweredJointCollection();
            Initialize();
        }



        public Spirit(Body mainBody, List<Body> bodies, PoweredJointCollection joints)
        {
            MainBody = mainBody;
            _pwdjoints = new PoweredJointCollection(joints);
            _bodies = new List<Body>(bodies);
            Initialize();
        }



        public Spirit(Body mainBody, List<Body> bodies)
        {
            MainBody = mainBody;
            _pwdjoints = new PoweredJointCollection();
            _bodies = new List<Body>(bodies);
            Initialize();
        }


        public Spirit(List<Body> bodies)
        {
            _pwdjoints = new PoweredJointCollection();
            _bodies = new List<Body>(bodies);
            Initialize();
        }



        #endregion



        #region Init

        private void Initialize()
        {

            IsScriptDirty = false;

            _energyLevel = 100000;         // initial energy level in Joules.

            if (_bodies == null)
            {
                _bodies = new List<Body>();
            }

            _behaviors = new BehaviorCollection();
            _behaviors.CollectionChanged +=
                new NotifyCollectionChangedEventHandler(behaviors_CollectionChanged);

            InitCommon();

            _behaviors.Add(new Behavior("Default"));


            IsSelfCollide = false;

            CreateFilters();

            _fixedJoints = new JointCollection();

            TimeDilateAdjustFactor = 1d;

            ResetHoldingStates();

        }


        private void CheckToCreateFilters()
        {
            if (_filters.Count() == 0)
            {
                CreateFilters();
            }

        }
        private void CreateFilters()
        {
            // Cached filter
            _offsetFilter1 = new OffsetFilter(Joints.Count);
            _offsetFilter2 = new OffsetFilter(Joints.Count);
            _offsetFilter3 = new OffsetFilter(Joints.Count);

            _targetFilter = new TargetFilter();
            _limitFilter = new LimitFilter();
            _targetFilterEx = new TargetFilter();

            _filters.Add(_targetFilter);
            _filters.Add(_offsetFilter1);
            _filters.Add(_offsetFilter2);
            _filters.Add(_offsetFilter3);
            _filters.Add(_limitFilter);  //needs to be last to limit results
            _filters.Add(_targetFilterEx);//needs to be last to reset prev results
          }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            try
            {

                TimeDilateAdjustFactor = 1;
                CreateLists();

                InitCommon();

                // reconnect event handler
                _behaviors.CollectionChanged += new NotifyCollectionChangedEventHandler(behaviors_CollectionChanged);

                UpdateTotalAndCenterMass();

                CacheJointProperties();

                EntityHelper.CalcCM(Bodies, out _worldCenterPrev, out _totalMass);// initialize _worldCenterPrev


                BlockMappedCommand = false;

                foreach (Body body in Bodies)
                {
                    body.ResetMassData();
                }

                // set owner for mind
                if (Mind != null)
                {
                    Mind.Parent = this;
                }

                CanBalance = true;


                GrabbingDepthMax = 0.08f; //half of hand is max..  we do finger len.., makes sense.. about half that is exact..
                //this is set in plugiin using the sizefactor

                // set original bodies count
                _bodyCountWhenLoaded = _bodies.Count;


                _ondeserializing = false;

            }
            catch (Exception exc)
            {
                Debug.WriteLine(" error in deserialzing (loading) spirit" + exc.Message);
            }

        }

        private void CreateLists()
        {
            if (_filters == null)
            {
                _filters = new List<IKeyframeFilter>();
            }

            if (_bodies == null)
            {
                _bodies = new List<Body>();
            }

            if (_pwdjoints == null)
            {
                _pwdjoints = new PoweredJointCollection();
            }

            if (_fixedJoints == null)
            {
                _fixedJoints = new JointCollection();
            }

        }





        public bool OnCollisionEventHandler(Fixture fixtureA, Fixture fixtureB, Contact contact)
        {
            return CollisionEffects.OnCollisionEventHandler(fixtureA, fixtureB, contact, this);
        }

        /// <summary>
        /// Common initialization that performed  both constructor and deserialization.
        /// </summary>
        private void InitCommon()
        {

            SizeFactor = 1;
            BodiesInSensor = new FarseerPhysics.Common.HashSet<Body>(32);  //TODO make this lazy load, its used for clouds breaking, fucksake
            SpiritsInSensor = new List<Spirit>();
            SharpPointsInSensor = new List<SharpPoint>();
            AttachPointsInSensor = new List<AttachPoint>();

            _mapGamekeyToBehaviors = new Dictionary<GameKey, List<Behavior>>();
            RebuildKeyMapCache();


            CanPickupProjectileWeapon = true;   //turn off if creature doesn't know how to pickup a gun.


#if CIRCLESENSOR
            BodySetInCircleSensor = new FarseerPhysics.Common.HashSet<Body>(32);
#endif


       


            if (MainBody == null)
            {
                MainBody = GraphWalker.FindOrDetermineAndMarkMainBody(_bodies);
            }


            if (MainBody != null && (MainBody.Info & BodyInfo.Cloud) != 0)
            {
                PlaceJointBreakHanders = false; //this is because the cloud is two puffs with a joint, it it breaks there are issues.
                //clouds dont bleed.. theres two much creature specific stuff in Spirit..  TODO cleanup.
            }
            else
            {
                PlaceJointBreakHanders = true;  //TODO revisit this.. too specific.  this was added for laser cut to shut it off, dont need to break the joint..  many devices don't need any of that stuff.. its for bleeding either hydralics or blood.
            }


            if (PlaceJointBreakHanders)
            {
                AddJointEventHandlers();
            }


            // ensure head part initialized
            Body dummy = Head;

            HeldSharpPoints = new List<SharpPoint>();
            HeldBodies = new List<Body>();


            _bodiesOrig = new List<Body>(Bodies);  //the original copy of the Bodies which will change on breaking.

            Bodies.ForEach(body => body.AttachPoints.ForEach(x => x.Detached += OnAttachPointDetached));
            AttachFixtureListeners();

            if (CanBleedOnBreak)
            {
                AddOrAdjustBloodEmittersToJoints();
            }

            CollectEyes();
            CollectHeadBodies();
            MaxShapes = 300;

            _shapesDetected = new Fixture[MaxShapes + 1];

            BodyProximityDetectWidth = 20; //todo try going back to  a sensor instead,  check for static
            BodyProximityDetectHeight = 15;//default he looks at low clouds..

            CacheBodySet();

            AveragingInterval = 0.6f;

            if (MainBody != null)   //TODO cleanup.. only happens with planet.. sprit is too hacked up for special spirits anyways
            {
                _posA = MainBody.Position;
            }


            AddHeldItemsToCM = true;
        

       

        }

        public void AttachFixtureListeners()
        {
            Bodies.ForEach(body => body.OnCollision += OnCollisionEventHandler);
        }

        void CacheBodySet()
        {
            BodySet.Clear();
            Bodies.ForEach(x => BodySet.Add(x));
        }


        #endregion

        #region Public Methods


        public bool IsWithinWorldAABB()
        {
            return AABB.TestOverlap(ref WorldAABBExistenceLimits, ref this.AABB);
        }


        /// <summary>
        /// gets the foot body furthest out ( can walk on shins).. 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="preferHeel">preferHeel  take heel even if toe present</param>
        /// <returns></returns>


        /// <summary>
        /// get the  joint  near contact location withing distancesq.
        /// for placing marks . dont want to place near joint since collision geom might not be near dress.  for bullet , can break the joint
        /// </summary>
        /// <param name="body"></param>
        /// <param name="contactWorldPosition"></param>
        /// <param name="minDistanceToJointAnchorSq"></param>
        /// <param name="info">skip joints connecting body with  this info </param>
        /// <returns>The Joint if found or null</returns>
        public static Joint GetFirstJointWithinDistance(Body body, ref Vector2 contactWorldPosition, float minDistanceToJointAnchorSq, bool breakableOnly, BodyInfo info)
        {
            Joint nearJoint = null;
            List<Joint> joints;
            List<Joint> auxjoint = new List<Joint>();
            GraphWalker.GetJointsFromBody(body, out joints, ref auxjoint);
            foreach (Joint joint in joints)
            {
                Vector2 worldAnchor = Vector2.Zero;

                if (breakableOnly && joint.Breakpoint == float.MaxValue)
                    continue;

                if ((joint.BodyA.Info & info) != 0)
                    continue;

                if ((joint.BodyB.Info & info) != 0)
                    continue;

                if (joint.BodyA == body)
                {
                    worldAnchor = joint.WorldAnchorA;
                }
                else if (joint.BodyB == body)
                {
                    worldAnchor = joint.WorldAnchorB;
                }
                else
                    continue;

                if ((worldAnchor - contactWorldPosition).LengthSquared() < minDistanceToJointAnchorSq)
                {
                    nearJoint = joint;
                    return nearJoint;
                }
            }
            return nearJoint;
        }




        /// <summary>
        /// get the  joint  near contact location withing distancesq.
        /// for placing marks . dont want to place near joint since collision geom might not be near dress.  for bullet , can break the joint
        /// </summary>
        /// <param name="body"></param>
        /// <param name="contactWorldPosition"></param>
        /// <param name="minDistanceToJointAnchorSq"></param>
        /// <param name="info">skip joints connecting body with  this info </param>
        /// <returns>The Joint if found or null</returns>
        public static Joint GetFirstBulletJointWithinDistance(Body body, ref Vector2 contactWorldPosition, float minDistanceToJointAnchorSq, BodyInfo info)
        {
            Joint nearJoint = null;
            List<Joint> joints;
            List<Joint> auxjoint = new List<Joint>();


            GraphWalker.GetJointsFromBody(body, out joints, ref auxjoint);
            foreach (Joint joint in joints)
            {
                Vector2 worldAnchor = Vector2.Zero;

                if ((joint.BodyA.Info & info) == 0 && (joint.BodyB.Info & info) == 0)  //looking for bullets..  NOTE  BodyB is usually the bullet, probably always..TODO   OPTIMIZATION CHECK
                    continue;

                if (joint.BodyA == body)  //TODO optimize..its alway one or the other..
                {
                    worldAnchor = joint.WorldAnchorA;
                }
                else if (joint.BodyB == body)
                {
                    worldAnchor = joint.WorldAnchorB;
                }
                else
                    continue;

                if ((worldAnchor - contactWorldPosition).LengthSquared() < minDistanceToJointAnchorSq)
                {
                    nearJoint = joint;
                    return nearJoint;  //not complete, just one is enough , because this is for surface bullets .. that are sticking out..  deep ones are using marks.  then we DRIVE FURTHEST FROM THE contactWorldPosition.. UNLESS WE ARE REMOVEING CHUNKS..
                }
            }

            return nearJoint;

        }


        #region HeldBodyMethods

        /// <summary>
        /// is holding any item (food, weapon, container, etc).
        /// </summary>
        /// <param name="left"></param>
        /// <returns></returns>
        public bool IsHoldingBody(bool left)
        {
            return left ? HeldBodyLeft != null : HeldBodyRight != null;
        }

        /// <summary>
        /// is holding the Body inputed
        /// </summary>
        /// <param name="left"></param>
        /// <returns></returns>
        public bool IsHoldingObject(Body heldBody)
        {
            return HeldBodyLeft != heldBody && HeldBodyRight != heldBody;
        }


        public bool IsHoldingItemWithPartType(bool left, PartType partType)
        {
            return left ?
                (HeldBodyLeft != null) && (HeldBodyLeft.PartType == partType) :
                (HeldBodyRight != null) && (HeldBodyRight.PartType == partType);
        }

        public bool IsHoldingFood(bool left)
        {
            return IsHoldingItemWithPartType(left, PartType.Food);
        }

        public bool IsHoldingContainer(bool left)
        {
            return IsHoldingItemWithPartType(left, PartType.Container);
        }

        public bool IsHoldingWeapon(bool left)
        {
            Body body = GetHeldBody(left);
            return (IsHoldingBody(left) && body.IsWeapon);
        }

        public bool IsHoldingGun(bool left)
        {
            Body body = GetHeldBody(left);
            return (IsHoldingBody(left) && body.IsInfoFlagged(BodyInfo.ShootsProjectile));
        }




        public bool GetIsHoldingLoadedGun(bool left)
        {
            Body body = GetHeldBody(left);
            return (IsHoldingBody(left) && body.IsInfoFlagged(BodyInfo.ShootsProjectile) && (body.Flags & BodyFlags.IsSpent) == 0);
        }


        public bool IsHoldingLoadedGun
        {
            get
            {
                return GetIsHoldingLoadedGun(true) || GetIsHoldingLoadedGun(false);
            }

        }


        public bool IsHoldingSharpWeapon(bool left)
        {
            Body body = GetHeldBody(left);
            return (IsHoldingBody(left) && body.IsWeapon && body.SharpPoints.Count() > 0);
        }


        /// <summary>
        /// true if holding a body (weapon, food, item) on the front side/ facing side.
        /// </summary>
        /// <returns></returns>
        public bool IsHoldingItemForward()
        {
            return IsHoldingBody(IsFacingLeft);
        }


        /// <summary>
        /// true if holding a body (weapon, food, item) on one side only, not both.
        /// </summary>
        /// <returns></returns>
        public bool IsHoldingItemOneSideOnly()
        {
            return IsHoldingBody(true) ^ IsHoldingBody(false);
        }


        /// <summary>
        /// Holding a weapon in forward hand..  for now its assumed to have a sharp point for some functions, like striking at joints.
        /// </summary>
        /// <returns></returns>
        public bool IsHoldingWeaponForward()
        {
            return IsHoldingWeapon(IsFacingLeft);
        }


        /// <summary>
        /// return weapon held in facing direction  
        /// </summary>
        /// <returns>body or null if not held</returns>
        public Body GetHeldWeaponBody(bool left)
        {
            return IsHoldingWeapon(left) ? GetHeldBody(left) : null;
        }

        public Body GetHeldBody(bool left)
        {
            return left ? HeldBodyLeft : HeldBodyRight;
        }

        public float GetHeldBodyMass(bool left) //TODO consider skipping this if a control.  
        {
            //TODO at least look up the mass of the gun..  trigger is heavy probably 
            return IsHoldingBody(left) ? GetHeldBody(left).Mass : 0;       //TODO if holding spirit consider whole mass.. as in pulling corpse
        }

        /// <summary>
        /// return weapon held in facing direction  
        /// </summary>
        /// <returns>body or null if not held</returns>
        public Body GetForwardHeldWeaponBody()
        {
            return GetHeldWeaponBody(IsFacingLeft);
        }

        public bool IsHoldingOtherSpiritUnderControl()
        {
            return IsHoldingOtherSpiritUnderControl(true) || IsHoldingOtherSpiritUnderControl(false);
        }

        public bool IsHoldingOtherSpiritUnderControl(bool isLeft)
        {
            return isLeft ? HeldSpiritUnderControlLeft != null : HeldSpiritUnderControlRight != null;
        }

        //TODO  cm should  also matter.. torque,  cm dist to cm.  Get .. Moment.. 
        public float GetMassCarried(bool left)
        {
            Spirit sp = GetHeldSpirit(left);

            if (sp != null)
                return sp.TotalMass;

            Body b = GetHeldBody(left);

            if (b != null)
                return b.Mass;

            return 0;
        }



        /// <summary>
        /// mass of left hand + right hand carried.  does not include joints parts.
        /// </summary>
        /// <returns></returns>
        public float GetMassCarriedTotal()
        {
            return GetMassCarried(true) + GetMassCarried(false);
        }

        #endregion



        /// <summary>
        /// Play current active behavior. First time behavior rule applied.
        /// </summary>
        public bool Play()
        {
            if (_isPaused)
            {
                IsAnimating = true;
                _isPaused = false;
                return true;
            }
            else
            {
                return Play(ActiveBehavior);
            }
        }


        /// <summary>
        /// Play behavior. First time behavior is checked, and loaded to start, followed by the next one with same key mapped to it.
        /// SpiritPlay.Repeat is the mode.
        /// </summary>
        public bool Play(Behavior behavior)
        {
            // if no key assigned to behavior, simply set as active behavior
            if (behavior.GKey == GameKey.None)
            {
                if (Behaviors.Contains(behavior) == true)
                {
                    ActiveBehavior = behavior;
                }
            }
            else    // if have key assigned
            {
                PlayAllBehaviorWithSameKeyAs(behavior);
            }

            _spiritPlay = SpiritPlay.Repeat;
            PlayAll();

            return true;
        }


        /// <summary>
        /// plays all the Behaviors with the same key as the one assigned to input behavior , starting with  the one marked FirstTimeExec
        /// </summary>
        /// <param name="behavior"></param>
        public void PlayAllBehaviorWithSameKeyAs(Behavior behavior)
        {
            // if no behavior found with specific key, return false.
            List<Behavior> bhvs;
            if (!_mapGamekeyToBehaviors.TryGetValue(behavior.GKey, out bhvs) || !bhvs.Any())
                return;

            SetActiveBehaviorToFirstInSequence(behavior, bhvs);

            if (ActiveBehavior == null)
            {
                ActiveBehavior = bhvs[0]; // none marked IsFirstTimeExec, just use the first one.
            }


        }




        /// <summary>
        /// if there are two mapped to the same game key, set active to the one marked FirstTimeExec, and NextBehavior to the other
        /// </summary>
        /// <param name="behavior"></param>
        /// <param name="bhvs"></param>
        private void SetActiveBehaviorToFirstInSequence(Behavior behavior, List<Behavior> bhvs)
        {
            ActiveBehavior = behavior;
            foreach (Behavior b in bhvs)  //TODO .. OPTIMIZATION better sort them on model changed.. make  first FirstTimeExec first in array.  Note only support two behavior sequences. the second being a repeater
            {
                if (b.FirstTimeExec)
                {
                    ActiveBehavior = b;
                }
                else
                {
                    NextBehavior = b;
                }
            }
        }


        /// <summary>
        /// Play behavior by input key. First time behavior is checked.
        /// </summary>
        public bool Play(GameKey gk)
        {
            if (gk == GameKey.None)
                return false;

            // if no behavior found with specific key, return false.
            List<Behavior> bhvs;
            if (!_mapGamekeyToBehaviors.TryGetValue(gk, out bhvs) || !bhvs.Any())
                return false;


            return Play(bhvs[0]);  // will pick the one marked IsFirstTimeExec if available for this key
        }


        /// <summary>
        /// Play auto behavior, without keypress input, interrupting active behavior. 
        /// Usually called from script.  First time behavior is ignored.
        /// Only change and execute new behavior if :
        /// z1. Behavior not already set, or
        /// 2. Behavior set but not playing.
        /// </summary>
        public void PlayAutoBehavior(Behavior behavior)
        {
            Debug.Assert(Behaviors.Contains(behavior));

            if (ActiveBehavior != behavior || !IsAnimating)
            {
                ActiveBehavior = behavior;
                NextBehavior = behavior;   //strange, but it means to repeat the active one..TODO should probably use the Repeat state and not use this then.

                _spiritPlay = SpiritPlay.Repeat;
                IsExecutingFirstCycleAutoBehavior = true;

                PlayAll();
            }
        }




        public bool Play(SpiritPlay spiritPlay)
        {
            if (Behaviors.Count < 1)
                return false;

            _spiritPlay = spiritPlay;

            PlayAll();
            return true;
        }


        public bool Play(string name)
        {
            return Play(Behaviors.FirstOrDefault(x => x.Name == name));
        }

        public bool Play(Behavior behavior, SpiritPlay spiritPlay)
        {
            if (Behaviors.Count < 1 || behavior == null)
                return false;

            _spiritPlay = spiritPlay;
            return PlayBehavior(behavior);
        }


        /// <summary>
        /// Play a transition from Current Pose to target behavior   ( not used.. could be for blending. transitioning between behaviors .. now it just starts at beginning..
        /// </summary>
        /// <param name="toBehavior">Target Behavior</param>
        /// <param name="duration">Duration of Transition Animation</param>
        /// <returns></returns>
        public bool Play(Behavior toBehavior, double duration)
        {
            if (toBehavior.Keyframes.Count < 1)
            {
                return false;
            }

            _behavior2 = toBehavior;
            _duration = duration;

            _keyEndIndex = 0;

            _spiritPlay = SpiritPlay.TransitionToTarget;

            IsAnimating = true;
            return true;
        }


        /// <summary>
        /// Play all the keyframes, from the beginning, 
        /// </summary>
        private void PlayAll()
        {
            _keyStartIndex = 0;
            _keyEndIndex = ActiveBehavior.Keyframes.Count - 1;
            _currentTime = 0;   // start from beginning.. See the PauseOnReleaseKey if not desired

            _isPaused = false;
            IsAnimating = true;

        }


        /// <summary>
        /// Play a sequence of keyframes, interpolating
        /// </summary>
        /// <param name="behavior"></param>
        /// <returns></returns>
        private bool PlayBehavior(Behavior behavior)
        {
            if (Behaviors.Contains(behavior))
            {
                ActiveBehavior = behavior;
                _keyStartIndex = 0;
                _keyEndIndex = ActiveBehavior.Keyframes.Count - 1;

                _isPaused = false;
                IsAnimating = true;

                return true;
            }
            return false;
        }



        /// <summary>
        /// Execute closing animation (revert to the start pose of sequence, usually a stablestance pose) before stop.    This happens un release key unless PauseOnKeyUp is true, needed for a behavior sequence where the second has an unstable start pose ( such as Walk Left)  .. So we go to the begging of the the sequence.. Current Time = 0 , clean ann other state for that sequence ( NextBehavior)
        /// </summary>
        public void EndAtStartPose()
        {
            // search first time behavior of current behavior
            GameKey gk = ActiveBehavior.GKey;

            foreach (Behavior b in MapGamekeyToBehaviors[gk])
            {
                if (b.GKey == gk && b.FirstTimeExec)
                {
                    ActiveBehavior = b;
                    NextBehavior = null;//end of  init Pose will match with next pose in sequence.    
                    CurrentTime = 0;   //this is for the type of walks that have initial cycles then the continous cycle.. see yndrd.. 
                                       //the Init Left walk and the Left Walk are mapped to the same key.   This is only used in Biped.. Should probably have been in the plugin.. 
                                       //this Spirit may be too complicated for a general purpose thing.   I think no other of the 20 plugins use this NextBehavior stuff.
                                       // however , and init or other sequence of behaviors might be a way to oranise stuff.. as in power stroke, return stroke, 
                                       // or open box , move to place thing inside , close box.. but thats high level stuff..
                                       //  its very different the init step to take when the speed is zero, than to maintain a step when accel is low and vel is > a minimum.
                                       //To avoid and unstable state , the init step starts with legs apart and is safe to stop at the zero pos.. not true with the walk sequence
                                       //the general idea is the spirit is a State Machine.  but certain meta states like WalkingLeft are animating though a set of states ( keyframes)
                                       //so when a cycle begins from a zero speed , it needs a different sequence to get started.   then that sequence blends to the Walk.
                                       //The init step might be slower , but its generally only the first few keyframes are different, the step out , then the rest matches with the walk.
                                       //another way to approach woul dbe like the symbicon project.. states machines with much fewer states..  power stroke, or swing step, return stroke. etc.
                                       //  but two legs side ways is much less symmetrical than 3d side view..  our front and back leg do not do the same thing at all.
                                       // out approach was justified as a "recorded " walk that worked.   no mocab , just using  the tool..   TODO break this out to explanation..

                    return;
                }
            }
        }


        public void Pause()
        {
            IsAnimating = false;
            _isPaused = true;
        }


        public void Stop()
        {
            IsAnimating = false;
            IsExecutingFirstCycleAutoBehavior = false;
            _isPaused = false;
            CurrentTime = 0;
        }




        /// <summary>
        /// Stop and the first time 0 of the Behavior with the same command key as the current one, marked FirstTimeExec.
        /// </summary>
        public void StopAtInitPoseInSequence()
        {
            Stop();
            EndAtStartPose();
        }



        public void ResetPosture()
        {
            if (Joints.Count > 0)
            {
                foreach (PoweredJoint joint in Joints)
                {
                    joint.TargetAngle = 0;
                }
            }
        }

        public void AddJoint(PoweredJoint joint)
        {
            if (Joints.Contains(joint) == false)
            {
                Joints.Add(joint);
            }

            int index = Joints.IndexOf(joint);
            foreach (Behavior behavior in Behaviors)
            {
                foreach (Keyframe keyframe in behavior.Keyframes)
                {
                    if (keyframe.Angles.Count < Joints.Count)
                        keyframe.Angles.Insert(index, joint.TargetAngle);
                }
            }
        }



        /// <summary>
        /// Update Spirit along with its internal sensor
        /// </summary>
        /// <param name="dt">dt  virtual elapsed time slice</param>
        public void Update(double dt)
        {
            try
            {
                if (!IsDead)   //TODO test.. is this why we can move arms after dead, not updating anything.. i think.. or during KO..  check..
                {
                    UpdateEnergyLevel(dt);
                    UpdateSpiritAbilities();
                }
                Update(dt, false);//below since LFE might affect joints affected by above

#if !PRODUCTION
                foreach (Body b in _bodies)
                {
                    foreach (AttachPoint attachpoint in b.AttachPoints)
                    {
                        attachpoint.Update();  //this is just to notify current force on joint.
                    }
                }
#endif
                if (WasSpawned)
                {
                    UpdateSpiritParticleLife(dt);
                }
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Spirit Update: Exc:" + ex.Message);
                System.Diagnostics.Debug.WriteLine("Spirit Update: Stack" + ex.StackTrace);
            }
        }



        /// <summary>
        /// Update Spirit in series with all the other spirts, entity update is in here since it spawns and uses globals, also attach points since they can delete joints
        /// </summary>
        /// <param name="dt">dt  virtual elapsed time slice in sec</param>
        /// <summary>
        public void Update(double dt, bool setPose)
        {
            if (_needUpdateCurrentTime)     // might need lock/semaphore later
            {
                _needUpdateCurrentTime = false;
                dt = _currentTime - _lastTime;
                setPose = true;
            }

            //Body update wont get called on Parts broken out of system, so wont source blood particles or have active emitters.
            //calling owned body update from spirit update only updates bodes that remain part of system.    
            //on most emitter or control , energy source comes from the main central system so this is appropriate.
            UpdateBodies(dt);


            // always update spirit aabb, important for selection rectangle (view)
            UpdateAABB();
            // current script requires center mass always updated
            UpdateTotalAndCenterMass();


            try
            {
                if (IsDeadAndCold())//seizure still going to stiffen joints after time
                {
                    if (Plugin != null && _isCallingPlugin == true)
                    {
                        Plugin.UpdatePhysics(dt, null);  //so bee can shut its noise off..  plugins could do special after death stuff like decompose..or do this at spirit level
                    }
                    return;
                }

                UpdateAverageMainBodySpeedAndPower(dt);

                if (IsMinded)
                {
                    UpdateMindedSpirit();
                }




                UpdatePhysics(dt);
            }

            catch (Exception exc)
            {
                Debug.WriteLine(exc.Message);
            }

            if (_pwdjoints == null)
                return;

            UpdateAnimation(dt, setPose);
        }






        /// <summary>
        /// Update our particle life based on level AABB or lifespan
        /// </summary>
        private void UpdateSpiritParticleLife(double dt)
        {
            if (!IsExpired)
            {
                _age += dt * 1000;
            }
        }


        public void UpdateEnergyLevel(double dt)
        {
            float totalImpulse = 0;

            //TODO Code Review.. consider Rename to PoweredJoints
            foreach (PoweredJoint pj in _pwdjoints)
            {
                if (pj.IsNumb)
                    continue;

                totalImpulse += pj.GetJointImpulse();

                //TODO in future consider  to removing angluar joint from powered joint, use Motor to match angle..  find the power in Watts * dt
                //pj.GetReactionTorque
                //  System.Diagnostics.Trace.TraceInformation("Joint Impulse {0}", pj.GetJointImpulse());
            }

            //  3000 calories = 12 552 joules.  Approx Energy expended by active  human in a day..
            //   1 calorie = 4.18400 joules
            //Banana = 100Cal or 400 Joules
            EnergyLevel -= totalImpulse * (float)dt;  //use   / dt;?  need Joules... forget units for now..who cares
        }

        private bool _isDead = false;



        public void DieRandomly(float maxSeizure)
        {
            if (OnKilled != null)
            {
                OnKilled(this, maxSeizure);
            }
            else
            {
                Die();
            }
        }

        public void Die()
        {
            if (_isDead)
                return;

            EnergyLevel = 0;
            SetDeadSpiritBodyStates();

            if (Died != null)
            {
                Died(this);
            }

            _isDead = true;
        }



        public void ApplyDragCoefficient(float value)
        {
            Bodies.ForEach(x => x.DragCoefficient = value);
        }

        public void ApplyJointMotorDamping(float maxTorque)
        {
            Joints.ForEach(x => x.SetMotorDamping(maxTorque));
        }

        private void SetDeadSpiritBodyStates()
        {
            JointSoftness = 1;

            IsSelfCollide = true; //collapsing figure will hit himself
            _isWeak = true;

            SetEyeLimits();  // eyes wont roll.
            SetIsBullet(false);   //when fall in heap, bullets are slow to pile, unbullet our connected pieces.
        }


        //this might be done in Plugin.. or ovveride in there somehow..

        public Action OnUpdateSpiritEnergy;


        public void UpdateSpiritAbilities()
        {
            if (OnUpdateSpiritEnergy != null)
            {
                OnUpdateSpiritEnergy();
            }

        }


        public void SetToTiredState()
        {
            _isTired = true;
        }



        //this is for spirits tuned in tool using prop sheets..  then at runtime tweaked with plugin
        //ACHTUNG! CAREFUL..  dangerous to do this.. saving the character after plugin messes with the model.  there is no "View model"..
        private float[] _jointSoftnessOrigValues;
        private float[] _jointBiasOrigValues;
        private float[] _jointDampingOrigValues;


        public void CacheJointProperties()
        {
            _jointSoftnessOrigValues = new float[Joints.Count];
            _jointDampingOrigValues = new float[Joints.Count];
            _jointBiasOrigValues = new float[Joints.Count];

            for (int i = 0; i < Joints.Count; i++)
            {
                _jointSoftnessOrigValues[i] = Joints[i].Softness;
                _jointDampingOrigValues[i] = Joints[i].DampingFactor;
                _jointBiasOrigValues[i] = Joints[i].BiasFactor;
            }
        }


        public void RestoreJointProperties()
        {
            for (int i = 0; i < Joints.Count; i++)
            {
                Joints[i].Softness = _jointSoftnessOrigValues[i];  //TODO should probably set this in plugin loaded.    saving level with weak creature causes problems
                Joints[i].DampingFactor = _jointDampingOrigValues[i];
                Joints[i].BiasFactor = _jointBiasOrigValues[i];
            }
        }


        /// <summary>
        /// for performance, bullets don't pile well. Also only one item in pair of colliding ineeds to be a bullet for CCD check.
        /// </summary>
        public void SetIsBullet(bool value)
        {
            Bodies.ForEach(x => x.IsBullet = value);
        }


        //TODO consider eliminate, not used..  might be used in impaired self collide...
        public void SetBulletForHandsBodyAndFeet()
        {
            foreach (Body b in Bodies)
            {
                b.IsBullet = IsExtremityBody(b);
            }
        }

        /// <summary>
        /// is true after last seizure ends , include rigormotis delay
        /// </summary>
        /// <returns></returns>
        public bool IsDeadAndCold()
        {
            return (IsDead && !IsHavingSeizure);//seizure is still going to stiffen joints after time  about 40 sec  
        }

    
        private void UpdateBodies(double dt)
        {
            try
            {
                foreach (Body b in Bodies)  //will give exception only if emitter cuts own body in system.  with complex laser weapon 
                {
                    b.Update(dt);
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine("exception updating bodies of spirit" + Name + exc);
            }
        }

        private void UpdateAnimation(double dt, bool setPose)
        {
            if (IsAnimating)
            {
                UpdateAnimation(dt);
            }

            List<Effect> copyLFE = new List<Effect>(Effects);  //some LEF will remove them self from list on Update if expired, so this needs to be a copy to interate
            foreach (Effect effect in copyLFE)
            {
                effect.Update(dt);
            }

            // This only happens when animation is playing
            PreupdateAnimation(dt);  // dh i moved this here from above.. its gives a chance to override effects.

            //should be able to uncheck isCallingPlugin and change pose.. 
            if (IsAnimating || setPose)
            {
                SetTargetAnglesFromKeyFramesAndFilters(ActiveBehavior.TimeDilateFactor * TimeDilateAdjustFactor);
            }

            PostUpdateAnimation(dt);
        }


        private void UpdateMindedSpirit()
        {
            RefreshSensedObjectLists();

            if (Effects.Contains(Spirit.RegrowKey))
            {
              //  SetLimbPartCache(true);
            }

            // update mind here, after sensor pos update. should not depend on other update.        
            //  TODO this does not allow plugin  to customize SensedObjectLists   .. consider moving this to after update physics.... not sure
            Mind.Update();
        }


        private void UpdatePhysics(double dt)
        {

            if (Plugin == null)
                return;

            // always called every update.
            if (_isCallingPlugin)
            {
                try
                {
                    Plugin.UpdatePhysics(dt, null);
                }
                catch (Exception ex)
                {
                    if (Spirit.OnSpiritException != null)
                    {
                        Spirit.OnSpiritException(this, ex, "Spirit.Plugin.UpdatePhysics()");
                    }
                    else
                    {
                        Debug.WriteLine("Exception  in UpdatePhysics" + ex.Message + ex.StackTrace);
                    }
                }

                UpdatePluginAIOnMindedSpirit(dt);
            }


        }


        private void UpdatePluginAIOnMindedSpirit(double dt)
        {
            if (Mind != null)
            {
                try
                {
                    if (!CannotDoAnything())//seizure still going to stiffen joints after time
                    {
                        _mindUpdate++;
                        if (_mindUpdate % Mind.DullNess == 0)
                        {
                            Plugin.UpdateAI(dt, Mind);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Spirit.OnSpiritException != null)
                    {
                        Spirit.OnSpiritException(this, ex, "Spirit.Plugin.UpdateAI()");
                    }
                    else
                    {
                        Debug.WriteLine("Exception  in Update AI" + ex.Message + ex.StackTrace);
                    }
                }
            }
        }


        private void PostUpdateAnimation(double dt)
        {
            if (Plugin != null && _isCallingPlugin == true)
            {
                try
                {
                    Plugin.PostUpdateAnimation(dt, null);
                }
                catch (Exception ex)
                {
                    if (Spirit.OnSpiritException != null)
                    {
                        Spirit.OnSpiritException(this, ex, "Spirit..Plugin.PostUpdateAnimation()");
                    }
                    else
                    {
                        Debug.WriteLine("Exception  in PostUpdateAnimation" + ex.Message + ex.StackTrace);
                    }
                }
            }
        }


        private void PreupdateAnimation(double dt)
        {
            if (Plugin != null && _isCallingPlugin == true && IsAnimating)
            {
                try
                {
                    Plugin.PreUpdateAnimation(dt, null);
                }
                catch (Exception ex)
                {
                    if (Spirit.OnSpiritException != null)
                    {
                        Spirit.OnSpiritException(this, ex, "Spirit.Plugin.PreUpdateAnimation()");  //TODO review this.. do these expcept go to the main window handler? is this left over from old scripts processor
                    }
                    else
                    {
                        Debug.WriteLine("Exception  in PreUpdateAnimation" + ex.Message + ex.StackTrace);
                    }

                }
            }
        }

        public bool IsHavingSeizure
        {
            get
            {
                return Effects.OfType<Seizure>().Count() > 0;
            }
        }



        /// <summary>
        /// Find all body and spirit in this AABB.  bigger AABB is expensive.
        /// 
        /// Called by Shadowplay traveler level switch. 
        /// Similar to Shadowtool GenericSelectToolBase.FindSelectionBodies(), will include spirit as whole, not only limbs.
        /// </summary>
        public FarseerPhysics.Common.HashSet<Body> GetOtherBodiesAndSpiritsInAABB(AABB aabb, out IEnumerable<Spirit> spirits)
        {


            FarseerPhysics.Common.HashSet<Body> bodiesInAABB = DetectBodiesInAABB(aabb, false, CollisionGroupId);
            FarseerPhysics.Common.HashSet<Body> bodiesToBeRemovedFromAABB = new FarseerPhysics.Common.HashSet<Body>();
            FarseerPhysics.Common.HashSet<Spirit> spiritToAdd = new FarseerPhysics.Common.HashSet<Spirit>();

            foreach (Body otherBody in bodiesInAABB)
            {
                foreach (Spirit spirit in SpiritsInSensor)
                {
                    if (spirit.BodySet.Contains(otherBody))
                    {
                        bodiesToBeRemovedFromAABB.Add(otherBody);
                        spiritToAdd.CheckAdd(spirit);
                        break;  // found spirit that own body
                    }
                }
            }

            foreach (Body toRemove in bodiesToBeRemovedFromAABB)
            {
                bodiesInAABB.Remove(toRemove);
            }

            spirits = spiritToAdd;
            return bodiesInAABB;
        }


        /// <summary>
        /// Find all body and spirit in this AABB.  bigger AABB is expensive . 
        /// 
        /// Note: For other spirit, only checks inclusion of spirit's MainBody. 
        /// If limbs are inside AABB but MainBody is outside, then only limbs Bodies collected, not its parent Spirit.
        /// 
        /// Note: SpiritsInSensor is updated using this method.
        /// </summary>
        private FarseerPhysics.Common.HashSet<Body> GetOtherBodiesAndSpiritsInAABB(AABB aabb, out FarseerPhysics.Common.HashSet<Spirit> spirits)
        {
            FarseerPhysics.Common.HashSet<Body> bodySet = DetectBodiesInAABB(aabb, false, CollisionGroupId);
            FarseerPhysics.Common.HashSet<Spirit> spiritList = new FarseerPhysics.Common.HashSet<Spirit>();
            foreach (Body otherBody in bodySet)
            {
                // if body is MainBody of other spirit, add that spirit, collect unless its us 
                Spirit othersp;
                if (otherBody.PartType == PartType.MainBody
                    && Level.MapBodyToSpirits.TryGetValue(otherBody, out othersp) && othersp != this)
                {
                    spiritList.CheckAdd(othersp);
                }
            }
            spirits = spiritList;
            return bodySet;
        }


        /// <summary>
        /// Call this to update spirit input state.       EVERY SPIRIT HAS A KEYSTATE..     left and right.. using weapons that have triggers, etc.   Issue could be, fire one machine gun, switch to other side, then first gun keep firing,
        /// ? TODO TEST this.. only if both are held at once can that be allowed.. so see no need to copy key state for every hand.   Just one keystate per input device. thats it.   for multilayer , a device per spirit TODO NEED TO COPY KEYSTATE OVER TO THE HELD ITEMS, NO , JUST USE THE PARENT KEYSTATE
        /// MUST KEEP IN MIND CONTROL IS PASSED TO HELD OBJECTS.. wold be need for multiplayer, 2 joysicks or 2 keyboards on one machine, kontrol is perfect for that
        //for future multilayer  dual controller or one keyboard..  not refer to parent spirit.. unless maybe on spirit is assigned to one input device...
        /// </summary>    
        public void UpdateInput(GameKey newGameKeyState)
        {
            if (BlockMappedCommand)  //todo    CLEANout,  EASY  is THIS IS NOT USED??
                return;

            // get changed key state, Exclusive OR 
            GameKey changedState = newGameKeyState ^ GameKeyState;

            // 0 means nothing changed in input
            if (changedState == 0)
            {
                return;
            }

            // update cache of key state
            _gameKeyState = newGameKeyState;// dh i moved this up in case plugin needs to query key combinations.   this is for local multi-player, why there are two key states.

            // some held spirit plugin code might also check for its gamekeystate on input,
            // so we update them too here.
            // NOTE: update both held spirit KeyState, but only call plugin on the one facing, far below.

            // UpdateHeldSpiritKeyState(true, newGameKeyState);  //KEYSTATEPERSPIRIT  TODO CLEAN OUT
            // UpdateHeldSpiritKeyState(false, newGameKeyState);

            // NOTE: this one direction call cause issue with mouse click to change facing and gun fire.  //TODO FIX
            // when hold gun right and change facing using click from right to left, gun event on right is not cancelled.
            // in the end spirit change facing to left but still firing gun on right.
            //UpdateHeldSpiritKeyState(IsFacingLeft, newGameKeyState);

            UpdateInputOnChangedGameKeyEvent(newGameKeyState, changedState);

            // end of changed game key iteration
        }


        private void UpdateInputOnChangedGameKeyEvent(GameKey newGameKeyState, GameKey changedState)
        {


            //TODO FUTURE LAZY.,. BITS ARE EASIER AND SIMPLER THAN PARSNIG
            // held spirit is proceeded further down below on enum loop.

            //TODO CODE REVIEW //why using string and parse?  ??
            //better to use Shift Right and bitwise.

            //comment .. need to find out which command bits changed..

            //Bitwise way is best ...

            // http://www.dotnetperls.com/bitcount

            // pass in changeState, the XOR

            /*    static int IteratedBitcount(int n)
             {
                 int keybits = n;
                 int count = 0;

                 while (keybits != 0)
                 {
                     if ((keybits & 1) == 1)
                     {
                       //  count++;
    
              Plugin.OnUserInput(this, keybits, turnOn);  //turnON can figure out from a newGameKeyStatebits & keybits
                     }
                     keybits >>= 1;  //shift bit right..
                 }
                 return count;
             }*/



            // enum loop, iterate each changed game key.  
            //TODO UWP PORT should really replace this parse..see MONOGAMES STUFF HERE. AND IF THE KEYS ARE TIED TO THE VSNYNC, 
            //WE CAN BE MORE RESPONSIVE MAYBE

            int max = GameKeyUtils.GameKeyNames.Count;
            for (int i = 0; i < max; i++)
            {
                //CODO REVIEW FUTURE avoid this Parse and use of names.    need to use bitwise operation
                GameKey key = (GameKey)Enum.Parse(typeof(GameKey), GameKeyUtils.GameKeyNames[i], false);

                // skip unchanged key
                if ((changedState & key) == 0)
                    continue;

                // get pressed state
                bool turnOn;

                if ((newGameKeyState & key) == key)
                {
                    turnOn = true;
                }
                else
                {
                    turnOn = false;
                }


                GameKeyEventArgs inputArgs = new GameKeyEventArgs(key, turnOn);

                // NOTE: update input on held spirit only on facing side, unless want to fire gun on both direction later.
                // in that case will need to be handled by yndrd plugin.
                if (UpdateInputOnHeldSpirit(inputArgs, IsFacingLeft)
                    || IsControllngOneSpirit() && UpdateInputOnHeldSpirit(inputArgs, !IsFacingLeft)
                    )
                {
                    // if handled by held spirit, skip updating our behavior and plugin.
                    continue;
                }

                // update our own behavior and plugin input state here
                UpdateBehaviorAndPluginFromInput(inputArgs);

            }
        }



        private bool IsControllngOneSpirit()
        {
            return GetHeldSpiritUnderControl(true) != null ^ GetHeldSpiritUnderControl(false) != null;
        }


        //   private void UpdateHeldSpiritKeyState(bool isLeft, GameKey newGameKeyState)
        //  {
        //     Spirit heldSpirit = GetHeldSpiritUnderControl(isLeft);
        //     if (heldSpirit != null)
        //     {
        //         heldSpirit.GameKeyState = newGameKeyState;
        //     }
        //  }


        public Spirit GetHeldSpiritUnderControl(bool isLeft)
        {
            return isLeft ? HeldSpiritUnderControlLeft : HeldSpiritUnderControlRight;
        }


        public Spirit GetHeldSpirit(bool isLeft)
        {
            return isLeft ? HeldSpiritLeft : HeldSpiritRight;
        }


        public AttachPoint GetHeldGrip(bool left)
        {
            return left ? HeldGripLeft : HeldGripRight;
        }


        /// <summary>
        ///The angle to make it point straigh , at in gun.. positive counter clockwise, for left aiming weapon like gun, handle turned down by Angle
        /// </summary>
        /// <param name="isLeft"></param>
        /// <returns></returns>
        public float GetHeldWeaponGripAngle(bool left)
        {
            if (!IsHoldingWeapon(left))
                return 0.0f;

            return GetHeldGrip(left).HandleAngle;
        }


        /// <summary>
        /// Update user input on held spirit, also trigger behavior on held spirit that might respond to input.
        /// </summary>
        /// <param name="inputArgs"></param>
        /// <param name="isLeft"></param>
        /// <returns>TRUE if input is handled.  FALSE otherwise. </returns>
        public bool UpdateInputOnHeldSpirit(GameKeyEventArgs inputArgs, bool isLeft)
        {
            Spirit heldSpirit = GetHeldSpiritUnderControl(isLeft);

            if (heldSpirit == null)
                return false;

            AttachPoint heldGripOnControlledSpirit = GetHeldGrip(isLeft);

            // if we currently held another spirit (and controlling it), should pass 
            // key input to it first. if not handled, then we process it.
            inputArgs.ControlPoint = heldGripOnControlledSpirit;

            inputArgs.Sender = this;

            // because we didn't override all key, but only partial override, ?? HACK alert?
            // we need to check each changed key result back from plugin. 
            // so we call other spirit UpdatePluginInput(), not UpdateInput().

            heldSpirit.UpdateBehaviorAndPluginFromInput(inputArgs);

            if (inputArgs.Handled && (inputArgs.ChangedKey == GameKey.Left || inputArgs.ChangedKey == GameKey.Right))
            {
                HoldingLeftRightInputDevice = true; //TODO  should set this before doing anything..i guess it will just eat one frame, left and right, no big deal..
            }

            return inputArgs.Handled;

        }


        /// <summary>
        /// Update behavior and call plugin OnUserInput. This might also get called from other spirit.
        /// </summary>
        private void UpdateBehaviorAndPluginFromInput(GameKeyEventArgs e)
        {
            // update our behavior. change can be activating or deactivating behavior.
            // might need support for multiple active behavior later.
            UpdateBehaviorOnInput(GameKeyState, e.ChangedKey, e.IsPressed);

            // update plugin input with gamekey event
            try
            {
                if (Plugin != null && _isCallingPlugin)
                {
                    Plugin.OnUserInput(e);
                }
            }
            catch (Exception ex)  //error in plugin
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }


        /// <summary>
        /// Helper to reset spirit input state. Call this when deselect spirit, to
        /// clear all input applied to spirit.
        /// </summary>
        public void ResetInput()
        {
            //UpdateInput((int)GameKey.None);
            _gameKeyState = 0;
        }

        /// <summary>
        /// Check if spirit is currently have any input key pressed.
        /// </summary>
        /// <returns>TRUE if any key pressed.</returns>
        public bool IsOnInput()
        {
            return (GameKeyState != 0);
        }


        public void Clear()
        {
            Joints.Clear();
            foreach (Behavior b in Behaviors)
            {
                b.Keyframes.Clear();
            }
        }

        /// <summary>
        /// Update spirit aabb. This requires correct Bodies.
        /// </summary>
        public void UpdateAABB()
        {
            if (Bodies == null || Bodies.Count == 0)
                return;

            AABB aabb;
            bool init = false;

            AABB.Reset();

            foreach (Body b in Bodies)
            {

                if (b.GeneralVertices == null)
                {  
                    Debug.WriteLine("unexpected general verts is null on b," + b.PartType.ToString());

                    continue;
                }
                
                b.UpdateAABBForPolygon();

                // zero vector usually mark invalid aabb
                if (b.AABB.LowerBound == Vector2.Zero && b.AABB.UpperBound == Vector2.Zero)
                    continue;

                if (init == false)
                {
                    AABB.Copy(b.AABB);
                    init = true;
                }
                else
                {
                    aabb = b.AABB;
                    AABB.Combine(ref aabb);
                }
            }
        }



        /// <summary>
        /// Update spirit CM. Requires correct Bodies.
        /// </summary>
        public void UpdateTotalAndCenterMass()
        {
            if (Bodies == null || Bodies.Count == 0)
            {
                _totalMass = 0;
                return;
            }


            _prevLinearVelocity = LinearVelocity;

            Vector2 prevWorldCenter = _worldCenter;


            List<Body> bodies = new List<Body>(Bodies);

            if (AddHeldItemsToCM && HeldBodies.Count > 0)
            {
                bodies.AddRange(HeldBodies.Where(x => x.IsStatic == false
                    && x.PartType != PartType.Handhold && x.PartType != PartType.Control
                    && x.Mass < TotalMass / 2f // don't bother can't carry something half our weight.. also causes issues moving heavy doors and stuff
                                               //   && x.Mass < 40  // in case pushing sled or slippery ( TODO  // need to know if pushing something long and sliding..
                                               //otherwise it will detect falling.  fix is now in detect falling, its pushes better with the cm adjust , seems.
                    )
                    //     && x.JointList.Next != null && x.JointList.Next.Next == null) //make sure it is not a rope of connect to a static?
                    );  //TODO 
            }

            EntityHelper.CalcCM(bodies, out _worldCenter, out _totalMass);

            if (prevWorldCenter != _worldCenter)  //if it changed.. we in case UpdateTotalAndCenterMass got called twice this frame ;  //TODO remove this and separate UpdateTotalAndCenterMass from the method that gets called only once to calc WorldCMDisplacementPerFrame
            {
                _worldCenterPrev = prevWorldCenter;
            }






        }


        protected Vector2 _prevLinearVelocity;

        /// <summary>
        /// Rebuild cache for game key input.
        /// </summary>
        public void RebuildKeyMapCache()
        {
            _mapGamekeyToBehaviors.Clear();

            foreach (Behavior b in _behaviors)
            {
                // multiple behaviors (first time / normal) can use the same key
                List<Behavior> bs;
                if (_mapGamekeyToBehaviors.TryGetValue(b.GKey, out bs))
                {
                    bs.Add(b);
                }
                else
                {
                    bs = new List<Behavior>();
                    bs.Add(b);
                    _mapGamekeyToBehaviors.Add(b.GKey, bs);
                }
            }
        }



        /// <summary>
        /// Get Body, called from within Script, with debugging feature
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>body</returns>
        public Body GetBodyDebug(int index)
        {
            if (index > Bodies.Count - 1 || index < 0 || Bodies.Count == 0)
                throw new NullReferenceException("The Body Index is invalid");

            return Bodies[index];
        }

        /// <summary>
        /// Get Body, called from within script, it will return null, if the body is no longer exist.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>null if not found</returns>
        public Body GetBody(int index)
        {
            if (index > Bodies.Count - 1 || index < 0 || Bodies.Count == 0)
                return null;

            return Bodies[index];
        }

        //wont throw exception if missing key
        public Behavior GetBehavior(string behaviorName)
        {
            if (Behaviors.Contains(behaviorName))
            {
                return Behaviors[behaviorName];
            }

            return null;
        }

        public void RebuildSpiritInternalGraph(bool designTime)
        {
            IEnumerable<Body> removedBodies;
            RebuildSpiritInternalGraph(designTime, null, out removedBodies);
        }

        public void RebuildSpiritInternalGraph(bool designTime, List<PoweredJoint> newJoints)
        {
            IEnumerable<Body> removedBodies;
            RebuildSpiritInternalGraph(designTime, newJoints, out removedBodies);
        }

        /// <summary>
        ///Rebuild Spirit Internal Graph. by walking graph away from main body.
        /// </summary>
        /// <param name="designTime">happend in tool, with rebuild joint list</param>
        /// <param name="newJoints">add new joints to model , for design / animation tool</param>
        /// <param name="removedBodies">bodies that were removed, ie if shoulder breaks, will contain arm parts</param>
        public void RebuildSpiritInternalGraph(bool designTime, List<PoweredJoint> newJoints, out IEnumerable<Body> removedBodies)
        {
            List<Body> removedBodiesList = new List<Body>();
            Debug.Assert(MainBody != null);    //all spirit must have a main body

            List<Body> originalBodiesSet = new List<Body>(Bodies);   //copy  the existing body set.

            // Iterate the graph collect all joinded bodies and joints
            List<Body> currentlyConnectedBodies;
            PoweredJointCollection currentGraphJoints;
            JointCollection currentFixedJoints;
            List<Joint> auxJoints;
            //GraphWalker.WalkGraph(this.MainBody, out bodies, out currentGraphJoints);
            GraphWalker.WalkGraphCollectingJoints(MainBody, out currentlyConnectedBodies, out currentGraphJoints, out currentFixedJoints, out auxJoints);

            //The commented code below is for testing the validity of walked bodies and joints, put it here for later use
            //SysLog.Instance.Print(string.Format("graph pjoints: {0}, fjoints: {1}, bodies: {2}", currentGraphJoints.Count, currentFixedJoints.Count, bodies.Count));

            // Update our bodies list with connected bodies only
            Bodies.Clear();
            Bodies.AddRange(currentlyConnectedBodies);

            //dont allow severed limbs to twitch , set the joint power to 0
            foreach (PoweredJoint pj in Joints)
            {
                if (!currentGraphJoints.Contains(pj))
                {
                    pj.HasPower = false;  //no power but still has stiffness.
                }
            }

            if (designTime)  //recollect Joints
            {
                RecollectJoints(newJoints, currentGraphJoints, currentFixedJoints);
            }

            CacheBodySet();//just use a HashSet for fast lookups by contains, etc.  //TODO consider removing the Bodies  list , just use set

            // bodies outside main bodies should reset its collision group, because
            // it will be removed from spirit.
            foreach (Body b in originalBodiesSet)
            {
                if (!BodySet.Contains(b))
                {
                    b.CollisionGroup = 0;
                    removedBodiesList.Add(b);
                }
            }

            removedBodies = removedBodiesList;
            IsSelfCollide = IsSelfCollide;  //reset it to be sure, there was intermittent issue,  rest of arm wont collide with other sprit after breaking..              

        }

        private void RecollectJoints(List<PoweredJoint> extraJoints, PoweredJointCollection currentGraphJoints, JointCollection currentFixedJoints)
        {
            // the new joints from walker will have different index order. a new 
            // mapping is required between animation keyframe and joint index.

            // we clone the joints from walker, but using order from Joints.
            PoweredJointCollection newJoints = new PoweredJointCollection();


            foreach (PoweredJoint pj in Joints)//this will preserve joint order, so that existing animations will work, if we are just adding joints , then will go to the end..
            {
                if (currentGraphJoints.Contains(pj) == true)
                    newJoints.Add(pj);
            }

            FixedJoints.Clear();
            FixedJoints.AddRange(currentFixedJoints);

            // fix animation keyframe when joint is removed. start from higher 
            // index number, to avoid index error when deleting keyframe angles.
            PoweredJoint pwj;
            for (int i = Joints.Count - 1; i >= 0; i--)
            {
                pwj = Joints[i];
                if (currentGraphJoints.Contains(pwj) == false)
                {
                    foreach (Behavior b in Behaviors)
                        b.DeleteJointIndex(i);
                }
            }

            this.Joints.Clear();
            // AddRange somehow raise exception
            foreach (PoweredJoint pj in newJoints)
            {
                this.Joints.Add(pj);
            }

            //validate animation keyframe list when joints are removed or added
            //remove excess 

            foreach (Behavior b in Behaviors)
            {
                b.ValidateJointCount(Joints.Count);
            }

            if (extraJoints != null)
            {
                foreach (PoweredJoint pj in extraJoints)
                {
                    Joints.Add(pj);
                }
            }

            // Reset filters joints numbering
            if (_pwdjoints != null && _filters != null)
            {
                foreach (IKeyframeFilter filter in Filters)
                {
                    Debug.WriteLine("TODO reset filters should we clear target ones?");
                    filter.Reset(_pwdjoints.Count);
                }
            }
        }


        /// <summary>
        /// Starting from selected Body, this method will walk all connected graph, ignoring Joint.SkipTraversal value.
        /// Then based on SkipTraversal value, it will separate graph to one main spirit and some auxiliary spirit.
        /// </summary>
        public void CollectAuxiliarySpirits()
        {
            Debug.Assert(Level != null);

            // walk entire connected graph, start from this spirit main body, ignore SkipTraversal
            List<Body> bodies;
            List<Joint> joints;
            List<Joint> auxJoints;
            GraphWalker.WalkGraph(MainBody, out bodies, out joints,  out auxJoints);

            UpdateAuxiliarySpiritJoints(auxJoints);
            FarseerPhysics.Common.HashSet<Spirit> connectedSpirits = CollectAllConnectedSpirits(Level);
            UpdateAuxiliarySpirits(connectedSpirits);
        }


        /// <summary>
        /// Replace previous AuxiliarySpiritJoints with new joints.
        /// </summary>
        /// <param name="joints"></param>
        private void UpdateAuxiliarySpiritJoints(List<Joint> joints)
        {
            AuxiliarySpiritJoints.Clear();
            AuxiliarySpiritJoints.AddRange(joints.Where(x => x.SkipTraversal));
        }


        /// <summary>
        /// Replace previous AuxiliarySpirits with new connected spirits.
        /// </summary>
        /// <param name="connectedSpirits"></param>
        private void UpdateAuxiliarySpirits(FarseerPhysics.Common.HashSet<Spirit> connectedSpirits)
        {
            AuxiliarySpirits.Clear();

            foreach (Spirit sp in connectedSpirits)
            {
                if (sp != this)
                {
                    AuxiliarySpirits.Add(sp);

                    if (sp.AuxiliarySpirits.Contains(this))
                        sp.AuxiliarySpirits.Remove(this);

                    // for other spirit, clear aux spirit contents, so it wont circular references, circular nesting
                    //TODO this  makes sense to do .. but now seems balloon connected to 
                    //before if i selected the Mainbody ( lower hull ) or ship , copy paste worked, now its broken..
               //     sp.AuxiliarySpirits.Clear();



                    // NOTE: only clear AuxiliarySpirits, but DO NOT clear AuxiliarySpiritJoints as balloon check if its connected using this... TODO.. clean this..could be a problem..
                    // tested in shadowtool, copy paste ship from level 4 to 5, if this code enabled, only ship copied without balloon.
                    // without this code, ship+balloon will be copied proper.
                    //sp.AuxiliarySpiritJoints.Clear();  // <-- CLEAR THIS


                    //TODO CLEAN UP AUX SPIRITS THING..  USING THE TANK WITH DUAL RECOIL THAT IS MESSED UP   START WITH A FLAT MODEL
                    //MAKE SURE LEVEL SPIRITS CAN BE REWALKED, SPIRITS REMOVED.
                    // CLICK ON GUN SHOUN SHOW GUN PLUGIN.. ALL JOINTS NOT TRAVERSAL.. FLAT.. JOINTS BELONG TO LEVEL OR THE GUN THING
                    //


                }
            }
        }


        private FarseerPhysics.Common.HashSet<Spirit> CollectAllConnectedSpirits(Level level)
        {
            IEnumerable<Spirit> levelSpirits = level.GetSpiritEntities();


            FarseerPhysics.Common.HashSet<Spirit> levelSpiritSet = new FarseerPhysics.Common.HashSet<Spirit>();


            foreach (var sp in levelSpirits)
            {

                if (!levelSpiritSet.Contains(sp))
                {
                    levelSpiritSet.Add(sp);
                }
                else
                {

#if DEBUG
                    level.Entities.Remove( sp);
                    throw new Exception("dublicate spirit");
#else

                    level.Entities.Remove( sp);
#endif

                }
            }

            

            FarseerPhysics.Common.HashSet<Spirit> connectedSpirits = new FarseerPhysics.Common.HashSet<Spirit>();

            // if there's skip traversal joints, collect all connected spirits
            foreach (Joint j in AuxiliarySpiritJoints)
            {
                // get spirit from each joint body a & b
                foreach (Spirit sp in levelSpirits)
                {
                    if (sp.Bodies.Contains(j.BodyA) && !connectedSpirits.Contains(sp))
                    {
                        connectedSpirits.Add(sp);
                    }
                    if (sp.Bodies.Contains(j.BodyB) && !connectedSpirits.Contains(sp))
                    {
                        connectedSpirits.Add(sp);
                    }
                }
            }

            return connectedSpirits;
        }


        /// <summary>
        /// Set position and rotation for each of our body parts, 
        /// by using position and rotation from matching body parts in other spirit.
        /// The original spirit might be damaged, so 
        /// </summary>
        public void CopyTransformsByPartType(Spirit originalSpirit)
        {
            Dictionary<PartType, Body> existingPartsOnOriginal = originalSpirit.GetPartTypeToBodyMap();

            // -this- object is the clone.. want to make it position and configuration as originalSpirit
            Vector2 cloneMainBodyPos = MainBody.Position;
            float cloneMainBodyRotation = MainBody.Rotation;


            originalSpirit.Bodies.ForEach(x => {// x.IsStatic = true;
                x.Enabled = false; });// REsetting at the end of this function not sure if both are needed.. quick fix to regrow jumping for demo..static seemed to fix it. might needto let it be static for a fraome ot take up spza after replace and xfrom
    
            // for matching parts, copy body position and rotation from it.
            // else, just use relative transform from main body.
            foreach (Body cloneBody in Bodies)
            {
                Body originalBodyPart;
                if (cloneBody.PartType != PartType.None &&
                    existingPartsOnOriginal.TryGetValue(cloneBody.PartType, out originalBodyPart))  // TOOD there are currently two  Neck parts in the Spr.. should be upper neck and lower neck.
                {
                    // copy body position and rotation here
                    Vector2 originalPartPosition = originalBodyPart.Position;

             //       cloneBody.IsNotCollideable = true;///   seems more stablle.. collision is set after shrinking
                    cloneBody.SetTransformIgnoreContacts(ref originalPartPosition, originalBodyPart.Rotation);  // this is supposed to prevent teleport issues                 

                    //All these issue we caused by teleport, now we shrink gradually.
                    //  cloneBody.Position = originalPartPosition;
                    //  cloneBody.Rotation = originalBodyPart.Rotation;  //dh test .. both ways cause issues    
                    ///TODO i think we shouild try adding the Main LinearVelocity and angluar to all.
                    //Linear Vel is not correct every frame need to test in falling balloon..
                    //   cloneBody.LinearVelocity = originalBodyPart.LinearVelocity;  // for now its replaced only at rest ,   
                    //   cloneBody.AngularVelocity = originalBodyPart.AngularVelocity;// it should do this but a) was commented out before b) ive seen creature launch sometimes  after i uncommented it..


                    cloneBody.Enabled = false;
                    cloneBody.Enabled = true;

                }
                else    // PartType.None and replacement for missing parts will use relative transform here..  ( TODO compare with paste spirit code.. and then doubel check rotation.. 
                {
                    //TODO i see rotation  son the replace legs , whippoing aroudn.. especially when one fu leg is broken and  lying down.. healing.
                    // the creature i
                    // sitting with one leg bent high.. 
                    Vector2 positionRelativeToMainBody = cloneBody.Position - cloneMainBodyPos;
                    //todo future .. would this be correct if angle went  negative?  for now its past 200, so should be fine.
               
                    
                    float angleRelativeToMainBody = cloneBody.Rotation - cloneMainBodyRotation;


             //       if (cloneBody.PartType == PartType.LeftUpperArm)
            //            Debug.WriteLine("angle"+angleRelativeToMainBody);

               //     cloneBody.IsNotCollideable = true;///   seems more stablle.. collision is set after shrinking

                    Vector2 newWorldPosition = originalSpirit.MainBody.Position + positionRelativeToMainBody;
                    cloneBody.SetTransformIgnoreContacts(ref newWorldPosition, originalSpirit.MainBody.Rotation + angleRelativeToMainBody);  // SetTransformIgnoreContacts is supposed to prevent teleport issues   

                //    cloneBody.RebuildFixtures();  //TODO clean any contacts.  TODO the  spirt explodes about when the shrink its done, then collide is turned on again

                    cloneBody.Enabled = false;
                    cloneBody.Enabled = true;
                    //TODO the systems are is loaded when it is cloned

                    //this causes it to explode more often.. TODO but using IsAtRest in plugin ts dangerous.... if regrow in balloon.. will stop..
                    // TODO  if fixed .. then we can remove the absolute velocity check in  ReadyToRegenerate
                    //     cloneBody.LinearVelocity = originalSpirit.MainBody.LinearVelocity;  // for now its replaced only at rest on surface , but could be in a moving balloon  
                    //    cloneBody.AngularVelocity = originalSpirit.MainBody.AngularVelocity;// it should do this but a) was commented out before b) ive seen creature launch sometimes  after i uncommented it..
                    ///TODO i think we shouild try adding the Main LinearVelocity and angluar to all parts.. that should do it..

                }
            }


            originalSpirit.Bodies.ForEach(x => { x.Enabled = true; x.IsStatic = false;  });


        }


        /// <summary>
        /// Copy Body.VisibleMarks from matching body parts in other spirit.
        /// </summary>
        public void CloneBodyVisibleMarksByPartType(Spirit originalSpirit)
        {
            Dictionary<PartType, Body> existingPartsOnOther = originalSpirit.GetPartTypeToBodyMap();

            // for matching parts, clone visible marks
            foreach (Body ourBody in Bodies)
            {
                Body otherBody;
                if (ourBody.PartType != PartType.None &&
                    existingPartsOnOther.TryGetValue(ourBody.PartType, out otherBody))
                {

                    foreach (AttachPoint atc in otherBody.AttachPoints)
                    {
                        if (atc.Name == Body.BulletTemporaryTag)
                        {
                            Body body;   //TODO later.. knives, other weapons or debris sticking out..
                            originalSpirit.MapAttachPtToStuckBodies.TryGetValue(atc, out body);

                            if (body != null)
                            {
                                atc.Parent = ourBody;
                                ourBody.AttachPoints.Add(atc);
                                atc.Attach(body.AttachPoints[0]);
                            }
                        }

                        foreach (MarkPoint otherPoint in otherBody.VisibleMarks)
                        {
                            // so  clone the mark point, and re-create the view again.
                            MarkPoint clone = otherPoint.Clone();
                            clone.Parent = ourBody;
                            ourBody.VisibleMarks.Add(clone);
               
                        }
                    }
                }
            }
        }


        public void EnableJoints(bool enable)
        {
            foreach (PoweredJoint pj in Joints)
            {
                pj.Enabled = enable;
            }
        }


        /// <summary>
        /// Collect all available part type in spirit.
        /// </summary>
        /// <returns></returns>
        public Dictionary<PartType, Body> GetPartTypeToBodyMap()
        {
            // collect body parts type.
            // this assumes there are no duplicate parts having the same PartType, our creatures have none so far.
            //note  some old creature had two  parts called Neck
            Dictionary<PartType, Body> parts = new Dictionary<PartType, Body>();
            foreach (Body b in Bodies)
            {
                if (b.PartType != PartType.None &&
                    parts.ContainsKey(b.PartType) == false)
                {
                    parts.Add(b.PartType, b);
                }
            }
            return parts;
        }


        /// <summary>
        /// Use the same collision group id as other spirit.
        /// </summary>
        public void CloneCollisionGroupId(Spirit other)
        {
            CollisionGroupId = other.CollisionGroupId;
        }




        public void SetAllCollidable()
        {
            Bodies.ForEach(x => x.IsNotCollideable = x.PartType.HasFlag(PartType.Eye));         
        }

        /// <summary>
        /// Return all emitters owned by this Spirit's bodies ( will return blood emitters) 
        /// </summary>
        public IEnumerable<Emitter> Emitters
        {
            get
            {
                foreach (Body body in Bodies)
                {
                    foreach (Emitter emitter in body.EmitterPoints)
                    {
                        yield return emitter;
                    }
                }
            }
        }



        /// <summary>
        /// Move spirit to new position, based on specified displacement. 
        /// This will move all spirit Bodies, and recursively all connected AuxiliarySpirits.
        /// </summary>
        /// <param name="displacement"></param>
        public void Translate(Vector2 displacement)
        {
            foreach (Body b in Bodies)
            {
                //NOTE this mght not be needed.. donest hurt.
                //movign things we are supposed to set xforme ingoring contacts and this didnt
                bool wasenabled = b.Enabled;
                b.Enabled = false;
                b.Position = Vector2.Add(b.Position, displacement);
                b.Enabled = wasenabled;
            }

            AuxiliarySpirits.ForEach(x => x.Translate(displacement));
        }

 

        /// <summary>
        /// Scale this spirit and all its contents. All scaling uses world (0,0) as center.
        ///  NOTE only scale under 10% or so is safe.  to scale more, repeat scaling  
        /// running physics a few updates at least.   The reason  is that Body Position   needs to be moved,  we are relying on physics engine and interation to 
        /// keep the joint graph together and position and rotate the connected bodies..  so a sudden scale will teleport and joint constraint might not be able to be solved from that distance. result in jump or pop or explode.

        /// </summary>
        public void ApplyScale(float x, float y)
        {
            Vector2 scale = new Vector2(x, y);
            foreach (Body b in Bodies)
            {
                b.ScaleLocal(scale);
                Level.CacheUpdateEntityView(b, 0);
            }

            // udpate spirit
            UpdateTotalAndCenterMass();
            UpdateAABB();
        }


        public void ApplySkew(float factor)
        {
            foreach (Body b in Bodies)
            {
                b.Skew(factor, true);
            }

            UpdateTotalAndCenterMass();
            UpdateAABB();
        }




        /// <summary>
        /// Mirror current spirit horizontally. Use origin (0,0) as mirror axis.
        /// </summary>
        public void ApplyMirror()
        {

            //TODO CODE REVIEW FUTUREREVISIT :   try around LowerBound.X .. for now it fails.
            //i think its do to with  each bodies GeneralVertices are supposed to be in Body (local coordinates) 

            // float axis = AABB.LowerBound.X;

            float axis = 0;


            // mirror bodies and shape
            foreach (Body b in Bodies)
            {
                // TODO: might need to convert to local coordinate of each body first
                // TODO: body that has rotation seems to give improper result.  might take rotation into account, 
                // which means should use Vector2.Reflect()
                b.MirrorHorizontal(axis);
            }

            // for joints, we only need to translate the local coordinate.
            // note: plugin that references joint index directly will not work because of this.
            foreach (PoweredJoint joint in Joints)
            {
                joint.LocalAnchorA = Vector2.MirrorHorizontal(joint.LocalAnchorA, axis);
                joint.LocalAnchorB = Vector2.MirrorHorizontal(joint.LocalAnchorB, axis);

                joint.TargetAngle *= -1f;
            }

            foreach (WeldJoint joint in this.FixedJoints)
            {
                joint.LocalAnchorA = Vector2.MirrorHorizontal(joint.LocalAnchorA, axis);
                joint.LocalAnchorB = Vector2.MirrorHorizontal(joint.LocalAnchorB, axis);
            }

            //TODO use and interface..factor out..
            foreach (DistanceJoint joint in FixedJoints)
            {
                joint.LocalAnchorA = Vector2.MirrorHorizontal(joint.LocalAnchorA, axis);
                joint.LocalAnchorB = Vector2.MirrorHorizontal(joint.LocalAnchorB, axis);
            }



            // mirror behavior here. or by caller (shadowtools) later.
            // note that behavior name is not mirrored.
            foreach (Behavior bhv in _behaviors)
            {
                foreach (Keyframe k in bhv.Keyframes)
                {
                    int cnt = k.Angles.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        k.Angles[i] *= -1f;
                    }
                }
            }

            UpdateTotalAndCenterMass();
            UpdateAABB();
        }


        /// <summary>
        /// Set all bodies in system to input density
        /// </summary>
        /// <param name="density"></param>
        public void SetDensity(float density)
        {
            ApplyDensity(density);
        }

        /// <summary>
        /// Apply uniform density to all fixture in spirit. 
        /// </summary>
        public void ApplyDensity(float density)
        {
            if (density == 0) return;

            foreach (Body b in Bodies)
            {
                b.Density = density;
            }
        }

        /// <summary>
        /// Apply uniform Mass to all Bodies in spirit. 
        /// </summary>
        public void ApplyMass(float value)
        {
            foreach (Body b in Bodies)
            {
                b.Mass = value;
            }
        }



        public bool IsLeftOfUs(Vector2 pos)
        {
            return pos.X < this.MainBody.WorldCenter.X;
        }


        public bool IsRightOfUs(Vector2 pos)
        {
            return this.MainBody.WorldCenter.X < pos.X;
        }



        /// <summary>
        /// Check if specified position is below spirit (relative, which means closer to foot than to mainbody).
        /// </summary>
        /// <param name="pos">position in world coordinate.</param>
        /// <returns></returns>
        public bool IsBelowUsRelative(Vector2 pos)
        {
            Vector2 mainbodyToItem = pos - MainBody.WorldCenter;

            // this angle is relative to mainbody orientation. calculated from 3 o clock, ccw.
            float itemAngle = MainBody.PositiveAngleToBody(mainbodyToItem);

            // check if item low.

            float belowHorizon = MathHelper.ToRadians(20f);    // horizontal line -20 degree below. -30 seems still able to accidentally pick door lock behind on airship level.
            if (itemAngle > (MathHelper.Pi + belowHorizon) &&       // left side, 180 + 20
                itemAngle < ((2 * MathHelper.Pi) - belowHorizon))   // right side, 360 - 20
            {
                return true;
            }

            return false;
        }



        /// <summary>
        /// using xform and local center of Main Body. is pt to left of that
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public bool IsToLeftOfCenter(Vector2 pt)
        {
            return (MainBody.GetLocalPoint(pt).X < MainBody.LocalCenter.X);
        }




        //TODO replace that below with this.. where possible..     angles are ok  to work with..  
        //consider a gun especially curved , cna shoot to the other side..

        /// <summary>  // NOTE.. yuck api.. better to go to main body CS  if ..i local center..
        /// Check if specified position is left or right of spirit. Based on projected angle on left/right of mainbody.
        /// </summary>
        /// <param name="pos">position in world coordinate.</param>
        /// <param name="projectionAngle"> 
        /// projection angle, in degree, to the left or right of mainbody. 
        /// smaller angle will include less area on side of spirit. 
        /// </param>
        /// <param name="isLeft">if TRUE check for left side, if FALSE check for right side.</param>
        /// 
        /// <returns>true if its to our left hand</returns>
        public bool IsSideOfUsRelative(Vector2 objPos, float projectionAngle, bool isLeft)
        {
            if (projectionAngle <= 0)
                throw new ArgumentException("projectionAngle must be larger than 0 in degrees.");

            float halfAngle = MathHelper.ToRadians(projectionAngle * 0.5f);
            Vector2 mainbodyToObject = objPos - MainBody.WorldCenter;

            // this angle is relative to mainbody orientation. calculated from 3 o clock, ccw.
            float objectAngle = MainBody.PositiveAngleToBody(mainbodyToObject);  //NOTE ..easier just go to main bodylocal CS

            if (isLeft) // left side
            {
                // from 180 - halfAngle to 180 + halfAngle
                if (objectAngle >= (MathHelper.Pi - halfAngle) &&
                    objectAngle <= (MathHelper.Pi + halfAngle))
                    return true;

            }
            else    // right side
            {
                // from 0 to halfAngle, or from 360 - halfAngle to 360
                if ((objectAngle >= 0 && objectAngle <= halfAngle)
                    ||
                    (objectAngle >= ((2 * MathHelper.Pi) - halfAngle) && objectAngle <= (2 * MathHelper.Pi)))
                    return true;
            }
            return false;
        }


#endregion


#region Internal Methods




        //TODO OPTIMIZE.. USE THE AfterCollide , it gives everything we need.  Its callled last, in the         private void Report(ContactConstraint[] constraints)
        //I think there is a report for collisions, then TOI collisions...  using the Bodies callback sets up ownship, we do'nt have to check.

        //retest Joints since they use the ourBody.TotalContactForce to decide the gain for  to add mass..

        //TODO check with CollisionEffects, its much cleaner code.   used for bullets and is on preResponse so as not to assumple elastic
        //collisino...but putting a joint after will concerv momentum migth be better for the impact plus bullet lodgin..well joint will stip it either way and it will react.but simler code no rays needed..
        //maybe not.. there is a place before reporting thqat has accurate contact info and impulse 

        // note: only Fixture.OnCollision event that always have fixtureA.Body as our body (Spirit Bodies).
        // ContactManager.PostSolve event must check both fixtureA and fixtureB.
        private void PostSolve(Contact contact, ContactConstraint impulse)
        {
            // dead / headless spirit won't respond to any collision
            //TODO .. use isminded?.. allow   bruising cutting. body after death.. and pumping bullets in ..
            if (!(IsMinded || DoPostSolve))
                return;

            // ignore collision with any sensor
            if (contact.FixtureB.IsSensor || contact.FixtureA.IsSensor)
                return;

            bool FAinSpirit = false;
            bool FBinSpirit = false;

            //TODO will need to use OnCollide, this can get exponentially complex with many spirits.
            //every spirit must process every collide everywhere.
            //however OnCollide has issues with Impulse reports, see powered joint.
            //and this.. has issues with CCD and the position of contact.


            //TODO OPTIMIZE.. USE THE AfterCollide , it gives everything we need and has the ownership
            //remove all this stuff here in PostSolve
            if (!FindWhichCollidedFixtureIsOwnedBySpirit(contact, ref FAinSpirit, ref FBinSpirit))
                return;

            Body ourBody = null;
            Body externalBody = null;
            Vector2 worldNormal = impulse.Normal;    // NOTE: normal direction is _always_ from FixtureA to FixtureB


            //TODO not needed with AfterCollide listener, Fixture A is always 
            //TODO see if AfterCollide called once per pair..

            if (!SetInternalAndExternalBodyOnCollide(contact, FAinSpirit, FBinSpirit, ref ourBody, ref externalBody, ref worldNormal))
                return;


            //TODO delegate this to plugin..  
            //  TODO   .. fix position of marks by using the AfterCollide.. also the on collison gives the correction location now for toi  ( has to be toi to be fast enoug to bruise) 
            //would do this earlier  but  kicks and punches are possible to make bruises on own fist and feet..
            // hands and feet are mostly large transparent fixture bigger than fill so don't mark them.   also since ray rast might happen don't do this on foot they collide every frame  TODO remove ray cast
            //if  only joint break from bullet will be considered.



            float maxImpulse = GetMaxNormalImpulseOnCollide(contact, impulse);
            //shadowplay Mod c.FixtureA.Body.TotalContactForceOnThis +=  TODO if use.. now handled in spirit..  
            //could  add this for all bodies   , in    private void Report(ContactConstraint[] constraints) in Island..   handle stuff like pebbles of rock being crushed..by large objects.
            //this causes shaking, should flatten or raise mass of tiny rock to stability..  also.. issue of foot tunneling trough massive  thing, like spaceship hull...
            //small mass piled under or accel against large mass is troublesome..

            ourBody.TotalContactForce += maxImpulse * World.DT;  //TODO  if this is still there on joint update...if so..  use it then, then clear it..  this is cleared in a tricky place, just after reporting, and they are reported twice, a second time after TOI
            //mass maybe should be increased to stabilize with this force is large   ( TODO) .. for joints, and for piling... try a  large boulder on tiny toe..


            if (IsMinded)
            {
                bool makeMark = ((ourBody.PartType & (PartType.Hand | PartType.Foot | PartType.Toe)) == 0);  // don't make mark if hand, foot ,or toe,

                // if reach here then one of our body have collide with external body not from this spirit
                HandleCollisionDamage(contact, ourBody, externalBody, worldNormal, maxImpulse, makeMark);  //TODO this should go to the plugin..  devices don't bruise like organics
            }


            //note.. if general use in joint, .. can make throw jump / or throw,  super powerful...all sorts of side effects..  CANNOT DO THIS AS A GENERAL CORRECTION
            //NEED TO TUNE BALLOON AND 
            //LIKE ANY OTHER.. ITS SLOW TO REACT IF DIST IS FAR, SO MUST HAVE A SOLID GAIN..

            //should only use with ropes.

            // check for bump on head..   TOOD just listen to head like he do feet in plugin.
            // NOTE: this cause OnHeadCollisionDefault called twice, one for Head and one for LowerJaw

            if (ourBody == Head || ourBody == LowerJaw)  //TODO listen in plugin, remove this..  or listen to head in here, then give special event
            {
                if (OnHeadCollision != null)
                {
                    OnHeadCollision(maxImpulse, ourBody, externalBody);
                }

                OnHeadCollisionDefault(externalBody);
            }

        }


        private void HandleCollisionDamage(Contact contact, Body ourBody, Body externalBody, Vector2 worldNormal, float maxNormalImpulse, bool placeMark)
        {
            // copied from joint OnJointBoneCollide, to get contact points and normal
            Vector2 normal;
            FixedArray2<Vector2> points;

            contact.GetWorldManifold(out normal, out points);  //says this assumes "modest" motion from the original state.   with CCD these world points are wrong.  

            // tried using OnCollide with the Ian Quist fix ( see Powered Joint and,  Island.cs) . for the normal , didn't work either.       local is far from collision point
            // NOTE: impulse.LocalPoint seems based on BodyA, which can be either our or external Body.  so it's not safe to use impulse.LocalPoint.
            //Vector2 worldContactPoint = impulse.BodyA.GetWorldPoint(ref impulse.LocalPoint);
            // for all spirit bodies.   //NOTE TODO.. with new fix in reporting , its correct for TOI.  see bullet strikes..
            Vector2 worldContactPoint = points[0];  // TODO:  checking both contacts would be most complete. 
            //Vector2 worldContactPointB = points[1];   //NOTE for sharps or fists or these should be very close..  bruise rarely would haven when falling bone parallel anyways.   and a one point contact is likely at some point in the collision

            // make scars wound mark in our body if necessary.

            //TODO redo this, handle like bullet..   bruises are offset now..
            Level.HandleDamageOnCollide(this, ourBody, externalBody, worldContactPoint, worldNormal, maxNormalImpulse, placeMark);
        }


        private static float GetMaxNormalImpulseOnCollide(Contact contact, ContactConstraint impulse)
        {
            //taken from Farseer Breakable Body
            float maxImpulse = 0.0f;
            //float maxTangentialImpulse = 0.0f;
            int count = contact.Manifold.PointCount;

            for (int i = 0; i < count; ++i)
            {
                maxImpulse = Math.Max(maxImpulse, impulse.Points[i].NormalImpulse);
                //maxTangentialImpulse = Math.Max(maxTangentialImpulse, impulse.Points[i].TangentImpulse);
            }
            return maxImpulse;
        }


        private static bool SetInternalAndExternalBodyOnCollide(Contact contact, bool FAinSpirit, bool FBinSpirit, ref Body ourBody, ref Body externalBody, ref Vector2 worldNormal)
        {
            if (FAinSpirit)
            {
                ourBody = contact.FixtureA.Body;
                externalBody = contact.FixtureB.Body;
            }
            else if (FBinSpirit)
            {
                ourBody = contact.FixtureB.Body;
                externalBody = contact.FixtureA.Body;
                worldNormal *= -1f;      // reverse the normal
            }
            else
            {
                // if both are not from this spirit then return.
                // Fixed for 2 spirit instance getting event when one was struck on head.
                return false;
            }

            return true;
        }

        /// <summary>
        /// return false if this is a self collision
        /// </summary>
        /// <param name="contact"></param>
        /// <param name="FAinSpirit"></param>
        /// <param name="FBinSpirit"></param>
        /// <returns></returns>
        private bool FindWhichCollidedFixtureIsOwnedBySpirit(Contact contact, ref bool FAinSpirit, ref bool FBinSpirit)
        {
            //TODO don't like having to do a 2 hash lookup  on every collide of everything , for every spirit.. this is wrong..  
            // do this on body collided.
            // FUTURE  FIX OPTIMIZATION .. use onCollide which will give the External  Body.
            //issue .. need to use 3.3 and make sure impulse infor is valid .. or see island  Qvist fix
            FAinSpirit = BodySet.Contains(contact.FixtureA.Body);
            FBinSpirit = BodySet.Contains(contact.FixtureB.Body);

            return !(FAinSpirit && FBinSpirit);

        }


        private static short GetNextSpiritCollisionGroup()  //static so every spirit instance will have a unique value.
        {
            _newSpiritCollisionGroup -= 1;
            return _newSpiritCollisionGroup;
        }




        /// <summary>
        /// Update spirit animation Time
        /// </summary>
        private void UpdateAnimation(double dt)
        {
            if (_pwdjoints.Count <= 0)
                return;

            double invScale = ActiveBehavior.TimeDilateFactor * TimeDilateAdjustFactor;
            double maxTime = 0;

            if (ActiveBehavior.Keyframes.Count > 0 && _pwdjoints.Count > 0)
            {
                maxTime = ActiveBehavior[ActiveBehavior.Keyframes.Count - 1].Time * invScale;

                if (_spiritPlay == SpiritPlay.TransitionFromSourceToTarget ||
                    _spiritPlay == SpiritPlay.TransitionToTarget)
                {
                    maxTime = _duration;
                }


                // update current animation time
                _currentTime += dt;

                if (_currentTime > maxTime)
                {
                    //if we have passed max time, get next behavior,  in the case of SpiritPlay.Repeat it is set to the ActiveBehavior
                    if (NextBehavior != null)
                    {
                        // switch to next Behavior
                        ActiveBehavior = NextBehavior;
                        _keyEndIndex = ActiveBehavior.Keyframes.Count - 1;
                        _behavior2 = NextBehavior;
                    }
                    // always reset interrupt flag here when the first key commanded behavior is ended 
                    IsExecutingFirstCycleAutoBehavior = false;

                    if (NextBehavior == null || _spiritPlay == SpiritPlay.OneTime ||
                        _spiritPlay == SpiritPlay.TransitionFromSourceToTarget ||
                        _spiritPlay == SpiritPlay.TransitionToTarget)
                    {
                        Stop();
                        if (OnStop != null) OnStop(this);
                    }


                    // always reset elapsed time here
                    _currentTime = 0;
                }

                // just in case reverse play, or negative deltaTime 
                if (_currentTime < 0)
                {
                    _currentTime = maxTime;
                }

                NotifyPropertyChanged("CurrentTime");

            }
        }


        /// <summary>
        /// In this spirit, get joints that connected to a group of bodies. 
        /// Should only used after Joints are modified by GraphEdit. 
        /// </summary>
        private List<PoweredJoint> GetJointsOnBodies(IEnumerable<Body> bodies)
        {
            List<PoweredJoint> joints = new List<PoweredJoint>();

            foreach (PoweredJoint pj in Joints)
            {
                if (bodies.Contains(pj.BodyA) == true || bodies.Contains(pj.BodyB) == true)
                {
                    joints.Add(pj);
                }
            }
            return joints;
        }

        /// <summary>
        /// Call this when any joint break and cause spirit.Bodies modified.
        /// </summary>
        private void CheckIfHeadSevered()
        {
            // if internal null then assume head already severed before
            if (_head == null)
                return;

            // update head part
            Head = GetBodyWithPartType(PartType.Head, false, true);

            // if spirit doesn't contain head, it should be dead.
            if (Head == null && EnergyLevel > 0)
            {
                LoseHead();
            }
        }

        private void LoseHead()
        {
            if (OnKilled != null)
            {
                OnKilled(this, float.MaxValue);
            }
        }

        public void DropDead()
        {
            if (OnKilled != null)
            {
                OnKilled(this, float.MaxValue);
            }

            Die();
        }




        public enum SensorShape {[EnumMember(Value = "Circ")] Circle, Rectangle };


        [DataMember]
        public SensorShape SensorType { get; set; }



        private void ResizeSensor()
        {
            // sensor is expensive on moving items because it needs to update the tree. 
            //for yndrd we  query AABB ( which can be expensive also if big) butg that requires rebuilding the lists.  adding and removing just the item
            //to a quickarray for hashset on body separated might be faster.
            //TODO cehck for clouds if breaking can just be a sensor
            //for using query items of grid, its seems ideal and shown in velcrophyscis tests
            //TODO  NOTEs can sensor detect a static body?


            if (_sensorRadius <= 0)
                return;

            // try to change shape radius if possible
            if (SensorFixture != null)
            {
                if (sensorFixture.Shape is CircleShape && SensorType != SensorShape.Circle ||
                        sensorFixture.Shape is PolygonShape && SensorType != SensorShape.Rectangle)
                {
                    DisposeSensor();
                }


#if CIRCLESENSOR
                // remove listener first, because we want to clear collided bodies, since onsperaration must track items. 
                //?? this comment is scarey, so TEST//TODO if we resize this in action, mabye see if onseparation bodies are called,  if it was touchign something then got smaller
#endif
                SensorFixture.OnCollision = null;  //not sure why resetting these if its just because of logic arrangement  flow
                SensorFixture.OnSeparation = null;

                // resize sensor shape
                SensorFixture.Shape.Radius = SensorRadius;
            }
            else
            {
                if (MainBody == null)
                {
                    Debug.WriteLine("no Main Body, invalid spirit");
                    return;
                }

                CreateSensorFixture();


                //and implement without collide response..  ( or maybe switchable to unstick it).   testing.. the optimization works. and can afford sensor
                SensorFixture.CollisionFilter.CollisionGroup = CollisionGroupId;
            }

#if CIRCLESENSOR
            // collision listener will collect all body collide with our spirit
            SensorFixture.OnCollision += OnSensorCollision;
            SensorFixture.OnSeparation += OnSensorSeparation;
#endif
        }

        private void CreateSensorFixture()
        {
            if (SensorType == SensorShape.Circle)
            {
 
                SensorFixture = FixtureFactory.CreateCircle( _sensorRadius, 1f, MainBody,MainBody.LocalCenter);//for some reason this offset doest work for the sensor view in tool, does work for rectangle
           //     SensorFixture = FixtureFactory.CreateCircle(_sensorRadius, 1f, MainBody);

                // most spirit dont have one, i think none in production levels, no IsSensor or SensorRadius set by plugin calsses taking into production.. mabye older loose plugin scripts.  was used for joint and ai now disused
                SensorFixture.IsSensor = true;
                //dh this body is static   ..  need to try a transparent joined body instead for  bullet around body?? why if this works for cloud eras
                //Kinematic body?... TODO clouds should be.. zero mass vel set...  clouds should have aabb set the sensor and it could be resized.. mabye offset
                //SensorFixture.Body.IsBullet = true;   TODO check.. this would be slow
                //TODO consider using a senor around all 

                //consider using  a special broadphase or farseer that just collides the aabb fixtures first... 
                //TODO consider like OPTIMIZENESTEDAABBEXPERIMENT to just make collidable spirits in the insersect aabb of colliding body and this aabb sensor
                //so, in case of walking on ground  would be  is just the feet , since the groudn collision with the extended for vel , aabb of spirt, with its intersection would be feet

            }
            else if (SensorType == SensorShape.Rectangle)
            {

              // create the polygon shape for rect and attach to the MainBody
                  SensorFixture = FixtureFactory.CreateRectangle( _sensorRadius * 2f, _sensorRadius * 2f / SensorAspectRatio, 0, MainBody.LocalCenter, this.MainBody);
     
                SensorFixture.IsSensor = true;

            }
        }

        /// <summary>
        /// Remove current sensor, make sure active contacts and such are done.  Must be done when physics is locked.
        /// </summary>
        private void DisposeSensor()
        {
            if (SensorFixture == null)
                return;

            // remove previous sensor view
            if (SensorDestroying != null)
                SensorDestroying(this);

            //NOTE   settings these to null are over caution, really not necessary since the gc . leak can happen if source outlives listener, like a listening view that is removed, we must null the listener in the view as we remove it.  in this case our source is dying now, listener is the spirit
            //https://stackoverflow.com/questions/3662842/how-do-events-cause-memory-leaks-in-c-sharp-and-how-do-weak-references-help-miti

#if CIRCLESENSOR
            SensorFixture.OnCollision = null;
            SensorFixture.OnSeparation = null;
#endif
            // remove any dummy body &  fixture
            if (sensorFixture.Body != this.MainBody)
            {
                World.Instance.RemoveBody(SensorFixture.Body);
            }
            else
            {
                MainBody.DestroyFixture(sensorFixture);
            }

            SensorFixture = null;
        }


        /// <summary>
        /// Activate or deactivate behavior based on changed game key. 
        /// Return false if no behavior found for given key.
        /// </summary>
        /// <param name="pressed">TRUE if key is changed from OFF to ON. FALSE otherwise.</param>
        private bool UpdateBehaviorOnInput(GameKey newGameKeyState, GameKey changedGameKey, bool pressed)
        {

            List<Behavior> behaviors;
            if (!_mapGamekeyToBehaviors.TryGetValue(changedGameKey, out behaviors)) // if no behavior registered with game key, return false
            {
                return false;
            }

            // to ON
            if (pressed)
            {
                // only play keypressed behavior if it's not being played.                    
                if (PauseOnReleaseKey && changedGameKey == ActiveBehavior.GKey && IsPaused)
                {
                    Resume();
                }
                else
                if (changedGameKey != ActiveBehavior.GKey
                    || (IsAnimating == false && !PauseOnReleaseKey) ///NOTE  TODO TEST consider removing this is maybe annoying.  if pressed again it should override safety or block and go, by resume.   
                    )
                //note ..if pressed again while in "suspended animation" , (only possilbe if plugin said it coul dnot stop)  need not do anything..  or.. start from 0, if we took off our speed.. but that for the plugin to deal with.. this is just a 
                ///simple state machine . plugin needs to deal with animated creature, having its speed taken by obstacles , etc.  it can decide if to start again at zero.
                {
                    // TODO: this is where IsSafeChangeDirection() for player yndrd should take place.
                    // check if safe/allowed to changing behavior
                    //System.Diagnostics.Trace.TraceInformation("Playing behavior Key : {0}", k);

                    Behavior first = behaviors.FirstOrDefault(x => x.FirstTimeExec == true);
                    if (first == null) first = behaviors[0];

                    if (ActiveBehaviorChangingKeyDown == null || ActiveBehaviorChangingKeyDown(first))
                    {
                        Play(changedGameKey);  //this will play the first one, and set the NextBehavior state..
                    }
                    else
                    {
                        Debug.WriteLine("can't change" + ActiveBehavior + " to" + changedGameKey);
                    }

                }

            }
            // OFF state
            else
            {
                // I think we only need to deactivate current behavior, and switch 
                // to another active behavior, no need to stop the entire animation. 
                // If there are no more active behavior, then we will stop the 
                // entire spirit animation. 
                // But that requires state for multiple active behavior. Game key 
                // state alone is not enough, as there might be key in ON state 
                // but not mapped to any behavior.
                // only stop if behavior with associated key is running.
                if (changedGameKey == ActiveBehavior.GKey && IsAnimating == true)
                {

                    if (PauseOnReleaseKey)
                    {
                        Pause();
                    }
                    else
                    {

                        //NOTE TODO .. this might continue with  an extra short  step to avoid falling forward, or bend knee to absorb shock, or delay and lean back., AI code didnt work perfectly at all needs to improve like this
                        //NOTE TODO in plugin if in return step, best to abort that and return the back foot  to straight down if possible.

                        //use the friend follower to test.... it can alert the plugin to stop using by using  crouch or bent knee.. see more about this.

                        //important , especially on taller robot.. with unstable states.  this is a state machine, cannot have transition to unstable state
                        //it will be fine.. with knee bends, good auto intervention modes.. manual overrides..
                        //good state management.. no hacks.  


                        bool SuspendedAnimation = false;   //suspected animation state..   dont need to do anything.. if we do lean back druing delay.. other code in plugin should deal with this "perturbance"..  things like prevent return steup.. if we lost all our vel.. that can happen by running into something..

                        if (ActiveBehaviorStoppingOnKeyUp != null)
                            SuspendedAnimation = !ActiveBehaviorStoppingOnKeyUp(ActiveBehavior);


                        if (!SuspendedAnimation)
                        {
                            Debug.WriteLine("stop on key up");
                            Stop();
                            EndAtStartPose();
                        }
                        else
                        {
                            Debug.WriteLine("suspended animation for " + ActiveBehavior.Name);
                        }

                    }
                }
            }

            // if reach here return true
            return true;
        }



        public void Resume()
        {
            if (_isPaused)
            {
                IsAnimating = true;//sets the ispaused to false.. 
            }
        }

        /// <summary>
        /// Collect  Grabber attach points from our spirit bodies. 
        /// </summary>
        /// <param name="type">If PartType.None, then collect attach point regardless of type.</param>
        /// <param name="skipConnected">If true, skip if attach point currently connected.</param>
        public List<AttachPoint> CollectOurSpiritGrabberAttachPoints(PartType type, bool skipConnected)
        {
            List<AttachPoint> attachPoints = new List<AttachPoint>();
            foreach (Body b in Bodies)
            {
                if (b.AttachPoints.Count == 0)
                    continue;

                attachPoints.AddRange(CollectGrabberAttachPoints(b.AttachPoints, type, skipConnected));
            }
            return attachPoints;
        }


        public RegenerateMissingBodyParts GetRegrowEffect()
        {
            RegenerateMissingBodyParts regrowEffect = null;
            if (Effects.Contains(Spirit.RegrowKey))
            {
                regrowEffect = Effects[Spirit.RegrowKey] as RegenerateMissingBodyParts;
            }
            return regrowEffect;
        }


        /// <summary>
        /// Body is still regrowing, not at full size.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="scale"> scale from 0 to 1. 1 if not regenerating</param>
        /// <returns>true if regrowing</returns>
        public bool GetRegeneratingScale(Body body, out float scale)
        {
            scale = 1.0f;

            RegenerateMissingBodyParts regrowEffect = GetRegrowEffect();

            if (regrowEffect != null)
            {
                if (regrowEffect.ScalingPartScaleMap.ContainsKey(body))
                {
                    scale = regrowEffect.ScalingPartScaleMap[body];
                    return true;
                }
            }
            return false;
        }


        public bool IsShrinkingForRegen(Body body)
        {
            RegenerateMissingBodyParts regrowEffect = GetRegrowEffect();
            if (regrowEffect != null)
            {
                return regrowEffect.DoesNestedShrinkContain(body);
            }
            return false;
        }


        /// <summary>
        /// Collect grabber attach points from input list .     if parent is a growing hand, skip it.
        /// </summary>
        /// <param name="type">If PartType.None, then collect attachpoint regardless of type.</param>
        /// <param name="skipConnected">If true, skip if attach point currently connected.</param>
        private List<AttachPoint> CollectGrabberAttachPoints(IEnumerable<AttachPoint> attachpoints,
            PartType type, bool skipConnected)
        {
            List<AttachPoint> newAtp = new List<AttachPoint>();

            foreach (AttachPoint ap in attachpoints)
            {
                if (skipConnected == true && ap.Joint != null)
                    continue;

                if (!ap.IsGrabber)
                    continue;

                float scale;
                GetRegeneratingScale(ap.Parent, out scale);

                // if small hand  or shrinking hand  dont do grab
                // TODO check the grip size..targetGrip .HandleWidth for now allow grab anything with 70% hand for now.
                // need to pass in Target attach point tho
                if (scale < minScaleGrabUseable || ap.Parent.IsNotCollideable || IsShrinkingForRegen(ap.Parent))
                    continue;


                if ((type & ap.Parent.PartType) == type)
                {

                    newAtp.Add(ap);
                    continue;
                }


            }

            return newAtp;
        }

        public bool CannotDoAnything()
        {
            return (IsDead || IsHavingSeizure || IsUnconscious);
        }





        /// <summary>
        /// Collect external attach points inside sensor range. SensorFixture must be available.
        /// </summary>
        /// <param name="type">If PartType.None, then collect attachpoint regardless of type.</param>
        /// <param name="skipConnected">If true, skip if attach point currently connected.</param>
        public List<AttachPoint> CollectExternalAttachPoints(PartType type, float reachRange, bool skipConnected)
        {


            float MaxDistSq = GetMaxReachFromMainBodyCMSq(reachRange);  //TODO should set a mainbody cm to shoulder or hip  or neck, whatever grabs.... this is for biped...conservative   his height is 1.8 m

            List<AttachPoint> newAtp = new List<AttachPoint>();


            foreach (AttachPoint ap in AttachPointsInSensor)
            {
                if (ap.IsGrabber || ap.IsTarget)
                    continue;

                if ((ap.Flags & AttachPointFlags.IsDisabled) != 0)
                    continue;

                if (skipConnected &&
                    (ap.Joint != null ||
                    ((ap.Parent.PartType & PartType.Hand) != 0) && ap.Parent.AttachPoints.Any(x => x.Joint != null)))  // hands have 2 attach points on fingers..LIMITATION.. ON OBJECT PER HAND if any attachments are holding something on same hand don't allow to grab it, this is because grip of sword handle would prevent it. 
                {
                    continue;
                }

                //TODO future .. use this way for tasted foods..   search Food
                if (type == PartType.Weapon && ap.Parent.IsWeapon && CanPickupProjectileWeapon)
                {
                    if (!Mind.GunHandleWeKnowToBeSpent(ap))
                    {
                        newAtp.Add(ap);  //take this one                
                    }
                    continue;
                }


                if ((ap.WorldPosition - MainBody.WorldCenter).LengthSquared() > MaxDistSq)
                    continue;


                if (IsCarnivore && type == PartType.Food && ap.Parent.Nourishment > 0 &&
                    ((ap.Parent.PartType & (PartType.Toe | PartType.Eye | PartType.Foot | PartType.Hand)) != 0))
                {
                    newAtp.Add(ap);
                    continue;
                }
                else
                // if type is specified and didn't match then continue
                    if (type != PartType.None && ap.Parent.PartType != type)
                    continue;


                newAtp.Add(ap);
            }

            return newAtp;
        }

        //TODO improve, this is guess..  GRABBING   CREATURES BIPED specific  OPTIMIZATION
        private float GetMaxReachFromMainBodyCMSq(float reachRange)
        {
            float bodydistSq = 2 * SizeFactor + reachRange;
            return bodydistSq *= bodydistSq;
        }




      

        /// <summary>
        /// </summary>
        /// <param name="ignoredBodies">Additional bodies to be ignored by raycast. 
        /// If null then only the default grabber and item body that ignored by raycast.</param>
        public bool IsLOSClearBetweenGrabberAndItem(Sensor sensor,
            AttachPoint grabberAP, AttachPoint itemAP, IEnumerable<Body> additionalIgnoredBodies)
        {
            // later we might  ignore other  spirit body except legs.. if we can reach around head, etc..
            // for now just ignore our grabbing arm bodies, including main body.   its collected  in _leftArmParts
            // testing reach for something above my shoulder.

            // NOTE: because attachpoint is non-hittable and often located outside of its Parent Body 
            // (to prevent non-shaking pickup), careful for case when attach point is sunk into other Body.
            // example: item on ground might have attach point that sunk into ground, while its 
            // parent body is above ground. in this case, ground must also be included in ignoredBodies
            // to get a non-intersect raycast and clear LoS.   this is done, except angle of attack is not accoutned for (TODO)

            List<Body> ignoredBodies = new List<Body>();

            if (additionalIgnoredBodies != null)
            {
                ignoredBodies.AddRange(additionalIgnoredBodies);

                //   List<Body> armBodies = grabberAP.Parent.PartType == PartType.LeftHand ? _leftArmParts : _rightArmParts;    
                //we might be passing an item from hand to hand , ignore both
                //ignoredBodies.AddRange(_leftArmParts);
                //ignoredBodies.AddRange(_rightArmParts);
            }

            // NOTE: We can't ignore parent bodies, others might try to grab attach point on
            // opposite site of parent body, which is certainly blocked. 
            // So instead, we check the length of blocked ray:
            // dist to object (ignoring parents) subtracted by  dist of ray intersect (when including parents). 
            // if blocked length is small, we might just ignore parent bodies.


            // first, we raycast by ignoring attachpoints' parent. if already ignored, don't need to add again.
            if (!ignoredBodies.Contains(grabberAP.Parent))
            {
                ignoredBodies.Add(grabberAP.Parent);
            }
            if (!ignoredBodies.Contains(itemAP.Parent))
            {
                ignoredBodies.Add(itemAP.Parent);
            }


            bool grabberAndItemOnSamePosition = (grabberAP.WorldPosition == itemAP.WorldPosition);
            bool isGrabberLOSClear = false;


            if (grabberAndItemOnSamePosition)
            {
                isGrabberLOSClear = true;
            }
            else
            {
                // Check LOS from grabber

                RayInfo grabberLOS = sensor.AddRay(
                grabberAP.WorldPosition, itemAP.WorldPosition,
                    grabberAP.GetHashCode().ToString() + itemAP.GetHashCode().ToString(),
                    ignoredBodies
#if DEBUG
                 , BodyColor.NeonGreen, true, false);
#else
                );
#endif
                isGrabberLOSClear = (grabberLOS.IsIntersect == false);
            }

            // if LOS is clear, now doing second raycast, with parent NOT ignored. 
            // hopefully didn't affect performance much. no need to do this if LOS is already blocked,
            // or grabber and item are already in same position.
            if (isGrabberLOSClear == true && grabberAndItemOnSamePosition == false)
            {
                // AttachPointParentIntersectSmallDistance is based on current yndrd hand and foot thickness, 
                // assuming door is always thicker than hand grabber. 
                // and assuming door lying on ground is always thicker than foot.

                ignoredBodies.Remove(itemAP.Parent);

                RayInfo losClearIncludeParent = sensor.AddRay(
                    grabberAP.WorldPosition, itemAP.WorldPosition,
                    grabberAP.GetHashCode().ToString() + itemAP.GetHashCode().ToString(),
                    ignoredBodies);


                // if LOS not clear when including parents, check blocked ray length
                if (losClearIncludeParent.IsIntersect)
                {

                    float distToObject = (grabberAP.WorldPosition - itemAP.WorldPosition).Length();
                    float clearLength = (losClearIncludeParent.Intersection - grabberAP.WorldPosition).Length();
                    float blockedLength = distToObject - clearLength;  //this is the depth of the attach point inside the parent.. say sword handle

                    //TODO on approach from and angle .. do the dot product along the normal.. ..

                    // if blocked ray length is small relative to handsize, consider it as clear LOS
                    // should be about half handsize.. 

                    if (blockedLength > GrabbingDepthMax)
                    {
                        isGrabberLOSClear = false;
                    }

#if BADFIXFORROPE
//to fix this proper use the direction of the attach point ,  the hand angle would be too great 180
//to mactch it dir on other side of rope will point out, or auto gen attac points based on features , how thick and far out a part is
                    else
                    {   //now check reverse ray.. if  attach pt is too deep , as in , on other side of parent.. say a rope.. don't grab it... will break hand.. ( fix for rope climb)
                        losClearIncludeParent = sensor.AddRay(
                        itemAP.WorldPosition, grabberAP.WorldPosition,
                        grabberAP.GetHashCode().ToString() + itemAP.GetHashCode().ToString() + "r",
                        ignoredBodies);

                        if (losClearIncludeParent.IsIntersect)
                        {
                            isGrabberLOSClear = false;
                        }

                }
#endif
            }
            }

            return isGrabberLOSClear;
        }


        /// <summary>
        /// How deep can an Attach pt be inside its parent for grab to be considered..ray from grabber.. 
        /// got by dist grabber to attach pt ( example, attach pt  in sword handle) , which overlaps with hand  on command to attach, the dist hand atc to grb - hat atc to  the ray intersect of ray from hand atc to grip atc and grip atc part body 
        /// </summary>
        public float GrabbingDepthMax { get; set; }


        //TODO  move all callers to creature , erase this method.

        /// <summary>
        /// Get index of shoulder joint. Returns -1 if shoulder joint not found.
        /// </summary>
        public int GetShoulderJointIndex(bool left)
        {
            PartType upperArm = Body.SetPartDir(PartType.UpperArm, left);
            int shoulderIdx = -1;

            //TODO CODE REVIEW FUTURE could just walk the graph till main body.. but this is ok.    body has jointlist..etc..
            // shoulder is joint that connect main body and upper arm. search that.
            for (int i = 0; i < _pwdjoints.Count; i++)
            {
                PoweredJoint pj = _pwdjoints[i];
                bool aUpperArm = (pj.BodyA.PartType == upperArm);
                bool bUpperArm = (pj.BodyB.PartType == upperArm);

                if (aUpperArm || bUpperArm)
                {
                    bool aMainBody = (pj.BodyA.PartType == PartType.MainBody);
                    bool bMainBody = (pj.BodyB.PartType == PartType.MainBody);

                    if ((aUpperArm && bMainBody) || (bUpperArm && aMainBody))
                    {
                        shoulderIdx = i;
                        break;
                    }
                }
            }
            return shoulderIdx;
        }

  

        private Spirit GetOtherSpiritFromAttachPoint(AttachPoint objectAP)
        {
            // because level MapBodyToSpirits only store spirit MainBody,
            // we need to graphwalk starting from control part.
            return GraphWalker.GetSpiritFromBody(objectAP.Parent);
        }


//PLAYABLILITY TODO 

        /// <summary>
        /// on select Target
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public void GoAndGrab( Body body)
        {
            //TODO use an effect?           
        }

        public bool CanGoAndGrab(Body body)
        {
           //check LOS , walls, etc
            //TODO use an effect?
            return true;
        }



        /// <summary>
        /// This will update HeldBodieslist and HeldSharpPoints on every pickup and drop.
        /// </summary>
        public void UpdateHeldItemsLists(AttachPoint attachPoint, bool attach)
        {
            if (attachPoint == null)
                return;

            if (attach)
            {
                if (HeldBodies.Contains(attachPoint.Parent) == false)
                {
                    HeldBodies.Add(attachPoint.Parent);
                }
            }
            else
            {
                HeldBodies.Remove(attachPoint.Parent);
            }

            foreach (SharpPoint sh in attachPoint.Parent.SharpPoints)
            {
                // if attaching and sword tip not yet in ignore list, add it
                if (attach)
                {
                    if (HeldSharpPoints.Contains(sh) == false)
                    {
                        HeldSharpPoints.Add(sh);
                    }
                }
                else
                {
                    HeldSharpPoints.Remove(sh);
                }
            }
        }


        /// <summary>
        /// This method will give angle correction required to make a straight line between 
        /// hand CM to grip direction vector.
        /// </summary>
        public float GetAngleBetweenHandAndTargetGrip(AttachPoint objAtp, AttachPoint handAtp)
        {
            // if picked up object has sharp point, rotate grip hand so that weapon is
            // straightened with hand, so it appears more held between fingers.
            Body hand = handAtp.Parent;
            Body obj = objAtp.Parent;

            if (objAtp.Direction != Vector2.Zero && handAtp.Direction != Vector2.Zero)
            {
                // a vector from hand cm to object grip cm
                Vector2 spiritHandToObjectGripVector = obj.WorldCenter - hand.WorldCenter;

                Vector2 spiritHandDirection = hand.GetWorldVector(handAtp.Direction);
                Vector2 objGripDirection = -(obj.GetWorldVector(objAtp.Direction));    // grip dir vector always point to back hilt, not to sword tip.

                // shortest angle from obj direction to spiritHandToObjectGripVector
                double angleLinedUp1 = MathUtils.VectorAngle(ref objGripDirection, ref spiritHandToObjectGripVector);

                // shortest angle from spiritHandToObjectGripVector to hand direction
                double angleLinedUp2 = MathUtils.VectorAngle(ref spiritHandToObjectGripVector, ref spiritHandDirection);

                // return angle correction.
                // NOTE: this is not always guaranteed to work, on rare occasion sword still stuck on pickup because wrong rotation direction.
                // however this is still better than previous fix.
                return (float)(angleLinedUp1 + angleLinedUp2);


                //// this one didn't work on level1b. might be useful later.  //TODO consider using body CS
                //{
                //    // get shortest angle to rotate objGripDirection to lined up with spiritHandToObjectGripVector, so both pointing same direction.
                //    // the sign of this angle will be used to determine further joint rotation.
                //    double angleLinedUpSign = MathUtils.VectorAngle(ref objGripDirection, ref spiritHandToObjectGripVector);

                //    // get shortest angle to rotate objGripDirection to lined up with spiritHandDirection, so both pointing same direction.
                //    // this is the actual angle that needed to lined up object with hand.
                //    double angleToLineUp = MathUtils.VectorAngle(ref objGripDirection, ref spiritHandDirection);

                //    // make it so angleLinedUp rotate to the same direction/sign as angleLinedUpSign
                //    if (angleLinedUpSign >= 0 && angleToLineUp < 0)
                //    {
                //        angleToLineUp += 2 * Math.PI;    // rotate to the same target angle, but using opposite direction (+, ccw)
                //    }
                //    else if (angleLinedUpSign < 0 && angleToLineUp >= 0)
                //    {
                //        angleToLineUp -= 2 * Math.PI;    // rotate to the same target angle, but using opposite direction (-, cw)
                //    }
                //}
            }

            return 0;
            //TODO CODE REVIEW  should throw exception.. 0 is a valid angle
            // Zero can also means no angle correction needed, just pickup as it is.  -DC.
        }




        /// <summary>
        /// used when arms are broken..  or too small.. can subs head for eitehr left or right . can do some two handing thing like climb
        /// </summary>
        public Nullable<bool> SubstHeadForLeft;

        /// <summary>
        /// Check all attach point in spirit. Update which side of spirit that holds item. 
        /// This should be called before and after every attach.
        /// </summary>
        public void UpdateHeldItemSide()
        {
            // always reset first
            ResetHoldingStates();

            bool left = false;
            //TODO  NEW  USE THE ISGRABBER AND INCLUDE HEAD IF NOT RIGHT   SET THAT ON HEAD WHEN ARM IS NOT AVAILABLE AND GET RID OF SUBSTHEADFORLEFT AND THIS COMPLEX METHOD
            foreach (Body b in Bodies)
            {

                if (b.PartType == PartType.MainBody)  // grab with main body not supported.. since grab points for weapon sticking such as heart, are there it can conflict.  use appendages
                    continue;

                foreach (AttachPoint attachpoint in b.AttachPoints)
                {
                    if (attachpoint.Joint != null)
                    {
                        if ((attachpoint.Flags & AttachPointFlags.IsTemporary) != 0)
                            continue;


                        if ((attachpoint.Parent.PartType & PartType.Head) != 0)
                        {
                            if (SubstHeadForLeft != null)
                            {
                                if (SubstHeadForLeft == true)
                                {
                                    left = true;
                                }
                                else
                                {
                                    left = false;
                                }
                            }
                        }



                        if ((attachpoint.Parent.PartType & PartType.Left) != 0)
                        {
                            left = true;
                        }

                        else if ((attachpoint.Parent.PartType & PartType.Right) != 0)
                        {
                            left = false;

                        }
                        else
                        {
                            Debug.WriteLine(" grabber needs to be marked left or right, guessing using pts in main body local space, not tested");
                            Vector2 pt = new Vector2(attachpoint.WorldPosition.X, attachpoint.WorldPosition.Y);
                            Vector2 ptLocal = MainBody.GetLocalPoint(pt);
                            left = (ptLocal.X < MainBody.LocalCenter.X);

                        }



                        if (left)
                        {
                            HeldBodyLeft = attachpoint.Partner.Parent;
                            HeldGripLeft = attachpoint.Partner;
                        }
                        else
                        {
                            HeldBodyRight = attachpoint.Partner.Parent;
                            HeldGripRight = attachpoint.Partner;
                        }


                        if (Head == null)
                            return;

                        if (attachpoint.Partner.Parent.PartType == PartType.Rope)
                        {
                            HoldingRope = true;

                            //    if (attachpoint.WorldPosition.Y > WorldCenter.Y)   //if dragging rope.. don't want to be climbing it.. //?TODO do we support folding rope.. would climbing be like pulling it?  try.. 
                            {
                                HoldingClimbHandle = true;
                                if (left)
                                {
                                    HoldingClimbHandleLeft = true;
                                }
                                else
                                {
                                    HoldingClimbHandleRight = true;

                                }
                            }

                        }
                        else    //the above logic with ropes supersedes this..    we can be dragging a robe or climbing it.. so the height check is important...
                        if (attachpoint.Partner.Parent.PartType == PartType.Handhold
                            || attachpoint.Partner.IsClimbHandle)
                        {
                            HoldingClimbHandle = true;


                            if (left)
                            {
                                HoldingClimbHandleLeft = true;
                            }
                            else
                            {
                                HoldingClimbHandleRight = true;

                            }
                            //TODO if using bite hold..do right if other hand msssing?
                        }

                        // if using this hand part type check, other spirit will update 
                        // its HeldSpiritUnderControl property to us.
                        //else if (attachpoint.Pair.Parent.PartType == PartType.LeftHand || attachpoint.Pair.Parent.PartType == PartType.RightHand)
                        //{
                        //}
                        // if held object is a controller, need to update HeldSpiritUnderControl.
                        // this will update HeldSpirit property after control part on other spirit 
                        // had been attached to us.

                        //TODO for walking hand in hand with another  HeldSpirit, even if not in control.   friend or swordsman..
                        else
                         if (attachpoint.Partner.Parent.PartType == PartType.Control || attachpoint.Partner.IsControl)
                        {
                            SetHeldSpirit(attachpoint, left, true);
                        }
                        else
                        {
                            SetHeldSpirit(attachpoint, left, false);
                        }
                    }
                }
            }
        }

        private void ResetHoldingStates()
        {
            HeldBodyLeft = null;
            HeldBodyRight = null;

            HoldingRope = false;
            HoldingClimbHandle = false;

            HoldingClimbHandleLeft = false;
            HoldingClimbHandleRight = false;

            HeldSpiritUnderControlLeft = null;
            HeldSpiritUnderControlRight = null;

            HeldSpiritLeft = null;
            HeldSpiritRight = null;

            HeldGripLeft = null;
            HeldGripRight = null;

            HoldingLeftRightInputDevice = false;

            HoldingGun = false;
        }

        private void SetHeldSpirit(AttachPoint ownAttachpoint, bool isLeft, bool isControlling)
        {
            Spirit spirit = GetOtherSpiritFromAttachPoint(ownAttachpoint.Partner);
            if (isLeft)
            {
                if (isControlling)
                {
                    HeldSpiritUnderControlLeft = spirit;
                }

                HeldSpiritLeft = spirit;

            }
            else
            {
                if (isControlling)
                {
                    HeldSpiritUnderControlRight = spirit;
                }

                HeldSpiritRight = spirit;
            }
        }





        //TODO check this , after I scale a creature,.. it often places joint 1 on wrong end of bone..
        //can maually fix it..

        /// <summary>
        /// Add emitter for bleeding on each joint position. 
        /// </summary>
        public void AddOrAdjustBloodEmittersToJoints()
        {
            // only execute this cleanup to recreate all joint emitters.
            //ClearAllBloodEmitters("blood");
            // usually after joints index order in spirit are changed, should clean all emitters,
            // to avoid duplicate emitters in a body, and to avoid renaming a lot of emitter points.
            int max = _pwdjoints.Count;
            for (int idx = 0; idx < max; idx++)
            {
                PoweredJoint pj = _pwdjoints[idx];

                // make unique name for this emitter, required if body have different emitters for different joints
                string strBloodtag = "blood " + idx;

                // set and check both bodies, might already exist emitter in one of those bodies.
                bool existInA = SetBloodEmitterPropertiesInBody(pj.BodyA, strBloodtag, SizeFactor);
                bool existInB = SetBloodEmitterPropertiesInBody(pj.BodyB, strBloodtag, SizeFactor);

                // if exist then skip blood emitter creation for this joint.
                if (existInA || existInB)
                    continue;

                // TODO add only the one that closest to MainBody. but then will need walk graph that can work on non-broken joints.
                // or just simply add emitter on both bodies, we can delete one of them using tools,
                // when later this code detect at least 1 emitter available, it should prevent from creating new emitter.
                //must manually remove them.
                AddNewBloodEmitter(pj.BodyA, pj.LocalAnchorA, strBloodtag);
                AddNewBloodEmitter(pj.BodyB, pj.LocalAnchorB, strBloodtag);
            }
        }




        public void AdjustBloodEmittersToJoints(float scale)
        {
            int max = _pwdjoints.Count;
            for (int idx = 0; idx < max; idx++)
            {
                PoweredJoint pj = _pwdjoints[idx];
                // make unique name for this emitter, required if body have different emitters for different joints
                string strBloodtag = "blood " + idx;

                // set and check both bodies, might already exist emitter in one of those bodies.
                SetBloodEmitterPropertiesInBody(pj.BodyA, strBloodtag, scale);
                SetBloodEmitterPropertiesInBody(pj.BodyB, strBloodtag, scale);

            }
        }



        /// <summary>
        /// Set properties for blood emitter in Body. Emitter name must contains specific tag. 
        /// Return true if exist at least one matched emitter in Body.
        /// </summary>
        protected bool SetBloodEmitterPropertiesInBody(Body body, string tag, float scale)
        {
            bool exist = false;
            foreach (Emitter emitter in body.EmitterPoints)
            {
                BodyEmitter bodyEmitter = emitter as BodyEmitter;
                if (bodyEmitter == null || emitter.Name == null)
                    continue;
                // set prop for emitter
                if (bodyEmitter.Name.Equals(tag) == true)
                //if (bodyEmitter.Name.Contains(tag) == true)
                {
                    SetBloodEmitterProperties(bodyEmitter, BloodColor);
                    ApplySizeFactor(bodyEmitter, scale);
                    exist = true;
                }
            }

            return exist;
        }


        private void ClearAllBloodEmitters(string tag)
        {
            foreach (Body b in _bodies)
            {
                List<BodyEmitter> todelete = new List<BodyEmitter>();

                foreach (Emitter emitter in b.EmitterPoints)
                {
                    BodyEmitter bodyEmitter = emitter as BodyEmitter;
                    if (bodyEmitter == null || emitter.Name == null)
                        continue;

                    if (bodyEmitter.Name.Contains(tag) == true)
                    {
                        todelete.Add(bodyEmitter);
                    }
                }

                foreach (Emitter emitter in todelete)
                {
                    b.EmitterPoints.Remove(emitter);
                }
            }
        }


        //   static BodyColor _bloodColorDefault = new BodyColor(150, 255, 12, 255); //insect green blood
        static BodyColor _bloodColorDefault = new BodyColor(255, 0, 0, 255);

        public static void SetBloodEmitterProperties(BodyEmitter bodyEmitter, BodyColor bloodColor)
        {

            if (bodyEmitter == null)
                return;

            bodyEmitter.ProbabilityCollidable = 0.2f; // costly if very high, gets adjusted to zero if fps drops goes to zero
            bodyEmitter.Density = 40;//blood looks bigger than it is for visibility.  should be as dense as water.. so make actual density smaller .. better add stroke instead so it will "soak" mmore?           
            bodyEmitter.DragCoefficient = 3.1f;  // carried by wind fairly easily since not dense..  ( particle force will be clipped   if accel greater than wind speed         
                                                 //bodyEmitter.Mass   since mass is proportional to r * r.. big particles will affected by wind more anyways , can use density..
            bodyEmitter.Friction = 0.03f;    // slippery blood
            bodyEmitter.Frequency = 12;
            bodyEmitter.DeviationAngle = 0.5f;
            bodyEmitter.ParticleCountPerEmission = 3;

            bodyEmitter.EmissionForce = 2.5f;  // so that it wont tunnel on first emit

            bodyEmitter.CheckInsideOnCollision = true;  //in case it tunnels in somehow..

            bodyEmitter.SkipRayCollisionCheck = false;   //non collidable blood will stick on collide

            bodyEmitter.MagnitudeAspectOscillation = 0.6f;
            bodyEmitter.OscillationPeriod = 150; //msec
            bodyEmitter.OscillationPeriodDeviation = 0.5f;

            bodyEmitter.Color = (bloodColor == null) ? _bloodColorDefault : bloodColor;

            bodyEmitter.EdgeStrokeColor = BodyColor.Transparent;

            //  bodyEmitter.SlowFrameRateReductionFactor = 0.5f;  //half blood when fps low,  below 36..
            bodyEmitter.EdgeStrokeThickness = 0;
            bodyEmitter.AutoDeactivateAfterTime = 4f;

            bodyEmitter.CheckRayOnEmit = true;   // don't emit if right on something, will probably tunnel.
            bodyEmitter.ZIndex = -999;  // make blood not appear to interpenetrate ( TODO future.. markup as bloody.. attach to dress or something..) 

            if (bodyEmitter.Parent.PartType == PartType.Head)
            {
                bodyEmitter.Size = 0.012f;
                bodyEmitter.DeviationSize = 0.003f;  //TODO should be a percentage 
                bodyEmitter.EmissionForce = 0.6f;
                bodyEmitter.ProbabilityCollidable = 0.7f; // make it fall off face.. less blood pressure 
                bodyEmitter.Frequency = 5;
            }
            else
            {
                bodyEmitter.Size = 0.022f;
                bodyEmitter.DeviationSize = 0.02f;
            }

            // lots of force and count on neck
            if ((bodyEmitter.Parent.PartType & PartType.Neck) == PartType.Neck ||
                bodyEmitter.Parent.PartType == PartType.MainBody ||
                bodyEmitter.Parent.PartType == PartType.Thorax ||
                bodyEmitter.Parent.PartType == PartType.Abdomen)
            {
                bodyEmitter.Frequency += 8;
                bodyEmitter.ParticleCountPerEmission += 3;
                bodyEmitter.EmissionForce += 5;
                bodyEmitter.ProbabilityCollidable = 0.2f; // make it faster..but more blood
                bodyEmitter.DeviationAngle = 1.1f;
            }

            bodyEmitter.Info = BodyInfo.Liquid;
        }



        public static void ApplySizeFactor(BodyEmitter bodyEmitter, float sizeFactor)
        {
            //  bodyEmitter.DeviationSize *= SizeFactor; ;
            // bodyEmitter.Size  *= SizeFactor;
            bodyEmitter.EmissionForce *= sizeFactor;
            bodyEmitter.Frequency *= sizeFactor;
            bodyEmitter.Size *= sizeFactor;
            bodyEmitter.DeviationSize *= sizeFactor;
        }


        public BodyEmitter AddNewBloodEmitter(Body body, Vector2 refPoint, string strBloodtag)
        {
            BodyEmitter bodyEmitter = new BodyEmitter(body, refPoint, World.Instance);
            bodyEmitter.Name = strBloodtag;
            bodyEmitter.Direction = new Vector2(0.0f, -0.1f);  //TODO always up?  should be away from bone.   for now its been manally set an saved in file..
            SetBloodEmitterProperties(bodyEmitter, BloodColor); //TODO move to skin in case striking bullet, of offset around it..
            ApplySizeFactor(bodyEmitter, SizeFactor);
            return bodyEmitter;
        }

        public static Emitter FindEmitterByName(Body body, string name)
        {
            if (body == null) return null;
            
            return body.EmitterPoints.FirstOrDefault(x => x.Name == name);
        }


        /*  TODO for now we just rebuild each update on AABB query..   NOTE the sensor should just move around and be observed, thats what its for
         /*  a proper implementation would be faster than another AABB quiery since the tree requires those passes anyways.
        //if this is faster we can use hashset diff to do this.     circle sensor is slow for many spirits.
        private void RemoveBodyFromSensor(Body removedBody)
        {
            if (BodiesInSensor.Contains(removedBody) == true)
            {
                BodiesInSensor.Remove(removedBody);

                // if removed body is MainBody of other spirit, also remove that spirit
                Spirit othersp;
                if (Level.MapBodyToSpirits.TryGetValue(removedBody, out othersp) == true &&
                    othersp != this)
                {
                    SpiritsInSensor.Remove(othersp);
                }

                // if body contains one or more sharp point, also remove those points
                foreach (SharpPoint sh in removedBody.SharpPoints)
                {
                    SharpPointsInSensor.Remove(sh);
                }

                // if body contains one or more attach point, also remove those
                foreach (AttachPoint ap in removedBody.AttachPoints)
                {
                    AttachPointsInSensor.Remove(ap);
                }
            }
        }
         * */

#endregion



#region Event Listener

        [OnDeserializing]
        public void OnDeserializing(StreamingContext sc)
        {
            _ondeserializing = true;
            _jointsoftness = float.NaN;  //special initialization, means undefined..TODO why are bias and the others not done like this.  TODO investigate and remove this..

        }

        /// <summary>
        /// Allows client or plugin to save its data as a string in UserData
        /// </summary>
        public Action Serializing;

        [OnSerializing]
        public void OnSerializing(StreamingContext sc)
        {
            _onserializing = true;

            Serializing?.Invoke();
        }

        [OnSerialized]
        public void OnSerialized(StreamingContext sc)
        {
            _onserializing = false;

        }

        private void behaviors_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // when behaviors content change, invalidate keymap cache
            _mapGamekeyToBehaviors.Clear();   // TODO: changing of behavior key should also invalidate keymap cache           

        }


        private void RefreshSensedObjectLists()
        {
            AABB aabb;
            // takes 10 more ms for so to expand this to include a new ynrd.. should make it smaller and query nearest spirit some other way.
            //TODO OPTIMIZE ..  try query spirits directly from level , make detect area smaller.
            //could use aabb = AABB.Expand(2.0f, 2.0f);  if level spirit count is < 10, we can just linear search it..
            //we do care about fast moving sharps are away tho.. for autoblock..

            //TODO maybe expand if holding  a ShootsProjectile.
            aabb.LowerBound = MainBody.WorldCenter + new Vector2(-BodyProximityDetectWidth / 2, -BodyProximityDetectHeight / 2);
            aabb.UpperBound = MainBody.WorldCenter + new Vector2(+BodyProximityDetectWidth / 2, +BodyProximityDetectHeight / 2);

            FarseerPhysics.Common.HashSet<Spirit> spirits;
            FarseerPhysics.Common.HashSet<Body> bodySet = GetOtherBodiesAndSpiritsInAABB(aabb, out spirits);

            List<Spirit> farSpirits = GetSpecialSensedSpirits();
            farSpirits.ForEach(x => spirits.CheckAdd(x));

            RefreshSensedObjectLists(bodySet, spirits);
            //to detect attachpoint in static ground?..  for now these are not supported, have to add a dynamic grip and anchor it.
            //RefreshSensedObjectLists(DetectBodiesInAABB(aabb, true, GroupId)); 
        }



        private void RefreshSensedObjectLists(FarseerPhysics.Common.HashSet<Body> newBodies, IEnumerable<Spirit> spirits)
        {
            //TODO try compare existing bodies and new bodies.. see if its any faster, i doubt it.
            // newBodies.Except<Body>  ..etc.  then call AddBodyFromSensor, and RemoveBodyFromSensor after optimizing those..

            BodiesInSensor = newBodies;

            SpiritsInSensor.Clear();
            SpiritsInSensor.AddRange(spirits);

            SharpPointsInSensor.Clear();
            AttachPointsInSensor.Clear();

            foreach (Body otherBody in BodiesInSensor)
            {
                // if body contains one or more sharp point, add those points
                foreach (SharpPoint sh in otherBody.SharpPoints)
                {
                    SharpPointsInSensor.Add(sh);
                }
                // if body contains one or more attach point, add those points

                foreach (AttachPoint ap in otherBody.AttachPoints)
                {
                    AttachPointsInSensor.Add(ap);
                }
            }
        }


        //TODO allow to delegate to plugin, and or to put tag for level editor to save
        //  now used for autoaim at rock above .  TODO def remove levelnumber check
        //plguin can put items with sharps in special collection on level load, generally to shoot at traps or dangerous things wiht autoaim
        List<Spirit> GetSpecialSensedSpirits()
        {
            List<Spirit> spirits = new List<Spirit>();
            if (Level.LevelDepth == 2 && Level.LevelNumber == 2)
            {

                foreach (Spirit spirit in Level.GetSpiritEntities())
                {
                    if (spirit.Bodies.Count == 2
                      && spirit.Bodies.Any(x => x.PartType == PartType.Stalagtite))
                        spirits.Add(spirit);
                }
            }
            return spirits;
        }
        //Dh: DWI review  I  broke this functino out, but commented it out
        // its takes long to update the sensor if there are many spirit, and there is linear time Contains checks here.
        // replacing using AABB query and body hash set . and   refresh  the lists each update .  
        //Since   attach point, sharp point, etc  .. all dont have to work at "bullet speed" , i think this is better.
        // I am repurposing  the Circle sensor as a bullet sensor to wake up collision of intertal parts when needed.
        //tested better  performance with many spirits on screen like swarm of bees and, no different in weapon block test..
        // 
        /*
        //  we could use difference in  HashSet<Body> from previous to current to update this
         // for now , just refresh the whole list and check performance in play.
        private void AddBodyFromSensor(Body otherBody)
        {
            // add body if it's not yet in our sensor
            if (_bodies.Contains(otherBody) == false &&    /// CODE REVIEW linear search, slow.. hash set is better.
                BodiesInSensor.Contains(otherBody) == false)//CODE REVIEW linear search, slow.. hash set is better . usually not needed..
            {
                BodiesInSensor.Add(otherBody);

                // if body is MainBody of other spirit, add that spirit
                Spirit othersp;

                if (Level != null)
                {
                    if (Level.MapBodyToSpirits.TryGetValue(otherBody, out othersp) == true &&
                        othersp != this && SpiritsInSensor.Contains(othersp) == false)
                    {
                        SpiritsInSensor.Add(othersp);
                    }
                }

                // if body contains one or more sharp point, add those points
                foreach (SharpPoint sh in otherBody.SharpPoints)
                {
                    if (SharpPointsInSensor.Contains(sh) == false)
                    {
                        SharpPointsInSensor.Add(sh);
                    }
                }

                // if body contains one or more attach point, add those points
                foreach (AttachPoint ap in otherBody.AttachPoints)
                {
                    if (AttachPointsInSensor.Contains(ap) == false)
                    {
                        AttachPointsInSensor.Add(ap);
                    }
                }
            }
        }
         */


#if CIRCLESENSOR
        //  dh the circle sensor does not collide with a Static body like ground, cant trace why.  So not using this..
        //    doing aabb query instead.


        /// <summary>
        /// When sensor collide with a Body, update list of collided Body, Spirit, and SharpPoint.
        /// </summary>
        private bool OnSensorCollision(Fixture fixtureA, Fixture fixtureB, Contact ctlist)
        {
            // fixtureA is always our Sensor

            Body otherBody = fixtureB.Body;

            //Dh: DWI review  I moved this code to update nearby lists.
            // its takes long to update the sensor if there are many spirits, and there are linear time Contains checks here.
            // replacing using AABB query and body hash set . and   refresh  the lists each update .  
            //Since   attach point, sharp point, etc  .. all dont have to work at "bullet speed" , i think this is better.

            // Will repurpose  the Circle sensor as a bullet sensor around spirits to wake up coillsion of intertal parts when needed.    
            //   

            //  dh the circle sensor does not collide with a Static body like ground.  So not using this..
            //#if CIRCLESENSOR
            //  AddBodyFromSensor(otherBody);
            if (!BodySetInCircleSensor.Contains(otherBody))
            {
                BodySetInCircleSensor.Add(otherBody);
            }
            //#endif

            foreach (Body body in Bodies)
            {
                //  if ( body.PartType != PartType.Eye)
                //     body.IsNotCollideable = false;
            }

            return true;
        }


        /// <summary>
        /// When a Body no longer collide with sensor, update list of collided Body, 
        /// and related Spirit and SharpPoint.
        /// </summary>
        private void OnSensorSeparation(Fixture fixtureA, Fixture fixtureB)
        {
            //  RemoveBodyFromSensor(fixtureB.Body);
            // fixtureA is always our Sensor   
            Body otherBody = fixtureB.Body;


#region optimisationexperiment  //try to have many moving guys like 20.
#if OPTIMIZENESTEDAABBEXPERIMENT //.. not collidable creatures walking aroudn with just feet collidabel .. unless nothing is nearby
            //  if (BodySetInCircleSensor.Contains(otherBody))
            BodySetInCircleSensor.Remove(otherBody);
            if (BodySetInCircleSensor.Count == 0)  //static like ground.. bodies dont appear in sensor   only collide feet..
            {
                if ( !Mind.IsFallingOrGettingUp())
            {
                foreach (Body body in Bodies)
                {
                    //   if (body.PartType != PartType.MainBody)  
                    //what if bullet thown at use.. i think spirit  needs a bullet circle transparent  body that move like sensor with creature to reenable collide..
                    //could  use this for detection as well..
                    if (body.PartType != PartType.LeftFoot && body.PartType != PartType.RightFoot)
                    {
                        //     body.IsNotCollideable = true;
                    }
                }
            }
                    }
            }
#endif
#endregion
        }
#endif

        //todo move to a plugin base for living creatures, maybe generalize, just make nourishment based on mass
        void SetPropertiesToMakeEdible(Body body, bool includeEyes)
        {
            //todo FUTURE check size?  only eat particaly grown ones.
            if ((body.PartType & PartType.Toe) != 0)
            {
                body.Nourishment = 25;
            }
            else
                if ((body.PartType & PartType.Hand) != 0)
            {
                body.Nourishment = 35;
                //   body.PartType = PartType.Food; // this make AI look for it.. 
            }
            else
                    if ((body.PartType & PartType.Foot) != 0)
            {
                body.Nourishment = 35;
                //   body.PartType = PartType.Food; //TODO remove this..
            }
            else
                        if (includeEyes && (body.PartType & PartType.Eye) != 0)
            {
                body.Nourishment = 22;  //don't change to   PartType.Food;  .   then eyes wont dim.. //todo later.. add food flag instead.  but since its not a body part anymore this is o
                body.CollisionGroup = 0;
            }

            else return;  // cant be eaten..

            body.Info |= BodyInfo.Food; // this should make AI look for it..TODO 

            body.Color.R = 155;  //make it "meat color" so body will glow dark red on eat..
            body.Color.G = 2;
            body.Color.B = 4;
        }

        //this sets the normal away from CM.. that is valid for most items.

        void AddAttachGripOnBrokenBody(Body body, Vector2 jointPos)
        {

            if ((body.PartType & PartType.Eye) != 0)///TODO  use bodyinfo food..  
            {
                PutTwoAttachPointsOnSmallRoundObject(body, 0.6f);  // could do scale factor.. but thats another matter, dont know which hand will grab it.
                                                                   //put a delay or will be eaten as it bounces off mouth..
                Delay eyeFall = new Delay(this, "eyefall" + body.GetHashCode().ToString(), GROW_DELAY / 2f);  //otherwise spirit will be replaced before this is called
                eyeFall.UserData = body;
                eyeFall.OnEndEffect = OnEyeSevered;
            }
            else if (body.PartType != PartType.MainBody && body.PartType != PartType.Head) //cant grab head or main body , too big
                                                                                           //for limbs.. put attach pt at end of limb
            {
                Vector2 locatAttachPt = body.GetLocalPoint(jointPos);
                AttachPoint atc = new AttachPoint(body, locatAttachPt);

                //point attach point handle away from center..
                Vector2 handleDirection = (atc.WorldPosition - body.WorldCenter);
                handleDirection = body.GetLocalVector(handleDirection);
                handleDirection.Normalize();
                atc.Direction = handleDirection;
                body.AttachPoints.Add(atc);
            }

        }

        //todo move out of spirt
        void PutTwoAttachPointsOnSmallRoundObject(Body body, float maxWidth)
        {

            for (int i = 0; i < 2; i++)
            {
                Vector2 worldPos = body.WorldCenter;

                float min = Math.Min(body.AABB.Width, body.AABB.Height);

                if (min > maxWidth)// object too big
                    return;

                if (i == 0)  //TODO .. maybe better to put in N places around perimeter?
                    worldPos.X += min;
                else
                    worldPos.X -= min;

                //TODO fine edge .. or fail if  body.AABB.Width 

                Vector2 locatAttachPt = body.GetLocalPoint(worldPos);
                AttachPoint atc = new AttachPoint(body, locatAttachPt);

                //point attach point handle away from center..
                Vector2 handleDirection = (atc.WorldPosition - body.WorldCenter);
                handleDirection = body.GetLocalVector(handleDirection);
                handleDirection.Normalize();
                atc.Direction = handleDirection;

                body.AttachPoints.Add(atc);
            }

        }


        void OnEyeSevered(Effect eyeBreakDelay)
        {
            SetPropertiesToMakeEdible(eyeBreakDelay.UserData as Body, true);//allow eyes to be eaten
        }

        void AddAttachGripOnBrokenParts(Joint joint)
        {
            AddAttachGripOnBrokenBody(joint.BodyB, joint.WorldAnchorB);
            AddAttachGripOnBrokenBody(joint.BodyA, joint.WorldAnchorA);
        }


        /// <summary>
        /// Handle when any joint on spirit is about to break.   If returns false will prevent breaking, but this is not 
        /// currently used.
        /// </summary>
        private bool OnJointBreaking(Joint joint)
        {

            //      return true;   // was to see if this affects the intermittent collide catergory issue on non-collide connected object  broken then pass through each other
            //TODO FUTURE .. clean and reorganize this huge method.. its used for all spirits, and much ynrd only stuff in here
     //       return false;
            // mark as broken
            // 

            joint.IsBroken = true;

            //in case eyes.. make sure they bounce on ground.  eyes are notcollidable when connected on face
           joint.BodyA.IsNotCollideable = false;
           joint.BodyB.IsNotCollideable = false;


            List<Emitter> emitters = new List<Emitter>(joint.BodyA.EmitterPoints);
            emitters.AddRange(joint.BodyB.EmitterPoints);

            // trigger bleeding. to know which emitter used for bleeding, 
            // just get the closest emitter to broken joint.

            CheckToBleed(joint, emitters);

            World physics = World.Instance;
            IEnumerable<Body> removedBodies;

            if (physics.JointList.Contains(joint))
            {
                physics.RemoveJoint(joint);// API checks already if removed twice.
            }

            // if this joint is this spirit's joint, then rebuild the graph
            if (Joints.Contains(joint as PoweredJoint) || FixedJoints.Contains(joint))//TODO wont it  always be in this?  check on break griped item out of hand..
            {
                RebuildSpiritInternalGraph(false, null, out removedBodies);

                bool headSevered = removedBodies.Contains(Head);
                bool simplfyBrokenShapes = IsMinded;
                //|| PluginName.Contains("Balloon")//TODO try  replace on baloon broken 
                //)||PluginName.Contains("Rope");   ///for now just test on these.   dont do rope yet.. unless on or two pieces.. becuase we might be using hald rope

                //	);

                foreach (Body b in removedBodies)
                {
                    //  should  bullet the severed piece.. in case thrown piece of dead body.. 
                    //BUT bullets are slow to pile.cuase CCD issues.. on piles of dead bodies
                    //for now dont .      only  issue  i imagine is throw eye at non bullet wall .. will tunnel.  spirit extremeities are bullet should not be a problem..
                    //test hit head with severed leg tho...TODO  also  consider to set bullet on any held or thrown object?
                    b.IsBullet = false;
                    b.CollisionGroup = 0;
                    b.Info &= ~BodyInfo.PlayerCharacter; // remove PC flag, its now just a piece of junk


                    if (IsMinded)
                    {
                        DetachAllHeldItemsFromBody(b);  //again CCD issues..  hand on death grip w/ sword is interesting but can get stuck and cause issues.    
                        SetPropertiesToMakeEdible(b, headSevered);// allow to eat eyes from severed head.. otherwise there is a delay or eye will be eaten as it falls.

                        if (headSevered && b.PartType == PartType.Head)
                        {
                            b.Density *= 2.5f;  //head is extra light for balance.. lets make it heavier for realism.. lots of skull in there.
                        }

                    }
                    //  added after body verts simplification on break whole arm off.. seems collide connect man not always work .. seen shaking
                    SoftenAttachedJoints(b);

#if REDUCEVERTSONBONEBREAKING
                    if (simplfyBrokenShapes)
                    {
                        SimplifyShapesBrokenOff(b);
                    }
#endif

                    if (BodyLeavingSystem != null)
                    {
                        BodyLeavingSystem(b);
                    }
                }
            }

            // update mass. AABB and CM always updated on Update().  
            UpdateTotalAndCenterMass();
            // set energy level to 0 if head was severed

        //    if (IsMinded)
        //    {
        ////        CheckIfHeadSevered();
              //  SetLimbPartCache(true);
          //      UpdateHeldItemSide();
        //    }

            //TODO check the width on joints.. airship parts should not do this..
            //or add a flag to prevent it.   ( Info  not grabable on break.. either on joint or on body..)
            // airship with big parts should not do this.
            AddAttachGripOnBrokenParts(joint);

            if (JointBreaking != null)
            {
                JointBreaking(joint);
            }

            // set energy level to 0 if head was severed

            if (IsMinded)
            {
                
                CheckIfHeadSevered();
                //  SetLimbPartCache(true);
                UpdateHeldItemSide();
            }





            if (IsMinded)
            {
                CollectEyes();
                CollectHeadBodies();
                CheckRegrowOnJointBreaking();
            }

#if TEST_REGROW_ZERO_FORCE
            // test regrow under zero gravity
            World.Instance.Gravity = Vector2.Zero;
#endif
            // always return true, except spirit want to prevent its joint from break
            return true;
        }

        private static void SoftenAttachedJoints(Body b)
        {
            JointEdge je = b.JointList;
            while (je != null)
            {
                JointEdge je0 = je;
                je = je.Next;

                Joint joint = je0.Joint;
                joint.CollideConnected = false;
                if (joint is PoweredJoint)
                {
                    //do all this in case other code tries to ower up the joint.. is still in spirit joints.
                    SetJointToDeadState(joint as PoweredJoint);
                }
            }
        }

        //NOTE rigormortis is not necessary if not stable.. its just unnecessary realism
        // barely move .. take shape  of where it is but dont fight to stay stiff..  goal is to get it to sleep and not shake   
        public static void SetJointToDeadState(PoweredJoint pj)
        {
            pj.DampingFactor = 3;
            pj.Softness = 0.97f;
            pj.HasPower = false;
            pj.BiasFactor = 0;
        }

        // reduce removed bodies vertices. must use delayed entity process. 
        // we're called from joint breaking event, which is not safe area to modify any body/fixture
        private void SimplifyShapesBrokenOff(Body b)
        {
            if (    //  method in body directly  verts and regens fixtures and AABB.. works only most normal bones
              (b.PartType & PartType.Hand) == 0   //this should  ok   but need a small minium distance
             && (b.PartType & PartType.Head) == 0
             && (b.PartType & PartType.MainBody) == 0
             && (b.PartType & PartType.Eye) == 0)
            {
                Level.AddBodyToHaveCollisionShapeSimplifiedThreadSafe(b);  //this should not affect view.  TODO check that
            }
        }


        private void CheckToBleed(Joint joint, List<Emitter> emitters)
        {

            Emitter bloodEmitter = GetClosestEmitter(joint.WorldAnchorA, emitters, "blood");
            if (bloodEmitter != null)
            {
                bloodEmitter.Active = true;
            }

        }

        public void DetachAllHeldItemsFromBody(Body b)
        {
            List<AttachPoint> atcs = new List<AttachPoint>(b.AttachPoints); //avoid enum changed exceptoin
            atcs.ForEach(x => x.Detach());
        }

        public void CollectEyes()
        {
            _eyes = new List<Body>();
            _eyes.AddRange(GetAllBodiesWithPartFlags(PartType.Eye, false, false));
        }


        public IEnumerable<Body> HeadBodies
        {
            get
            {
                if (_headParts == null)
                {
                    CollectHeadBodies();
                }
                return _headParts;
            }
        }

        protected void CollectHeadBodies()
        {
            _headParts = new List<Body>();

            CollectEyes();
            _headParts.AddRange(_eyes);

            if (Head != null)
            {
                _headParts.Add(Head);
            }

            _lowerJaw = null;  // force recollect

            if (LowerJaw != null)
            {
                _headParts.Add(LowerJaw);
            }
        }


        public IEnumerable<Body> HeadAndNeckBodies
        {
            get
            {
                if (_headAndNeckParts == null)
                {
                    _headAndNeckParts = new List<Body>(HeadBodies);
                    _headAndNeckParts.AddRange(GetAllBodiesWithPartFlags(PartType.Neck));
                }
                return _headAndNeckParts;
            }
        }


        public int EyeCount
        {
            get
            {
                if (_eyes == null)
                    return 0;

                return _eyes.Count();
            }
        }

        private void OnJointPoisoned(PoweredJoint pjoint, float poisonLevel)
        {
            if (Poisoned != null)
            {
                Poisoned(poisonLevel);
            }
        }

        //TODO FUTURE templatize getClosest. we using it everywhere..  pickup , etc.
        public Emitter GetClosestEmitter(Vector2 pos, IEnumerable<Emitter> emitters, string tag)
        {

            float closestDist2 = float.MaxValue;

            Emitter closestEm = null;
            foreach (Emitter em in emitters)
            {
                float dist2 = (pos - em.WorldPosition).LengthSquared();
                if (dist2 < closestDist2 && em.Name != null && em.Name.Contains(tag))
                {
                    closestDist2 = dist2;
                    closestEm = em;
                }
            }
            return closestEm;
        }

        /// <summary>
        /// Handle when any attach point is detached.
        /// </summary>
        private void OnAttachPointDetached(AttachPoint sender, AttachPoint pair)
        {

            //fix AI pickup again right after knock sword from hand.. add a little delay
            // but need to kknow if joint event came from breaking , collided body is null in both case , need 
            //phyisics modification..
            //Delay breakOutofHandDelay = new Delay( Parent, grabDelay
            //then dont attach if this Delay is present in Delays..
            UpdateHeldItemsLists(pair, false);
            UpdateHeldItemSide();
        }


#endregion



#region Helper

        public Keyframe FindKeyframeByTime(double time)
        {
            Keyframe result = null;

            if (ActiveBehavior.Keyframes.Count > 0)
            {
                foreach (Keyframe keyframe in ActiveBehavior.Keyframes)
                {
                    if (keyframe.Time == time)
                    {
                        result = keyframe;
                        break;
                    }
                }
            }

            return result;
        }

        /*
                //  http://www.mvps.org/directx/articles/catmull/
                //TOd consolidate

        /// <summary>
        /// a -d control pts.    between a nd b... and c and d... use linear interp..  or add ui to make poits here.. our use linear intertp to create them...for starter.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="x"></param>
        /// <returns></returns>
                public static double CutMullInterp(
                double a, double b,
                double c, double d,
                double x)
                {
                    double xsq = x * x;
                    double xcu = xsq * x;

                    double minV = Min(a, Min(b, Min(c, d)));
                    double maxV = Max(a, Max(b, Max(c, d)));

                    double t
                            = a * (0.0 - 0.5 * x + 1.0 * xsq - 0.5 * xcu) //uses the double prec
                            + b * (1.0 + 0.0 * x - 2.5 * xsq + 1.5 * xcu)
                            + c * (0.0 + 0.5 * x + 2.0 * xsq - 1.5 * xcu)
                            + d * (0.0 + 0.0 * x - 0.5 * xsq + 0.5 * xcu);

                    return Min(Max(t, minV), maxV);
                }*/


        /// <summary>
        /// Update the Spirits Target Angles based on interpolation between KeyFrames,  for 1 cycle
        /// Also applies all the Filters in the collection to adjust the target angles, before setting them to the joints
        /// </summary>
        /// <param name="invScale">the invert of animation speed scale</param>
        private void SetTargetAnglesFromKeyFramesAndFilters(double invScale)
        {

            if (invScale == 0)
            {
                invScale = 1;
            }

            // If the keys is default then set it to the highest keyframe time

            //TODO can we remove _keyStart, _keyEnd.. just useActiveBehavior.Keyframes.Count an 0
            // have a differnt funct for transition..

            _keyEndIndex = ActiveBehavior.Keyframes.Count - 1;

            Keyframe first;
            Keyframe second;
            bool found = FindKeyframesInterval(_currentTime, _keyStartIndex, _keyEndIndex, out first, out second, invScale);


            if (found)
            {
                for (int i = 0; i < _pwdjoints.Count; i++)
                {
                    if (_pwdjoints[i].IsBroken)
                        continue;

                    float angle = _pwdjoints[i].TargetAngle;



                    // If there's newly added joint where the angle not exist in the first keyframe already recorded, just skip the interpolation
                    if (i < first.Angles.Count && i < second.Angles.Count)
                    {


                        //todo SMOOTH OUT ROBOTIC MOVEMENT with catmulrom
                        //   http://glasnost.itcarlow.ie/~powerk/technology/xna/Catmulroml.html
                        //    Numerics.Interpolation.CubicSpline might  , later if slow,  should record and cache trackpoints, samples if needed.. 

                        if (_bIsInterpolate)
                        {
                            // if (_keyStartIndex != 0) && _keyEndIndex != _keyEndIndex)
                            //  {
                            //       angle = CutMullInterp((float)(first.Time, )
                            //TODO try with the Math libs?                  
                            //    }
                            //TODO   pass in closed  to the method... deal with cachingn and let use tweak
                            //put a spline draw tool also
                            //TODO put a tiny display..
                            angle = Interpolate((float)_currentTime, (float)(first.Time * invScale), (float)(second.Time * invScale),
                                               (float)first.Angles[i],
                                              (float)second.Angles[i]);

                            //TODO try this so that joinst can be tightened with more iterations                  
                            //     Numerics.Interpolation.CubicSpline spline = (MathNet.Numerics.Interpolation.CubicSpline)MathNet.Numerics.Interpolate.CubicSplineRobust
                            //    (new double[] { _currentTime, first.Time* invScale,
                            //   second.Time* invScale}, new double[] { first.Angles[i], (double)second.Angles[i] });
                        }
                        else
                        {
                            angle = (float)first.Angles[i];
                        }

                    }

                    float totalAngle = angle;
                    // Iterate every filter
                    // This is iterative angle updates, please pay attention to the 
                    // filter sequence added into Filters collection
                    // Limit Filter should be at the last index to take a good effect


                    //target overrided offsets.. yuck

                    //limit is first.  should be reversed.

                    foreach (IKeyframeFilter filter in Filters)
                    {
                        // Each filter should update the given reference angle
                        filter.Update(i, ref totalAngle);
                    }

                    // Set the angle for this joint 
                    _pwdjoints[i].TargetAngle = totalAngle;

                }
            }
        }


        /*no good..
        private void ApplyFiltersToCurrentTargetAngles()
        {

         
                for (int i = 0; i < _pwdjoints.Count; i++)
                {
                    if (_pwdjoints[i].IsBroken)
                        continue;

                    float totalAngle = _pwdjoints[i].TargetAngle;


                    foreach (IKeyframeFilter filter in Filters)
                    {
                        // Each filter should update the given reference angle
                        filter.Update(i, ref totalAngle);
                    }

                    // Set the angle for this joint 
                    _pwdjoints[i].TargetAngle = totalAngle;

                }
            
        }*/

        /// <summary>
        /// Force to update Filters to the pose, this is  useful for 
        ///  for when effects with target angles are used, yet creature is not animating, just standing.  Should be called from PostUpdateAnimation
        /// </summary>
        public void ForceFilterUpdate()
        {
            if (!IsAnimating)
            {  //TODO check why does this go over keyframes 
               // Debug.WriteLine("applyfilters"); 
               //NOTE targetEx filters override any offsets..
               SetTargetAnglesFromKeyFramesAndFilters(ActiveBehavior.TimeDilateFactor);
               // ApplyFiltersToCurrentTargetAngles();
            }
        }

        ///TODO this gets called  many times, but there are very few kfs, usually  < 12
        ///CODE REVIEW the  name are not clear keyTime is in sec i believe , but , firstKeyTime  ( is one this a keyframe index_
        ///TODO The list is sorted by Time,  is a comparer.  Binary search can be used


        /// <summary>
        /// Finding a start and end keyframe given a interpolated time with certain invert scale
        /// </summary>
        /// <param name="keyTime">time in sec of currenttime, function will give keyframes on either side</param>
        /// <param name="firstIndex">first index of keyframes to start searching from</param>
        /// <param name="lastIndex">last  index of keyframes to start searching from</param>
        /// <param name="firstKF">index of nearest KF earlier </param>
        /// <param name="secondKF">index of nearest KF earlier afteer></param>
        /// <param name="invScale">inverse scale, the time dilate total factor being used to speed up or slow </param>
        /// <returns></returns>
        public bool FindKeyframesInterval(double keyTime, int firstIndex, int lastIndex,
                                          out Keyframe firstKF, out Keyframe secondKF, double invScale)
        {
            firstKF = null;
            secondKF = null;

            // We need at least 2 keyframes to interpolate
            if (ActiveBehavior.Keyframes.Count < 2)
                return false;

            lastIndex = Math.Max(0, lastIndex);
            firstIndex = Math.Max(0, firstIndex);

            // If the current frame is out of keyframes range, then bug out
            if ((keyTime > ActiveBehavior[lastIndex].Time * invScale ||
                (keyTime < ActiveBehavior[firstIndex].Time * invScale)))
                return false;


            int keyStart = -1;


            ///TODO CODE REVIEW The list is sorted by Time somewhere, there is a comparer already  Binary search can be used
            /// consider implementing like a SortedCollection that is objectable
            //Or HashSet keyed by time..
            // TODO CODE REVIEW  could improve this using a  search algorithm.  NOTE its so few keyframes, never mind..

            // Linear Scan to find the interval between 2 keyframes
            for (int i = firstIndex; i <= lastIndex; i++)
            {
                if (keyTime >= ActiveBehavior[i].Time * invScale)
                {
                    keyStart = i;
                }
            }

            if (keyStart < 0)
                return false;

            if (keyStart == lastIndex)
                keyStart = Math.Max(0, keyStart - 1);

            int keyEnd = keyStart + 1;

            firstKF = ActiveBehavior[keyStart];
            secondKF = ActiveBehavior[keyEnd];

            return true;
        }





        /// <summary>
        ///Linear Interpolate between  the angles between 2 keyframes.  TODO maybe  try adding option,  would be smoother to use  Hermite, cubic spline interpolate.  But with lower bias like 0.3 which is used to avoid oscillation or unrelistic strength on biped, don even see a difference with no interpolitoin ( sawtooth shape in time) .  maybe be useful for robot or strong machine like tank.  for hermite  need the tangent at the time.. avoid sawtooth shapes in time wiht high bias this can we seen
        /// </summary>
        /// <param name="targetTime">Current key time</param>
        /// <param name="time1">First time</param>
        /// <param name="time2">Second time</param>
        /// <param name="angle1">First angle</param>
        /// <param name="angle2">Second angle</param>
        /// <returns>Interpolated angle</returns>
        public static float Interpolate(float targetTime, float time1, float time2, float angle1, float angle2)
        {
            float da = angle2 - angle1;
            float dt = time2 - time1;
            return (targetTime - time1) * (da / dt) + angle1;
        }


        /// <summary>
        /// Add event handler for all current joints.
        /// </summary>
        private void AddJointEventHandlers()
        {
            foreach (Joint joint in GetAllJoints())
            {
                AddJointEventHandler(joint);
            }
        }

        public IEnumerable<Joint> GetAllJoints()
        {
            List<Joint> joints = new List<Joint>(Joints.OfType<Joint>());
            joints.AddRange(FixedJoints);
            return joints;
        }

        /// <summary>
        /// Add event handler when new joint is added into spirit.
        /// </summary>
        private void AddJointEventHandler(Joint joint)
        {
            joint.Breaking -= OnJointBreaking;
            joint.Breaking += OnJointBreaking;

            PoweredJoint pj = joint as PoweredJoint;

            if (pj != null)
            {
                pj.PoisonInjected -= OnJointPoisoned;
                pj.PoisonInjected += OnJointPoisoned;
            }
        }


        /// <summary>
        /// Remove event handler for all current joints.
        /// </summary>
        private void RemoveJointEventHandlers()
        {
            foreach (Joint joint in GetAllJoints())
            {
                RemoveJointEventHandler(joint);
            }
        }


        /// <summary>
        /// Add event handler when new joint is added into spirit.
        /// </summary>
        private void RemoveJointEventHandler(Joint joint)
        {
            joint.Breaking -= OnJointBreaking;

            PoweredJoint pj = joint as PoweredJoint;
            if (pj != null)
            {
                pj.PoisonInjected -= OnJointPoisoned;

            }
        }


#endregion




#if STRENGTHENARMSONSTRETCH
        public void SetArmPartsDensity(bool left, float density)
         {
           IEnumerable<Body> parts = left ? _leftArmParts : _rightArmParts;
           parts.ForEach(x => x.Density = density);
        }
#endif

    
        private float _updateCounter = 0;
        private Vector2 _posA = Vector2.Zero;

        private float _averageSpeedX = 0;
        private float _averageSpeedSq = 0;

        private float _averageSpeed = 0;


        private float _energyLevelA = 0;
        private float _averagePowerConsumption = 0;


        /// <summary>
        /// updated every AveragingInterval frames..
        /// </summary>
        public double AverageMainBodyHorizontalSpeed
        {
            get { return _averageSpeedX; }
        }

        public double AverageMainBodySpeedSq
        {
            get { return _averageSpeedSq; }
        }


        /// <summary>
        /// Linear Vel of Main body, averaged over and updated every AveragingInterval frames..
        /// </summary>
        public double AverageMainBodySpeed
        {
            get { return _averageSpeed; }
        }



        /// <summary>
        /// Speed of Cm.  this is using the speed of all the bodies, so no need to average it.
        /// </summary>
        public double WorldCMSpeed
        {
            get { return WorldCMLinearVelocity.Length(); }
        }

        public double WorldCMSpeedSq
        {
            get { return WorldCMLinearVelocity.LengthSquared(); }
        }


        /// <summary>
        /// how much power is being used.. in Joules   , uses AveragingInterval
        /// </summary>
        public double AveragePowerConsumption
        {
            get { return _averagePowerConsumption; }
        }


        /// <summary>
        /// AveragingInterval in sec, default is 2 sec , for walking optimizing or speedometer..
        /// </summary>
        public float AveragingInterval { get; set; }

        /// <summary>
        /// Average speed, uses the distance covered/ time..
        /// </summary>
        /// <param name="dt"></param>
        public void UpdateAverageMainBodySpeedAndPower(double dt)
        {

            //Try average speed..  tried CM speed , about the same.. still has inssue in playh
            _updateCounter += (float)dt;

            if (_updateCounter >= AveragingInterval)
            {
                //speed
                _averageSpeedX = Math.Abs((MainBody.WorldCenter.X - _posA.X) / AveragingInterval);//for walking optimizing.

                _averageSpeedSq = (MainBody.WorldCenter - _posA).LengthSquared(); //todo REMOVE THIS   TODO THAT .. UPDATG ETHE USEING CONSIDERING AVERGININTERVAL.. ERROR


                _averageSpeed = ((MainBody.WorldCenter - _posA).Length()) / AveragingInterval;
                _posA = MainBody.WorldCenter;

                //power use per interval
                //TODO should / AveragingInterval but only if that might change AveragingInterval
                _averagePowerConsumption = _energyLevelA - EnergyLevel;
                _energyLevelA = EnergyLevel;

                if (Level.ActiveSpirit == this)
                {

                    if (AverageMainBodySpeedSq > 0.0001)
                    {
                        //  Debug.WriteLine("average horiz speed m/s" + AverageMainBodyHorizontalSpeed);
                        //Debug.WriteLine("average horiz speed mph" +  AverageMainBodyHorizontalSpeed/ MathHelper.KmhToMs * KmfToMph) ;
                        //Debug.WriteLine("average main body sq" + AverageMainBodySpeedSq);
                        if (OnAvgSpeedXUpdate != null)
                        {
                            OnAvgSpeedXUpdate(_averageSpeedX);
                        }

                        if (OnAvgSpeedUpdate != null)
                        {
                            OnAvgSpeedUpdate((float)AverageMainBodySpeed);
                        }

                    }
                }
                _updateCounter = 0;
            }
        }

        public void ResetMainBodyAveSpeed()
        {
            _averageSpeedX = 0;
            _updateCounter = 0;
            _averageSpeedSq = 0;
        }

#if OLD
        public Vector2 GetLeftGroundContactPoint(bool atToe)
        {
            return GetFootGroundContactPoint(true, false, atToe);
        }

        public Vector2 GetRightGroundContactPoint(bool atToe)
        {
            return GetFootGroundContactPoint(false, false, atToe);
        }


#endif


        public IEnumerable<Body> GetBodiesWithPartType(PartType partType)
        {
            PartType[] parttypes = new PartType[1];
            parttypes[0] = partType;
            return GetAllBodiesWithPartTypes(parttypes, false, true);
        }



        public Body GetBodyWithPartType(PartType[] typelist)
        {
            return GetBodyWithPartType(typelist, false);
        }


        public Body GetBodyWithPartType(PartType[] typelist, bool collidable)
        {
            return GetBodyWithPartType(typelist, false, collidable);
        }

        /// <summary>
        /// Get a body from spirit.Bodies that have a specific PartType.
        /// </summary>
        /// <param name="typelist">List that contain types to be searched, ordered by priority first.</param>
        /// <param name="middle">indicates inner or middle leg.. ie     
        /// /// <param name="collidable"></param>mustg be collidable..
        /// If first type not found, continue with second type, and so on, until one found then return.
        /// </param>
        /// <returns>Body with desired PartType found, or null if nothing found.</returns>

        public Body GetBodyWithPartType(PartType[] typelist, bool middle, bool collidable)
        {
            Body body = null;
            foreach (PartType pt in typelist)
            {
                body = GetBodyWithPartType(pt, middle, collidable);

                if (body != null)
                    break;
            }
            return body;
        }

        /// <summary>
        /// Get all bodies with a body infoflag
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public IEnumerable<Body> GetBodiesWithInfo(BodyInfo info)
        {
            return Bodies.Where(x => ((x.Info & info) != 0));
        }

        /// <summary>
        ///Get first of Bodies withthe body infoflag
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public Body GetFirstBodyWithInfo(BodyInfo info)
        {
            return Bodies.FirstOrDefault(x => ((x.Info & info) != 0));
        }

        /// <summary>
        /// Get a body from spirit.Bodies that have a specific PartType.
        /// </summary>
        /// <param name="typelist">List that contain types to be searched, ordered by priority first.
        /// If first type not found, continue with second type, and so on, until one found then return.</param>
        /// <returns>Body with desired PartType found, or null if nothing found.</returns>

        public Body GetBodyWithPartType(PartType partType)
        {
            return GetBodyWithPartType(partType, (partType & PartType.Middle) == PartType.Middle, false);
        }


        /// <summary>
        /// Get a body from spirit.Bodies that have a specific PartType.
        /// </summary>
        /// <param name="partType">Part type to find</param>
        /// <param name="middle">is middle ( for 4 leg animals</param>
        /// <param name="mustBeCollidable">dont count body set with non collidable state (as in during shrink part of regen when creature has invisible parts being shrunk over frames </param>
        /// <returns></returns>
        public Body GetBodyWithPartType(PartType partType, bool middle, bool mustBeCollidable)
        {
            Body body = null;


            if (middle)
            {
                partType |= PartType.Middle;
            }

            foreach (Body b in Bodies)
            {
                if (b.PartType == partType && b.IsMiddlePart == middle && (!mustBeCollidable || !b.IsNotCollideable))
                {
                    body = b;
                    break;
                }
            }
            return body;
        }


        public Body GetCollideableBodyWithPartType(PartType pt)
        {
            return GetBodyWithPartType(pt, false, true);
        }


        public Body GetGrabCapableHand(bool left)
        {
            PartType pt = left ? PartType.LeftHand : PartType.RightHand;

            Body body = GetBodyWithPartType(pt, false, true);

            if (body == null)
                return null;

            float scale;
            GetRegeneratingScale(body, out scale);

            if (scale < minScaleGrabUseable) //todo check the grip size
                return null;
            else
                return body;
        }

        public const float minScaleGrabUseable = 0.65f;



        public List<Body> GetAllBodiesWithPartFlags(PartType partType, bool middle, bool mustBeCollidable)
        {
            List<Body> body = new List<Body>();
            foreach (Body b in BodySet)
            {
                if ((b.PartType & partType) != 0 && b.IsMiddlePart == middle && (!mustBeCollidable || !b.IsNotCollideable))
                {
                    body.Add(b);
                }
            }
            return body;
        }

        public List<Body> GetAllBodiesWithPartFlags(PartType partType)
        {
            return GetAllBodiesWithPartFlags(partType, false, false);
        }


        public List<Body> GetAllBodiesWithPartTypes(PartType[] partTypes, bool middlePart, bool mustBeCollidable)
        {
            List<Body> bodies = new List<Body>();
            foreach (Body b in Bodies)
            {
                if (partTypes.Contains<PartType>(b.PartType) && bodies.Contains(b) == false && b.IsMiddlePart == middlePart)
                {
                    bodies.Add(b);
                }
            }

            return bodies;
        }


        public void HaveSeizure(float magnitude, float duration, float fadefactor)
        {
            HaveSeizure(magnitude, duration, fadefactor, false, 0);
        }

        /// <summary>
        /// Makes creature shake or spasms
        /// </summary>
        /// <param name="magnitude">1 twitch, 10 = grand Mal</param>
        /// <param name="duration">duration in sec</param>
        ///   <param name="fadefactor">magiture change by this during </param>
        ///   <param name="fatal">after seizure enegy become zero</param>
        public void HaveSeizure(float magnitude, float duration, float fadefactor, bool fatal, float rigormortisTime)
        {
            //     float seizureLen = MathUtils.RandomNumber(1, 6);
            //     float magnitude = MathUtils.RandomNumber(0.5f, 2f);
            new Seizure(this, "spazm", magnitude, duration, fadefactor, fatal, rigormortisTime);
            //if 
        }



        /// <summary>
        /// Get the time of the keyframe, in the current behavior.. can be compared to current time.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public double GetKeyFrameTime(int index)
        {
            return ActiveBehavior.Keyframes[index].Time * ActiveBehavior.TimeDilateFactor * TimeDilateAdjustFactor;
        }


        /// <summary>
        /// Set joint to ragdoll, then turn selfcollide on.
        /// </summary>
        public void FallLimp()
        {
            foreach (PoweredJoint pj in this.Joints)
            {
                pj.IsNumb = true;
            }

            IsSelfCollide = true;

        }



        /// <summary>
        /// Reset joint back to stiff but not rigid..  Rigor mortis
        /// </summary>
        /// 
        //TODO change to LFE .. need stiff joints to unstick before record..

        //TODO frame count in LFE..

        //TODO after die , body might be walked on   best to add an unstick selp
        //is selfcollide on 2 from every 10 frames or so ..
        // maybe detect rest vel..first before dudate..


        public void StiffenJoints()
        {
            foreach (PoweredJoint pj in Joints)
            {
                SetJointToDeadState(pj);
            }
            //   IsSelfCollide = false; //for performance,, since its stiff should not be needed?
            //TODO FUTURE: for walkingon on dead bodies better leave self collide.. and spirit should be forced alseep even when dead, check in plugin..
        }

        //TODO use the AABB way, this does not account for severed parts.
        /// <summary>
        /// Is the body outer on the graph , returns false for inner bones
        /// </summary>
        /// <returns></returns>
        bool IsExtremityBody(Body b)
        {
            return (b.PartType & (PartType.Hand | PartType.Foot | PartType.Head | PartType.Toe)) != 0;  //TODO NOT TESTED, CURRENTLY UNUSED
        }

        public void ExtremitySelfCollide()
        {
            foreach (Body b in Bodies)
            {
                if (IsExtremityBody(b))
                {
                    b.CollisionGroup = 0;
                }
                else
                {
                    b.CollisionGroup = CollisionGroupId;   //self collide off for all spirit parts..
                }
            }
        }


        /// <summary>
        /// Apply joint Bias factor to all the joints on this spirit.  0 = weak joints, 1 = rigid and strong.
        /// </summary>
        /// <param name="?"></param>
        public void ApplyJointBias(float biasFactor)
        {
            foreach (PoweredJoint pj in Joints)
            {
                pj.BiasFactor = biasFactor;
            }
        }



        /// <summary>
        /// Apply joint  DampingFactor to all 
        /// </summary>
        /// <param name="?"></param>
        public void ApplyJointDampingFactor(float factor)
        {
            foreach (PoweredJoint pj in Joints)
            {
                pj.DampingFactor = factor;
            }
        }

        /// <summary>
        ///  Apply joint Breakpoint to all the joints on this spirit.
        /// </summary>
        /// <param name="biasFactor"></param>
        public void ApplyJointBreakpoint(float value)
        {
            Joints.ForEach(x => x.Breakpoint = value);
            FixedJoints.ForEach(x => x.Breakpoint = value);
        }


        /// <summary>
        ///  Apply joint Softness to all the joints on this spirit.
        /// </summary>
        /// <param name="biasFactor"></param>
        public void ApplyJointSoftness(float factor)
        {
            foreach (PoweredJoint pj in Joints)
            {
                pj.AngleJoint.Softness = factor;

            }
        }





#region IEntity Members

        public Vector2 Position
        {
            //TODO CODE REIVEW..  why dont  we use  World Position??   isnt it the same.. WorldCM   need to check this
            get { return MainBody.Position + MainBody.LocalCenter; }
            set
            {
                Vector2 posInWorld = MainBody.LocalCenter + MainBody.Position;

                if (posInWorld != value)
                {

                    //TODOD  review this looks suspect 
                    Vector2 delta = Vector2.Subtract(value, posInWorld);

                    foreach (Body body in Bodies)
                    {
                        body.Position = Vector2.Add(body.Position, delta);// -body.LocalCenter;    //TODO need to check this.
                    }
                }
            }

            //get { return MainBody.WorldCenter; }
            //set
            //{
            //    Vector2 delta = Vector2.Subtract(value, MainBody.WorldCenter);
            //    foreach (Body body in Bodies)
            //    {
            //        body.Position = Vector2.Add(body.Position, delta);
            //    }
            //}
        }


        public float Rotation
        {
            get { return MainBody.Rotation; }
            set { MainBody.Rotation = value; }
        }

        public AABB EntityAABB
        {
            get { return this.AABB; }
        }


    
        public void SetAsCloneOf(Spirit spSource)
        {
            _behaviors = spSource.Behaviors;
            //TODO listen to active behavior ?
        }

#endregion




        /// <summary>
        /// Swoon or  Faint.  Creature will fall limp for a specified time..
        /// </summary>
        /// <param name="duration"></param>
        public Swoon Swoon(float duration, float energyLevel, float massFactorSq)
        {
            string key = "swoon";
            if (!Effects.Contains(key))
            {
                Swoon swoon = new Swoon(this, key, duration, energyLevel, massFactorSq);
                return swoon;
            }
            else
                return null;
        }


        /// <summary>
        ///  Eat, snap jaws.
        /// </summary>
        /// <param name="duration"></param>
        public void Chew(int jawJointIndex, double duration, double magnitude, double frequency)
        {

            new Bite(this, "chewFood", jawJointIndex, duration, magnitude, frequency);

        }

        /// <summary>
        /// find the first Joint connecting a body part, useful for extremes:  Jaw, hand.. etc.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public int GetBodyPartPoweredJointIndex(PartType type)
        {
            foreach (PoweredJoint pj in Joints)  //only connected bodies.  cant just iterated joints..
            {
                if (pj.BodyA.PartType == type || pj.BodyB.PartType == type && !pj.IsBroken)
                    return Joints.IndexOf(pj);

            }
            return -1;
        }


        /// <summary>
        /// Glow with a color for a period..  
        /// </summary>
        /// <param name="glowColor"></param>
        /// <param name="duration"></param>
        /// <param name="magnitude"></param>
        /// <param name="frequency"></param>
        public void Glow(BodyColor glowColor, double duration, double minMagnitude, double maxMagnitude, double frequency)
        {
            new Glow(this, "glow", glowColor, duration, minMagnitude, maxMagnitude, frequency);
        }


        /*
        //TODO might need this for futre if full self collide, not used now... would need to check relative speed of itmes..
        /// <summary>
        ///  Unstick setl
        /// </summary>
        /// <param name="duration"></param>
        public void UnstickCycle(int cycleLen, int unstickFrames)
        {
            if (LowFrequencyEffects.OfType<BeatEffect>().Count() == 0)//avoid  have 2 running  
            {
                BeatEffect beat = new BeatEffect(cycleLen, unstickFrames, this, "beat");
                beat.OnOffCycle = OnOffBeat;
                beat.OnEffect = UnstickSelf;
                LowFrequencyEffects.Add(beat);
            }
        }*/

        public void OnOffBeat()
        {
            IsSelfCollide = true;    //this effect is mean for use with     IsSelfCollide = true;  thats when  it can get stuck to self due to tunnelling
        }


        //TODO when  a good self collide is in .. erasse... possible using joints.. drawn in tool.. something like a distance min limit joint or parametric joint limit or rods.
        ///// <summary>
        /////whis was supposed to  unstick the thing usually by turning IsSelfCollide to false every X frames..  not used now using angle relation formulas and approximations.
        ///// </summary>
        //public void UnstickSelf()
        //{
        //    //this will unstick the thing usually by turning IsSelfCollide to false.
        //    if (OnUnstickSelfCycle != null)
        //        OnUnstickSelfCycle();

        //}

        /// <summary>
        /// DizzySpell  Creature will not be able to balance for a specified time..
        /// </summary>
        /// <param name="duration"></param>
        public void GetDizzy(float duration)
        {
            new Dizzyness(this, "dizzyspell", duration);
        }

        /// <summary>
        ///Affects OffsetFilter This method is good for leveling and want to keep the shins near vertical, as offset from our standing pose.  We are able to apply it as offsets to any stance though. 
        //which miraculously enables walking over uneven ground , even climbing with minor adjustments.  See the diagrams in Kontrol docs under formulas. and the trigonometric solution.  A target slope can be passed in, allowing for leaning the body by adjusting both hips and knees.
        /// </summary>
        /// <param name="leftHipIndex"></param>
        /// <param name="leftKneeIndex"></param>
        /// <param name="rightHipIndex"></param>
        /// <param name="rightKneeIndex"></param>
        /// <param name="leftShoulderIndex"></param>
        /// <param name="leftElbowIdx"></param>
        /// <param name="rightShoulderIndex"></param>
        /// <param name="rightElbowIdx"></param>
        /// <param name="shoulderRaiseFactor"></param>
        /// <param name="elbowRaiseFactor"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        public bool SetHipKneeOffsetFilterForStandingOnGradientTwoLegged(int leftHipIndex, int leftKneeIndex,
             int rightHipIndex, int rightKneeIndex, int leftShoulderIndex, int leftElbowIdx, int rightShoulderIndex, int rightElbowIdx, float shoulderRaiseFactor, float elbowRaiseFactor, float dx, float dy)
        {


            // check if feet too close, cannot get an accurate slope if so.
            if (dx < 0.03)
            {
                return false;
            }

            double slope = dy / dx;

            // Safe slope value -0.78 .. +0.78
            // Formula fails (NaN) outside this limit
            // If we feel this slope is making creature bend its knee too much, we can decrease this value, but beyond (+/-)0.78 formula will fail
            // My favorite safeSlope is 0.5-0.6
            double safeSlope = 0.78;

            if (slope < -safeSlope)
            {
                slope = -safeSlope;
            }
            if (slope > safeSlope)
            {
                slope = safeSlope;
            }

            //System.Diagnostics.Trace.TraceInformation(string.Format("slope = {0}", slope));

            // TODO consider check if slope really is too steep to bother
            double slopeThreshold = 0.03;

            //if level enough ground dont need to do anything..
            if (Math.Abs(slope) < slopeThreshold)
            {
                return false;
            }

            //analysis in vault and google docs under formulas

            double m = 1 / Math.Sqrt(1 + slope * slope);
            double x = dx;
            double l = 1.25;
            double s = slope;

            if (slope < 0)
            {
                s = s * -1;
            }

            double sx = s * x;
            double lm = l * m;
            double m1 = 1 / m;
            double first = Math.Asin(m1 - (sx / lm));
            double second = Math.Asin(s / m);

            // for tracing purposes only, if safe slope is out of limit, then this conditional will get executed
            if (double.IsNaN(second))
            {
                //System.Diagnostics.Trace.TraceInformation(string.Format("NaN: dx = {0}, dy = {1}, slope = {2}", dx, dy, slope));
                return false;
            }

            float alpha = (float)(first - second);

            float a90 = (float)Math.PI * 0.5f;
            alpha = alpha - a90;

            float atanslope = (float)Math.Atan(slope);
            float angleShoulder = atanslope * shoulderRaiseFactor;//overcompensate.. raising hand helps climb;
            float angleElbow = atanslope * elbowRaiseFactor;

            if (slope < slopeThreshold)
            {
                OffsetFilter1.SetOffset(rightHipIndex, alpha);
                OffsetFilter1.SetOffset(rightKneeIndex, -alpha);

                //this is to raise arm to level, so knee doesnt hit it
                OffsetFilter1.SetOffset(rightShoulderIndex, (float)angleShoulder);

                // NOTE: this interfere with punch on AI
                OffsetFilter1.SetOffset(rightElbowIdx, (float)angleElbow);

            }
            else if (slope > slopeThreshold)
            {
                OffsetFilter1.SetOffset(leftHipIndex, -alpha);
                OffsetFilter1.SetOffset(leftKneeIndex, alpha);
                //this is to raise arm to level with ground, so knee doesnt hit it
                OffsetFilter1.SetOffset(leftShoulderIndex, (float)angleShoulder);

                // NOTE: this interfere with punch on AI
                OffsetFilter1.SetOffset(leftElbowIdx, (float)angleElbow);
            }

            return true;
        }


        public bool SetHipKneeOffsetFilterForStandingOnGradientTwoLegged(int leftHipIndex, int leftKneeIndex,
            int rightHipIndex, int rightKneeIndex, float dx, float dy)
        {

            return SetHipKneeOffsetFilterForStandingOnGradientTwoLegged(leftHipIndex, leftKneeIndex, rightHipIndex, rightKneeIndex, -1, -1, -1, -1, 1, 1, dx, dy);
        }


        //TODO move to biped.. almost all stuff below this
        public void SetEyeLimits()  //TODO fix eye pointing,  keep limits on at all times.  now its during swoon or die so eyes wont spin comically
        {
            foreach (PoweredJoint pj in EyeJoints)
            {
                pj.UpperLimit = 2.7f; //TODO tune this rollect back into head?
                pj.LimitEnabled = true;
                pj.Softness = 0.8f;
            }
        }

        public List<PoweredJoint> EyeJoints
        {
            get
            {
                if (_eyeJoints == null || _eyeJoints.Count == 0)
                {
                    CollectEyes();
                    CollectEyeJoints();
                }
                return _eyeJoints;
            }
        }

        public void DisableEyeLimits()
        {
            foreach (PoweredJoint pj in EyeJoints)
            {
                pj.LimitEnabled = false;
            }
        }



        private List<PoweredJoint> _eyeJoints = new List<PoweredJoint>();

        public List<PoweredJoint> CollectEyeJoints()
        {
            _eyeJoints = new List<PoweredJoint>();
            foreach (Body eye in _eyes)
            {
                if (eye.JointList == null)// physics will set this on first frame.
                    continue;

                PoweredJoint pj = eye.JointList.Joint as PoweredJoint;

                JointEdge jointEdge = eye.JointList;


                while (jointEdge!= null)  //sometime legacy eye has pupil or glint welded on
                {
                    pj = jointEdge.Joint as PoweredJoint;
                    if (pj != null && jointEdge.Other.PartType == PartType.Head)
                        break;

                    jointEdge = jointEdge.Next;
                }

                if (pj == null)
                {
                    continue;
                }
                _eyeJoints.Add(pj);
            }
            return _eyeJoints;
        }

        public const float MinEnergyForRegen = 100;
        const float BulletToMainBodyEnergyLoss = 50;
        const float SharpToTenderSpotEnergyLoss = 2f;



        /// <summary>
        /// if bullet or sword hits body, die.. TODO move this to creature.. its not general
        /// </summary>
        /// <param name="ourBody"></param>
        /// <param name="contactPointWorld"></param>
        /// <param name="atc"></param>
        /// <param name="isBullet"></param>
        public void HandleSharpPenetrationDamage(Body ourBody, Vector2 contactPointWorld, AttachPoint atc, bool isBullet)
        {
            if (ourBody.PartType == PartType.Head)
            {
                DropDead(); //TODO if bullet..  or if head cut.. just fall dead?
            }
            else if (ourBody.PartType == PartType.MainBody)
            {

                if (atc != null && (atc.Flags & AttachPointFlags.IsHeart) != 0)
                {
                    //just drop dead after freezing a few secs.. (low magnitude seizure) 
                    HaveSeizure(0.2f, MathUtils.RandomNumber(0.5f, 2f), 0f, true, 40);
                    return;
                }

                float energyLoss = isBullet ? BulletToMainBodyEnergyLoss : SharpToTenderSpotEnergyLoss;
                EnergyLevel -= SizeFactor * energyLoss;

            }
            else
            {

                //TODO MINOR IMPROVEMENT .get nearest within dist.
                const float minDistanceToJointAnchorSq = 0.2f * 0.2f;
                PoweredJoint joint = (PoweredJoint)GetFirstJointWithinDistance(ourBody, ref contactPointWorld, minDistanceToJointAnchorSq, true, BodyInfo.Bullet);

                if (joint != null)
                {
                    int idx = Joints.IndexOf(joint);
                    String numbNessKey = "DeadLimb" + contactPointWorld.ToString() + ourBody.GetHashCode() + idx.ToString();   // make sure its unique in case hit twice.

                    //NOTE to have really dead arms they will  self collide.
                    const float WeakBias = 0.02f;
                    // need to adjust relax to handle 0 bias or use softness.. or add anther effect with same duration.. for both bodies... and bullet and make collldble
                    //also would need a delay before applying  on the effect so that bullet reaction wount tunnel the body if collide
                    SetBias relax = new SetBias(this, numbNessKey, MathUtils.RandomNumber(2f, 10f), WeakBias, new int[] { idx });// TOD later relate to the impulse .. and size of creature just as bosses
                }
            }
        }

        //public int GetNearestJointIdxWithinDistance(Body ourBody, Vector2 contactPointWord)
        //{
        //    List<Joint> joints = new List<Joint>();
        //    GraphWalker.GetJointsFromBody(ourBody, out  joints, true);
        //}



        public static PoweredJoint GetFirstJointConnectingType(Body body, PartType pt)
        {
            JointEdge je = body.JointList;
            while (je != null)
            {
                PoweredJoint pj = je.Joint as PoweredJoint;

                if (pj != null)
                {
                    if ((pj.BodyA.PartType & pt) != 0 ||
                        (pj.BodyB.PartType & pt) != 0)
                    {
                        return pj;
                    }
                    je = je.Next;
                }
            }
            return null;
        }


        /// <summary>
        /// Returns an angle from ranging for -pi to pi  from  vector relative to the body, can be used for Target angleon a joint anchored to body
        /// </summary>
        /// <param name="b"></param>
        /// <param name="targetVec"></param>
        /// <returns>angle to body</returns>
        public float AngleToBody(Body b, Vector2 targetVec)
        {
            if (b == null)
            {
                return 0f;
            }
            return b.AngleToBody(targetVec);
        }



        /// <summary>
        /// Find angle (in radian) relative to body r. Interface for 
        /// GetAngleFromVectorCartesian(x,-y). This is for graphics coordinate,
        /// where the y direction is reversed. 
        ///  Find angle from vector  relative to body r.  always return positive angle. The 0 angle is on
        /// 3 o'clock, positive value is calculated counterclockwise.
        /// </summary>
        /// <param name="b"></param>
        /// <param name="targetVec"></param>
        /// <returns></returns>
        public float CartesianAngleToBody(Body b, Vector2 targetVec)
        {
            targetVec = b.GetLocalVector(ref targetVec);

            return MathHelper.ToRadians(
                GeomUtility.GetAngleFromVectorCartesian(targetVec.X, targetVec.Y)
                );
        }


        /// <summary>
        /// To be used with result from PositiveAngleToBody(), so that 0 angle is on 12 o'clock,
        /// left side is positive 0 to pi, right side is negative 0 to -pi.
        /// </summary>
        public void AngleFrom12ClockOrigin(ref float angle)
        {
            // because 0 angle is on 3 o'clock, we need to substract it to make 0 angle on 12 o'clock.
            float an270 = MathHelper.ToRadians(270);
            float an90 = (float)(Math.PI * 0.5f);
            float an450 = MathHelper.ToRadians(450);
            if (angle <= an270)
            {
                angle -= an90;
            }
            else
            {
                angle -= an450;   // 360 + 90
            }
        }

        public bool IsUnconscious
        {
            get
            {
                return (EnergyLevel < 10);//TODO consider.. separate this from energy level..  for now seems fine, too weak to think, if swoon will be 8 
            }
        }


#region Spirit's Sensor Helper


    


    

        //TODO future code review , rename Bite to Chew.. its chewing repeatedly 
        public bool IsChewing()
        {
            return (Effects.OfType<Bite>().Count() > 0);
        }


        //TODO move to creature ..

        /// <summary>
        /// Called when spirit head collide with external body.
        /// </summary>
        internal bool OnHeadCollisionDefault(Body externalBody)
        {
            //// this is to fix issue on multipart food
            //ValidateFood(externalBody);

            //if (externalBody.PartType == PartType.Food /*&& externalBody.Nourishment <= 0 && !IsChewing()*/)
            //{
            //    ValidateFood(externalBody);
            //}

            // sometimes this OnHeadCollisionDefault might get called twice, one for head, and one for jaw. 
            // if head collide with food, eat it
            if (!IsChewing() && externalBody.Nourishment != 0f && EnergyLevel >= 10)// if sleeping or fainting cant eat..  //TODO CODE REVIEW consider separate state for sleep/ swoon..
            {
                // add nourishment to spirit energy level
                EnergyLevel += externalBody.Nourishment * 12;

                if (OnHeadFoodCollision != null)//plugin can snap the beak or chew
                {
                    OnHeadFoodCollision(externalBody);
                }

                externalBody.Nourishment = 0;  ///this prevents multple collision events from calling into this section


                // remove body from level, use cached op to prevent thread owner exception
                Level.CacheRemoveEntity(externalBody);


            }
            return true;
        }



        /// <summary>
        /// Return angle of ground below spirit. 
        /// Projects 2 ray from MainBody AABB left and right point to ground, ignoring all spirit body.
        /// Then calculate angle of line from 2 intersection point on ground.
        /// Angle is centered on left point, moved counter-clockwise, 0 angle is on 3 o'clock.
        /// Normal range is between 0-180 (uphill to right) and 270-360 (downhill to right).
        /// </summary>
        public float DetectGroundAngleUnderAABB(Sensor sensor)
        {
            AABB aabb = MainBody.AABB;

            // start ray from upper-side of aabb. 
            // if ray start from bottom side of aabb, one point might miss (non-hit) intersection on sloped ground.
            Vector2 lStartPos = new Vector2(aabb.LowerBound.X, aabb.LowerBound.Y);
            Vector2 rStartPos = new Vector2(aabb.UpperBound.X, aabb.LowerBound.Y);

            // because spirit might be tall, ray length should be spirit height + additional value
            Vector2 rayVector = new Vector2(0, 1.5f * AABB.Height + 3f);
            Vector2 lEndPos = lStartPos + rayVector;
            Vector2 rEndPos = rStartPos + rayVector;

            //ignore all bodies in our spirit system.
            List<Body> bodyPartsToIgnore = new List<Body>(Bodies);
            bodyPartsToIgnore.AddRange(HeldBodies);

            string hname = MainBody.GetHashCode().ToString();
            string lname = "MainBodyLeft" + hname;
            string rname = "MainBodyRight" + hname;
            RayInfo left = sensor.AddRay(lStartPos, lEndPos, lname, bodyPartsToIgnore);
            RayInfo right = sensor.AddRay(rStartPos, rEndPos, rname, bodyPartsToIgnore);

            if (left == null || left.IsIntersect == false ||
                right == null || right.IsIntersect == false)
            {
                return 0;
            }

            // vector line from left to right ground intersection
            Vector2 gline = right.Intersection - left.Intersection;

            // get angle of the ground line, calculated from 3 o'clock. center is on left ground point.
            return GeomUtility.GetAngleFromVector(gline.X, gline.Y);
        }


        public bool IsRegrowingArms()
        {
            RegenerateMissingBodyParts regrow = GetRegrowEffect();
            if (regrow == null)
                return false;
            else
                return regrow.IsRegrowingArms;
        }


        // static method. body dont need to be from this spirit. any connected bodies should work.
        public static bool IsConnectedToMainBody(Body body)
        {
            // walk entire connected graph, start from this body, check SkipTraversal
            List<Body> bodies;
            List<Joint> dummy;
            List<Joint> dummy2;
            GraphWalker.WalkGraph(body, out bodies, out dummy, out dummy2);
            return (GraphWalker.FindOrDetermineAndMarkMainBody(bodies) != null);
        }


        /// <summary>
        /// includes static bodies and bodies in this spirit...
        /// </summary>
        /// <param name="aabb"></param>
        /// <returns></returns>
        public FarseerPhysics.Common.HashSet<Body> DetectBodiesInAABB(AABB aabb)
        {
            return DetectBodiesInAABB(aabb, true, 1);
        }

        /// <summary>
        /// DetectBodiesInAABB   finds other bodies in specified AABB ,  specified in WCS
        /// </summary>
        /// <param name="width"></param>
        /// <param name="length"></param>
        /// <param name="includeAllStaticBodies">will return static  exception is made if static has an attach points.. need</param>
        /// <param name="collisionGroup"></param>
        /// <returns>Set of bodies, no duplicates </returns>
        public FarseerPhysics.Common.HashSet<Body> DetectBodiesInAABB(AABB aabb, bool includeAllStaticBodies, short collisionGroup)
        {
            FarseerPhysics.Common.HashSet<Body> bodySet = new FarseerPhysics.Common.HashSet<Body>(32);   //hash set is just a dictionary of int.. this is a quick way to remove duplicates since a body can have many 
            int shapeCount = 0;


            World.QueryAABBSafe(
                fixture =>
                {
                    // fixture.IsSensor  include  ?

                    if (shapeCount > MaxShapes)
                        return false;

                    if (!fixture.IsSensor)
                    {
                        _shapesDetected[shapeCount++] = fixture;
                    }

                    return true;// Continue the query.
                }, ref aabb);

            if (shapeCount == 0)
                return bodySet;

            //fixtures that will be returned by the query.. its in .net 4 and implemented and used  in Farseer 

            for (int i = 0; i < shapeCount; i++)
            {
                Fixture fixture = _shapesDetected[i];

                if (!includeAllStaticBodies && fixture.Body.BodyType == BodyType.Static && fixture.Body.AttachPoints.Count() == 0)
                    continue;

                if (fixture.Body.CollisionGroup != collisionGroup
                    && !bodySet.Contains(fixture.Body) // build a set of unique bodies ..for list its linear time search, for dictionary its log time, faster.
                    && !_bodySet.Contains(fixture.Body))  //dont detect our own bodies 
                {
                    bodySet.Add(fixture.Body);
                }
            }

            return bodySet;
        }


#if OLD
        public RayInfo RayFromLeftFoot(Sensor sensor, float length, float yOffset, float angle)
        {
            return RayFromFoot(sensor, length, 0, yOffset, angle, true, false, true);
        }

        public RayInfo RayFromRightFoot(Sensor sensor, float length, float yOffset, float angle)
        {
            return RayFromFoot(sensor, length, 0, yOffset, angle, false, false, true);
        }
#endif

        /// <summary>
        /// detemines if the body its defining the AABB boundary.
        /// determined by shrinking sprit AABB a bit and see what doesnt lie inside.
        /// things inside 
        /// </summary>
        /// <param name="body"></param>
        /// <param name="marginFactor"> should be less than 1... .1 means a 10% margin </param>
        /// <returns></returns>
        public bool IsExtremity(Body body, float marginFactor)
        {
            AABB tighterAABB = AABB.Expand(marginFactor, -marginFactor);
            return !(tighterAABB.Contains(ref body.AABB));
        }

#endregion


        //TODO move all this to creature..
        /// <summary>
        ///  Get the Nearest AttachPoint, for looking or climbing
        /// </summary>
        /// <param name="unheld">only return if not already in our grip</param>
        /// <param name="facing">only if facing it.</param>
        /// <param name="filter"></param>
        /// <returns></returns>
        /// 
        public AttachPoint GetNearstAttachPoint(bool unheld, bool facing, PartType filter)//, bool above);
        {
            return GetNearestAttachPoint(unheld, facing, filter, float.MaxValue);
        }

        /// <summary>
        ///  Get the Nearest AttachPoint, for looking or climbing
        /// </summary>
        /// <param name="unheld">only return if not already in our grip</param>
        /// <param name="facing">only if facing it.</param>
        /// <param name="filter"></param>
        /// <returns></returns>
        /// <param name="minDistance">must be within this distance </param>
        /// <returns></returns>
        public AttachPoint GetNearestAttachPoint(bool unheld, bool facing, PartType filter, float minDistance)//, bool above);
        {
            float shortestDistance = minDistance + 0.0001f; //so that it will be closer and counted.

            AttachPoint currentAtc = null;
            foreach (AttachPoint atc in AttachPointsInSensor)
            {
                if (filter != PartType.None && atc.Parent.PartType != filter)
                    continue;

                if (HoldingClimbHandle && unheld && HeldBodies.Contains(atc.Parent))
                    continue;

                if (facing && (IsFacingLeft != atc.Parent.WorldCenter.X < MainBody.WorldCenter.X))//if not facing spirit, skip it.
                    continue;

                float dist = Vector2.Distance(MainBody.WorldCenter, (atc.WorldPosition));

                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    currentAtc = atc;
                }
            }
            return currentAtc;
        }

        /// <summary>
        /// For climbing, get the NearestClimbingAttachPoint
        /// </summary>
        /// <param name="unheld">only return if not already in our grip</param>
        //     /// <param name="above">only return if above our main body</param>
        /// <returns></returns>
        public AttachPoint GetNearestClimbingAttachPoint(bool unheld)//, bool above);
        {
            return GetNearstAttachPoint(unheld, false, PartType.Handhold);
        }


        //move to creature.

#region Regenerate & Grow


        public void StartNewDelayBeforeRegrow()
        {
            // compare current and original body parts, if no body missing then we should return.
            if (_bodyCountWhenLoaded == _bodies.Count)
            {
                return;
            }
            // no need to stop current regrow, spirit will be changed after this
            // if currently delayed, just reset delay to default value

            string key = "startRegrowDelay";

            Delay delay = null;

            if (Effects.Contains(key))
            {
                delay = Effects[key] as Delay;
            }

            if (delay == null)
            {
                delay = new Delay(this, key, GROW_DELAY);

                delay.CanEndEffect = ReadyToRegenerate;
                delay.OnEndEffect = OnGrowDelayEnded;   // on end of delay it will request regrow, then change spirit
            }

            delay.Reset();
        }


        /// <summary>
        /// dont replace spirit untill its in a quiet state, can have a strange pause in middle of a move..
        /// </summary>
        bool ReadyToRegenerate(Effect effect)
        {
            bool pluginCanEnd = true;
            if (CanEndRegenerateDelay != null)
            {
                pluginCanEnd = CanEndRegenerateDelay();
            }

            return (
                pluginCanEnd
                && EnergyLevel > MinEnergyForRegen
                && MainBody.LinearVelocity.LengthSquared() < 4f * 4f     // //TODO remove this , test higher speed regrow in balloon in jetstream.. just linear vel.. spirit doest not have vel applied on replace.. migth have issue.    can regen while drifting slowly in balloon
                                                                         //      && Math.Abs(MainBody.LinearVelocity.Y) < 4f     // TODO try this if not moving in Y direction probably at rest.., TODO test higher speed regrow in balloon in jetstream.. just linear vel.. spirit doest not have vel applied on replace.. migth have issue.    can regen while drifting slowly in balloon

                && (
                 //!HasLimbs() ||// i.e.EDGE CASE   CHECK MABYE TODO GRAVITY DIR sometimes spirit still doing animation even when all limbs are severed,  should not check for IsAnimating if spirit is no longer has limbs.
                 !IsAnimating)    
                && !(IsThrustingRight || IsThrustingLeft)
                //       if (Effects.OfType<Grow>().Count() > 0)   //TODO check if other LFE running beside swoon or grow..
                && (GetRegrowEffect() == null)    // don't do regrow if another regrow is in process..  TODO  make it allowed again if shrink is removed..
             );

        }


        /// <summary>
        /// Compare this spirit and soon-to-be-removed spirit, to know which missing part should regenerate.
        /// Call this from level listener, when handling SpiritRegenerate event.
        /// </summary>
        public void RegenBasedOnInjuredSpirit(Spirit injuredSpirit, IPlugin<Spirit> injuredSpirtPlugin, Sensor sensor)
        {

            try
            {
                // it's possible to have EntityOperation.SpiritRegenerate get queued more than once for same spirit.
                if (GetRegrowEffect() != null)
                    return;

                // current injured spirit parts
                Dictionary<PartType, Body> existingPartsOnInjured = injuredSpirit.GetPartTypeToBodyMap();

                // get missing parts
                FarseerPhysics.Common.HashSet<Body> missingParts = GetMissingBodyPartsExludingNeck(existingPartsOnInjured);
                CopyEffects(injuredSpirit);

                // get previous lfe bodies from injured spirit
                Dictionary<Body, float> missingPartsForNewLFE = GetRegrowingBodiesFromInjuredSpiritBasedOnPartType(injuredSpirit, RegrowKey);

                // include current missingParts as new regrow body, with default growSize=1.
                //TODO add other LFE?   like punching.. or better , well dont all regen when LFE is happening.
                // sprit must  be at rest, standing or lying down..
                //or we'd have to clone all LFE state

                foreach (Body b in missingParts)
                {
                    if (missingPartsForNewLFE.ContainsKey(b) == false)
                    {
                        missingPartsForNewLFE.Add(b, 1);
                    }
                }
                // animate growing body parts

#if !TEST_REGROW_ZERO_FORCE

                // 20 is good value , but a bit long wait. // 0.05 is stable for start scale but sprouts pretty big..
                // NOTE SET TOO LOLW AND MASS RATIO OR SOMETHING BREAKS ESPECTIALL UPPER THE ARM .08 IS TOO LOW FOR ARM
                // tried to set to 0.03 but body replace often breaks something else right after.. 
                // maybe well remove breakpoints druing init regrow..
                //this is VERY SENSITIVE... THERE IS AN ISSUE WITH ARMS.. SET IT HIGH AND SHOW INVISIBLE SMALL ARM SPINS AND CAUSES ISSUES
                //MIGHT NEED INCREATEA DENSITY DURING GROW  
                int frameCounterPerCycle = 10;

                float minscale = 0.09f;//if too small can explode after shrink..mass ratio , inertia or joint solving 

                //if  leg missng .. not much player can do ... so regrow it faster..
                //TODO allow to use one arm down to help move aroudn.. they can hop around one leg..       
                // however player must deal with missing arms longer.
                if (missingParts.Any(x => (x.PartType & PartType.Thigh) != 0 || (x.PartType & PartType.Shin) != 0 || (x.PartType & PartType.Foot) != 0))
                {
                    frameCounterPerCycle = 6;// dh bumped this up from 5 since creature explosion was reported.  before build 666 was 10 for all
                }
                //clue is arm spin then creature explode  this is a workaroud..set drag flag drawinvisible to see it happen


                if (missingParts.Any(x => (x.PartType & PartType.Arm) != 0))
                {
                    minscale = 0.13f;// dh bumped this up from 5 since creature explosion was reported.  before build 666 was 10 for all
                }

                //dh commented this section all out since Sumit reported creature explosion on his slow .. Harder to test since its framerate dependent.
                // it maybe take more than 2 frames to stabillize the body and joint graph..
                // int  realtimeFactor = ( int) (60 /  World.Instance.UpdatePerSecond);
                // frameCounterPerCycle = frameCounterPerCycle / realtimeFactor;
                // frameCounterPerCycle = Math.Max(2, frameCounterPerCycle);//DWI review is 2 frames stable enough for shrink i think you tuned this.

                //TODO maybe regrow faster if have eaten more..  or special fruit.. or  when tap .. but uses energy??   possible               
                //TODO can we shrink faster always.. then regrow at different speed..
                //have separate frameCountPerCycle.. shrink as fast as stable..

                //TODO tuning .. change to 0.06 from from 0.05 after buidl 669 ..   when its really small its can get unstable and break.
                // either do to mass items too small for physics to handle well.. ( or mass ratio) .   tryied 0.07 but it appear a bit too long too soon ..
                new RegenerateMissingBodyParts(this, RegrowKey, missingPartsForNewLFE, minscale, 1.0f, 0.05f, frameCounterPerCycle);
                //test for replacing with full scale, never seems a problem..
                //Effects.Add(new Grow(this, RegrowKey, missingPartsForNewLFE, continuePrevRegrow, 1f, 0.03f, 10));  
                //TODO check mass ratio on rescale of existing growing parts..
#endif

                //injuredSpirit.Plugin.UnLoaded();  //is done on delete Replace entity on delete..

                // use the same plugin as injured spirit
                Plugin = injuredSpirtPlugin;   //it might still hold old refs.
                Plugin.Parent = this;

                // use the same mind, rather than copy list of enemy, fear, threat, etc
                //body replaced, mind lives on ..

                //TODO this is dangerout as hell , can have old listeners and old refs to old spirit 

                //this shold all be deep cloned


                Mind = injuredSpirit.Mind;

                Plugin.UnLoaded(); 

                Mind.Parent = this;  // this is for Spirit refs in AI..  goes through Mind.Parent.

                Plugin.Loaded();  // loaded references Mind..   need to be careful loaded does not reset below prop
                CopyPropertiesFromOriginal(injuredSpirit);


            }
            catch (Exception ex)
            {

                Debug.WriteLine("regen based on injured "+ ex.ToString());
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private void CopyEffects(Spirit injuredSpirit)
        {
            foreach (Effect effect in injuredSpirit.Effects)
            {
                if (effect.Name == "swoon")  // this is for when a fainted creature starts to regrow..  other Effects that dont ref Bodies might be safe to copy over...
                {
                    effect.Parent = this;
                    Effects.Add(effect);  //at the end of this.. it sets origEnergy level, bug was full enery after knockout , then starts regrowing..
                }
            }
        }


        /// <summary>
        /// Call this from level listener, BEFORE injured spirit is unloaded from level.
        /// </summary>
        public void RestoreHeldItemsFromOriginal(IEnumerable<Body> heldBodiesFromInjuredSpirit, Sensor sensor, IPlugin<Spirit> oldplugin)
        {
            if (!heldBodiesFromInjuredSpirit.Any())
                return;


            var iattach = (oldplugin as IAttachItems);

            if (iattach == null)
            {
                Debug.WriteLine("not Iattachitem iface");
                return;
            }
            
            FarseerPhysics.Common.HashSet<Body> heldBodyHash = new FarseerPhysics.Common.HashSet<Body>();
            foreach (Body b in heldBodiesFromInjuredSpirit)
            {
                heldBodyHash.Add(b);
            }
            // because regen spirit haven't update its RefreshSensedObjectLists yet in here,
            // manually add heldbodies into sensor, so that it can be picked up by current spirit.
            // next cycle will reset RefreshSensedObjectLists with proper contents, should be no problem.
            RefreshSensedObjectLists(heldBodyHash, new List<Spirit>());

            foreach (Body b in heldBodyHash)  //TODO GAVITY dir use a listener or interface to plugin
            {
                // TODO: might also need to check pickup priority, or just copy priority from injured spirit.
                iattach.Attach(sensor, PartType.RightHand, b.PartType, 0.1f);
                iattach.Attach(sensor, PartType.LeftHand, b.PartType, 0.1f);
            }

            /*  //TODO future , better to filter the grab in case its fighting close grab the wrong thing ..
               if (injuredSpirit.HeldBodyRight != null)         
               Attach(sensor, PartType.RightHand, injuredSpirit.HeldBodyRight.PartType, 0.3f);       

             if (injuredSpirit.HeldBodyLeft != null)
                Attach(sensor, PartType.LeftHand, injuredSpirit.HeldBodyLeft.PartType, 0.3f);*/
        }


    

        //spirit spirit.spr is just used for replacement body and dress from the species..  
        //copy the rest of properties which may not be set in spr.
        private void CopyPropertiesFromOriginal(Spirit injuredSpirit)
        {
            try
            {
                // copy pose, active behavior, etc, from injured spirit

                if (injuredSpirit.ActiveBehavior != null)
                {
                    ActiveBehavior = Behaviors[injuredSpirit.ActiveBehavior.Name];
                }

                if (injuredSpirit.NextBehavior != null)
                {
                    NextBehavior = Behaviors[injuredSpirit.NextBehavior.Name];
                }

                CurrentTime = injuredSpirit.CurrentTime;
                // copy energy too, or else spirit will instantly have full energy after regrow.
                EnergyLevel = injuredSpirit.EnergyLevel;

                IsCallingPlugin = injuredSpirit.IsCallingPlugin;  //easy to forget to set this on import new dress.
                PluginName = injuredSpirit.PluginName;
                PluginScript = injuredSpirit.PluginScript;
                Name = injuredSpirit.Name;
                Tribe = injuredSpirit.Tribe;
                SpiritFilename = injuredSpirit.SpiritFilename;
                AABB = injuredSpirit.AABB;

                World = injuredSpirit.World;

                //if holding arms up or something, set angles.  
                for (int i = 0; i < injuredSpirit.Joints.Count(); i++)
                {
                    if (injuredSpirit.BodySet.Contains(injuredSpirit.Joints[i].BodyA) &&
                         injuredSpirit.BodySet.Contains(injuredSpirit.Joints[i].BodyB))
                    {
                        Joints[i].TargetAngle = injuredSpirit.Joints[i].TargetAngle;
                    }
                }

                // for each body copy visible marks 
                CloneBodyVisibleMarksByPartType(injuredSpirit);
            }
            catch(Exception exc)
            {

                Debug.WriteLine("error in copy props injured to new");

                Debug.WriteLine(exc.Message);


            }
        }



        //TODO can remove the neck  when all the files and spr.  have marked Upper and LowerNeck.
        //But doesnt matter now.. this is currently used for regrow, and if any part of neck is missing its dead.

        /// <summary>
        /// Compare _bodies and injuredParts, return list of bodies that didn't exist on injuredParts.
        /// </summary>

        private FarseerPhysics.Common.HashSet<Body> GetMissingBodyPartsExludingNeck(Dictionary<PartType, Body> injuredParts)
        {
            FarseerPhysics.Common.HashSet<Body> missingParts = new FarseerPhysics.Common.HashSet<Body>();
            foreach (Body currentBodyPart in _bodies)
            {
                if (currentBodyPart.PartType == PartType.None
                    || (currentBodyPart.PartType & PartType.Neck) != 0)  // fix for regrow on  old files.. neck was not marked with upper and lower
                                                                         // its safe to do this since we never regrow neck.  its dead if any of that is missing
                {
                    continue;
                }

                Body missingBodyPart;
                if (injuredParts.TryGetValue(currentBodyPart.PartType, out missingBodyPart) == false)
                {
                    missingParts.Add(currentBodyPart);
                }
            }

            return missingParts;
        }


        /// <summary>
        /// Get RegrowingParts from the LFE of soon-to-be-removed injured spirit, correct its Body reference to current spirit based on PartType.
        /// </summary>
        private Dictionary<Body, float> GetRegrowingBodiesFromInjuredSpiritBasedOnPartType(Spirit injuredSpirit, string regrowKey)
        {
            RegenerateMissingBodyParts injuredGrowLFE = null;
            foreach (Effect effect in injuredSpirit.Effects)
            {
                if (effect.Name == regrowKey)
                {
                    injuredGrowLFE = effect as RegenerateMissingBodyParts;
                    if (injuredGrowLFE != null)
                    {
                        break;
                    }
                }
            }

            // always return a list even when empty
            Dictionary<Body, float> regrowingPartsForCurrentSpirit = new Dictionary<Body, float>();

            if (injuredGrowLFE != null)
            {
                MapInjuredPartsByPartType(injuredGrowLFE, regrowingPartsForCurrentSpirit);
            }

            return regrowingPartsForCurrentSpirit;
        }

        private void MapInjuredPartsByPartType(RegenerateMissingBodyParts injuredGrowLFE, Dictionary<Body, float> regrowingPartsForCurrentSpirit)
        {
            // because grow lfe references to body, we use PartType to transfer grow lfe between spirit.
            // duplicate body with same PartType will not work using this method.
            Dictionary<PartType, float> injuredGrowLFEPartTypeMap = new Dictionary<PartType, float>();
            foreach (KeyValuePair<Body, float> pair in injuredGrowLFE.ScalingPartScaleMap)
            {
                if (injuredGrowLFEPartTypeMap.ContainsKey(pair.Key.PartType) == false)
                {
                    injuredGrowLFEPartTypeMap.Add(pair.Key.PartType, pair.Value);
                }
            }

            foreach (Body b in _bodies)
            {
                float growValue;
                if (injuredGrowLFEPartTypeMap.TryGetValue(b.PartType, out growValue) == true)
                {
                    regrowingPartsForCurrentSpirit.Add(b, growValue);
                    injuredGrowLFEPartTypeMap.Remove(b.PartType);   // prevent duplicate bodies with same PartType use growValue
                }
            }
        }


        private void CheckRegrowOnJointBreaking()
        {
            RegenerateMissingBodyParts regrowEffect = Effects.OfType<RegenerateMissingBodyParts>().FirstOrDefault();
            if (regrowEffect != null)
            {
                UpdateStillConnectedRegrowParts(regrowEffect);
            }

            // always do regrow when joint break, but only if head is still intact
            if (Head != null && !IsDead)
            {
                // regen parts will create grow delay by default.
                StartNewDelayBeforeRegrow();
            }
        }


        private void UpdateStillConnectedRegrowParts(RegenerateMissingBodyParts regrowEffect)
        {
            // check if any growing parts got severed, don't continue grow if it separated from main body.
            Dictionary<Body, float> stillconnectedGrowingPieces = new Dictionary<Body, float>();
            //   IEnumerable<Body> stillconnectedGrowingPieces =  regrowEffect.RegrowingParts.Intersect<Body>(Bodies);
            //TODO why doesnt above, work.. i tthink  Body would  need IComparable

            foreach (KeyValuePair<Body, float> pair in regrowEffect.ScalingPartScaleMap)
            {
                if (BodySet.Contains(pair.Key))  //make sure growning parts still connected to  us ( like has blood flow to grow) 
                {
                    stillconnectedGrowingPieces.Add(pair.Key, pair.Value);
                }
            }

            regrowEffect.ScalingPartScaleMap.Clear();

            foreach (KeyValuePair<Body, float> pair in stillconnectedGrowingPieces)
            {
                regrowEffect.ScalingPartScaleMap.Add(pair.Key, pair.Value);
            }
        }


        protected void OnGrowDelayEnded(Effect effect)
        {
            RequestSpiritRegrow();
        }


        /// <summary>
        /// Request spirit regrow to level so they send original spirit template for this spirit through RegenBasedOnInjuredSpirit
        /// </summary>
        private void RequestSpiritRegrow()
        {
            if (Head == null || IsDead || IsHavingSeizure)// can't regrow head.. or regrow during seizure.. could be dying
                return;


            // just add this spirit to list of level spirit to be regenerate.
            Level.DelayedEntityList.Enqueue(
                new KeyValuePair<IEntity, Core.Data.EntityOperation>(
                    this, Core.Data.EntityOperation.SpiritRegenerate));
        }

#endregion

        //TODO cleanup move to some helper class not this
        /// <summary>
        /// give length from first attach point to first 
        /// only valid for weapon with one handle and one point like sword.
        /// </summary>
        /// <param name="body"></param>
        /// <returns>length</returns>
        public static double MeasureSwordLength(Body body)
        {
            return Math.Sqrt(MeasureSwordLengthSq(body));
        }

        public static double MeasureSwordLengthSq(Body body)
        {
            if (body == null || body.AttachPoints.Count != 1 || body.SharpPoints.Count != 1)//only valid for simple swords .
                return 0;

            return (body.AttachPoints[0].WorldPosition - body.SharpPoints[0].WorldPosition).LengthSquared();
        }

        //try for bones or sticks that have an attack point on either  end for holding
        public static float MeasureHoldableStickLength(Body body)
        {
            if (body == null || body.AttachPoints.Count != 2)
                return 0;

            return (body.AttachPoints[0].WorldPosition - body.AttachPoints[1].WorldPosition).Length();
        }

        public FarseerPhysics.Common.HashSet<Body> GetOurIgnoredBodiesLOS()
        {
            FarseerPhysics.Common.HashSet<Body> ignoredBodies = new FarseerPhysics.Common.HashSet<Body>();
            Bodies.ForEach(x => ignoredBodies.Add(x));
            HeldBodies.ForEach(x => ignoredBodies.CheckAdd(x));
            return ignoredBodies;
        }

        protected bool IsClearLOSHeadToAttachPoint(Sensor sensor, AttachPoint atc)
        {
            FarseerPhysics.Common.HashSet<Body> ignoredBodies = GetOurIgnoredBodiesLOS();
            ignoredBodies.Add(atc.Parent);
            return (sensor.AddRay(Head.WorldCenter, atc.WorldPosition, atc.Parent.GetHashCode().ToString() + MainBody.GetHashCode().ToString(),
                ignoredBodies, new BodyColor(20, 20, 255, 255), true, true).IsIntersect == false);
        }

#region AIstuff
        public bool IsBetweenUs(Vector2 point, Spirit otherSpirit)
        {
            return ((WorldCenter.X > point.X && point.X > otherSpirit.WorldCenter.X)
                || (WorldCenter.X < point.X && point.X < otherSpirit.WorldCenter.X)
                );
        }



        /// <summary>
        /// other Sprit is between This  and friend.
        /// </summary>
        /// <param name="otherSpirit">xcommon enemy</param>
        /// <param name="friend">our comrade in battle</param>
        /// <returns>truen if other Sprit is between This  and friend.</returns>
        public bool IsBetweenUs(Spirit otherSpirit, Spirit friend)
        {
            return IsBetweenUs(otherSpirit.WorldCenter, friend);
        }

        /// <summary>
        /// GetPickableWeaponCloserToMeThanHim.. 
        /// for racing to a weapon lying between me and enemy.   we are still agressive to other creature if we think the sword is closer to us.
        /// </summary>
        public void GetPickableWeaponCloserToMeThanHim(Sensor sensor, ref int aggression, ref int fear, Spirit otherSpirit, float reachDistance)
        {

            Mind.UpdateClosestPickable(otherSpirit.IsHoldingGun(true) || otherSpirit.IsHoldingGun(false));  //Im not sure about all this..
            AttachPoint weaponAtc = Mind.ClosestPickableWeaponGrip;
            //TODO closest by range..gun, sword, knife..

            bool weaponIsGun = weaponAtc.Parent.IsInfoFlagged(BodyInfo.ShootsProjectile);

            bool weaponIsLeft = IsLeftOfUs(weaponAtc.WorldPosition);
            bool weaponIsLeftFromOtherSp = otherSpirit.IsLeftOfUs(weaponAtc.WorldPosition);

            Body leftHand = GetGrabCapableHand(true);
            Body rightHand = GetGrabCapableHand(false);

            Body leftHandOtherGuy = otherSpirit.GetGrabCapableHand(true);
            Body rightHandOtherGuy = otherSpirit.GetGrabCapableHand(false);

            // Is hand available on correct side to pickup weapon ?

            // 1. Hand available on pickup side
            if (weaponIsLeft && leftHand != null || !weaponIsLeft && rightHand != null)
            {
                //TODO it should have a  different  reach ReachDistance on closest shoulder to  attach handle, and angle,
                //mabye even using LOS at this point in logic also check yndrd MainAI loop 

                float ourDist = Mind.DistanceFrom(weaponAtc); // this is main body CM to item

                //// pickup range used in mind, slightly differs from spirit
                //float pickupRange = Parent.PickupRange + (0.5f * Parent.AABB.Width);

                // Run to weapon if equal dist or closer than enemy, else fear++.
                // Run until weapon is inside pickup range (dist <= spirit.PickupRange),
                // then execute Pickup().

                // 1.1  Pickable weapon out of reach.
                if (ourDist > reachDistance)
                {
                    // 1.1.1  We think that we are closer to weapon than enemy 
                    // So run to weapon.  //TODO check slope ... climbing is slower.   check their speed.
                    if (ourDist <= otherSpirit.Mind.DistanceFrom(weaponAtc))
                    {
                        if (IsBetweenUs(weaponAtc.WorldPosition, otherSpirit) && IsClearLOSHeadToAttachPoint(sensor, weaponAtc))
                        {
                            aggression += 1;
                        }
                        else  // this will run toward weapon by running away from the guy.. NOTE.. works, but its kind of a hack.
                        {
                            fear += 1;
                        }
                    }
                    // 1.1.2  We think enemy is closer to weapon, 
                    // so dont try running to it, he could beat us..
                    else
                    {
                        // Since swordsman also treat weaponless spirit as enemy, must check if enemy hold weapon. 

                        // Does enemy hold and point weapon to us ?                 
                        // 1.1.2.1 Yes, Just add fear then, on next cycle we'll runaway from other spirit.

                        if (IsSpiritHoldingLongRangeWeaponOnSideTowardsUs(otherSpirit))  //TODO shot gun.. highest fear.
                        {
                            fear += 9;
                        }
                        else
                        if (IsSpiritHoldingWeaponOnSideTowardsUs(otherSpirit) == true)
                        {
                            fear += 6;  //will probably runaway.. he can reach weapon before me.      and he already has a sword picked up.                                
                        }
                        else
                           if ((weaponIsLeft && rightHandOtherGuy != null || !weaponIsLeft && leftHandOtherGuy != null)//is  His Hand is available on pickup side      ?
                           && IsBetweenUs(weaponAtc.WorldPosition, otherSpirit))
                        {
                            fear += 3;
                        }
                        else
                        {
                            //probably attack.. he is wounded and can't use sword on that side.
                            //TODO  we could do potentially armed.. Us and them,  instead of this..  who is armed more, or potentailly armed more?    
                            //measure ourselves and them who is armed, or potentially better armed..( how close are they to a weapon)  
                            //are we running to a knife while they are running to a long sword??   
                            aggression += 3;
                        }
                    }
                }     // 1.2  Pickable weapon in reach    
            }

            // 2. Hand not available on pickup side
            else
            {
                // just normal run away then..if he can pick up and  the sword from that side
                if (SpiritHasGrabCapableHandTowardsUs(otherSpirit))
                {// TODO should check if he got legs .. otherwise we should go over and beat him to death..
                    fear += 6;
                }
            }
        }

        public bool SpiritHasGrabCapableHandTowardsUs(Spirit otherSp)
        {
            return (otherSp.GetGrabCapableHand(!IsLeftOfUs(otherSp.WorldCenter)) != null);
        }

        public bool IsAtOurVerticalLevel(Spirit othersp, float yTolerance)
        {
            return (Math.Abs(othersp.MainBody.WorldCenter.Y - MainBody.WorldCenter.Y) < yTolerance);
        }

        public bool IsSpiritHoldingWeaponOnSideTowardsUs(Spirit othersp)
        {
            return IsSpiritHoldingWeaponOnSideTowardsUs(othersp, AABB.Height);
        }


        /// <summary>
        /// Check if left of us and hold weapon right, or right of us and hold weapon left.
        /// </summary>
        /// <param name="othersp"></param>
        /// <param name="yTolerance"></param>
        /// <returns></returns>
        public bool IsSpiritHoldingWeaponOnSideTowardsUs(Spirit othersp, float yTolerance)
        {
            bool leftOfUs = IsLeftOfUs(othersp.MainBody.WorldCenter);

            //TODO GUN .. if ray is clear don't care the level.  for sniper..  might need find cover..
            return (IsAtOurVerticalLevel(othersp, yTolerance) && othersp.IsHoldingWeapon(!leftOfUs));
        }


        /// <summary>
        /// Check if left of us and hold weapon right, or right of us and hold weapon left.   now guess 30 meters above or below
        /// </summary>
        /// <param name="othersp"></param>
        /// <returns></returns>
        public bool IsSpiritHoldingLongRangeWeaponOnSideTowardsUs(Spirit othersp)
        {
            bool leftOfUs = IsLeftOfUs(othersp.MainBody.WorldCenter);

            //TODO GUN .. if ray is clear don't care the level.  for sniper..  might need find cover..  
            //anyways if he has a Gun an we AI sees or hears it.. hes on alert
            return (IsAtOurVerticalLevel(othersp, 30) && othersp.IsHoldingGun(!leftOfUs));
        }



        public bool IsSpiritPunchingAtUs(Spirit othersp, float reachDistance)
        {

            if (othersp.Mind.DistanceFrom(this) > reachDistance)
                return false;

            bool leftOfUs = IsLeftOfUs(othersp.Position);

            // left of us and hold weapon right, or right of us and hold weapon left.
            bool isPunchingAtUs = (
                (leftOfUs && othersp.IsThrustingRight) ||
                (!leftOfUs && othersp.IsThrustingLeft)
                );

            return isPunchingAtUs;

        }

#endregion

        public void SetBiasOnRegeneratingParts(float factor)
        {
            if (GetRegrowEffect() == null)
                return;

            foreach (Body b in Bodies)
            {
                float scale = 1;
                GetRegeneratingScale(b, out scale);

                if (scale < 0.15f)
                {
                    SetBiasAllConnectedJoints(factor, b);
                }
            }
        }

        private static void SetBiasAllConnectedJoints(float factor, Body b)
        {
            for (JointEdge jn = b.JointList; jn != null; jn = jn.Next)
            {
                PoweredJoint pj = jn.Joint as PoweredJoint;
                if (pj != null)
                {
                    pj.BiasFactor = factor;
                  }
            }
        }

        ///// <summary>
        ///// should give angular vel of main body.. whole system should average out..
        ///// since its a joint  graph, the AngularVelocity of any one body is not stable, needs averaging.
        ///// Notes.. not really usefull.. aver on main body might be
        ///// </summary>
        ///// <returns></returns>
        //public float GetAverageAngularVel()
        //{
        //    float totalAngularVel = 0;
        //    Bodies.ForEach(x => totalAngularVel += x.AngularVelocity);
        //    return (totalAngularVel / Bodies.Count);
        //}

#region IEntity Members


        /// <summary>
        /// Returns center of mass of this spirit.
        /// This is used for AI moving towards it.. might be changed to World center of main body if more practical.
        /// </summary>
        public Vector2 WorldCenter
        {
            get { return _worldCenter; }
        }

        /// <summary>
        /// The Displacement  of the center of the mass of this system since last frame.  since its a whole system.. doesn't seem to need averaging or smoothing, as any one body can shake
        /// </summary>
        public Vector2 WorldCMDisplacementPerFrame
        {
            get
            {
                return (_worldCenter - _worldCenterPrev);
            }
        }


        /// <summary>
        ///  The velocity of the CM, usually just below groin on biped,  should not  need averaging or smoothing, as any one body can shake
        /// </summary>
        public Vector2 WorldCMLinearVelocity
        {
            get
            {
                return (_worldCenter - _worldCenterPrev) / World.DT;
            }
        }

        /// <summary>
        ///  The velocity of the CM, usually just below groin on biped,  should not  need averaging or smoothing, as any one body can shake
        /// </summary>
        public Vector2 LinearVelocity
        {
            get
            {
                return WorldCMLinearVelocity;
            }
        }

        public Vector2 LinearAcceleration
        {
            get  => LinearVelocity - _prevLinearVelocity;           
        }



        public Body BodyNearestToCenterMass { get; set; }  //TOOD remove this and calculate it better..add some moment around pt maybe if useful

        /// <summary>
        /// Approx angular vel, using BodyNearestToCenterMass, set by plugin.
        /// </summary>
        public float AngularVelocity
        {
            get
            {
                if (BodyNearestToCenterMass == null)
                { //TODO for spine creature it is abdomen.. , or should search and cache.but main body is high up on shoulders..TODO use moment data..speed around pivot, and calc angular vel avg bodies, tangent aroudn cm,  /3.14
                  //the best answer if we really need i think is to integrated like we do for CM position .  or find a body or several ( then average)  and see its tangential vel relative to cm , then divide by 

                    BodyNearestToCenterMass = MainBody;
                }

                return BodyNearestToCenterMass.AngularVelocity;
            }

        }

        public int ID
        {
            get
            {
                return MainBody.ID;
            }
        }

        public ViewModel ViewModel => ((IEntity)_mainBody).ViewModel;

        public Transform Transform => ((IEntity)_mainBody).Transform;

        public IEnumerable<IEntity> Entities => Bodies;

        public byte[] Thumbnail => null;
#endregion



        /// <summary>
        /// Only for traveler spirit.
        /// </summary>
        public void ResetTravelerPhysicsWorldState()
        {
            Bodies.ForEach(x => x.ResetStateForTransferBetweenPhysicsWorld());
            Joints.ForEach(x => x.ResetStateForTransferBetweenPhysicsWorld());
            FixedJoints.ForEach(x => x.ResetStateForTransferBetweenPhysicsWorld());
            ResetTravelerAttachedItemJoints();


            ReleaseFixtureListeners();
            ReleaseListeners();
            AttachFixtureListeners();
            AddJointEventHandlers();
        }


        /// <summary>
        /// Only for traveler spirit.
        /// </summary>
        private void ResetTravelerAttachedItemJoints()
        {
            // only for minded spirit that can grab stuff
            if (!IsMinded)
                return;

            foreach (Body b in Bodies)
            {
                b.AttachPoints.ForEach(x => { if (x.Joint != null) x.Joint.ResetStateForTransferBetweenPhysicsWorld(); });
            }
        }



        public void ForceStatic(bool isStatic)
        {
            Bodies.ForEach(x => x.IsStatic = isStatic);
        }


        //TODO move to  biped..
        public void OpenEyesDress(Effect effect)
        {
            if (Head != null && !string.IsNullOrEmpty(Head.DressingGeom2))
            {
                Head.IsShowingDress2 = false;
            }
        }

        //this assumes dress 2 is blinked eye on head
        public void CloseEyesDress()
        {
            if (Head != null && !string.IsNullOrEmpty(Head.DressingGeom2))
            {
                Head.IsShowingDress2 = true;
            }
        }


        public void BlinkEyes()
        {
            const string blink = "Blink";
            if (Head != null && !string.IsNullOrEmpty(Head.DressingGeom2))
            {
                if (!Effects.Contains(blink))
                {
                    Delay blinkEffect = new Delay(this, blink, (double)MathUtils.RandomNumber(0.15f, 0.3f));
                    CloseEyesDress();
                    blinkEffect.OnEndEffect = OpenEyesDress;
                }
            }
        }




        /////NOTES  seem  less bouncy with this.. tried 1 , doesn't damp much.       making value higher than say 10000 doesnt help.  in tool you can see the effect, if setting softness to 1 or bias to zero.. can optain critical damping. 
        //with Bias = 0.3  , this damping appears to help prevent over compensation and falling back..
        //SetAnkleDamping(200000); // can't do both..   cant do knee and hip either .. unstable .. starts widly moving
        //also, the revolute joint will change in 3.5.. not much point to tune this perfectly
        public void SetJointDampingMotor(int index, float maxTorque)
        {
            Joints[index].SetMotorDamping(maxTorque);
        }

        /// <summary>
        /// Bias and motor damping are closely related in farseer 3.1.. Notes.
        /// Notes:
        /// tuning bias..0.1 doesn't restore fast enough.  0.3 or above ( maybe below) .. damping has no effect
        ///0.28 , and 0.29.. very little damping happen.
        /// </summary>
        /// <param name="index">joint index</param>
        /// <param name="maxTorque">maxTorque, high is more damping 0.1 to 2 or so</param>
        /// <param name="bias">bias, must be lower than 0.3 or wont work</param>
        public void SetJointDampingMotor(int index, float maxTorque, float bias)
        {
            Joints[index].SetMotorDamping(maxTorque);
            Joints[index].BiasFactor = bias;
        }

        public void SetToStatic()
        {
            FixedJoints.ForEach(x => x.Enabled = false);
            Joints.ForEach(x => x.Enabled = false);
            Bodies.ForEach(x => x.IsStatic = true);
        }


        /// <summary>
        /// on Bodies with dress , remove the wire frames, Edges, useful in tool.
        /// </summary>
        public void RemoveWireFrames()
        {
            foreach (Body b in Bodies)
            {
                if (!string.IsNullOrEmpty(b.DressingGeom))
                {
                    b.EdgeStrokeThickness = 0;
                    Level.CacheUpdateEntityView(b, 0);
                }
            }
        }



        public static void WakeJoinedBodies(Joint joint)
        {
            if (joint != null)
            {
                if (joint.BodyA != null)
                {
                    joint.BodyA.Awake = true;
                }

                if (joint.BodyB != null)
                {
                    joint.BodyB.Awake = true;
                }
            }

        }


        /// <summary>
        /// Because of a complex joint graph under gravity strain, the normal per-body sleep is not enough , it never sleeps.
        /// this is a way to relax the requirement, force it to sleep, then it will stay that way untill a part is woken.
        /// </summary>
        /// <returns>true if sleeping</returns>
        public bool CheckToSleepBodySystem()
        {
            if (Level.ActiveSpirit == this)  //don't sleep our main character.  also usefull to compare creature performance in physics update
                return false;

            if (AverageMainBodySpeedSq != 0 &&
            Math.Abs(AverageMainBodySpeedSq) < 0.13f &&
            Math.Abs(MainBody.AngularVelocity) < 0.3f)
            {
                MainBody.Awake = false;
                ResetMainBodyAveSpeed();
            }

            if (MainBody.Awake == false)
            {
                foreach (Body b in Bodies)
                {
                    b.Awake = false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ReleaseListeners()
        {
            World = null;
            ReleaseFixtureListeners();
            Bodies.ForEach(body => body.AttachPoints.ForEach(x => x.Detached -= OnAttachPointDetached));
            DisposeSensor();

            if (PlaceJointBreakHanders)
            {
                RemoveJointEventHandlers();
            }

        }

        public void ReleaseFixtureListeners()
        {
            Bodies.ForEach(body => body.OnCollision -= OnCollisionEventHandler);
        }

        public void Draw(double dt)
        {

            if (Plugin != null)
            {
                Plugin.Draw(dt);
            };
        }

        public void UpdateThreadSafe(double dt)
        {

            if (Plugin == null)
                return;

            // always called every update.
            if (_isCallingPlugin)
            {
                try
                {
                    Plugin.UpdatePhysicsBk(dt, null);
                }
                catch (Exception ex)
                {
                    if (Spirit.OnSpiritException != null)
                    {
                        Spirit.OnSpiritException(this, ex, "Spirit.Plugin.UpdatePhysicsBk()");
                    }
                    else
                    {
                        Debug.WriteLine("Exception  in UpdatePhysics in Parellel Loop" + ex.Message + ex.StackTrace);
                    }
                }

            }
        }

        public object Clone()
        {//NOTE we only clone to draw during physkcsx update.. the draw code is only iin plugins for now.
            //so dont need to clone everything
         
            if (Plugin is ICloneable)
            {
          
                Spirit clone = this.MemberwiseClone() as Spirit;

                clone.Plugin = (Plugin as ICloneable).Clone() as IPlugin<Spirit>;
                return clone;
            }
         
            return null;
        }



        /// <summary>
        /// Modes how spirit will play the keyframe.. most are using Repeat, and in walk cycle. TODO document others
        /// Plugin has done alot with Poses, bypassing much  of the interpolation in here
        /// </summary>
        public enum SpiritPlay
        {
            OneTime,
            Repeat,
            TransitionFromSourceToTarget,  // NOTE TODO  this is unused, and no differnet than TransitionToTarget
            TransitionToTarget// // NOTE TODO  this is untested, , and no differnet than TransitionToTarget
        }



    }
}







