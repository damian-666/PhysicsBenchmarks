using System;
using System.Collections.Generic;

using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Common;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Contacts;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Core.Data.Entity
{
    public enum RayType
    {
        eRayDefault = 0,
        eRayLaser
    }

    /// <summary>
    /// Info class for Ray Intersection Sensor
    /// </summary>
    public class RayInfo
    {
        public RayInfo()
        {
            RayType = RayType.eRayDefault;
            RayThickness = 1;
            RayColor = new BodyColor(255, 0, 0, 255);
        }

        public RayType RayType { get; set; }

        public BodyColor RayColor { get; set; }

        public float RayThickness { get; set; }

        public Fixture IntersectedFixture  { get; set; }

        private Vector2 _start = new Vector2();
        public Vector2 Start
        {
            get { return _start; }
            set { _start = value; }
        }

        private Vector2 _end = new Vector2();
        public Vector2 End
        {
            get { return _end; }
            set { _end = value; }
        }

        private Vector2 _intersection = new Vector2(float.MinValue, float.MinValue);
        
        /// <summary>
        /// Intersection of ray to object in world coodinates
        /// </summary>
        public Vector2 Intersection
        {
            get { return _intersection; }
            set { _intersection = value; }
        }

        private Vector2 _normal = new Vector2(float.MinValue, float.MinValue);
        public Vector2 Normal
        {
            get { return _normal; }
            set { _normal = value; }
        }

        private bool _isIntersect = false;
        public bool IsIntersect
        {
            get { return _isIntersect; }
            set { _isIntersect = value; }
        }
        //TODO see why this is herer this does not need to be cached.. used on ray cast..
        private FarseerPhysics.Common.HashSet<Body> _ignoredBodies = new FarseerPhysics.Common.HashSet<Body>();
        public FarseerPhysics.Common.HashSet<Body> IgnoredBodies
        {
            get { return _ignoredBodies; }
            set { _ignoredBodies = value; }
        }

        private bool _isDirty = false;
        internal bool IsDirty
        {
            get { return _isDirty; }
            set { _isDirty = value; }
        }

    
    }


    //TODO name this Senor to RaySensor or something 
    /// <summary>
    /// Interactive Sensor for use in Script, managed Rays and ray views , used by wind and plugins.
    /// </summary>
    public class Sensor
    {
        #region MemVars

        public static event Action<RayInfo> OnRayCreated = null;
        public static event Action<RayInfo> OnRayDestroyed = null;

        public  static Sensor Instance {get; private set; }

        public World World { get; set; }

        /*  this was never used, comment out
        private List<Body> _ignoredBodies = new List<Body>();
        
        /// <summary>
        /// Global Ignored Bodies - Works across spirit
        /// </summary>
        public List<Body> IgnoredBodies
        {
            get { return _ignoredBodies; }
            set { _ignoredBodies = value; }
        }*/

        public bool IsClearIgnoredBodiesPerFrame     {  get;   set;  }

        private List<Vector2> intSecPoints = new List<Vector2>();

        private ConcurrentDictionary<string, RayInfo> _mapRay;
        /// <summary>
        /// Get Internal Ray Maps
        /// </summary>
        public ConcurrentDictionary<string, RayInfo> RayMap
        {
            get { return _mapRay; }
        }

  

        #endregion


        #region Ctor

        public Sensor(World world)
        {
            this.World = world;
            _mapRay = new ConcurrentDictionary<string, RayInfo>();
            IsClearIgnoredBodiesPerFrame = false;
            Instance = this;
        }

        #endregion


        #region Public Methods


        /// <summary>
        /// clear the rays from backgroudn thread. Used in  winddrag
        /// </summary>
        public void SetClearPending()
        {
            clearPending = true;
        }

        private bool clearPending = false;  



        public RayInfo LaserCast(Vector2 startPos, Vector2 endPos, string laserName, Body ignoredBody, BodyColor beamColor)
        {
            RayInfo info = AddRay(startPos, endPos, laserName, ignoredBody, beamColor, true);
            info.RayType = RayType.eRayLaser;
            Raycast(info, true, true);  //make lasers pass through transparent things.  ( future bounce from shiny Armour and mirrors

            return info;
        }

          
        public RayInfo AddRay(Vector2 startPos, Vector2 endPos, string rayName, Body ignoredBody, BodyColor color, bool lineOfSight)
        {
            List<Body> bodies = new List<Body>();
            bodies.Add(ignoredBody);
            return AddRay(startPos, endPos, rayName, bodies, color, true, lineOfSight);
        }


        public RayInfo AddRay(Vector2 startPos, Vector2 endPos, string rayName, Body ignoredBody)
        {
            List<Body> bodies = new List<Body>();
            bodies.Add(ignoredBody);
            return AddRay(startPos, endPos, rayName, bodies, null ,true, false);
     
        }

        public RayInfo AddRay(Vector2 startPos, Vector2 endPos, string rayName, IEnumerable<Body> ignoredBodies )
        {
            return AddRay(startPos, endPos, rayName, ignoredBodies, null, true, false);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="endPos"></param>
        /// <param name="rayName"></param>
        /// <param name="ignoredBodies"></param>
        /// <param name="color"></param>
        /// <param name="makeViewable"></param>
        /// <param name="lineOfSight"></param>
        /// <returns></returns>
        public RayInfo AddRay(Vector2 startPos, Vector2 endPos, string rayName, IEnumerable<Body> ignoredBodies,  BodyColor color, bool makeViewable, bool lineOfSight)
        {
            RayInfo info = null;

            if ( makeViewable)   // keep the ray info for the UI thread to draw
            {
                if (_mapRay.ContainsKey(rayName))
                {
                    info = _mapRay[rayName];
                }
                else
                {
                        info = new RayInfo();
                        _mapRay.TryAdd(rayName, info);
                    

                }
            }else 
            {
                info = new RayInfo();
            }

            info.Start = startPos;
            info.End = endPos;
            info.IgnoredBodies.Clear();

            if (ignoredBodies != null)
            {
                foreach ( Body body in ignoredBodies)
                {
                    try
                    {
                        info.IgnoredBodies.CheckAdd(body);
                    }

                    catch (Exception )
                    {
                        System.Diagnostics.Debug.WriteLine("duplicateIgnoredBody: " + body.PartType);
                    }
                }
            }

            if (color != null)
            {
                info.RayColor = color;
            }

            Raycast(info, makeViewable, lineOfSight);

            return info;
        }



 

        /// <summary>
        /// Add a ray to test with other fixtures, but with per Spirit ignored body detection enabled
        /// </summary>
        /// <param name="spirit">spirit that has the body that will be ignored</param>
        /// <param name="startPos">ray start position</param>
        /// <param name="endPos">ray end position</param>
        /// <param name="rayName">unique ray name</param>
        public RayInfo AddRay(Vector2 startPos, Vector2 endPos, string rayName)
        {
            RayInfo info = null;
            if (_mapRay.ContainsKey(rayName))
            {
                info = _mapRay[rayName];
            }
            else
            {
                info = new RayInfo();
                _mapRay.TryAdd(rayName, info);
            }

            info.Start = startPos;
            info.End = endPos;

            Raycast(info);
            return info;
        }



        /// <summary>
        /// Add a ray to see a vector force field.  just for viewing vector field.. dont do ray cast.
        /// </summary>
        /// <param name="spirit">spirit that has the body that will be ignored</param>
        /// <param name="startPos">ray start position</param>
        /// <param name="endPos">ray end position</param>
        /// <param name="rayName">unique ray name</param>
        ///   <param name="color"> ray color</param>
        public void  AddRayNoCast(Vector2 startPos, Vector2 endPos, string rayName,  BodyColor color)
        {

         
                //TODO factor out  this repeat code..
                RayInfo info = null;
                if (_mapRay.ContainsKey(rayName))
                {
                    info = _mapRay[rayName];
                }
                else
                {
                    info = new RayInfo();
                    _mapRay.TryAdd(rayName, info);
                }

                info.Start = startPos;
                info.End = endPos;

                info.IsIntersect = false;
                info.IsDirty = true;

                if (color != null)
                {
                    info.RayColor = color;
                }

                if (OnRayCreated != null)
                {
                    OnRayCreated(info);
                }
            
        }


        /// <summary>
        /// Get a ray info owned by the Sensor
        /// </summary>
        /// <param name="rayName">unique ray name</param>
        /// <returns>Ray Info</returns>
        public RayInfo GetRayInfo(string rayName)
        {
            if (_mapRay.ContainsKey(rayName))
            {
                return _mapRay[rayName];
            }


            // If we found nothing, return blank RayInfo
            RayInfo info = new RayInfo();
            info.IsIntersect = false;

            return info;
        }

        /// <summary>
        /// Sensor update, should be called before game update
        /// </summary>
        public void Update(World world)
        {
        }

        /// <summary>
        /// Clear the view ray from Sensor
        /// </summary>
        public void ResetRayViews()
        {
            List<RayInfo> rays = new List<RayInfo>(_mapRay.Values);

            foreach (RayInfo info in rays)
            {
         
                // If ray is dirty, then raycast is performed OnPreUpdate on this frame
                // So clear the dirty state, so this reset code can inform UI for removal of ray view on the next frame
                if (info.IsDirty)
                {
                    info.IsDirty = false;
                }
                else
                {
                    // If the ray is not dirty, then no raycast is performed OnPreUpdate on this frame, we have IsDirty==false from last frame
                    // so, inform the listener to remove any view associated with this ray
                    if (OnRayDestroyed != null)
                    {
                        OnRayDestroyed(info);
                    }
                }
            }

            if (clearPending)
            {
                _mapRay.Clear();
                clearPending = false;
            }
        }

        #endregion
        #region Internal Methods

        private void Raycast(RayInfo info)
        {
            Raycast( info,  true, false );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="makeViewable">For debugging, draw the ray</param>
        /// <param name="lineOfSight">for line of sight, Ignore transparent objects</param>
        public void Raycast(RayInfo info, bool makeViewable, bool lineOfSight)
        {
            try
            {

                info.IsIntersect = false;
                info.IsDirty = true;

                this.World.RayCast((fixture, point, normal, fraction) =>
                {
                    if (!info.IgnoredBodies.Contains(fixture.Body) &&
                        !fixture.IsSensor
                        && !(lineOfSight && fixture.Body.IsInfoFlagged(BodyInfo.SeeThrough))
#if !( PRODUCTION )
                    && !fixture.Body.IsNotCollideable  //necessary only in tool since IsNotCollideable is selectable.
#endif
                   )
                    {

                    //NOTE! OPTIMIZE .. seems the points are given starting furthest to closest...  the last one gives the smallest fraction.. TODO.. verify this.
                    //perhaps reverse the cast, END OF LASER FIRST and take the first point then stop returning zero.. will be faster, less narrow phase linear N with N = number verts.
                    //look at box2d manual

                    //also for finding nearest hit.. seems it should stop at first interset.. using backwards ray.. 
                    //need to carefully trace the laser code.. not sure if this callback is called every hit.. or at the end of a particular
                    // broadphase subtree..must trace carefully .. using simple laser and box test.
                    if ((fixture.Body.Flags & BodyFlags.StopToBlockWind) != 0)  //TODO fix in case of LASER.. do something .. if !type is laser or somethign
                        return 0;

                        info.Intersection = point;
                        info.IsIntersect = true;
                        info.IntersectedFixture = fixture;
                        info.Normal = normal;

                        return fraction;
                    }

                // -1.0f - Filter out this fixture, as if this fixture is non existent
                // 0.0f - Terminate ray here
                // 1.0f - Continue as if no hit occured

                return -1.0f;    // ignore this fixture, next please

            }, info.Start, info.End);


#if !PRODUCTION
             if (makeViewable)
#else
            if ( makeViewable  && info.RayType == RayType.eRayLaser )  //dont ever show detection rays in game.. only tool
#endif
                {
                    if (OnRayCreated != null)
                    {
                        OnRayCreated(info);
                    }
                }


            }
            catch(Exception exc)
            {
                Debug.WriteLine(exc);
            }
			        
        }

        #endregion
    }
    
}
