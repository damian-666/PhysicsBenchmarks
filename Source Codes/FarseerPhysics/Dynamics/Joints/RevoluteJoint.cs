/*
* Farseer Physics Engine based on Box2D.XNA port:
* Copyright (c) 2010 Ian Qvist
* 
* Box2D.XNA port of Box2D:
* Copyright (c) 2009 Brandon Furtwangler, Nathan Furtwangler
*
* Original source Box2D:
* Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using FarseerPhysics.Common;
using Farseer.Xna.Framework;

namespace FarseerPhysics.Dynamics.Joints
{
    /// <summary>
    /// A revolute joint rains to bodies to share a common point while they
    /// are free to rotate about the point. The relative rotation about the shared
    /// point is the joint angle. You can limit the relative rotation with
    /// a joint limit that specifies a lower and upper angle. You can use a motor
    /// to drive the relative rotation about the shared point. A maximum motor torque
    /// is provided so that infinite forces are not generated.
    /// </summary>
    [DataContract(Name = "RevoluteJoint", Namespace = "http://ShadowPlay", IsReference = true)]
    [KnownType(typeof(PoweredJoint))]
    public class RevoluteJoint : Joint
    {
        [DataMember(Order = 0)]
        public Vector2 LocalAnchorA;
        
        [DataMember(Order = 1)]
        public Vector2 LocalAnchorB;

        private bool _enableLimit;
        private bool _enableMotor;
        private Vector3 _impulse;
        private LimitState _limitState;
        private float _lowerAngle;
        private Mat33 _mass; // effective mass for point-to-point constraint.
        private float _maxMotorTorque;
        private float _motorImpulse;
        private float _motorMass; // effective mass for motor/limit angular constraint.
        private float _motorSpeed;
        private float _referenceAngle;
        private float _tmpFloat1;
        private Vector2 _tmpVector1, _tmpVector2;
        private float _upperAngle;


 

        /// <summary>
        /// Initialize the bodies and local anchor.
        /// This requires defining an
        /// anchor point where the bodies are joined. The definition
        /// uses local anchor points so that the initial configuration
        /// can violate the constraint slightly. You also need to
        /// specify the initial relative angle for joint limits. This
        /// helps when saving and loading a game.
        /// The local anchor points are measured from the body's origin
        /// rather than the center of mass because:
        /// 1. you might not know where the center of mass will be.
        /// 2. if you add/remove shapes from a body and recompute the mass,
        /// the joints will be broken.
        /// </summary>
        /// <param name="bodyA">The first body.</param>
        /// <param name="bodyB">The second body.</param>
        /// <param name="localAnchorA">The first body anchor.</param>
        /// <param name="localAnchorB">The second anchor.</param>
        public RevoluteJoint(Body bodyA, Body bodyB, Vector2 localAnchorA, Vector2 localAnchorB)
            : base(bodyA, bodyB)
        {
            JointType = JointType.Revolute;

            // Changed to local coordinates.
            LocalAnchorA = localAnchorA;
            LocalAnchorB = localAnchorB;

            ReferenceAngle = BodyB.Rotation - BodyA.Rotation;

            _impulse = Vector3.Zero;

            _limitState = LimitState.Inactive;
        }

        public override Vector2 WorldAnchorA
        {
            get { return BodyA.GetWorldPoint(LocalAnchorA); }
        }

        public override Vector2 WorldAnchorB
        {
            get { return BodyB.GetWorldPoint(LocalAnchorB); }
            set
            {   

                #region ShadowPlay Mods  
	             // this is to prevent world anchor get deserialized on some joint  
                //TODO FUTURE..  remove this and remove  [datamember]  from WorldAnchorB in base..
                //World anchor should  be serialized only on FixedRevoluteJoint, (tool anchor) 
                //its should  be marked there [datamember] there, and not on base class.
                //however that way anchor drifts on save reload, and need  to retain backward compatibility 
                //NOTE  does not seem that Datameter works on derived class 
	            if (_ondeserializing)   
                    return;

                #endregion
                Debug.Assert(false, "You can't set the world anchor on this joint type.");            
            }
        }


#if DEBUG  //tool only 
        #region Shadowplay Mods
           //props with get; set are supposed to be slower according to Ian Qvist blog..dont want to change it, so adding Prop
     
        
        /// <summary>
        ///  The LocalAnchorA X for this joint, in body coordinates
        /// </summary>
        public  float LocalAnchorA_X
        {
            get { return LocalAnchorA.X; }
            set { LocalAnchorA.X = value; }
        }

        /// <summary>
        ///  The LocalAnchorA Y for this joint, in body coordinates
        /// </summary>
        public float LocalAnchorA_Y
        {
            get { return LocalAnchorA.Y; }
            set { LocalAnchorA.Y = value; }
        }

        /// <summary>
        ///  The LocalAnchorB X for this joint, in body coordinates
        /// </summary>
        public float LocalAnchorB_X
        {
            get { return LocalAnchorB.X; }
            set { LocalAnchorB.X = value; }
        }

        /// <summary>
        ///  The LocalAnchorB Y for this joint, in body coordinates
        /// </summary>
        public float LocalAnchorB_Y
        {
            get { return LocalAnchorB.Y; }
            set { LocalAnchorB.Y = value; }
        }

        #endregion
#endif 




        [DataMember(Order = 2)] 
        public float ReferenceAngle
        {
            get { return _referenceAngle; }
            set
            {
                WakeBodies();
                _referenceAngle = value;
                NotifyPropertyChanged("ReferenceAngle");
            }
        }

        /// <summary>
        /// Get the current joint angle in radians.
        /// </summary>
        /// <value></value>
        public float JointAngle
        {
            get { return BodyB.Sweep.A - BodyA.Sweep.A - ReferenceAngle; }
        }

        /// <summary>
        /// Get the current joint angle speed in radians per second.
        /// </summary>
        /// <value></value>
        public float JointSpeed
        {
            get { return BodyB.AngularVelocityInternal - BodyA.AngularVelocityInternal; }
        }

        /// <summary>
        /// Is the joint limit enabled?
        /// </summary>
        /// <value><c>true</c> if [limit enabled]; otherwise, <c>false</c>.</value>
        [DataMember(Order = 99)]
        public bool LimitEnabled
        {
            get { return _enableLimit; }
            set
            {
#region ShadowPlay Mods  
                //don't wake bodies or reset unless value will actually change
                if (_enableLimit == value)
                    return;
#endregion
                WakeBodies();
                _enableLimit = value;
                
                NotifyPropertyChanged("LimitEnabled");  // must after _enableLimit value changed or UI give improper value
            }
        }

        /// <summary>
        /// Get the lower joint limit in radians.
        /// </summary>
        /// <value></value>
        [DataMember(Order = 99)]
        public float LowerLimit
        {
            get { return _lowerAngle; }
            set
            {

                #region ShadowPlay Mods  
                //don't wake bodies or reset unless value will actually change
                if (_lowerAngle == value)
                    return;
               
               // WakeBodies(); TODO revisit in spirit plugin since limits are set in base call.. then narrowed,  
                //stuff is woken every cycle.  for now .. just dont wake when limit changes

                #endregion
                _lowerAngle = value;

                NotifyPropertyChanged("LowerLimit");    // so result display immediately when changed by Apply Limit on ribbon
            }
        }

        /// <summary>
        /// Get the upper joint limit in radians.
        /// </summary>
        /// <value></value>
        [DataMember(Order = 99)]
        public float UpperLimit
        {
            get { return _upperAngle; }
            set
            {
                #region ShadowPlay Mods  
                //don't wake bodies or reset unless value will actually change
                if (_upperAngle == value)
                    return;
                #endregion

                // WakeBodies(); TODO revisit in spirit plugin. since limits are set in base call.. then narrowed,  
                //stuff is woken every cycle.  for now .. just dont wake when limit changes
                _upperAngle = value;

                NotifyPropertyChanged("UpperLimit");
            }
        }

        /// <summary>
        /// Is the joint motor enabled?
        /// </summary>
        /// <value><c>true</c> if [motor enabled]; otherwise, <c>false</c>.</value>


        [DataMember]
        public bool MotorEnabled
        {
            get { return _enableMotor; }
            set
            {
                if (value != _enableMotor)
                {
                    WakeBodies();
                    _enableMotor = value;
                    NotifyPropertyChanged("MotorEnabled");
                }
            }
        }

        /// <summary>
        /// Set the motor speed in radians per second.  Note : set to zero, and set Motor Enalbed true and max motor torque for damping
        /// </summary>
        /// <value>The speed.</value>
        [DataMember]
        public float MotorSpeed
        {
            set
            {
                if (_motorSpeed != value)
                {
                    WakeBodies();
                    _motorSpeed = value;
                    NotifyPropertyChanged("MotorSpeed");
                }

            }
            get { return _motorSpeed; }
        }

        /// <summary>
        /// Set the maximum motor torque, usually in N-m.
        /// </summary>
        /// <value>The torque.</value>
         [DataMember]
        public float MaxMotorTorque
        {
            set
            {
                WakeBodies();
                _maxMotorTorque = value;
                NotifyPropertyChanged("MaxMotorTorque");
            }
            get { return _maxMotorTorque; }
        }

        /// <summary>
        /// Get the current motor torque, usually in N-m.
        /// </summary>
        /// <value></value>
        public float MotorTorque
        {
            get { return _motorImpulse; }
            set
            {
                WakeBodies();
                _motorImpulse = value;
                NotifyPropertyChanged("MotorTorque");

            }
        }

        public override Vector2 GetReactionForce(float inv_dt)
        {
            Vector2 P = new Vector2(_impulse.X, _impulse.Y);
            return inv_dt*P;
        }

        public override float GetReactionTorque(float inv_dt)
        {
          //  return inv_dt*_impulse.Z;   this is always zero.
            return _motorImpulse* inv_dt;  // Shadowplay Mod this seems to be closer.. aligns with MaxMotorTorque on damping need to test with leverage tho..  tested with mass on spring.
        }




//NOTE .. first try just on position.. see effect on changing mass to I... and tyr in formulas..

//realize.. these are all solved toghet.. if whole rope wher adjusted outside the frame.. then back.. might give
//better results.. we do this in plugin for feet to strengthen jonts.

//so plugin handles the error,, it checks especialll at contait point from the roap to the balloon..

//of posit is good, angle should be good..
//fixing collide connected on bullet might be imporatnat its seeds an  errorl..
//might check latest far seer..

//in strong winds blocking could go off in strained ( to avoid that pressue) , and bulllet off,
//or using a joint to keep oposing joint from crossing... the anchor can keep right and left apart and it can be placed 
//earier.  the balloon cna be force to unstick..that way using rod joints... also , we crank up the interations,
//puting contraisnts after, and put the physics fPS up
 

     //   float jointMassF = 1; //to strengthen joint without adding gravity.. just change the effect of mass and moment here.
        //   float OriginMassA;  //put it back after the whole loop?  mark certain joints to be strengthened.. see if mixed with the contact..
        //  float OriginMassB;  // run balloon over hill adn see if light contacts are ok.. consider when the ship falls on balloon , then raise its mass.
        // using the TotalForce thing...
        //private void ApplyJointStrenghthenWorkaround()
        //{

        //    //better way would be cache this. Orig Mass.. or the MassData.. put it back without notification, level listens to mass changed to update the cm.  
        //  could remove this because it is updated every spirit update anyways
        //    if (BodyA.PartType == PartType.None || BodyA.PartType == PartType.Rope || BodyB.PartType == PartType.None ||
        //        BodyB.PartType == PartType.Rope)
        //    {
        //        jointMassF = 2f;
        //        // jointMassF = 1f;  //TODO  to try making joints strong this way, but has side effects, updates CM for spirit.. don't need that
        //    }
        //    else
        //    {
        //        jointMassF = 1.0f;  //don't , this causes updates to CM..even on small change
        //    }
        //}




        internal override void InitVelocityConstraints(ref TimeStep step)
        {
            Body b1 = BodyA;
            Body b2 = BodyB;

            //  ApplyJointStrenghthenWorkaround();  //SHADOWPLAY MOD STRENGTHENJOINT

        //    b1.Mass = b1.Mass * jointMassF;
        //    b2.Mass = b2.Mass * jointMassF;

            if (_enableMotor || _enableLimit)
            {
                // You cannot create a rotation limit between bodies that
                // both have fixed rotation.

                if (!(b1.InvI > 0.0f || b2.InvI > 0.0f))
                    return;
              //  Debug.Assert(b1.InvI > 0.0f || b2.InvI > 0.0f);
            }

            // Compute the effective mass matrix.
            /*Transform xf1, xf2;
            b1.GetTransform(out xf1);
            b2.GetTransform(out xf2);*/

            Vector2 r1 = MathUtils.Multiply(ref b1.Xf.R, LocalAnchorA - b1.LocalCenter);
            Vector2 r2 = MathUtils.Multiply(ref b2.Xf.R, LocalAnchorB - b2.LocalCenter);

            // J = [-I -r1_skew I r2_skew]
            //     [ 0       -1 0       1]
            // r_skew = [-ry; rx]

            // Mat lab
            // K = [ m1+r1y^2*i1+m2+r2y^2*i2,  -r1y*i1*r1x-r2y*i2*r2x,          -r1y*i1-r2y*i2]
            //     [  -r1y*i1*r1x-r2y*i2*r2x, m1+r1x^2*i1+m2+r2x^2*i2,           r1x*i1+r2x*i2]
            //     [          -r1y*i1-r2y*i2,           r1x*i1+r2x*i2,                   i1+i2]

            float m1 = b1.InvMass, m2 = b2.InvMass;   //SHADOWPLAY MOD TODO RETRY APPLYING JUST HERE..   BUT CAREFUL WITH THE ANGULAR.. DONT DO IT AT ALL, RECALC? // STRENGTHENJOINT
            float i1 = b1.InvI, i2 = b2.InvI;

            _mass.Col1.X = m1 + m2 + r1.Y * r1.Y * i1 + r2.Y * r2.Y * i2;
            _mass.Col2.X = -r1.Y * r1.X * i1 - r2.Y * r2.X * i2;
            _mass.Col3.X = -r1.Y * i1 - r2.Y * i2;
            _mass.Col1.Y = _mass.Col2.X;
            _mass.Col2.Y = m1 + m2 + r1.X * r1.X * i1 + r2.X * r2.X * i2;
            _mass.Col3.Y = r1.X * i1 + r2.X * i2;
            _mass.Col1.Z = _mass.Col3.X;
            _mass.Col2.Z = _mass.Col3.Y;
            _mass.Col3.Z = i1 + i2;

            _motorMass = i1 + i2;
            if (_motorMass > 0.0f)
            {
                _motorMass = 1.0f / _motorMass;
            }

            if (_enableMotor == false)
            {
                _motorImpulse = 0.0f;
            }

            if (_enableLimit)
            {
                float jointAngle = b2.Sweep.A - b1.Sweep.A - ReferenceAngle;
                if (Math.Abs(_upperAngle - _lowerAngle) < 2.0f * Settings.AngularSlop)
                {
                    _limitState = LimitState.Equal;
                }
                else if (jointAngle <= _lowerAngle)
                {
                    if (_limitState != LimitState.AtLower)
                    {
                        _impulse.Z = 0.0f;
                    }
                    _limitState = LimitState.AtLower;
                }
                else if (jointAngle >= _upperAngle)
                {
                    if (_limitState != LimitState.AtUpper)
                    {
                        _impulse.Z = 0.0f;
                    }
                    _limitState = LimitState.AtUpper;
                }
                else
                {
                    _limitState = LimitState.Inactive;
                    _impulse.Z = 0.0f;
                }
            }
            else
            {
                _limitState = LimitState.Inactive;
            }

            if (Settings.EnableWarmstarting)
            {
                // Scale impulses to support a variable time step.
                _impulse *= step.dtRatio;
                _motorImpulse *= step.dtRatio;

                Vector2 P = new Vector2(_impulse.X, _impulse.Y);

                b1.LinearVelocityInternal -= m1 * P;
                MathUtils.Cross(ref r1, ref P, out _tmpFloat1);
                b1.AngularVelocityInternal -= i1 * ( /* r1 x P */_tmpFloat1 + _motorImpulse + _impulse.Z);

                b2.LinearVelocityInternal += m2 * P;
                MathUtils.Cross(ref r2, ref P, out _tmpFloat1);
                b2.AngularVelocityInternal += i2 * ( /* r2 x P */_tmpFloat1 + _motorImpulse + _impulse.Z);
            }
            else
            {
                _impulse = Vector3.Zero;
                _motorImpulse = 0.0f;
            }


            ////    b1.Mass = b1.Mass / jointMassF; STRENGTHENJOINT   adjusting model .. to be avoided..
        //    b2.Mass = b2.Mass / jointMassF;
        }

       


        const float _minError = 0.01f;
        internal override void SolveVelocityConstraints(ref TimeStep step)
        {
            Body b1 = BodyA;
            Body b2 = BodyB;

          //  ApplyJointStrenghthenWorkaround();

        //    b1.Mass = b1.Mass * jointMassF;// STRENGTHENJOINT   //TODO see the effect on InvI  on a rode or rope piece.  Adjusting Model... to be avoided..
        //    b2.Mass = b2.Mass * jointMassF;



       //     float strainAdjustment = 1.0f;

            Vector2 v1 = b1.LinearVelocityInternal;
            float w1 = b1.AngularVelocityInternal;
            Vector2 v2 = b2.LinearVelocityInternal;
            float w2 = b2.AngularVelocityInternal;


			float m1 = b1.InvMass, m2 = b2.InvMass;
          
          //  float m1 = b1.InvMass* strainAdjustment, m2 = b2.InvMass* strainAdjustment; // STRENGTHENJOINT  not needed if adjusting Model
			//float i1 = b1.InvI* strainAdjustment, i2 = b2.InvI* strainAdjustment;// STRENGTHENJOINT

			float i1 = b1.InvI, i2 = b2.InvI;

            // Solve motor constraint.
            if (_enableMotor && _limitState != LimitState.Equal)
            {
                float Cdot = w2 - w1 - _motorSpeed;
                float impulse = _motorMass*(-Cdot);
                float oldImpulse = _motorImpulse;
                float maxImpulse = step.dt*_maxMotorTorque;
                _motorImpulse = MathUtils.Clamp(_motorImpulse + impulse, -maxImpulse, maxImpulse);
                impulse = _motorImpulse - oldImpulse;

                w1 -= i1*impulse;
                w2 += i2*impulse;
            }

            // Solve limit constraint.
            if (_enableLimit && _limitState != LimitState.Inactive)
            {
                /*Transform xf1, xf2;
                b1.GetTransform(out xf1);
                b2.GetTransform(out xf2);*/

                Vector2 r1 = MathUtils.Multiply(ref b1.Xf.R, LocalAnchorA - b1.LocalCenter);
                Vector2 r2 = MathUtils.Multiply(ref b2.Xf.R, LocalAnchorB - b2.LocalCenter);

                // Solve point-to-point constraint
                MathUtils.Cross(w2, ref r2, out _tmpVector2);
                MathUtils.Cross(w1, ref r1, out _tmpVector1);
                Vector2 Cdot1 = v2 + /* w2 x r2 */ _tmpVector2 - v1 - /* w1 x r1 */ _tmpVector1;
                float Cdot2 = w2 - w1;
                Vector3 Cdot = new Vector3(Cdot1.X, Cdot1.Y, Cdot2);

                Vector3 impulse = _mass.Solve33(-Cdot);

                if (_limitState == LimitState.Equal)
                {
                    _impulse += impulse;
                }
                else if (_limitState == LimitState.AtLower)
                {
                    float newImpulse = _impulse.Z + impulse.Z;
                    if (newImpulse < 0.0f)
                    {
                        Vector2 reduced = _mass.Solve22(-Cdot1);
                        impulse.X = reduced.X;
                        impulse.Y = reduced.Y;
                        impulse.Z = -_impulse.Z;
                        _impulse.X += reduced.X;
                        _impulse.Y += reduced.Y;
                        _impulse.Z = 0.0f;
                    }
                }
                else if (_limitState == LimitState.AtUpper)
                {
                    float newImpulse = _impulse.Z + impulse.Z;
                    if (newImpulse > 0.0f)
                    {
                        Vector2 reduced = _mass.Solve22(-Cdot1);
                        impulse.X = reduced.X;
                        impulse.Y = reduced.Y;
                        impulse.Z = -_impulse.Z;
                        _impulse.X += reduced.X;
                        _impulse.Y += reduced.Y;
                        _impulse.Z = 0.0f;
                    }
                }

                Vector2 P = new Vector2(impulse.X, impulse.Y);

                v1 -= m1*P;
                MathUtils.Cross(ref r1, ref P, out _tmpFloat1);
                w1 -= i1*( /* r1 x P */_tmpFloat1 + impulse.Z);

                v2 += m2*P;
                MathUtils.Cross(ref r2, ref P, out _tmpFloat1);
                w2 += i2*( /* r2 x P */_tmpFloat1 + impulse.Z);
            }
            else
            {
                /*Transform xf1, xf2;
                b1.GetTransform(out xf1);
                b2.GetTransform(out xf2);*/

                _tmpVector1 = LocalAnchorA - b1.LocalCenter;
                _tmpVector2 = LocalAnchorB - b2.LocalCenter;
                Vector2 r1 = MathUtils.Multiply(ref b1.Xf.R, ref _tmpVector1);
                Vector2 r2 = MathUtils.Multiply(ref b2.Xf.R, ref _tmpVector2);

                // Solve point-to-point constraint
                MathUtils.Cross(w2, ref r2, out _tmpVector2);
                MathUtils.Cross(w1, ref r1, out _tmpVector1);
                Vector2 Cdot = v2 + /* w2 x r2 */ _tmpVector2 - v1 - /* w1 x r1 */ _tmpVector1;
                Vector2 impulse = _mass.Solve22(-Cdot);

                _impulse.X += impulse.X;
                _impulse.Y += impulse.Y;

                v1 -= m1*impulse;
                MathUtils.Cross(ref r1, ref impulse, out _tmpFloat1);
                w1 -= i1* /* r1 x impulse */_tmpFloat1;

                v2 += m2*impulse;
                MathUtils.Cross(ref r2, ref impulse, out _tmpFloat1);
                w2 += i2* /* r2 x impulse */_tmpFloat1;
            }

            b1.LinearVelocityInternal = v1;
            b1.AngularVelocityInternal = w1;
            b2.LinearVelocityInternal = v2;
            b2.AngularVelocityInternal = w2;


            //   b1.Mass = b1.Mass / jointMassF; STRENGTHENJOINT
            //   b2.Mass = b2.Mass / jointMassF;
        }

        float positionError { get; set; }    //shadowplay mod..pos made public..TODO temporary.. to compare on convergence.
        internal override bool SolvePositionConstraints()
        {
            // TODO_ERIN block solve with limit. COME ON ERIN

            Body b1 = BodyA;
            Body b2 = BodyB;


            //  ApplyJointStrenghthenWorkaround();

            //     b1.Mass = b1.Mass * jointMassF; STRENGTHENJOINT   FIRST TRY JUST THIS AND JUST LINEAR NOT MOMENT
            //     b2.Mass = b2.Mass * jointMassF;



            float angularError = 0.0f;
           //    float positionError;   //shadowplay mod..pos made public..

            // Solve angular limit constraint.
            if (_enableLimit && _limitState != LimitState.Inactive)
            {
                float angle = b2.Sweep.A - b1.Sweep.A - ReferenceAngle; 
                float limitImpulse = 0.0f;

                if (_limitState == LimitState.Equal)
                {
                    // Prevent large angular corrections
                    float C = MathUtils.Clamp(angle - _lowerAngle, -Settings.MaxAngularCorrection,
                                              Settings.MaxAngularCorrection);
                    limitImpulse = -_motorMass*C;
                    angularError = Math.Abs(C);
                }
                else if (_limitState == LimitState.AtLower)
                {
                    float C = angle - _lowerAngle;
                    angularError = -C;

                    // Prevent large angular corrections and allow some slop.
                    C = MathUtils.Clamp(C + Settings.AngularSlop, -Settings.MaxAngularCorrection, 0.0f);
                    limitImpulse = -_motorMass*C;
                }
                else if (_limitState == LimitState.AtUpper)
                {
                    float C = angle - _upperAngle;
                    angularError = C;

                    // Prevent large angular corrections and allow some slop.
                    C = MathUtils.Clamp(C - Settings.AngularSlop, 0.0f, Settings.MaxAngularCorrection);
                    limitImpulse = -_motorMass*C;
                }

                b1.Sweep.A -= b1.InvI*limitImpulse;
                b2.Sweep.A += b2.InvI*limitImpulse;

                b1.SynchronizeTransform();
                b2.SynchronizeTransform();
            }

            // Solve point-to-point constraint.
            {
                /*Transform xf1, xf2;
                b1.GetTransform(out xf1);
                b2.GetTransform(out xf2);*/

                Vector2 r1 = MathUtils.Multiply(ref b1.Xf.R, LocalAnchorA - b1.LocalCenter);
                Vector2 r2 = MathUtils.Multiply(ref b2.Xf.R, LocalAnchorB - b2.LocalCenter);

                Vector2 C = b2.Sweep.C + r2 - b1.Sweep.C - r1;
                positionError = C.Length();

            //    float strainAdjustment = 1.0f;

                float invMass1 = b1.InvMass, invMass2 = b2.InvMass;
           //     float invMass1 = b1.InvMass * strainAdjustment, invMass2 = b2.InvMass * strainAdjustment; //STRENGTHENJOINT
                
  				float invI1 = b1.InvI , invI2 = b2.InvI ;
   			//	float invI1 =  b1.InvI* strainAdjustment,  invI2 = b2.InvI * strainAdjustment; //STRENGTHENJOINT

                // Handle large detachment.
                const float k_allowedStretch = 10.0f*Settings.LinearSlop;
                if (C.LengthSquared() > k_allowedStretch*k_allowedStretch)
                {
                    // Use a particle solution (no rotation).
                    Vector2 u = C;
                    u.Normalize();
                    float k = invMass1 + invMass2;

                    //shadow play mod, not needed, for giant planets joints work fine
            //        Debug.Assert(k > Settings.Epsilon);
                    float m = 1.0f/k;
                    Vector2 impulse2 = m*(-C);
                    const float k_beta = 0.5f;
                    b1.Sweep.C -= k_beta*invMass1*impulse2;
                    b2.Sweep.C += k_beta*invMass2*impulse2;

                    C = b2.Sweep.C + r2 - b1.Sweep.C - r1;
                }

                Mat22 K1 = new Mat22(new Vector2(invMass1 + invMass2, 0.0f), new Vector2(0.0f, invMass1 + invMass2));
                Mat22 K2 = new Mat22(new Vector2(invI1*r1.Y*r1.Y, -invI1*r1.X*r1.Y),
                                     new Vector2(-invI1*r1.X*r1.Y, invI1*r1.X*r1.X));
                Mat22 K3 = new Mat22(new Vector2(invI2*r2.Y*r2.Y, -invI2*r2.X*r2.Y),
                                     new Vector2(-invI2*r2.X*r2.Y, invI2*r2.X*r2.X));

                Mat22 Ka;
                Mat22.Add(ref K1, ref K2, out Ka);

                Mat22 K;
                Mat22.Add(ref Ka, ref K3, out K);


                Vector2 impulse = K.Solve(-C);

              //  b1.Sweep.C -= invMass1 * impulse;//STRENGTHENJOINT   if we just leave the model unchanged and tweak the invMass1 copy here..didnt work the firsst tye
                b1.Sweep.C -= b1.InvMass*impulse;  //original farseer


                MathUtils.Cross(ref r1, ref impulse, out _tmpFloat1);
                b1.Sweep.A -= b1.InvI* /* r1 x impulse */_tmpFloat1;

               // b2.Sweep.C += invMass2 * impulse;//STRENGTHENJOINT

                b2.Sweep.C += b2.InvMass*impulse;

                MathUtils.Cross(ref r2, ref impulse, out _tmpFloat1);



               // b2.Sweep.A += invI2 * /* r2 x impulse */_tmpFloat1;//STRENGTHENJOINT  if we just leave the model unchanged  (TODO test.. see if we can just change this..they are the same..
                b2.Sweep.A += b2.InvI* /* r2 x impulse */_tmpFloat1;


                b1.SynchronizeTransform();
                b2.SynchronizeTransform();
            }

            //     b1.Mass = b1.Mass / jointMassF;  //STRENGTHENJOINT    first consider changing this outside.. affect consitions also..
            //     b2.Mass = b2.Mass / jointMassF;                          //but if balloon panel will collide head , should reduce the mass... should be light weight..note try panels with density 20, but for joints 80..

            return positionError <= Settings.LinearSlop && angularError <= Settings.AngularSlop;
        }


        #region ShadowPlay Mods


        //TODO CODE REVIEW .. is this the Farseer way to do this..?  check if breakable was added
        //this gets called every velocity constraint iteration.. seems it should get called once per update

        // I think this was copied from base Joint.Validate(), so it should be farseer way.
        // the main thing occurs here is to check if _jointError < breakpoint. same as in base.
        // -DC.   //TODO.. move to update.    Joints break unexpectedly,
        //could be as its converging on a solution.. should not use early values..  maybe this is to protest against wild reaction though.. just  break the joint before it destabilizes the solver..
        //TODO  unless its extreme, say double the breakpoint, lets use check this once per frame, at the end of the loop...

        internal override void Validate(float invDT)
        {


            _jointError = GetReactionForce(invDT).Length();


            if (Enabled == false || Settings.IsJointBreakable == false)
            {
                return;
            }


            float bp = Breakpoint;

            // when warmstarting is disabled, joint will be harder to break, 
            // because no impulse accumulated from previous frame.
            // reduce breakpoint limit to compensate.
            if (Settings.EnableWarmstarting == false) bp *= 0.3f;


         //   ApplyJointStrenghthenWorkaround();
       


            //note ORIGINAL PLAY WAS TO FIX IF NEVER CONVERGES FAST ENOUGH TO GETS SOLVED, BUT THAT IS TOO LATE, NEED TO TUNE IT..STRENGTHENJOINT
            // float gain = 2f;
            //  if ( BodyA.PartType == PartType.Rope
            //     ||    BodyB.PartType == PartType.Rope)
            //|| BodyA.PartType == PartType.None)  //TODO do something about latches, hooks , special end points, and grabbers like arms.
         //   {
                //   Debug.WriteLine("_err - bp " + error.ToString("f2") + " e:" + GetReactionForce(invDT).ToString());
                //  if ( BodyB.GetNumJointsConnected(true)  ==1 )  //probably the endpoint.   NOTE.. if climbing a rope with hand-holds.. its will have more joints from attach points
             //   {
                    //TODO.. record total impulse on body?
                    //Debug.WriteLine("BodyB.TotalContactForceOnThis" + BodyB.TotalContactForce.ToString("F2"));

             //   }

               // if (BodyA.GetNumJointsConnected(true) == 1)  //probably the endpoint.   NOTE.. if climbing a rope with hand-holds.. its will have more joints from attach points
            //    {
                    //TODO.. record total impulse on body?
                  //  Debug.WriteLine("BodyA.TotalContactForceOnThis" + BodyA.TotalContactForce.ToString("F2"));

            //    }

          //  }

         //   error = (Math.Abs(_jointError) / jointMassF - bp); //STRENGTHENJOINT,  MAKE IT NO BREAK SO EASILY


         
			 float error = (Math.Abs(_jointError) - bp);     // NOTE this error might not be the stretch.. see the  pos contrainct or the strech.. difference in anchor..

            if (error <= 0 )
            {
                return;
            }



            // check if allowed to break by event
            if (Breaking != null && Breaking(this) == false)
            {
                return;
            }

            Enabled = false;
            IsBroken = true;

            #region ShadowPlay Mods:
            System.Diagnostics.Debug.WriteLine("RevoluteJoint Broken: " + _jointError);
            #endregion


            if (Broke != null)
            {
                Broke(this, _jointError);
            }
        }

        #endregion
    }
}