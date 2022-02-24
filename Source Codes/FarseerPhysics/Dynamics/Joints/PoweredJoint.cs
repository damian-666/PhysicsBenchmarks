/*Copyright 2010 Shadowplay studios, all rights reserved.
 * Contains a class of joint that when position is specified will move under power
 * To this position
 * This Powered Joint can have a break sensor, that when touched, will sever the joint.
 * */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using FarseerPhysics.Common;
using System.ComponentModel;
using FarseerPhysics.Factories;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Dynamics.Contacts;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Particles;
using System.Diagnostics;


namespace FarseerPhysics.Dynamics.Joints
{

   
    //TODO FUTURE there is a lot of separation in the joint.
    //could consider adding a Rope joint in here with dist = 0;  , via Ian advice.. also diff with latest farseer stable release
    //try this on various ropes, etc.. to allow contrainsts to converge, we want some softness in the  joints but around the angle not the hidge


    /// <summary>
    /// This Joint is a combination of RevoluteJoint  ( position constrint and AngleJoint    ( angle contraint)
    /// Its breakable on contact with a sensor.   setting TargetAngle can power the joint.
    /// Set Bias up for strength.  Turn  Softness up for weakness
    /// </summary>
    [DataContract(Name = "PoweredJoint", Namespace = "http://ShadowPlay", IsReference = true)]
    public class PoweredJoint : RevoluteJoint
    {
        #region MemVars & Props

        /// <summary>
        /// Fires when poison on sharp point is injected into joint.
        /// </summary>
        public Action<PoweredJoint, float> PoisonInjected;
        private float _sensorSize;


        /// <summary>
        /// Get or set the radius of BreakSensor fixture. This also create and 
        /// destroy BreakSensor. Set to 0 or below to destroy break sensor and 
        /// also trigger sensor view cleanup.
        /// </summary>
        [DataMember]
        public float SensorSize
        {
            get { return _sensorSize; }
            set
            {
                _sensorSize = value;

                if (_ondeserializing)
                {
                    return;
                }

                ResetBreakSensor();
                NotifyPropertyChanged("SensorSize");
            }
        }


        // Shadowtool only, since we're no longer using break sensor in shadowplay.
#if !SILVERLIGHT
        /// <summary>
        /// This sensor is used for contact detection between this joint and external 
        /// body. Default is NULL, will be non-null if SensorSize > 0. This sensor 
        /// will be rebuilt everytime sensor size changed or deserialized.
        /// </summary>
        public Fixture BreakSensor { get; set; }
#endif

        /*
        public int _breakGroupId = 99;
        /// <summary>
        /// The Group ID in which the Joint will break if Body with the same group Id hit it.
        /// </summary>
        [DataMember]
        public int BreakGroupID
        {
            get { return _breakGroupId; }
            set { _breakGroupId = value; }
        }*/

        public bool HasPower { get; set; }

        public float FoldingJointAngle;

        /// <summary>
        /// Backward compatible Target Angle. in Radians.
        /// </summary>
        [DataMember]
        public float TargetAngle
        {
            get { return _angleJoint.TargetAngle; }
            set
            {
                //TODO  CODE REVIEW .. stiffen joints was tough because of this.. consider other use of HasPower and IsBroken
                //allow TargetAngle state to get changed always
                if (HasPower && !IsBroken)
                {
                    float angle = value;
                    if (LimitEnabled)
                    {
                        angle = MathHelper.Clamp(angle, LowerLimit, this.UpperLimit);
                    }


                    _angleJoint.TargetAngle = angle;
                    NotifyPropertyChanged("TargetAngle");
                }
            }
        }




        /// <summary>
        /// FUTURE Mark the joint so that animation will not record its position or play it back, must be controlled by other means.
        /// 
        /// </summary>
    //    [DataMember]
    //    public  bool SkipRecording { get; set; }

        /// <summary>
        /// Tags that prevents the Graph Walker from traversing this Joint. 
        /// Set when the joint is created at runtime.
        /// </summary>
        public bool IsTemporary { get; set; }

        /// <summary>
        /// Simplified anchor position, interface to world anchor get and local anchor set.
        /// Not serializING this its not part of the model,  Serialize local anchor only.   TODO FUTURE remove Datamemter from base class put in FixedRevolutedJoint  ( BUT THIS DIDNT WORK IN MY TEST)
        /// Note this is fixed in the farseer 3.5 .. clarified.
        /// </summary>
        public override Vector2 WorldAnchorB
        {
            get { return base.WorldAnchorB; }
            set
            {
                if (_ondeserializing || BodyB == null)
                    return;

                Vector2 anchor = BodyB.GetLocalPoint(value);
                LocalAnchorB = anchor;
                LocalAnchorA = BodyA.GetLocalPoint(BodyB.GetWorldPoint(anchor));
            }
        }

        private AngleJoint _angleJoint = null;

        /// <summary>
        /// Angle Joint for this PoweredJoint.   The joints work together as was demo'd in Jensen walker
        /// could use the motor , but adjusting bias and softness and setting the target angle is a good way to achieve powered ragdoll animation.
        /// This is for until I found a more robust solution for PoweredJoint, 
        /// </summary>
        /// 
        [DataMember]
        public AngleJoint AngleJoint
        {
            get { return _angleJoint; }
            set { _angleJoint = value; }    // for deserialization only, do not access.
        }

