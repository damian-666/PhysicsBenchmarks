using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Particles;

using Core.Data.Animations;
using Core.Data.Interfaces;
using Core.Data.Geometry;
using Core.Data.Collections;
using System.Diagnostics;



namespace Core.Data.Entity
{
    /// <summary>
    /// Spirit AI Mind.
    /// 
    /// Avoid serializing spirit list or dict from here, because we're also referenced from Spirit. 
    /// Serializing might lead to recursive Spirit serialization, which prone to bugs 
    /// from complex deserialization order.
    /// </summary>
    [DataContract(Name = "Mind", Namespace = "http://ShadowPlay", IsReference = true)]
    public class Mind : NotifyPropertyBase
    {
        /// <summary>
        /// Spirit that own this Mind. Set by constructor, or deserializer from owner Spirit.
        /// </summary>
        [DataMember]
        public Spirit Parent { get; set; }


        /// <summary>
        /// means we are in a state of following a leader, can be in battle or as wandering together
        /// </summary>
        public Spirit Leader;   //  Cache the spirit leader when in attack or other formation. currenly used only in one place to avoid double search..  IsHeldSharpNearSelfOrFriend  which is is a Delay effect.. complicated to pass around

        //height of one floor
        public float StageHeight;

        private bool _hasQueriedSpirits = false;// ugly one shot  but due to emitters need to search for spirit like friends / enemies after first emit of spawned..  so on first mind update this is called.


        //Dictionaries so it can learn develop complex behaviors at runtime or during development.. creature will be able to give out via the natural languate query system infor about its self, bits like my "FavoriteColor" is blue, etc
        //tags, relations, and attributes for the bits of knowledge, TDB, 
        //[DataMember]
        // public Dictionary<string, object> LongMemories;   //things liek "FavoriteFoodColor = "Orange"   MostHatedSpirit = object

        //  Dictionary<string, object> objectsSeen

        //ex yndrd? what you you see?  answer qery objectsSeen, that is filed by the raycasts and sensor.



        #region Constructor

        public Mind(Spirit spirit)
        {
            if (spirit == null)
                throw new ArgumentNullException("spirit is null");

            Parent = spirit;
            Initialize();
        }

        private void Initialize()
        {
            Enemies = new FarseerPhysics.Common.HashSet<Mind>();

            // default values
            HeadSharpPointThreatRange = 0.8f;

            HeadSharpPointFastThreatRange = 1.8f;

            SharpPointThreatRange = 1.5f;  //for not moving swords

            FastSharpPointMinSpeed = 2.0f;
            SafeDistanceFromArmedMan = 12.0f;  //default meters, for a guy with sword
            FollowingDistMin = 2f;
            FollowingDistMax = 10f;
            DullNess = 1;

            Leader = null;
            StageHeight = 4f;

        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            Initialize();
        }

        #endregion


        #region Sensor parameter

        /// <summary>
        /// Measured from Head, all sharp point in this range is considered threat to head. 
        /// </summary>
        public float HeadSharpPointThreatRange { get; set; }

        /// <summary>
        /// Measured from Head, all fast sharp point in this range is considered threat to head. 
        /// </summary>
        public float HeadSharpPointFastThreatRange { get; set; }


        /// <summary>
        /// Measured from MainBody, all incoming sharp point in this range is considered threat, 
        /// except sharp point that not moving (slow) or lies below Spirit.
        /// </summary>
        public float SharpPointThreatRange { get; set; }

        /// <summary>
        /// Sharp point with speed higher than this will be considered fast. In positive value.
        /// </summary>        
        public float FastSharpPointMinSpeed { get; set; }

        #endregion


        #region Sensor

        /// <summary>
        /// Inform if left side of spirit is dead end, horizontal hip sensor blocked.
        /// </summary>
        public bool IsLeftDeadEnd { get; set; }

        public bool IsRightDeadEnd { get; set; }

        #endregion


        #region Threat state

        /// <summary>
        /// Any sharp point that threatening. If null then no sharp point threat
        /// </summary>
        public SharpPoint SharpPointThreat { get; private set; }

        /// <summary>
        /// If true then sharp point threat have high velocity.
        /// </summary>
        public bool IsSharpPointThreatFast { get; private set; }

        /// <summary>
        /// If true then sharp point threat come from left. Else it's from right
        /// </summary>
        public bool IsSharpPointThreatLeft { get; private set; }

        /// <summary>
        /// If true then sharp point threat height is on upper area of spirit (main body and head). 
        /// </summary>
        public bool IsSharpPointThreatUpper { get; private set; }

        /// <summary>
        /// The mind of most feared spirit. The one with highest value in FearOf value table.
        /// </summary>
        public Mind MindThreat { get; set; }

        /// <summary>
        /// Helper interface for MindThreat
        /// </summary>
        public Spirit SpiritThreat
        {
            get
            {
                if (MindThreat == null)
                {
                    return null;
                }
                return MindThreat.Parent;
            }
            set
            {
                if (value == null)
                {
                    MindThreat = null;
                }
                else
                {
                    MindThreat = value.Mind;
                }
            }
        }

        /// <summary>
        ///  Bogey ( unknown spirit ) detected in level.  this will a target to throw stuff at..
        /// </summary>
        public Mind PossibleMindThreat { get; set; }


        /// <summary>
        /// Helper interface for PossibleMindThreat
        /// </summary>
        public Spirit PossibleSpiritThreat
        {
            get
            {
                if (PossibleMindThreat == null)
                {
                    return null;
                }
                return PossibleMindThreat.Parent;
            }

        }

        #endregion


        // Attribute: abilities and habits: can be set by plugin, can be improved or changed during game play..
        #region Abilities

        /// <summary>
        /// This tells how often to update mind.   when mind is slow.. it will not react so quickly.
        /// For Dullness = 0 , mind is updated every cycle.  Dullness = 10, it update only  every 10 physics cycle
        /// </summary>
        public int DullNess { get; set; }
        public bool WantsToDismemberDeadEnemies { get; set; }  //if true it may chop up dead enemy if no other enemy around
        public bool CanLeanIntoPunches { get; set; }

