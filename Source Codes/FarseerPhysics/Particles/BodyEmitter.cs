#define VisualizeParticles
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Factories;
using FarseerPhysics.Collision;
using FarseerPhysics.Controllers;

using System.Threading.Tasks;
using System.ComponentModel;

namespace FarseerPhysics.Dynamics.Particles
{
    //TODO on properties  use  member body as template and clone
    /// <summary>
    /// Emitter type that will emits circle bodies
    /// </summary>
    [DataContract]
    public class BodyEmitter : Emitter
    {

        #region Delegates


#if XAMLCACHE

        /// <summary>
        /// Spawn Entity Handler
        /// </summary>
        /// <param name="emitter">This emitter</param>
        /// <param name="XMLString">The deserialized Entity strin UNUSED </param>
        /// <param name="worldForce">The world force need to be applied to all body in the Entity </param>
        /// <param name="lifeSpan">The lifespan in msec</param>
        public delegate void OnSpawnEntityEventHandler(BodyEmitter emitter, string XMLString, Vector2 worldForce, Vector2 velocity, double lifeSpan);
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="emitter"></param>
        /// <param name="worldForce"></param>
        /// <param name="velocity"></param>
        /// <param name="lifeSpan"></param>
        public delegate void OnSpawnEntityEventHandler(BodyEmitter emitter, Vector2 worldForce, Vector2 velocity, double lifeSpan);

        /// <summary>
        /// delegate for OnSpawnCachedEntity
        /// </summary>
        /// <param name="emitter"></param>
        /// <param name="entity">entity cache from emitter.</param>
        /// <param name="worldForce"></param>
        /// <param name="velocity"></param>
        /// <param name="lifeSpan"></param>
        public delegate void OnSpawnCachedEntityEventHandler(BodyEmitter emitter, IEntity entity, Vector2 worldForce, Vector2 velocity, double lifeSpan);

        public static OnSpawnEntityEventHandler OnSpawnEntity = null;
        public static OnSpawnCachedEntityEventHandler OnSpawnCachedEntity = null;

#if XAMLCACHE
        public static Func<BodyEmitter, string> OnEntityXAMLRead = null;
#endif
        /// <summary>
        /// Fires when emitter is about to preloading entity. Accept bodyemitter sender.  Returns entity object.
        /// </summary>
        public static Func<BodyEmitter, IEntity> OnEntityPreLoad = null;

        public static Func<BodyEmitter, Particle, bool> OnSpawnParticle = null;




        public delegate void OnAddParticleStrikeMarkHandler(Particle particle, Vector2 normalAtCollision, Body struckBody, Vector2 intersection, float fraction);
        public static OnAddParticleStrikeMarkHandler OnAddParticleStrikeMark = null;

        public Action<BodyEmitter, FieldParticle> OnSpawnFieldParticle;


        /// <summary>
        /// Sent before partcles are created so that Grid based Fluid can use this for adding flow and density to its grid, usually with SkipSpawnParticle to false
        /// </summary>
        public Action<BodyEmitter, Vector2 , float > OnPreEmission;

        public Action<Particle> OnThisSpawnParticle;  //non static version for gun, with pellets, 10 at a time..TODO change to non static for all..


        static short collisionGroup = 0;

        public Action OnUnload;

      

#endregion

        private static short GetNextGasCollisionGroup()  //static so every spirit instance will have a unique value.
        {
            collisionGroup += 1;
            return collisionGroup;
        }



        //   public CollisionFilter collisionFilter;
        public Category CollidesWith { get; set; }




        /// <summary>
        /// Cache of entity to be emitted next
        /// This is used to fix issue with slow emitting when using cached xaml, most probably because of slow xaml string parsing.
        /// Currently only used by bullet.
        /// </summary>
        protected IEntity _entityCache = null;

#region MemVars & Props

        public IEntity LastEntityLoaded;

        /// <summary>
        /// So that client can adjust it.. say .. put grey  stroke to see at night..  or on some bullets.
        /// </summary>
        public IEntity NextEntityToEmit
        {
            get
            {
                return _entityCache;
            }
            set
            {
                _entityCache = value;
            }
        }

        //TODO FUTURE .. to use this must have  public parameterless constructor , set world later
        // private ParticleGenerator<Particle> particles = new ParticleGenerator<Particle>();

        /// <summary>
        /// How many particles emitted per cycle
        /// </summary>
        [DataMember]
        public int ParticleCountPerEmission { get; set; }

        [DataMember]
        public float OscillationPeriod { get; set; }// period  in millsec
        //     public short FrameCountPerScale;  //skip frames.   dont need to  blood particles move like .3 meter per frame.. mabye as a functino of speed

        [DataMember]
        public float MagnitudeAspectOscillation { get; set; }  //sin Oscillation for liquid blob as it updates.

        [DataMember]
        public float OscillationPeriodDeviation { get; set; }




        //TODO consider breaking out super props into struct if too many.. but tree view

        /// <summary>
        /// for Particles that represent a cloud of particles, here the shape that will repel the neighbors, square or circle
        /// circle can transfer curl , (vorticity) via friction, squares can pack better.  Note gas forces must not act trough walls
        /// with enough temperature , a new light source will be generated, only bodies lit by visibility polygons receive forces..
        /// or.. gas outer geom can collide with walls... and grow.. or  we cast a ray from the small particle to the object
        /// if it touches then
        /// </summary>
        [DataMember]
        public FieldParticle.Shape SuperShape { get; set; }