        private bool _isNumb = false;
        /// <summary>
        /// Set if this joint is Numb or not. 
        /// True - The Joint will have no angle contraint power. 
        /// False - The Joint will have its angle constraint power. 
        /// </summary>
        [DataMember]
        public bool IsNumb
        {
            get { return _isNumb; }
            set
            {
                _isNumb = value;
            }
        }

        protected int _limbSection;
        /// <summary>
        /// Metadata used by animation  tool.
        /// For mirroing behaviors to work,
        /// The Limb section on the left and right sides of a creature need to  match.  
        /// Need to be Set in the prop page by designer.
        /// </summary>
        [DataMember]
        public int LimbSection
        {
            get { return _limbSection; }
            set
            {
                _limbSection = value;
            }
        }


        public bool IsAngleAcute;

        protected PoweredJoint _edgeJointPartner;
        /// <summary>
        /// If not null , this is a "partner joint on foliding rope, stackable self colide...   on NewAngle change, miight disable self then eable this.. and vise versa.
        /// </summary>
        //   [DataMember]   //TODO hope this ok ref..  well check leaks again later.. now clouds leak anyways.  TODO .. limit of this wil be differnt thatn edge joint. i think limits can stay same..
        public PoweredJoint EdgeJointPartner  //to be sense on Balloon  Loaded or creature tool later..
        {
            get { return _edgeJointPartner; }
            set
            {
                _edgeJointPartner = value;

                if (value != null)
                {
                    // when joint partner set, add break handler too
                    Breaking += OnJointBreaking;
                    value.Breaking += OnJointBreaking;
                }
            }
        }

        /// <summary>
        /// This is the current effective angle. Should always try to get closer to TargetAngle.
        /// </summary>

        public float NewAngle
        {
            get { return AngleJoint.NewAngle; }
        }


        /// <summary>
        /// Allows the joint to bend under force
        /// </summary>
        public float Softness
        {
            get { return AngleJoint.Softness; }
            set
            {
                AngleJoint.Softness = value;
            }
        }



        /// <summary>
        /// Torque against angular velocity
        /// </summary>
        public float DampingFactor
        {
            get { return AngleJoint.DampingFactor; }
            set
            {
                AngleJoint.DampingFactor = value;
            }
        }



        static float _curFps; 



        //TODO set rename BiasFactgor prop to a method that takes a gain and or implies will be adjusted

        /// <summary>
        /// sets witthout that stupid gain
        /// </summary>
        public float BiasFactorRaw
        {
            set
            {
                AngleJoint.BiasFactor = value;
            }

            get 
            { 
                return AngleJoint.BiasFactor;
            }
        }

        /// <summary>
        /// Sets the stiffness of the joint, 1 is the max, very springy, 0.3 is the default. Note world DT going up make is more springy, so need to set bias down to compensate
        /// </summary>
        public float BiasFactor
        {
            get { return AngleJoint.BiasFactor; }
            set
            {

                float fps = (1 / World.DT);

#if DEBUG
                if (fps > 86f || fps < 33f)
                {
                    if ( _curFps != fps)
                    {
                        Debug.WriteLine("FPS extreme   86 to 33 , see note. SetBiasForActions Parent.ApplyJointBias in yndrd plugin, also consider other side scenario, such as if creature weak..hungry");//  see SetBiasForActions Parent.ApplyJointBias in yndrd plugin, tune for walk first";
        
                        _curFps = fps;
                    }
               }
               
#endif
                //got this formula after taking a couple data points tuned , putting a linear model , works well fro 35 to 86
                const float basis = 60f;
                float Aparam =-1.9f;  //TODO  tune just a little closer..  going up makes the creatuer weaker, seams.. should be the same.
                float gain = 1 + Aparam * ( 1 - basis / fps);  // a smaller dt make bias need to be smaller.  60 is 0.3bias (gain = 1) 80 is 1.5 ,(gain = 0.5), 100 is 0.1..,  30 is way to weak tho..needs abtu 2x bias
           
                AngleJoint.BiasFactor = value * (gain);

            }
        }




        [DataMember]
        public bool DoRayCastBreakCheck { get; set; }// might need this false for balloon  if packed too tightly

        /// <summary>
        /// Separation distance  in meters between anchor when joint is stretched, 
        /// that is required to perform raycast.
        /// </summary>
        public float MinStretchForRaycast; 

        /// <summary>
        /// When anchors are separated above MinStretchForRaycast, a counter will start 
        /// to count how long it has been separated above the limit. 
        /// If counter passed above this MinCycleForRaycast, raycast will be performed.
        /// Counter is incremented on every update frame as long as it's still above the limit.
        /// Counter might also affected by MaxPositionIterations.
        /// </summary>
        public int MinCycleForRaycast; 

        /// <summary>
        /// In cycles.. 60 is about one second
        /// When raycast performed and reports blocked LOS, a counter will start 
        /// to count how long it has been blocked. 
        /// If counter passed above this BlockedRaycastTimeout, joint will break.
        /// Counter is incremented on every update frame as long as raycast is still blocked.
        /// Might be affected by MaxPositionIterations.
        /// 
        /// </summary>
        public int BlockedRaycastTimeout;

        /// <summary>
        /// Same as BlockedRaycastTimeout, but only applies to object with low density.
        /// </summary>
        public int BlockedByLowDensityTimeout;
        /// <summary>
        /// Same as BlockedRaycastTimeout, but only applies to weapon.  In cycles.. 
        /// </summary>
        public int BlockedByWeaponTimeout;