        //  public bool AvoidStabbingWithFriendInWay { get; set; }  
        // FUTURE if no agreesion toward a spriit... LOS check should check if it might hurt buddy if this is on.
        //Note not needed let , friend is usually wide enough, to prevent guy from being in stab range..

        /// <summary>
        ///  if true creatre Eye does not need to see target to strick , used by AI plugin 
        /// </summary>
        public bool CanTargetBlind { get; set; }
        #endregion


        #region Relation table

        ///// <summary>
        ///// List of spirit mind that remembered as friend. 
        ///// Not sure if friend should be remembered across level.
        ///// </summary>
        //public List<Mind> Friends { get; private set; }

        /// <summary>
        /// List of spirit mind that remembered as threat. 
        /// TODO: traveler might also need to clear this, forget spirit on previous level, also to prevent reference leak.
        /// </summary>
        public FarseerPhysics.Common.HashSet<Mind> Enemies { get; private set; }

        private Dictionary<Mind, int> _aggressionTowards;
        public Dictionary<Mind, int> AggressionTowards
        {
            get
            {
                if (_aggressionTowards == null)
                {
                    _aggressionTowards = new Dictionary<Mind, int>();
                }
                return _aggressionTowards;
            }

        }


        private Dictionary<Mind, int> _fearOf;
        public Dictionary<Mind, int> FearOf
        {
            get
            {
                if (_fearOf == null)
                {
                    _fearOf = new Dictionary<Mind, int>();
                }
                return _fearOf;
            }
        }


        public void SetAggressionTowards(Spirit othersp, int value)
        {
            if (AggressionTowards.ContainsKey(othersp.Mind) == true)
            {
                Parent.Mind.AggressionTowards[othersp.Mind] = value;
            }
            else
            {
                AggressionTowards.Add(othersp.Mind, value);
            }
        }

        public void SetFearOf(Spirit othersp, int value)
        {
            if (FearOf.ContainsKey(othersp.Mind))
            {
                Parent.Mind.FearOf[othersp.Mind] = value;
            }
            else
            {
                FearOf.Add(othersp.Mind, value);
            }
        }


        /// <summary>
        /// this is the safe distance from people with long weapon
        /// for thieves  or creatures armed with something like gun, it is max float or level width,  usually safer closer so it will approach /attack 
        /// </summary>
        private Dictionary<Mind, float> _safeDistanceFrom;
        public Dictionary<Mind, float> SafeDistanceFrom
        {
            get
            {
                if (_safeDistanceFrom == null)
                {
                    _safeDistanceFrom = new Dictionary<Mind, float>();
                }
                return _safeDistanceFrom;
            }
        }


        private Dictionary<IEntity, int> _desireOf;
        public Dictionary<IEntity, int> DesireOf
        {
            get
            {
                if (_desireOf == null)
                {
                    _desireOf = new Dictionary<IEntity, int>();
                }
                return _desireOf;
            }
        }

        public bool WantsWeapon;


        /// <summary>
        /// for ai to attacked enemy from any distance.   don't run away from thief  or someone weidling long range weapon
        /// </summary>
        /// <returns>true if just added to enemies list..</returns>
        public bool BeginHatingOnPossibleEnemy()
        {
            if (!Enemies.Contains(PossibleMindThreat))
            {
                if (PossibleMindThreat.Parent.IsDead || PossibleMindThreat.Parent.IsHavingSeizure)
                    return false;

                Enemies.Add(PossibleMindThreat);
                SafeDistanceFrom[PossibleMindThreat] = Parent.Level.BoundsAABB.Width;
                Debug.WriteLine("PossibleSpiritThreatadd ");
                return true;
            }
            else
                return false;
        }

        public bool IsOnSameFloorLevel(Spirit sp, float tol)
        {
            return (sp.WorldCenter.Y < Parent.WorldCenter.Y + tol && sp.WorldCenter.Y > Parent.WorldCenter.Y - tol);
        }


        #endregion


        #region States, needs

        /// <summary>
        /// For precise attack targeting and pickup. Usable by player or swordsman.
        /// Updated through YndrdPlugin and Swordsman1.
        /// </summary>
        public TargetInfo AttackTarget;

        /// <summary>
        /// Second target for rear attack.    useful sword in rear hand or in both hands
        /// Similar to AttackTarget, this one is updated through YndrdPlugin and Swordsman1.    always for a straight jab
        /// </summary>
        public Vector2 AttackTarget2 { get; set; }

        /// <summary>
        /// Closest free attach point inside our sensor range that have parent Body of any type.
        /// Not including attach point on our spirit. Updated by calling UpdateClosestPickable().
        /// 
        /// NOTE: This only help reach-hand animation to aim to closest free item, but the item
        /// itself is not always pickable, because we cannot check distance and clear line-of-sight 
        /// from all possible hand from here, that will cost a lot. 
        /// The final pickup will always be determined by Spirit.CanAttach().
        /// So it's always possible for hand to pick different item other than the one aimed here.
        /// </summary>
        public AttachPoint ClosestPickableItem { get; private set; }

        /// <summary>
        /// Closest free attach point inside our sensor range that have parent Body of type weapon.
        /// Updated by calling UpdateClosestPickable().
        /// </summary>
        public AttachPoint ClosestPickableWeaponGrip { get; private set; }

        /// <summary>
        /// Indicate a distance to stop running away from threat. 
        /// If distance from threat is equal or larger than this, spirit should stop runaway.
        /// This is the default value
        /// </summary>
        public float SafeDistanceFromArmedMan { get; set; }

        //TODO consider removal.. IsMovingAway and closer.  inputs and outputs.. less state.   State needs to be minimal so it cannot get conflicted, like my BRAIN from overloaded with JUNK.
        /// <summary>
        /// Indicate if our spirit is currently in move away mode. Usually from a spirit threat.
        /// </summary>
        public bool IsMovingAway { get; set; }

        /// <summary>
        /// Indicate if our spirit is currently in move closer mode. Usually to a spirit threat.
        /// </summary>
        public bool IsMovingCloser { get; set; }

        public bool IsSleeping { get; private set; }

        [DataMember]
        public int Happiness { get; set; }

        /// <summary>
        /// Minimum distance allowed when following other spirit (SpiritToFollow is not null).
        /// </summary>
        public float FollowingDistMin { get; set; }