        /// <summary>
        /// The outer size of this particle used to deflect  gas particles in its group.   This may Oscillate  if allowed to collide with everything..
        /// </summary>
        [DataMember]
        public float SuperSize { get; set; }

        /// <summary>
        /// randow deviation factor.. not like legacy Size.. this is a factor of the size.
        /// </summary>
        [DataMember]
        public float SuperSizeDeviation { get; set; }

        /// <summary>
        /// related to collision size..usually bigger.. 1.5 means R = 1.5 ad supersize * deviation
        /// </summary>
        [DataMember]
        public float FieldSizeFactor { get; set; }



        private int _maxNumberOfParticles { get; set; }
        /// <summary>
        /// How many particles this emitter can shoot before Dead.
        /// Zero means unlimited, for the sake of old files and there is no Infinity value for int.
        /// </summary>
        [DataMember]
        public int MaxNumberOfParticles
        {
            get { return _maxNumberOfParticles; }
            set
            {
                _maxNumberOfParticles = value;
                _numParticlesLeft = _maxNumberOfParticles;
            }
        }

        public bool SkipPreload { get; set; } = false;

        private int _numParticlesLeft;
        /// <summary>
        /// Current number of unshot particles.  Read only.
        /// Value is set by MaxNumberOfParticles, can never become larger than MaxNumberOfParticles.
        /// </summary>
        public int NumParticlesLeft
        {
            get { return _numParticlesLeft; }
        }


        /// <summary>
        ///  size
        /// </summary>
        [DataMember]
        public float Size { get; set; }

        /// <summary>
        /// The End size of the Particle near its final age
        /// </summary>
        //  [DataMember]
        //  public float EndSize { get; set; }

        /// <summary>
        /// Force to propell particle on emission
        /// </summary>
        [DataMember]
        public float EmissionForce { get; set; }

        /// <summary>
        /// Life Span of particles in millisec
        /// </summary>
        [DataMember]
        public float LifeSpan { get; set; }

        /// <summary>
        ///set Mass directly can override that set by density
        /// </summary>
        [DataMember]
        public float Mass { get; set; }

        [DataMember]
        public float Density { get; set; }


        /// <summary>
        /// Determine IsBullet property of emitted Body.
        /// </summary>
        [DataMember]
        public bool IsBullet { get; set; }

        /// <summary>
        /// Partially implemented, idea is to add features like vorticiity by using existing broad phase, adding particles which show a velocity field
        /// For this to work, would need to render the particles velocity contribution to a parents grid, finding these particles for a location, using the broad phase rather than simply combining all,  would need to be implemented
        /// as a form of adaptive grid refinement, then add or solve them together
        /// </summary>
        [DataMember]
        public bool IsFieldParticle { get; set; }


        /// <summary>
        /// Restitution of Particles
        /// </summary>
        [DataMember]
        public float Restitution { get; set; }


        [DataMember]
        public float DragDeviation { get; set; }

        /// <summary>
        /// Friction of Particles
        /// </summary>
        [DataMember]
        public float Friction { get; set; }

        //   http://en.wikipedia.org/wiki/Drag_(physics).   dimensionless factor how much air and drag affects particle
        // also affected by size and vel  square.. set to a  - number and paricle will not be affected by wind or  air   
        [DataMember]
        public float DragCoefficient { get; set; }



        [DataMember]
        public float AngularDamping { get; set; }


        //if false  will check for wind field blockage 
        [DataMember]
        public bool SkipWindBlockCheck { get; set; }

        /// <summary>
        /// if Is is NotCollidable , dont even check usnig  rays.. faster for  lots of rain  with no creature around..
        /// </summary>
        [DataMember]
        public bool SkipRayCollisionCheck { get; set; }

        //   [DataMember]
        //  public bool DoRayTraceCheckIfClear { get; set; }  ///TODO dont emit particle if theres not enough space for it..
        //check if clear , and use particle size..( maybe velocity as well, avoid tunneling)
        //use for ballon fire emitter....

        /// <summary>
        /// If true, the wind flow will treat this body as a particle, skip the verices..
        /// </summary>
        [DataMember]
        public bool ApplyDragAsParticle { get; set; }

        [DataMember]
        public bool FixedRotation { get; set; }

        /// <summary>
        /// for Bodies ( for  Spirit emitission)  apply a rotation on emission.   bodies will not be rotated if zero.
        /// </summary>
        [DataMember]
        public Nullable<float> Rotation { get; set; }

        /// <summary>
        /// some particles like rain are better in the backgroudn so they don't appear to penetrate
        /// </summary>
        [DataMember]
        public int ZIndex { get; set; }

        //TODO gun  reuse as a reference for bulletxx.body file..
        protected string _spiritResource;

        /// <summary>
        /// Name of embedded resource, the one that will be emitted by this emitter.
        /// Now used for  spirit  .spr , sprx ( binary xml of spirit) and also .body files.
        /// FUTURE TODO: rename to EntityResource   but its a  pain for old files.
        /// </summary>
        [DataMember]
        public string SpiritResource
        {
            get
            {
                return _spiritResource;
            }
            set
            {

                if (_spiritResource != value)
                {
                    _spiritResource = value;

                    _entityCache = null; //force a reload

                    FirePropertyChanged();

                }
            }
        }



