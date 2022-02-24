/*
 * Game level, collection of game  and entities.   A heirarchical object data base.. some entities may contain other entites
 */
#define COLLISIONEFFECTONALL

using Core.Data.Collections;
using Core.Data.Entity;
using Core.Data.Interfaces;
using Core.Data.Plugins;
using Farseer.Xna.Framework;
using FarseerPhysics.Collision;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics.Particles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Core.Data
{
    [DataContract(Name = "Level", Namespace = "http://ShadowPlay")]
    public class Level : NotifyPropertyBase, IEntity
    {
        #region MemVars
        private ObservableCollectionUndoable<Planet> _planets;  //not fully working or much used.   A spirit and plugin can handle this.
        [DataMember]
        public int Version { get; set; } = 0;    // a stamp we can use to see what version we are using in for sanity

        /// <summary>
        /// so that clients can set this to false after doing something once per session
        /// </summary>
        static public bool FirstLoad = true;


        /// <summary>
        /// so that clients can set this to true after doing something once per level load or reset
        /// </summary>
        public bool DoneOnceOnLevelLoad = false; 
        #endregion

        #region Constructor

        public Level()
        {
            _gravity = Vector2.Zero;
            _joints = new JointCollection();//new ObservableCollectionUndoable<Joint>();// new ObservableCollectionS<Joint>();
            _planets = new ObservableCollectionUndoable<Planet>();// new ObservableCollectionS<Planet>();
            _mapBodyToSpirit = new Dictionary<Body, Spirit>();


#if PRESERVE_FIXTURES
           _mapBodyToNewShapes = new Dictionary<Body, List<PolygonShape>>();
#endif
            _entities = new EntityCollection();

            InitCommon();
        }

        /// <summary>
        /// Time in seconds for each frame.. usually 1/60 sec .. set in Simworld
        /// </summary>
        static public float PhysicsUpdateInterval = 1 / 60f;



        [OnSerializing]
        public void OnSerializing(StreamingContext sc)
        {
            Version++;
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            if (_joints == null)
            {
                _joints = new JointCollection();//new ObservableCollectionUndoable<Joint>();// new ObservableCollectionS<Joint>();
            }


            // Setup entities
            if (_entities == null)
            {
                _entities = new EntityCollection();
            }


            // set level ref to spirit
            SetParentLevelOnSpirits();

            // TODO_CHANGE: subject to elimination.. planet can just be spirit with plugin.
            if (_planets == null)
                _planets = new ObservableCollectionUndoable<Planet>();//new ObservableCollectionS<Planet>();

            if (_mapBodyToSpirit == null)
                _mapBodyToSpirit = new Dictionary<Body, Spirit>();

            if (_velocityIterations == 0)
                _velocityIterations = 10;

            if (_positionIterations == 0)
                _positionIterations = 10;

            InitCommon();

            //TODO  support other vehicles.. maybe snowboard and  partial vehicles  on extreme close block particles
            //would simply need overlap AABB.. then ..


            //TODO HACK.. FIXTHIS.... apply to BOATS AND OTHER
            _enclosedVehicle = GetSpiritEntities().FirstOrDefault(x => x.PluginName.ToLower().Contains("airship"));
        }

        private void SetParentLevelOnSpirits()
        {
            IEnumerable<Spirit> spirits = GetSpiritEntities();
            foreach (Spirit sp in spirits)
            {
                sp.SetParentLevel(this);
            }
        }


        /// <summary>
        /// Common initialization that performed similar on both constructor and deserialization.
        /// </summary>
        private void InitCommon()
        {
            Instance = this;
            Filename = "";

            DelayedEntityList = new Queue<KeyValuePair<IEntity, EntityOperation>>();
            DelayedEntityViewList = new Dictionary<IEntity, short>();



            BodiesBrokenOffToHaveCollisionShapeSimplified = new Queue<Body>();
            _addBodiesBrokenOffToHaveCollisionShapeSimplifiedLock = new object();

            SwitchMargin = new LevelMargin(3.5f, 1f, 3f, 4.5f);

            // record everthing inside this area from extends of screen.
            // 8 was choose for 1 level to record guy above falling to first bend in tube  ( thats safe since gravity is going down..).. in tunnel  
            TravellerMargin = new LevelMargin(3f, 1f, 3f, 8f);
        }
        #endregion

        #region Method

        /// <summary>
        /// Remove all object references from level. Only from level, not from 
        /// physics. Usually used before physics reset.
        /// </summary>
        public void Unload()
        {
            //TODO .. leak...seems level is slower when reset is used.. or level select..
            // Unload Spirit's Plugins
            foreach (Spirit sp in Entities.OfType<Spirit>())
            {
                if (sp.Plugin != null)
                {

                    try
                    {
                        sp.Plugin.UnLoaded();
                    }
                    catch (Exception exc)
                    {
                        Debug.WriteLine("exception in plugin Unloaded" + exc);
                    }

                    sp.Plugin = null;
                }
                sp.ReleaseListeners();
            }

#if COLLISIONEFFECTONALL

            try
            {

                foreach (Body b in GetBodyEntities())  //future all to penetrate stuff like wood
                {
                    b.OnCollision -= CollisionEffects.OnCollisionEventHandler;
                    b.ReleaseListeners();//release listers it set up
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine("exception Release collisionListeners" + exc);
            }

#endif

            _entities.Clear();
            _joints.Clear();
            _planets.Clear();

            _mapBodyToSpirit.Clear();


#if PRESERVE_FIXTURES
			if (_mapBodyToNewShapes != null)
            {
                _mapBodyToNewShapes.Clear();
            }
#endif

            Instance = null;
        }


        /// <summary>
        /// Get Body Entities, only loose bodies that dont belong to a spirit
        /// </summary>
        /// <returns>Body Entities</returns>
        public IEnumerable<Body> GetBodyEntities()
        {
            return Entities.OfType<Body>();
        }


        /// <summary>
        /// Get Spirit Entities
        /// </summary>
        /// <returns>Spirit Entities</returns>
        public IEnumerable<Spirit> GetSpiritEntities()
        {
            return Entities.OfType<Spirit>();
        }


        /// <summary>
        /// Get Planet Entity
        /// </summary>
        /// <returns>Planet Entity</returns>
        public IEnumerable<Planet> GetPlanetEntities()
        {
            return Entities.OfType<Planet>();
        }


        /// <summary>
        /// recursively remove nested set of spirit..  should never be circular..
        /// </summary>
        /// <param name="sp"></param>

        public void RemoveComplexSpirit(Spirit sp)
        {

            if (sp.AuxiliarySpirits.Count == 0)
            {
                if (Entities.Contains(sp))
                {
                    Entities.Remove(sp);
                    return;
                }
            }

            foreach (var x in sp.AuxiliarySpirits)
            {
                RemoveComplexSpirit(x);
            }

            if (Entities.Contains(sp))
                Entities.Remove(sp);
        }


        public void AddComplexSpirit(Spirit sp)
        {

           
            if (sp.AuxiliarySpirits.Count == 0)
            {
                if (!Entities.Contains(sp))
                {
                    Entities.Add(sp);
                }
                return;
            }

            foreach ( var x in sp.AuxiliarySpirits) 
            {
                AddComplexSpirit(x);
            }

            if (!Entities.Contains(sp))
            {
                Entities.Add(sp);
            }

        }

        /// <summary>
        /// Get All Bodies contained in every entity on the Entities collection
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Body> GetAllBodiesFromEntities()
        {
            // Collect all bodies from all entities
            List<Body> bodies = new List<Body>(GetBodyEntities());
            foreach (Spirit sp in GetSpiritEntities())
            {
                bodies.AddRange(sp.Bodies);
            }

            CachePlanetList();

            _planets.ForEach(x => bodies.AddRange(x.Spirit.Bodies));

            return bodies;
        }

        public void CachePlanetList()
        {
            _planets.Clear();
            foreach (Planet planet in GetPlanetEntities())
            {
                _planets.Add(planet);
            }
        }


        // this will add all object in level into simulation.
        //TODO
        // Note.. fixtures in body on deserlizated are added.. 
        //THAS probably cause of all the instabilty and crap  we should do that anymore..at serialzation time its a nightmare to maintine
        //fisrt deserialize.. then gen fixture, make the proxy later

        public void InsertToPhysics(World physics)
        {
            physics.Gravity = _gravity;


            //TODO fix thati
            // level specific gravity
            // when deserialized, Body is not automatically inserted into physics, however fixtures are.
            // because it didn't call Body(World) or Body(World,Object) constructor.
            // only newly created Body are those which will be automatically inserted into physics on new.
            foreach (Body b in GetAllBodiesFromEntities())
            {

                b.World = World.Instance;

                physics.AddBody(b);// Add body checks duplicates quickly using Addset..  

                //for entering level when creature is holding somethign with joint.                     
                foreach (AttachPoint at in b.AttachPoints)
                {
                    if (at.Joint != null)
                    {
                        physics.AddJoint(at.Joint);
                    }
                }
            }

#if COLLISIONEFFECTONALL  //for pressure calculations
            foreach (Body b in GetBodyEntities())  //future all to penetrate stuff like wood
            {
                b.OnCollision += CollisionEffects.OnCollisionEventHandler;
            }
#endif
            // joints for connecting bodies.  
            foreach (Joint pj in _joints)
            {
                if (!pj.IsBroken && physics.JointList.Contains(pj) == false)
                {
                    physics.AddJoint(pj);
                }
            }

            // TODO FUTURE   subject to elimination due to the use of general EntityCollection, remove _planets
            //dont even need planet class.. can use a plugin for this i think.  unless for follow camera sake its more elegant
            foreach (Planet planet in _planets)
            {
                planet.InsertToPhysics(physics);
            }
        }


        private AABB _staticBoundsAABB;

        /// <summary>
        /// contains the bounds of the level, cached on load, defined by static ojects.  ( highest might be static invisible clouds)
        /// </summary>
        public AABB BoundsAABB
        {
            get
            {
                return _staticBoundsAABB;
            }
        }


        /// <summary>
        /// Get level approximate boundary in aabb, combined from all entities.   Slow, this is not cached..
        /// </summary>
        public AABB CalculateAABB(bool staticBodyOnly)
        {
            AABB aabb = new AABB();
            aabb.MinimumSize();

            IEnumerable<Body> bodies = GetAllBodiesFromEntities();
            foreach (Body b in bodies)
            {
                if (staticBodyOnly && b.IsStatic == false)
                    continue;

                if ((b.Info & BodyInfo.InMargin) != 0)
                    continue;

                b.UpdateAABB();
                aabb.Combine(ref b.AABB);
            }

            if (staticBodyOnly)
            {
                _staticBoundsAABB = aabb;
            }

            return aabb;
        }


        public Spirit _enclosedVehicle;  //for special expanded AABB bounds needed when inside a ship.. used so that viewport dust doesnt originate inside ship


#if PRESERVE_FIXTURES
		private Dictionary<Body, List<PolygonShape>> _mapBodyToNewShapes;

        public Dictionary<Body, List<PolygonShape>> MapBodyToNewShapes
        {
            get { return _mapBodyToNewShapes; }
        }
#endif

        /// <summary>
        ///  if active spirit is riding in ship get better AABB..
        ///  used to prevent dust entering viewport on zoom in, when emitted outside viewport...
        /// </summary>
        /// <returns>true if expanded </returns>
        public bool GetExpandedAABBForContainerIfActiveSpiritInside(ref AABB aabb)
        {
            if (_enclosedVehicle != null)
            {
                if (_enclosedVehicle.AABB.Contains(ActiveSpirit.WorldCenter))
                {
                    aabb.Combine(ref _enclosedVehicle.AABB);
                }

                return true;
            }
            return false;
        }


        /// <summary>
        /// old enough so that sky static cloud markers dont exist, and old dress, no regen..  just to test garden , bee , and stuff like that 
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool IsActiveSpirterRevisionBeforeTransparencyAndOrganBoneDress()
        {
            return (ActiveSpirit == null ||
                string.IsNullOrEmpty(ActiveSpirit.MainBody.DressingGeom) || // this is before dress, 
                ActiveSpirit.MainBody.DressingGeom.Length < 1500); // this is before organ dress, 
        }


        /// <summary>
        ///  this is for old level with no sky marker or moutain top, just a floor 
        /// // reverse Y.. if lower bound (top) is greater ( lower)  than main creature , expand it.
        /// </summary>
        /// <param name="levelBoundary"></param>
        /// <returns></returns>
        public AABB ExpandLevelAABBIfNoSkyMarker(AABB levelBoundary)
        {



            if (ActiveSpirit != null && ActiveSpirit.MainBody != null
                && levelBoundary.LowerBound.Y > ActiveSpirit.MainBody.Position.Y)// reverse Y.. if lower bound (top) is greater ( lower)  than main creature , expand it.
            {


                AABB dynamicBoundary = CalculateAABB(false);


                if (this.Gravity == Vector2.Zero)
                {
                    levelBoundary = dynamicBoundary;
                }
                else
                {
                    // for sky on terrestial levels, we limit using both static and dynamic body.
                    // because sky is rather empty, we might add more vertical space for limit.
                    levelBoundary.LowerBound.Y -= dynamicBoundary.Height * 6.0f;
                    Debug.WriteLine("explanded level margin, sky static cloud should mark the top");
                }

            }
            return levelBoundary;
        }


        //calling this manually for now  after  replacing active spirit ..  listening to EntityCollection changed will happen every particle emission 
        public void UpdateSpiritsCache()
        {
            _spirits = new List<Spirit>(GetSpiritEntities());
        }


        /// <summary>
        /// Helper to cache entity add op on DelayedEntityList.
        /// </summary>
        public void CacheAddEntity(IEntity en)
        {
            DelayedEntityList.Enqueue(
                new KeyValuePair<IEntity, EntityOperation>(en, Core.Data.EntityOperation.Add));

        }

#if PRESERVE_FIXTURES
        //cant do this during physics update. . like on collison,  so we do it next frame
        public void CacheReplaceShapes(IEntity en, List<PolygonShape> newShapes)
        {

            Body b = en as Body;
            Debug.Assert(b != null);
            b.OnCollision -= CollisionEffects.OnCollisionEventHandler; // is case its already listenened, as in travel across level..        
            MapBodyToNewShapes.Add(en as Body, newShapes);
            DelayedEntityList.Enqueue(
               new KeyValuePair<IEntity, Core.Data.Level.EntityOperation>(en, Core.Data.Level.EntityOperation.ReplaceShapes));

        }

#else

        //cant do this during physics update. . like on collison,  so we do it next frame
        public void CacheReplaceShapes(IEntity en)
        {
            Body b = en as Body;
            Debug.Assert(b != null);

            DelayedEntityList.Enqueue(
               new KeyValuePair<IEntity, EntityOperation>(en, Core.Data.EntityOperation.ReplaceShapes));

        }

#endif


        /// <summary>
        /// Helper to cache entity remove op on DelayedEntityList.   This allows an entity to remove itself while is collectionis being iterated
        /// </summary>
        public void CacheRemoveEntity(IEntity en)
        {
            DelayedEntityList.Enqueue(
                new KeyValuePair<IEntity, EntityOperation>(
                    en, EntityOperation.Remove));

            if (en is Body)
            {
                Body b = en as Body;
                b.OnCollision -= CollisionEffects.OnCollisionEventHandler; // is case its already listenened, as in travel across level..


                
            }
        }


        /// <summary>
        /// Helper to cache entity update view on DelayedEntityViewList.   This will 
        ///remove all the old ObjectViews (dress and main body, and place new ones.
        /// 
        public void CacheUpdateEntityView(IEntity en)
        {
            CacheUpdateEntityView(en, 0);
        }

        /// <summary>
        /// Helper to cache entity update view op on DelayedEntityViewList.
        /// frameDelay will determine how many frame/cycle to skip before creating new view.
        /// frameDelay=0 should create new view immediately on next cycle, replacing the old one.
        /// </summary>
        public void CacheUpdateEntityView(IEntity en, short frameDelay)
        {
            // combine task for the same entity. 
            // previous view update task must complete first before new view update task is added for the same entity.
            // to avoid skipped update, don't set delay too long for object that changing view rapidly.
            if (DelayedEntityViewList.ContainsKey(en) == false)
            {
                DelayedEntityViewList.Add(en, frameDelay);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Entity already scheduled for view update.");
            }
        }


 


        private object _addBodiesBrokenOffToHaveCollisionShapeSimplifiedLock;
        public void AddBodyToHaveCollisionShapeSimplifiedThreadSafe(Body body)
        {
            lock (_addBodiesBrokenOffToHaveCollisionShapeSimplifiedLock)
            {
                BodiesBrokenOffToHaveCollisionShapeSimplified.Enqueue(body);
            }
        }


        /// <summary>
        /// Return all Entity in level that outside specified AABB.
        /// NOTE: Entity must already be updated to get proper AABB. 
        /// </summary>
        public EntityCollection CollectEntitiesOutsideLevelBounds(AABB trimAABB)
        {
            EntityCollection outside = new EntityCollection();

            foreach (IEntity en in Entities)
            {
                en.UpdateAABB();
                AABB entityAABB = en.EntityAABB;
                // check if not intersect
                if (AABB.TestOverlap(ref trimAABB, ref entityAABB) == false)
                {
                    // then it's fully outside. scheduled for removal.
                    outside.Add(en);
                }
            }
            return outside;
        }


        //use this to clear a rock in level 6 i can hear but cant see ...
        //usefull for similiar stuff..
        public void CleanUndesiredBodies()
        {
            //    List<Body> bodiesToClean = new List<Body>(  Level.Instance.GetAllBodiesFromEntities());
            List<Body> bodiesToClean = new List<Body>(Level.Instance.Entities.OfType<Body>());
            foreach (Body body in bodiesToClean)
            {

                if (body.SoundEffect != null && !string.IsNullOrEmpty(body.SoundEffect.StreamName) &&
                     body.SoundEffect.StreamName.ToLower().Contains("rock"))
                {
                    Level.Instance.Entities.Remove(body);
                    Debug.WriteLine("undesired body found, removign from Entities and spirits ");
                }
            }
        }


        /// <summary>
        /// Get precached AABBs for each margin side, Left, Top, Right, Bottom respectively.
        /// Used to transport objects like weapons and chasing enemies between levels.
        /// </summary>
        public AABB[] GetCachedTravellerMargins(AABB levelAABB)
        {
            AABB[] cachedTravellerMarginAABB = new AABB[4];

            // get boundary points first.
            // TravellerMargin is the span SwitchMargin boundary, span in both front and rear of it.
            // when ActiveSpirit reaches SwitchMargin,  TravellerMargin must include both in front and rear of ActiveSpirit.
            const float detectTravellerMarginWidth = 0.5f;  //  make sure its big enough to pick up all held items such as spirits.

            //TODO CODE REVIEW DWI EXPLAIN: what is the .5 for . and then why repeated?    why is different between switch and traveller considered.. im so confused

            //TravellerMargin is defined.. as // record everthing inside this area from extends of screen.     so what has SwitchMargin go to do with it?
            //so example:
            // left margin is simply: 
            //   _cachedMarginAABB[0] = new AABB(
            //      new Vector2(levelAABB.LowerBound.X, levelAABB.LowerBound.Y),
            //       new Vector2(levelAABB.LowerBound.X TravellerMargin.Left,  levelAABB.LowerBound.Y+ TravellerMargin.Bottom);


            // that 0.5 is because TravellerMargin span in both front and rear of SwitchMargin.
            // TravellerMargin is not calculated from level boundary,   but from SwitchMargin boundary, span in both front and rear of it.
            // when ActiveSpirit reach SwitchMargin,  TravellerMargin must include both in front and rear of ActiveSpirit.
            // -DC.

            //TODO DAMIAN FUTURE .. CANT JUST WIDEN THE 0.5 MOVES EVERYTHING OUT, SEE IN TOOL THE DOTTET.. SORT THIS OUT PROPERLY 
            // MY GUESS IS ITS A VALUE THAT IS NOT NEEDED. THE OTHER MARGINS MEASURE 1.5.
            //PROBLEM:  SOMETIMES WEAPONS DONT TRAVEL WITH CREATURE IF ARM EXTENDED. 0.5 METERS IS NOT MUCH
            //ALSO HAVE SEEN BALLOON NOT TRAVEL WITH CREATURE.. 


            float left = levelAABB.LowerBound.X + SwitchMargin.Left - (detectTravellerMarginWidth * TravellerMargin.Left);
            float right = levelAABB.UpperBound.X - SwitchMargin.Right + (detectTravellerMarginWidth * TravellerMargin.Right);
            float top = levelAABB.LowerBound.Y + SwitchMargin.Top - (detectTravellerMarginWidth * TravellerMargin.Top);
            float bottom = levelAABB.UpperBound.Y - SwitchMargin.Bottom + (detectTravellerMarginWidth * TravellerMargin.Bottom);

            // left margin
            cachedTravellerMarginAABB[0] = new AABB(
                new Vector2(left, top),
                new Vector2(left + TravellerMargin.Left, bottom));

            // top margin
            cachedTravellerMarginAABB[1] = new AABB(
                new Vector2(left, top),
                new Vector2(right, top + TravellerMargin.Top));

            // right margin
            cachedTravellerMarginAABB[2] = new AABB(
                new Vector2(right - TravellerMargin.Right, top),
                new Vector2(right, bottom));

            // bottom margin
            cachedTravellerMarginAABB[3] = new AABB(
                new Vector2(left, bottom - TravellerMargin.Bottom),
                new Vector2(right, bottom));


            return cachedTravellerMarginAABB;
        }


        /// <summary>
        /// Check if spirit with specific name exists in level. Case insensitive.
        /// </summary>
        public Spirit GetSpiritWithName(string spiritName)
        {
            string spiritNameLowerCase = spiritName.ToLower();
            return Spirits.FirstOrDefault(x => x.Name.ToLower().Equals(spiritNameLowerCase));
        }

        #endregion

        #region Properties

        private EntityCollection _entities;
        [DataMember]
        public EntityCollection Entities
        {
            get { return _entities; }
            set { _entities = value; }
        }

        private JointCollection _joints;
        [DataMember]
        public JointCollection Joints
        {
            get { return _joints; }
            set { _joints = value; }
        }

        private Dictionary<Body, Spirit> _mapBodyToSpirit;
        /// <summary>
        /// This only stores the MainBody of each Spirit.
        /// </summary>
        [DataMember]
        public Dictionary<Body, Spirit> MapBodyToSpirits
        {
            get { return _mapBodyToSpirit; }
            set { _mapBodyToSpirit = value; }
        }


        private AABB _startView;
        /// <summary>
        /// Initial world window view. We use AABB here, because Rect object
        /// failed to deserialized properly in silverlight.
        /// </summary>
        [DataMember]
        public AABB StartView
        {
            get { return _startView; }
            set { _startView = value; }
        }

        private float _startViewRotation;
        [DataMember]
        public float StartViewRotation
        {
            get { return _startViewRotation; }
            set { _startViewRotation = value; }
        }

        private Vector2 _gravity;
        [DataMember]
        public Vector2 Gravity
        {
            get { return _gravity; }
            set { _gravity = value; }
        }

        private int _velocityIterations = 10;   // 10 is recommended by Box 2d manual, impulses
        [DataMember]
        public int VelocityIterations
        {
            get { return _velocityIterations; }
            set { _velocityIterations = value; }
        }

        private int _positionIterations = 10;   // 10 recommended  affects position of bodies connected by joints 
        [DataMember]
        public int PositionIterations
        {
            get { return _positionIterations; }
            set { _positionIterations = value; }
        }

        private float _scale;
        /// <summary>
        /// Scale of this level. Tools only.
        /// </summary>
        [DataMember]
        public float Scale
        {
            get { return _scale; }
            set { _scale = value; }
        }

        private Spirit _activeSpirit;
        [DataMember]
        public Spirit ActiveSpirit
        {
            get { return _activeSpirit; }
            set
            {
                if (_activeSpirit == value)
                    return;

                _activeSpirit = value;
                NotifyPropertyChanged("ActiveSpirit");
            }
        }




        private string _title;
        /// <summary>
        /// Title of this level, optional, descriptive name
        /// </summary>
        [DataMember]
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                NotifyPropertyChanged("Title");
            }
        }


        //TODO check that traveler spirits force clear this cache..
        private List<Spirit> _spirits;
        /// <summary>
        /// Cache of of Spirits in the level,  so we dont have to query all entities every cycle.
        /// </summary>
        /// 

        public void ClearSpiritsCache()
        {
            _spirits = null;
        }

        /// <summary>
        /// After moving spirit using (move/respawn) this is needed to update the references in the AIs minds.
        /// </summary>
        public void ResetSpiritMindRefs()
        {
            foreach (Spirit sp in GetSpiritEntities())
            {
                if (sp.IsMinded)
                {
                    sp.Mind.Parent = sp;
                    sp.Mind.ReQuerySpirits();
                }
            }
        }

        public IEnumerable<Spirit> Spirits
        {
            get
            {
                if (_spirits == null)
                {
                    _spirits = new List<Spirit>(GetSpiritEntities());
                }
                return _spirits;
            }
        }


        /// <summary>
        /// Set by loader, levels start from 1 to N..   
        /// </summary>
        public int LevelNumber;

        /// <summary>
        /// Set by loader, levels start from 1 to N ( deep under ground).    Negative would mean up in sky.. ( since had to get  used to increasing Y  going down) 
        /// </summary>
        public int LevelDepth;


        /// <summary>
        /// Id of currently loaded level X, for traveling
        /// </summary>
        public int PrevLevelNumber;

        /// <summary>
        /// Id of currently loaded level Depth, for traveling
        /// </summary>
        public int PrevLevelDepth;



        /// <summary>
        /// Short file name
        /// </summary>
        public string Filename { get; set; }


        /// <summary>
        /// path to file or embedded name
        /// </summary>
        public string FilePath { get; set; }



        private bool _isDirty = false;
        /// <summary>
        /// Tools only.
        /// Mark if current level contents have been edited. Any action that 
        /// change object inside level should set this to true. 
        /// </summary>
        public bool IsDirty
        {
            get { return _isDirty; }
            set { _isDirty = value; }
        }

        /// <summary>
        /// Most Recent physics Level instantiated.
        /// </summary>
        public static Level Instance { get; private set; }


        /*  TODO ease, binding experiment not sure needed bind to treevew
        private static Level instance = null;


        /// <summary>
        /// Most Recent physics Level instantiated.
        /// </summary>
        public static Level Instance
        {
            get => instance;
       
        
       
            set{ 
                instance = value;

                FirePropertyChanged();
            };
        }*/

        /// <summary>
        /// A queue to manage transactions on bodies to the physics engine every cycle to avoid concurrency issues
        /// This list is applied to phyics in PreUpdatePhysics.
        /// The same entity could be inserted more than once, each for different operation.
        /// </summary>
        public Queue<KeyValuePair<IEntity, EntityOperation>> DelayedEntityList { get; private set; }

        /// <summary>
        /// Similar to DelayedEntityList, but this one specialize for updating view only.
        /// Using dictionary here, prevent duplicate entity.
        /// </summary>
        public Dictionary<IEntity, short> DelayedEntityViewList { get; private set; }


   
        public Queue<Body> BodiesBrokenOffToHaveCollisionShapeSimplified { get; private set; }


        /// <summary>
        /// Margins for level switch detection. Left , top, right , bottom. 
        /// 
        /// This margin WILL trigger level switch.  When character AABB gets this close to AABB edge .. level changes.
        /// This margin is also used to limit Camera movement.
        /// 
        /// Notes:
        /// -If traveller  in front of main character  ( leader) is to work  , SwitchMargin should be the bigger  that as traveller margin. ( on X)
        /// that will prevent character being fall of edge of world.. then get recorded and replace into ground..
        /// 
        ///     Traveller margin  should ideal match the overlap between levels ( otherwise followers or leaders might drop off world and fall forever) .
        /// -Only Main body of spirit is currently used to look for travelling spirits via broad phase query.
        /// </summary>
        public LevelMargin SwitchMargin;
        //  if left is greater than left below,   in case  of  creature being chased, and it falls of world just before PC exits should  not be recorded then repaced inside hill 
        //TODO test this..    for now .. i put a little  wall to prevent creature from falling.


        /// <summary>
        /// Margins width for transporting nearby items also with traveller to next level.. ( for better continuity between levels) 
        /// 
        /// This span in both direction of SwitchMargin, making SwitchMargin point positioned in middle of TravellerMargin.
        /// Code normally didn't check this directly, instead will check _cachedTravellerMarginAABB.  See CacheTravellerMargins().
        /// This margin is NOT used to trigger level switch.
        /// 
        /// This is lower on the bottom tunnels  so that margin area can catch falling stuff.  and for left and right sprits and objects following or being chased
        ///TODO adjust this to biggest case  for all tunnel levels.  make sure level actuall boundary is big enough on down edge
        /// currently must be 4.5 meters at least.  in level 1.. items moving down and left might actually miss the ledge ( minor bug)  might need to make this bigger in future.
        /// </summary>
        public LevelMargin TravellerMargin;
        //This amount on X Must overlap between levels or stuff will just fall.  TODO extend X overlap area for followers..
        //1 meter X  is just for the first few test levels.  8 is for Level 1 to 1b tunnel.. since there is an upper ledge.

        #endregion


        public void SetLevelNumberForTesting()
        {
            string filenameLower = Filename.ToLower();

            Level.GetTileCoordinatesFromLevelName(filenameLower, out LevelNumber, out LevelDepth);


            //PatchInTitle();
        }

        /*
        //todo fix these, finish.. THEN maybe add toooling for level top props wiht a tree view, remove 

        //TODO might just be easier add athe tree view to tool
        //SCRiPTING
        string[] levelTitles = new string[] { "The Kingdom", "The Valley", "Hill1", "HIll2", "Cliff", "Maelstrom", "Chasm", "Tank", "Boat1", "Surf" },
        string[] levelTitlesB = new string[] { "", "Mine Shaft", "Underworld", "Rope Bridge", "The Tomb", "The Mine" };
        string[] levelTitlesC = new string[] { "", "Underworld", "Corner", "", "", "" };
        //stick the level title from switch numbs to the instance fields for display in game 

      
        private void PatchInTitle()
        {
            var l = Level.Instance;
            if (LevelDepth == 1)
            {

                if (LevelNumber < levelTitles.Count())
                {
                    l.Title = levelTitles[LevelNumber];
                }
            }
        }*/
    



        /// <summary>
        /// gets the Y of the ground level.. (usually used for wind) 
        /// </summary>
        /// <returns></returns>
        public float GetGroundLevelY()  //TODO  remove this hack, and  use a sort of marker for this..  AttachPt tagged. or something.  //or add level data and prop sheet for it..
        {
            switch (LevelNumber)
            {
                case 1:
                    return 5f;
                case 2:
                    return 22.2f;
                case 3:
                    return 41f;
                case 6:
                    return -130;
                case 7:
                    return 362;

                case 9:
                    return 419.5f;  // currrent sea levle  

                case 10:
                case 11:
                    return 419.5f;  // below waves trough in seas


                default:
                    return 103f;  //TODO sea level?  or bottom  cliff.. and long plain 

            }
        }





        //TODO review , generalize and Comment , and clean.. NOw.. all characters are auto named after the level they came from.
        /// <summary>
        /// Concat level ID with spirit name, for spirit other than Player Charater  & clouds which are just for ambience.  ( used for travelling between levels)
        /// Purpose is to prevent duplicates when follower spirit goes back to its level of origin.  
        /// </summary>
        public void SetNameofSpawnedSpirit(Spirit spirit)
        {
            if (!spirit.Name.ToLower().Equals("yndrd")
                && spirit.SpiritFilename != null
                && !spirit.SpiritFilename.ToLower().Contains("cloud"))
            {
                // _level.FileName mostly null, can't use that  , level num and depth will be unque. Here we name characters after where they were spawned from
                //so if a character follows you from level to another , or more, then back to his place of origin.. he shold not be respawned there. 
                spirit.Name += spirit.SpiritFilename + LevelNumber.ToString() + LevelDepth.ToString();
            }
        }



        //TODO add num verts , original pos or something else
        //add to deserialize .. and on loaded maybe bump duplicates.
        public string GetUniversalID(IEntity entity, int num, int depth)
        {
            return entity.ID.ToString() + num.ToString() + depth.ToString();
        }



        public void RemoveDupicateEntities(IEnumerable<IEntity> travellers)
        {

            //NOTE  .. Body ID is not guaranteed unique.. need to add # verts + original pos..
            List<IEntity> travelingBack = new List<IEntity>();


            foreach (IEntity traveller in travellers)
            {

                //TODO shold do with simple swords?.. might as well  try.
                foreach (IEntity savedEntity in Entities)
                {
                    if (savedEntity == traveller)  //this is all called  AFTER. travellers are imported.. TODO do this before and sort out its messed up.
                        continue;

                    if (GetUniversalID(traveller, PrevLevelNumber, PrevLevelDepth) == GetUniversalID(savedEntity, LevelNumber, LevelDepth))
                    {
                        travelingBack.Add(savedEntity);
                    }
                }
            }

            travelingBack.ForEach(x => Entities.Remove(x));

        }



        /// Spawn Spirit into level.
        /// Iterate all bodies in level (that are not part of any spirit), 
        /// and trigger spirit spawn on all BodyEmitter in Body.
        /// Note   currently used only for characters. 
        /// </summary>
        /// <param name="travellers">the list of travellers into the level.. wont  spawn these  unless player is starting frmo there</param>
        /// <param name="playerCharacterSpirit">if the player character to be emitted, fill this, caller can set to active spirit</param>
        public void InsertSpawnedEntities(IEnumerable<IEntity> travellers, out Spirit playerCharacterSpirit)
        {
            IEnumerable<Body> bodies = GetBodyEntities();
            playerCharacterSpirit = null;
            foreach (Body b in bodies)
            {
                foreach (Emitter em in b.EmitterPoints)
                {
                    if (em is BodyEmitter bem && !string.IsNullOrEmpty(bem.SpiritResource))
                    {
                        bool emitsPlayerCharacter = EmitsPlayerCharacter(bem);


                        if (emitsPlayerCharacter && bem.Name != "Yndrd")
                        {

                            Debug.WriteLine("fixing name on player characgter emitter legacy" + bem.Name);
                            bem.Name = "Yndrd";
                        }

                        //NOTE TODO try removing this.. because i think the naming should prevent the emission, or it will be reomved
                        //LIKELY REDUNDANT, prior solution to better solution based on name not class... items are auto named on birth, using level.
                        //dont emit main character , we have travelled one into level 
                        if (emitsPlayerCharacter && travellers.Any()
                            || EmitsVehicle(bem) && ContainsPlayerVehicle(travellers) //or airship vehicle we flew in with

                            )
                        {
                            bem.Active = false;
                            bem.SkipPreload = true;  //so it wont preload
                            continue;
                        }

                        if (emitsPlayerCharacter)
                        {
                            playerCharacterSpirit = bem.NextEntityToEmit as Spirit;
                        }
                    }
                }
            }
        }


        //TODO place a MakeActive prop would be better.. when changed might remove the make active from all the others
        //or a pointer to active spirit emitter on the level..

        private static bool EmitsPlayerCharacter(BodyEmitter bem)
        {

            // .. TODO use the unique name... sometimes we need to spawn enemies, sometimes not. , when going back
            bool isPlayerCharacter = (bem.SpiritResource.ToLower() == "namiad.spr" &&
              (string.IsNullOrEmpty(bem.PluginName) || bem.PluginName.ToLower() == "yndrdplugin"));

            return isPlayerCharacter;
        }

        private static bool EmitsVehicle(BodyEmitter bem)  //TODO FUTURE more general way  .. using bem.PluginName.. set in all emitters... or better use Name..
        {
            return (!string.IsNullOrEmpty(bem.SpiritResource) && bem.SpiritResource.ToLower().Contains("airship"));
        }


        private static bool ContainsPlayerVehicle(IEnumerable<IEntity> travellers)
        {
            if (travellers == null)
                return false;

            foreach (IEntity en in travellers)
            {
                if (GetPluginName(en) == "airship")       //TODO FUTURE fix  HACK..  use the name , generate one from level.... name the vehicle or person is all .. 
                    return true;
            }
            return false;
        }

        private static string GetPluginName(IEntity en)
        {
            Spirit sp = en as Spirit;
            return sp == null ? "" : sp.PluginName.ToLower();
        }



        /// <summary>
        ///  Script compiled and run when a level is loaded in Game or Tool, for initializion or tests that go with this level.  
        ///  For example , Load runtner, level loaded method implemented  is a loop spawn,  get currenttime, run until AcitveSpirit.WorldCenter.X > 15;  record best time as Level Params 
        ///  TODO, like FluidParams clean respawn, repeat, tunning params keeping best score
        /// </summary>



        //TODO  finish basically useful metrics for tuners optimizer levels 
        //Timers , 
        /// <summary>
        /// ElaspedTime in seconds since the level was Loaded and run.
        /// </summary>

        double ElaspedTime { get; set; }

        //current best time for an  optimization study   ( todo, save this into a Study like Generative Design in Autocad Fusion 360?
        // [DataMember]
        double BestTime { get; set; }



        //private static bool EmitsPlayerVehicle(BodyEmitter bem)
        //{
        //    return (bem.PluginName.ToLower() == "airship");  //TODO .. find a better way to know fi character in or on vehicle.
        //}

        public void NullAllPreloadedEntities()
        {
            IEnumerable<Body> bodies = GetAllBodiesFromEntities();
            foreach (Body b in bodies)
            {

                if (b.EmitterPoints == null)
                    continue;

                foreach (Emitter em in b.EmitterPoints)
                {
                    var bem = em as BodyEmitter;
                    if (bem != null)
                    {
                        bem.NextEntityToEmit = null;
                    }
                }
            }
        }


        //TODO REDO ALL THIS CODE.. WE HAVE ACCURATE COLLISION DATA.. WAS FOUND FOR BULLET TO BRAIN..IMPLEMENTATNO.. EVEN BETTER  TO USE POST SOLVE ALSO..

        //TODO move all this to creature..  or separate class.

        /// <summary>
        /// when collide, create wound marks, depending on impulse or sharp points nearby.
        /// some object that walked over can also give impulse, about 5-11 Ns, distance 0.04 - 0.05f.
        /// </summary>
        /// <param name="contact"></param>
        /// <param name="worldContactPoint"></param>
        /// <param name="normal">normal pointing away from our Body</param>
        /// <param name="maxImpulse"></param>
        /// <param name="ourBody"></param>
        /// <param name="strikingBody"></param>
        public void HandleDamageOnCollide(Spirit spirit, Body ourBody, Body strikingBody, Vector2 contactWorldPosition, Vector2 contactWorldNormal, float contactImpulse, bool makeMark)
        {


            //TODO refactor this with the bullet cut code..in CollisionEffects
            if (contactImpulse < Body.MinImpulseForMarkCreation)
                return;

            bool isStrikingBullet = (strikingBody.Info & BodyInfo.Bullet) != 0;

            if (!makeMark && !isStrikingBullet)
                return; // dont bother must be hand , foot , or something.

            //using rel  last frame beause  TOI reports this after LinearVelocity as been changed.. its already on reaction.. for bullets is already bouncing back.
            Vector2 relativeVelocity = strikingBody.LinearVelocityPreviousFrame - ourBody.LinearVelocityPreviousFrame;  //always go to our body at rest intertial reference frame ( never mind rotation).. this fight could be in a fast spaceship.
#if DEBUG
            Vector2 relVelocity = strikingBody.LinearVelocity - ourBody.LinearVelocity;
#endif

            //a ray intersect from contact point along normal..
            Vector2 velOfStrikingBody = relativeVelocity;
            SharpPoint closestSharp = GetClosestSharpPointingFromStrikingBody(strikingBody, ref contactWorldPosition, ref contactWorldNormal, 30);

            //// weapon stab is between 12 - 64.  but toe can get 20-30 sq linear vel on normal walk, so it was blocked before this.
            //if (relativeVelocity.LengthSquared() < 10f) // squared vel seems to have high variation
            // boxing vs swordsman give about 5-7. tested 10 only sword stab will pass. 
            // tested 5.5 while still give reasonable bruise when punching with swordsman, mostly on head and neck.

            float minRelVel = 5.5f;

            if (closestSharp != null)
            {
                minRelVel = 2f;  //testing with Shuriken ( star)  in Brusiesharp test.
            }

            if (relativeVelocity.Length() < minRelVel)
                return;

            if (closestSharp != null && !isStrikingBullet)//
            {
                //TODO event for pickaxe ...should    to get this info from the prior transform.. but only  case it bounces from target..
                // if local origin is near the head of pickaxe.. linear vel  previous frame would be fine..  TODO can do this when translate vertex is done.

                //TODO should use xform from previous frame here GetLinearVelocityFromLocalPoint
                //or  put  0,0 near pickxe end..  that shoudl be where linear vel is i think
                velOfStrikingBody = strikingBody.GetLinearVelocityFromLocalPoint(closestSharp.LocalPosition)// for a think like a swinging pickaxe this will make a difference.                 
                   - ourBody.LinearVelocityPreviousFrame;  //always go to our body at rest reference frame.. this fight could be in a fast spaceship            
            }

            bool isFromRayIntersection = false;

            //TODO consider move this to the CollisionEffects, then clean out  static Level.PhysicsUpdateInterval or make it per level

            //TODO this can lead to scars  inside on fixture boundaries.. use prior postion.  

            //TODO CLEAN :  AFTER FIXING THE FARSEER EVENT INTO THIS MAYB NOT BE NEEDED, SHOULD HAVE BEEN CLEANED OUT.
            Vector2 contactCollisionPointWorldAdjusted = GetAdjustedWorldPostionUsingRayIntersection(ourBody, ref contactWorldPosition, ref velOfStrikingBody, out isFromRayIntersection);

            //CreateMarkPoint  will check isInside.. if say a heavy stone or a fall causes bruse.. it won't come from the intersection.       
            //avoid creating mark point near joint anchor on this body.. . this is an curved area. so the normal wont be good it will stick out
            //skin and Zorder creates  the illusion of a continuous skin
            //TODO consider to  only put bruise not from isFromRayIntersection..

            // if (isFromRayIntersection || closestSharp == null)  // TODO must  fix..  issue.. marks can be floating in air.. not moved..
            {
                //TODO consider bullet striking neck should cut it  .. need a different size here.
                //each body part maybe different dist.. and get nearest joitn
                //TODO organs collide....



         const float minDistanceToJointAnchorSq = 0.04f * 0.43f;  //avodi brusie or close to ends direction can be off or its in overlap.

                //TODO  can still make ton of bruise near joints ..looks bad.   
                Joint nearbyJoint = Spirit.GetFirstJointWithinDistance(ourBody, ref contactCollisionPointWorldAdjusted, minDistanceToJointAnchorSq, true, BodyInfo.Bullet);
                if (nearbyJoint == null)
                {
                    CreateMarkRefPoint(ourBody, strikingBody, contactCollisionPointWorldAdjusted, contactWorldNormal, contactImpulse, closestSharp != null, false); //TODO can get accurate pts on the proper event.. post solv..or  see the bullet penerate to skull code.. post solve i think
                }


                //TODO send event..  or move all this code to creature  .. however any spirit can be damaged or shot up..     mark style depends on material tho.  
            }
            //TODO  sword handle    back or stalatite ( if sharp is not near the contact point) .. .
            ///Refactor common code between scar impact and mark creation and sharp.
        }

        /// <summary>
        /// on fast moving objects the manifold word point is not correct.. use a reay to our our body to 
        /// </summary>

        private Vector2 GetAdjustedWorldPostionUsingRayIntersection(Body ourBody, ref Vector2 startWorldPosition, ref Vector2 velOfStrikingBody, out bool isIntersecting)
        {
            Vector2 contactCollisionPointWorld = startWorldPosition;  //start with what the collision tells us
            isIntersecting = false;
            //TODO FUTURE NOTE will just need to cache old transforms to make this work perfectly..however it works well enoug..  //TODO ERASE all this.. was solved properly  for bullet to brain code.
            // on fast reaction the user won't even see this.
            // one way is to Report in Island -before- the collision (TOI ) is solved or applied
            //TODO to find intersect point using  last frame would have to add a sensor for the body..
            //xform it with last xfrom,   see ACCESS_LAST_FRAME    to the tree. and  then do the ray cast to
            //however.. its ok  .. take new intersec.. new body.. and stick  a mark there.    that should work, close enough.
            //should try and figure peneration at least were to put  bullet in head or in body..  using ray intersection.
            //should try for old transfrom.. or find a point inside that is projected from the ray interfect ..
            //on bodylocal.. to the normal.. but should be using last frame.. with bullet hit.. old transforms                 
            //Then try insert bullet into main body..
            const float rayExtensionFactor = 1.4f;  //make x factor longer..   if not, the distance of frame may still not be intersecting with our body, because of the body reaction away
            Vector2 displacementPerFrame = velOfStrikingBody * PhysicsUpdateInterval * rayExtensionFactor;
            const float minDistPerFrame = 0.005f;//  half centimeter.

            if (displacementPerFrame.LengthSquared() > minDistPerFrame * minDistPerFrame)// don't do ray if length == zero, cause tree exception.  
            {
                startWorldPosition -= displacementPerFrame;  //start back one frame.. dont want to start inside fixture.. this is after the reaction.
                RayInfo rayInfo = Sensor.Instance.AddRay(startWorldPosition, startWorldPosition + displacementPerFrame, "collideMark");

                //TODO either bring this out( see above, its done)  or test inside .. getting bruises inside main body
                //TODO fix fixtures on AIs then
                if (rayInfo.IsIntersect && rayInfo.IntersectedFixture.Body == ourBody)
                {
                    contactCollisionPointWorld = rayInfo.Intersection;
                    isIntersecting = true;
                }
            }
            return contactCollisionPointWorld;
        }

        /// <summary>
        /// Returns closest sharp point if a starp is struck at a good angle ( checked for sword only)  or else null if blunt or sword is not poked
        /// </summary>
        /// <param name="external"></param>
        /// <param name="contactWorldPosition"></param>
        /// <param name="contactWorldNormal"></param>
        /// <param name="angleOfIncidence"></param>
        /// <returns></returns>
        private static SharpPoint GetClosestSharpPointingFromStrikingBody(Body external, ref Vector2 contactWorldPosition, ref Vector2 contactWorldNormal, float angleOfIncidence)
        {
            SharpPoint closestSharp = GetClosestSharpPoint(external, ref contactWorldPosition);
            //or check if sword or bullet is pointed at victiim
            // specific case for sword only, check sword angle.
            CheckIfSwordPointingAtSurface(external, ref contactWorldNormal, angleOfIncidence, ref closestSharp);
            return closestSharp;
        }

        private static void CheckIfSwordPointingAtSurface(Body external, ref Vector2 contactWorldNormal, float angleOfIncidence, ref SharpPoint closestSharp)
        {
            if (closestSharp != null &&
                ((external.Info & BodyInfo.Bullet) == 0) && //since this is after collision response.. bullet no longer points at target
                external.AttachPoints.Count == 1 &&
                external.SharpPoints.Count == 1)
            {

                Vector2 swordVec = external.AttachPoints[0].WorldPosition - external.SharpPoints[0].WorldPosition;
                double strikeAngle = MathUtils.VectorAngle(ref contactWorldNormal, ref swordVec);

                // only cause scar if sword hit from angle to angle degree.
                float angleLimit = MathHelper.ToRadians(angleOfIncidence);
                if (!(strikeAngle > -angleLimit && strikeAngle < angleLimit))
                {
                    closestSharp = null;    // if outside angle, sword hit considered blunt.
                }
            }
        }

        private static SharpPoint GetClosestSharpPoint(Body external, ref Vector2 contactWorldPosition)
        {
            SharpPoint closestSharp = null;
            float smallestDist2 = float.MaxValue;
            foreach (SharpPoint sharpPoint in external.SharpPoints)
            {
                //TODO FUTURE CONSIDER to  use position from last frame, for TOI.. or report before solving
                float dist2 = (sharpPoint.WorldPosition - contactWorldPosition).LengthSquared();
                if (dist2 < smallestDist2)
                {
                    smallestDist2 = dist2;
                    closestSharp = sharpPoint;
                }
            }
            return closestSharp;
        }


        public IEnumerable<BodyEmitter> GetAllLevelEmitters()
        {
            return GetAllLevelEmitters(GetAllBodiesFromEntities());
        }


        public IEnumerable<BodyEmitter> GetAllLevelEmitters(IEnumerable<Body> bodyList)
        {
            foreach (Body body in bodyList)
            {
                foreach (Emitter em in body.Emitters)
                {
                    if (em is BodyEmitter && (((BodyEmitter)em).SpiritResource)!= null && ((BodyEmitter)em).SpiritResource.EndsWith(".wyg"))
                        yield return em as BodyEmitter;
                }
            }
        }
        /// <summary>
        /// Returns all the enity emitters using external files by reference
        /// </summary>
        /// <param name="bodyList"></param>
        /// <returns></returns>

        public IEnumerable<BodyEmitter> GetAllXrefEntityEmitters(IEnumerable<Body> bodyList)
        {
            foreach (Body body in bodyList)
            {
                foreach (Emitter em in body.Emitters)
                {
                    if (em is BodyEmitter && !string.IsNullOrEmpty(((BodyEmitter)em).SpiritResource) )
                        yield return em as BodyEmitter;
                }
            }
        }

        public IEnumerable<BodyEmitter> GetAllXrefEntityEmitters()
        {
            return GetAllXrefEntityEmitters(GetAllBodiesFromEntities());
        }



        /// <summary>
        /// Place appropriate wound mark .. these marks are permanent but could fade with time..
        /// Sharp hit  makes scar.
        //  Blunt hit can make bruise.
        /// </summary>
        /// </summary>
        /// <param name="ourBody"></param>
        /// <param name="strikingBody"></param>
        /// <param name="contactWorldPosition"></param>
        /// <param name="contactWorldNormal"></param>
        /// <param name="contactImpulse"></param>
        /// <param name="isSharpObject">if true scar like triange.. if false bruise</param>
        private void CreateMarkRefPoint(Body ourBody, Body strikingBody, Vector2 contactWorldPosition, Vector2 contactWorldNormal, float contactImpulse, bool isSharpObject, bool bulletWound)
        {
            MarkPoint woundMark = ourBody.AttachContactPointAsVisualMark(contactWorldPosition, contactWorldNormal, contactImpulse, isSharpObject, strikingBody);
        }

        /// <summary>
        /// Iterate all bodies BodyEmitter and preload the entities from referenced files so that emitting at realtime will be faster
        public void PreloadAllBodyEMitters()
        {

            IEnumerable<BodyEmitter> bems = GetAllXrefEntityEmitters();


            foreach (BodyEmitter x in bems)
            { 
                x.CheckToCacheEntity(); 
            }

        }




        public Spirit SpawnEntities()
        {
            return SpawnEntities(Enumerable.Empty<IEntity>());

        }

        public Spirit SpawnEntities(IEnumerable<IEntity> travellers)
        {
            Spirit pcSpirit = null;  //player character spirit

            try
            {
                InsertSpawnedEntities(travellers, out pcSpirit);  //Todo use the Name for this, creature name, make sure unique creature can travel and not meet its clone

                PreloadAllBodyEMitters();

                //    _level.RemoveDupicateEntities(travelers); //TODO 
             //   InsertSpawnedEntities(travellers, out pcSpirit);  //Todo use the Name for this, creature name, make sure unique creature can travel and not meet its clone
            }

            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
            return pcSpirit;
        }


        #region IEntity // level has an entity so it can be hyperlinked and drawn on edge of a planet that "emits" it, using Emitter draw

        /// <summary>
        /// returns level1.wyg  level2.wyg  level3.wyg..  level1b for under 1, level2b for under 2, etc. 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static string GetLevelNameFromTileCoordinates(int x, int y)
        {

            if (x < 0)
                return null;

            string lower = "";

            char lowerChar = 'a';  // a is not used, starts with b.

            if (y > 1)
            {
                int ascii = (int)lowerChar;
                ascii += (y - 1);
                lowerChar = (char)ascii;
                lower = new string(new char[] { lowerChar });
            }

            return "level" + x.ToString() + lower + ".wyg";

        }

        /// <summary>
        /// The reverse of the above
        /// </summary>
        /// <param name="levelName"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public static void GetTileCoordinatesFromLevelName(string levelName, out int x, out int y)
        {
            x = -1; y = -1;

            try
            {


                string name = System.IO.Path.GetFileNameWithoutExtension(levelName);


                if (!name.StartsWith("level"))
                    return;

                y = 1;//ground level;

                //TODO REVISIT quick and dirty, mabye move to hyperlinks regions on levels for passing out of instead of 4 margins
                //a is not used
                //we can assume levels end with a number and a letter, level1, level1b, level1c, is below level1b is below level1,   level2 is to the left  of level1, , 1 is not necessary, and 1`is assumed ground level, etc
                if (name.EndsWith("b"))//TODO use ascii code if we are gonna continue with this way, alternative is to make hyperlinks in the files that connect
                    y = 2;
                else
                if (name.EndsWith("c"))
                    y = 3;
                else
                  if (name.EndsWith("d"))
                    y = 4;
                else
                    if (name.EndsWith("e"))
                    y = 5;

                name = name.TrimEnd(new char[] { 'a', 'b', 'c', 'd', 'e' });

                string levelNum = name.Substring(5);  //take out 5 chars for "level"

                if (levelNum.Length < 3)
                {
                    x = int.Parse(levelNum);
                }
            }
            catch (Exception exc)
            {
                Debug.WriteLine("can't parse level  filename levels " + exc.Message);
                return;
            }

        }

        public void UpdateAABB()
        {
            Level.Instance.CalculateAABB(true);
        }

        public void Update(double dt)
        {

        }

        public void UpdateThreadSafe(double dt)
        {

        }

        public void Draw(double dt)
        {

        }

        public Vector2 Position { get => BoundsAABB.LowerBound; }

        public Transform Transform => default(Transform);

        public Vector2 WorldCenter => BoundsAABB.Center;

        public float Rotation { get => 0; set { } }

        public AABB EntityAABB => BoundsAABB;

        public bool WasSpawned => false;

        public int ID => 0;

        public Vector2 LinearVelocity => default(Vector2);

        public float AngularVelocity => 0;

        public ViewModel ViewModel => null;

        /// <summary>
        /// Jpg encoded thumbnail for the level view last saved
        /// </summary>
        [DataMember]
        public byte[] Thumnail { get; set; }//todo fix spelling and resave duh





        #endregion

        /// <summary>
        /// compiled extension for startup stuff, and unloading, similar to spirit plugin
        /// </summary>
        public IPlugin<Level> Plugin { get; set; }


        private string pluginScript = "";
        /// <summary>
        /// PluginScript  loaded from xternal file
        /// </summary>
        [DataMember]
        public string PluginScript
        {
            get { return pluginScript; }
            set
            {
                pluginScript = value;
                FirePropertyChanged();
            }
        }

        IEnumerable<IEntity> IEntity.Entities => Entities;

        Vector2 IEntity.Position
        {
            get => BoundsAABB.LowerBound;
            set { throw new NotImplementedException(); }
        }

        byte[] IEntity.Thumbnail => this.Thumnail;

        public string Name => Title;

        public string PluginName =>  PluginScript;
    }

    public enum EntityOperation
    {
        Add,
        Remove,
        SpiritRegenerate,
        ReplaceShapes
    }


    // just copied from Thickness struct
    public struct LevelMargin
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;


        public LevelMargin(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }


}