        /// <summary>
        /// Maximum distance allowed when following other spirit (SpiritToFollow is not null).
        /// Note that this value must be smaller than sensor range to work proper. 
        /// Sensor range at least should allow detection if distance is > FollowingDistMax.
        /// </summary>
        public float FollowingDistMax { get; set; }

        /// <summary>
        /// Indicate that spirit energy level become low.
        /// If true spirit should start searching for food.
        /// </summary>
        public bool IsHungry { get; set; }

        /// <summary>
        /// Lowest limit for energy level when it will start hungry.
        /// Default is 200. Plugin might set different value.
        /// </summary>
        public float EnergyLevelLimitForHungry = 200.0f;

        #endregion


        #region Update-related

        /// <summary>
        /// Update mind AI state for AI or main character. This is called from spirit.Update().   This is done every cycle.  Its mostly for reflexes 
        /// </summary>
        public void Update()
        {
            ResetState();
            UpdateState();
            UpdateSharpPointThreat();
            UpdateClosestPickable(MindThreat != null && ( SafeDistanceFrom.ContainsKey(MindThreat) && SafeDistanceFrom[MindThreat] > 40));
            CleanExpiredObjectsOfDesire();

            //Plugin AI update will update after this, and  other Mental state such as SpiritThreat and some FearOfTables.   If creature has some mental slowest .. these might be some frames off.
        }

        public void ResetState()
        {
            SharpPointThreat = null;
            IsSharpPointThreatFast = false;
            IsSharpPointThreatLeft = false;
            IsSharpPointThreatUpper = false;
            ClosestPickableItem = null;
            ClosestPickableWeaponGrip = null;
        }

        public void UpdateState()
        {
            IsHungry = Parent.EnergyLevel <= EnergyLevelLimitForHungry; //TODO multiply by mass factor for scaled version..
        }

        //TODO future.. allow to auto block rocks thrown.. use to use hack that put a shark inside rocks,, caused bugs.
        public void UpdateSharpPointThreat()
        {
            // note: spirit sensor use main body as center. 
            // mind threat detection are calculated using head as center.
            Body head = Parent.Head;
            if (head == null)
                return;

            // get closest sword tip from head.
            SharpPoint tip;
            bool isTipIncoming;
            float speed;
            GetIncomingOrClosestSharpPoint(head,  out tip, out isTipIncoming, out speed);
            if (tip == null)
            {
                return;
            }

            float distToHead = (tip.WorldPosition - head.WorldCenter).Length();

            // ignore all tip that outside threat range and move slowly.
            // all fast moving tip should not be ignored.
            if ((distToHead > SharpPointThreatRange && isTipIncoming == false)
                ||
                (distToHead > HeadSharpPointFastThreatRange + speed))   //or if just too far away for the speed 
            {
                return;
            }

            // update threat state 
            SharpPointThreat = tip;
            IsSharpPointThreatFast = isTipIncoming;
            IsSharpPointThreatLeft = tip.WorldPosition.X < head.WorldCenter.X;

            float midPoint = GetMidHeight();
            IsSharpPointThreatUpper = tip.WorldPosition.Y < midPoint;

            if (IsSharpPointThreatUpper)
            Debug.WriteLine("isthreat upper" + IsSharpPointThreatUpper);

            return;
        }


        /// <summary>
        /// Get incoming and/or closest sharp point tip, measured from spiritPart. 
        /// Incoming speed is compared to FastSharpPointMinSpeed.
        /// Incoming sharp point get priority, so result is not always the closest one.
        /// Sharp point that located below spirit is ignored.
        /// </summary>
        /// <param name="spiritPart">Body that part of parent spirit.</param>
        /// <param name="checkLOS">if true LOS check will be performed between spiritPart and sharp point.</param>
        /// <param name="tip">Sharp point result. Null if none found.</param>
        /// <param name="isTipIncoming">return TRUE if result is incoming tip. FALSE if result is the closest tip.</param>
        /// <param name="incomingTipSpeed">return incoming tip speed, only valid if isTipIncoming=TRUE.</param>
        private void GetIncomingOrClosestSharpPoint(Body spiritPart, /*bool checkLOS,*/ /*Sensor sensor,*/ out SharpPoint tip,
            out bool isTipIncoming, out float incomingTipSpeed)
        {
            tip = null;
            isTipIncoming = false;
            incomingTipSpeed = 0;

            if (spiritPart == null)
                return;


            

            SharpPoint closestTip = null;
            SharpPoint closestTipIncoming = null;
            float closestDist2 = float.MaxValue;
            float closestDist2Incoming = float.MaxValue;
            float dist2ToPart;


            //GRAVITY DIR... this doesnt account for relative velocty .. fighting on other platfroms, etc..
            //TODO later if ever needed..
            // get head height to ground, then divide by 5

            float groundPos = Parent.AABB.UpperBound.Y;
            float height = groundPos - Parent.Head.WorldCenter.Y; // larger down
            float lowHeight = groundPos - (height / 5f);  //TODO REVISIT FIGHTING this might ne a hack useless.. if his sword is downand not moving at use it should know
            //he might try to stab our angkesl..



            // check all sharp point in sensor
            foreach (SharpPoint sh in Parent.SharpPointsInSensor)
            {
                if (sh.Parent.Awake == false)
                    continue;

                // if sharp point belongs to weapon that currently attached to spirit, ignore it
                if (Parent.HeldSharpPoints.Contains(sh) == true)
                    continue;

                // if need los check for all possible sharp point threat. note that this is will be costly.
                //if (/*checkLOS*/ sensor != null)
                //{
                //    // if los not clear then continue
                //    if (sensor.AddRay(Parent.Head.WorldCenter, sh.WorldPosition, "sharpPointLoS" + sh.GetHashCode(), Parent.Bodies)
                //        .IsIntersect == true)
                //        continue;
                //}

                // get distance
                dist2ToPart = (sh.WorldPosition - spiritPart.WorldCenter).LengthSquared();


                //TODO incoming should use general rel vector math not this hack..  vel in our direction

                // when tip directon is incoming and above minspeed, get the closest one

                // on left, fast moving right
                if ((sh.Parent.LinearVelocity.X > FastSharpPointMinSpeed && sh.WorldPosition.X < spiritPart.WorldCenter.X) ||
                    // on right, fast moving left
                    (sh.Parent.LinearVelocity.X < -FastSharpPointMinSpeed && sh.WorldPosition.X > spiritPart.WorldCenter.X) ||
                    // on above, fast moving down
                    (sh.Parent.LinearVelocity.Y > FastSharpPointMinSpeed && sh.WorldPosition.Y < spiritPart.WorldCenter.Y) ||
                    // on below, fast moving up
                    (sh.Parent.LinearVelocity.Y < -FastSharpPointMinSpeed && sh.WorldPosition.Y > spiritPart.WorldCenter.Y))
                {
                    if (dist2ToPart < closestDist2Incoming)
                    {
                        closestTipIncoming = sh;
                        closestDist2Incoming = dist2ToPart;

                        incomingTipSpeed = sh.Parent.LinearVelocity.Length();
                    }
                }

                // if tip is not incoming
                else
                {
                    // if tip located about spirit foot height (on floor), skip it, this prevent guard for sword located on that area.
                    //TODO why?   its trying to cut our ankle?
                    if (sh.WorldPosition.Y > lowHeight)
                    {
                        continue;
                    }
                    // if tip not on floor, get closest one
                    else if (dist2ToPart < closestDist2)
                    {
                        closestTip = sh;
                        closestDist2 = dist2ToPart;
                    }
                }
            }

            // return value
            if (closestTipIncoming != null)
            {
                tip = closestTipIncoming;
                isTipIncoming = true;
            }
            else
            {
                tip = closestTip;
                isTipIncoming = false;
            }


        }