        static public bool SkipSpawnParticles = false;

        /// <summary>
        /// Apply the plugin name to the entity if a Spirit, can override the saved plugin if the entity is a spirit
        /// </summary>
        [DataMember]
        public string PluginName { get; set; }


        //if this is not null or "", try load a thing like namiadspr and insert in world.
        // will be used to toss dead bodies parts and limbs in wind during word destruction scene 2..  prevent intro..
        //these items will expire when pass level  boundary.. only..  some items might end up as litter on ground..
        //just the spirit, dont care about plugin.. can test with namiad.spr   or one arm or leg..
        //todo gun
        /// <summary>
        /// for gun type this. if true... load the SpiritResource as a body, then create a view for it, and place like a market on the parent.
        /// </summary>

        //for gun.. that way they can see each bullet inside the clip , magazine or gun, and can be removed like  marker
        //on firing round.   Other uses we used for moving organs.. or rotating eyes.  or blinking eyes.   Springs than can be scaled at runtime or popped out...   other purpose would be gibbing.. emit on eye cut.. etc. simpifies joint graph..
        //These are an alternative to using physics joints which can complicate the joint graph and have issues with mass ratios.


        bool _useRefAsView = false;
        [DataMember]
        public bool UseEmittedBodyAsView
        {
            get => _useRefAsView;


            set
            {

                _useRefAsView = value;
                IsVisible = true;//legacy fro gun to be stable
            }
        }

        /// <summary>
        /// Emits serialized body showing alternate dress , for when SpiritResource is used with a .body file.
        /// </summary>
        [DataMember]
        public bool IsShowingDress2 { get; set; }

        /// <summary>
        /// Will scale the dress2 on emission, not use to make 9mm bullets more visible after leaving gun
        /// </summary>  
        public Vector2 DressScale2 { get; set; }

        /// <summary>
        /// Particles ignore gravity
        /// </summary>
        [DataMember]
        public bool IgnoreGravity { get; set; }

        /// <summary>
        /// Auto stop emitter after elapsed time.  In seconds
        /// Default is 0 or -1, which mean auto stop disabled (always emit continuously).     Setting Active to true again resets the timer
        /// </summary>
        [DataMember]
        public float AutoDeactivateAfterTime { get; set; }



        [DataMember]
        public BodySoundEffectParams SoundEffect { get; set; }

        [DataMember]
        /// <summary>
        /// Particles dont collide or update broad phase.
        /// </summary>
        public bool IsNotCollideable { get; set; }

        /// <summary>
        /// usually set to -something so that partlces wont collide with each other.
        /// </summary>
        [DataMember]
        public short CollisionGroup { get; set; }

        [DataMember]
        /// <summary>
        /// If true partcle will be spawned just outside of current window.. 
        /// but the same relative positionnd percentage as it is from the world center fo the Parent Body.
        /// Example..   just on AABB height the main body, it  will come from one unit of view window in WCS..
        /// used to originate dust and other stuff 
        /// </summary>
        public bool SpawnRelativeToCurrentView { get; set; }

        /// <summary>
        /// if particle gets inside something kill it.    relative to viewport..
        /// </summary>
        [DataMember]
        public bool CheckInsideOnCollision { get; set; }



        //TODO remove this region WPF MG_GRAPHICS. only used by tool now..  in MG draw it uses the worldtopixel scale at draw time like it should
        //we level this in tool because its harder in WPF to draw in screen coords on canvas
        #region WPFSCALING
        /// <summary>
        /// give particle annotational size .. dont scale it with zoom to certain extent..
        /// </summary>
        [DataMember]
        public bool SizeDivideByZoom { get; set; }

        //the zoom level at which particle size is unscaled.
        [DataMember]
        public float ZoomLevelForNormalScale { get; set; }


        [DataMember]
        public float SizeScaleMin { get; set; }

        [DataMember]
        public float SizeScaleMax { get; set; }
        #endregion

        /// <summary>
        /// if true don't emit unless clear for one frame of particle travel at Emittion Force. 
        /// using emitter point velocity .  ignores parent pody. usually  prevents tunneling
        /// </summary>
        [DataMember]
        public bool CheckRayOnEmit { get; set; }


        /// <summary>
        /// Tag is to differentiate between emitters when being accessed from the Plugin, so we can set different color to different tag
        /// Use lowercase for uniformity
        /// </summary>

        private float _probabilityCollidable = 0;
        /// <summary>
        /// If not zero, some will be collidable , some will  disappear on collision ..  between 0,  ( isNotcollidabe and 1  always collidable);
        /// </summary>
        [DataMember]
        public float ProbabilityCollidable
        {
            get { return _probabilityCollidable; }
            set
            {
                _probabilityCollidable = value;
                if (_probabilityCollidable != 0)  // zero is the defaut so if set to something means we want IsNotCollideable true as a basis
                {
                    IsNotCollideable = true;
                }
            }
        }






        //[DataMember] not used for now.. could be for smoke.. but looks too much like collection of  perfect circle unless moving close and slowly .. also a common and cheap effect
        //public BodyColor StartLifeColor;  //to put back search all these..  not many.

        //[DataMember]
        //public BodyColor StartLifeGradientBrushColorStop;

        //[DataMember]
        //public BodyColor EndLifeColor;

