using System;
using System.Diagnostics;
using Farseer.Xna.Framework;

namespace FarseerPhysics.Dynamics.Joints
{
    public class FixedAngleJoint : Joint
    {
        public float BiasFactor;
        public float MaxImpulse;
        public float Softness;
        private float _bias;
 
        #region ShadowPlay Mods
        // private float _jointError;  //moved this to base class 
        #endregion 
  
        
        private float _massFactor;
        private float _targetAngle;

        public FixedAngleJoint(Body bodyA)
            : base(bodyA)
        {
            JointType = JointType.FixedAngle;
            TargetAngle = 0;
            BiasFactor = .2f;
            Softness = 0f;
            MaxImpulse = float.MaxValue;
        }

        public float TargetAngle
        {
            get { return _targetAngle; }
            set
            {
                if (value != _targetAngle)
                {
                    //TODO unwrap this target, so it cannot jump more thant 180 deg  .. see UnWrapAngle commented in MathHelper
                   //see Angle property on Body..  test.. point eye at object moving 360  around..
                    //offset will be calculated this way:  _jointError = BodyA.Sweep.A - TargetAngle,

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
            get { return BodyA.Position; }
            set { Debug.Assert(false, "You can't set the world anchor on this joint type."); }
        }

        public override Vector2 GetReactionForce(float inv_dt)
        {
            //TODO
            //return _inv_dt * _impulse;
            return Vector2.Zero;
        }

        public override float GetReactionTorque(float inv_dt)
        {
            return 0;
        }

        internal override void InitVelocityConstraints(ref TimeStep step)
        {
            _jointError = BodyA.Sweep.A - TargetAngle;

            _bias = -BiasFactor*step.inv_dt*_jointError;

            _massFactor = (1 - Softness)/(BodyA.InvI);
        }

        internal override void SolveVelocityConstraints(ref TimeStep step)
        {
            float p = (_bias - BodyA.AngularVelocity)*_massFactor;
            BodyA.AngularVelocity += BodyA.InvI*Math.Sign(p)*Math.Min(Math.Abs(p), MaxImpulse);
        }

        internal override bool SolvePositionConstraints()
        {
            //no position solving for this joint
            return true;
        }
    }
}