        /// <summary>
        /// Update closest available item and weapon for pickup.
        /// </summary>
        public void UpdateClosestPickable(bool needGun)
        {
            float closestItemDist2 = float.MaxValue;
            //float closestFoodDist2 = float.MaxValue;
            float closestWeaponDist2 = float.MaxValue;
            float dist2;

            //int i = 1;
            foreach (AttachPoint ap in Parent.AttachPointsInSensor)
            {
                // if connected / held by other, ignore
                if (ap.Joint != null)
                    continue;

                // get distance
                dist2 = (ap.WorldPosition - Parent.MainBody.WorldCenter).LengthSquared();

  
                // weapon only
                if ((ap.Parent.IsWeapon && !GunHandleWeKnowToBeSpent(ap)))//TODO  closest gun and closest sword.. maybe closes knife.. && !(needGun && !ap.Parent.IsInfoFlagged(BodyInfo.ShootsProjectile)))
                {
                    if (dist2 < closestWeaponDist2)
                    {
                        ClosestPickableWeaponGrip = ap;
                        closestWeaponDist2 = dist2;
                    }
                }

                // all item
                if (dist2 < closestItemDist2)
                {
                    ClosestPickableItem = ap;
                    closestItemDist2 = dist2;
                }

                //i++;
            }
        }

        public bool GunHandleWeKnowToBeSpent(AttachPoint ap)  //TODO  can some AIs be scared off with a spent weapon.
        {
            return (DesireOf.ContainsKey(ap.Parent) && DesireOf[ap.Parent] == 0);
        }


        /// <summary>
        /// Is this spirit our friend.  for now if same tribe or any swordsman, then yes.   TODO later a friend can before an enemy if 
        ///  he hurts  him several times.. etc.  then we have a List&lt;Mind&gt; Friends
        /// </summary>
        public bool IsFriend(Mind other)
        {
            return (
                     !string.IsNullOrEmpty(Parent.Tribe) && other.Parent.Tribe == Parent.Tribe ||  // creatures are marked as in same tribe                
                     other.Parent.PluginName == Parent.PluginName ||  //for now.. if same plugin class, assume friend..TODO..  mark tribes  on all            
                     Parent.Name == "Yndrd" && other.Parent.Name == gfName   //for Garden level , thats the girlfriend follows Yndrd around,.       HACK.. TODO later cleanup.   this should be at plugin level.
                     );
        }

        const string gfName = "Cunegonde";
        /// <summary>
        /// Update SpiritThreat with the most feared spirit, if same then choose the closest one.
        /// Need a properly updated FearOf table, which is currently only on Swordsman.
        /// </summary>
        public Spirit FindSpiritThreat()
        {
            Spirit mostFeared = null;
            int mostFearedLv = int.MinValue;
            float closestFearDist = float.MaxValue;

            //todo  maybe consider long and short range weapons, safe distance of..
            List<Spirit> otherSpirits = new List<Spirit>(Parent.SpiritsInSensor);

            // add PossibleMindThreat to list of potential spirit threat
            if (PossibleMindThreat != null 
                && Enemies.Contains(PossibleMindThreat)
                && !otherSpirits.Contains(PossibleMindThreat.Parent))   // else will contain duplicate
            {
                otherSpirits.Add(PossibleMindThreat.Parent);
            }

            int fearLv;
            float distance;
            foreach (Spirit othersp in otherSpirits)
            {
                if (othersp.Mind != null && FearOf.TryGetValue(othersp.Mind, out fearLv))
                {
                    if (IsFriend(othersp.Mind))
                        continue;

                    if (othersp.IsDead)
                        continue;

                    if (fearLv > mostFearedLv)
                    {
                        mostFearedLv = fearLv;
                        mostFeared = othersp;
                        closestFearDist = DistanceFrom(othersp);
                    }
                    else if (fearLv == mostFearedLv)
                    {
                        distance = DistanceFrom(othersp);
                        if (distance < closestFearDist)
                        {
                            mostFeared = othersp;
                            closestFearDist = distance;
                        }
                    }
                }
            }

            return mostFeared;

        }