        //[DataMember]
        //public BodyColor EndLifeGradientBrushColorStop;


        /// <summary>
        /// SlowFrameRateReductionFactor, turn this down to reduce particles if FPS under a certain amout.   0.3 means emit 30% desired particles until FPS catches up.
        /// System uses slower of physics update time and render update, 
        /// this is to save on adding to the visual tree, that can  slow everything down with low of moving objects or particles, especially bullet items
        /// </summary>
        [DataMember]
        public Nullable<float> SlowFrameRateReductionFactor { get; set; }

        private string _tag = "";
        /// <summary>
        /// Tag is to differentiate between emitters when being accessed from the Plugin, so we can set different color to different tag
        /// Use lowercase for uniformity
        /// </summary>
        [DataMember]
        public string Tag
        {
            get { return _tag; }
            set { _tag = value; }
        }

        /// <summary>
        /// optional can be used to mark emitter in a series.
        /// </summary>
        [DataMember]
        public short ID { get; set; }


        Mat22 _matParticleRot;
        Random _randomizer;

        public World World { get; set; }


        [DataMember]
        public BodyInfo Info { get; set; }


        
        /// <summary>
        /// Stickiness of particle generated.  so balloon sparks don't stick to balloon, but dust does. 0 means dont 1 means always.   
        /// TODO not implemented
        /// </summary>
        [DataMember]
        public float ElectroStatic { get; set; }


#if !UNIVERSAL
        [Description("Inflow speed for Eulerian grid fluids"), Category("Eulerian Fluid")]
#endif
      
        /// <summary>
        /// used to set inflow and outflow  in meters/ sec in grid for fluid at its locatino and using size.. 
        /// For now uses Size to determine the width of flow at this velocity
        /// </summary>
        [DataMember]
        public float FlowSpeed { get; set; }

#if !UNIVERSAL
        [Description("Use this emitter for adding density and/or  flow to grid"), Category("Eulerian Fluid")]
#endif

        /// <summary>
        /// Dont spawn visible particle in case just using for the reaction. for setting inflow or other effect
        /// </summary>
        [DataMember]
        public bool SkipSpawnParticle { get; set; }


        [DataMember]
        /// <summary>
        /// How far in world distance to apply the flow vel starting from emitter in flow direction
        /// </summary>
        public float FlowDepth { get; set; }

        /// <summary>
        /// use full to make dummies for testing
        /// </summary>
        [DataMember]
        public Nullable<bool> IsCallingPlugin { get; set; }

        /// <summary>
        /// every 3 frames will add some jitter..  a vector between for -1  RandomForceMax, + RandoomForceMax
        /// 3 frames is hard coded to let it coast a bit.. used on bubbles or snow flakes that are supposed to represent a complex shape and low density but dont have on in the wind model
        /// </summary>  
        /// [DataMember]
        public Vector2 RandomForceMax { get; set; }



        /// <summary>
        /// scale all the Y in this reference before drawing or emittingTODO implement for emit
        /// </summary>

        [DataMember]
        public float ScaleY { get; set; } = 1f;


        /// <summary>
        /// scale all the Y in this reference before drawing or emitting  TODO implement for emit
        /// </summary>


        [DataMember]
        public float ScaleX { get; set; } = 1f;

        [DataMember]
        public bool UseEulerianWindOnly { get; set; }


        #endregion
        #region Ctor

        public const short DefaultParticleGroup = -665; // particles from anything don't collide with each other or its too complex.

        public BodyEmitter(Body parent, Vector2 localPos, World world)
            : base(parent, localPos)
        {
            MaxNumberOfParticles = 1;
            ParticleCountPerEmission = 1;
            Frequency = 3f;  //3 per second
            LifeSpan = 5000;  // 5sec
            Active = false;
            EmissionForce = -0.3f;
            Size = 0.02f;
            //EndSize = 0.002f;
            CollisionGroup = DefaultParticleGroup;
            DeviationAngle = 0.0f;

            Density = 200;

            Restitution = 0f;
            Friction = 0.2f;
            IsBullet = false;

            AutoDeactivateAfterTime = -1f;

            // initial view will be the same as parent body
            EdgeStrokeColor = parent.EdgeStrokeColor;
            EdgeStrokeThickness = parent.EdgeStrokeThickness;

            //ResetColorToDefault();
            _randomizer = new Random();

            //TODO gun.. dont rename this , just reuse for body filename.
            SpiritResource = "";
            World = world;

            ZoomLevelForNormalScale = 40f;

            SizeScaleMax = 3f;
            SizeScaleMin = 0.3f;

            ElectroStatic = 1f;     // default is always sticky

            DragCoefficient = 0.4f;
            RandomForceMax = Vector2.Zero;

            DragDeviation = 0;

            SuperSizeDeviation = 0;
            SuperSize = 0.5f;
            SuperShape = FieldParticle.Shape.circle;
            IsBullet = false;
            FixedRotation = false; 
            Friction = 0;
            
            EmissionForce = 1f;

        
        }

        #endregion




        /*
        private void SetToGreySmokeGradient()
        {
            StartLifeColor = new BodyColor(10, 10, 200, 255);//this.Color;
            StartLifeGradientBrushColorStop = new BodyColor(200, 200, 200, 0);
            EndLifeColor = new BodyColor(160, 160, 0, 255);
            EndLifeGradientBrushColorStop = new BodyColor(255, 255, 0, 0);
        }
        */


