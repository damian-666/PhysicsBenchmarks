using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;

using Core.Data.Collections;
using Core.Data.Entity;
using System.Diagnostics;

namespace Core.Data.Geometry
{  
    //TODO this belongs with spirit not Geometry its related to a body system.

    // sadly this relies on physics.   There are many many flaky bugs related to spirits , aux spirits, and levels, and data, copy paste, etc.
    //THIS COULD  BE REWRITTEN TO USE ONLY MODEL DATA. (jOINTS HAVE BODY A AND BODYb).  HOWEVER PICKING A BODY Belonging TO A SPIRIT WILL BE SLOW.
    // IT COULD BE A REQUIREMENT TO PICK THE MAIN BODY TO SELECT A SPIRIT.
    //THE SPIRIT , ITS BODIES LIST, AND ITS POWERED JOINT COLLECTION.. 
    //THE TOOL IS USEABLE , BUT BODIES MUST BE SAVED OFTEN, REVERTED WHEN CORRUPT, CLEANED USING CLEAN COMMAND ON RIBBON , ETC.
    //check the entity count , the spirit count , and aux joitn count.. revert to the architectural diagram.
    // perhaps the aux joint should not be collected..
    //USE THE JOINT TOOL TO ERASE AUX JOINTS.   THIS IS LIKE MODEL VIEW.. THE EDITOR SHOULD  TO EDIT THE MODEL WITHOUT USING THE VIEW OR THE PHYSICS, 
    //TEMPORARY POINTERS.. UNFORTUALLY USE OF JOINT EDGE TO WALK THE GRAPH SINCE A BODY HAS A JointList.. it could easily have a Parent Spirit. thats 32 bits
  //  Bi-Directional Management of Child Objects, quite common for each navigation of object models.

    //physics is like a view of a model..

    /// <summary>
    /// Graph Helper to walk over body or joint, Result will be stored in IteratedBodies and IteratedJoints
    /// </summary>
    public class GraphWalker
    {


        public static bool GetJointsFromBody(Body body, out List<Joint> joints)
        {

             var auxJoints = new List<Joint>();
            return GetJointsFromBody(body, out joints, ref auxJoints);


        }


            //TODO this belongs with spirit not Geometry
            /// <summary>
            /// Get Joint Collection from a given body.   This only works after joints are added to physics.  doest not use model.. or spirit data.
            /// </summary>
            /// <param name="body">body</param>
            /// <param name="joints">joints</param>
            /// <returns>true if it found any, false if otherwise</returns>
            public static bool GetJointsFromBody(Body body, out List<Joint> joints,  ref List<Joint> auxJoints)
            {
            joints = new List<Joint>();
          


            if (body == null)
                return false;

            JointEdge je = body.JointList;
            bool result = false;
          
            while (je != null)
            {
                result = true;
        

                if (je.Joint.SkipTraversal && !auxJoints.Contains(je.Joint))
                {
                    auxJoints.Add(je.Joint);

                }else
                if (!(je.Joint is FixedMouseJoint) && !(je.Joint is FixedRevoluteJoint) )  //DH dont collect anchor joints.. just a fix for flakiness without rewriting for now 4/14/15
                {
                    joints.Add(je.Joint);
                }

             
             //    code below is to check for circular reference on joint edge, however currently no longer needed,
              //   as long as bodies and joints are imported proper into level, this circular ref is unlikely to happen.
              //   just left the code here in case needed later.
                // temporary hack. if already added, might be a sign of circular joint loop. fix and break immediately.
                if (je.Next != null && joints.Contains(je.Next.Joint))  //TODO comment out again if never happens..
                {
                    System.Diagnostics.Debug.WriteLine("possible circular JointEdge.");
                    //je.Next.Prev = null;  // seems always null
                    je.Next = null;
                   break;
                }
                je = je.Next;
            }

            return result;
        }




        public static Spirit GetSpiritFromBody(Body body)
        {
            // because level MapBodyToSpirits only stores spirit MainBody,
            // we need to graphwalk starting from control part.
            List<Body> bodies;
            List<Joint> joints;
            List<Joint> auxjoints;
            WalkGraph(body, out bodies, out joints, out auxjoints);  //TODO OPTIMISE slow.. should stop at main body

            Spirit spirit = null;
            foreach (Body b in bodies)
            {
                if (b.PartType != PartType.MainBody)
                    continue;

                if (Level.Instance.MapBodyToSpirits.TryGetValue(b, out spirit))
                    break;
            }

            return spirit;
        }

