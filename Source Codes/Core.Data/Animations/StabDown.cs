//#define BENDELBOW
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics;

using Core.Data.Animations;
using Core.Data.Entity;



namespace Core.Data.Animations
{        
    /// <summary>
    /// Hook down down or hack down stab.  On first pose, rotate it 45-90 degree from actual target (todo, not yet implemented). 
    /// On second pose just swing shoulder down to target, change elbow so fist will strick head, but wrist always aim to target.
    /// </summary>
    public class StabDown : ArmMotionTwoPose
    {
        private float _lowerArmLength;  // assume same for both left & right
        public TargetInfo TargetInfo;

        public delegate bool CanExecutePoseDelegate();
        public CanExecutePoseDelegate CanExecuteSecondFramePose;  // if delegate returns false, second pose won't get executed

        //so can be adjusted in script
        public float ElbowExtendedtAngle;
        public float ShoulderRaiseAngle;
        public float ElbowRaiseAngle;

        private float _elbowAngle2ndPose;


        public StabDown(TargetInfo targetInfo, Spirit spirit, string name, float lowerArmLength, bool leftArm, int idxShoulder, int idxElbow, int idxWrist)
            : base(spirit, name, leftArm, idxShoulder, idxElbow, idxWrist)
        {
            // longer draw time for better aiming, stabilize wrist angle before thrust
            DrawBackTime = 0.35;   //TODO maybe check when newangle is at target also..  
            
            ExtendTime = 0.5;   // longer extend time for solid thrust

            // TODO: should be able to have repeated stab later. 
            // either stop when yndrd keypress stopped, or add parameter for end time, repeat number.
            RepeatDelay = 0;
            TargetInfo = targetInfo;
            _lowerArmLength = lowerArmLength;     

            ElbowExtendedtAngle = 0.15f ;
            ShoulderRaiseAngle = 2.0f ;  // this angle was measured from tool  .. raise arm  high without hitting face.
          
            //ElbowRaiseAngle = 1.4f ; // this angle was measured from tool  .. raise arm  high without hitting face.
            ElbowRaiseAngle = 0.25f; //  raise arm  high without hitting face. //TODO adjust amount to raise arm.. and can bend elbow more.. 
            //or limit this with shoulder .. only when raising from below horizontal..

#if BENDELBOW  // TODO this is useful on punch down only if elected to do hook punch, even with jab clear.
            // otherwise its better to get around lower obstacles to either keep a straight arm, allow elbow  to bend backwards ( and using same angle from formula but negative
          //
            // ..   Tested on stabDownTestsquatHeadAtFeet
            // and hookPunchStandingAbove..
            // could  use the Event onFirstPose, since client knows _lowerArmLength, and knows GetStraightElbow angle         
            
            //TODO use a max angle here .. does not work well on stab wont lower knife enough.
            float shoulderToTargetDistance = Vector2.Distance(_targetInfo.TargetPosition, Parent.Joints[_shoulderindex].WorldAnchorA);

            //TODO check the _dirfactor*ElbowExtendedtAngle .. not sure if the right sign.
            _elbowAngle2ndPose = _dirfactor*ElbowExtendedtAngle + _dirfactor * 2f * (float)Math.Asin((0.5 * shoulderToTargetDistance) / _lowerArmLength);  //This formula assumes lowerArm is the sameAsupperArm..   (//TODO check if straigth..
             //TODO assumes 0 is straight elbow, ishould be - ElbowExtendedtAngle.    probably close enough
#else
            _elbowAngle2ndPose = _dirfactor + ElbowExtendedtAngle; 
#endif            
        }


        // this should applicable for both front and rear grabber
        protected override void UpdateOnFirstPose(double dt)
        {
            //TODO FUTUREconsider bending middle neck forward.. lower nect back far (fold neck)  to lower head tucked in by elbow.  then can bend more and use more shoulder.
            //TODO   . bend elbow in alot  ( new 1st pose)  while forearm is less that out from shoulder.   extend while raising hand ( 2nd pose as in here)   this is most efficient and natural looking, requires least energy to raise weapon
            //TODO FUTURE .. consider bending elbow backward past -.15 for punch, may looks unatural
                  
            Parent.TargetFilter.SetTarget(_shoulderindex, ShoulderRaiseAngle * _dirfactor);  // this angle was measured from tool  .. raise arm  high without hitting face.
            //Parent.TargetFrr.SetTarget(_elbowindex, ElbowRaiseAngle* _dirfactor);// meaured from tool 
            //base.Update(dt);  ?
        }


        protected override void UpdateOnSecondPose(double dt)
        {
            // must check on every update, because los might be blocked in middle of animation.
            // might be less costly if we only check los on beginning of second pose.
            // if null means second pose always allowed.
            if (CanExecuteSecondFramePose != null && CanExecuteSecondFramePose() == false)
            {
                //Debug.WriteLine("second pose was blocked."); //TODO  future move this check to base class 
                //   TODO FUTURE cast send clear ray tip weapon in plugin on handler, avoid stabing our foot if raised  
                return;
            }

            //Debug.WriteLine("Second pose, Left=" + IsLeftArm.ToString());

            Parent.TargetFilter.SetTarget(_shoulderindex, 0.0f);  //TODO calculate this angle so it wound go so far as to hit ourself throw target
            Parent.TargetFilter.SetTarget(_elbowindex, _elbowAngle2ndPose );  

            //TODO  something with hip angle.  if hip high.. then keep arm straight or elbow will hit the knee.  also elbow limits are bad and take effect here.
            //TODO dont squat if holding long weapon and target lying down, no need to.

            // TODO: when STANDING and raising hand readying for stab down, front foot tend to raise, and its getting in the way,
            // sometimes cause yndrd cut its own foot. 

            //plugin onUpdate now handles 
            
        }


    }
}