        //ligacy onDeserialized code puts fixtures and contact releated stuff to the currelt world as it loads
        //totally dont want this just to draw an item that may never be spawned

        //for now we use this in preloading emitter items, making srue its gets set back
        //so we dont break anything TODO,,. put the fixutre and colision proxy stuff in insert to physics
        //after a level is loaded and put to physics or when something is acutallyspawned
        public class NoFixtureLoading : IDisposable
        {
            bool prevVal;
            public NoFixtureLoading()
            {
                prevVal = Body.NotCreateFixtureOnDeserialize;
                Body.NotCreateFixtureOnDeserialize = true;

                Debug.WriteLine("NotCreateFixtureOnDeserialize" + Body.NotCreateFixtureOnDeserialize);
            }
            public void Dispose()
            {
                Body.NotCreateFixtureOnDeserialize = prevVal;
                Debug.WriteLine("NotCreateFixtureOnDeserialize" + Body.NotCreateFixtureOnDeserialize);

            }
        }


        /// <summary>
        /// Cache entity for next emit.
        /// Call OnEntityPreLoad and let external code instantiate entity object, and store it on _entityCache.
        /// Don't call this on deserialized, _level or Level.Instance is still null.
        /// </summary>
        public void CheckToCacheEntity()
        {
            if (_entityCache != null || string.IsNullOrEmpty(SpiritResource) || NumParticlesLeft < 1 
                || SkipPreload)
                return;

            
            //NOTE TODO FUTURE bullets and pickaxe body leave a bad fixture a few meters to the left if preloaded.. Dont know why
            //not issues with characters.    Fixture and body serialization is suspect.. adding to collision should be do

            //if (
            // (Info & BodyInfo.SpawnOnly) != 0
            //   && !SpiritResource.ToLower().EndsWith(".body") //TODO FUTURE.. this is a workaround. dont preload bullet or any body unless bad fixture in level 1 or 3b is fixed.. or try moving preload with emitter, or dont disable,  that my skip isnotcollidable fix.
            //  )
            {
                if (OnEntityPreLoad != null)
                {

                    if (UseEmittedBodyAsView)
                    {
                        using (var x = new NoFixtureLoading())     //Not on load fixtures.. its added to collision  tho.. causes weird ness
                        {
                            _entityCache = OnEntityPreLoad(this);
                        }
                    }
                    else
                    {
                        _entityCache = OnEntityPreLoad(this);
                    }

                    LastEntityLoaded = _entityCache;
       
                }
            }

        }




        //TODO clean this usign Spawn pattern to have a body used as a template, simply clone it and spit it out. simplify and ERASE all the dublicate props
        public void SetProperties(Body body)  //TODO , should optionally be used. by spirit, to apply emitter prop to each body,  
        {
            body.FixedRotation = FixedRotation;
            body.IgnoreGravity = IgnoreGravity;
            body.SkipWindBlockCheck = SkipWindBlockCheck;
            body.ApplyDragAsParticle = ApplyDragAsParticle;
            body.FixedRotation = FixedRotation;
            body.DragCoefficient = DragCoefficient;
            body.Density = Density;
            body.IsNotCollideable = IsNotCollideable;
            body.ZIndex = ZIndex;
            body.Info = Info;
            body.IsShowingDress2 = IsShowingDress2;
            body.AngularDamping = AngularDamping;

            if (DressScale2 != Vector2.Zero)
            {
                body.DressScale2 = DressScale2;
            }

            body.SoundEffect = SoundEffect;
            body.IsBullet = IsBullet;
            body.Restitution = Restitution;
            body.Friction = Friction;
            body.CollisionGroup = CollisionGroup;

            if (Rotation != null)
            {
                body.Rotation = (float)Rotation;
            }
        }

        public void SetIsDead()
        {
            IsDead = true;
        }