        /// <summary>
        /// specialy longer timeout  for our players character  In cycles.. 
        /// </summary>
        public int BlockedPlayerCharacterTimeout;
        /// <summary>
        /// specialy longer timeout  for our players character neck  In cycles.. 
        /// </summary>
        public int BlockedPlayerCharacterNeckTimeout;
        /// <summary>
        /// Longer timeout for foot. In cycles.. 
        /// </summary>
        public int BlockedFootTimeout;

        /// <summary>
        /// Counter for over-stretch frame cycle.
        /// </summary>
        private int _stretchCycleCounter;
        /// <summary>
        /// Counter for blocked raycast frame cycle.
        /// </summary>
        private int _blockedRaycastCycleCounter;

        //a body stuck in the joint, just as sword.
        private Body _blockingBody;
        /// <summary>
        /// Minimum sharp point velocity, before calling IsJointBlockedByTunneledWeapon().
        /// From test, velocity when resting under gravity is between 0.2 - 0.5.
        /// </summary>
        public const float MinSharpPointSpeedToCheckForTunnelingSq = 0.7f * 0.7f;

        private const float MinJointErrorSqToCheckForTunneling = 0.02f * 0.02f;
        private const float BulletSensorFactor = 3f;   //make bullets more destructive..
        public const float MinImpulseForBoneBreak = 10f; //same as knockout to head..  //cant use since impulse is not filled on event..
        public const float MinEnergyForBreak = 100f; //experimentation  with bullet.. is kinetic energy = 1/2 mv2

#endregion


#region Constructor
        public PoweredJoint(Body bodyA, Body bodyB, Vector2 worldAnchor) :
            base(bodyA, bodyB, bodyA.GetLocalPoint(ref worldAnchor), bodyB.GetLocalPoint(ref worldAnchor))
        {
            // This will freeze the joint into certain angle joint
            LimitEnabled = false;

            //Disable motor.  Motor is currently used only for damping , setting desired speed to 0.. Angle joint provides power.
            MotorEnabled = false;
            _angleJoint = new AngleJoint(bodyA, bodyB);

            // Set target angle to the angle between bodyA and bodyB, so it won't jumpy
            _angleJoint.TargetAngle = _angleJoint.NewAngle;

            IsTemporary = false;
            HasPower = true;

            EdgeJointPartner = null;

            DoRayCastBreakCheck = true;

            InitCommon();

        }

        // method marked with OnDeserialized attribute cannot be virtual
        [OnDeserialized]
        public new void OnDeserialized(StreamingContext sc)
        {
            HasPower = true;

            // rebuild break sensor
            if (_sensorSize > 0)
            {
                ResetBreakSensor();
            }

            if (_angleJoint == null)
            {
                _angleJoint = new AngleJoint(base.BodyA, base.BodyB);
            }

            //fix for old levels
            if (_angleJoint.DampingFactor == 0)
            {
                _angleJoint.DampingFactor = 1;
            }

            ///TODO todo  ERASE .. old attempt at unwinding in model.. we need to allow rotation to pass 2pi or tons of issues occur.  no point in this
            //// this is to reduce jumpy spirit or rotating cloud that occurred when  BodyA or BodyB rotation 
            //// were clamped to 2pi in Body.OnDeserialized(StreamingContext).
            //LimitEnabled = false;
            //TargetAngle = NewAngle; // can't go above joint limit, that;s why it's disabled first.
            //// NOTE: spirit plugin also changes target angle on each frame, disable that to reduce jumpy when starting level
            ////TargetAngle = 0;      // this still cause jumpy if difference with NewAngle is big
            ////_angleJoint.TargetAngle = 0;  // this still cause jumpy if difference with NewAngle is big
            ////ReferenceAngle = 0;   // should already use proper value from BodA & BodyB, no need to change this..  can unwind this though..
            ///best to make joints between bodies with rotation near zero.. 

            InitCommon();
        }

        /// <summary>
        /// Common initialization that performed similar on both constructor and deserialization.
        /// </summary>
        public void InitCommon()
        {
            _angleJoint.Breaking = OnOurAngleJointBreaking;

            // not .05  because thats a arms width.. also theres is  that thin sword..
            //MaxStretchDistBeforeRaycast = 0.05f;
            MinStretchForRaycast = 0.025f;

            // if too small will affect pickup too, because pickup joint always start stretched.  TODO check this compromise.
            MinCycleForRaycast = 10;

            // larger value means more raycast performed before joint break, can affect performance
            BlockedRaycastTimeout = 30;   //in frames, about 1/3 sec.    
            BlockedByWeaponTimeout = 12;
            // weapon should break quicker than other object.. this is 2 sec.. much too long especailly if held by enemy ant stabbed.
            //its left long for when rolling with own sword,, stepping on sword, etc.

            BlockedPlayerCharacterTimeout = 30;   // have to be blocked  a long time before it breaks..   our own bone is stuck in our joint.  
            BlockedPlayerCharacterNeckTimeout = 200;   // TODO have to be blocked  and ever longer time before it breaks..  since this is fatal ends session

            BlockedFootTimeout = 36;  // be lienint steping on stuff

            BlockedByLowDensityTimeout = 30;  //low density items like leaves can work their way into joints.. need time to squeeze out.. they shoudl never .. but at least wont ruin the session
                                                        // 200 was  tested on debug usually can't go above this value before its worked out


            FoldingJointAngle = 2.95f;

        }

#endregion


#region Methods

