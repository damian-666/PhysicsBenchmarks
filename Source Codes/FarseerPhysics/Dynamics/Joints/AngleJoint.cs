using System;
using System.Runtime.Serialization;
using FarseerPhysics.Common;
using Farseer.Xna.Framework;
using System.Diagnostics;


namespace FarseerPhysics.Dynamics.Joints
{
	/// <summary>  
	/// Maintains a fixed angle between two bodies  
    /// </summary>  
    [DataContract(Name = "AngleJoint", Namespace = "http://ShadowPlay", IsReference = true)]
    public class AngleJoint : Joint
    {
        [DataMember]
        public float BiasFactor;

        [DataMember]
        public float MaxImpulse;

        [DataMember]
        public float Softness;

        private float _bias;


        #region ShadowPlay Mods
        // private float _jointError;  moved this to base class 
        #endregion


        private float _massFactor;
        private float _targetAngle;

        public AngleJoint(Body bodyA, Body bodyB)
            : base(bodyA, bodyB)
        {
            JointType = JointType.Angle;
            TargetAngle = 0;
            
            Softness = 0f;
            MaxImpulse = float.MaxValue;

            #region Shadowplay mods: default Bias from 0.2 to 0.3 ( stronger)

            BiasFactor = 0.3f;

            _dampingFactor = 1.0f;

            #endregion

        }

        [DataMember]
        public float TargetAngle
        {
            get { return _targetAngle; }
            set
            {
                if (value != _targetAngle)
                {
                    _targetAngle = value;
                    WakeBodies();
                }
            }
        }

        public override Vector2 WorldAnchorA
        {
            get { return BodyA.Position; }
        }

        public override Vector2 WorldAnchorB
        {
            get { return BodyB.Position; }
            set
            {
                #region ShadowPlay Mods

                // this is to prevent world anchor get deserialized on certain joint
                if (_ondeserializing) 
                    return;

                #endregion

                Debug.WriteLine( "You can't set the world anchor on AngleJoint.");
          
			}
        }

        public override Vector2 GetReactionForce(float inv_dt)
        {

            //TODO
            //return _inv_dt * _impulse;
            return Vector2.Zero;
        }

        public override float GetReactionTorque(float inv_dt)
        {
            return 0;//TODO
        }

        internal override void InitVelocityConstraints(ref TimeStep step)
        {
            _jointError = (BodyB.Sweep.A - BodyA.Sweep.A - TargetAngle);

            _bias = -BiasFactor * step.inv_dt * _jointError;

            _massFactor = (1 - Softness) / (BodyA.InvI + BodyB.InvI);
        }


#region ShadowPlay Mods
        public float GetJointImpulse()
        {
            return Math.Min(Math.Abs(_p), MaxImpulse);
        }

        float _p = 0;

#if !PRODUCTION
        public float JointImpulse
        { 
            get {  return GetJointImpulse();}
        }       
#endif
 
#endregion

        internal override void SolveVelocityConstraints(ref TimeStep step)           
        {

        
          //  float p = (_bias -  BodyB.AngularVelocity +  BodyA.AngularVelocity) * _massFactor; // original
          //  BodyA.AngularVelocity -= BodyA.InvI * Math.Sign(p) * Math.Min(Math.Abs(p), MaxImpulse);
           // BodyB.AngularVelocity += BodyB.InvI * Math.Sign(p) * Math.Min(Math.Abs(p), MaxImpulse);

			#region ShadowPlay Mods

            if (float.IsInfinity(_massFactor))// we also in cloud spirit use a powered joint to connect two fixed rotation bodies just to make it a spirit.  
                return;   //so divide by zero gives this..result, sodont crash
               
            
            //reduce impulse by multple  of AngularVelocity, effectively apply  joint friction.  To reduce bounciness increase _dampingFactor
            //not sure, but guessing p is power used by the joint,  cache it .     TODO this helps on neck, but its not stable with different softness
            _p = (_bias - _dampingFactor * (BodyB.AngularVelocity - BodyA.AngularVelocity)) * _massFactor;

            //TODO see box2d manual.. use powered jiont, with speed = 0 and maxturgue on the revolution joint.
            //should probably at some point remove angle joint.
            // and use motor, max torque, etc..to set a Target position as per manual but bias is fine.. would take forever to retune.
            //hopefully motor speed zero.. and angle joint can work together.. 
            // joint friction you can use a motor setting the maximum torque to some small value and the speed to 0. For reference check page 43 of the box2d manual:

            //http://box2d.org/manual.pdf

            //tried with farseer 3.3  joints
            // address Bouncy Isssue #1.. try damping.. ( action against joint angular speed)
            //Expermient with using box 2d damping:
            //Mixed Results.  This could be due to our mixing of AngleJoint and RevoluteJoint.  if power was implemented fully with Motor maybe it would work.
            //On Neck it was -more- bouncy  whatever setting on MaxMotorTorque.. but  on hips appears to help if set max torque to 200000 and MotorSpeed = 0 ; MotorEnabled = true;.
            //So.. leaving my guessed implementation of DampingFactor  .  using for neck.. and set the MotorEnabled for hip on standing or crouching.. not walking.
            //TODO  maybe retry with 3.5 joints .Revolute is differnet.  
            //or bend knees auto on landing to prevent some bounce , dancing back and forth.

            if ( float.IsNaN(_p))  //TODO check this. new check works but setting to zero doesnt seem to avoid problems, flly balloon in our of view, save it in there to try
                _p = 0;

            BodyA.AngularVelocity -= BodyA.InvI * Math.Sign(_p) * Math.Min(Math.Abs(_p), MaxImpulse);
            BodyB.AngularVelocity += BodyB.InvI * Math.Sign(_p) * Math.Min(Math.Abs(_p), MaxImpulse);
            
            #endregion
        }

        internal override bool SolvePositionConstraints()
        {
            //no position solving for this joint
            return true;
        }


        #region ShadowPlay Mods

        //TODO CODE REVIEW  consider   erase SUHENDRA this seems not called anywere anymore.. is it correct,? what is it supposed to do.
        //saw an earlier comment that  you removed a call to this to fix the head whip around issue on spirit tool..  theres a MathHelper.WrapAngle
        public static double ClampAngle(float angle)
        {

       //     return MathHelper.WrapAngle(BodyB.Rotation - BodyA.Rotation); //this might do what is below 

            if (-System.Math.PI <= angle && angle < System.Math.PI) { return angle; }
            double rem = (angle + System.Math.PI) % (System.Math.PI * 2);
            return rem + ((rem < 0) ? (System.Math.PI) : (-System.Math.PI));
        }



  
        /// <summary>
        /// This is the current effective angle. Always try to get closer to TargetAngle.
        /// </summary>
        public float NewAngle
        {
               get { return BodyB.Rotation - BodyA.Rotation; }
        }


        float _dampingFactor;

        /// <summary>
        /// Torque against angular velocity, Rotational friction (damping)
        /// Warning.  can be unstable if used > 1  with Softness = 0
        /// </summary>
        /// 
        [DataMember]
        public float DampingFactor
        {
            get { return _dampingFactor; }
            set
            {
                _dampingFactor = value;
            }
        }

        #endregion
    }
}