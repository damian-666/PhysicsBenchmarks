//#define SHOWFORCES
#define SPLASHES

// work and force clipping should do the same thing
/////items in wind should ultimately move at same speed

using System;
using System.Net;
using System.Windows;


using System.Collections.Generic;
using System.Diagnostics;

using System.Threading;

using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Controllers;
using FarseerPhysics.Common;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Collision;
using FarseerPhysics.Dynamics.Particles;

using Core.Data.Entity;
using Core.Data;

using System.Linq;
using Core.Game.MG;
using System.Threading.Tasks;

namespace Core.Game.MG.Simulation
{




    /// <summary>
    /// provide a drag against motion  in air  or fluid due to shape, also applies the bouncies and mighty apply a reaction for on the fluid or surface.
    /// takes a wind field as a parameter
    ///  http://en.wikipedia.org/wiki/Drag_(physics)
    ///  
    /// Drag of a flat plate perpendicular to flow  is  1.98 to 2.05 in 2d
    /// with Foils pressure drag and friction drag both act.  NOTE only forms draw is done, no friction drag for the tangential air is implemented
    /// </summary>

    //  https://en.wikipedia.org/wiki/Drag_coefficient

    public class WindDrag : FarseerPhysics.Controllers.Controller//  this is for debugging... see the IField wind drag is refering to.
    {


       static public bool ViewRays =
#if (PRODUCTION)
       false;   //TODO the keys never get cleaned on particles.. might leak forever.. need to clean this.      
#else
       true;
#endif


#if SHOWFORCES
        static bool ViewForceRays = ViewRays; //can show forces in parallel
#else
        static bool ViewForceRays = false;
#endif

        static BodyColor forceColor = BodyColor.Yellow;
        static BodyColor forceColor2 = new BodyColor(255, 180, 0, 255);
        static BodyColor forceBouyancyColor = new BodyColor(100, 100, 100, 255);

        static Dictionary<Body, bool> MapNoSignificantFaces = new Dictionary<Body, bool>();

        static BodyColor _waterColor;
        public static BodyColor WaterColor     ///TODO darken water in day,   lighten at night for contrast.
        {
            get
            {
                if (_waterColor == null)
                {
                    //note color has a special use.. must match particles color for particles to die on enter water.  TODO put a special use flag instead  HACK
                    _waterColor = new BodyColor(0, 100, 245, 200);  //greenish blue
                }

                return _waterColor;
            }

            set { _waterColor = value; }
        }

        public static bool IsRayViewVisible
        {
            get { return ViewRays; }
            set
            {
                if (ViewRays != value)
                {
                    ViewRays = value;
                    if (value == false)
                    {
                        Sensor.Instance.ResetRayViews();
                        Sensor.Instance.SetClearPending();
                    }
                }
            }
        }

        /// <summary>
        /// Allows us to exclude the wind controller completely
        /// </summary>
        static public bool IsWindControllerOn { get; set; } = true;

        const float forceScale = 0.1f;  // forces are too big to show on screen
        /// In world coorinates.   part of the world mapped to the visible part of the canvas  
        static public AABB _currentViewport;

        //TODO move to WindDragConstants.

        public const float MphToMs = MathUtils.KmhToMs / MathUtils.KphToMph;

        static int _bodyCount;
        static float _dt;

        static private WindDrag instance;  //is a singleton
        static WindFieldCollection _windFields = new WindFieldCollection();
     
        //extra dont check blocking for particles in light wind..
        //TODO consider removal for this.... only use MinAccel..
        public const float MinWindBlockCheckVelSquareParticleDrag = 60f;  //for particles , dont check blocking for particles with low velocity.... test in hull of fast falling ship tho..ship particles should fall ..
        const float _minWindBlockCheckVelSquare = 60; //8  //dont check blocking in very light winds or slow movement..

        const float _minWindVelSquareSound = (3f * MathUtils.KmhToMs) * (3f * MathUtils.KmhToMs);  //minimum to check light  wind for soulnd

        //TODO  test this with gusts, and falling off cliff and with balloon..test level 1.. TODO separate wind and drag components.
        //parts like arms and legs move fast durnig steps..  so  this is actually very expensive.. a lot of ray casts.
        //TODO optimize walking in light wind..
        //TODO separate  falling from wind i think.. and dont check blocking unless strong wind or falling fast..
        //note most is handled by  BodyMinAccel
        //tuned/  but  tested with sword on  rock on ground in light 10 m/ sec wind..  
        //tested with sleeping small rock.  tested with sleeping small rock.  turnip and sword.  tested strong wind can wake it up. //TODO min Torque?
        //LARGE  BOULDER IS NOT BE WOKEN BY WIND UNLESS IT WILL BUDGE..   
        public static float DefaultAirDensity = WindDragConstants.DefaultAirDensity;
        public static float DefaultTemperature = WindDragConstants.DefaultTemperature;     //  21f;  //celcius
        static int _portionCount;   //threading , how many bodies in a portion of  body list into
        static int _numThread;

        /// <summary>
        /// Standard angle used for wind blocking shadow
        /// </summary>
        static public float RayAngle = 0.4f;

        static Body _mainCharHeadBody;

        /// <summary>
        /// when a face under drag  is about to exit the water, on next frame            
        ///  body, normalWorld, applicationVector2, velInEdgeNormalDirection, vertices[i1], vertices[i2]);
        /// </summary>


        //TODO make bubble turn to foam and stop rising in sky.                         
        public delegate void FaceCrossWaterHandler(Body body, Vector2 normalWorld, Vector2 applicationVector2,
        float velInEdgeNormalDirection, Vector2 vertex1, Vector2 vertex2);
        static public FaceCrossWaterHandler OnFaceCrossWater;

        public override void Update(float dt)
        {

            if (SimWorld.Instance == null )
                return;

            if (!SimWorld.IsWindOn)  //allows us to have the controller but and toggle its effect
                return;

            _dt = dt;// SimWorld.Instance.PhysicsUpdateInterval;
            _bodyCount = World.BodyList.Count;

            if (_bodyCount == 0)
                return;

            FindHeadBodyOnListener();

            if (SimWorld.HasUIAccess)//we can now spawn threads when called fro uI step.. pause and drag spirit but we cant see rays so leaving this in for now
            {
                for (int i = 0; i < World.BodyList.Count; ApplyAirEffect(World.BodyList[i++], true)) ;
            }
            else
            {
              Parallel.For(0, World.BodyList.Count, x => ApplyAirEffect(World.BodyList[x], true));

            }
  
        }

        private void FindHeadBodyOnListener()
        {
            if (Level.Instance != null && Level.Instance.ActiveSpirit != null)
            {
                _mainCharHeadBody = Level.Instance.ActiveSpirit.Head;
            }
            else
            {
                _mainCharHeadBody = null;
            }
        }



#if DEBUG
        static bool oneShot = false;
#endif