        private void ResetBreakSensor()
        {
            // only valid if sensor size > 0
            if (_sensorSize <= 0)
            {
                UnlistenToBonesCollide();
                return;
            }

            ListenToBonesCollide();
        }


        void ValidatePre(float invDT)
        {
            if (!Enabled

           //     || !Settings.IsJointBreakable  //TODO debug the tunneling joint code.
                )
                return;
            // we only need to check broken by stretch here
            // if check stretch distance from here, we might get  position from 
            // previous physics cycle, not current cycle, because position constraint 
            // for current cycle is not yet solved here (see Island.Solve).

            if (!ValidateStretchDistance() )  // want to do this once per frame, not once per iteration.
            {
                // check if allowed to break by event
                if (Breaking != null && Breaking(this) == false)
                    return;

                Enabled = false;
                IsBroken = true;

                if (Broke != null &&  Settings.IsJointBreakable)
                {
                    Broke(this, _jointError);
                }
            }

        }


        internal override void Validate(float invDT)
        {
        //    ValidatePre(invDT);  
            base.Validate(invDT);
            return;
        }


        private void ListenToBonesCollide()
        {
            UnlistenToBonesCollide();// in case we are  already listening , this will prevent build up of listeners

            // for each body we now have a listeners for every joint connected to it..  but its ok see we need to consider near each joint.      

            //TODO check is is this for precollide.. check with bullet.
            BodyA.OnCollision += OnJointBodyCollide;
            BodyB.OnCollision += OnJointBodyCollide;

        }

        private void UnlistenToBonesCollide()
        {
            BodyA.OnCollision -= OnJointBodyCollide;
            BodyB.OnCollision -= OnJointBodyCollide;
        }


        //  ShadowPlay Mod to core physics.  call Update for powered joint once per frame 
        //  even if disabled..
        internal void Update(ref TimeStep step)
        {
            //0 is straight on generated leg ropes.. -3.14 or 3.14 is folded... add stroke or visible joint on edge when on, to rope to look full.    

            ValidatePre(step.inv_dt);
        }

        public void UpdateFoldingState()
        {
            IsAngleAcute = Math.Abs(NewAngle) > FoldingJointAngle;

            if (EdgeJointPartner != null)
            {
                TogglePartner(IsAngleAcute);// if angle is acute.. enable partner..  disable self
                                            //edge angles are not stable on straight rope under tension, but they allow self collide and stacking for a folded rope.             

                //TODO override collide connected.
                CollideConnected = IsAngleAcute;
                AngleJoint.CollideConnected = IsAngleAcute;

                EdgeJointPartner.CollideConnected = IsAngleAcute;
                EdgeJointPartner.AngleJoint.CollideConnected = IsAngleAcute;

                //TODO remove  if farseer bug is fixed.
                // if CollideConnected == false does not work for some reason even if not bullet..
                // they push away.  so we force bullet off.  better for stacking anyways..
                if (CollideConnected)
                {
                    BodyA.IsBullet = false;
                    BodyB.IsBullet = false;
                }
                //if it ever -was- collide connected, bullets can cause self collide to occur, i believe with the disabled joint.

            }
        }


        // this is called once per update.. 
        internal override void InitVelocityConstraints(ref TimeStep step)
        {

            if (Enabled)//wont even be called unless enabled.   but that could change.
            {
                base.InitVelocityConstraints(ref step);

                if (!_isNumb)
                {
                    _angleJoint.InitVelocityConstraints(ref step);
                }
  
                NotifyPropertyChanged("NewAngle");  //this InitVelocityConstraints gets called once per update cycle.. send a notification for the prop page..
                NotifyPropertyChanged("ReactionTorque");  //this InitVelocityConstraints gets called once per update cycle.. send a notification for the prop page..           
                NotifyPropertyChanged("ReactionForce");
                NotifyPropertyChanged("JointImpulse");
                NotifyPropertyChanged("MotorSpeed");

            }
        }

        /// <summary>
        /// for acute bend, turns on edge jiont and off this joint.. and vice versa
        /// </summary>
        /// <param name="toPartner">true for edge joint on</param>
        private void TogglePartner(bool toPartner)
        {
            // note: this function is only executed if joint is enabled.// not true..   with mod to core in ShadowPlay..
            // so this means center joint should always be enabled for this to execute properly.

            Enabled = !toPartner;
            AngleJoint.Enabled = !toPartner;

            EdgeJointPartner.Enabled = toPartner;
            AngleJoint.Enabled = toPartner;
        }

        //after the main loop processing constaints, do a another pass of just powered joints 
        //this extra pass , for joints in a system that have DoExtraVelocityIterations set to true with reduce the joint error without affecting much else
        // this is currenlty used during standing to fix the legs working apart, spreading out after a time
        public bool DoExtraVelocityIterations { get; set; }


        internal override void SolveVelocityConstraints(ref TimeStep step)
        {
            if (Enabled == true)
            {
                base.SolveVelocityConstraints(ref step);

                if (!_isNumb)
                {
                    _angleJoint.SolveVelocityConstraints(ref step);
                }
            }
        }



        internal override bool SolvePositionConstraints()
        {
            bool rj = false;
            bool aj = false;
            //bool stretchOK = false;

            if (Enabled == true)
            {
                rj = base.SolvePositionConstraints();
                aj = _angleJoint.SolvePositionConstraints();

                //// if check stretch distance here after position constraint is solved, 
                //// we get proper position for current physics cycle.
                //// but breaking event is more proper to execute from Validate.  // not sure why ,, as itst converging.. TODO.. verify..
                //stretchOK = ValidateStretchDistance();
            }

            return (Enabled && rj && aj /*&& stretchOK*/);
        }