        /// <summary>
        /// Get all Enemies in range.   Enemies with X or Y  distance above these values will not be included.
        /// </summary>
        /// <param name="maxXdist">Max X distance.</param>
        /// <param name="maxYdist"> Max Y distance.</param>
        /// <returns></returns>
        public IEnumerable<Spirit> GetEnemySpiritsInFrame(float maxXdist, float maxYdist)
        {
            List<Spirit> closeEnemies = new List<Spirit>();
            foreach (Mind enemyMind in Enemies)
            {
                Spirit enemy = enemyMind.Parent;

                if (Math.Abs(Parent.MainBody.WorldCenter.X - enemy.MainBody.WorldCenter.X) > Math.Abs(maxXdist))
                    continue;

                if (Math.Abs(Parent.MainBody.WorldCenter.Y - enemy.MainBody.WorldCenter.Y) > Math.Abs(maxYdist))
                    continue;
                
                closeEnemies.Add(enemyMind.Parent);
            }

            return closeEnemies;
        }


        public bool IsToTheLeft(Spirit spirit)
        {
            return spirit.MainBody.WorldCenter.X < Parent.MainBody.WorldCenter.X;
        }


        /// <summary>
        /// Remove any expired or invalid target from DesireOf.
        /// </summary>
        public void CleanExpiredObjectsOfDesire()
        {
            List<IEntity> invalidEntity = new List<IEntity>();

            // remove Body desire object that outside BodiesInSensor
            foreach (KeyValuePair<IEntity, int> pair in Parent.Mind.DesireOf)
            {
                Body body = pair.Key as Body;
                if (body == null)
                    continue;

                // check if entity still exist in sensor, or else we forget it
                if (Parent.BodiesInSensor.Contains(body) == false)
                {
                    invalidEntity.Add(pair.Key);
                }
            }

            // clean non-existent desire
            foreach (IEntity entity in invalidEntity)
            {
                Parent.Mind.DesireOf.Remove(entity);
            }
        }



        public Vector2 GetPositionToLookAt()
        {
            Spirit spiritInFront = GetClosestSpirit(RelativePosition.InFront, false, false, RelationShip.None);

            Body bodyOfInterest = GetClosestBody(1, true, null, true);//anything edible  in front or in hands

            Vector2 ahead = GetVectorHorizontalStraightAhead();  //default

            if (bodyOfInterest == null)
            {
                //grabable body must be 2 meters away .. relevant.
                float minDistanceOfInterest = 2 * Parent.SizeFactor;

                AttachPoint atc = Parent.GetNearestAttachPoint(true, true, PartType.None, minDistanceOfInterest);
                if (atc != null)
                {
                    bodyOfInterest = atc.Parent;
                }
            }

            if (bodyOfInterest == null)
            {
                bodyOfInterest = GetClosestBody(0, true, Parent.HeldBodies, true);
                bodyOfInterest = ExcludeBodyFromInterest(bodyOfInterest);
            }

            if (SharpPointThreat != null)
            {
                return SharpPointThreat.WorldPosition;
            }
            else
            {

                if (SpiritThreat != null)
                {
                    Parent.PositionLookingAt = GetPointToLookAtOnSpirit(SpiritThreat);
                }
                //if spirit moving  or emitting in front
                else if (spiritInFront != null && spiritInFront.MainBody.LinearVelocity.Length() > 0.05)
                {
                    return GetPointToLookAtOnSpirit(spiritInFront);
                }
                else
                    if (bodyOfInterest != null)
                    {
                        return bodyOfInterest.WorldCenter;
                    }
                    else
                    {//should we use target filter, just revert to default stare..                   
                        return ahead;// 
                    }
            }

            return ahead;
        }

        private Body ExcludeBodyFromInterest(Body bodyOfInterest)
        {
            if (bodyOfInterest != null && bodyOfInterest.PartType ==
                PartType.Stone &&
                Parent.IsWalking &&
                DistancePerceivedFrom(bodyOfInterest) > Parent.AABB.Height * 1.6//dont look at stones unless close by and we are walking  towards it.
                 ||   // a bullet stuck to us.. dont look at it.
                 (bodyOfInterest != null && ((bodyOfInterest.Info & BodyInfo.Bullet)!=0) && bodyOfInterest.AttachPoints.Count() > 0
                && bodyOfInterest.AttachPoints[0].Joint != null) && Parent.BodySet.Contains(bodyOfInterest.AttachPoints[0].Partner.Parent))
            {
                bodyOfInterest = null;
            }
            return bodyOfInterest;
        }


        public Vector2 GetVectorHorizontalStraightAhead()
        {
            return GetVectorHorizontal(Parent.IsFacingLeft);
        }

        public Vector2 GetVectorHorizontal(bool left)
        {
            float xHorizon = 10000;
            float yHorizon = Parent.MainBody.WorldCenter.Y;

            if (left)
            {
                xHorizon *= -1;
            }

            Vector2 horizon = new Vector2(xHorizon, yHorizon);
            return horizon;
        }


        public Vector2 GetVerticalStraightBelowVector()
        {
            Vector2 below = new Vector2(Parent.MainBody.WorldCenter.X, 10000);
            return below;
        }


        Vector2 GetPointToLookAtOnSpirit(Spirit spirit)
        {
            Body spiritPart = spirit.Head;
            if (spiritPart == null)
            {
                spiritPart = spirit.MainBody;

                if (spiritPart == null)
                {
                    Debug.WriteLine("GetPointToLookAtOnSpirit failed.. Mainbody missing " + spirit.Name);
                    return Vector2.Zero;
                }
            }
            return spiritPart.WorldCenter;

        }


        //return first active emitter from spirit..
        bool LookAtEmissionPointFromSpirit(Spirit spirit, ref Vector2 point)
        {
            if (spirit == null)
                return false;

            foreach (Body body in spirit.Bodies)
            {
                foreach (Emitter em in body.EmitterPoints)
                {
                    if (em.Active == true)
                    {
                        point = em.WorldPosition;
                        return true;
                    }
                }
            }
            return false;
        }


#endregion


#region Actions or responses


