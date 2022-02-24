//#define TIGHTENJOINTEXPERIMENT// this is also in YNRD plugin.. but cannot affect ais during tuning becuase its a directed class and a technical
//limitation.  the Level could have a plugin..that could do this for all...control a controller..


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Common;

using Core.Data;
using FarseerPhysics.Collision;
using System.Diagnostics;


namespace Core.Data.Entity
{
    /// <summary>
    /// Helper to iterate Spirits, update all plugins, etc
    /// Should be accessible 
    /// </summary>
    public class SpiritHelper
    {

        /// <summary>
        /// lear to climb and pass
        /// 
        /// first ID the cycle of hands and feet, grabbers   .. 
        /// see the grabber query code or use the marks, dont have to walk graph for this, too hard  though might be useful for skinning.. wed mark Joints with flags like the body
        /// we want a joint and a body, the grabber..
        /// 
        /// TODO stick bodyflags on joints to help ..can query or map the joints w flags   each flag cobo is uniqu
        /// 
        /// Map<BodyFlags, PoweredJoint></BodyFlags>
        /// 
        /// start w hand.. walk to main body on joint graph, mark and map the joint then
        /// 
        /// now we have a circuit
        /// 
        /// build the circularqueue or array to them
        /// 
        /// 
        /// start by reaching out up or down ( might need to bend for lower pickup , but we have lower hands now) 
        /// with grabber furthest toward DESIRED goal.. furhter handhold.. ground claw hold ( feet will be grabbers too, add a attack Pt to them)
        /// this is fold climb and walk better
        /// 
        /// then grab pull and or Pass to next thing in circuit.. how to interpolate the path or meet halfway.. that is TBD
        /// 
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="userData"></param>
        /// 

        //maybe use indices   crawl left = 1,2,3    or  crawl right =  6 5 4 for pattern, roll = 1,2,3,4,5,6,7,1 or backwards
        //pass it arround.. we can roll , but usually start at head, skip it, pick a mirror side, and pass hand to hand to foot and out the back to crawl or climb, passing is all but feet, mabye jaw..
        //bite is a 0 leading 0, we lead with a bite.
        static PartType[] partTypesCycle = new PartType[] { PartType.LowerJaw, PartType.LeftUpperHand, PartType.LeftLowerHand, PartType.LeftToe, PartType.RightToe, PartType.RightLowerHand, PartType.RightUpperHand };


        Body[] Grabbers = new Body[partTypesCycle.Length];


        Dictionary<AttachPoint, int> mapGrabbersToJointIdx = new Dictionary<AttachPoint, int>();

        //    Dictionary<PartType, int> mapGrabbersToJointIdx = new Dictionary<PartType, int>();

        //TODO if breaks, Toe to Foot, maybe?? jaw if hand broken? skip broken or subs, like RightFoot for toe, mabye we are going claws so doubt we sub except mabye teh jaw which can bite a hold
        //TODO give creature a proper hook beak


        //TODO should used the Joint angle..redo all this passing  with climb, claw, bite crawl, pass code
        //TODO design effects so that we can move hards toward each other, on paths
        //move hand on path would be nice, record angles, play back
        // animation tool that can blend would be ideal, partial animations.. see Dwi code on that



        //1 for walk, on foot near ground get the grabber on the toe and put a joint
        //implement grab for when attachpoints get near targets like ground, using existing like walk
        //try loosen limb on the grab, it can go out, leg can extend, set a tolerance or find the minimum

        //add a claw type attachpt, that shoots a ray and uses it to find the thing to stick too..

        //try this with handes too  , it its hits somethign with an attach pt, it moves the wrist to line up.fr


        //2 implement 4 arm pickup and  pass around, with elbow bend or distance to hand, full extend or less..  combine this with roll , climb


        //see the existing attachpoint pair thing, handle angles and such


        /// <summary>
        /// This will put a ray cast from each hand, bite,  and foot attach pt, will cast a ray, anything near enough will stick to foot or hand as if claw, to help walk, crawl, climb
        /// </summary>
        /// <param name="sp"></param>
        public static List<AttachPoint>  FindAndAddClawJointsRayGrabbers(Spirit sp)
        {

         List<AttachPoint> clawPts = sp.CollectOurSpiritGrabberAttachPoints(
           //  PartType.Hand | 
             PartType.Foot |  PartType.Toe, false);

         clawPts.ForEach(x => x.Flags |= AttachPointFlags.IsClaw);
          return clawPts;

        }

        public static Vector2 GetDownDir()
        {
            //TODO convert all hardcoded foot rays and other down to support accel refs , force fields and  radial gravity, implemet toward accel here to support wall walk and improve surfing pipes
            return Vector2.UnitY;
        }
        public static void RayCastAndGrabNearby( Spirit sp, IEnumerable<AttachPoint> clawpts, float maxDist, bool front)
        {

            foreach ( AttachPoint atc in clawpts)
            {

                bool  frontClaw =     (atc.Parent.PartType & (sp.IsFacingLeft ? PartType.Left : PartType.Right)) != 0;

                   if (frontClaw && !front)
                       continue;


                bool isFoot = (atc.Parent.PartType & (PartType.Toe | PartType.Foot))!= 0;

                const float inset = 0.02f;
                RayInfo ray = Sensor.Instance.AddRay(atc.WorldPosition, atc.WorldPosition + (  isFoot ? GetDownDir(): atc.WorldDirection )* (maxDist + inset),
                    atc.Parent.PartType.ToString() + "grabRay", atc.Parent);

                if (ray.IsIntersect )
                {
                    Body body = ray.IntersectedFixture.Body;  //our target body, can be ground or dynamic thing

                    AttachPoint tempPt = new AttachPoint(body, body.GetLocalPoint(ray.Intersection));

                    tempPt.Flags |= AttachPointFlags.IsTemporary;
    
                    tempPt.Attach(atc);  // we rely on animation to know when to Detach, since temporary the  point will be removed on Detach
                    
     
                }
            }
        }




        /// <summary>
        /// Collect grabber attach points from input list .     if parent is a growing hand, skip it.
        /// </summary>
        /// <param name="ptFilter">If PartType.None, then collect attachpoint regardless of type.</param>
        /// <param name="skipConnected">If true, skip if attach point currently connected.</param>
        public static List<AttachPoint> CollectGrabberAttachPoints(Spirit sp, IEnumerable<AttachPoint> attachpoints,
            PartType ptFilter, bool skipConnected)
        {
            List<AttachPoint> newAtp = new List<AttachPoint>();

            foreach (AttachPoint ap in attachpoints)
            {
                if (skipConnected == true && ap.Joint != null)
                    continue;

                if (!ap.IsGrabber)
                    continue;

                float scale;
                sp.GetRegeneratingScale(ap.Parent, out scale);

                // if small hand  or shrinking hand  dont do grab
                // TODO check the grip size..targetGrip .HandleWidth for now allow grab anything with 70% hand for now.
                // need to pass in Target attach point tho
                if (scale < Spirit.minScaleGrabUseable || ap.Parent.IsNotCollideable || sp.IsShrinkingForRegen(ap.Parent))
                    continue;


                if ((ptFilter & ap.Parent.PartType) == ptFilter)
                {

                    newAtp.Add(ap);
                    continue;
                }
            }

            return newAtp;
        }





    }
}