        public float PosError { get { return( WorldAnchorA - WorldAnchorB).Length(); } }

  

        /// <summary>
        /// Check stretch distance and perform raycast if value above threshold.
        /// If raycast is blocked by object such as weapon for some frame then joint should break.
        /// Return FALSE if joint should break. Return TRUE otherwise.
        /// Joint break by LOS usually make separated body parts fall nearby, similar to break by sharp point.
        /// Unlike break by joint error which cause body parts to explode / flying, because of accumulated force.
        /// </summary>
        private bool ValidateStretchDistance()
        {

            if (!DoRayCastBreakCheck)
                return true;

            // check distance. if below limit return.

            float dist = PosError;


            if (dist <= MinStretchForRaycast)
            {
                _stretchCycleCounter = 0;  //start counters over
                _blockedRaycastCycleCounter = 0;
                return true;
            }

            // check cycle. if below limit return.

            _stretchCycleCounter++;
            if (_stretchCycleCounter <= MinCycleForRaycast)
            {
                _blockedRaycastCycleCounter = 0;
                return true;
            }

            bool isIntersect = false;
            _blockingBody = null;

            isIntersect = RayCastBetweenJointAnchors();

            //System.Diagnostics.Debug.WriteLine(
            //    "Joint LOS check: intersect " + isIntersect.ToString() +
            //    ", stretch cycle: " + _stretchCycleCounter.ToString() +
            //    ", blocked LOS cycle:" + _blockedRaycastCycleCounter.ToString());

            if (isIntersect == false)
            {
                // when LOS is clear, clear all counter, can reduce amount of raycast
                _blockedRaycastCycleCounter = 0;
                _stretchCycleCounter = 0;
                return true;
            }

            _blockedRaycastCycleCounter++;
            return (IsBlockedRaycastShouldBreak(_blockingBody/*, invDT*/) == false);
        }


        private bool RayCastBetweenJointAnchors()
        {
            bool isIntersect = false;
            // raycast. if clear los / non-intersect then return.                
            World.Instance.RayCast((fixture, point, normal, fraction) =>
            {
                if (fixture.Body != BodyA && fixture.Body != BodyB && !fixture.IsSensor
                  //  && fixture.Body.BodyType != BodyType.Static     // ignore static ( ground ) bodies for now..  //TODO on jagged terrain we might want to allow breaking here
                    && fixture.Body.IsNotCollideable == false
                    && (fixture.Body.Info & BodyInfo.Cloud) == 0
                    && !(fixture.Body.IsInfoFlagged( BodyInfo.Bullet) || fixture.Body.Mass < 0.3)  //still seen old small bullets break joints as in neck if lying on ground.. dont do this. they will not remain stuck in joint.
                    && ( fixture.Body.Flags & BodyFlags.DontBreakJointOnBlocking) ==  0
                    )
                {
                    isIntersect = true;
                    _blockingBody = fixture.Body;
                    return fraction;
                }

                // ignore this fixture, could be more stuff in there, keep looking
                return -1.0f;

            }, WorldAnchorA, WorldAnchorB);
            return isIntersect;
        }


        //TODO hack.. REMOVE THIS ..
        private bool IsNeck( Body body)
        {
             return ( body.PartType & PartType.Neck) != 0;
        }

