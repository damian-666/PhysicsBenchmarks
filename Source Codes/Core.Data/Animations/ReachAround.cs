using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Animations;
using Core.Data.Entity;
using FarseerPhysics.Dynamics.Joints;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;


namespace Core.Data.Animations
{

    ////Currently not used
    //public class ReachAround : ArmMotionTwoPose
    //{

    //    public ReachAround(Spirit spirit, string name, bool leftArm, int idxShoulder, int idxElbow, int idxWrist)
    //        : base(spirit, name, leftArm, idxShoulder, idxElbow, idxWrist)
    //    {
    //     //   ArmBias = 0.5;   // softer during reach?   maybe harder if this used for punch around..
    //        DrawBackTime = 0.15;  //time to draw arm in before extended punch
    //        ExtendTime = 0.065;  //TODO taken from jab.. might need be slower for this..
    //        RepeatDelay = 0;
    //    }


    //    public override void Update(double dt)
    //    {
    //        base.Update(dt);

    //        Vector2 attackTarget;
    //        //   if (_executePickup)    do a separte LEF for stab / hack down..
    //        //     {
    //        //         attackTarget = _currentPickedItem.WorldPosition;
    //        //       }
    //        //   else
    //        {
    //            attackTarget = Parent.Mind.AttackTarget.TargetPosition;
    //        }

    //        Vector2 targetVector = attackTarget - Parent.Joints[_shoulderindex].WorldAnchorB;

    //        //TODO fix this.. wrist needs to be aligned with the Attach Point Direction..  this was for stabbing..
    //        Vector2 targetVectorWrist = attackTarget - Parent.Joints[_wristindex].WorldAnchorB;

    //        float shoulderAngle;
    //        float wristAngle;

    //        if (Left)
    //        {

    //            //TODO change to using atan aronnd shouder pos i think.   this will be angle from body origin.
    //            // both angle returned by these  functions are always going up (postive , with counter clockwise angle change)
    //          //  shoulderAngle = Parent.MainBody.PositiveAngleToBody( targetVector);  wrong..  angle to 0,0 in body space.  use atan..
    //            //TODO fix this.. wrist needs to be aligned with the Attach Point Direction..  this was for stabbing..
    //            //    wristAngle = Parent.AngleToBody(Parent.GetBodyWithPartType(PartType.LeftLowerArm), targetVectorWrist);
    //        }
    //        else
    //        {
    //            wristAngle = 0;
    //         //   shoulderAngle = Parent.AngleToBody(Parent.MainBody, targetVector);
    //            Body rtLower = Parent.GetBodyWithPartType(PartType.RightLowerArm);
    //            wristAngle = rtLower == null ? 0 : rtLower.PositiveAngleToBody( targetVectorWrist);
    //        }
    //    }


    //    protected override void UpdateOnFirstPose(double dt)
    //    {
    //        //TODO reach above or below it..
    //        //   Parent.TargetFrr.SetTarget(_shoulderindex, -1.1f * _dirfactor);
    //        //}

    //        // bend elbow.. TODO bend less at high angles..
    //        //   if (_executePickup)
    //        {
    //            //       Parent.TargetFrr.SetTarget(_elbowindex, _pickupElbowAngleBend * _dirfactor);
    //        }
    //    }


    //    protected override void UpdateOnSecondPose(double dt)
    //    {
    //        //TODO swing down.. or bend elbow or both..

    //        //   Parent.TargetFrr.SetTarget(_shoulderindex, -1.1f * _dirfactor);
    //        //}

    //        // bend elbow.. TODO bend less at high angles..
    //        //   if (_executePickup)
    //        {
    //            //       Parent.TargetFrr.SetTarget(_elbowindex, _pickupElbowAngleBend * _dirfactor);
    //        }
    //    }

    //}
}
