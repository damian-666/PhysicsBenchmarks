using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;
using Farseer.Xna.Framework;


namespace Core.Data.Animations
{
    public class StraightJabOrGrab: ArmMotionTwoPose
    {

        /// <summary>
        /// TODO FUTURE.. consider  the jab code  moving to here from plugin..
        /// </summary>
        /// <param name="spirit"></param>
        /// <param name="leftArm"></param>
        /// <param name="idxShoulder"></param>
        /// <param name="idxElbow"></param>
        /// <param name="idxWrist"></param>

        protected StraightJabOrGrab(Spirit spirit, string name,  bool leftArm, int idxShoulder, int idxElbow, int idxWrist)
            : base(spirit, name, leftArm, idxShoulder, idxElbow, idxWrist)
        {

        }

        public override void Update(double dt)
        {
            base.Update(dt);
            //TODO this might not be practicle since we dont know the plugin parent... but maybe it could..

            /*
            Vector2 attackTarget;
            if (_executePickup)
            {
                attackTarget = _currentPickedItem.WorldPosition;
            }
            else
            {
                attackTarget = Parent.Mind.AttackTarget;
            }

            Vector2 targetVector = attackTarget - Parent.Joints[shoulderindex].WorldAnchorB;
            Vector2 targetVectorWrist = attackTarget - Parent.Joints[wristindex].WorldAnchorB;

            float shoulderAngle;
            float wristAngle;

            if (left)
            {
                // both angle returned by these  functions are always going up (postive , with counter clockwise angle change)
                shoulderAngle = Parent.PositiveAngleToBody(Parent.MainBody, targetVector);
                wristAngle = Parent.AngleToBody(Parent.GetBodyWithPartType(PartType.LeftLowerArm), targetVectorWrist);
            }
            else
            {
                shoulderAngle = Parent.AngleToBody(Parent.MainBody, targetVector);
                wristAngle = Parent.PositiveAngleToBody(Parent.GetBodyWithPartType(PartType.RightLowerArm), targetVectorWrist);
            }

   
            //to get this i punch at enemy.  measure the error looking at the joint prop, and subtract it.
            float calibrationOffset = left ? (float)Math.PI * 2 - 1.9f : -1.2f;// TODO precisely calibrate more.
            float calibrationOffset2 = left ? -1.9f : (float)Math.PI * 2.1f - 1.1f;// TODO precisely calibrate more.

            // readying to stab
            if (elapsedMilis < _punchDrawBackTime)
            {
                SetNormalShoulderLimits();
                //if (_executeStoop)
                //{
                //    // keep straight when stoop pickup, same as stab angle,
                //    Parent.TargetFrr.SetTarget(shoulderindex, -shoulderAngle + calibrationOffset);
                //    //Parent.OsoftestFrr.SetOffset(shoulderindex, -shoulderAngle + calibrationOffset);
                //}
                //else
                //{
                // for punch or normal pickup, move shoulder inside 
                Parent.TargetFrr.SetTarget(shoulderindex, -1.1f * dirfactor);
                //}

                // bend elbow.. TODO bend less at high angles..
                if (_executePickup)
                {
                    Parent.TargetFrr.SetTarget(elbowindex, _pickupElbowAngleBend * dirfactor);
                }
                else
                {
                    Parent.TargetFrr.SetTarget(elbowindex, 2.9f * dirfactor);
                }
                //   Trace.TraceInformation("wristAngle:" + wristAngle );
            }
            // stab
            else
            {
                //for some reason angle TARGET of shoulder is negative counter clockwise, on both sides .  so its -shoulderAngle
                Parent.TargetFrr.SetTarget(shoulderindex, -shoulderAngle + calibrationOffset);     // stretch outside
                if (_executePickup)
                {
                    Parent.TargetFrr.SetTarget(elbowindex, _pickupElbowAngleExtended * dirfactor);
                }
                else
                {
                    Parent.TargetFrr.SetTarget(elbowindex, 0.15f * dirfactor);   // straighten elbow  //TODO CODE REVIEW.. use _elbowAngleStraight to cache this 0.15f value... its repeated
                }
                Parent.OsoftestFrr.SetOffset(shoulderindex, 0);

                //     Parent.TargetFilter.SetTarget(wristindex, stabWristAngle);  //straight
                //  Trace.TraceInformation("wristAngle:" + wristAngle );

                if (!IsSquatting)
                {
                    BendAtHipsForPunch(0.35f * dirfactor);
                }
            }

            //always aim hand at target
            Parent.TargetFrr.SetTarget(wristindex, -wristAngle + calibrationOffset2);//* dirfactor); 

            //TODO maybe look at elbox , release when straight instead of 0.02;
            if (_executeThrow && _thrustCurrentTime > _punchDrawBackTime + _punchThrowTime - 0.03)
            {
                   PartType handPart = Parent.IsFacingLeft ? PartType.LeftHand : PartType.RightHand;
                Parent.Detach(handPart, PartType.None, false);
                _executeThrow = false;
            }
             */


        }



        protected override void UpdateOnFirstPose(double dt)
        {/*
            SetNormalShoulderLimits();
            //if (_executeStoop)
            //{
            //    // keep straight when stoop pickup, same as stab angle,
            //    Parent.TargetFrr.SetTarget(shoulderindex, -shoulderAngle + calibrationOffset);
            //    //Parent.OsoftestFrr.SetOffset(shoulderindex, -shoulderAngle + calibrationOffset);
            //}
            //else
            //{
            // for punch or normal pickup, move shoulder inside 
            Parent.TargetFrr.SetTarget(shoulderindex, -1.1f * dirfactor);
            //}

            // bend elbow.. TODO bend less at high angles..
            if (_executePickup)
            {
                Parent.TargetFrr.SetTarget(elbowindex, _pickupElbowAngleBend * dirfactor);
            }
            else
            {
                Parent.TargetFrr.SetTarget(elbowindex, 2.9f * dirfactor);
            }
            //   Trace.TraceInformation("wristAngle:" + wristAngle );
          * */
        }

        protected override void UpdateOnSecondPose(double dt)
        {
            /*
                        //for some reason angle TARGET of shoulder is negative counter clockwise, on both sides .  so its -shoulderAngle
                        Parent.TargetFrr.SetTarget(shoulderindex, -shoulderAngle + calibrationOffset);     // stretch outside
                        if (_executePickup)
                        {
                            Parent.TargetFrr.SetTarget(elbowindex, _pickupElbowAngleExtended * dirfactor);
                        }
                        else
                        {
                            Parent.TargetFrr.SetTarget(elbowindex, 0.15f * dirfactor);   // straighten elbow  //TODO CODE REVIEW.. use _elbowAngleStraight to cache this 0.15f value... its repeated
                        }
                        Parent.OsoftestFrr.SetOffset(shoulderindex, 0);

                        //     Parent.TargetFilter.SetTarget(wristindex, stabWristAngle);  //straight
                        //  Trace.TraceInformation("wristAngle:" + wristAngle );

                        if (!IsSquatting)
                        {
                            BendAtHipsForPunch(0.35f * dirfactor);
                        } 
             */
        }
    }
}
