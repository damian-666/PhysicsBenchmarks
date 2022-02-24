using System;
using System.Collections;
using System.Collections.Generic;

using Farseer.Xna.Framework;
using FarseerPhysics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Common;
using FarseerPhysics.Collision;


using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics.Particles;
using Core.Data;

using System.Diagnostics;

using Core.Data.Animations;
using Core.Data.Plugins;
using FarseerPhysics.Common.PolygonManipulation;
using FarseerPhysics.Factories;
using System.Threading.Tasks;
using static Core.Trace.TimeExec;

#if NEZ
using Nez;
#endif
using System.IO;
using Core.Game.MG.Simulation;

using Core.Data.Geometry;


using Core.Game.MG.Drawing;
using Storage;
using System.Runtime.CompilerServices;
using System.Threading;
using static Core.Game.MG.Graphics.BaseView;
using Core.Game.MG.Graphics;
using FarseerPhysicsView;
using Core.Data.Collections;

namespace Core.Game.MG
{
    //SimLoop
    /// <summary>
    ///    Handles updating the physics and graphics, using a 2 thread Producer Consumer model, physics is updated on the background thread,
    ///    a lock is obtained,  a list of drawable is copies from the physics bodies, lock released than then the list is drawn on concurently on  the UI thread
    ///    while the next physics frame is updated

    ///     This class brings in the Level model,  needs a new name :
    ///     Simulation,  GameLoop
    ///    it  is cross platfrom,  Takes functionality from old SimWorld which is windows dependent and will exist as part of game until
    ///    the monogame graphics completely overtakes the windows and RT graphics.
    /// </summary>
    public class SimWorld
    {

        public delegate void ResetHandler(World newWorld);
        public event ResetHandler OnReset = null;

        private float _physicsUpdateInterval = 1 / 60f;   // default value

        //TODO cleanup.  consider should be a just a thread safe singleton  called Instance.   
        /// <summary>l
        /// Most Recent physics SimWorld instantiated.  For now only one is instantiated during the game and tool 
        /// </summary>
        public static SimWorld Instance { get; private set; }


        public static bool IsSensorRayVisible = false;  // for working sensor rays... lasers are always visible..

        public static event Action<LaserEmitter, Vector2, Fixture, float> OnLaserCut = null;

        public static bool HasUIAccess { get; set; } = false;

        public static bool IsDirectX { get; set; } = false;


        public static bool EnableSounds { get; set; } = true;
        public static bool CloudBreak { get; set; } = true;

        

        public Action<IEnumerable<BodyEmitter>> EmitterPreloading;


     
        #region Constructor

        public SimWorld(bool UseThread = true, bool startPhysics = false, World physics = null)
        {
            Instance = this;

            LaserEmitter.OnLaserHit += new Action<LaserEmitter, Fixture, Vector2>(OnLaserHit);


            Sensor.OnRayCreated += new Action<RayInfo>(Sensor_OnRayCreated);
            Sensor.OnRayDestroyed += new Action<RayInfo>(Sensor_OnRayDestroyed);

            IgnoreSensorFixture = false;


            //PlanetRotationPeriod = 8;
            PlanetRotationPeriod = 60 * 5;   //5 minute days for now


            CuttingTools.OnCutBody += new Action<Body, CuttingTools.Splitter>(CuttingTools_OnCutBody);// keep body , but shave off other side
            CuttingTools.OnNewCutBody += new Action<Body, Vertices, CuttingTools.Splitter>(CuttingTools_OnNewCutBody); //clone body, and shave off original side..

            // Put callbacks on Initialize not on LevelChangeHandler, because it will keep adding callback upon load level, it leaks
            Reset(startPhysics, UseThread, physics);

        }