        private void SpawnNewParticles(World world, double dt)
        {
            // if limited, check num of particle left
            if (MaxNumberOfParticles != 0)
            {
                _numParticlesLeft -= ParticleCountPerEmission;
                // if no more particle left, continue but prevent next Update to spawn
                if (_numParticlesLeft <= 0)
                {
                    IsDead = true;
                }
            }

            // Transform our force in respect to emitter orientation
            Vector2 direction = Direction;
            direction.Normalize();

            Vector2 worldForce = _parent.GetWorldVector(Vector2.Multiply(direction, EmissionForce));
            Vector2 emitterVelocity = _parent.GetLinearVelocityFromWorldPoint(WorldPosition);
            // Only process deviation angle != 0, to save computations
            Vector2 emissionForce = worldForce;
            DeviateForce(ref emissionForce);

        


            //if doesnt shoot projectile and emits a spirit, spirits are just spawned, not forced for now..  todo generalized, let anything get shot out, bullets, creature, etc
            if (!string.IsNullOrEmpty(SpiritResource) && (Info & BodyInfo.ShootsProjectile) == 0) //TODO clarify or simplify, smells, could be generalized wiht below, repeat code here
             {
                if (_entityCache == null)
                {

                    // TODO  FUTURE   use a entity  pool.. keyed by name.   get a unused dead one or grow the pool
                    // might not make any difference tho in fps.
                    if (OnSpawnEntity != null && !SkipSpawnParticle )
                    {
                 OnSpawnEntity(this,  emissionForce, emitterVelocity, LifeSpan);
                    }

                }
                else
                {
                    if (OnSpawnCachedEntity != null && !SkipSpawnParticle)
                    {
                        OnSpawnCachedEntity(this, _entityCache, emissionForce, emitterVelocity, LifeSpan);
                        // SetAsActiveVisible(_entityCache, true);  //only need if preloaded into physics and view , currently not used.
                        // after spawn, clear cache and reload 
                        _entityCache = null;  //TODO FUTURE should clone entity
                        CheckToCacheEntity();
                    }
                }

                _parent.ApplyForce(-emissionForce, WorldPosition);  //apply recoil to parent  
            }
            else
            {
                //  TODO  FUTURE   use a particle pool..
                for (int i = 0; i < ParticleCountPerEmission; i++)
                {
                    //todo  future account for ParticlesLeft 
                    //TODO FUTURE DONT create fixtures at all on particles that are is not collidable  OPTIMIZATION
                    //Trace.TraceInformation("worldForce X:" + worldForce.X.ToString() + " Y:" + worldForce.Y.ToString() + "Mag: " + worldForce.Length().ToString());
                    //Trace.TraceInformation("engine vel X:" + velocity.X.ToString() + " Y:" + velocity.Y.ToString() + "Mag: " + velocity.Length().ToString());


                    Vector2 offset = Offset;
                    offset.X = MathUtils.GetRandomValueDeviationFactor(offset.X, DeviationOffsetX);
                    offset.Y = MathUtils.GetRandomValueDeviationFactor(offset.Y, DeviationOffsetY);

                    Vector2 pos =Vector2.Add(WorldPosition, offset);

                    float particleSize = Size;

                    if (DeviationSize > 0)
                    {
                        particleSize = Size + ((float)_randomizer.NextDouble() * DeviationSize) - (DeviationSize * 0.5f);//TODO change DeviationSize to a percentage
                    }



                    //see in tool if particles go in the level.entities..
                    //who updates them then?

                    //todo we need to collect these separately if not goonna colide anywayw slows down everythihg

                    //the viewprot this hass abbb zer sometimgs particle is positoin is not affects so its removed

                    //make test level on particle emitter and test that.. see how call its update per particle

                    OnPreEmission?.Invoke(this, pos, particleSize);

                    if (!SkipSpawnParticle && !SkipSpawnParticles)
                    {
                        Particle particle = CreateParticle(world, pos, particleSize);
                        emissionForce = worldForce;
                        DeviateForce(ref emissionForce);
                        particle.LinearVelocity = emitterVelocity;
                        particle.ApplyForce(emissionForce);
                        particle.LinearVelocityPreviousFrame = World.DT * particle.InvMass * emissionForce + emitterVelocity; //incase point blank collision this is needed  to initialze this for
                        //handling on collide. 
                        // No level access from within physics engine, so use the callback to spawn the particles
                        //this is called on update in UI thread.. not sure if the best idea.
                        //TODO CODE REVIEW FUTURE /  ARCHITECTURE consider we  should have an updatephysics IEntity..   ( nename plugin UpdatePhysics since its on UI thread i think) 
                        //than maybe controller calls for all entities before physics update, on physics thread.

                        //TODO FUTURE .. use ParticleGenerator <Particle>

                        if (!CheckRayOnEmit || !PlaceMarkIfWillCollideOnFirstFrame(particle, worldForce, emitterVelocity, dt, true))
                        {
                            if (OnSpawnParticle != null)
                            {
                                if (!OnSpawnParticle(this, particle))
                                {
                                    world.RemoveBody(particle);
                                } else
                                {
                                    if (OnThisSpawnParticle != null)
                                    {
                                        OnThisSpawnParticle(particle);
                                    }

                                }
                            }
                        }
                        else
                        {
                            world.RemoveBody(particle);  //was added on new Particle(world) 
                        }
                    }

                    _parent.ApplyForce(-emissionForce, WorldPosition);  //recoil , add up all the particles.
                    if (emissionForce.Length() > 1000000)
                    {
                        Debug.WriteLine("giant particle emission force " + Name + " " + "numpartcle" + i + Tag + " " + WorldPosition + " " + emissionForce.ToString());
                    }

                }  //particle per emission, add up the forces..

            }

        }



        void DeviateForce(ref Vector2 worldForce)  //tested with bird shot..
        {
            if (DeviationAngle != 0)
            {
                // angle = -deviationAngle/2 ... rnd ... +deviationAngle/2
                float angle = ((float)_randomizer.NextDouble() * DeviationAngle) - (DeviationAngle * 0.5f);
                _matParticleRot.Set(angle);
                worldForce = MathUtils.Multiply(ref _matParticleRot, ref worldForce);
            }
        }