        /*  TODO FUTURE or never. its not that easy.. be nice to walk this without allocating full  collections.. for now we are using GetSpirtFromBody in spirt class
         * 
       /// <summary>
        /// Find the first body in a joint graph.. joints much be connect, unbroken, enabled, only walks a chain like a limb , maybe not a tree.   TODO test on ynrds and other spirt.. all parts.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static Body WalkGraphToFindPartType(Body body, PartType pt)
        {
            if ( body == null)
                return null;

            if ((body.PartType & pt) != 0)
                return body;

            JointEdge je = body.JointList;
            
            while (je != null)
            {
                Joint joint = je.Joint;
                if (joint != null)
                {

                    List<Joint> joints;
                    GetJointsFromBody(joint.BodyB, out  joints, true);  

                    if (joints.Count > 1)
                        return WalkGraphToFindPartType(joint.BodyB,, pt);

                    je = je.Next;
                }
            }
            return null;
        }

        public static Body FindMainBody(Body body)
        {
            return WalkGraphToFindPartType(body, PartType.MainBody);
        }*/

        /// <summary>
        /// Walk graph
        /// </summary>
        /// <param name="body">body</param>
        /// <param name="outBodies">bodies result</param>
        /// <param name="outJoints">joints result</param>
        /// <param name="checkSkipTraversal">if TRUE then joint will be checked for its SkipTraversal value.
        /// if FALSE then joint SkipTraversal is not checked.</param>
        public static void WalkGraph(Body body, out List<Body> outBodies, out List<Joint> outJoints, out List<Joint> auxJoints)
        {
            outBodies = new List<Body>();
            outJoints = new List<Joint>();
            auxJoints = new List<Joint>();

            IterateBodies(body, ref outBodies, ref outJoints, ref auxJoints);
        }


        /// <summary>
        /// Iterate a body for a series connected joints starting from this body
        /// </summary>
        /// <param name="body">start body</param>
        public static void IterateBodies(Body body, ref List<Body> outBodies, ref List<Joint> outJoints, ref List<Joint> outAuxJoints)
        {
            // If this body was in the spirit map, then add this body to the cache
            outBodies.Add(body);

            List<Joint> joints;
            if (GetJointsFromBody(body, out joints, ref outAuxJoints))
            {
                // If this body contains > 0 of joints, then iterate the connected joints of this body
                if (joints.Count > 0)
                {
                    IterateJoints(joints, ref outBodies, ref outJoints, ref  outAuxJoints);
                }
            }
        }

        /// <summary>
        /// Iterate list of joints to find bodies from a given list of joints.
        /// This one output list of joints in general (including poweredjoint, weld joint), other than FixedMouseJoint.
        /// </summary>
        /// <param name="col">list of joints</param>
        private static void IterateJoints(IEnumerable<Joint> col, ref List<Body> outBodies, ref List<Joint> outJoints, ref List<Joint> outAuxJoints)
        {
            foreach (Joint joint in col)
            {
                // don't walk past broken joint. 
                // skip if joint is already in connected joint list.  note: Contains() might faster using HashSet.
                if (joint.IsBroken || outJoints.Contains(joint) || outAuxJoints.Contains(joint))
                {
                    continue;
                }

                // if its a powered joint, dont walk past temporary one.
                PoweredJoint pj = joint as PoweredJoint;
                if (pj != null && pj.IsTemporary)
                {
                    continue;
                }



                if (joint.SkipTraversal)
                {
                    outAuxJoints.Add(joint);
                    continue;
                      // dont go any deeper we dont wann collect nested ones.. just ones on this graph..
                }
                else
                    outJoints.Add(joint);
                    //TODO add IsTempory to weld joints
                    // if its a weld joint, dont walk past disabled one.     WHY???   dh commented out this could be a hack its not explained. just simplifying code.. spirit tool is  wacky 
                    // don't ignore disabled powered joint, some balloons have double on-off powered joint, will explode.    also using disabled joints to help organize complex spirits with bodies as part of spirit but not actively joined
                    //   if (joint is WeldJoint)
                    //  {
                    //       continue;
                    //  }

                    // add into list


                    // Check the first joint's body, if the 1st body is not available in the cached list, the process the body
                    if (joint.BodyA != null)    // Check null geom to avoid crash
                {
                    Body b1 = joint.BodyA;
                    if (!outBodies.Contains(b1))
                        IterateBodies(b1, ref outBodies, ref outJoints, ref outAuxJoints);
                }

                // Check the second joint's body, if the 2nd body is not available in the cached list, the process the body
                if (joint.BodyB != null)    // Check null geom to avoid crash
                {
                    Body b2 = joint.BodyB;
                    if (!outBodies.Contains(b2))
                        IterateBodies(b2, ref outBodies, ref outJoints, ref outAuxJoints);
                }
            }

        }