        /// <summary>
        /// Check if timeout occurs on blocked raycast. Return TRUE if timeout, false otherwise.
        /// Different items and joints are treated differently , amount of time it needs to be blocked before breaking.
        /// Some times such as ankle it  happens frequency and resolves itsself.  This is for the case of say .. a bone stuck in an enbow.
        ///  It might take too long to wiggle out, looks wrong cause any cause slow FPS so we break it.    if its only one or two frames we can allow it 
        /// </summary>
        /// 
        private bool IsBlockedRaycastShouldBreak(Body blockingBody/*, float invDT*/)
        {
            if (blockingBody != null)
            {

                //TODO set BlockedRaycastTimeout  based on connected stuff in init BodyInfo.PlayerCharacter)
                //TODO test ,, why breaking neck with hand so easily happening
                // player character take priority
                if ((BodyA.Info & BodyInfo.PlayerCharacter) == BodyInfo.PlayerCharacter)  //is this joint connected to our ynrd, then dont break unless stuck a long time.
                {                                                          // hopefully other body or spirit  will wiggle our or break.

                    if ((blockingBody.Flags & BodyFlags.IsSharpWeaponHeldByAI )!= 0)
                    {   
                        Debug.WriteLine("Player char joint blocked by sharp weapon held by AI:" );  //tunneling happens alot stil, this should not allow AI to pass sword visible into joint it not break  and you not die , happens in level 2, and 3 alot, with longer swords held by bigger AI
                        return true;
                    }

                    if ((blockingBody.Info & BodyInfo.PlayerCharacter) == BodyInfo.PlayerCharacter &&  (IsNeck(BodyA) || IsNeck(BodyB))  )
                    {
                        if (_blockedRaycastCycleCounter > BlockedPlayerCharacterNeckTimeout)
                        {
                            Debug.WriteLine("Player neck blocked by own connected bone, breaking LOS cycle:" + _blockedRaycastCycleCounter.ToString());
                            return true;
                        }

                    }else
                    if (_blockedRaycastCycleCounter > BlockedPlayerCharacterTimeout)
                    {
                        Debug.WriteLine("Player char joint blocked, breaking LOS cycle:" + _blockedRaycastCycleCounter.ToString());
                        return true;
                    }
                }
              // foot next. if this joint connect any other spirit foot, don't break so easily.    toes can break off easily , its ok.
                else if ((BodyA.PartType & PartType.Foot) == PartType.Foot
                    || (BodyB.PartType & PartType.Foot) == PartType.Foot)
                {
                    if (_blockedRaycastCycleCounter > BlockedFootTimeout)
                    {
                        Debug.WriteLine("Foot joint break  blocked by object and TIMEOUT. Blocked LOS cycle:"
                            + _blockedRaycastCycleCounter.ToString());

                        return true;
                    }
                }
                // weapon.
                else if (blockingBody.IsWeapon    //flag is not set.. so its  not being held..
                    /*&& _blockedRaycastCycleCounter > BlockedByWeaponRaycastTimeout*/ )    // don't fall through
                {

                    if ((blockingBody.Flags & BodyFlags.IsSharpWeaponHeldByPC) != 0)
                    {
                        Debug.WriteLine("Player char joint blocked by sharp weapon held by Player:");  //tunneling happens alot stil, this should not allow Player to pass sword through joint and it not break
                        return true;
                    }

                    if (_blockedRaycastCycleCounter > BlockedByWeaponTimeout)
                    {
                        Debug.WriteLine("Joint break blocked by WEAPON object and TIMEOUT. Blocked LOS cycle:"
                            + _blockedRaycastCycleCounter.ToString());

                        return true;
                    }
                }
                // density. dont specially becuase its easy for low density things like stems or leaves to tunnel into feel or other parts.
                else if (blockingBody.Density <= 80      // current balloon has density=80, but door 60.
                    /*&& _blockedRaycastCycleCounter > BlockedByLowDensityRaycastTimeout*/)     // don't fall through
                {
                    if (_blockedRaycastCycleCounter > BlockedByLowDensityTimeout)
                    {
                        Debug.WriteLine("Joint break blocked by LOW DENSITY object and TIMEOUT. Blocked LOS cycle:"
                            + _blockedRaycastCycleCounter.ToString());

                        return true;
                    }
                }

                //// from test, not happen often
                //else if (hitBody.PartType == PartType.Container)
                //{
                //}

                //// from test, usually door can cause balloon break only if door fully opened.
                //else if (hitBody.PartType == PartType.Door)
                //{
                //}
                // general object last
                else if (_blockedRaycastCycleCounter > BlockedRaycastTimeout)
                {
                    Debug.WriteLine("Joint break blocked by object and TIMEOUT. Blocked LOS cycle:"
                        + _blockedRaycastCycleCounter.ToString());

                    if (blockingBody.IsStatic)
                    {
                        Debug.WriteLine("Player char joint blocked by static body");
                    }

                    return true;
                }
            }

            return false;
        }


        /// <summary>
        ///Use back trace of tip linear trajectory to see if intersect line between joint Anchors, might have otherwise be undetected by  tunneling through sensor.
        /// </summary>
        private bool IsJointCutByTunneledSharpPoint(Vector2 sharpPoint, Vector2 sharpVel)
        {
            Debug.Assert(World.DT != 0);

            float extensionFactorTime = 2;//sometimes one frame is not enough.. lets do more to be sure.  we are drawing line back to handle .. or ins case of pulling sword out ( should be rare) from tip out through neck.   

            //TODO this is only valid then body  is more than DL old
            Vector2 prevSharpPoint = sharpPoint - sharpVel * extensionFactorTime / World.DT;
            // when joint raycast blocked, WorldAnchorA and WorldAnchorB will be separate.
            // create line between current and previous position of sharp point.
            // create line between WorldAnchorA and WorldAnchorB.
            // finally check line intersect between sharp point movement and 2 world anchor.
            // if true (intersect), then sharp point really thrusting, intent to break joint. 
            // which means weapon tunneled.
            Vector2 dummy;

#if DEBUG
         //   Debug.WriteLine("Dist streched " + (WorldAnchorB - WorldAnchorA).Length().ToString());
#endif

            if ((WorldAnchorB - WorldAnchorA).LengthSquared() < MinJointErrorSqToCheckForTunneling)
                return false;

            // extend line a bit.. in case path is not straight
            // since we have a min dist MinJointErrorSqToCheckForTunneling.. no worries about bent angles breaking 
            //unless point carries a very direct straight path .. this line will be short and it wont intersect.
            //Note .. need to be carefull here.. when neck is near ground.. or under pressure might be alot of error transverse to the nect line.
            //two long here an extension factor and sword clearly sliding past neck may cut it.. looks wrong
            //Tried doing this only when joint is blocked by weapon body via raycast, but my then its too late.. would need to go back earlier frames
            // for a complete solution .. only cut next when sword tip has stabbed though it.
            // not when pressing against or other .. more joint iterations will help in future on gen3 creature
            float betweenJointLineExtensionFactor = 1.2f;   //was 1  saw tunneling .. was 3 .. trying  1.2, 1.4 still tunneling.  this cuts too often testing on level 2.  now that we have blocking body test do no need these extension   higher values can mean wrong cuts.  At least own head weapon is excluded.  test rolling with sword

            //TOOD dont do this if head  in contact with groudn.. ( too much error)  or it own sword?

            //or if error not in direction of neck?  ( stretch) 

            Vector2 extension = (WorldAnchorB - WorldAnchorA) * betweenJointLineExtensionFactor;
            return LineTools.LineIntersect2(sharpPoint, prevSharpPoint, WorldAnchorA - extension, WorldAnchorB + extension, out dummy);
        }