        /// <summary>
        /// Move head away from incoming sharp point. Keyframe filter is used to animate the neck.
        /// Currently called from script, parameter needs to come from there.
        /// </summary>
        public void SetTargetFilterToMoveHeadAwayFromSwordTip(int neck1Idx, int neck2Idx)
        {
            if (SharpPointThreat == null)
            {
                return;
            }

            Body head = Parent.Head;

            float distToHead = (SharpPointThreat.WorldPosition - head.WorldCenter).Length();

            // if outside head near range but move slowly, return
            if (distToHead > HeadSharpPointThreatRange && IsSharpPointThreatFast == false
                || distToHead > HeadSharpPointFastThreatRange)   //or if just too far away.. 
            {
                return;
            }

            // set angle direction
            float angle;
            // System.Diagnostics.Trace.TraceInformation("passed");
            // sword tip on left of creature
            if (IsSharpPointThreatLeft == true)
            {
                angle = 0.4f;
            }
            else
            {
                angle = -0.4f;
            }

            //move neck joints
            Parent.TargetFilter.SetTarget(neck1Idx, angle);
            Parent.TargetFilter.SetTarget(neck2Idx, angle);
        }


#endregion


#if !(PRODUCTION || SILVERLIGHT)
        const bool MakeViewable = true;
#else
        const bool MakeViewable = false;
#endif


#region Methods

        /// <summary>
        /// Distance between us and other spirit.
        /// </summary>
        public float DistanceFrom(Spirit other)
        {
            //TODO AABB?
            return Vector2.Distance(Parent.MainBody.WorldCenter, other.MainBody.WorldCenter);
        }


        public float DistancePerceivedFrom(Body other)
        {
            if (Parent.Head != null)
            {
                return DistanceFromHead(other);
            }
            else
                return DistanceFromMainBody(other);
        }

        /// <summary>
        /// Distance between our head  and other body.
        /// </summary>
        public float DistanceFromHead(Body other)
        {
            return Vector2.Distance(Parent.Head.WorldCenter, other.WorldCenter);
        }

        /// <summary>
        /// Distance between our main body and other body.
        /// </summary>
        public float DistanceFromMainBody(Body other)
        {
            return Vector2.Distance(Parent.MainBody.WorldCenter, other.WorldCenter);
        }


        /// <summary>
        /// AABB distance between us and other spirit.  
        /// </summary>
        public float DistanceBetweenAABB(Spirit other)
        {
            float x;
            float y;

            if (Parent.AABB.Contains(ref other.AABB))
            {
                return 0;
            }

            if (Parent.AABB.UpperBound.X <= other.AABB.LowerBound.X)
            {
                x = other.AABB.LowerBound.X - Parent.AABB.UpperBound.X;

            }
            else
            {
                x = other.AABB.UpperBound.X - Parent.AABB.LowerBound.X;
            }


            if (Parent.AABB.UpperBound.Y <= other.AABB.LowerBound.Y)
            {
                y = other.AABB.LowerBound.Y - Parent.AABB.UpperBound.Y;

            }
            else
            {
                y = other.AABB.UpperBound.Y - Parent.AABB.LowerBound.Y;
            }

            return new Vector2(x, y).Length();

        }

        /// <summary>
        /// Calculate distance from main body to an attachpoint .
        /// This is different from Spirit.Attach() where distance is measured from closest 
        /// possible distance (hand with open attachpoint).
        /// </summary>
        public float DistanceFrom(AttachPoint ap)
        {
            //TODO AABB?
            return Vector2.Distance(Parent.MainBody.WorldCenter, ap.WorldPosition);
        }

        private float GetMidHeight()
        {
//TODO GRAVITYDIR

               Body head = Parent.Head;
               if (head == null)
               {
                    return 0;
               }


            //OLD code.. dont use stuff moved to walk that knows feet.
            // get head height to ground, then divide by 2. remember y pos is higher going down.
              float groundPos =  Parent.AABB.UpperBound.Y;
              float midPoint = head.WorldCenter.Y - ((head.WorldCenter.Y - groundPos) / 2f);


           

            return midPoint;

        }



        //todo spirits are  always taking measurement  of others to decide what to do about them
        // might separate this stuff to a Perception class later for better battles..
        bool IsBiggerThanMe(Spirit otherSpirit)
        {
            return (otherSpirit.AABB.Height > Parent.AABB.Height);  //looking big might scare off something..
        }



        /// <summary>
        /// Check if line of sight is clear from any eye eye/head to AttackTarget.
        /// </summary>
        public bool IsClearEyeLOSToTarget(Sensor sensor, Vector2 attackTarget, IEnumerable<Body> ignoredBodiesTarget)
        {
            if (Parent.Head == null)
                return false; //head not connected, can't see target..

            List<Body> ignoredbodies = new List<Body>();
            ignoredbodies.AddRange(ignoredBodiesTarget);

            IEnumerable<Body> eyes = Parent.GetAllBodiesWithPartFlags(PartType.Eye, false, false);
            ignoredbodies.AddRange(eyes);
            foreach (Body eye in eyes)
            {
                if (sensor.AddRay(eye.WorldCenter,
                    attackTarget, "headLoS" + eye.GetHashCode(), ignoredbodies).IsIntersect == false)
                    return true;   // clear view from one eye at least
            }

            return false;
        }



        public bool IsClearLOSPointToTarget(Sensor sensor, Vector2 attackTarget, Vector2 startPoint, IEnumerable<Body> bodiesToIgnore, out Body blockingBody)
        {

            blockingBody = null;
            try
            {
                BodyColor jointFindRayColor = new BodyColor(33, 233, 24, 255);
                RayInfo ray = sensor.AddRay(startPoint, attackTarget, "IsClearPointToTt" + startPoint.ToString() + attackTarget.ToString(), bodiesToIgnore, jointFindRayColor, MakeViewable,true);

                if (ray.IntersectedFixture != null)
                {
                    blockingBody = ray.IntersectedFixture.Body;
                }
                
                return (!ray.IsIntersect);
            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in IsClearLOSPointToTarget" + exc.Message);// get dublicate key sometimes are we checking same point twice?
                return false;
            }
        }

        public bool GetHeldSwordTipPosition(bool left, out Vector2 weaponPointPosition)
        {
            weaponPointPosition = Vector2.Zero;
            Body attackBody = Parent.GetHeldWeaponBody(left);

            // get weapon tip. if multiple sharp point, just pick the first one for now
            if (attackBody != null && attackBody.SharpPoints != null && attackBody.SharpPoints.Count > 0)
            {
                weaponPointPosition = attackBody.SharpPoints[0].WorldPosition;
                return true;
            }
            // weapon is invalid, no need to continue
            return false;
        }