        /// <summary>
        /// WalkGraphs, puts the joints into separate collection by type
        /// </summary>
        /// <param name="body"></param>
        /// <param name="outBodies"></param>
        /// <param name="outPoweredJoints"></param>
        /// <param name="outOtherJoints"></param>
        /// <param name="checkSkipTraversal"></param>
        public static void WalkGraphCollectingJoints(Body body, out List<Body> outBodies, out PoweredJointCollection outPoweredJoints,
            out JointCollection outOtherJoints, out List< Joint> outAuxJoints)
        { 
            outPoweredJoints = new PoweredJointCollection();
            outOtherJoints = new JointCollection();

            List<Joint> outJoints;
            outAuxJoints = new List<Joint>();

            WalkGraph(body, out outBodies, out outJoints, out outAuxJoints);

            outJoints.AddRange(outAuxJoints);

            // separate each joint type to its own list
            foreach (Joint j in outJoints)
            {
                if (j is PoweredJoint)
                {
                    outPoweredJoints.Add(j as PoweredJoint);
                }
                else if (j is WeldJoint || j is PrismaticJoint || j is LineJoint)
                {
                    outOtherJoints.Add(j);
                }
            }
        }

        /// <summary>
        /// Walk a graph for non null joint without callback
        /// </summary>
        /// <param name="parentBody">Parent Body</param>
        /// <param name="body">Body</param>
        /// <param name="outBodies">Bodies Result</param>
        /// <param name="outJoints">Joints Result</param>
        public static void WalkGraphNonNullJoint(Body parentBody, Body body,
            out List<Body> outBodies, out PoweredJointCollection outJoints)
        {
            outBodies = new List<Body>();
            outJoints = new PoweredJointCollection();
            IterateBodyNonNullJoint(parentBody, body, ref outBodies, ref outJoints, null);
        }

        /// <summary>
        /// Walk a graph for non null Joint using callback
        /// </summary>
        /// <param name="parentBody">Parent Body</param>
        /// <param name="body">Body</param>
        /// <param name="outBodies">Bodies result</param>
        /// <param name="outJoints">Joints result</param>
        /// <param name="skippedJoints">skipped joints result</param>
        /// <param name="foundCallback">callback when joint found</param>
        public static void WalkGraphNonNullJoint(Body parentBody, Body body,
            out List<Body> outBodies, out PoweredJointCollection outJoints,
            Action<Body, Body, PoweredJoint> foundCallback)
        {
            outBodies = new List<Body>();
            outJoints = new PoweredJointCollection();

            IterateBodyNonNullJoint(parentBody, body, ref outBodies, ref outJoints, foundCallback);
        }


        /// <summary>
        /// Iterate a body for a series of joints that minimal has at least 1 joint connected
        /// </summary>
        /// <param name="parentBody">The parent body</param>
        /// <param name="body">The body</param>
        public static void IterateBodyNonNullJoint(Body parentBody, Body body,
            ref List<Body> outBodies, ref PoweredJointCollection outJoints, bool checkSkipTraversal)
        {
            IterateBodyNonNullJoint(parentBody, body, ref outBodies, ref outJoints, null);
        }

        /// <summary>
        /// Iterate a body for a series of joints that minimal has at least 1 joint connected
        /// </summary>
        /// <param name="parentBody">The parent body</param>
        /// <param name="body">The body</param>
        /// <param name="foundCallback">The Callback when joint is found</param>
        public static void IterateBodyNonNullJoint(Body parentBody, Body body,
            ref List<Body> outBodies, ref PoweredJointCollection outJoints,
            Action<Body, Body, PoweredJoint> foundCallback)
        {
            List<Joint> joints;
            List<Joint> auxJoints = new List<Joint>();  
            if (GetJointsFromBody(body, out joints, ref auxJoints))
            {

                if (((joints.Count > 2 || joints.Count == 1) && body != parentBody) || body.PartType == PartType.MainBody)
                {
                    return;
                }

                if (joints.Count > 0)
                {
                    outBodies.Add(body);
                    IterateJointsNonNull(parentBody, joints, ref outBodies, ref outJoints, foundCallback);
                }
            }
        }