        public override void ResetStateForTransferBetweenPhysicsWorld()
        {
            base.ResetStateForTransferBetweenPhysicsWorld();
            _angleJoint.ResetStateForTransferBetweenPhysicsWorld();

            if (_edgeJointPartner != null)
            {
                _edgeJointPartner.ResetStateForTransferBetweenPhysicsWorld();
            }
            // re-listen to bone collide, because fixture should also rebuilt
            UnlistenToBonesCollide();
            ListenToBonesCollide();
        }


        public float GetJointImpulse()
        {
            return _angleJoint.GetJointImpulse();
        }

        /// <summary>
        /// set either upper or lower joint limit based on current NewAngle.
        /// if NewAngle is closer to UpperLimit then UpperLimit will be set.
        /// otherwise LowerLimit will be set.
        /// </summary>
        public void SetClosestJointLimitFromNewAngle()
        {
            float diff1 = Math.Abs(NewAngle - UpperLimit);
            float diff2 = Math.Abs(NewAngle - LowerLimit);
            if (diff1 < diff2)
            {
                UpperLimit = NewAngle;
            }
            else
            {
                LowerLimit = NewAngle;
            }
        }

#endregion

#region Event Methods

        /// <summary>
        /// Called when either bone collide with external body.
        /// </summary>
        private bool OnJointBodyCollide(Fixture fixtureA, Fixture fixtureB, Contact ctlist)
        {

            try
            {
                // fixtureA is either BodyA or BodyB fixture 
                // fixtureB is always from external object.  according to Farseer documentation on OnCollision
                // for powered joint, WorldAnchorA and WorldAnchorB should be on same location, except when stretched.


                Body strikingBody = fixtureB.Body;
                Body boneBody = fixtureA.Body;

                bool affectsJoint = false;
                float effectiveSensorSize = SensorSize;

                //TODO consider to put mark points here..  do head collide here as well.
                //i think we dont have impulse or correct contact infoinfo on this event so bruises might not be good here, put scars ok
                if ((strikingBody.Info & BodyInfo.Bullet) != 0)  //bullets are handled else onCollide on contacted.. dont need to check for tunneling.  issue is falling on bullets.. can cut neck off.. not realistic
                    return true;

                //TODO if head or neck on groudn.. consider more robust cut test.. 
                //line allong neck not just two anchors would make sense .. sucks to lose head when it should not happen
                //TODO test gen3 .. see if joint are tighter

                if (strikingBody.SharpPoints != null)
                {
                    // Find the sharp points of the colliding body 

                    foreach (SharpPoint sharpPoint in strikingBody.SharpPoints)
                    {
                        Vector2 sharpPointWorld;
                        //The idea here is that the sharp point must be pressed hard enough to displace the bones and penetrate.
                        //so the actually collide location is used.. however for bullets... they bounce, and CCD may not mean that the world location is the same as the collide location.
                        //TODO might need to do this for certain type of sword.. check max impulse..we still see somtimes sword enter neck and does not cut.
                        sharpPointWorld = sharpPoint.WorldPosition;

                        Vector2 relativeSharpVel = GetRelativeVelocity(strikingBody, sharpPoint);

                        // if sharp point is outside sensor range. (simply touching with any weapon part will fulfill this)
                        if (Vector2.Distance(sharpPointWorld, WorldAnchorB) > effectiveSensorSize)
                        {

                            bool tunneling = false;
                            //    Vector2 sharpPos = sharpPoint.WorldPosition;
                            //    Vector2 sharpToJoint = WorldAnchorB - sharpPos;
                            //    sharpToJoint.Normalize();
                            //    float speedTowardBone = Vector2.Dot(relativeSharpVel, sharpToJoint); // this is not good because wiht hight separatino
                            //its moviing away from one and towards the other.. also on jump.
                            //need to find vel in direction of sword..  Ideally .. for now just inore toes and feet.. jumpoing on sword should not cut fe            

                            if (relativeSharpVel.LengthSquared() > MinSharpPointSpeedToCheckForTunnelingSq
                                && (boneBody.PartType & (PartType.Foot | PartType.Toe | PartType.Shin)) == 0
                                 &&
                                 !(((boneBody.Info & BodyInfo.PlayerCharacter) != 0) && (strikingBody.Flags & BodyFlags.IsSharpWeaponHeldByPC) != 0)) // NOT it is our own sworld held by us, helps allow rolling , falling with sword safely
                            {
                                // NOTE: this will called often  only when object containing a sharp point is colliding with joint bone and moving fast
                                if (IsJointCutByTunneledSharpPoint(sharpPoint.WorldPosition, relativeSharpVel))
                                {
                                    Debug.WriteLine("Joint break TUNNEL prevention. sharp point track intersect line projected between joint anchors.");
                                    tunneling = true;
                                }
                                //TODO future
                                /*       else   ///it it struck hard enough to create a wound?
                                       {
                                           Vector2 normal;
                                           FixedArray2<Vector2> points;
                                           ctlist.GetWorldManifold(out normal, out points);
                                
                                           for (int i = 0; i < 2; i++)
                                           {
                                               if ((sharpPoint.WorldPosition - points[i]).LengthSquared() < 0.01f)
                                               {
                                                   //check it normal is  perpendicular.. impulse or rel vel..   maybe not need since   MinSharpPointVelocitySqToCheckForTunneling    passed
                                                   Debug.WriteLine("bone contact points " + points[i].ToString());

                                                   Vector2 contactPoint =points[i];
                                                   boneBody.EmitterPoints.Add ( new Emitter( boneBody, boneBody.GetLocalPoint( ref contactPoint)));

                                                   //TODO put a emitter with a view and drop a fiew drops of blood or skin..
                                                   //TODO place at intersection of shape edge and line from point ot body cm.. or allong dir of sword tip motion..

                                               }
                                           }
                                       
                                       }   */
                            }

                            if (!tunneling)
                                continue;
                        }
                        else
                        {                        // if reach here then it's a valid hit
                    // if there are poison injection, then don't break the joint.
                            affectsJoint = HandleDamage(affectsJoint, sharpPoint, relativeSharpVel);
                        }
                    }
                }

                if (Settings.IsJointBreakable)// && fixtureB.Body.BreakGroupID == this.BreakGroupID)
                {
                    //TODO check laser 
                    if (strikingBody.Acidity < -2)
                    {
                        strikingBody.UpdateAABB();
                        //is it close enough to joint to melt it?  
                        affectsJoint =
                            Vector2.Distance(strikingBody.WorldCenter, this.WorldAnchorB) <
                            (this.SensorSize + Math.Max(strikingBody.AABB.Height, strikingBody.AABB.Width));
                    }

                    // check if allowed to break by event, only when sharp points is collided with break sensor or laser or acid
                    if (affectsJoint && Break())
                    {
                        UnlistenToBonesCollide();
                    }
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine(" exc in joint collide " + exc.Message);
            }

            return true;
        }

        private bool HandleDamage(bool bBreakJoint, SharpPoint sharpPoint, Vector2 relVel)
        {
            if (sharpPoint.PoisonInjection > 0)
            {
                // trigger poison event here.
                if (PoisonInjected != null)
                {
                    PoisonInjected(this, sharpPoint.PoisonInjection);
                }
            }
            else if (Settings.IsJointBreakable)   // if no poison then this joint can be broken.
            {
                if ( (sharpPoint.Parent.Info & BodyInfo.Bullet)!= 0 && relVel.LengthSquared() < MinSharpPointSpeedToCheckForTunnelingSq)
                     return false;

                bBreakJoint = true;
            }
            return bBreakJoint;
        }

        private Vector2 GetRelativeVelocity(Body strikingBody, SharpPoint sharpPoint)
        {
            // only if sharp point moving fast
            Vector2 sharpVelocity = strikingBody.GetLinearVelocityFromLocalPoint(sharpPoint.LocalPosition);
            // relative  vel//  body b and a ends are about the same at this point
            Vector2 jointVel = BodyB.GetLinearVelocityFromWorldPoint(sharpPoint.WorldPosition);
            Vector2 relativeSharpVel = sharpVelocity - jointVel;
            return relativeSharpVel;
        }

        /// <summary>
        /// Called when subjoint (angle joint) is about to broken.
        /// </summary>
        private bool OnOurAngleJointBreaking(Joint joint)
        {
            if (_angleJoint != joint)
                return true;

            // check if allowed to break by event.
            // no need to check IsBreakable, subjoint should have checked that.
            if (Breaking != null && Breaking(this) )
            {
                // here joint break because subjoint break
                Enabled = false;
                IsBroken = true;
                UnlistenToBonesCollide();
            }
            return true;
        }


        public float GetJointAngleError()
        {
            return Math.Abs(NewAngle - TargetAngle);  //TODO winding?
        }

        // called for this and partner joint
        protected bool OnJointBreaking(Joint joint)
        {
            // from this joint, break joint partner
            if (joint == this)
            {
                if (EdgeJointPartner != null)
                {
                    EdgeJointPartner.Enabled = false;
                    EdgeJointPartner.IsBroken = true;

                    if (EdgeJointPartner.Broke != null)
                    {
                        EdgeJointPartner.Broke(EdgeJointPartner, _jointError);
                    }
                }
            }
            else    // from joint partner, break that joint
            {
                Enabled = false;
                IsBroken = true;

                if (Broke != null)
                {
                    Broke(EdgeJointPartner, _jointError);
                }
            }

            return true;
        }
        
        /// <summary>
        /// use maxTorque  to set a damping and described in box2d manual.   does not work well with high bias joints tho.
        /// </summary>
        /// <param name="maxTorque"></param>
        public void SetMotorDamping(float maxTorque)
        {
            if (maxTorque > 0)
            {
                //notes seems to make neck more bouncy.. but works on hips in biped..  using DampingFactor for hips.
                MaxMotorTorque = maxTorque;
                MotorEnabled = true;
                MotorSpeed = 0;
            }
            else
            {
                MotorEnabled = false;   
                MaxMotorTorque = 0;

            }
        }


#endregion


#region IDisposable Members

        //TODO CODE REVIEW FUTURE.. call when unload level .. i logged an issue for this.
        //listeners cause leaks.
        public void ReleaseListeners()
        {
            UnlistenToBonesCollide();
        }

#endregion
    }
}