        private Particle CreateParticle(World world, Vector2 worldPos, float size)
        {
            Particle particle = null;

            if (size <= 0)//for old levels or prevent design/ ui errors. 
            {
                size = 0.02f;
            }

            float particleSize = size;

            if (SuperSize <= 0)//prevent crash .. 
            {
                SuperSize = 0.04f;
            }


            if (!SkipSpawnParticle) //used for when a emitter is a propulsion force, like bee wings, or rocket engine 
            {

                particle = IsFieldParticle ? new FieldParticle(world) 
                    : new Particle(world);

                LastEntityLoaded = particle;

 
                particle.Position = worldPos;
                particle.BodyType = BodyType.Dynamic;
                particle.LifeSpan = LifeSpan;


                SetPhysicalProps(particle);
                SetCollidable(particle);

                particle.RandomForceMax = RandomForceMax;

                SetVisibleProps(particle, size);  //TODO sparks based on temp...  other with trails of black smoke


                FixtureFactory.CreateCircle(particleSize, Density, particle);     //Note setting Density to 1 for now.. gets overridden by mass.

                particle.CollisionGroup = CollisionGroup;//don't want to collide particle with emitter body usually.   Set this after CreateCircle so it will be applied to fixtures
                                                         // If 0, then collision will occur, else it will not collide each other
                                                         // Can only be applied to new spawned particles, past frame particle can't be applied, since it will die anyway

                // set visual appearance prop of particle. should be done before view creation for this particle.

             
                SetLastParticleProps(particle);  //set it after , sets mass overrides density..on particles scaled to be seen in annotational space.. (pixels)


                if (IsFieldParticle)
                {
                    SetFieldParticleProps(particle as FieldParticle);
                }
     
              //  if (OnSpawnFieldParticle ==null && IsFieldParticle)//first time for this emitter, set up the group, we can mix particles, or spread out the work
              //  {
                    //NOTE.. this GasPressureField could be a spirit.. but there are no joints..   mabye there should be .. could help make fillaments.. in liquids anyways...  but ,
                    //the field is hot the particles should follow each other a bit...
                    //and rotate by collisions with the other  particle
            //  }


                //recycle
              if (OnSpawnFieldParticle!=null)
                 OnSpawnFieldParticle(this,particle as FieldParticle);  //generalize to superparticle?   reuse for water sprays, because they can be grouped, have pressure, and mixed, and 
          
              //TODO on start and stop callbacks?... hook to the gaspressurefield... bodyemittercant know about it?

            }
            return particle;
        }

        private void SetLastParticleProps(Particle particle)
        {
            if (Mass != 0) //default  //for thing like snow.. set the 
            {
                particle.Mass = Mass;  // needs to be set after CreateCircle 
            }

            if (DragCoefficient < 0)
            {// no aireffect
                particle.DragCoefficient = 0;
            }
            else
            {
                particle.DragCoefficient = DragCoefficient;
            }

        }

        private void SetFieldParticleProps(FieldParticle particle)
        {
            particle.MetaShape = SuperShape;
            particle.CollisionGroup = CollisionGroup;
            particle.FieldRadiusFactor = FieldSizeFactor;
            particle.CurrentParticleSizeX = SuperSize;
            particle.SuperSizeDeviation = SuperSizeDeviation;
        }

        private void SetCollidable(Particle particle)
        {


             if (ProbabilityCollidable > 0)  //this is for fuller fluids, only some will be collidable and go into the dynamic tree, the rest will cast collide detect/die rays.
            {
                particle.IsNotCollideable = !(_randomizer.NextDouble() < _currentProbabiltyCollidable);  //double negative i know..  but defautl is zero, collidable
            }
            else
            {
                particle.IsNotCollideable = IsNotCollideable;
            }
        }


        private void SetPhysicalProps(Particle particle)
        {
            SetProperties(particle as Body);
            particle.CheckInsideOnCollision = CheckInsideOnCollision;
            particle.ElectroStatic = ElectroStatic;
            particle.DragDeviation = DragDeviation;
        }

        private void SetVisibleProps(Particle particle, float particleSize)
        {
            particle.Color = Color;
            particle.EdgeStrokeColor = EdgeStrokeColor;
            particle.EdgeStrokeThickness = EdgeStrokeThickness;

            particle.ParticleSize = particleSize;
            particle.MagnitudeAspectOscillation = MagnitudeAspectOscillation;
            particle.OscillationPeriod = MathUtils.GetRandomValueDeviationFactor(OscillationPeriod, OscillationPeriodDeviation);


            particle.SizeDivideByZoom = SizeDivideByZoom;

           if (particle is FieldParticle)//TODO MG_GRAPHICS    does this affectr gass at all?  prolly we removce this stuff... unfinished work only in rocket now i think
          {
                FieldParticle fp = particle as FieldParticle;

            
                fp.SuperSizeDeviation = SuperSizeDeviation;
                fp.CurrentSuperSize = MathUtils.GetRandomValueDeviationFactor(SuperSize, fp.SuperSizeDeviation);

#if VisualizeParticles
                particle.ParticleSize = fp.CurrentSuperSize;  //TODO gas particles should be visible as smoke.. normally..
                particle.Color.R = 0;
                particle.Color.A = 20;
                particle.IsVisible = true;//quick fix we dont wanna see this now//TODO do we even use this
#endif

            }
          
     
        }


        private double _elapsedTimeFreq = 0;
        private double _elapsedTimeStop = 0;