        /// <summary>
        /// This method clones the kept 
        /// </summary>
        /// <param name="origBody"></param>
        /// <param name="vertices"></param>
        /// <param name="split"></param>
        private void CuttingTools_OnNewCutBody(Body origBody, Vertices vertices, CuttingTools.Splitter split)
        {

            try
            {
                Body newBody = BodyFactory.CreateBody(Physics, origBody.Position, vertices, origBody.Density);

                newBody.GeneralVertices = vertices;
                newBody.CopyPropertiesFrom(origBody);
                newBody.CollisionGroup = 0;  //dont want collide group as parent.


                //TODO VISUALSLOP
                if (origBody.EdgeStrokeThickness == 0 && origBody.Color.A != 0)  //TODO do something else for transparent
                {
                    origBody.EdgeStrokeThickness = Settings.HalfContactSpacing;
                    origBody.EdgeStrokeColor = origBody.Color;
                }

                newBody.SoundEffect = origBody.SoundEffect;

                if (newBody.SoundEffect != null)
                {
                    newBody.SoundEffect.PitchShiftFactor -= 0.5; // higher pitch/ smaller piece..  just a guess.
                }

                newBody.Nourishment = origBody.Nourishment / 2;  //TODO could refine this // amount of mass..  anyways eating any meat gives alot of energy now

                newBody.BodyType = BodyType.Dynamic;  //.. in case was frozen for performance ( TODO .. if static airship frozen , unfreeze) TODO what if shooting off a piece of ground.. future.
                newBody.Info |= BodyInfo.ClipDressToGeom;


                //TODO bug  on boat, dont see the emitter Vector2 get copied.

                //make simple test file for these..
                //TODO add a AddRefVector2 API that sets the parent.   Vector2s in the right collection.
                //TODO not sure if this works..

                origBody.VisibleMarks.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { newBody.VisibleMarks.Add(x); x.Parent = newBody; } });
                origBody.SharpPoints.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { newBody.SharpPoints.Add(x); x.Parent = newBody; } });
                origBody.AttachPoints.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { newBody.AttachPoints.Add(x); x.Parent = newBody; } });
                origBody.EmitterPoints.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { newBody.EmitterPoints.Add(x); x.Parent = newBody; ; } });

                //TODO bug .. weird.. after leg cut off,  leg parts thigh and lower cant be cut again.. bullet sticks in instead.  
                //cut complex fails walking aroudn the Vector2s on slip..
                //BUT IF I COPY PASTE THE leg parts its works.. too weird.. skip it.   also works if cut first.

                //TODO  if leg or arm cut.. make the broken piece falls off after a time.. start growing from the joint.. now its replaces too fast.
                //should be able to walk on cut legs.


                //TODO iterate joints.. see if its easy just copy them over.. 
                //NOTE... might be easy.. just copy the joint to new body.. and change the BodyA..
                Level.Instance.CacheAddEntity(newBody);

                //TODO add little grip to hold pieces. ( for eating) .. or add from joint location prior or cut Vector2?

                //we need the general vertices for wind drag anyways.
                //TODO add blood  emitter if along if its a body part.
                //TODO pass in the cut line from call back.. replace the joints, and ref Vector2s from  old body

            }

            catch (Exception exc)
            {
                Debug.WriteLine("error creating cut polygon" + exc.Message);
            }
         
        }


        /// <summary>
        /// This method keeps the body that was cut, but discards the parts on the other side of the slipper, then retesselates on next pass
        /// </summary>
        /// <param name="body">The body to be shaved </param>
        /// <param name="split"></param>
        private void CuttingTools_OnCutBody(Body body, CuttingTools.Splitter split)
        {
            //body has new GeneralVertices set 
            Level.Instance.CacheReplaceShapes(body);

            // remove all the visible marks on the other side since we are removing that whole side.
            //( NOTE OPTIMIZATION if mark views were done properly as children in the visual tree of bodyview parent
            //, clipping  the whole thing would probably be enough and simpler.. althoug might be slower.. having to update the tree)..
            //now remove all the stuff on the cut out side.. will need to be added to the
            body.VisibleMarks.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { x.Terminate(); } });
            body.EmitterPoints.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { x.Active = false; } });

            body.SharpPoints.GetEnumerator();


            List<SharpPoint> sharpPtsOrg = new List<SharpPoint>(body.SharpPoints);   //create a copy of the list so we can modify the original ( avoid exception modified a collection being iterated.)           
            sharpPtsOrg.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { body.SharpPoints.Remove(x); } });

            //   RemoveSplitSidePts(body.SharpPoints, split);    //TODO FUTURE CLEANUP..should be doing something with contravariance here..  wont compile.. its using covariance and contravariance.. ahh whatever saves 2 lines of code use a interface method in   < in ReferencePoint>   .. to make the copy of these there, in keyword

            //TODO cutting .. not sure if IsOnSide is right or always same.. might depend winding.. etc.
            //for simple cut of the arm piece.. needed to do !OnSide..

            //TODO use an anchored simple thing to test .. the new body will be the falling one..

            List<AttachPoint> atcPtsOrg = new List<AttachPoint>(body.AttachPoints);
            atcPtsOrg.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { body.AttachPoints.Remove(x); } });

            List<Emitter> emitterPtsOrg = new List<Emitter>(body.EmitterPoints);
            emitterPtsOrg.ForEach(x => { if (split.IsOnSide(x.LocalPosition)) { body.EmitterPoints.Remove(x); } });

            //TODO  why dont legs cut.. they should ..
            //TODO  .. some emitters appear active..

            if (body.PartType == PartType.MainBody)
            {
                if (Level.Instance.MapBodyToSpirits.TryGetValue(body, out Spirit sp))
                {
                    if (!sp.IsDead)
                    {
                        sp.DieRandomly(0);
                    }
                }

            }
        }


        public void ClearLevelViewsAndTextures() 
        {
        

            Graphics.Presentation.Instance?.Clear();
            Graphics.Graphics.BodyTextureMap.Clear();
            Graphics.Graphics.BodySpriteMap.Clear();
            Graphics.Graphics.EntityThumbnailTextureMap.Clear();
            Graphics.Graphics.EntityThumbnailTextureMap2.Clear();
       
        }


        /// <summary>
        /// Create a new Farseer world on physics thread. Any existing Farseer world will be disposed.   
        /// </summary>
        /// <param name="start">start the physics background update thread right away</param>
        public void Reset(bool start = true, bool useThread = true, World physics = null)
        {


#if GRAPHICS_MG
            // reset rendering counter
#endif

            // create new physics world


            _physics = physics ?? new World(new Vector2(0, 10));

            if (EnableSounds)
            {
                _physicsSounds = new PhysicsSounds(_physics);
            }


            //// remove previous thread
            //this.Dispose();
            // dont dispose thread, so elapsed time not reset, just reset all param
            if (_physicsThread == null || !_physicsThread.IsStarted())
            {
                _physicsThread = new PhysicsThread();
            }
            else
            {
                _physicsThread.IsRunning = false;
                _physicsThread.UpdateCallback = null;
            }



            // note that UpdateParam / PhysicsUpdateInterval will be overwritten 
            // in gamecode or mainlogic.
            _physicsThread.UpdateParam = _physicsUpdateInterval;
            _physicsThread.UpdateCallback = _physics.Step;




            //TODO, set to  null in game, see if used anywhere.... I think this is overcomplicated , this threading stuff.. two WaitHandles.. this is not used in Game..check if used in tool..

            if (OnReset != null)
            {
                OnReset(_physics);
            }

            _physics.ContactManager.PreSolve += OnPreSolvePhysics;
            //TODO LEAKS.. this orpans the old sensor... that can hold refs to bodies... gc probably will  clear it
            //probably should clear the ray map tho first.
            _sensor = new Sensor(_physics);

            if (start && useThread)
            {
                StartThread();
            }
        }




        #endregion

        #region Methods


        /// <summary>
        /// Performing single update to PhysicsSimulator using thread-safe mechanism.
        /// Stepping through single update at a time.
        /// </summary>
        public void SingleStepPhysicsUpdate(bool ignorePhysicsEnabled)
        {
            // return when physics is already running, so this won't accidentally 
            // stop current running physics/animation.

            if (PhysicsThread.IsRunning && !ignorePhysicsEnabled)
            {
                return;
            }

            if (PhysicsThread.WaitForAccess(100) == false)
            {
                return;
            }

            int oldSlowMo = PhysicsThread.SlowMotionTime;

            PhysicsThread.SlowMotionTime = 0;
            PhysicsThread.UpdatePhysics();
            PhysicsThread.SlowMotionTime = oldSlowMo;
            PhysicsThread.FinishedAccess();
        }




        //The emitter update is called from the UI thread.. physics  is locked.. so dispatch to UI should not be needed
        private void OnLaserHit(LaserEmitter emLaser, Fixture hitFixture, Vector2 hitPos)
        {

            Body hitBody = hitFixture.Body;

            float DensityNormal = 200;   //the creature.. water is 15...  TODO test laser water

            const float MaxLaserCutFactor = 10; //10 meters cuts on normal laser
                                                //laser can stil only cut one cloud because the beam stops at first object struck.  (TODO beam should contine, maybe combine the code with cutting if cutting is successful...

            float cutLength = emLaser.Power;

            if (hitBody.Density != 0)  //TODO apply to bullets also.. not too important .. its in bullet code already some stuff regarding angle and density, dont want to do this generallly in CutCompex, explored that
            {
                cutLength = Math.Min(emLaser.Power * MaxLaserCutFactor, DensityNormal / hitBody.Density);
            }

            try
            {


                if (hitBody == null || hitBody.IsStatic == true || emLaser.Power < 0.001f)  //cannot cut ground for now..
                {                                ///TODO .. consider sleep for static items wake and dyanmic piece of ground..
                    return;
                }

                //TODO make a function of density

                //TODO tune the 00.1  thatn the power.. . fix the blood.

                //then move to boats and dress and expanding the level screen.... then the walking.. bouncing..
                //do a tank.. a four.. legged..   a smaller ship.. break Vector2s... cut Vector2s..
                //fix cuts... check for bugs on cut..

                //TODO consider normalizing the direction  all.. but that requires a lot of regress testing.   its not specified as normalized..
                //  Vector2 dirNormal =  emitter.WorldDirection
                //   dirNormal.Normalize();   already done on laser cast
                //TODO optimize.. can avoid another cay cast, since we have the entry Vector2 alredy.. hit Vector2.. but.. anyways an smaller cut uses a smaller broad phase..
                if (CuttingTools.CutComplex(Physics,
                   hitPos - emLaser.WorldDirection * Settings.Epsilon * 3,   //make sure if starts outside of object
                   hitPos + emLaser.WorldDirection * cutLength, hitBody))
                {

                    if (OnLaserCut != null)
                    {
                        OnLaserCut(emLaser, hitPos, hitFixture, cutLength);
                    }

                };  //FUTURE .. put thickness back in .. was in old cut in previous farseer..  only if supporting thick death beams, and partial cuts


                //TODO bug with creating weird shapes.

                //sometimes it works perfectly..

                // remove tiny shapes.. or add density.. piles are too bouncy.. add a stroke.. issue with dress needed better expansiion.

                //TODO cut... remove blood on removed bloodies..

                //TODO LASER.. on body remove.. .remove the marks first..

                // as WORKAROUNDS FOR bad cuts leaving long or big peices..( MOVING ITEMS?).. if piece has segment longer that perim... dont cut.. if too acute or more massive that total.. dont cut...

                //todo.. blood on edges cut?  or cautarised.. its better.   if joits cut.. maybe not blood either.

                // or .. make edge high friction useing effect.. so they stack.. then slide apart... cool effect.

            }

            catch (Exception exc)
            {
                Debug.WriteLine("exception on on Laser cut" + exc.Message);
            }


            if (hitBody.OnLaserStrike != null)
            {
                hitBody.OnLaserStrike(hitPos, emLaser.WorldDirection, emLaser.Power);
                return;
            }


            //both cutting and breaking.. result in small pieces.. consider testing this first..
            //TODO we are doing a pulse laser.. maybe best each pulse do only one damage, a cut out..   simpler less. buggy.
            if (Settings.IsJointBreakable)
            {
                hitPos = BreakNearbyJoint(hitFixture, hitPos);
            }

        }

        private Vector2 BreakNearbyJoint(Fixture hitFixture, Vector2 hitPos)
        {

            JointEdge je = hitFixture.Body.JointList;
            while (je != null)
            {

                if (je.Joint is PoweredJoint)
                {

                    PoweredJoint joint = je.Joint as PoweredJoint;

                    if (Vector2.Distance(hitPos, joint.WorldAnchorB) < joint.SensorSize)
                    {
                        joint.Enabled = false;  //TOD this will cause blood.. normally its cauterized..
                        joint.IsBroken = true;

                        joint.BreakQuietly = true;
                    }

                    PoweredJoint pj = je.Joint as PoweredJoint;
                }
                je = je.Next;
            }

            return hitPos;
        }


        /*   TODO put device and others in a baseplugins dll 
        //TODO put in Spirit section  
        private Vector2 _dynGroundVel = Vector2.Zero;

        /// <summary>
        ///  body or body system CM velocity relative  to ground on which its standing speed.  at least one foot must be on it.. (TODO maybe layer only if -supporting it- more impulse than body under other foot if different
        /// </summary>
        public Vector2 DynamicGroundVelocity
        {
            get
            {
                Spirit sp = Level.Instance.ActiveSpirit;

                if (sp?.Plugin is Device)
                {
                    return (sp.Plugin as Device).GetGroundRelVel();
                }
                else
                {
                    return Vector2.Zero;
                }
            }
        }*/

        /*
                Vector2 _airSpeed = Vector2.Zero;KKKKKKKKKKKKKKKKKKKKKKKKKKKK
                /// <summary>  
                /// body or body system cm speed relative to air velocity in at  cm location  ( for large bodies in turbulence,  this doesn't tell  all
                /// </summary>
                public Vector2 AirVelocity
                {
                    get { return _airSpeed; }

                    set {

                            Spirit sp = Level.Instance.ActiveSpirit;

                            if! (sp.Plugin is IField)
                            {
                               return    WindDrag.GetVelocityField(  )
                            }
                            else
                            {

                            }

                     }
                }*/




        private void OnPreSolvePhysics(Contact contact, ref Manifold oldManifold)
        {
            contact.Enabled = EnableCollision;
        }

        public void ShutDown()
        {
            if (_physicsThread != null)
            {
                _physicsThread.ShutDown();
                _physicsThread = null;
            }
        }


        public void StartThread()
        {
            _physicsThread.IsRunning = true;
            _physicsThread.Start();
        }

        public bool IsPhysicsThreadRunning()
        {
            return _physicsThread != null && _physicsThread.IsStarted() && _physicsThread.IsRunning;
        }




        public void PausePhysics()
        {
            _physicsThread.IsRunning = false;

        }



        // this called  pre-physics update,  physics data is accessible 
        public void Update(object sender, TickEventArgs e)
        {
            AABB viewport = Graphics.Graphics.Instance.CTransform.GetWorldWindowAABB();

            WindDrag._currentViewport = viewport;  // MAY BE NEEDED FOR SPECIAL HANDLING OF PARTICLES OUT OF VIEWPORT..

            _sensor.Update(this.Physics);
        }


        /// <summary>
        /// After physics cycle, code in here must run in parallel with next physics engine update in progress
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void PostUpdate(object sender, TickEventArgs e)
        {

        }



        /// </summary>
        /// <param name="Vector2">The Vector2 position</param>
        public Body HitTestBody(Vector2 Vector2)
        {
            if (_physics == null)
            {
                return null;
            }

            // get all potential bodies first using rectangle selection. 
            IEnumerable<Body> bodies = HitTestRectangle(Vector2 - _hitVector2Size, Vector2 + _hitVector2Size);

            //return the smallest if overlapping.
            float smallestSize = float.PositiveInfinity;

            Body smallestBody = null;


            foreach (Body b in bodies)
            {
                b.UpdateAABB();
                AABB aabb = b.AABB;

                float minSize = Math.Min(aabb.Width, aabb.Height);
                if (minSize < smallestSize)
                {
                    smallestSize = minSize;
                    smallestBody = b;
                }

            }

            return smallestBody;

        }



        /// <summary>
        /// Get all bodies that intersect with specified rectangular area.
        /// Note: This method query physics data asynchronously, so physics thread lock 
        /// is used internally.  //TODO use this in play for hittest.. checkthe lock..
        /// </summary>
        public IEnumerable<Body> HitTestVector2(Vector2 Vector2)
        {
            List<Body> results = new List<Body>();

            if (Physics == null)
            {
                return results;
            }

            // obtain physics lock first, we're about querying physics, which can interfere 
            // with current physics data.  Since this is used in UI and physics runs in background  TODO see if necessary
            if (_physicsThread.WaitForAccess(1000) == false)
            {
                return results;
            }

            // broadphase. check if aabb overlap.

            Fixture f = _physics.TestPoint(Vector2);

            if (IgnoreSensorFixture && f.IsSensor)
            {
                return results;
            }

            results.Add(f.Body);

            // finished accessing physics data
            _physicsThread.FinishedAccess();
            return results;

        }


        public IEnumerable<Body> HitTestRectangle(AABB aabb)
        {
            return HitTestRectangle(aabb.LowerBound, aabb.UpperBound);
        }




        /// <summary>
        /// Get all bodies that intersect with specified rectangular area.
        /// Note: This method queries physics data asynchronously, so physics thread lock 
        /// is used internally. TODO lock needed?
        /// </summary>
        public IEnumerable<Body> HitTestRectangle(Vector2 startVector2, Vector2 endVector2)
        {
            List<Body> results = new List<Body>();

            if (Physics == null)
            {
                return results;
            }

            // ensure edge length is valid
            float hlength = Math.Abs(endVector2.X - startVector2.X);
            float vlength = Math.Abs(endVector2.Y - startVector2.Y);

            if (hlength <= Settings.Epsilon || vlength <= Settings.Epsilon)
            {
                return results;
            }


            AABB aabb = new AABB(ref startVector2, ref endVector2);

            // obtain physics lock first, we're about querying physics, which can interfere 
            // with current physics data.   NOTE TODO  is this necessary.. the query should now be thread safe.. for a while it was not.
            //   if (_physicsThread.WaitForAccess(1000) == false)
            //  {
            //      return results;
            //    }

            // broadphase. check if aabb overlap.

            _physics.QueryAABBSafe(
        delegate (Fixture f)
        {
            if (IgnoreSensorFixture && f.IsSensor)
            {
                return true;  //keep searching
            }

            f.Body.UpdateAABB();
            AABB bodyAABB = f.Body.AABB;

            // if shape really overlap, collect the body.
            if (AABB.TestOverlap(ref bodyAABB, ref aabb))
            {
                if (results.Contains(f.Body) == false)
                {
                    results.Add(f.Body);
                }
            }

            return true;
        }
        , ref aabb);

            // finished accessing physics data
            //      _physicsThread.FinishedAccess();

            return results;
        }

        #endregion



        #region Properties

        /// <summary>
        /// Determine if HitTestFixture or HitTestRectangle should ignore Fixture with IsSensor==true.
        /// </summary>
        public bool IgnoreSensorFixture { get; set; }


        // default hit Vector2 size
        private Vector2 _hitVector2Size = new Vector2(0.03f, 0.03f);  //TODO do hit test Vector2?  
        /// <summary>
        /// The size of hit test Vector2 (AABB)
        /// </summary>
        public Vector2 HitVector2Size
        {
            get { return _hitVector2Size; }
            set
            {
                // Setting lower than 0.01 will often throw collinear edge exception
                // when creating rectangle for aabb.

                if (value.X >= 0.01f)
                {
                    _hitVector2Size.X = value.X;
                }
                if (value.Y >= 0.01f)
                {
                    _hitVector2Size.Y = value.Y;
                }
            }
        }


        private Sensor _sensor;
        /// <summary>
        /// Simulation's Sensor
        /// </summary>
        public Sensor Sensor
        {
            get { return _sensor; }
        }


        private World _physics;
        /// <summary>
        /// Farseer PhysicsSimulator
        /// </summary>
        public World Physics
        {
            get { return _physics; }

            set { _physics = value; }
        }




        // multi-threaded physics
        private PhysicsThread _physicsThread;

        /// <summary>
        /// Separate wrapper for thread that running PhysicsSimulator.
        /// </summary>
        public PhysicsThread PhysicsThread
        {
            get { return _physicsThread; }
        }



        /// <summary>
        /// Get or Set the interval of virtualtime in ms for the timeslice used by each step in physics simulation
        /// Default is 16.6 ms
        /// A lower value will give a faster but more rough simulation 
        /// Reasonable range is 1ms to 100ms
        /// </summary>
        public float PhysicsUpdateInterval
        {
            get { return _physicsUpdateInterval; }
            set
            {
                _physicsUpdateInterval = value;
                _physicsThread.UpdateParam = value;
                World.DT = value;
            }
        }


        private bool _enableCollision = true;
        public bool EnableCollision
        {
            get { return _enableCollision; }
            set { _enableCollision = value; }
        }

        private PhysicsSounds _physicsSounds;
        public PhysicsSounds PhysicsSounds
        { get { return _physicsSounds; } }

        /// <summary>
        /// lenght of  PlanetRotationPeriod simulated rotating planet = day + night , 
        /// </summary>
        public double PlanetRotationPeriod { get; set; }


        public bool IsNight
        {
            get
            {
                return (TimeOfDay > PlanetRotationPeriod / 2);
            }

            //   set
            //   {
            //pick back ground..


            ///   }

        }


        public bool IsSunOverhead { get; set; } // set by ambience controller.  not true during sun set, sunrise


        public double TimeOfDay
        {
            get
            {
                return PhysicsThread.ElapsedTime % PlanetRotationPeriod;
            }
        }



        #endregion
        public bool NitroBoost = false;   // secret key allow super fast..

        /// <summary>
        /// Toggles wind controller effect
        /// </summary>
        static public bool IsWindOn { get; set; } = true;


        static bool _isParticleOn = true;
        static public bool IsParticleOn
        {
            get
            {
                return _isParticleOn;
            }
            set
            {
                _isParticleOn = value;
                BodyEmitter.SkipSpawnParticles = !value;
            }
        }




        /// <summary>
        ///   for general tuning of coefficients in relations or things like windspeed in tool using sliders, provides values between 0 and 1
        /// </summary>

        public float _paramA;


        /// <summary>
        ///  For general tuning of coefficients in relations or things like windspeed in tool using sliders, provides values between 0 and 1
        /// Parameters for this instance set by tuning or by emitters or plugin, can be anthing, vortex speed, etc, does not require a spirit to be selected while tuning, plugin can copy the value on spirit with this, on loaded and update
        /// </summary>
        public float ParamA
        {
            get { return _paramA; }
            set
            {
                if (_paramA != value)
                {
                    _paramA = value;
                }
            }
        }

        public float _paramB;

        // <summary>
        ///     for general tuning of coefficients in relations or things like windspeed in tool using sliders, provides values between 0 and 1
        /// </summary>
        public float ParamB
        {
            get { return _paramB; }
            set
            {
                if (_paramB != value)
                {
                    _paramB = value;

                }
            }
        }

        public static bool LooseFiles { get => looseFiles; set => looseFiles = value; }




        #region Particle Codes

        private Level _level = null;
        public AABB AABBStaticObjects;


        static bool looseFiles = true;




        public void PrepareParticleCallbacks()
        {
            BodyEmitter.OnSpawnEntity = OnSpawnEntity;
            BodyEmitter.OnSpawnCachedEntity = OnSpawnCachedEntity;
            BodyEmitter.OnSpawnParticle = OnSpawnParticle;


            BodyEmitter.OnEntityPreLoad = GetEmitterResourceAsEntity;  //TODO check if this is used .. and with name sprits that should not be loaded agani  if travelling
            BodyEmitter.OnAddParticleStrikeMark = WindDrag.CheckToAddParticleStrikeMark;

            // Only
            // will use this laser callback on SimWorld, Tool will have its specialized callback
            LaserEmitter.OnSpawnLaser += new LaserEmitter.SpawnLaserDelegate(OnSpawnLaser);
            LaserEmitter.OnLaserOff += new Action<LaserEmitter, string>(OnLaserOff);

        }

        /// <summary>
        /// This is called just after level is deserialized
        /// </summary>
        /// <param name="level"></param>
        public void SetLevel(Level level)
        {
            _level = level;
            _level.SetLevelNumberForTesting();

            Level.PhysicsUpdateInterval = PhysicsUpdateInterval;//NOTE TODO, a level might save its own interval,  and be tuned to work this, for example traps tension are set, what should should persist , not be static andn update physics one instead
        }


        /// <summary>
        /// This is called after all plugins are Loaded
        /// </summary>
        /// <param name="travellers">characters passing into level , coulld include PC, vehiciles , leaders, AI followers, etc</param>
        public void OnLevelLoaded(IEnumerable<IEntity> travellers)
        {

            AABBStaticObjects = _level.CalculateAABB(true);
            AABBStaticObjects = _level.ExpandLevelAABBIfNoSkyMarker(AABBStaticObjects);

            if (EmitterPreloading != null)

            {
                List<BodyEmitter> levelRefEmitters = new List<BodyEmitter>(_level.GetAllLevelEmitters());//make a copy because i saw exceptions interating in parrallel
                EmitterPreloading(levelRefEmitters);
            }


          
            _level.PreloadAllBodyEMitters();
            

            Spirit pcSpirit = _level.SpawnEntities(travellers);



            if (pcSpirit != null)
            {
                pcSpirit.SetParentLevel(_level);
                pcSpirit.World = SimWorld.Instance.Physics;

                if (_level.ActiveSpirit == null)
                {
                    _level.ActiveSpirit = pcSpirit;
                }
            }

#if PRODUCTION  //TODO clean out, shoudl be bound directly to ActiveSpirit.EnergyLevel or something
            if (_level.ActiveSpirit != null)
            {
                _level.ActiveSpirit.EnergyLevel = Spirit.PlayerStartEnergy;
            }
#endif
            // init all bodies (including those inside spirit)
            IEnumerable<Body> bodies = _level.GetAllBodiesFromEntities();

            _level.ClearSpiritsCache();   //todo better no use a cache..how often do we quey for spirits


            Presentation.Instance.CreateViewsForLevel(_level);

     


        }



        public static void MoveSpirit(ref Spirit sp, Vector2 newPos)
        {
            sp.UpdateAABB();
            Vector2 spcenter = sp.AABB.Center;
            Vector2 disp = newPos - spcenter;
            sp.Translate(disp);
        }

        protected static bool SpawnRelativeToCurrentView(SimWorld instance, BodyEmitter emitter, ref Particle particleBody)
        {
            Vector2 emitterRelPos = particleBody.WorldCenter - emitter.Parent.WorldCenter;
            float sizeEmitterBody = emitter.Parent.AABB.Width;

#if NEZ
            Nez.RectangleF WorldWindow = Nez.Graphics.Instance.GetActiveWindowRect();
#else
            RectangleF WorldWindow = Graphics.Graphics.Instance.GetActiveWindowRect();
#endif

            float widthWindow = WorldWindow.Width;      //assuming wider rect, almost square viewport here.
            float ratio = widthWindow / sizeEmitterBody;

            IField winddragField = WindDrag.WindField;

            if (winddragField == null)
            {
                return false; // if no wind  dont spawn  these
            }

            Vector2 relPosition = emitterRelPos * ratio;  //put in viewport scale

            Vector2 particlePosition = Vector2.Zero;




            //NOTE this doensnt have viewport aabb..


            SimWorld.AdjustPositionToViewportEdgeForWindField(


              Graphics.Graphics.Instance.Presentation.Camera.Bounds,


                relPosition, ref particlePosition);


            float temp;
            Vector2 windFieldCenter = winddragField.GetVelocityField(particlePosition, out float density, out temp);

            if (windFieldCenter.LengthSquared() < 2f)// don't bother unless there is some noticeable wind..
            {
                return false;
            }

            Vector2 windField = winddragField.GetVelocityField(particlePosition, out density, out temp);   // refine the wind field at the edge.., in case its different there.


            //TODO revisit.. i dont understand this.. its in MG can give particles that live way too long and bog the  system
            //removing for now.. todo after camera windows fixed.. revisit , retest level1 and maestrom MG_GRAPHICS
            //float maxSpeedComponent = Math.Max(Math.Abs(windField.X), Math.Abs(windField.Y));
            //particleBody.LifeSpan = (widthWindow / maxSpeedComponent) * 1000 * 5;   // extra particle life for acceleration..tested level 5 maelstrom w dust

            if (windField.LengthSquared() > WindDrag.MinWindBlockCheckVelSquareParticleDrag
                 && WindDrag.IsEffectivelyBlocked(particlePosition, -windField, WindDrag.RayAngle / 2, particleBody.GetHashCode() + "emit", null, null, true, false))
            {
                return false;
            }

            if (particlePosition != Vector2.Zero)
            {
                particleBody.Position = particlePosition;
                return true;
            }

            return false;
        }

        /*

                protected static bool CheckIfInCurrentView(SimWorld instance, BodyEmitter emitter)
                {
                    Vector2 wcs = emitter.WorldPosition; //  + emitter.DeviationOffsetX

                    RectangleF WorldWindow = Graphics.Graphics.Instance.GetActiveWindowRect();

                }
                */


        protected static bool CheckIfInCurrentView( Vector2 pos)
        {

            RectangleF WorldWindow = Graphics.Graphics.Instance.GetActiveWindowRect();

            return (WorldWindow.Contains(pos));

        }





        /// <param name="emtter"></param>
        /// <param name="ratio"></param>

        /// <param name="windField"></param>
        /// 

        /// <summary>
        /// to show wind.. just for the current viewport.. show win directino indicatios.. on zooming out.. dust will be absent.. so should be mixed with some not viewport dust
        /// alternative would be to put dusta everywhere but just keep it out of the visual tree.. expensive tho.
        /// </summary>
        /// <param name="emtter"></param>
        /// <param name="aabb">viewport AABB </param>
        /// <param name="relPosition">randomized from emitter devition inside sqaure design emitter body</param>
        /// <param name="aabb">the viewport</param>
        /// <param name="position">where to start the particle</param>
        private static void AdjustPositionToViewportEdgeForWindField( AABB aabb, Vector2 relPosition, ref Vector2 position)
        {

            float dt = 1f / 60f;
            IField windFld = WindDrag.WindField;

            //try all 4  sides.. put  a partlce to emitter from there..  TEst.. put a viewport with cyclone at centerl
            Vector2 randomizedPosition = aabb.Center + relPosition;

            //get four candidates for particles.
            Vector2 left = new Vector2(aabb.LowerBound.X, randomizedPosition.Y);
            Vector2 right = new Vector2(aabb.UpperBound.X, randomizedPosition.Y);

            Vector2 top = new Vector2(randomizedPosition.X, aabb.LowerBound.Y);
            Vector2 bottom = new Vector2(randomizedPosition.X, aabb.UpperBound.Y);

            float distanceExtension = 3f * -dt;

            bool foundParticle = false;

            //consider diagonal windfield.. 
            if (MathUtils.IsOneIn(2))  //should be maybe based on speed Y vs X ..  try x first.. on chance
            {
                foundParticle = GetHorizontalParticle(ref position, windFld, ref left, ref right, distanceExtension);
            }

            if (!foundParticle)   // if not x wind.. try Y..
            {
                GetVertParticle(ref position, windFld, ref top, ref bottom, distanceExtension);
            }
        }

        private static bool GetVertParticle(ref Vector2 position, IField windFld, ref Vector2 top, ref Vector2 bottom, float distanceExtension)
        {
            float temp;
            if (windFld.GetVelocityField(top, out float density, out _).Y > 0)
            {
                position = GetStartPosition(top, distanceExtension);
                return true;
            }
            else
                if (windFld.GetVelocityField(bottom, out density, out temp).Y < 0)
            {
                position = GetStartPosition(bottom, distanceExtension);
                return true;
            }

            return false;
        }

        private static bool GetHorizontalParticle(ref Vector2 position, IField windFld, ref Vector2 left, ref Vector2 right, float distanceExtension)
        {

            if (windFld.GetVelocityField(left, out _, out _).X > 0)
            {
                position = GetStartPosition(left, distanceExtension);
                return true;
            }
            else
                if (windFld.GetVelocityField(right, out _, out _).X < 0)
            {
                position = GetStartPosition(right, distanceExtension);
                return true;
            }

            return false;
        }

        private static Vector2 GetStartPosition(Vector2 pos, float distanceExtension)
        {

            float density, temperature;
            return (pos + WindDrag.WindField.GetVelocityField(pos, out density, out temperature) * distanceExtension);
        }

        /// <summary>
        /// Init BodyEmitter and create its view.   Certain emitters are visible
        /// </summary>
        /// <param name="bodies"></param>
        private void InitBodyEmitterWorld(IEnumerable<Body> bodies)
        {

            foreach (Body b in bodies)
            {
                SetWorldOnEmitters(b);
            }
        }

        public void InitBodyEmitterPhysics(Spirit sp)
        {


            sp.Bodies.ForEach(x => SetWorldOnEmitters(x));

        }


        public void SetWorldOnEmitters(Body b)
        {

            foreach (Emitter emitter in b.EmitterPoints)
            {
                BodyEmitter bem = emitter as BodyEmitter;
                if (bem != null)
                {
                    bem.World = Physics;  

                }
            }
        }


  
     


        protected bool OnSpawnParticle(BodyEmitter emitter, Particle emittedBody)
        {

            if (!IsParticleOn && !emitter.IsInfoFlagged(BodyInfo.SpawnOnly) || _level == null)
            {
                return false;
            }

            if (emitter.SpawnRelativeToCurrentView)
            {
                //comare with disney... this is to make sure there are enoug and to manange the particles.

                // if this can all be put oan Gpu its better

                //   const int PARTICLE_COUNT = 100000;
                //   ParticleLt[] particleLts = new ParticleLt[PARTICLE_COUNT];
                //    ParticleGenerator generator=   new ParticleGenerator(particleLts);    //one idea is to try one 1bit layers.. used those forst dust , each with a color 
                //fast to gpu .. and use shader to render if dense.. sample.. if close use the color.

                //Idea use teh shader to move all the particles.

                //the v is gotten from the gpu.. then advence them... this is for special GPU particles.. or textures loaded..or parts of creatures..


                //TODO first put this on the  outer writable bitmap

                if (!SimWorld.SpawnRelativeToCurrentView(this, emitter, ref emittedBody))  //was blocked on emit... creature must be near blockage or inside  airship..
                {
                    emittedBody.LifeSpan = 0;
                    return false;
                }
            }


            //this is a light dust partice for sure....TODO..
            Particle particle = emittedBody as Particle;

            particle.SkipRayCollisionCheck = emitter.SkipRayCollisionCheck;
            particle.UseEulerianWindOnly = emitter.UseEulerianWindOnly;
            particle.ParentBody = emitter.Parent;

  

            AddEmittedEntityToCurrentLevel(emittedBody);

            return true;
        }






        //     https://stackoverflow.com/questions/17248680/await-works-but-calling-task-result-hangs-deadlocks/32429753#32429753   we have the physics thread and the UI thread NEEDS to be blocked or it will go and release the WaitHandle, then when this is emitted its wrting to physics when physics thread is updating

        //TODO might port this more generally using  template, or change to sync at the api level.  not
        static string RunSync(Func<Task<string>> x)
        {

            return Task.Run(

        x).GetAwaiter().GetResult();
        }
        static Body RunSync(Func<Task<Body>> x)
        {

            return Task.Run(

        x).GetAwaiter().GetResult();
        }

        static Spirit RunSync(Func<Task<Spirit>> x)
        {

            return Task.Run(
x).GetAwaiter().GetResult();
        }




        /// <summary>
        /// Get resource named by BodyEmitter.SpiritResource, in the form of IEntity.
        /// </summary>
        public static IEntity GetEmitterResourceAsEntity(BodyEmitter emitter)
        {


            string path = string.Empty;
            IEntity entity = null;


            if (string.IsNullOrEmpty(emitter.SpiritResource))
                return null;


            if (emitter.LastEntityLoaded != null)
            {
                entity = emitter.LastEntityLoaded;
            }
            else
            {
                PreloadEntity(emitter, ref entity);
            }

            return entity;
        }






        public static void PreloadEntity(BodyEmitter emitter)
        {
            IEntity entity=null;
            PreloadEntity(emitter, ref entity);
        }

         private static void PreloadEntity(BodyEmitter emitter,  ref IEntity entity)
        {
       
                string path = "";
                try
                {
                    if (string.IsNullOrEmpty(emitter.SpiritResource))
                        return;

                    //even w particles off basic functionality like hyperlinks, bullets,  and emitter views need to work so let those though
                    if (!SimWorld.IsParticleOn && !emitter.IsInfoFlagged(BodyInfo.SpawnOnly) && !emitter.Parent.IsInfoFlagged(BodyInfo.ShootsProjectile))
                        return;

                    if (LooseFiles)
                    {
                        entity = LoadEntity(emitter, ref path);
                    }

                    if (entity == null)
                    {
                        entity = GetEmitterEmbeddedResourceAsEntity(emitter);
                    }
                                 
                    emitter.LastEntityLoaded = entity;
                }

                catch (Exception exc)
                {
                    Debug.WriteLine("exception in  GetEmitterResourceAsEntity , cant load,  will set LOOSEFiles to off in production " + path + exc.Message);
#if PRODUCTION
                    LooseFiles = false;// stop trying 
#endif
                }
          
        }


        const int bufsize = 200000;
        //this is duplicated in Tool SimWorld, TODO see if we can eliminate
        private static IEntity LoadEntity(BodyEmitter emitter, ref string path)
        {

 
                IEntity entity = null;
                path = Serialization.GetSpiritPath(emitter.SpiritResource);

                if (emitter.SpiritResource.ToLower().EndsWith(".body"))
                {
                    entity = Serialization.LoadDataFromFileInfo<Body>(new FileInfo(path));
                    SetupPreloadedBodyProperty(entity);
                }
                else if (emitter.SpiritResource.ToLower().EndsWith(".wyg"))
                {

                    if (!DebugView.LoadThumbnails)// don't  load preload anything ,or map , will immediate mode draw will put the current filename text in place if it got the file.                                      
                        return null;// designed will want system to keep trying to find the file as he spells it

                    if (Graphics.Graphics.LevelProxyMap.ContainsKey(emitter.SpiritResource))
                    {
                        entity = Graphics.Graphics.LevelProxyMap[emitter.SpiritResource];
                    }
                    else
                    {
                        path = Serialization.GetMediaLevelPath();
                        path = System.IO.Path.Combine(path, emitter.SpiritResource);

                        byte[] thumnail = new byte[bufsize];//make sure whole thing gets loaded at once, chunking not implemented 

                        FileInfo fi = new FileInfo(path);
                        thumnail = Serialization.LoadThumbnailDataFromFileInfo<Level>(fi, ref thumnail);


                
                        if (fi.Exists // make proxy anyways so it wont keep trying to load missing thumnails.
                            || (thumnail != null && thumnail.Length > 0))
                        {
                            entity = new LevelProxy(thumnail, emitter.SpiritResource);
                            Graphics.Graphics.LevelProxyMap.TryAdd(emitter.SpiritResource, entity);
                        }
                    }

                    //    entity = Serialization.LoadDataFromFileInfo<Level>(new FileInfo(path));  //if we do this have to set Level.Instance back to parent its set on deserialized and stuff gets in the root world, better not to 
                }
                else  //try as a spirit
                {
                    entity = Serialization.LoadDataFromFileInfo<Spirit>(new FileInfo(path));
                }

                return entity;
            
        }

        //TODO is this repeated?  dont we have preload? LOOK alwasy preload in one pass at beginning

        //do we have to supprot drawtime .. and additions.. when SpiritResouce  changed listen and preload in tool to elimt this 

        /// <summary>
        /// Get resource named by BodyEmitter.SpiritResource, in the form of IEntity.
        /// </summary>
        private static IEntity GetEmitterEmbeddedResourceAsEntity(BodyEmitter emitter)
        {
           
                string path = string.Empty;
                IEntity entity = null;

                try
                {
                    path = emitter.SpiritResource;

                    if (emitter.SpiritResource.ToLower().EndsWith(".body"))
                    {

                        entity = Serialization.LoadDataFromAppResource<Body>(path);
                        Body body = entity as Body;
                        SetupPreloadedBodyProperty(entity);
                    }
                    else if (emitter.SpiritResource.ToLower().EndsWith(".wyg"))
                    {
                        if (!DebugView.LoadThumbnails)
                            return null;
                        
                        if (Graphics.Graphics.LevelProxyMap.ContainsKey(emitter.SpiritResource))
                        {
                            entity = Graphics.Graphics.LevelProxyMap[emitter.SpiritResource];
                        }
                        else
                        {
                 
                            byte[] thumnail = new byte[bufsize];//make sure whole thing gets loaded at once, chunking not implemented 
                            thumnail = Serialization.LoadThumbnailDataFromAppResource<Level>(emitter.SpiritResource, ref thumnail);

                            if (thumnail != null && thumnail.Length > 0)
                            {
                                entity = new LevelProxy(thumnail, emitter.SpiritResource);
                                Graphics.Graphics.LevelProxyMap.TryAdd(emitter.SpiritResource, entity);
                            }
                         
                            return entity;//thumnial missing , level name will be displayed, soreturn before it logs an error message about missing resource,
                        }
                        //  entity = Serialization.LoadDataFromAppResource<Level>(path);  // this screws up everyting, level.Instance would have to be set back and vectors are mixed up
                    }
                    else
                    {
                        entity = Serialization.LoadDataFromAppResource<Spirit>(path);
                    }

                    if (entity == null)
                    {
                        Debug.WriteLine("GetEmitterResourceAsEntity can't find resource " + path);
                    }

                    return entity;
                }

                catch (Exception exc)
                {
                    Debug.WriteLine("exception in  GetEmitterResourceAsEntity , cant load " + path + exc.Message);
                    return null;
                }

        }


        /// <summary>
        /// Mark preloaded body entity with WasSpawned.
        /// this prevent preloaded bullet get saved in level. prevent black bullet bug in center of level.
        /// </summary>
        private static void SetupPreloadedBodyProperty(IEntity entity)
        {
            if (entity != null && entity is Body)
            {
                (entity as Body).Info |= BodyInfo.WasSpawned;
            }
        }


        public void AddEmittedEntityToCurrentLevel(IEntity entity)
        {


            //thjs prevents swordsman duplicated when going back to level where he's originated from.
            //TODO SPAWNING   give instances unique names, across all levels.. level born in,  unique spiritID + pluginclass + level number born in.. ID

            if (DoesSpiritNameExistInLevel(entity))  //TODO this assumes each spirit has unique name, its not true. Levels need to be edited.. They will be named given the level num and class but if there are more than one of one class in one level then must name them
            {
                return;
            }

            SetWorld(entity);

            _level.CacheAddEntity(entity);
        }

        private static void SetWorld(IEntity entity)
        {
            Spirit sp = entity as Spirit;

            if (sp != null)
            {
                foreach (var b in sp.Bodies)
                {
                    SetUpPhysics(b);
                }

                sp.AuxiliarySpirits.ForEach(SetWorld);
            }
            else
            {
                Body body = entity as Body;
                SetUpPhysics(body);
            }
        }



        private static void SetUpPhysics(Body b)
        {
            if (b != null)
            {
                if (b.World == null)
                {
                    b.ResetStateForTransferBetweenPhysicsWorld();
                    b.World = World.Instance;
                    b.RebuildFixtures();
                   
                }
            }
        }

        public void OnSpawnEntity(BodyEmitter emitter, Vector2 worldForce, Vector2 velocity, double lifeSpan)
        {
            //TODO check here if this is called for all.
     
            if (!IsParticleOn && !emitter.IsInfoFlagged(BodyInfo.SpawnOnly)&& !emitter.Parent.IsInfoFlagged( BodyInfo.ShootsProjectile))
                return;

            IEntity entity = GetEmitterResourceAsEntity(emitter);

            emitter.LastEntityLoaded = entity;

            if (entity == null)
            {
                return;
            }

            SetUpSpawnedEntity(emitter, worldForce, velocity, lifeSpan, entity);

            AddEmittedEntityToCurrentLevel(entity);

            emitter.LastEntityLoaded = null;



        }


        public void OnSpawnCachedEntity(BodyEmitter emitter, IEntity cachedEntity, Vector2 worldForce, Vector2 velocity, double lifeSpan)
        {
            if ((!IsParticleOn && !emitter.IsInfoFlagged(BodyInfo.SpawnOnly) || cachedEntity == null)
                && !emitter.Parent.IsInfoFlagged(BodyInfo.ShootsProjectile))
                return;


            SetUpSpawnedEntity(emitter, worldForce, velocity, lifeSpan, cachedEntity);

            AddEmittedEntityToCurrentLevel(cachedEntity);

            emitter.LastEntityLoaded = null;
        }




        /// <summary>
        /// Return TRUE if spirit entity with same name already exist in level.   Automatically Named spirits are named after the level they were spawned in
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private bool DoesSpiritNameExistInLevel(IEntity entity)
        {
            if (entity is Spirit)
            {
                Spirit spirit = entity as Spirit;
                if (!spirit.Name.ToLower().Contains("noname") && _level.GetSpiritWithName(spirit.Name) != null)
                {
                    return true;
                }
            }
            return false;
        }


        private void SetUpSpawnedEntity(BodyEmitter emitter, Vector2 worldForce, Vector2 velocity, double lifeSpan, IEntity entity)
        {
            Vector2 offset = emitter.Offset;
            offset.X = MathUtils.GetRandomValueDeviationFactor(offset.X, emitter.DeviationOffsetX);
            offset.Y = MathUtils.GetRandomValueDeviationFactor(offset.Y, emitter.DeviationOffsetY);

            //TODO FUTURE finish .. allow wind to reverse direction .. just add offset left  .. this would be a "wrap around" emitter..
            /*     if ((emitter.Parent.Info &= BodyInfo.InMargin) != 0)
                 {
                     if (spirit.MainBody.DragCoefficient != 0)  //allow wind to reverse directino , emitt from other side
                     {
                         IField winddragField = WindDrag.WindField;
                         if (winddragField != null)
                         {
                             Vector2 windField = winddragField.GetField(emitter.WorldPosition + offset);
                         }
                     }
                 }*/

            //todo GRAPHICS_MG MABYE LOAD STUUFF..BUT no view needed, at draw timg this will get done
            if (entity is Spirit)
            {
                Spirit spirit = entity as Spirit;
                SetUpSpawnedSpirit(emitter, worldForce, velocity, lifeSpan, ref spirit, ref offset);

                //TODO GRAPHICS_MG

                //   InitBodyEmitterViews(spirit);
                //CAUSED DUPLICATE PARTICLA BODY ADD, todo mg_grpahics Reviews WITH BODY VIEWS

            }
            else if (entity is Body)
            {
                Body emittedBody = entity as Body;

                if ((emitter.Info & BodyInfo.SpawnOnly) == 0)
                {
                    SetUpSpawnedBodyProperty(emitter, ref worldForce, ref velocity, emittedBody);
                }

                emittedBody.Position += (emitter.WorldPosition - emittedBody.WorldCenter);

                if (!emitter.FixedRotation)
                {
                    Vector2 worldDir = emitter.Parent.GetWorldVector(emitter.Direction);  //accounts for for emitter rotation and body rotation.
                    float rotation = (float)Math.Atan2((float)worldDir.Y, (float)worldDir.X);
                    emittedBody.Rotation = rotation;

                }

                emittedBody.Info |= BodyInfo.WasSpawned;
            }
        }


        private static IEntity GetEntityFromXMLString(BodyEmitter emitter, string spiritString)
        {
            IEntity entity;

            // InstantiateObjectFromXMLString did not work with IEntity.. or object.. , TODO FUTURE think of a know a better way
            if (!emitter.SpiritResource.ToLower().EndsWith(".body"))
            {
                entity = Serialization.LoadObjectFromString<Spirit>(spiritString);
            }
            else
            {
                entity = Serialization.LoadObjectFromString<Body>(spiritString);
            }
            return entity;
        }


        private void SetUpSpawnedSpirit(BodyEmitter emitter, Vector2 worldForce, Vector2 velocity, double lifeSpan, ref Spirit spirit, ref Vector2 offset)
        {
            MoveSpirit(ref spirit, emitter.WorldPosition + offset);

            spirit.LifeSpan = lifeSpan;
            spirit.WorldAABBExistenceLimits = AABBStaticObjects;
            spirit.WorldAABBExistenceLimits.Combine(ref emitter.Parent.AABB);   //now used for clouds so they disappear when leaving the level, as particles do.

            spirit.World = this.Physics;
            spirit.SetParentLevel(Level.Instance);

            if ((emitter.Info & BodyInfo.SpawnOnly) != 0)
            {
                spirit.MainBody.Info |= BodyInfo.SpawnOnly;   //SpawnOnly means dont apply emitter properties to the emitted object, but also , we  don't remove this spirit on out of bounds as we do spawned clouds or dust or other ambience. TODO should be a separate flag. 
            }
            else   // Apply the force and velocity   and the properties 
            {
                spirit.Bodies.ForEach(x => SetUpSpawnedBodyProperty(emitter, ref worldForce, ref velocity, x));
                spirit.WorldAABBExistenceLimits.Expand(1.1f, 1.1f);
            }

            // Spirit Particle setup, so the spirit will know on its Update, that this is special spirit that act as particle?  what?   special.. TDOO clarify this commnt and behavior.. acts as particle means it just gets removed after leaving the bounds.. oh god so sad with these hacks DWI.
            spirit.WasSpawned = true;

    

            if (!string.IsNullOrEmpty(emitter.PluginName))
            {
                spirit.PluginName = emitter.PluginName;
                spirit.SpiritFilename = emitter.SpiritResource;
            }


            spirit.IsCallingPlugin = (bool)emitter.IsCallingPlugin;


            if (!string.IsNullOrEmpty(emitter.Name))
            {
                spirit.Name = emitter.Name;
            }

            _level.SetNameofSpawnedSpirit(spirit);   //so that it wont be spawned again.. TODO consoldate this with remove spawned entities..
        }




        private void SetUpSpawnedBodyProperty(BodyEmitter emitter, ref Vector2 worldForce, ref Vector2 velocity, Body body)
        {
            body.LinearVelocity = Vector2.Add(emitter.Parent.LinearVelocity, velocity);
            // body.ApplyForce(worldForce); // NOTE this is supposed to apply force to center of mass, but I see a Torque after, to work around farseer 3.1 bug, using below call.
            body.ApplyForce(worldForce, body.WorldCenter);//this works.. Torque is zero after .
            body.LinearVelocityPreviousFrame = body.InvMass * worldForce * _physicsUpdateInterval;//incase Vector2 blank collision this is needed  to initialze this for
            emitter.SetProperties(body);
            // TODO .. see if putting particles in separate world will help.. pretty sure no.. time is spent in graphics adding to SL tree.  should write particle to Writeable

        }



        #endregion


        #region Ray, Laser 


        public void ClearRayViews()
        {
            Sensor.Instance.ResetRayViews();
            Sensor.Instance.SetClearPending();
        }


        private Dictionary<RayInfo, LineSegment> _mapRayToView = new Dictionary<RayInfo, LineSegment>();



        public Dictionary<RayInfo, LineSegment> RayViews => _mapRayToView;


        //this is create views of rays, for both lasers and sensors
        private
             void Sensor_OnRayCreated(RayInfo rayInfo)
        {


            if (rayInfo.RayType != RayType.eRayLaser && !IsSensorRayVisible)
            {
                return;
            }



                LineSegment view = default;
                if (_mapRayToView.ContainsKey(rayInfo))
                {
                    view = _mapRayToView[rayInfo];
                }
                else
                {
                    view = new LineSegment();
                    _mapRayToView.Add(rayInfo, view);
                }
        

                view.X1 = rayInfo.Start;
                view.Color = DebugView.GetXNAColor(rayInfo.RayColor);

                // view.Stroke = new SolidColorBrush(Color.FromArgb(rayInfo.RayColor.A, rayInfo.RayColor.R, rayInfo.RayColor.G, rayInfo.RayColor.B));
                // view.StrokeThickness = rayInfo.RayThickness * (1 / Graphics.Instance.CTransform.Zoom);

                if (rayInfo.IsIntersect)   //TODO FUTURE LASER consider to  draw inside cut..  maybe half way.. or allow cut multiple..   using part of its energy ( cutLenght -= cutDist).. untill weak then let it just stop at hit like before
                {
                    view.X2 = rayInfo.Intersection;
                }
                else
                {
                    view.X2 = rayInfo.End;
                }

        
        }

        private void Sensor_OnRayDestroyed(RayInfo rayInfo)
        {
            if (_mapRayToView.ContainsKey(rayInfo))
            {
                LineSegment line = _mapRayToView[rayInfo];
                _mapRayToView.Remove(rayInfo);

                //TODO remove form display list
            }

        
        }

        private void OnLaserOff(LaserEmitter emitter, string laserID)
        {
            if (Sensor.RayMap.ContainsKey(laserID))
            {
                RayInfo info = Sensor.RayMap[laserID];
                Sensor.RayMap.TryRemove(laserID, out _);


                if (_mapRayToView.ContainsKey(info))
                {
                    LineSegment view = _mapRayToView[info];

                    //remove from Displalist TODO
                    _mapRayToView.Remove(info);
                }
            }
        }






        protected Fixture OnSpawnLaser(LaserEmitter emitter, Vector2 endVector2, string laserId, float laserThickness, out Vector2 hitPos)
        {
            //TODO OPTIMIZE LASER.. the ray cast seems backwards... the last value for fraction is the nearest.  so to get the first, avoiding 
            //alot of o(n) near phase collision which are tossed...would switch the end and begin... try that.
            //observed this with one simple polygon.. tracing the tree, it gave the back pt first.

            //i think custom code to make it faster.  backwards cast is all.  if laser is to shine through cut.. more .. 
            //want the furthers Vector2 then.   so would leave it forward and return the first hit.. ( would need to change the API to do this)
            //
            //see Box2d latest maybe
            // use gpu for these its 1000x faster   CPU should be rigid body only

            RayInfo ray = Sensor.LaserCast(emitter.WorldPosition, endVector2, laserId, emitter.Parent, emitter.Color);

            hitPos = endVector2;

            if (ray != null)
            {
                ray.RayColor = emitter.Color;
                ray.RayThickness = emitter.Thickness;
                hitPos = ray.Intersection;
                return ray.IntersectedFixture;   //TODO do cut here or callback..
            }


            return null;
        }


        #endregion

        /// <summary>
        /// Process cached add/remove/update of level entity.
        /// This should be called from safe thread.
        /// Cache should be empty when this method returns.
        /// </summary>
        public void ProcessLevelDelayedEntity()
        {
            if (_level == null)
            {
                return;
            }

            try
            {
                // for delayed entity creation
                while (_level.DelayedEntityList.Count > 0)
                {
                    KeyValuePair<IEntity, EntityOperation> pair = _level.DelayedEntityList.Dequeue();
                    switch (pair.Value)
                    {

                        case EntityOperation.Add:
                            try
                            {
                                _level.Entities.Add(pair.Key);
                                if (pair.Key is Spirit)
                                {
                            

                                    Spirit sp = pair.Key as Spirit;


                                    _level.AddComplexSpirit(sp);


                                }

                            }
                            catch (Exception exc)
                            {
                                Debug.WriteLine("error adding new body" + exc.ToString() + pair.Key.GetType().ToString());
                            }
                            break;

                        case EntityOperation.ReplaceShapes:
                            {


                                try
                                {

                                    Body body = pair.Key as Body;
                                    ReplaceShapes(body);

                                }
                                catch (Exception exc)
                                {
                                    Debug.WriteLine("error in EntityOperation.ReplaceShapes" + exc.Message);
                                }

                                break;
                            }

                        case EntityOperation.Remove:
                            _level.Entities.Remove(pair.Key);
                            break;

                        case EntityOperation.SpiritRegenerate:
                            pair = ReplaceSpiritWithRegrowing(pair);
                            break;
                    }
                }   // end of while loop


                // now for delayed view replacement
                if (_level.DelayedEntityViewList.Count > 0)
                {
                    Dictionary<IEntity, short> temp = new Dictionary<IEntity, short>(_level.DelayedEntityViewList);
                    foreach (KeyValuePair<IEntity, short> pair in temp)
                    {
                        // if delay 0, update view and remove from delay list
                        if (pair.Value <= 0)
                        {
                            _level.DelayedEntityViewList.Remove(pair.Key);
                        }

                        // if delay > 0, decrement its delay to be processed again on next cycle
                        else
                        {
                            _level.DelayedEntityViewList[pair.Key] = (short)(pair.Value - 1);
                        }
                    }
                }

                //TODO  graphics_mg sort BodyList by draworder

                // reduce bone vertices


#if SIMPLIFYBROKENBODIES  //todo give liftspan disintegrate to marks
                while (_level.BodiesBrokenOffToHaveCollisionShapeSimplified.Count > 0)
                {
                    Body body = _level.BodiesBrokenOffToHaveCollisionShapeSimplified.Dequeue();

                    const float minimumEdgeSizesSq = 0.05f * 0.05f;  // 0.26 is dist verts  in hip.      checked all body parts are 4 verts after this.

                    bool hasOnlyShortFaces;
                    body.RemoveSmallFacesOnPolygonalCapsule(minimumEdgeSizesSq, out hasOnlyShortFaces);   //bigger than the smallest face we want to remove..   TODO 
                    body.OnCollision += CollisionEffects.OnCollisionEventHandler;

                    if (hasOnlyShortFaces)
                    {
                        _level.Entities.Remove(body);  //probably a tiny part regrowing.. better just remove it... causes piling issues and such
                    }

#if ADDBROKENENTITIESTOLEVEL// mayen in game not tooll..enitity gets update  any other reason.. body can update ad kill itself if older thna life or degen
                     _level.Entities.Add( body);  //this is so broken parts are in the level..  not sure if good for PRODUCTION ( for save game feature) ,  TODO might be an iterationg cost or double views.just so i can save file and look at the part..
#endif

                    //TODO hands could be simplified at wrist..  
                    //should be retest if very different system enters gaem.. or use a Percent of perimiter or of longest..  works on balloon , creature and most scales.
                }
#endif

            }
            catch (Exception exc)
            {
                Logger.Trace("exception in ProcessLevelDelayedEntity" + exc.Message + exc.StackTrace);
            }
        }

        private static void ReplaceShapes(Body body)
        {



#if PRESERVE_FIXTURES // in our implementation for cutting we replace the GeneralVerts and retesselate the body.. much easier than cutting all the fixtures.  this is a partial implementation
                                
                                List<PolygonShape> shapes;
                                if (!Level.Instance.MapBodyToNewShapes.TryGetValue(body, out shapes))
                                {
                                    Debug.WriteLine( "unexpected.. missing fixtures for this body ", body.PartType.ToString());
                                    return;
                                } 
                                
                                body.OnCollision -= CollisionEffects.OnCollisionEventHandler; // is case its already listenened to, as in travel across level..                          
                                body.DestroyAllFixtures();

                                foreach ( PolygonShape shape in shapes)
                                {
                                    PolygonShape polygonShape = new PolygonShape( shape.Density );
                                    body.CreateFixture(shape);  //OPTIMIZE.. very minor.. UpdateMassData is getting called for all fixtures evey fixture.. but its quick                           
                                }

                                body.OnCollision += CollisionEffects.OnCollisionEventHandler;  //this will listen to all the new fixtures, made from the old ones + the cut one(s)
#else



            //todO MOVE THIS ?  
            body.OnCollision -= CollisionEffects.OnCollisionEventHandler;
            body.RebuildFixtures();
            body.OnCollision += CollisionEffects.OnCollisionEventHandler;   //this will listen to collide for all the new fixtures


            //todo GRAPHICS_MG
            //  Graphics.Instance.Presentation.CreateOrReplacePrimaryBodyView(body, true, true); // TODO  OPTIMIZATION dont have to reload dress.. add a param.. share dress.... however its still mostly graphics code thats heavy.. and its rasterized.. no big deal.  let the system do the work.


#endif


        }

        private KeyValuePair<IEntity, EntityOperation> ReplaceSpiritWithRegrowing(KeyValuePair<IEntity, EntityOperation> pair)
        {

            try
            {
                Spirit injuredSpirit = pair.Key as Spirit;

                // ignore if broken spirit is not from level
                if (injuredSpirit != null && _level.Entities.Contains(injuredSpirit) == true)
                {
                    if ((string.IsNullOrEmpty(injuredSpirit.SpiritFilename) && injuredSpirit.PluginName != null && ////HACK alert.. we prollly shouldnt get here if these are not setup
                        injuredSpirit.PluginName.Contains("YndrdPlugin")))
                    {
                        injuredSpirit.SpiritFilename = "Namiad.sprx";    //default to player character, and for old levels

                        Trace.TimeExec.Logger.Trace(2, "regen", "spritfilenaem segt to Namiad.sprx");

                    }

                    //   https://stackoverflow.com/questions/17248680/await-works-but-calling-task-result-hangs-deadlocks/32429753#32429753
                    Spirit newSpirit = LoadSpiritForRegenerate(injuredSpirit.SpiritFilename);

                    if (newSpirit != null)
                    {
                        newSpirit.SetParentLevel(_level);
                        //newSpirit.Mind.Parent = newSpirit;
                        // replace injured spirit with new spirit. 
                        // view creation for new spirit will take place here.
                        // reposition & rotate also take place here.

                        //newSpirit.ApplyScale(0.5f, 0.5f);   // test..   now it doesnt work  because joints list are not collected yet and emitters are not scaled properly in local space.

                        string name = injuredSpirit.PluginName;
                        newSpirit.PluginName = null;   //this is so that plugin wont be compiled, it will be referencing the old one.

                        IPlugin<Spirit> injuredSpiritPlugin = injuredSpirit.Plugin;//grad a reference to injuredSpirit before its deleted.

                      
                        ReplaceSpirit(injuredSpirit, newSpirit, true, injuredSpiritPlugin);  // this will remove and unload injuredSpirit
                        newSpirit.PluginName = name;

                        // adjust body parts of new spirit to match the injured one.
                        // scale & temp collidable take place here.
                        newSpirit.RegenBasedOnInjuredSpirit(injuredSpirit, injuredSpiritPlugin, SimWorld.Instance.Sensor);

                        // NOTE 1: if we able to scale down before inserting into phyiscs, might
                        // eliminate one-frame flicker when growing parts created at initial full-size,
                        // and then scaled down to tiny size.
                        // however that need complex modification on Body.ScaleLocal().
                        // would need to model in spirit space.. now we are using joints  to model.. so we need to gradually shrink to avoid teleport issues.


                        // NOTE 2: even if we able to scale down first before inserting into physics,
                        // there's no guarantee that those small parts are already positioned
                        // close to MainBody when inserted into physics. 
                        // Bug of growing parts outside ship will still occurs if those tiny
                        // scaled bodies still positioned outside ship when inserted into physics.
                        // appendum.. we now scale as IsNotCollidable and invisible.. so should not be a problem excpet a  little physical balance may look odd.

                        // Note  ... could scale small enough so its inside body .. is not collidable could cause same probelm gradually shrinking parrts
                        //if fixed properly ( scale model data first)  all these notes can be erased..

                        // NOTE 3:  For now the LFE Grow, skips the view update for one frame
                        // utilized Body.IsNotCollidable for one frame, since body is added to physics at full scale, can be outside the space ship. 
                        // when replacing spirit.  the body part start scale should be usually small enough and inside the main body..

                        //NOTE 4.  mass  of tiny body needs to be increased , so that mass ratio
                        //with main body is not extreme and joint contraint can be solved .  This is done in LFE and 
                        //carefully not reset in plugin for toes..

                        World.Instance.ProcessChanges();
                    }
                }
                return pair;
            }
            catch(Exception ex)
            {

                Debug.WriteLine("except in ReplaceSpiritWithRegrowing" + ex, "regen");
                return pair;
            }
        }




        /// <summary>
        /// New spirit not yet inside level and phycis.
        /// Old spirit (the one to be replaced) should still inside level and physics.
        /// Old spirit will be removed at the end of this method.
        /// </summary>
        public  void ReplaceSpirit(Spirit oldSp, Spirit newSp, bool addToLevel, IPlugin<Spirit> oldplugin)
        {
            // set new active spirit, if old one is active spirit
            if (oldSp == Level.Instance.ActiveSpirit)  //this is done twice and once below.. if fixing .. check camera shake on regrow , and check passing one levle to next.
            {
                Level.Instance.ActiveSpirit = newSp;
                //   ActiveSpirit.UpdateAABB();  AABB was copied from old one, not needed
            }

        //    oldSp.Bodies.ForEach(x => x.Enabled = false);//hopefully clear contacts but not ruin state


            newSp.Bodies.ForEach(x => x.Enabled = false);//this didnt fix it but might helpp

         //   newSp.Bodies.ForEach(x => x.IsStatic = true);//TODO test..attempt fix for jumping after spriti replace.. mabye it needs to be static for a few frames.. 
            // copy transform before view creation.   //eye regrow issue, tried commenting this out ..
            if (addToLevel)
            {

                //TODO #PRIORITY #APPSTORE #PORT should fix on next release. according to Catto on ragdolls 2012 GDC presentation on ragdolls, this is a general problem  .  joints are under strain and body systems are distored.  a new one , then having the pos and rot set,
                //will be suddently under this strain that mabye took frames to sag under gravity and solver   .   so, the pos and vel of the mainbody,  could be applied, then maybe just the joint angles state..
                //the rest here are just blind trials without the wisdom  of the ragdoll master catto

                new SetBias(newSp, "relaxAfterGen", 0.3, 0.0f, null);// doesnt seem to hurt, prevents jumping on replace  //TODO might not be needed.. the issue was tunneling due to instant shrinking of leg.. now its done in steps
                //this rotates all the bodies to match, we disable the joints for the first few frames in case they need to flip 180 ..TODO   check.. i dont think we do anymore
                newSp.CopyTransformsByPartType(oldSp);
                // copy its collision group id
               // newSp.CloneCollisionGroupId(oldSp);

            }

            MapStuckItems(oldSp);

            // copy heldbodies from old spirit, they will gone when spirit removed from level.
            // will be held by new spirit after it's inside level.
            List<Body> heldBodiesFromInjured = new List<Body>(oldSp.HeldBodies);

            // copy its plugin state also ? for now regen will instantiate new plugin

            // remove old spirit  .. CLUE TO REPLACE JOINT WEIRDNESS.. ONCE EYES DID NOT Vector2 ON REPLACEDMENT SPIRIT, DUE TO JOINTLIST BEING NULL ON BODIES..
            //copy paste works fine in tool , eyes move..
            _level.Entities.Remove(oldSp);
           
            oldSp.Bodies.ForEach(x => x.Enabled = false);//hopefully clear contacts but not ruin state

            oldplugin.Parent = newSp;
            // view creation for new spirit occurs here
            if (addToLevel)
            {
                SimWorld.Instance.AddEmittedEntityToCurrentLevel(newSp);
              //  _level.Entities.Add(newSp);
                // grab held item again
            
                newSp.RestoreHeldItemsFromOriginal(heldBodiesFromInjured, SimWorld.Instance.Sensor, oldplugin);
            }
         
            //we dont want them to collide w eachother
            newSp.Bodies.ForEach(x => x.Enabled = true); // this didnt fix it but might help
          //  newSp.Bodies.ForEach(x => x.IsStatic = false);//this make it stop happening not sure.. leaving it for demo.
        }

        public void ReloadNewCreatureAtPosition(float x, float y)
        {
            BodyEmitter be;
            CreatePlayerCharacterEmitter(x, y, out Body dummy, out be);

            Level.Instance.Entities.Add(dummy); // so emitter will be activated
            //Physics.RemoveBody(dummy); // no need for it in physics engine.   see below cacheremove comment

            be.CheckToCacheEntity();

            Level.Instance.Entities.Remove(Level.Instance.ActiveSpirit);
            Level.Instance.ActiveSpirit = (Spirit)be.LastEntityLoaded;
            Level.Instance.ResetSpiritMindRefs();

            //TODO future ( or never)   clean this.. not on delete body the emitters are cleared ( for cut issue)
            //so we cant remove it even this way.. for now not collidabel static bodies are not an issue
            // Level.Instance.CacheRemoveEntity(dummy);

        }

        public void AddBody(Body body)
        {
            if (!body.IsNotCollideable)
            {
                if (body.FixtureList == null)
                    body.RebuildFixtures();

                body.CheckToCreateProxies();
            }

            if (body.InvMass == 0)
            {
                body.ResetMassData();
            }

            World.Instance.AddBody(body);
        }

        public void RemoveBody(Body body)
        {

            // remove collision listener
            if (body.FixtureList != null)
            {
                foreach (Fixture f in body.FixtureList)
                {
                    f.ClearOnCollisionListeners();
                }
            }


            body.Enabled = false; //this will destroy proxies


            World.Instance.RemoveBody(body);

        }

        //used to move a creature, create an emitter and respawn it.
        private void CreatePlayerCharacterEmitter(float x, float y, out Body dummy, out BodyEmitter be)
        {
            dummy = new Body(Physics);
            dummy.IsNotCollideable = true;
            dummy.IsStatic = true;
            dummy.IsVisible = false;
            dummy.Position = new Vector2(x, y);

            be = new BodyEmitter(dummy, Vector2.Zero, Physics);
            be.Active = true;
            be.Info |= BodyInfo.SpawnOnly;
            be.SpiritResource = "Namiad.spr";
            be.MaxNumberOfParticles = 1;
            be.IsCallingPlugin = true;
        }


        private static void MapStuckItems(Spirit oldSp)
        {
            foreach (Body b in oldSp.Bodies)
            {
                foreach (AttachPoint ap in b.AttachPoints)
                {
                    if (ap.Name == Body.BulletTemporaryTag && ap.Joint != null)
                    {
                        oldSp.MapAttachPtToStuckBodies.Add(ap, ap.Partner.Parent);
                    }
                }
            }
        }

        /// <summary>
        /// Load new spirit to replace current spirit that need to regenerate.
        /// Call this in respond to SpiritRegenerate event.
        /// </summary>
        /// <param name="levelName">Name of spr file that defines  new spirit.</param>
        private Spirit LoadSpiritForRegenerate(string sprName)
        {
            Spirit regenSp = null;
            try
            {
                using (new BodyEmitter.NoFixtureLoading())
                { 

                if (Serialization.UseGameAssemby)
                {
                    regenSp = Serialization.LoadDataFromAppResource<Spirit>(sprName);

                }
                else
                {

                    regenSp = Serialization.LoadDataFromFileInfo<Spirit>(new FileInfo(Serialization.GetSpiritPath(sprName)));
                }

            }

            }
            catch (Exception exc)
            {
                Debug.WriteLine("load spr regeneration failed" + exc.Message);
                return null;
            }

            return regenSp;
        }




    }
}