        private void ApplyAirEffect(Body body, bool doParticles)
        {
            try
            {
                if (_windFields.Count() == 0)
                {
#if DEBUG
                    if (!oneShot)
                    {
                        Debug.WriteLine("No IField spirits with plugins providing IField found in level Using defaut wind drag");
                        oneShot = true;
                    }
#endif
                }

                if (body == _mainCharHeadBody)
                    return; // will be handled in plugin  so it can make wind sound heard if not blocked by arm

                //TODO consider skipping thigh and upper arms.. need to economize this.  wind is slow.
                //for balloon every 2nd panel..
                body.LinearDamping = 0;// TODO LEVELS.. CLEAN IT OUT LATER for now just make sure this is zero was setting set in some levels..  otherwise falling in capsule is too weightless

                if (body.IsStatic || body.DragCoefficient == 0)
                    return;

                //TODO turn on collide if body marked debris is about to hit something..
                if (body is Particle) //assuming circle for now.
                {
                    Particle particle = body as Particle;

                    if (body.IsNotCollideable)
                    {
                        if (!doParticles)
                            return;

                        if (!particle.SkipRayCollisionCheck && CheckParticleCollision(particle, _dt))
                            return; //particle lifespan set to 0
                    }

                    float diameter = particle.ParticleSize;    // close enough to radius for now. if IsCollidabe is off, there is no Fixture in play..  and radius is not stored on Partilce  either 
                    //Todo check threading test this on ui thread..  suspected tree issue.. TODOO test with no ynrdrd.. or no ray ynrdd.
                    //does this even use rays???

                    //WindDrag.ApplyAirFlowDamping(body, diameter*diameter, 0.01f, windVel, 0.2f, (float)_dt, SimWorld.Instance.Sensor);
                    //try just one ray for blocking test blood in wind .

                    //TODO changing this 0.1  will cause ray extension to be not correction lenght .. TODO fix this.  too long an it disappears early..  need to check inside also of give lifespan based on intersect len 
                    // can we know particle accel?
                    //higher  effect i 0.6 default  so blood,  snow, or wind,  can get caught in wind and retain its density for better collision response and less tunnelling.
                    //smaller particls at noraml density do get picked up in wind.. but are hard to see, so
                    //we add a higher coefficient for particles.   mass is proportional  radius squared   and wind force  to radius.. so smaller like dust can get picked in wind 
                    float rayAngle = 0.0f; //use just one ray for partilce..  usuall 0.2 for other stuff..
                    WindDrag.ApplyAirFlowDamping(body, diameter * diameter, rayAngle, (float)_dt, SimWorld.Instance.Sensor, null);
                }
                else
                {
                    const float minEdgeSize = 0.19f;              //.19 is just under foot size.
                    WindDrag.ApplyAirFlowDamping(body, minEdgeSize * minEdgeSize, RayAngle, (float)_dt, SimWorld.Instance.Sensor, null);
                    //float minFaceSize = (body.AABB.Width + body.AABB.Height) / 4;  //TODO also need to take into account density or mass or size
                    //WindDrag.ApplyAirFlowDamping(body, minFaceSize * minFaceSize, 0.5f, windVel, 0.2f, (float)_dt, SimWorld.Instance.Sensor);        
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine("exc in apply air effect" + exc.Message);
            }
        }

        public class WindFieldCollection : List<IField>, IField
        {

#region IField Members


            //TODO EULERIAN combining grids and fields needs to be done better..  dont take make density , apply each separtely etc.


            /// <summary>
            /// Gets the atmospheric state for a position as a  field.
            /// </summary>
            /// <param name="pos">input body position</param>
            /// <param name="density">air density at this position</param>
            /// <param name="temperature">temp at this postion</param>
            /// <returns>Air velocity</returns>
            public Vector2 GetVelocityField(Vector2 pos, out float density, out float temperature)
            {
                //TOOD this is repeated with the exclude one, its in a tight multi-threaded loop.  however could just pass null for exclude
                Vector2 vel = Vector2.Zero;

                if (Count == 0)
                {
                    temperature = DefaultTemperature;
                    density = DefaultAirDensity;
                    return vel;
                }
                else
                {
                    float sumTemperature = 0;
                    density = 0; //will take the max
                    float densityComponent = 0;
                    foreach (IField field in this)
                    {
                        vel += field.GetVelocityField(pos, out densityComponent, out temperature);

                        if (densityComponent > density)   // for complex winds, made by sums, just take the highest , dont want to add it up.  also good for water region in air
                        {
                            density = densityComponent;
                        }
                        sumTemperature += temperature;
                    }

                    temperature = sumTemperature;   //   add temps , use case:multiple bombs or flames...  energy sources  combining..
                    return vel;
                }
            }


            public AABB GetSourceAABB()
            {
                throw new Exception("misusue, Dont call this for the entire collecion, see WindBlockRegion");
            }
#endregion

            public IEntity GetSourceEntity()
            {
                return null;
            }

#region IField Members
            public bool GetIsExclusive()
            {
                throw new Exception("misusue, Dont call this for the entire collecion, see WindBlockRegion, particle");
            }

            public bool IsEulerian()
            {
                return false;
            }

            #endregion
        }


        //Attach a physics sound controller to this world.
        public WindDrag(World physicsWorld) :
            base(ControllerType.WindDrag)
        {

            if (!IsWindControllerOn)
                return;

            physicsWorld.AddController(this);
        }

        public static void AddWindField(IField field)
        {

            if (!IsWindControllerOn)
                return;

#if DEBUG
            if (_windFields.Contains(field))
            {
                Debug.WriteLine("field collected twice");
            }
#endif
            IEntity entity = field.GetSourceEntity();

            if (_windFields.Any(x => x.GetSourceEntity() == entity))
            {
                Debug.WriteLine("field for same entity  collected twice, could be recompilation of plugin.  Use AddorReplaceWindField unless its on performance critical");
            }

            _windFields.Add(field);
        }

        public static void AddorReplaceWindField(IField field)
        {


            if (!IsWindControllerOn)
                return;

            if (_windFields.Contains(field))
            {
                _windFields.Remove(field);
            };

            IEntity entity = field.GetSourceEntity();  // field can be implemented in plugin which is recompiled in tool, but spirit object will be the same
            IField oldField = _windFields.FirstOrDefault(x => x.GetSourceEntity() == entity);

            if (_windFields.Contains(oldField))
            {
                _windFields.Remove(oldField);
            };
            _windFields.Add(field);
        }

        public static void RemoveWindField(IField field)
        {

            if (!IsWindControllerOn)
                return;

            _windFields.Remove(field);
        }

        public static IField WindField
        {
            get
            {
                return _windFields;
            }
        }

        public static WindFieldCollection WindFields
        {
            get
            {
                return _windFields;
            }
        }


        //this can be used to find just other fields..  currently not used  TODO FUTURE ERASE
        public static Vector2 GetVelocityField(Vector2 pos, out float density, out float temperature, IField exclude)
        {
            Vector2 sum = Vector2.Zero;

            if (_windFields.Count == 0)
            {
                density = DefaultAirDensity;
                temperature = DefaultTemperature;
                return sum;
            }

            float sumTemperature = 0;
            density = 0; //will take the max
            float densityComponent = 0;

            foreach (IField field in _windFields)
            {
                if (field == exclude)
                    continue;

                sum += field.GetVelocityField(pos, out densityComponent, out temperature);

                if (densityComponent  >density)   // for complex winds, made by sums, just take the highest , dont want to add it up.  also good for water region in air
                {
                    density = densityComponent;
                }
                sumTemperature += temperature;
            }

            temperature = sumTemperature;
            return sum;   //don't count us.,. TODO.. what if there are more than one blocking areas?    if they are not overlapping should not be an issue..

        }


        static public void PlaceWindDragController()
        {

            if (!IsWindControllerOn)
                return;

            if (instance != null && World.Instance.Controllers.Contains(instance))  //only one is needed
                return;

            instance = new WindDrag(World.Instance);  //we keep this reference only for the above check.
        }


        public static bool IsEffectivelyBlocked(Body body, Vector2 applicationPoint, IField parentField)
        {

            if (!IsWindControllerOn)
                return false;

            List<Body> ignoredList = new List<Body>(1);
            ignoredList.Add(body);

            Vector2 windField = Vector2.Zero;

            Vector2 applicationPt = body.WorldCenter;

            // TODO  should we mark rain as SkipWindBlockCheck.. check fps

            float airDensity, temp;

            //dont include our own wind field

            //NOTE todo our field apply small forces moving our grid around due to the drag and windfield not automatically excluding any parent

            //one fix would be add a Field field to the bodies in this and exclude its own

            windField = GetVelocityField(body.WorldCenter, out airDensity, out temp, parentField);

            if (!windField.IsValid())
            {
                windField = Vector2.Zero;
            }

            Vector2 relVelocity = body.GetLinearVelocityFromWorldPoint(ref applicationPoint) - windField;

            Vector2 rayWindVector = relVelocity * 2;

            return IsEffectivelyBlocked(applicationPt, rayWindVector, WindDrag.RayAngle / 2, body.GetHashCode().ToString(), null, ignoredList, false, true);



        }

        public static void ApplyAirFlowDampingParticle(float diameter, Body body, float airDensity, Vector2 windField, float temp, float windBlockAngle, float dt, Sensor sensor)
        {
            float dragCoefficient = body.DragCoefficient;

            if (body is Particle && ((body as Particle).DragDeviation != 0))
            {
                dragCoefficient = MathUtils.GetRandomValueDeviationFactor((body as Particle).DragCoefficient, (body as Particle).DragDeviation);
            }


            if (dragCoefficient == 0)
                return;

            if (!body.LinearVelocity.IsValid())
                return;

            //TODO consider factoring out repeat with  verts.. and head
            bool ignoreGround = true;  //TODO FUTURE  if wind field exists adjusted flow  along  ground .. ground might block.. for now just ignore it.

            float rayExtensionFactor = 1.0f;// using 2 is other bodies   1 is ok for particles..
            Vector2 applicationVector2 = body.WorldCenter;
            // TODO  should we mark rain as SkipWindBlockCheck.. check fps
            Vector2 rayWindVector = -windField * rayExtensionFactor;

            List<Body> ignoredList = null;
            if (!body.IsNotCollideable && !(body is Particle))
            {
                ignoredList = new List<Body>(1);
                ignoredList.Add(body);
            }

            //TODO should consolidate this with faces its alot of repeat code 
            // Drag at high velocityMain article: Drag equation
            //(i.e. high Reynolds number, Re > ~1000), also called quadratic drag.
            // F the force of drag, 
            // D the density of the fluid,[12] 
            // Vthe speed of the object relative to the fluid,     "squared"
            // Cthe drag coefficient (a dimensionless parameter, e.g. 0.25 to 0.45 for a car) 
            // A the reference area,   ( length for 2d world) 

            //F = 1/2 * C * D  * A sq .. we are now using impulse F / M * dt  = dV to make sure not to exceed particle velcity and blow it backwards.  //TODO look up impulse
            //can happen with thick fluid or very light object like Cloud TODO do this for flying and balloon .. and torque.
            //impulse =  Force * time       
            dragCoefficient *= 2f;  //so we dont have to tune old levels.  //TODO wtf?   

            float airFrictionFactor = 0.5f * airDensity * dragCoefficient * diameter;// * newModelFactor;

            //TODO do it this way for all .. separate blocking wind and falling ..  factor out code..
            //or could try one ray.. for rain or blood particles..       
            //TODO ignore ground may be needed.. SHOULD filter out stuff like ground before narrow phase..

            Vector2 edgeVelocityRelativeToAir = Vector2.Zero;


            Vector2 relVelocityAirToBody = windField - body.LinearVelocity;  //airSpeed..         

#if WINDPARTICLEIMPULSE
            Vector2 windPushImpulse = Vector2.Zero;
#endif
            //if ((body.Info & BodyInfo.DebugThis) != 0)
            //{
            //    Debug.WriteLine(body.Info.ToString());               
            //}

            //TODO  if level contains wind particle emitters  use query to find wind fields, dont add them up.  dont block particle ones..
            //
            //TODO 

            bool isUnderWater = airDensity >= WindDragConstants.MinLiquidDensity;

            //  World.Instance.QueryAABB();
            if ( //ifield.IsEulerian()
                   SkipCheckBlocking(windField, body, _minWindBlockCheckVelSquare) || isUnderWater
                || !IsEffectivelyBlocked(applicationVector2, rayWindVector, windBlockAngle / 2, body.GetHashCode().ToString(),
                null, ignoredList, ignoreGround, ViewRays))
            {

#if WINDPARTICLEIMPULSE    //TODO don't know if there is an advantage to IMPULSE. for now its the same result as clipped force
                windPushImpulse = relVelocityAirToBody * airFrictionFactor  * dt;  //    
                //body.ApplyLinearImpulse(windPushImpulse);  ///theres no limit to this..  hurricane wind might pick it up//todo add a way to limit so that one frame wont make it go pas windField      
#else
                edgeVelocityRelativeToAir -= windField;  //particle is not blocked  from wind...       //This is negative since we want relative bodies relative velocity in wind field .       
#endif
                //TODO test rocket in snow , rain, etc..
                if (temp > 500 && (body is Particle) && !body.IsInfoFlagged(BodyInfo.Fire))  // kind of a hack.. allow red sparks to exist but not other shite like snow..
                {
                    (body as Particle).LifeSpan = 0;// evaporate it.   
                    return;
                }
            }

            //now for the   other drag component ( static air friction against movement based on bodies interia and forces. )
            Vector2 bodyVelRay = body.LinearVelocity * rayExtensionFactor;

#if WINDPARTICLEIMPULSE   //TODO   apply CFL like condition.. cant move more DX in one frame..and not allow to go negative ( montonic )
            Vector2 windDragImpulse = Vector2.Zero;
#endif
            //now check flying / falling blockangle  ( combined with wind) ..   tested bleeding in falling airship in wind..  fighting in winds.          
            //if blocked here, particle is probably contained in hull  or falling behind something , blocked from air damping..
            if (SkipCheckBlockingVelSq(bodyVelRay, body, MinWindBlockCheckVelSquareParticleDrag) || airDensity >= WindDragConstants.MinLiquidDensity || !IsEffectivelyBlocked(applicationVector2, bodyVelRay, windBlockAngle / 2, body.GetHashCode().ToString() + "down", null, null, ignoreGround, ViewRays))
            {
                //TODO WIND remove the component directly against wind direction.. help not pass through balloon thas just raised into jet stream with clouds.
                //or better , put a wind shock wave  field directly in front of moving panels..  use a spatial tree moving with balloon
                // for clouds that burst , if pushed into wind direction.. goint  do'nt negate the effect.. so just skip  the damping.  help prevent cloud parts getting inside balloon..
                //    if ((body.Info & BodyInfo.Cloud) != 0)  // 
                //    {
                //project using dot product into wind dir.. just damp that..  TODO WIND  or not have to if using wind field on balloon face lookup, thats better will do a turbulens flow around , a wake..
                //     }                   
#if WINDPARTICLEIMPULSE
                Vector2 velfactor = relVelocityAirToBody;

                if (relVelocityAirToBody.LengthSquared() > 1)//for small velocity otherwise it never converges.. for small velocity its proportial to the Vel from Wiki 
                {
                velfactor *= relVelocityAirToBody.Length();// 
                }
                windDragImpulse =  dt * velfactor  /* -body relative to air*/ * airFrictionFactor;// * body.Mass     
                body.ApplyLinearDragImpulse(ref windDragImpulse);  // this will limit drag to a dead stop during one frame, not make it go backwards.       not sure if its needed tho.                                  
#else
                edgeVelocityRelativeToAir += body.LinearVelocity;   //result is  relative velocity in wind field .   This is how we achieve damping..
#endif
            }

            // same could be done with falling large object ( wake and pressure wave...) 
            //deflect rain.. try raster..  make rain thicker.. consider  using ray on emitter  first..  see if anything around..
            //to check graphics hit test?
            float velSq = edgeVelocityRelativeToAir.LengthSquared();

            if (velSq > 10000000 && body is Particle)//case it gets in a bad state somehow.. particles flying  supper high speed..
            {
                (body as Particle).LifeSpan = 0;
                //  Debug.WriteLine("edgeVelocity Squared > 100000, particle removed");
            }

#if !WINDPARTICLEIMPULSE
            Vector2 windForce = -edgeVelocityRelativeToAir * airFrictionFactor;
            //this should be done but is not working yet.. for both body and particle
            //      Vector2 limitedForce = body.LimitAirForceField(ref windForce, dt, -edgeVelocityRelativeToAir);          
            //    body.ApplyForce(ref limitedForce);

            body.ApplyForce(ref windForce);

            // Debug.WriteLine("force " +windForce.ToString());
            //  Debug.WriteLine("impulse force " + (windDragImpulse / dt).ToString());  // make sure these are the same..
#endif
            if (
            !(body is Particle)
            //      && (body.Info & BodyInfo.Cloud) != BodyInfo.Cloud
            )
            {
                if (ViewRays && sensor != null && ViewForceRays)
                {
#if !PRODUCTION
#if WINDPARTICLEIMPULSE   //TODO Maybe clean
                 sensor.AddRayNoCast(applicationVector2, applicationVector2 + windDragImpulse / (dt), "wind force impuse/dt" + body.GetHashCode().ToString(), forceColor2);
#else

                 sensor.AddRayNoCast(applicationVector2, applicationVector2 + windForce * forceScale, "wind force" + body.GetHashCode().ToString(), forceColor);
#endif
#endif

                }
            }
            return;
        }

        /// <summary>
        /// don't both casting ray trace for blocking  if wind is small  or  if body is marked not to care
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="body"></param>
        ///    <param name="windForceMag?></param>
        /// <returns></returns>
        private static bool SkipCheckBlocking(Vector2 ray, Body body, float windForceMag)
        {
            //TODO we might just use (windForceMag / body.Mass < Body.MinAccel)))//  in every case.. makes more sense..
            return (SkipCheckBlocking(ray, body) ||
                 (!body.Awake && (windForceMag / body.Mass < Body.MinAccel)));//if its not going to budge the object.. dont bother  check blocking.  it wont apply this force anyways            
        }

        private static bool SkipCheckBlocking(Vector2 ray, Body body, ref Vector2 windForce)
        {
            return (SkipCheckBlocking(ray, body) ||
                 (!body.Awake && (windForce.LengthSquared() / body.Mass * body.Mass < Body.MinAccelSq)));//if its not going to budge the object.. dont bother checker
        }

        private static bool SkipCheckBlocking(Vector2 ray, Body body)
        {
            return ((body.SkipWindBlockCheck
                    || ray.LengthSquared() < _minWindBlockCheckVelSquare));
        }

        public static bool SkipCheckBlockingVelSq(Vector2 ray, Body body, float minWindBlockCheckVelSquare)
        {
            return ((body.SkipWindBlockCheck || ray.LengthSquared() < _minWindBlockCheckVelSquare));
        }

        //treat head a a circle, and heards wind sound  if not blocked.
        public static void ApplyAirFlowDampingOnHeadWithSound(Body body, float airDensity, float dragCoefficient, Vector2 windField, float windBlockAngle, float dt, Sensor sensor, IEnumerable<Body> ignoredList, string soundKey)
        {

            if (!IsWindControllerOn)
                return;

            //TODO factor this out w/  collide as particle.. and use it for Door opening .. piece.. sound.. relevant to that also
            //need to check two components  separately .. wind and air resistance..
            //TODO this is repeat code with above , factor out.. 
            //TODO can we afford the 4 rays for all body verts..    thats the correct way .. see if bugs a obvious with trees and stuff..
            //   its most important here since affects sound..            //without it jumping or un crouching near blockage makes wind suddenly get "unblocked" 
            Vector2 edgeVelocity = Vector2.Zero;
            Vector2 edgeVelocityForSound = Vector2.Zero;
            bool ignoreGround = true;
            float rayExtensionFactor = 3.0f;// using 2 is other bodies   1 is ok for particles..  for a tall tree its needs to be 3  can block lot of wind in 2dworld

            float minWindVolume = 0;

            Vector2 applicationVector2 = body.WorldCenter;

            const float minaudibleLevel = 0.1f;
            const float windBlockFactor = 0.2f;   // block 80% of the sound..  allow blocked wind to create some some (leak) or new sound source..from there.. like open door..     
            //TODO sound less  if blocked further away..
            bool hasEnoughWindForEffect = false;

            //TODO check blocknig full or narrow , test on hill... ignore main body??
            if (windField.LengthSquared() >= _minWindVelSquareSound)
            {
                hasEnoughWindForEffect = true;

                Vector2 rayVector = -windField * rayExtensionFactor;
                //TODO do it this way for all .. separate blocking wind and falling ..  factor out code..
                //TODO check ignore ground is needed.. SHOULD filter out stuff like ground before narrow phase..

                if (airDensity >= WindDragConstants.MinLiquidDensity || SkipCheckBlocking(windField, body, _minWindBlockCheckVelSquare) || !IsEffectivelyBlocked(applicationVector2, rayVector, windBlockAngle, body.GetHashCode().ToString(), null, ignoredList, ignoreGround, ViewRays))
                {
                    edgeVelocity -= windField;
                    edgeVelocityForSound -= windField;
                }
                else
                {
                    edgeVelocityForSound -= windField * windBlockFactor; //  allow blocked wind to create some some (leak) or new sound source..from there.. like open door..                                   
                    //TODO might be quieter in balloon with door closed..  or add another sound for that..
                    //or check  if also blocked with   a wide angle..  
                }

                minWindVolume = minaudibleLevel * 2;  //we have some audible wind.. dont see it get blocked completely..                 //particle is not blocked  from wind     
            }
            //      Debug.WriteLine(body.LinearVelocity.LengthSquared());

            //TODO can we consolidate this.. with the    Skipblock check , etc..
            // need to make sure walking  jumping is not enough to create  audible wind..  //TODO tune this more..       
            float minWindVelSquareSound = 60;  //use average speed?  TODO this seems high for actual speed.. i think its due to shaking on joint graph..

            //TODO use faster wind.. less .2  Coefficeint.. what is 6 meter/sec mean..  what does our man travel at..?      
            //this part is for wind caused by freefall..  TODO revisit absolute vel here, and hopefully do all  blocking with grids instead, test with thin obects.


            if (body.LinearVelocity.LengthSquared() > minWindVelSquareSound)  //now if other component ( static air friction is high enough check for blocking in front) 
            {
                hasEnoughWindForEffect = true;
                Vector2 rayVector = body.LinearVelocity * rayExtensionFactor;
                //now check flying / falling blockagle  ( combined with wind) ..   tested bleeding in falling airship in wind..  fighting in winds.
                if (!IsEffectivelyBlocked(applicationVector2, rayVector, windBlockAngle / 2, body.GetHashCode().ToString() + "down", null, ignoredList, ignoreGround, ViewRays))
                {
                    edgeVelocity += body.LinearVelocity;
                    edgeVelocityForSound += body.LinearVelocity;
                }
                else
                {
                    edgeVelocityForSound += body.LinearVelocity * windBlockFactor;   //  allow blocked wind to create some some (leak) or new sound source..from there.. like open door..                               
                }

                minWindVolume = minaudibleLevel * 2;  //we have some audible wind.. dont see it get blocked completely..                 //particle is not blocked  from wind          
            }

            if (!hasEnoughWindForEffect)
            {

                if (AudioManager.Instance.IsPlaying(soundKey))
                {
                    AudioManager.Instance.StopSound(soundKey);
                }
                return;
            }

            Vector2 windForce = -edgeVelocity * dragCoefficient * airDensity * body.AABB.Width;  //for head just assume almost square or  round.
            body.ApplyForce(windForce, body.WorldCenter);


            if (ViewRays && sensor != null && ViewForceRays)
            {
                sensor.AddRayNoCast(applicationVector2, applicationVector2 + windForce * forceScale, "wind force" + body.GetHashCode().ToString(), forceColor);
            }

            //TODO background  ambient wind.. + separate track in your ear wind..    FUTURE 5.1 surrond sound for  backgrond..if ever in silverlight 
            if (airDensity > WindDragConstants.MinLiquidDensity)  //TODO  mute all sounds head underwater..?   
                return;


            if (SimWorld.EnableSounds)
            {
                PlayWindSound(body, soundKey, ref edgeVelocity, ref edgeVelocityForSound, minWindVolume, minaudibleLevel);
            }

        }

        private static void PlayWindSound(Body body, string soundKey, ref Vector2 edgeVelocity, ref Vector2 edgeVelocityForSound, float minWindVolume, float minaudibleLevel)
        {
            if (soundKey == null)
                return;

            // how about
            //( 6 to 30 meters sec?)    vol =  speed * 0.03;?   
            float vol = Math.Min(edgeVelocityForSound.Length() * 0.07f, 1);

            //wind is loud  when rushing by ears.. turn ear to wind you cant hear it as loud
            float windAngleToHead = body.AngleToBody(edgeVelocity);
            float angleFactor = (float)Math.Sin(windAngleToHead);

            float factor = 0.4f;  //dont close of all the sound from head angle..  allow 40 percent through ..

            //and more on high vell.. TODO like sky dive..
            vol = vol * factor + (Math.Abs(angleFactor) * (1 - factor) * vol);
            vol = Math.Max(minWindVolume, vol);
            //   Debug.WriteLine("headwindrelVel" + edgeVelocity.Length().ToString());  

            if (vol >= minaudibleLevel && !AudioManager.Instance.IsPlaying(soundKey))  //TODO check pitch and volume? 
            {
                AudioManager.Instance.PlaySound(soundKey, vol);//TODO possible breakup here.. maybe need to ramp volume..
            }
            else
            {
                AudioManager.Instance.SetVolume(soundKey, vol);
                //AudioManager.Instance.SetVolumeStepped(soundKey, vol);  //not implemented in SL would be easy if needed to prevent breakup
                //  Debug.WriteLine("vol" + vol.ToString());
            }
        }

        public static void ApplyAirFlowDamping(Body body, float minimumEdgeSquaredSize, float dt, Sensor sensor)
        {
            Vector2 airField = Vector2.Zero;
            ApplyAirFlowDamping(body, minimumEdgeSquaredSize, 0, dt, sensor, null);
        }

        /// <summary>
        /// Apply a simulated air resistance to a convex shape.   assuming air aill around.
        /// force is opposite to movement, and propertional to face direction and normal to body velcoty .
        /// force is applied 
        /// </summary>
        /// <param name="windField"> wind flow field ( todo provide a field method callback for finer detail at each sample Vector2 vertex).   for now use body CM</param>
        /// <param name="windBlockAngle">The wind block angle . two rays will be cast at this angle in direction of movement..   if both are intersect , this wind is blocked.  consider falling in an airship hull.</param>
        /// <param name="sensor"></param>
        /// <param name="ignoredList">blocking rays will ignreo this bodies,  usually our own systems..</param>
        /// <param name="body"></param>
        /// <param name="minimumEdgeSquaredSize"></param>
        /// <param name="airDensity"></param>
        /// <param name="dragCoefficient"></param>
        /// <param name="windField"> </param>
        /// <param name="windBlockAngle"></param>
        /// <param name="dt"></param>
        /// <param name="sensor"></param>
        /// <param name="ignoredList"></param>
        public static void ApplyAirFlowDamping(Body body, float minimumEdgeSquaredSize, float windBlockAngle, float dt, Sensor sensor, IEnumerable<Body> ignoredList)
        {
            ApplyAirFlowDamping(body, body.DragCoefficient, minimumEdgeSquaredSize, windBlockAngle, dt, sensor, ignoredList);
            return;
        }

        public const string SplashTag = "spl";

        const float DensityFactorWater = 4f;  //for swords and such,   they explode the water due to rouding issues or going to less that zero on the v squared componnet.
                                              //TOOD... find a real solution.. find a moment , when shoould do thsi.
                                              //NOTE.. swords in air are made extra light so that they can be carried and fast.. in waster, might as well make them metal .. density.. = 200..

        const float DragFactorWater = 0.25f;


        public static void ApplyAirFlowDamping(Body body, float dragCoefficient, float minimumEdgeSquaredSize, float windBlockAngle, float dt, Sensor sensor, IEnumerable<Body> ignoredList)
        {

            // try to determine flux  
            // along a 5 m pole .. the force applied  should not be just at the verts.. a 1 m pole should have less .
            //but we are using edge length.. to multiple the force..
            //for v squared fast fluid drag...   v  per body is ok for slow v drag ( laminar flow model) .
            // this will not support small body in little vortex.. future..  for this need to get windfield Inteface in here anyways an call for each Vector2
            ILiquidRegion iLiquid;

            if (body is Particle || body.ApplyDragAsParticle)  //treat cloud as particle so it can merge into a stream
            {




                ApplyParticleDrag(body, windBlockAngle, dt, sensor);
            }

            bool inAirContainer = false;
            //check for convergence ( slow to stop no gravity or buoyancy)..
            //tune if / when laminar under paddle speeds.. should be vel 2 on power stroke..  needs to be such that force is less on return stroke,
            //either way.. slow vel square  less than 1 is slower i think.
            //  if (body.IsInfoFlagged(BodyInfo.DebugThis) 
            //  || body.IsInfoFlagged(BodyInfo.AirContainer) 
            //   )
            {

                if (   //TODO when leaping over peak waves... might need to fatten this more base on vel...  or will get a vertical colum  NOTE
                IsFatAABBUnderWater(body, out iLiquid)
                )    //any part of AABB is in water
                {
                    SetInWaterAdjustmentFlag(body, true);  //they explode, especially when held..


                    //TODO  add a DragCoefficent to Body.. that sucks tho.. the bug is in here..  affects clouds too..

                    //  else 
                    //  {
                    //
                    //      body.DragCoefficient *= DragFactorWater;
                    //  }


                    iLiquid.ApplyBouyancyForces(body, out inAirContainer);
                    //Fix wind block thing.  Fix the 
                    //added this because swords and other seem unstable.. rocks back and forth.. might be due to fucntion vel sq in high density..
                    //need to be tweaked.. laminar vs turnbulent.. best for  paddling.. slowly retracting fins. needs to work.
                    // remove this.. try sword and other in water..

                    //TODO  WATER.. maybe objects are metal but light for handling ..need to be made to sink..  wood floats however.. and its more dense
                    //due  to walking on them...  same with surfboards.. .. might need an extra factor in the body.   (  BouyancyFactor) 

                    //TODO test bullets in water.   ( now they jump around.. smoke needs to be a bubble)    pops with smoke a bonus..
                    ///TODO keep mapp bool Body.. add or  remove bubble or splash emitters.  like bload rotation ellipses with bouyancy.
                    //blood needs to impart force  //TODO blood ready should.. on collide if not colliable.. if enough pressure..

                    //TODO why can force get unstable in high density ..check Nan.
                    //gun will spin forever.. flapping can go unstable                   
                    // Debug.WriteLine( "damping" + dampingTorque.ToString());            
                    //TODO low importance at slow speeds damping never converges on flat still water..   damping sould be like slinding friction.. less friction at high speeds in ocean code
                    //TODo cast a ray along boat to find contained air.. add this - submerged area to , TODO other water methods support this..
                    // buoyancy..
                }
                else
                {
                    SetInWaterAdjustmentFlag(body, false);
                }

            }

            if (body.ApplyDragAsParticle && !(body is Particle))  //NOTE this is mostly for hands feet and toes , which are treated as particles... we do not have exact Vector2s to emit splashes from...
            //they can be under the body and emerge out, should look fine
            {
#if SPLASHES
                DoSplashOrPushedBubble(body, dt);
#endif
                return;
            }

            if (body is Particle)  // we are done, applied drag and buoyancy to bubbles if needed
                return;

            Vertices vertices = body.GeneralVertices;

            //this is for the Kris ( curvy knife) , it has bumpy sides..  just approximate it as one long thing
            if ((body.Info & BodyInfo.UsePointToHandleForDragEdge) != 0)
            {
                if (body.AttachPoints.Count < 1 || body.SharpPoints.Count < 1)
                    return;

                vertices = new Vertices(2);
                vertices.Add(body.AttachPoints[0].LocalPosition);
                vertices.Add(body.SharpPoints[0].LocalPosition);
            }

            bool computeNormals = false;
            if (body.Normals == null || body.Normals.Count != vertices.Count)//TODO CODE REVIEW have tool clear this on grip moved.. handle this in Body
            {
                body.Normals = new Vertices(vertices.Count);
                computeNormals = true;
            }

            int numSignificantFaces = 0;
            for (int i = 0; i < vertices.Count; ++i)
            {
                //code was adapted from loop in    public void Set(Vertices vertices) on polygon shape
                int i1 = i;
                int i2 = i + 1 < vertices.Count ? i + 1 : 0;
                Vector2 edge = vertices[i2] - vertices[i1];

                //I believe winding of GeneralVertices is always counterclockwise.   TODO  possible issue on breaking  items like clouds tho.       
                Vector2 normal;
                if (computeNormals)
                {
                    normal = new Vector2(edge.Y, -edge.X);  // or cross product -1, edge
                    normal.Normalize();
                    body.Normals.Add(normal);
                }
                else
                {
                    normal = body.Normals[i];
                }


                float edgeLenSq = edge.LengthSquared();  //TODO cache lengths?  profile this..

                //TODO average adjacent  small piece, average normals , add them up until > minimumEdgeSquaredSize and  , do one panel.  
                //consider foot for this.
                //TODO IMPORTANT  if no panel is more than  40 % or is small permiter.. just treat as particle, one ray. like Head.
                //tested vegetables in wind..
                // measure whole surface length.   (perimeter)  if face is < x percent, dont apply .. or apply to middle only    
                //TODO IMPORTANT if density is High.. divide max lenght by that.. not use casting rays for stones in this case if it will not be affected by ligther winds..
                //look consider to  at this.. dont bother with face close to joints either GetClosestEmitter(joint.WorldAnchorA, ;
                // and average groups of small  edges  that are convex..?
                //TODO replace with with the PermiterSq, if a percentage of that.. for now just on small items like the leaves of turnip.
                //note some old creatures have calf roundness that escapes all wind, bad for swimming.
                if (edgeLenSq < minimumEdgeSquaredSize)  //don't bother with little edges..   TODO consider below method for larger objects. might skip  some insignificant parts..
                {
                    if (Math.Sqrt(edgeLenSq) < (body.PerimeterOfEdges * 0.2f))  //if 20 percent of perim, even if small ,  then do this face.   test with stems or grass pieces or leaves.                   
                        continue;
                }

                numSignificantFaces++;
                //TODO if numSignificantFaces is only one.. apply a particle force to center also.

                Vector2 normalWorld = body.GetWorldVector(normal); //check that it Vector2s away from cm?

                //TODO consider blocking only if its not moving toward you.. at least with static air restance..
                //this should prevent balloon colliding.
                //also add a prevent collide param.. if about  to hit ( close ray) do a separation force..
                //mark balloon piece with that property?

                //TODO adjust win field for static ground features.   cliffs, etc.. along faces..
                Vector2 faceMidVector2 = (vertices[i2] + vertices[i1]) / 2;
                // for now  if joint exist.. just do midVector2.. or unless really long face like airship hull > 2.5 meters
                int numSampleVector2s;

                bool panelRayFromInsideCenter = false;

                //TODO FUTURE  if any convex body shaped like panel , with adjacent edges nearly parallel in system.. then treat this way to avoid excess ray casts.
                //this is now a blocking ray cast on both sides of the object.. this is needed for concave items.
                //for now we mark them as UseSingleDragEdgePanel

                //TODO OPTIMIZE IDEA might refine this to avoid maybe blcoking rays on complex bodies with lot of faces .. consider angle of edge..   consider a flatish section  with slight angle between the pieces, just consider it one face.   
                //do 1/ 2 or zero sample Vector2 for this face ..  and with fl leaves..    

                // http://www.iforce2d.net/b2dtut/buoyancy   improved with help from this article.

                //TODO optimze dont check density if no water
                bool canUserOnePt = UseOnePtOnEdge(body, vertices[i2], vertices[i1]);

                if (canUserOnePt)   // no need for two rays to figure out if concave body is blocking  a face.
                {
                    numSampleVector2s = 1;
                    panelRayFromInsideCenter = true;
                }
                else
                //NOTE TODO  floppy  weak creature explodes when joints are loose.. first consider the rope joints.. also consider the two forces here, might be unstable.
                //possible go to one.
                //This was comented out after  putting the guy in the water.  want no blocking of currents  and 2 forces per bone  //TODO ERASE..  dont mark UseSingleDragEdgePanel   on large systems then.
                //dont know any system that is marked as UseSingleDragEdgePanel and has larger thatn 2.5 meter panels, dont remember why this was done.
                //    if ( canUserOnePt &&  body.JointList != null && (body.Info & BodyInfo.UseSingleDragEdgePanel) != 0 && edgeLenSq < (2.5 * 2.5))  //TODO REFINE OPTIMIZIATION wind  blocking rays are very expensive if part is part of body system  and not very long , just do middle.. TODO "long" should be relative to system.. 
                //    {
                //        numSampleVector2s = 1;
                //     }
                //     else
                {
                    numSampleVector2s = 2;  // this allows torque to happen on an object. stop it spinning
                }

                for (int j = 0; j < numSampleVector2s; j++)// apply to middle of edge or at 1/3, and 2/3.   this will ensure it stops rotating in a medium like water
                {
                    //Vector2 applicationVector2Local = faceMidVector2;  //midVector2 .. works for some but no torque on simple stick.. will rotate too long in viscous
                    //Vector2 applicationVector2Local = vertices[i1];   
                    // apply both endVector2 of this face  problem here  with this is too often another body like hand or 
                    //new fixture is at this Vector2, and causes blocking.
                    //    if (j == 1)
                    //     applicationVector2Local = vertices[i2];  
                    //    ends opten intersect self.. better do middle of halfs.
                    //best solution  1/4, 3/4 down face..

                    Vector2 applicationVector2Local;

                    if (numSampleVector2s == 1)
                        applicationVector2Local = faceMidVector2;
                    else
                    {
                        //   applicationVector2Local =  j==1 ? vertices[i2] : vertices[i1];//having torque spread out leads to a less numberical errors.
                        //might be gode for single  free panels 

                        //best for swimming and convex hulls like the airship
                        applicationVector2Local = j == 1 ? (faceMidVector2 + vertices[i2]) / 2 : (faceMidVector2 + vertices[i1]) / 2;
                    }

                    //TODO should actually check  separate wind component and falling one.. separately..  consider falling next to wall.
                    //TODO FLUID, the grid should take care of this blocking at least  if something thick is blocking
                    //TODO SOON check on flapping ,  having separate components would be useful.. we dont not want ot to blocking for ground ( falling towards) rocket and other, having this will cause a performance hit but maybe  more important 
                    //and than particles.   static bodies currently cannot block wind , if separate components then they could block the wind one.

                    //CASE  cant flag away from building..unless using dont block on fines
                    //could have info ( separate air vel components)   use it for everything but balloon panels.
                    //now they are expensive.  ( and balloon is not much fun ) 

                    //as is now done for particles..  it not as critical since unless super strong winds or fast falling..  cliffs , drafts?
                    //check blocking for wind field separatly.. if blocked dont add it..         
                    //from wiki
                    // http://en.wikipedia.org/wiki/Dot_product
                    // If only B  is a unit vector, then the dot product a . b   gives  ||a|| cos o , i.e., the magnitude of the projection of a  in the direction of  b , with a minus sign if the direction is opposite. 
                    //This is called the scalar projection of  onto , or scalar component of  in the direction of  (see figure). 
                    //This property of the dot product has several useful applications (for instance, see next section
                    //force acts only if body not already moving in flow
                    Vector2 applicationVector2World = panelRayFromInsideCenter ? body.WorldCenter : body.GetWorldPoint(ref applicationVector2Local);

                    Vector2 windField = Vector2.Zero;
                    float airDensity = WindDrag.DefaultAirDensity;
                    float temp = WindDrag.DefaultTemperature;

                    //TODO  could remove the blocking undo aread thing.. just make it a  Field object that is queryable..?
                    //if ( body.IsInfoFlagged( BodyInfo.DebugThis))// for tracing wind field uncomment, and mark one item with this flag.                            

                    //TODO WATER.. falling fast from air to water.. on a plank.. maby it should do a TOI sort of thing
                    //check on frame ahead,  mabye calc time of impact and react acordingly with force.. ( look at substepping)
                    //NOTE this is when a temp fixture ( edge ) might be used full.
                    //applying edges to the water .. like a rope might help also to determine the wave propagation direction
                    //some kind of custom collision response ( see collisions off) OnContacted.. this would be  needed for surfing on arbitrary waves)
                    //todo or  check  sensor on wave speed.. can skip verts ( effective smoothing) 
                    //ISSUE.. surf board falls fast .. and flat on water or wave edge and it sinks .. should bounce or skip.


                    //TODO refactor and generalize this, need it to get draw from object coupled wiht grid fluid.. since is pushing aside the fluid but this reduces lift

                    if (body.IsInfoFlagged(BodyInfo.AirContainer))
                    {
                        //note using the inner hull Vector2s i believe.
                        float offsetInside = 0.03f;  //this is so that is will be inside the air region in boat, otherwise its right on the edge and testInside is false.  NOTE  TODO waterclipregion is expanded, but do it anywyas, its not perfect for some hulls
                        applicationVector2World += (normalWorld * offsetInside);
                    }

                    windField = GetSummedVelocityField(applicationVector2World, out airDensity, out temp);

                    Vector2 edgeVelocity = body.GetLinearVelocityFromWorldPoint(ref applicationVector2World) - windField;
                    float velInEdgeNormalDirection = Vector2.Dot(edgeVelocity, normalWorld);

                    if (velInEdgeNormalDirection < 0)
                        continue;   // normal Vector2s backwards , it means it is  not a leading edge against the airflow.  This comes from the      //  http://www.iforce2d.net/b2dtut/buoyancy

                    //NOTE.. before  it was relying on self blocking to figure these ,  expensive  TODO see if offset below is used for the blocking.. ditch it if so.

                    //falling is for guy falling with hull of airship..  this currenlty works,  can walk on  falling air ship due to falling drag force..      
                    Vector2 rayStartVector2 = applicationVector2World;


                    if (!panelRayFromInsideCenter)
                    {
                        float offsetOutside = 0.02f;   // this is needed to prevent ray missing hit on self sometimes..  ( NOTE: now that leading edge check is used, dont think this is necessary) 
                        rayStartVector2 += (normalWorld * offsetOutside);
                    }

                    //TODO less drag for bullets..
                    //or default drag simply 
                    //using  50 for density of water..
                    ///realistic on creature for swimming.  close realistic for floating.. most objects lie wood are close to this.

                    //TODO    get the field at the contract.. for large objects  should help, necessary for water.
                    //maybe if level contrains a water field..
                    //start ray a bit outside so it wont hit our edge
                    //umbrella.. airship ok..  check normals..
                    //spear ok  
                    //TODO check case of balloon .. compressed balloon, it  blocks and theres no apparent resistance to faces.           
                    //should we only blocking on  air restance when falling down/ over with hull for now..<-- i think so..  need to block wind in hull also                  
                    //TODO partially done:  ballon on compression wind resistance blocked.. could do pressure force vs aabb shriking..
                    //or.. better create new wind field due to movement  ( compression or generated wind) .. wind field can be altered in front of objects...

                    //TODO make each body have a wind field in moving rel to air, high density .. use to push away clouds.. and put in dynamic tree.
                    //look up by rect on IField..
                    //  flaping big wings will move paper.. wind field will be ref, so wll exist near groudn backwards.. at least density ,, and push outwards maybe..
                    Fixture ignoreFixture = null; //use outer edges only dont ignore anything

                    //for now using vel *2  for blocking check.. avoids Normalize (slow) .. //TODO check , 2 repeated above
                    Vector2 rayVector = edgeVelocity * 2f;
                    String hashCode = body.GetHashCode().ToString() + j.ToString() + i.ToString();//so we can see rays in debug tool



                    //TODO  tried adjusting model in and out of water,
                    //but saving the level was a problem
                    // on, in the boat , isunderwater can return true, can happen untill the bugs are 100% solid.
                    //So , effective Mass, Drag, would be nice..
                    //underwater be nice with  more mass, and less drag for many items
                    //such as swords  (which were made light for walking ease)


                    if ((body.Flags & BodyFlags.InWaterAdjustment) != 0)
                    {
                        dragCoefficient = 0.08f;  ///note ..can apply this when held.. we dont save levels then..problem, is
                        // need to drop it before saving...hmm, 
                        //also could add metadata to certain items in a level, by Name.. TODO PERSISTENCE, TRAVELLERS 
                        //store weapons original Density, etc..   //Or.. Add the DensityOrg... prop.... to bodies.. okay to use hard reference.. put a hash table per level..
                        //control with yrdnd plugin..  add swords to it.. and the props.. then can adjust when held.. need to do this..
                        //things like IntertialDensity that is unaffected by gravity..  for joint strength..
                    }
                    else
                    {

                        //  Debug.Write("weaponat " + body.WorldCenter);

                    }

                    //TODO check falling and wind separately like particle and head
                    float windForceMag = velInEdgeNormalDirection * airDensity * dragCoefficient * (float)Math.Sqrt(edgeLenSq) / numSampleVector2s;

                    Debug.Assert(windForceMag >= 0);  ///velinEdgeNormal , on leading edge is positive, not doing suction  TODO should we?



                    //  http://en.wikipedia.org/wiki/Drag_(physics)
                    float magVel = Math.Abs(velInEdgeNormalDirection);

                    //TODO test/ tune  in fluid.   should be laminar.. except for fast swimming be nice is as turbulent (squared) .  need to adjust to return stroke vs power stroke for swimming , padding.
                    // now we hack it by changing  the coefficeint on power stroke.. otherwise swimming will move back to much on return stroke.
                    if (magVel > 1.0f)  ///add lower vel coeff might be differnet , laminar flow... anyways  for now  just do vel squared if vel below 1.. otherwise will never converge or stop in zero gravi.  0.01 = 0.0001, etc      
                    //TODO check if needed, slowing to slop possible, mass ratios
                    {
                        //TODO FLUIDS
                        windForceMag *= Math.Abs(velInEdgeNormalDirection);   //at high vel , wind turbulence drag is proportional vel squared   there will be a terminal velocity
                    }

                    //TODO check with swimming if 1 is a good value..  also we can tweak drag coefficient on return storke as done in rocketplane                       
                    //    const float maxReactionAccel = 10000f;
                    //testing .. in water density 50..  this model can be unstabe at high vel with vel squared reaction.mass is 134 .. unstable after 2,000,000 force.
                    //see extremely high forces , explodes.
                    //This hasn't proven to work.

                    //     float maxReactionForce = body.Mass * maxReactionAccel
                    //    if ( windForceMag > maxReactionForce)
                    //     {
                    //         windForceMag = maxReactionForce;                
                    //        Debug.WriteLine( "reaction force clamped at" + maxReactionForce.ToString());
                    //     }

                    //TODO for sword in level 8.. a drag coefficient that is less for water,  of 0.2
                    //for now just putting lower drag in the water...
                    //might need to use less water density..

                    //TOOD best to find a good solution to limit these.. as in LimitAirForceField
                    //but that is incomplete .. for now just tuning wil have to do.
                    //other wise it explodes..
                    Vector2 windForce = -normalWorld * windForceMag;

                    bool isUnderWater = airDensity >= WindDragConstants.MinLiquidDensity;

                    if (!SkipCheckBlocking(edgeVelocity, body, windForceMag) && !isUnderWater && IsEffectivelyBlocked(rayStartVector2, rayVector, windBlockAngle, hashCode, ignoreFixture, ignoredList, true, ViewRays))
                        continue;


                    if (velInEdgeNormalDirection > 100)
                    {
                        //  Debug.WriteLine("huge windvel");  //TODO  must limit amount in one frame
                        //investigate,  mabye check smaller dt,  fp errors"
                        //use deugthis.. or simeple water tanks.
                        //push..with mouse..
                    }

#if SPLASHES

                    if (!inAirContainer)
                    {
                        Vector2 posNextFrame = applicationVector2World + normalWorld * velInEdgeNormalDirection * dt;  //probably will exit water in this one frame.. lets make a splash.

                        //TODO if enter water.. add some bubbles.
                        if (isUnderWater)
                        {
                            //since each thread takes a body, lets use the bodies emitter collection, add the effects here.

                            //While not perfect ( getting close the surface is enough).. this is the good way to add a one time disturbance to the water.
                            //Using callback to achieve this using Disturb method in Ocean plugin.
                            //TODO also do vertex edges 
                            //TODO best to put a vel field here.. also in waves.. put a vel field so waves an crumble if suddenly adding up.  or hitting something
                            if (!IsUnderWater(posNextFrame))   //TODO the real volume of water would be a clip region like bouyance.. for now just place some based on edge length
                            {
                                // Vector2 posNextFrameLocal = Vector2.Zero;
                                //  body.GetLocalPoint(ref  posNextFrameLocal); // first frame , particle might not come out, its in water, but thats ok
                                //TODO future.. extend offset also vert. .. check also the verts.   these are actually where the splash would come from, however consider that if just one edge comes out would not be a big splash.. 
                                //best way is to sweep.

                                //TODO might work better using one Vector2 on face.
                                //TODO need to use Offset to spread out the splash.
                                //TODO shoudl add a motion field in the medium.. that will fade out.  then the relative motion will stop for a while.

                                if (!body.IsInfoFlagged(BodyInfo.AirContainer))
                                {
                                    ActivateOrCreateSplashorBubbleEmitter(body, normalWorld, applicationVector2Local, SplashTag + applicationVector2Local.ToString(), false); //TODO  use size for num particles... looks ok on hands and feet now tho.  Now.. too much splash come out of neck
                                }
                                //TODO if one face is vertical .. some in water , some out.. might do this to put a wake on boat..  or do it on relative accel  in ocean.
                                if (OnFaceCrossWater != null)
                                {
                                    OnFaceCrossWater(body, normalWorld, applicationVector2World, velInEdgeNormalDirection, vertices[i1], vertices[i2]);
                                }

                                //TODO bubble sound.
                            }
                        }
                        else
                        {
                            //TODO mabye implement bubbles for hull when moving or near water line, now its not done
                            float floatingDensity = body.SubmersionDensity != 0 ? body.SubmersionDensity : body.Density;

                            if ((body.JointList != null || floatingDensity > WindDragConstants.MinLiquidDensity) &&  // dont put bubbles if its going to  float and has no joints.  TODO if convex maybe  put some bubbles.
                                IsUnderWater(posNextFrame) && !body.IsInfoFlagged(BodyInfo.AirContainer))   //TODO the real volume of water would be a clip region like buoyancy.. for now just place some based on edge length
                            {
                                // Vector2 posNextFrameLocal = Vector2.Zero;
                                //  body.GetLocalPoint(ref  posNextFrameLocal); // first frame , particle might not come out, its in water, but thats ok
                                //TODO future.. extend offset also vert. .. check also the verts.   these are actually where the splash would come from, however consider that if just one edge comes out would not be a big splash.. 
                                //best way is to sweep.
                                ActivateOrCreateSplashorBubbleEmitter(body, normalWorld, applicationVector2Local, SplashTag + applicationVector2Local.ToString(), true); //TODO  use size for num particles... looks ok on hands and feet now tho.  Now.. too much splash come out of neck

                                //TODO if one face is vertical .. some in water some out.. might do this to put a wake on boat..  or do it on relative accel  in ocean.
                                if (OnFaceCrossWater != null)
                                {
                                    OnFaceCrossWater(body, normalWorld, applicationVector2World, velInEdgeNormalDirection, vertices[i1], vertices[i2]);
                                }

                            }

                        }
                    }
#endif
                    //Vector2 limitedForce = body.LimitAirForceField(ref windForce, dt, -edgeVelocity);   //seems to limit way too much
                    //TODO may need to gradually slow items all the way to to zero in thick medium.. 

                    //TODO make sure impulse doesn't go backward and explode.. easy to check pushing board in water.

                    //so sleep code will need be careful when to put stuff to sleep..
                    //but for now ground will stop it.  so applying  min accel to set things sleep, and to avoid waking stuff up is ok.
                    //todo tune this soon check wind turning up can wake stuff..          
                    //  body.ApplyForce(ref limitedForce, ref applicationVector2);//TODO spear seems to drift right..never stops..  
                    body.ApplyForce(ref windForce, ref applicationVector2World);//TODO spear seems to drift right..never stops..   check , could it be because ray is drawn from uI thread? 

                    //  Debug.WriteLine("windforce" + windForce.ToString());  

                    if (temp > 800 && body.JointList != null)  //TODO not used in game yet.. no burninh.  FUTURE
                    {
                        if (body.JointList.Joint != null)  //melt one joint.
                            body.JointList.Joint.Break();
                    }
                    //  Debug.WriteLine("Air Drag Wind force " +  windForce);
                    //  Debug.WriteLine("Edge Velocity" + edgeVelocity);

                    if (ViewRays && sensor != null && ViewForceRays)
                    {
                        sensor.AddRayNoCast(applicationVector2World, applicationVector2World + windForce * forceScale * 0.1f, "windforce" + hashCode, forceColor);
                    }

                    if (panelRayFromInsideCenter)
                        return;
                    //NOTE  TODO FIX :THIS DIRECTION AS SHOWN BY RAY IN  TOOL APPEARS OFF BY 1 FRAME DURING HIGH VELOCITY
                    //LOOK AT THE NORMAL VECTOR INS WORLD , ITS DIFFERENT THAN WHAT THE RAYS SHOWS
                    //see emitter view position off by one frame as well.    follow falling spear with camera in tool to see this
                    //coud be because phsics thread vs ui thread?
                    //     body.ApplyForce(-edgeVelocity, body.GetWorldPoint(vertices[i1]));  //TODO this works, brings it to stop no gravity
                }
            }

#if PRODUCTION
            if (numSignificantFaces == 0 )
            {
                Debug.WriteLine("no significant drag faces, setting ApplyDragAsParticle true for this " + body.PartType.ToString());  ///TODO this might be  a bad idea.   code logic changing the model  for now dont do it in the tool, we might be drawing something.
                body.ApplyDragAsParticle = true;  // next time //TODO make sure drag is comparable.. it dont think it is..       
            }
#else
            if (numSignificantFaces == 0 && !MapNoSignificantFaces.ContainsKey(body))  //only say it once
            {
                if (!body.IsInfoFlagged(BodyInfo.ClipDressToGeom))  ///means maybe was cut... TODO add a separate flag for was cut
                {
                    Debug.WriteLine("no significant drag faces, consider ApplyDragAsParticle as true for wind effect" + body.PartType + " " + body.Info + " " + body.WorldCenter);

                    if (body.Color != BodyColor.White)  //default color, maybe its being shaped..
                    {
                        MapNoSignificantFaces.Add(body, true);  // only warn once
                    }

                }
                else
                {
                    body.ApplyDragAsParticle = true;
                }
            }
#endif




        }

        //   private static void AdjustDensityAndDragForLongThinWeapons(Body body, bool inWater)
        private static void SetInWaterAdjustmentFlag(Body body, bool inWater)
        {
            if (body.IsWeapon || body.PartType == PartType.Rope) //TODO HACK WATER REMOVE TODO CLEANUP..
            {
                if (inWater)
                {
                    body.Flags |= BodyFlags.InWaterAdjustment;  //TODO.. template or helper.. something to set / upset flag.. its easy to make a typo
                }
                else
                {
                    body.Flags &= ~BodyFlags.InWaterAdjustment;
                }
            }
        }

        public static void SetBubbleEmitterProperties(BodyEmitter bem, float sizeFactor)
        {
            //start with these
            Spirit.SetBloodEmitterProperties(bem, new BodyColor(255, 255, 255, 255)); //TODO move to skin in case striking bullet, of offset around it..          
            Spirit.ApplySizeFactor(bem, sizeFactor);
            bem.Size = 0.022f * 1.4f;
            bem.DeviationSize = 0.03f;

            bem.Info |= BodyInfo.Bubble;
            bem.IsNotCollideable = false;
            bem.ProbabilityCollidable = 1f;  //bubbles usually need to bounce off arms..   TODO should it be bullet?  slow but they tunnel.
            bem.Density = 10f;  // light
            bem.AutoDeactivateAfterTime = 5;
            bem.CheckRayOnEmit = false;
            bem.EmissionForce = 3f;
            bem.LifeSpan = 10000;
            bem.DragCoefficient = 0.4f;
            bem.Friction = 0;
            bem.MagnitudeAspectOscillation = 0.4f;
            bem.OscillationPeriod = 350; //msec
            bem.OscillationPeriodDeviation = 0.7f;
            bem.RandomForceMax = new Vector2(0.5f, 0);
            bem.Frequency = 6f;//half as many bubbles for now
        }

        //TODO could double check on feet touching hull or rays to hull..  flag them..  sometimes they splash .
        //however the buffer expandsion should prevent it..

        //used by simple bodies treated as particles like hands.. with no faces.
        private static void DoSplashOrPushedBubble(Body body, float dt)
        {
            bool isUnderWater = IsCenterUnderwater(body);

            //TOOD if enter water.. add some bubbles.
            if (isUnderWater)
            {
                Vector2 posNextFrame = body.WorldCenter + body.LinearVelocity * dt;  //probably will exit water in this one frame.. lets make a splash.
                //since each thread takes a body, lets use the bodies emitter collection, add the effects here.
                //TODO best to put a vel field here.. also in waves.. put a vel field so waves an crumble if suddenly adding up.  or hitting something
                if (!IsUnderWater(posNextFrame))   //TODO the real volume of water would be a clip region like bouyance.. for now just place some based on edge length
                {
                    Vector2 direction = body.LinearVelocity;
                    direction.Normalize();
                    ActivateOrCreateSplashorBubbleEmitter(body, direction, body.LocalCenter, SplashTag + body.LocalCenter.ToString(), false);  //TODO  use size for num particles... looks ok on hands and feet now tho as is but this is a shared function
                }
            }
            else
            {
                //TODO add bubbles n the opposet case..     hands and feet will often hit water first.
            }
        }

        public static BodyEmitter ActivateOrCreateSplashorBubbleEmitter(Body body, Vector2 normalWorld, Vector2 emitPtLocal, string splashTag, bool bubble)
        {

            Emitter emitter = body.EmitterPoints.FirstOrDefault<Emitter>(x => x.Name == splashTag);//insure a unique key if splashing by passing in emitPtLocal.ToString

            BodyEmitter splashEmitter;
            if (emitter == null)
            {
                splashEmitter = CreateSplashorBubbleEmitter(body, ref emitPtLocal, splashTag, bubble);
            }
            else
            {
                splashEmitter = emitter as BodyEmitter;
                //  splashEmitter.DeviationOffsetX               //TODO deviate along edge.  add several emitters. or deviate along line like angle.  ( need this for water filll sink effect also)                                
                //    splashEmitter.DeviationOffsetX  = MathUtils.
                //    splashEmitter.DeviationOffsetY    
            }

            splashEmitter.SlowFrameRateReductionFactor = 0.7f;
            splashEmitter.EmissionDirection = normalWorld;
            splashEmitter.Active = true;
            return splashEmitter;

        }

        private static BodyEmitter CreateSplashorBubbleEmitter(Body body, ref Vector2 applicationVector2Local, string splashTag, bool bubble)
        {
            BodyEmitter splashEmitter;
            splashEmitter = new BodyEmitter(body, applicationVector2Local, SimWorld.Instance.Physics);

            if (bubble)
            {
                SetBubbleEmitterProperties(splashEmitter, 1);
                splashEmitter.AutoDeactivateAfterTime = 0.3f;
            }
            else
            {
                SetSplashEmitterProperties(splashEmitter);
            }

            splashEmitter.Name = splashTag; // recycle it for this body.              
            body.EmitterPoints.Add(splashEmitter);
            return splashEmitter;
        }


        //TODO move this to spirit .. or body.  everything will emit bubbles..
        static public void SetSplashEmitterProperties(BodyEmitter bem)
        {
            //start with blood , the blood of the sea
            //NOTE  .. partcles props here are different depending what body part they come from.. guess thats ok.. more variation
            //TODO should might want to break out the props , not use the blood ones.
            Spirit.SetBloodEmitterProperties(bem, WaterColor); //TODO move to skin in case striking bullet, of offset around it..   
            bem.Density = 152; //litte more  dense than water   ..  TODO.. apply forces in currents..   
            bem.DragCoefficient = 2f;  // carried by wind fairly easily since not dense..  ( particle force will be clipped   if accel greated than wind speed         
            //bodyEmitter.Mass   since mass is proportionall to r * r.. big particles will affected by wind more anyways , can use density..
            bem.Friction = 0.03f;
            bem.Frequency = 12;
            bem.DeviationAngle = 0.5f;
            bem.ParticleCountPerEmission = 3;
            bem.EmissionForce = 2.5f;  // so that it wont tunnel on first emit.   shoud not need much its moving with face
            bem.CheckInsideOnCollision = true;  //in case it tunnels in somehow..


            bem.SkipRayCollisionCheck = false;   //non collidable blood will stick on collide
            bem.MagnitudeAspectOscillation = 0.6f;
            bem.OscillationPeriod = 120; //msec
            bem.OscillationPeriodDeviation = 0.5f;
            bem.Color = WaterColor;
            //  bodyEmitter.SlowFrameRateReductionFactor = 0.5f;  //half blood when fps low,  below 36..
            bem.EdgeStrokeThickness = 0.0f;
            bem.CheckRayOnEmit = true;   // don't emit if right on something, will probably tunnel.
            bem.ZIndex = -999;  // make blood not appear to interpenetrate 
            bem.Info = BodyInfo.Liquid;

            //TODO.. not sure want to use blood as a start .. its different for every body part.=
            //   bem.Info |= BodyInfo.Bubble;   //future , for trapped air the other way around..
            bem.IsNotCollideable = true;
            bem.ProbabilityCollidable = 0;
            // bem.ProbabilityCollidable = 1f;  //bubbles usually need to bounce off arms
            bem.EmissionForce = 2f;

            bem.CheckInsideOnCollision = false;
            bem.AutoDeactivateAfterTime = 0.3f;
            bem.CheckRayOnEmit = false;
            bem.DeviationAngle = 1f;
            bem.LifeSpan = 3000;

            bem.SizeDivideByZoom = true;
            bem.Size = 0.022f;
            bem.DeviationSize = 0.02f;

            bem.SizeScaleMin = 1f;//
            //   bem.SizeScaleMax = 4f;//
            bem.ZoomLevelForNormalScale = 146;

        }

        /// <summary>
        ///  Determines if we should use one sample pt for this face on a body
        /// if both edge are not in a liquid , its ok to use on edge.  otherwise flapping swimming, etc looks unrealistic, fluids need to have greater effect.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="vert1">edge vert1 in body local</param>
        /// <param name="vert2">edge vert2 adjacent in body local</param>
        /// <returns></returns>
        private static bool UseOnePtOnEdge(Body body, Vector2 vert1, Vector2 vert2)
        {
            if (body.JointList != null && (body.Info & BodyInfo.UseSingleDragEdgePanel) != 0)
            {
                if (HasWater())
                {
                    if (GetDensity(body, vert1) > WindDragConstants.MinLiquidDensity)
                    { //TODO maybe cache this .. might be expensive calc.. check with wave..
                        return false;
                    }
                    else if (GetDensity(body, vert2) > WindDragConstants.MinLiquidDensity)
                    {
                        return false;
                    }
                }

                return true;
            }
            else
                return false;
        }


        /// <summary>
        /// returns true if any part of AABB is underwater, in a body of water.   supports multiple oceans in a level, 
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        /// </summary>
        /// <param name="body"></param>
        /// <param name="fluidSpirit">returns ILiquidRegion for this body of water its intersecting. </param>
        /// <returns></returns>
        public static bool IsFatAABBUnderWater(Body body, out ILiquidRegion fluidSpirit)
        {
            body.UpdateAABB();   ///TODO FUTURE for collidable can be get this from the  spatial tree faster?  has proxies there.  NOTE yes but they get slale and 

            AABB aabb = body.AABB;
            aabb.Expand(1.4f, 1.4f);  //in case flying up fast off a wave.. .TODO fatten according to body.LinearVel Y..  seems to fix the  issue with leaving vertical colums in the water
            IField water = WindFields.FirstOrDefault(x => x is ILiquidRegion && (x as ILiquidRegion).AABBIntersect(aabb));
            fluidSpirit = water as ILiquidRegion;
            return water != null;
        }


        public static bool IsUnderWater(Vector2 vertex)
        {
            return GetDensity(vertex) > WindDragConstants.MinLiquidDensity;
        }

        public static bool HasWater()
        {
            return WindFields.Any(x => x is ILiquidRegion);
        }

        public static bool IsCenterUnderwater(Body body)
        {
            return IsUnderwater(body.WorldCenter);
        }

        public static bool IsUnderwater(Vector2 pt)
        {
            return GetDensity(pt) >= WindDragConstants.MinLiquidDensity;
        }

        public static float GetDensity(Body body, Vector2 vert1)
        {
            Vector2 vert = body.GetWorldPoint(vert1);
            return GetDensity(vert);
        }

        private static float GetDensity(Vector2 vert1)
        {
            float fluidDensity;
            float temperature;
            GetSummedVelocityField(vert1, out fluidDensity, out temperature);
            return fluidDensity;
        }

        public static float GetDensityAtCenter(Body body)
        {
            return GetDensity(body.WorldCenter);
        }


        private static void ApplyParticleDrag(Body body, float windBlockAngle, float dt, Sensor sensor)
        {
            Vector2 windField = Vector2.Zero;
            float airDensity = WindDrag.DefaultAirDensity;
            float temp = WindDrag.DefaultTemperature;

            //TODO  could remove the blocking undo aread thing.. just make it a  Field object that is queryable..?
            //     if ( body.IsInfoFlagged( BodyInfo.DebugThis))// for tracing wind field uncomment, and mark one item with this flag.                            

//todo mark some particle as euler only.. other do this
            windField = GetSummedVelocityField(body.WorldCenter, out airDensity, out temp);

            if (!windField.IsValid())
                return;


#if ONEFIELDATTIMEEXP  //this breaks bubllbe adn dust and too much stuff
            //here we dont sum fields  ..sparks in balloon can swirl w wind if not broken... the density in the ballloon can be really low so not to 
            //ruin the exsting effect on panels an the bouyancy implemented already..
            //ideally the pressure solver would handle the ballloon forces tho..
            foreach (IField field in WindFields)
            {

                float density;
                Vector2 windvel = field.GetVelocityField(body.WorldCenter, out density, out temp);

                if (!windvel.IsValid())
                    continue;

                ApplyOneFieldDampingAsParticle(body, windBlockAngle, dt, sensor, windField, airDensity, temp, field);

            }


        }

        private static void ApplyOneFieldDampingAsParticle(Body body, float windBlockAngle, float dt, Sensor sensor, Vector2 windField, float airDensity, float temp, IField ifield)
        {

#endif

            //TODO get the particle that is the cause of this field if its a field particle.
            //TODO dont block it..            
            //TODO  could remove the blocking undo aread thing.. just make it a  Field object that is queryable..?


            float faceSize = 0;
            if (body is Particle) //assuming circle for now.  
            {
                //TODO  just kill the particles  if its snow or rain failing in sea.
                if (airDensity >= WindDragConstants.MinLiquidDensity)  //TODO check., shouldend it be killed when reenter the water? <=.

                //TODO redo this with a sort of particle clip  or collider ray or smeohing else...color is a hack..
                {
                    if (body.Color.Equals(WindDrag.WaterColor)) //NOTE..  for some reason reference equals== does not work, maybe because one is static.. its the same object.  
                    {
                        (body as Particle).LifeSpan = 0;    //splash back in water.    //TODO check if leaks work i dont think so..   
                    }
                    //TODO floating , swirling blood..  if bitten by shark under water.. if snow melt.. ..  handle bubbles up?
                    //    (body as Particle).LifeSpan = 300;  // TODO future.. set lifespan to varius value depend if water, snow, dust, etc , so can have "foam"  ..  dust can float.. ? sand will fall, blood will hover..
                }
                else
                {
                    if (body.IsInfoFlagged(BodyInfo.Bubble))
                    {
                        (body as Particle).LifeSpan = 400;  //slight foam on surface.. TODO should  mabye pop
                        (body as Particle).ResetDynamics();   //will stop the bubble
                    }
                }
                faceSize = (body as Particle).ParticleSize;
            }// close enough to radius for now. if IsCollidabe is off, there is no Fixture in play..  and radius is not stored on Partilce  either             
            else
            {
                if (body.AABB.Height == 0)
                {
                    body.UpdateAABB();
                }
                faceSize = (body.AABB.Height + body.AABB.Width) / 2;  //TODO use the one most perpendicular to the wind dir .. for now this is just used  for clouds and rocks and stuf
            }

            ApplyAirFlowDampingParticle(faceSize, body, airDensity, windField, temp, windBlockAngle, dt, sensor);
        }

        public static Body PlatformBody;   // standing on a moving body, treat as a moving reference frame.. used for blood collision.

        public static bool CheckParticleCollision(Particle particle, float dt)
        {
            //TODO seems particle collide too early and disappear on fighting. if accel is not there.. adjust this.. was adjustment for rain falling through
            //umbrella

            float rayExtension = 1.4f;  //make x percent longerto avoid tunnelling  however there is a performance impact.  also , particle disappears too early

            //todo consider  clip each particle  to rope for standing on rope?
            Vector2 refFrameVel = Vector2.Zero;

            if (PlatformBody != null && !PlatformBody.IsStatic)
            {
                refFrameVel = PlatformBody.LinearVelocity;
                rayExtension = 2;
            }

            float temp, density;
            if (particle.Mass < 0.03 && WindDrag.GetSummedVelocityField(particle.WorldCenter, out density, out temp).LengthSquared() > 3f) //  prevent snow in strong wind going through ground due to accel.  TODO figure this out properly, ignore source body ( ref on particle) , remove on inside.
            {
                rayExtension = 2.5f;
            }

            //TODO fix rays originating inside something .. call it blocked..  ( or ignore our body)
            Vector2 acceleration = Vector2.Zero;

            if (!particle.IgnoreGravity && particle.Age > _dt * 1000)  //    the Age check is not to extend on particle emit.  only after particle has been around.             fix from  balloo n w/ fire accel up..  particle.Parent.Parent ( find emitter accel?) 
            {
                acceleration = particle.Acceleration;  //this is measured..
            }

            Vector2 displacementPerFrame = (acceleration + particle.LinearVelocity - refFrameVel) * dt * rayExtension;
            Vector2 applicationVector2 = particle.WorldCenter;

            // dont do ray if length == zero, cause tree exception
            const float minDistPerFrame = 0.005f;//

            if (displacementPerFrame.LengthSquared() < minDistPerFrame * minDistPerFrame)
                return true;  // remove it if its this slow..

            //TODO expand for in ship
            //dont check collision on particles well  outside our viewport

            //TO fix dust originating  inside of ship, in wind, with guy inside.. check blocking before emitting is al..
            //if emitters is outside of viewport  ( not gun spark, etc)


            //dont allow tunnelling when zoomed in
            //  if (!_currentViewport.Contains(ref applicationVector2))  // TODO put a IsOutofViewport  .. if true dont check again inview.   or its it worth it  // two bool or two floating Vector2 >
            //     return false;


            // examle emitter  on cloud.. uses ray to determine when partilce should start casting this ray or be collidable.
            Vector2 rayEnd1 = applicationVector2 + displacementPerFrame;

            //TODO this is repeated in ray cast check in BodyEmitter.. not too big a deal
            Vector2 normalAtCollision = Vector2.Zero;
            Body struckBody = null;
            bool hitSomething = false;
            float rayFraction = 0;
            Vector2 intersection = Vector2.Zero;
            //TODO ray cast on diagonal to axis are super slow.. we should limit  it..
            // RaycastOneHit:  TODO might break this into a function..
            //TODO this ignores shapes that contains the starting Vector2.. see if  we can modify it to not do that
            //particles get insie stuff and stay there.  rather not do another hit test.
            // a graphics backing pixel test would also be nice if we could access backing store..


            try
            {
                World.Instance.RayCast((fixture, vector, normal, fraction) =>
                {
#if !PRODUCTION
                    if (fixture.Body.IsNotCollideable) //needed in tool since IsNotCollideable items like clouds  are in tree so they can be selected, and will be hit..  
                        return -1;  //skip and continue
#endif
                    // ignore collision with another particle
                    if (fixture.Body is Particle)
                        return -1;

                    // ignore collision with emitter body which this particle comes from 
                    if (fixture.Body == particle.ParentBody)
                        return -1;

                    if (fixture.Body.IsInfoFlagged(BodyInfo.Bullet) && !MathUtils.IsOneIn(10))  //usually don't stick to bullet,  otherwise it will be blocking all the blood, when stuck in skin and emitter is placed near it.
                        return -1;

                    //for dust.. sometimes the ground has multiple layers so above check wont work..
                    //TODO mark ground as ground or add special property or updraft dust..
                    //TODO Check if performance issue on methods.. algorithm .. update views.. etc.
                    // seems  blood lasts too long on dead body parts..  compare with old build?
                    // show should last  .5 longer if we can afford it 

                    //TODO check relative vel of sticken body..
                    struckBody = fixture.Body;
                    hitSomething = true;
                    intersection = vector;
                    normalAtCollision = normal;
                    rayFraction = fraction;
                    return fraction;

                }, applicationVector2, rayEnd1, true);//true mean check if ray starts inside


            }
            catch (Exception exc)
            {

              //  Debug.WriteLine("exc in particle ray collide test" + exc);// suspected threading bashing inputs so they len is zero
                return false;
            }

            if (!hitSomething)
                return false;

            //TODOD CLEAN get rid of this SHIT.. groudn effect winds.  this is onlyh so shit can fly  up off ground..walking righth what a HACK

            if (Level.Instance.LevelNumber == 1)  //TODO fix this properly, dust fields and eddies on ground, windws going up, etc.
            {

                bool startedFromInside = !particle.CheckInsideOnCollision && rayFraction == 0;

                // filter out static bodies for particle that start from inside of bodies.
                //Issue i think this was commented out because of dust entering spaceship when zoomed in 
                //Well it  broke level1 right side.
                // do this after particle mark is handled.
                if (startedFromInside)
                {
                    hitSomething = false;  //this is to allow dust emitter from outside viewport  to enter scene..
                    //       works  ok unless snow.. dust should be swept along surface.. was meant for level  1
                }
            }

            particle.LifeSpan = (rayFraction * dt * 1000) * 2.0f;   //might live another frame.. dont set to right zero

            if (hitSomething)
            {
                //TODO add a litte impulse 
                CheckToAddParticleStrikeMark(particle, normalAtCollision, struckBody, intersection, rayFraction);
            }

            return hitSomething;
        }

        //   private static void AfterParticleStrike(Particle particle, Body struckBody)
        //    {
        //TODO give it a chance to extend its life?
        // Vector2 relvel = struckBody.LinearVelocity - particle.LinearVelocity;
        //tested with 2.0 factor. ..rain seems to get close but not pass through the blockage.

        //TODO age is calc on UI thread..  should be moved to controller someday.

        /*
        Vector2 relvel = struckBody.LinearVelocity-body.LinearVelocity;
        //TOD apply force to struck body? pelting? 

        float minAfterStruckVel = relvel.Length();
        minAfterStruckVel *= 0.8f;

        //TODO bounc it using dot product.. can't just add normal
           
  //body.ApplyLinearImpulse( normalAtCollision * relvel.Length() *dt);   
        //LinearVelocity is affected just after ApplyLinearImpulse..
//apply action and reaction           //TODO use mass ratio..
       body.LinearVelocity += normalAtCollision * relvel.Length();
     
        //so if hit somethign directly just kill the particle 
        if (body.LinearVelocity.LengthSquared() < minAfterStruckVel * minAfterStruckVel)
        {
            body.LifeSpan = 0;
        }
        else
        {
            body.LifeSpan = 2000;
        }
*/
        //    }

        public static void CheckToAddParticleStrikeMark(Particle particle, Vector2 normalAtCollision, Body struckBody, Vector2 intersection, float rayFraction)
        {

            if (particle.IsInfoFlagged(BodyInfo.Bubble))
                return;


            // stick particle to struck body. check Vector2 where raycast blocked, place marks on that position.
            if (particle.ElectroStatic == 1f &&     //this should prevent flame sticking to balloon.   Info/ spark..
                (struckBody.PartType & PartType.Hand) == 0 && //don't stick to hand since hand body is much bigger than fist .
                (struckBody.PartType & PartType.Foot) == 0 && //don't stick to foot since top of  body is too high now.. changing foot shape now  will affect tuning..
                                                              //(struckBody.PartType & PartType.Eye) == 0 &&  cant  to check eyes they are non collidable when inside head
                (struckBody.PartType & PartType.Toe) == 0 &&
                (struckBody.Info & BodyInfo.Cloud) == 0

                && rayFraction != 0 // means collision from inside ( ray len = 0) viewport boundary generted dust started from inside , dont  put a mark
                )
            {

                // normalAtCollision might work to allign  ellipse.. one concern is the lineup of the dress to the edge.
                // if its easy we could try.   later we can make body edge closer to dress anyways.
                //todo offset this by the amout of the Stroke thickness  if struck body, offset it by strok.. check level 2 snow.. blood should  be on edige of stroke.
                // in level 4 , an other stroke is used thick to make feater appear to "touch " ground.. there is now a space. ( farseer issue , cant be adjusted) 
                //TODO remove this used of stroke and expand the views  .   its expensive to render especially on phones.  and stupid, must be done everywhere for a general problem.
                const float minDistanceToJointAnchor = 0.02f;
                float minDistanceToJointAnchorSq = minDistanceToJointAnchor * minDistanceToJointAnchor;

                //TODO  FUTURE or if bug with stuff not sticking  near this  hinge parts in general will have a tranparent area, but not all doors...  no major hurt if fail to mark near noor
                if (struckBody.PartType == PartType.Hinge || struckBody.PartType == PartType.Door) //for bleeding on Airship .. on level 5
                {
                    minDistanceToJointAnchorSq = 0.1f * 0.1f;// measured dist to joint from hidge outer edge..  ( on level 5) TODO hack here.
                }

                if (struckBody.Color.A == 255    //if fully filled , not transparent don't need to check.
                                                 //    || ( struckBody.Info & BodyInfo.PlayerCharacter) == 0 //TODO even if  spirit is sick its not fully opaque..  for now ..glow and sick are not fully transparent.. ..theres no way to check these charcters which use a dress and and fill.  for now assum fill is never fully opaque
                     || null == Spirit.GetFirstJointWithinDistance(struckBody, ref intersection, minDistanceToJointAnchorSq, true, BodyInfo.Bullet)
                    )
                {

                    if (particle.IsInfoFlagged(BodyInfo.Fire) && (struckBody.VisibleMarks.Count > 50))// can get slow burning up stuff.              
                        return;

                    MarkPoint Vector2 = struckBody.AttachParticleAsVisualMark(particle, intersection, normalAtCollision);


                    Vector2.UseType = ((particle.Info & BodyInfo.Liquid) != 0) ? MarkPointType.Liquid : MarkPointType.General;  // liquid is flattened circle, general is circle.

                    if (particle.IsInfoFlagged(BodyInfo.Fire))
                    {
                        Vector2.UseType = MarkPointType.Liquid | MarkPointType.Burn;
                    }


                    if ((particle.Info & BodyInfo.Liquid) != 0)
                    {
                        Vector2 relVel = particle.LinearVelocity - struckBody.GetLinearVelocityFromWorldPoint(ref intersection);
                        struckBody.ApplyLinearImpulse(relVel * particle.Mass * World.DT, intersection);
                    }

                }
            }
        }

        /// <summary>
        /// Considered blocked from wind or  drag in ambient air ( falling behind something)  if both rays cast at angle in direction against  wind or  against objects motion, ray lenght proportional to relative vel
        /// </summary>
        /// <param name="pos">point at which to originate rays,l usuall  just in front of a face</param>
        /// <param name="rayVector">how long and in which direction to cast</param>
        /// <param name="fieldToIgnore">If we are putting a wind field ignore our own</param>
        /// <returns></returns>

        static public bool IsEffectivelyBlocked(Body originBody, Vector2 pos, Vector2 rayVector, IField fieldToIgnore)
        {

            List<Body> ignoredList = new List<Body>();
            ignoredList.Add(originBody);

            return IsEffectivelyBlocked(pos, rayVector, WindDrag.RayAngle / 2, pos.ToString(), null, null, false, ViewRays);

        }


        /// <summary>
        ///   both rays at angle are blocked.  only one is cast if its is clear , or if angle is zero.
        /// </summary>
        /// <param name="applicationVector2"></param>
        /// <param name="rayVector"></param>
        /// <param name="angle"></param>
        /// <param name="name"></param>
        /// <param name="fixture"></param>
        /// <param name="ignoredBodies"></param>
        /// <param name="ignoreGround">ignore static body with parttype none</param>
        /// <param name="mapViewRays"></param>
        /// <returns></returns>
        static public bool IsEffectivelyBlocked(Vector2 applicationVector2, Vector2 rayVector, float angle, string name, Fixture fixture, IEnumerable<Body> ignoredBodies, bool ignoreGround, bool mapViewRays)
        {
            //TODO consider if stiking same body.. or if 3rd ray between  then keep going
            bool blocked = IsBlocked(applicationVector2, rayVector, angle / 2, name, fixture, ignoredBodies, ignoreGround, mapViewRays);
            return (blocked && (angle == 0 || IsBlocked(applicationVector2, rayVector, -angle / 2, name + "2", fixture, ignoredBodies, ignoreGround, mapViewRays))); // both rays are blocked                        
        }

        //TODO without graphics ray.. for physic thread . test rain..
        //TODO tune general wind for all.

        //TODO return intersection len.. or fraction to see how it its blocked close.. 
        static bool IsBlocked(Vector2 applicationVector2, Vector2 rayVector, float angle, string name, Fixture fixture, IEnumerable<Body> ignoredBodies, bool ignoreGround, bool mapViewRays)
        {
            Vector2 detectionRay;
            if (angle != 0)
            {
                Mat22 rotationMatrix = new Mat22(angle);
                //TODO limit this leng , ray has performance uses AABB..
                detectionRay = MathUtils.Multiply(ref rotationMatrix, rayVector);
            }
            else
            {
                detectionRay = rayVector;
            }

            Vector2 rayEnd1 = applicationVector2 + detectionRay;
            // dont do ray if length == zero, cause tree exception
            if ((applicationVector2 - rayEnd1) == Vector2.Zero)
                return false;

            List<Body> ignoredList = new List<Body>();

            if (ignoredBodies != null)
            {
                ignoredList.AddRange(ignoredBodies);
            }

            if (fixture != null)
            {
                ignoredList.Add(fixture.Body);
            }

            //TODO faster or custom .. ray reuse the Rayinfo.     two per thread, use array.    is slow and use garbage collection
            //TODO important.   Dont allocate here except in debug.
            RayInfo ray1 = Sensor.Instance.AddRay(applicationVector2, rayEnd1, "winddampblock" + name + angle.ToString(), ignoredList, new BodyColor(0, 255, 0, 255), mapViewRays, false);
            //ignore ground on this.
            //TODO max ray length.. 
            //draw intersect?
            //skip parallel  items  do only one.. dont offset.. and ingore self.. for  all bones and balloon panels.. duh.
            // dont need to 
            //do single rope in strong wind..
            // make cyclones.
            // fix that raise arms.
            return (ray1.IsIntersect && (!ignoreGround || !IsHittingGround(ray1)));

        }

        private static bool IsHittingGround(RayInfo ray1)
        {
            return (ray1.IntersectedFixture.Body.BodyType == BodyType.Static
                && !ray1.IntersectedFixture.Body.IsInfoFlagged(BodyInfo.Building)
                && ray1.IntersectedFixture.Body.PartType == PartType.None);  //NOTE normal terrain  ground is always left as PartType.None since not part of a system.
        }

#region IField Members


        //TODO future WATER.. add out IField or IFluidRegion so that force can react on fluid.
        //even air might do this.   since windws are added, not that important, but for IFluidRegion is important to return which object is to be reacted
        //if we support multiple Fluid regions in on level.   ( usefull or puddles , tide pools or resampling) 

        /// <summary>
        /// Field at postion in meters / per sec, giving density and temp of  fluid at that spot
        /// convert to mph or other by  applying velResult / MathHelper.KmhToMs * MathHelper.KphToMph
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="density"></param>
        /// <param name="temperature"></param>
        /// <returns></returns>
        public static Vector2 GetSummedVelocityField(Vector2 pos, out float density, out float temperature)
        {
            return _windFields.GetVelocityField(pos, out density, out temperature);
        }

        public AABB GetSourceAABB()
        {
            throw new NotImplementedException(); //win windbock region
        }

#endregion
    }

}