        public override void Update(double dt)
        {
            if (!Active || IsDead)
                return;

            _elapsedTimeFreq += dt;

            if (_elapsedTimeFreq > _currentPeriod)
            {
                _elapsedTimeFreq = 0;

                _currentProbabiltyCollidable = ProbabilityCollidable;

                //will detemine the next period..
                _currentFrequency = MathUtils.GetRandomValuePlusDeviationFactor(Frequency, FrequencyDeviation);

                //slower than this setting  becomes not much fun in my opinion  60 is often used for this threshold.
                const int MinFrameRateforEffects = 60;

                if (World.Instance.IsSlowerPhysicsUpdateThan(MinFrameRateforEffects)) //if physics collision is aready slow, dont ad more  particles using the collision tree
                {
                    _currentProbabiltyCollidable = 0;  
                }


                if (SlowFrameRateReductionFactor != null && SlowFrameRateReductionFactor > 0f &&  SlowFrameRateReductionFactor != 1.0f &&
                   (
                    World.Instance.IsSlowerPhysicsUpdateThan(MinFrameRateforEffects)||
                        World.Instance.IsSlowRenderUpdate(MinFrameRateforEffects))
                    )//dont reduce frame rate even more by adding bodyies , cant afford blood or rain or effects particles.. TODO ( check essential paritlces?)
                {
                    _currentFrequency *= (float)SlowFrameRateReductionFactor;
                }

                _currentPeriod = 1.0f / _currentFrequency;

                SpawnNewParticles(World, dt);
            }

            UpdateAutoStop(dt);
        }


        static public float UpdateGraphicsPerSec = 0;   // set by call back.

        /// <summary>
        /// Check if partlice will collide first frame
        /// </summary>
        /// <param name="particle"></param>
        /// <param name="emissionForce"></param>
        /// <param name="emitterVelocity"></param>
        /// <param name="dt"></param>
        /// <param name="placeMark"></param>
        /// <returns></returns>
        private bool PlaceMarkIfWillCollideOnFirstFrame(Particle particle, Vector2 emissionForce, Vector2 emitterVelocity, double dt, bool placeMark)
        {

            Vector2 accel = emissionForce / particle.Mass;
            Vector2 distPerFrame = (particle.LinearVelocity + accel * (float)dt) * (float)dt;   // rayExtension;       
            Vector2 applicationPoint = particle.WorldCenter;

            //TODO should check only if something is near by emitter.. rain high in sky useless to check..
            // can have plugin  or emitter mange this.. but consider balloon in rain..
            // example emitter  on cloud.. uses ray to determine when particle should start casting this ray or be collectible.
            Vector2 rayEnd1 = applicationPoint + distPerFrame;

            Vector2 normalAtCollision = Vector2.Zero;
            Body struckBody = null;
            bool hitSomething = false;
            float rayFraction = 0;
            Vector2 intersection = Vector2.Zero;

            World.Instance.RayCast((fixture, point, normal, fraction) => { 
          
 			    if (fixture == null)
 					return -1;

#if !( PRODUCTION)
                if (fixture.Body.IsNotCollideable) //needed in tool since IsNotCollideable are selectable..
                    return -1;  //skip and continue
#endif
                if (fixture.Body is Particle || fixture.Body == this.Parent)
                    return -1;

                struckBody = fixture.Body;
                hitSomething = true;
                intersection = point;
                normalAtCollision = normal;
                rayFraction = fraction;

                return fraction;
            }, applicationPoint, rayEnd1);//dont check if ray starts inside

            if (hitSomething && placeMark)
            {
                CheckToAddParticleStrikeMark(particle, ref normalAtCollision, struckBody, ref intersection, rayFraction);
            }

            //Debug.WriteLine("rayat : " + rayEnd1 + hitSomething.ToString());
            return hitSomething;
        }
  

        private static void CheckToAddParticleStrikeMark(Particle particle, ref Vector2 normalAtCollision, Body struckBody, ref Vector2 intersection, float rayFraction)
        {

            // normalAtCollision might work to allign  ellipse.. one concern is the lineup of the dress to the edge.
            // if its easy we could try.   later we can make body edge closer to dress anyways.
            //todo offset this by the amout of the Stroke thickness  if struck body, offset it by strok.. check level 2 snow.. blood should  be on edige of stroke.
            // in level 4 , and  other stroke is used thick to make objects appear to touch ground.. there is now a space. ( farseer issue , recommend this gap not attempt to be adjusted) 
            if (OnAddParticleStrikeMark != null)
            {
                OnAddParticleStrikeMark(particle, normalAtCollision, struckBody, intersection, rayFraction);
            }
        }


        protected void UpdateAutoStop(double dt)
        {
            if (Active && AutoDeactivateAfterTime >= 0)
            {
                _elapsedTimeStop += dt;
        
                if (_elapsedTimeStop >=  AutoDeactivateAfterTime)
                {
                    // full stop
                    Active = false;
                    _elapsedTimeStop = 0;
                }
            }
        }


        [OnDeserializing]
        public void OnDeserializing(StreamingContext sc)
        {
            World = World.Instance;
        }
        

        [OnDeserialized]
        public new  void OnDeserialized(StreamingContext sc)
        {
            _randomizer = new Random();

            if (ZIndex == 0)  //by default put particles in the backgroudn so they less like to appear to interpenetrate
            {
                ZIndex = -999;
            }

            if (IsCallingPlugin == null)  // for old files.
            {
                IsCallingPlugin = true;
            }


            if (SlowFrameRateReductionFactor == null)
            {
                SlowFrameRateReductionFactor = 0.2f;  //if frame rate is slow , by default cut to 20 percent frequency of particles unless essential.
            }

            if (ScaleX == 0)
                ScaleX = 1f;

            if (ScaleY == 0)
                ScaleY = 1f;

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

      

    }
}