        /// <summary>
        /// Check if line of sight is clear from weapon tip to AttackTarget.
        /// </summary>
        public bool IsClearLOSSwordTipToTarget(Sensor sensor, bool left, Vector2 attackTarget, IEnumerable<Body> bodiesToIgnore)
        {
            Vector2 weaponPointPosition;
            if (!GetHeldSwordTipPosition(left, out weaponPointPosition))
                return false;


            try {

                //note sword and our held objects are currenty  ignored
                return (sensor.AddRay(weaponPointPosition, attackTarget, "weaponLoS" + attackTarget.ToString(), bodiesToIgnore)
                    .IsIntersect == false);

            }

            catch(Exception exc)
            {
                Debug.WriteLine(exc);
                return false;
            }

        }


        /// <summary>
        /// if something is moving at us that could cause damage.
        /// </summary>
        /// <returns>something moveing at us</returns>
        public bool IsSomethingDangerousBeingThrownAtUs()
        {
            foreach (Body body in Parent.BodiesInSensor)
            {
                if (!body.Awake )
                    continue;

                if ((body.PartType == PartType.Rock ||
                    body.IsWeapon ||
                    body.PartType == PartType.Stone ||
                    body.Density > 550  //toes are 500 when on ground..  
                    )
                    && body.LinearVelocity.LengthSquared() > 15)
                {
                    // is body being held by any joints?  
                    // TODO FUTURE handle thrown spirit weapons.. //&&   !PossibleSpiritThreat.HeldBodies.Contains(body))            
                    // TODO what if our friend holding it?
                    if (body.JointList == null)
                    {
                        Vector2 vecBodyToUs = body.WorldCenter - Parent.MainBody.WorldCenter;
                        float factor = Vector2.Dot(body.LinearVelocity - Parent.MainBody.LinearVelocity, vecBodyToUs);
                        if (factor < 0) // negative is at us
                            return true;
                    }
                }
            }
            return false;
        }

        public Spirit GetClosestSpirit(RelativePosition posType, bool isMindedOnly, bool isLiveOnly)
        {
            return GetClosestSpirit(posType, Parent.IsFacingLeft, isMindedOnly, isLiveOnly, RelationShip.None, null, false);
        }

        public Spirit GetClosestSpirit(RelativePosition posType, bool isMindedOnly, bool isLiveOnly, RelationShip relationship)
        {
            return GetClosestSpirit(posType, Parent.IsFacingLeft, isMindedOnly, isLiveOnly, relationship, null, false);
        }

        public Spirit GetClosestSpirit(RelativePosition posType, bool facingLeft, bool isMindedOnly, bool isLiveOnly, RelationShip relationship)
        {
            return GetClosestSpirit(posType, facingLeft, isMindedOnly, isLiveOnly, relationship, null, false);
        }


        public Spirit GetClosestSpirit(RelativePosition posType, bool isMindedOnly, bool isLiveOnly, RelationShip relationship, Spirit enemySpiritOutsideGroup, bool onSameFloor)
        {
            return GetClosestSpirit(posType, Parent.IsFacingLeft, isMindedOnly, isLiveOnly, relationship, enemySpiritOutsideGroup, true);
        }


        /// <summary>
        /// Find closest spirit.
        /// </summary>
        /// <param name="pos">Get spirit in specific direction.</param>
        /// <param name="isLiveAndConsiousOnly">If true, consider only live spirit  </param>
        /// <param name="enemySpiritOutsideGroup">this is  enemy solder for taking a row attack formatino  </param>
        /// <param name="onSameFloor">must be at same level, uses StageHeight prop </param>
        /// <returns>sprit if found or null</returns>
        public Spirit GetClosestSpirit(RelativePosition posType, bool isFacingLeft, bool isMindedOnly, bool isLiveAndConsiousOnly, RelationShip relationship, Spirit enemySpiritOutsideGroup, bool onSameFloor)
        {
            float closestDist = float.MaxValue;
            Spirit otherSpirit = null;

            float distance;
            foreach (Spirit othersp in Parent.SpiritsInSensor)
            {
                distance = DistanceFrom(othersp);

                if (othersp == Parent || othersp == Parent.HeldSpiritUnderControlLeft || othersp == Parent.HeldSpiritUnderControlRight)
                    continue;

                if (onSameFloor && !IsOnSameFloorLevel(othersp, StageHeight))  //got to be on same row. for this.. TODO later pass in param for this bool.. same level
                    continue;

                if (isLiveAndConsiousOnly && (othersp.IsDead || othersp.IsUnconscious))
                    continue;

                if (isMindedOnly)
                {
                    if (!othersp.IsMinded)
                        continue;

                    if (relationship == RelationShip.Friend && !IsFriend(othersp.Mind))
                        continue;

                    if (relationship == RelationShip.Enemy && !Enemies.Contains(othersp.Mind))
                        continue;
                }

                if (posType == RelativePosition.InFront)
                {
                    if (!IsFacingSpirit(isFacingLeft, othersp))
                        continue;
                }
                else
                    if (posType == RelativePosition.Behind)
                    {
                        if (IsFacingSpirit(isFacingLeft, othersp))
                            continue;
                    }

                if (enemySpiritOutsideGroup != null)// for organised bunch of soldier in line ..   follow guy in front of you  ,  
                {
                    if (Parent.IsBetweenUs(enemySpiritOutsideGroup, othersp))
                        continue;

                    if (posType == RelativePosition.Between)
                    {
                        if (!enemySpiritOutsideGroup.IsBetweenUs(othersp, Parent))  // confusing .. but means that we ( This)  are between, enemy and otherguy ( follower.) from enemy viewpoint. So this is the leader or closer to enemy, so dont be returned as a leader if not true.
                            continue;
                    }
                }

                if (distance < closestDist)
                {
                    otherSpirit = othersp;
                    closestDist = distance;
                }
            }

            return otherSpirit;
        }


        public bool IsFacingSpirit(bool leftFacing, Spirit othersp)
        {
            bool spiritToLeft = othersp.WorldCenter.X < Parent.WorldCenter.X;  //use the center of mass..
            return (leftFacing == spiritToLeft);
        }