        /// <summary>
        /// Iterate a series of joints for another series of joints that minimal has at least 1 joint connected
        /// </summary>
        /// <param name="parentBody">The parent body</param>
        /// <param name="col">The joint collection</param>
        public static void IterateJointsNonNull(Body parentBody, IEnumerable<Joint> col,
            ref List<Body> outBodies, ref PoweredJointCollection outJoints)
        {
            IterateJointsNonNull(parentBody, col, ref outBodies, ref outJoints, null);
        }

        /// <summary>
        /// Iterate a series of joints for another series of joints that minimal has at least 1 joint connected
        /// </summary>
        /// <param name="parentBody">The parent body</param>
        /// <param name="col">The joint collection</param>
        /// <param name="foundCallback">The callback when joint is found</param>
        public static void IterateJointsNonNull(Body parentBody, IEnumerable<Joint> col,
            ref List<Body> outBodies, ref PoweredJointCollection outJoints,
            Action<Body, Body, PoweredJoint> foundCallback)
        {
            foreach (Joint j in col)
            {
                if (j.SkipTraversal)
                    continue;

                PoweredJoint joint = j as PoweredJoint;  //TODO  allow welds or silders to make spirits

                // Only process a powered joint
                if (joint == null)
                    continue;

                // Check if this joint is already in the connected joint list
                if (!outJoints.Contains(joint))
                {
                    Body b1 = joint.BodyA;
                    Body b2 = joint.BodyB;

                    if (b1 != parentBody)
                    {
                        // If this is not yet exist, then add it in the list
                        outJoints.Add(joint);

                        if (foundCallback != null)
                            foundCallback(b1, b2, joint);

                        // Check the first joint's body, if the 1st body is not available in the cached list, then process the body
                        if (!outBodies.Contains(b1))
                            IterateBodyNonNullJoint(parentBody, b1, ref outBodies, ref outJoints,  foundCallback);
                    }

                    if (b2 != parentBody)
                    {
                        // If this is not yet exist, then add it in the list
                        outJoints.Add(joint);

                        if (foundCallback != null)
                            foundCallback(b1, b2, joint);

                        // Check the second joint's body, if the 2nd body is not available in the cached list, then process the body
                        if (!outBodies.Contains(b2))
                            IterateBodyNonNullJoint(parentBody, b2, ref outBodies, ref outJoints, foundCallback);
                    }
                }
            }
        }

        /// <summary>
        /// Find a main Body from a series of bodies
        /// If no body mared PartType.MainBody exists, one will be chosen from body who has highest joint count
        /// </summary>
        /// <param name="bodies">The Bodies</param>
        /// <returns>Main Body</returns>
        public static Body FindOrDetermineAndMarkMainBody(IEnumerable<Body> bodies)
        {
            Body mainBody = null;
            // first find if there's any main body in the list of bodies
            foreach (Body b in bodies)
            {
                if (b.PartType == PartType.MainBody)
                {
                    if (mainBody != null)
                    {
                        Debug.WriteLine("Unexpected: more that one bodies with partype PartType.MainBody, only the Nexus, with most joints should be marked this way" + "WorldCenter other body with PartType MainBody, you should change PartType, or rebuild Spirit" + b.WorldCenter);
                    }
                    else
                    {
                        mainBody = b;
                    }
                }
            }


            if (mainBody != null)
                return mainBody;

            int maxJoints = 0;


            // not yet set, then search for highest count of joint list, and mark it
            if (mainBody == null)
            {
                
                foreach (Body b in bodies)
                {
                    List<Joint> joints;
                    if (GetJointsFromBody(b, out joints))
                    {
                        if (joints.Count > maxJoints)
                        {
                            maxJoints = joints.Count;
                            mainBody = b;
                        }
                    }
                }
            }

            if (mainBody != null)
            {
                // We have a main body, mark it.
                mainBody.PartType = PartType.MainBody;
                Debug.WriteLine("MainBody with most joints marked PartType.MainBody at location " + mainBody.WorldCenter + "with joints count =" + maxJoints);
            }
            else
            {
                Debug.WriteLine("Spirit needs at least one powered joint. Unexpected behavahior may result " + maxJoints);

                if (bodies.Count() > 0)
                {
                    mainBody = bodies.First<Body>();
                    Debug.WriteLine("Spirit has no powered joints, using first body as MainBody to refer to the spirit " + mainBody.WorldCenter);
                }
            }


            

            return mainBody;
        }
    }
}