        /// <summary>
        /// is facing a spirit.   the spirit might not be facing back at us...
        /// </summary>
        /// <param name="othersp"></param>
        /// <returns></returns>
        public bool IsFacingSpirit(Spirit othersp)  
        {
            return IsFacingSpirit(Parent.IsFacingLeft, othersp);
        }



    
      
        public bool IsFollowingSpirit(bool leftFacing, Spirit othersp)
        {
            bool spiritToLeft = othersp.WorldCenter.X < Parent.WorldCenter.X;
            return (leftFacing == spiritToLeft) && (leftFacing == othersp.IsFacingLeft);
        }

        /// <summary>
        /// both must be facing same direction and is behind the other one..    //TODO check this..
        /// </summary>
        /// <param name="othersp"></param>
        /// <returns>true on if following , if false, maybe be leading or facing the other one.. </returns>//TODO check this..
        public bool IsFollowingSpirit( Spirit othersp)
        {
            return IsFollowingSpirit(Parent.IsFacingLeft, othersp);
        }





        public bool IsFacingTarget(Vector2 target)
        {
            return (Parent.IsFacingLeft == Parent.IsLeftOfUs(target));
        }


        /// <summary>
        /// Find closest body  in front of creature, and if must be in front 
        /// </summary>
        public Body GetClosestBody(bool inFrontOnly)
        {
            return GetClosestBody(0, inFrontOnly, null, true);
        }


        /// <summary>
        /// Get closest body.
        /// </summary>
        public Body GetClosestBody()
        {
            return GetClosestBody(0, false, null, true);
        }


        public Body GetClosestBody(List<Body> bodiesToExclude)
        {
            return GetClosestBody(0, false, bodiesToExclude, true);
        }



        /// <summary>
        /// Find closest body of interest  in front of creature, specifiy in minNourishment, and if must be in front 
        /// </summary>
        public Body GetClosestBody(float minNourishment, bool inFrontOnly, List<Body> bodiesToExclude, bool ignoreParticles)
        {
            float closestDist = float.MaxValue;
            Body otherBody = null;

            float minimumNourishment = Math.Abs(minNourishment);

            float distance;
            foreach (Body other in Parent.BodiesInSensor)
            {
                if (inFrontOnly)
                {
                    bool isToLeft = other.WorldCenter.X < Parent.MainBody.WorldCenter.X;
                    if (Parent.IsFacingLeft != isToLeft) //if not facing spirit, skip it.
                        continue;
                }

                if (other.Nourishment < minimumNourishment || !other.IsFoodInAppearance)
                    continue;

                if (bodiesToExclude != null && bodiesToExclude.Contains(other))
                    continue;

                if (ignoreParticles && other is Particle)
                    continue;

                distance = DistancePerceivedFrom(other);

                if (distance < closestDist)
                {
                    otherBody = other;
                    closestDist = distance;
                }

            }
            return otherBody;
        }


#endregion


        /// <summary>
        /// Helper method for linq expression, shadowtool scripts can't use linq extension method.
        /// </summary>
        public static IEnumerable<KeyValuePair<Body, float>> SortBodiesByDistance(Dictionary<Body, float> bodies)
        {
            return bodies.OrderBy(item => item.Value);
        }

        /// <summary>
        /// Sets the state of PossibleMindThread and Girlfriend
        /// </summary>
        /// <returns>true if got new data, false if </returns>
        public bool QuerySpirits()
        {
            if (_hasQueriedSpirits)
                return false;

            List<Spirit> spirits = new List<Spirit>(Parent.Level.GetSpiritEntities());

            if (Level.Instance.ActiveSpirit != null &&!spirits.Contains(Level.Instance.ActiveSpirit) )
            {
                spirits.Add( Level.Instance.ActiveSpirit);
            }

            foreach (Spirit sp in spirits)
            {
                if (sp == null || sp.Mind == null  || sp == Parent)//TODO future how do null spirits get in this list
                    continue;
                    
                if( !Parent.Mind.IsFriend(sp.Mind)
                    && !sp.Name.Contains(gfName)) //the buddy is not considered a threat, it just follows the active spirit
                {
                    Parent.Mind.PossibleMindThreat = sp.Mind;
                    break;
                }


                if (Parent.Name.ToLower() == "namiad" && sp.Name == gfName)                 {
                    BestFriend = sp;
                }

            }

            _hasQueriedSpirits = true;
            return true;
        }



        public Spirit BestFriend; 
        /// <summary>
        /// Query again, for when moving or teleporting
        /// </summary>
        public void ReQuerySpirits()
        {
            _hasQueriedSpirits = false;
            QuerySpirits();
            return;
        }
    }


    public enum RelationShip
    {
        None,
        Friend,
        Enemy
    }

    public enum RelativePosition
    {
        None,
        InFront,        // Facing really now means walk pose direction. 
        Behind,
        Between, //for finding attack leader.. hes between us and them
        // Above,
        // Below,
        //OnLeftHandSide   TODO futue consider that can work on any orientation 
        //OnRightHandSide 

    }

    /// <summary>
    /// Information about the target that is semi automaticaly , automatic, or manually chosen.
    /// Arrow keys , last pressed chose the side of the creature, target desired to attack.. or most hurtfull shot available will be placed here
    /// </summary>
    public struct TargetInfo
    {
        public Vector2 TargetPosition;
        public Spirit Enemy;
        public bool WithinStrikingRange;  //can be reached with weapon
        public bool HeadStraightShotClear;   // clear unblocked shot to head or neck ( with sword)
        public bool HeadHookDownShotClear;   // clear unblocked shot to head or neck ( with sword)    
        public bool HeartStraightShotClear;   // clear unblocked jab or stab to target
        public bool JointStraightShotClear;   // clear unblocked jab or stab to target
        public Body BlockingStraightShotBody;//  to see what kind of cover enemy has.. shield or ground

    //  public bool TaintStraightShotClear;   // clear unblocked jab or stab to target  //TODO differentiate this using metadata or locla pos
    //  public bool JabClear;   // clear unblocked jab or stab to target//TDO separate Hook or Jab.. from Target head or joint  .. allow hook to Joint even for hack off foot in future
    // public bool HookUpClear;  //future TODO.. uppear cut for gills or face punch or arm cut from below
    
    }


}
