using System;

using System.Collections.Generic;
using System.Diagnostics;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Common.Decomposition;
using FarseerPhysicsUA.Common;

namespace FarseerPhysics.Common.PolygonManipulation
{
    public static class CuttingTools
    {
        //Cutting a shape into two is based on the work of Daid and his prototype BoxCutter: http://www.box2d.org/forum/viewtopic.php?f=3&t=1473

        // ShadowPlay Mods  //the whole thing has been mostly redone

        public class Splitter
        {
            public Vector2 Entry;
            public Vector2 cutVec;

            /// <summary>
            /// So that this instance of the splitter can indicate the left or right side of this body, as determined by the IfLeftOf function.  used for callbacks to remove marks and such
            /// </summary>
            public bool IsLeftSide;

            public Splitter(Vector2 entry, Vector2 exit)
            {
                Entry = entry;
                cutVec = exit - entry;
            }

            public void Extend(float factr)
            {
                Entry -= cutVec * factr;
                cutVec += cutVec * factr;
            }

            /// <summary>
            /// Tells if the test point lies on the left side of the cut line segment .
            /// </summary>
            public bool IsLeftOf(Vector2 test)
            {
                //NOTE:  looked up side of line.
                //     Use the sign of the determinant of vectors (AB,AM), where M(X,Y) is the query point:
                //position = sign( (Bx-Ax)*(Y-Ay) - (By-Ay)*(X-Ax) )
                return (Vector2.Dot(MathUtils.Cross(cutVec, 1), test - Entry) > Settings.Epsilon);
  
            }

            /// <summary>
            /// if on the left side if this is marked LeftSide, and vice vesra
            /// </summary>
            /// <param name="localPt"></param>
            /// <returns></returns>
            public bool IsOnSide(Vector2 localPt)
            {
                return IsLeftSide ? IsLeftOf(localPt) : !IsLeftOf(localPt);
            }
        }


       
    




    //PRESERVE_FIXTURES.. a cut like this is partially implemented in the vault versio  10013.
    //we just cut general verts, and regen fixtures.. only issue is when fixtures are saved.. currently  only pickaxe and torso.  not worth fixing.
    //very complex.. have to split the fixtures , add them all , then add the others if on the correct side, then do the general vertes, and then fix the issue
    //with the torso cut accorss the gills , there are no general verts for that area ( easiest  fix could be to add them to the model if we want to break at that spine joint hear gill openingings)  

    //Body will have new GeneralVerts set to cut..  all ref  mark points removed.. and joints broken on other side.
    public static event Action<Body, Splitter> OnCutBody;
        // will create a new 
        public static event Action<Body, Vertices, Splitter> OnNewCutBody;

        //note an aborted way, partially implement to PRESERVE_FIXTURES.. is in the vault  .. afters the files that hook to this via callback   erase.. its in the vault.. rev 10004
        //BUT .. this is  not good when fixtures have been customized.. example pickaxe and torso.     cuts all the fixtures.. chooses which to stick, more expensive and complex

        // make a lazer slice him up thin..   this to use for breakage and bombs and stuff.
        // first choose one with joints .. then walk to main body try to present spirit.. may need to add entity for one new body only
        // context// break paddle boat.. break creature at waist.. ( can it still walk , or arms.. move)... 
        //also bombs shranell.. more destruction.
        //TODO could it be two spirits??   should it spawn creature on deadly thing like this.. could have all  joined.  bones and be crushed..

        //this is like split shape cut if splits using general vertices.. Then it can be redecomposed later.
        //not perfect since it does not preserve existing saved fixtures. ( like torso and pickaxe.. rarely used  ) but that is much harder to implement .. see above its started.



         /// <param name="world">The world.</param>
        /// <param name="start">The startpoint.</param>
        /// <param name="end">The endpoint.</param>
        /// 
        /// <returns>True if a  cut was performed.</returns>
        public static bool CutComplex(World world, Vector2 start, Vector2 end)
        {
            return CutComplex(world, start, end, null);
        }




    /// <summary>
    /// cut an object that is convex or concave.. Some shapes such a C won't  work TODO ... par pair of points can cut it there first in general destruction.. 
    /// </summary>
    /// <param name="world"></param>
    /// <param name="ptpair"></param>
    /// <param name="bodyToCut"></param>
    /// <returns></returns>
    public static bool CutComplex(World world, PointPair ptpair, Body bodyToCut)
    {
        return CutComplex(world, ptpair.A, ptpair.B, bodyToCut);
    }




        /// <summary>
        /// cut an object that is convex or concave.. Some shapes such a C won't  work TODO ... par pair of points can cut it there first in general destruction.. 
        /// </summary>
        /// <param name="world"></param>
        /// <param name="ptpair"></param>
        /// <param name="bodyToCut"></param>
        /// <returns></returns>
        public static bool CutComplexLocalPts(World world, PointPair ptpair, Body bodyToCut)
        {
            return CutComplex(world, bodyToCut.GetWorldPoint(ptpair.A), bodyToCut.GetWorldPoint(ptpair.B), bodyToCut);
        }


  



        /// <param name="world">The world.</param>
        /// <param name="start">The startpoint.</param>
        /// <param name="end">The endpoint.</param>
        /// <param name="bodyToCut">Cut only this body</param>
        /// <returns>True if a  cut was performed.</returns>
        public static bool CutComplex(World world, Vector2 start, Vector2 end, Body bodytoCut)
        {
            //doesn't support cutting when the start or end is inside a shape.         
            //TODO should to support this at least when end is inside a  dynamic shape.. cast back to a different shape...
            // ignore static body ( like ground) on exit , for cutting objects lying on ground with machine gun.
            //TODO if long cut and dynamic body is behind , then it will fail... Somehow must cut just one object.
            //TODO should be just a matter of removing the odd point if enter pt count > exit?

            //  if (world.TestPointFiltered(start, BodyInfo.Bullet, false) != null || world.TestPointFiltered(end, BodyInfo.Bullet, true) != null)  //test point   ignoring bullet itself and static items
            //     return false;  // dont need this now with complex cut.  actually can interfere with cut. if  its at cut point.

            //if ( OnSetCutFixturesToBody == null || OnCreateNewCutBody == null)   //PRESERVE_FIXTURES.. if we need to .. but this is complex
            //     return false;  //erases all existing fixtures, replace with cut ones.    this code is commented out in 10016


            try {

                List<Fixture> fixtures = new List<Fixture>();
                List<Vector2> entryPoints = new List<Vector2>();
                List<Vector2> exitPoints = new List<Vector2>();

                if (!GetEnterAndExitPointsAndFixturesHit(world, start, end, fixtures, entryPoints, exitPoints))  //TODO return the length cut, use for sound.
                    return false;

                //We only have a single point. We need at least 2
                if (entryPoints.Count + exitPoints.Count < 2)
                    return false;

                if (entryPoints.Count != exitPoints.Count)

                {

                    /*       int mincount = Math.Min(entryPoints.Count, exitPoints.Count);

                           if (entryPoints.Count > mincount)
                           {
                               entryPoints.RemoveRange(mincount - 1, entryPoints.Count - mincount);
                           }

                           if (exitPoints.Count > mincount)
                           {
                               exitPoints.RemoveRange(mincount - 1, exitPoints.Count - mincount);
                           }*/
                    Debug.WriteLine("enter pts not equal exit count");
                    return false;

                }



                //  List<Vertices> newBodyVerts = new List<Vertices>();   //PRESERVE_FIXTURES. //adding a new body for the cut parts.. Joints and stuff might get lost on these unless we add them..  (then might do as above way.. its better) ..   TODO note.. need to check if its a single polygon            
                HashSet<Body> cutBodySet = new HashSet<Body>();   //possible to cut through several bodies  in one long super cut.. 



                for (int i = 0; i < fixtures.Count; i++)
                {

                    if (bodytoCut != null && fixtures[i].Body != bodytoCut)
                        continue;

                    // can't cut circles or edges yet , its ok we dont use it except for particles
                    if (fixtures[i].Shape.ShapeType != ShapeType.Polygon)
                        continue;




                    if (fixtures[i].Body.BodyType != BodyType.Static)
                    {
                        //Split the shape up into two shapes              
                        //TODO future  allow choping bits of static ground, off.. allow keeping some joints,                   
                        //TODO   use joints / side of line to see which fixtures to cut and put on one new body.  
                        //TODO or walk graph keep side closest to joint connecting to main body.  thats to preserve sprits if possible

                        Vertices vertsA;
                        Vertices vertsB;

                        //TODO be carefull need to use outer exist point only here.. this may belong outside of loop

                        //NOTE  .. on a long cut of C shaped figure .. this will mess up ( there will be more than 2 parts)  .. TODO see what happens.. .. and just let if break or do something..  NOTE.. the error gets caught , no cut is performmed.
                        //start just by any A or B.
                        //then count joints.
                        //fill both lists on maps.. then all the actions.

                        Body body = fixtures[i].Body;
                        if (!cutBodySet.Contains(body))  //only need to do this once for each body encountered during cut..  we are cutting general verts and then retesselating.
                        {

                            Splitter splitter;

                            try
                            {

                                //TODO future cleanup Optimize.. shouldnt come here.... check case v shaped items...etc..   entry doens't not match exit..
                                //TODO if supporting long cut on  vshape.. do ray cast shecial looking for outward normal..   for boat hull should not 
                                //matter.. or BETTER  do a "section removal" requiriing multiple laser pulses to cut through
                                //this would IMPROVE BULLET WOUNDS.. MAKE A HOLE..

                              

                                if (!SplitBody(body, body.GeneralVertices, entryPoints[i], exitPoints[i], out vertsA, out vertsB, out splitter))
                                {
                                   Debug.WriteLine("could not split body, probably a v shape or 3 pieces result.. could shorten cut to one fixture");
                                    return false;
                                }


                            }
                            catch (Exception exc)
                            {
                                Debug.WriteLine(exc.ToString());    //TODO future cleanup Optimize.. shouldnt come here.... check case v shaped items...etc
                                continue;
                            }


                            //TODO  favor for vertsA (put to  existing body) anything with a joint, or a valid polygon.

                            //do the call backs right here.  we have a new body cut.  
                            //    newBodyVerts.Add( vertsB);// call a callback to gen a body with new verts.  

                            //todo create try catch on create polygon ,not here is queued
                            float areaA = 0;
                            float areaB = 0;  //todo not sure if area is good for concave polygon..  check with sutherland/ bouyancy area calc.
                            if (!IsValidPolygonAndBigEnough(vertsA, out areaA) || !IsValidPolygonAndBigEnough(vertsB, out areaB))
                            {
                                Debug.WriteLine("polygon too small");
                                return false;  //dont cut it for now.
                            }



                            //   if ( SanityCheck)  was for simple poly.
                            //     
                            //TODO fix the Vertices.CheckPolygon  so that it can success and pass in the  concave or passing criteria
                            //because by the time we get to create polygon its too late.

                            if (vertsA.IsSelfIntersecting() || vertsB.IsSelfIntersecting())
                            {
                                Debug.WriteLine("pieces are self crossing, using IsSelfIntersecting");
                                return false;
                            }

                            //   if (!vertsA.IsSimple() || !vertsB.IsSimple())   // IsSimple is is complex o( n2)
                            //    {
                            //      Debug.WriteLine("pieces are self crossing, using isSimple");
                            //       return false;
                            //   }


                            //NOTE .. tis catches most times the bug happens ( large piece created.. seems one side is exactly with side the length of cut.)
                            //must be bug in split.. probably can catch bug on second zap..
                            //Also... could check.... if side leng = cut length.. then dont cut..  could be due to test inside..
                            //TO make this  rock solid .. use the fie arsenal-lasertestcutwoodandcomplexpressandholdforbug in Media\Tests\weaponsAndDamage
                            if (vertsA.GetArea() + vertsB.GetArea() > body.GeneralVertices.GetArea() + Settings.Epsilon * 3) //note for torso and head.. the area sum of the fixures is diffrent thatn the general verts area.
                            {
                                Debug.WriteLine("area sum  bigger than original");// TODO this happens sometimes with bodies like board in level 1..should be debugged.. if reproducible.
                                return false;
                            }



                            // if (vertsA.GetLongestEdgeSq  ( track in split .. dont count cut) //if large pieces still happen.. and not reporducible
                            // {
                            //      Debug.WriteLine("face sum  bigger than original");// TODO this happens sometimes with bodies like board in level 1..should be debugged.. if reproducible.
                            //   }



                            cutBodySet.Add(body);   //don't need to do this body again..  This is because GetEnterAndExitPointsAndFixturesHit returns fixtures.. we are cutting complex bodies in this.


                            //TODO check maybe if fixture not adjacent for one body..  ( cutting like C shape or vase) .. right now its caught in the split shape
                            //  if (cutAdded[0] != -1) 


                            //TODO adding blood to cut with slove spacing issue.
                            //TODO  expand dress clip region by 1/2 linear slop.

                            //TODO check the edge on bodies.. or indent the views. to make things touch on stack..  currenly uses the line weight..
                            //TODO if main  body ..pick the side with two hip joints.... not the most joints.. would be funny to see  walking torso.   happens..

                            // TODO  put weak points that cut?  on high  tidal fores break at spine joint location?   for now spine is broken at near bom.. but nead general solution for strain

                            //issues .. cant cut board in half.. check with smaller cut..
                            //dont cut weapon.. see hardness.


                            //get boats and waves better..

                            //TODO cutting, picking up and eating meat.   ( necessary to pass a level sometime) 
                            //  if small like 0.06 area ( measure hand area.. add an attach pt.. ( check longest face also).. would need be near shorter face..

                            //TODO count num joints.. or  quick walk to main body ( on arms its ,not a tree.. if head.. die anyways).. then figure out which
                            //see if walk with half  torso for a bit.
                            //TODO fix bleeding in cutt

                            body.GeneralVertices = vertsA;  //safe to do this because physics is not using this.. only window controller
                            body.Info |= BodyInfo.ClipDressToGeom;

                            //output the splitter so the level.. can use it ..?

                            splitter.IsLeftSide = true;

                            BreakSplitJoints(body, splitter);

                            //TODO add nourishment, 
                            //TODO make edible bits of flesh.. add grap points... mabe.. if shape corder

                            //TODO on very long cut , as in last can create larger region and started with..  on those boards with hands at acute angles..
                            //also .. TODO TEST stuff can get stuck in ground.. on cut..

                            splitter.IsLeftSide = false;


                            //  VISUALSLOP

                            //TODO IMPROVE STACKING..HIDE SLOP
                            //TODO expand view in direction AWAY FROM NORMAL AVERAGE OR ONE NORMAL..   use body normals.
                            //remove all the Edges used to    ( add to Clean)  make touching happen.
                            //For dress to both and make clip smaller..


                            //for now cheap workarond.. adding 0.015 stroke on pieces.    

                            //..TODO VISUALSLOP could automatically add this stroke  to all, in tool.. cheap whay.. not sure about rendering perf..
                            //Add to tool

                            //NOTE put the new cut body here before the OnCut.. so that the emitters are all intact to be copied..

                            //away from CM.. most cases fine.. if  CM is inside polygon.
                            OnNewCutBody(body, vertsB, splitter);    //TODO   decide which side.. could have walking legs with half body.. look funny.   generally 
                            OnCutBody(body, splitter);

                            //spirit should be dublicated.. i guess , and side with most joints preserved , rest nicked off..
                        }
                        // return false;  //must be a simple convex polygon.
                    }
                }


            }
            catch( Exception exc)
            {
                Debug.WriteLine("exception in cut" + exc.ToString());
                return false;

            }


            return true;   //cut something
        }

        private static void BreakSplitJoints(Body body, Splitter splitter)
        {
            JointEdge je = body.JointList;

            //TODO add a soundEffect to the joint.. fill with the default.. that way each joint cant have
            //differenct..
            //just as hydraulics.

            while (je != null)
            {
                if (!splitter.IsLeftOf(je.Joint.GetLocalAnchor(body)))
                {
                    je.Joint.BreakQuietly = true;
                    je.Joint.Break();  //TODO  shoud be quiet cut  take sound effect off... 
                }
                je = je.Next;
            }

        }

    

        private static bool GetEnterAndExitPointsAndFixturesHit(World world,  Vector2 start,  Vector2 end, List<Fixture> fixtures, List<Vector2> entryPoints, List<Vector2> exitPoints)
        {

            //TODO avoid complex regions or cutting 3 pieces.
            //if two fixtures for one body, make sure an entry point is close ( epsilon ) to the exist point)
            //TODO ifray ends in another body.. should cut the one body .. i think this is not supported.

            bool oblique = false;
           
            //  fixture, point, normal, fraction
            //Get the entry points
            world.RayCast((fixture, point, normal, fraction) =>
            {
                if (!fixture.Body.IsInfoFlagged(BodyInfo.Bullet))  //shadowplay mod, dont cut bullet bodies, they are doing the cutting.
                {
                    fixtures.Add(fixture);   //NOTE TODO CLEANUP.. SINCE WE ARE now cutting the whole general vertices.. dont need to collect  all fixtures
                                     
                    entryPoints.Add(point);

                    Vector2 strike = start - end;

                    double strikeAngle = Math.Abs(MathUtils.AngleBetweenVectorAndNormal(strike, normal));

                    ///TOD goto to debug the cut..  large object.. maybe just limit the cut endpoint.

                    if (strikeAngle > MathHelper.ToRadians(75)// TODO TUNING .. dont want sharp shards caused by explosions.. 0 is perfect perpendicular 
                        && fixtures.Count == 1)   //only care about the first fixture struck since we will retesselate
                    {
                        oblique = true;
                        Debug.WriteLine("entry angle too oblique for cut " + strikeAngle);
                        return 0;
                    }
                    else return fraction;
                }
                return 1;
            }, start, end);


            if (oblique)
                return false;

            //Reverse the ray to get the exitpoints
            world.RayCast((f, p, n, fr) =>
            {
                if (!f.Body.IsInfoFlagged(BodyInfo.Bullet))  //shadowplay mod.. don't cut bullets
                {
                    exitPoints.Add(p);
                    return fr;
                }
                return 1f;
               
            }, end, start);

            return true;
        }

        /* todo remove this..  use split body   PreserveFixture Preserve_Fixture
        /// <summary>
        /// Split a fixture into 2 vertex collections using the given entry and exit-point.
        /// </summary>
        /// <param name="fixture">The Fixture to split</param>
        /// <param name="entryPoint">The entry point - The start point</param>
        /// <param name="exitPoint">The exit point - The end point</param>
        /// <param name="first">The first collection of vertexes</param>
        /// <param name="second">The second collection of vertexes</param>
        public static void SplitShapeComplex(Fixture fixture, Vector2 entryPoint, Vector2 exitPoint, out Vertices first, out Vertices second)
        {
            Vector2 localEntryPoint = fixture.Body.GetLocalPoint(ref entryPoint);
            Vector2 localExitPoint = fixture.Body.GetLocalPoint(ref exitPoint);

            PolygonShape shape = fixture.Shape as PolygonShape;

            //We can only cut polygons at the moment
            if (shape == null)
            {
                first = new Vertices();
                second = new Vertices();
                return;
            }

            //Offset the entry and exit points if they are too close to the vertices
            foreach (Vector2 vertex in shape.Vertices)
            {
                if (vertex.Equals(localEntryPoint))
                    localEntryPoint -= new Vector2(0, Settings.Epsilon);

                if (vertex.Equals(localExitPoint))
                    localExitPoint += new Vector2(0, Settings.Epsilon);
            }

            Vertices vertices = new Vertices(shape.Vertices);
            Vertices[] newPolygon = new Vertices[2];

            for (int i = 0; i < newPolygon.Length; i++)
            {
                newPolygon[i] = new Vertices(vertices.Count);
            }

            int[] cutAdded = { -1, -1 };
            int last = -1;
            for (int i = 0; i < vertices.Count; i++)
            {
                int n;    //TODO  cleanup.. could make splitter class that holds the line, takes a point , just returns if point is left or right..  could be used by Level as well
                //Find out if this vertex is on the old or new shape.   ( what side of the line ) 
                if (Vector2.Dot(MathUtils.Cross(localExitPoint - localEntryPoint, 1), vertices[i] - localEntryPoint) > Settings.Epsilon)
                    n = 0;
                else
                    n = 1;

                if (last != n)
                {
                    //If we switch from one shape to the other add the cut vertices.
                    if (last == 0)
                    {
                        Debug.Assert(cutAdded[0] == -1);
                        cutAdded[0] = newPolygon[last].Count;
                        newPolygon[last].Add(localExitPoint);
                        newPolygon[last].Add(localEntryPoint);
                    }
                    if (last == 1)
                    {
                        Debug.Assert(cutAdded[last] == -1);
                        cutAdded[last] = newPolygon[last].Count;
                        newPolygon[last].Add(localEntryPoint);
                        newPolygon[last].Add(localExitPoint);
                    }
                }

                newPolygon[n].Add(vertices[i]);
                last = n;
            }

            //Add the cut in case it has not been added yet.
            if (cutAdded[0] == -1)
            {
                cutAdded[0] = newPolygon[0].Count;
                newPolygon[0].Add(localExitPoint);
                newPolygon[0].Add(localEntryPoint);
            }
            if (cutAdded[1] == -1)
            {
                cutAdded[1] = newPolygon[1].Count;
                newPolygon[1].Add(localEntryPoint);
                newPolygon[1].Add(localExitPoint);
            }

            for (int n = 0; n < 2; n++)
            {
                Vector2 offset;
                if (cutAdded[n] > 0)
                {
                    offset = (newPolygon[n][cutAdded[n] - 1] - newPolygon[n][cutAdded[n]]);
                }
                else
                {
                    offset = (newPolygon[n][newPolygon[n].Count - 1] - newPolygon[n][0]);
                }
                offset.Normalize();

                if (!offset.IsValid())
                    offset = Vector2.One;

                newPolygon[n][cutAdded[n]] += Settings.Epsilon * offset;

                if (cutAdded[n] < newPolygon[n].Count - 2)
                {
                    offset = (newPolygon[n][cutAdded[n] + 2] - newPolygon[n][cutAdded[n] + 1]);
                }
                else
                {
                    offset = (newPolygon[n][0] - newPolygon[n][newPolygon[n].Count - 1]);
                }
                offset.Normalize();

                if (!offset.IsValid())
                    offset = Vector2.One;

                newPolygon[n][cutAdded[n] + 1] += Settings.Epsilon * offset;
            }

            first = newPolygon[0];
            second = newPolygon[1];
        }

        */

       
        /// <summary>
        /// This is to provide the general Vertces  and perhaps replace the joints for a split body.   it would be complex to figure the outer verts from just the fixtures.
        ///  One way .. if point is in only one fixture then its a general vertex.. but winding .. ugh.. this is basically the same as splitting a fixture  .. ti does not not matter that is concave.. so long as cut is not extremely long and does not enter and exit multiple times
        /// </summary>
        /// <param name="fixture">The Body to split, in not modified</param>
        /// <param name="generalVertices">The vertexes defining the polygon to split  </param>
        /// <param name="entryPoint">The entry point - The start point</param>
        /// <param name="exitPoint">The exit point - The end point</param>
        /// <param name="first">The first collection of vertexes on one side</param>
        /// <param name="second">The second collection of vertexes on other side</param>
        /// <param name="splitter">the divider object, can be used for the rest of the job, removing joints and marks, etc</param>   
        public static bool SplitBody(Body body, Vertices generalVertices, Vector2 entryPoint, Vector2 exitPoint, out Vertices first, out Vertices second, out Splitter splitter)
        {
            Vector2 localEntryPoint;
            Vector2 localExitPoint;

            first = null;
            second = null;

 
            //this is a complex operations.. lineintersect.. walk general verts, etc.
            GetLocalEnterAndExit(body, generalVertices, ref entryPoint, ref exitPoint, out localEntryPoint, out localExitPoint);

            //TODO try extending this line.. look at dot product..
            splitter = new Splitter(localEntryPoint, localExitPoint);


            Vertices vertices = new Vertices(generalVertices);
            Vertices[] newPolygon = new Vertices[2];

            for (int i = 0; i < newPolygon.Length; i++)
            {
                newPolygon[i] = new Vertices(vertices.Count);
            }

            int[] cutAdded = { -1, -1 };
            int last = -1;
            for (int i = 0; i < vertices.Count; i++)
            {
                int n;
                //Find out if this vertex is on the old or new shape.   ( what side of the line ).
                if (splitter.IsLeftOf(vertices[i]))         //TODO if collinear should be ditched ( look at left of.. it will offset the entry points..?  .. consider tiny polygons.. i think its better to skip those  , but then chek
                    n = 0;
                else
                    n = 1;

                if (last != n)
                {
                    //If we switch from one shape to the other add the cut vertices.
                    if (last == 0)
                    {
                        if (cutAdded[0] != -1) //shadowplay mod changed from assert. this can happen when cutting through a v shaped items, it would  be 3 pieces   
                            return false;

                        cutAdded[0] = newPolygon[last].Count;
                        newPolygon[last].Add(localExitPoint);


                        newPolygon[last].Add(localEntryPoint);


                    }
                    if (last == 1)
                    {                                   //TODO cutting legs ..
                        if (cutAdded[last] != -1)    //shadowplay mod changed from assert. this can happen when cutting through a v shaped items, it would  be 3 pieces   
                            return false;

                        cutAdded[last] = newPolygon[last].Count;
                        newPolygon[last].Add(localEntryPoint);
                        newPolygon[last].Add(localExitPoint);
                    }
                }

                newPolygon[n].Add(vertices[i]);
                last = n;
            }

            //Add the cut in case it has not been added yet.
            if (cutAdded[0] == -1)
            {
                cutAdded[0] = newPolygon[0].Count;
                newPolygon[0].Add(localExitPoint);
                newPolygon[0].Add(localEntryPoint);
            }
            if (cutAdded[1] == -1)
            {
                cutAdded[1] = newPolygon[1].Count;
                newPolygon[1].Add(localEntryPoint);
                newPolygon[1].Add(localExitPoint);
            }

            for (int n = 0; n < 2; n++)
            {
                Vector2 offset;
                if (cutAdded[n] > 0)
                {
                    offset = (newPolygon[n][cutAdded[n] - 1] - newPolygon[n][cutAdded[n]]);
                }
                else
                {
                    offset = (newPolygon[n][newPolygon[n].Count - 1] - newPolygon[n][0]);
                }
                offset.Normalize();

                if (!offset.IsValid())
                    offset = Vector2.One;

                newPolygon[n][cutAdded[n]] += Settings.Epsilon * offset;

                if (cutAdded[n] < newPolygon[n].Count - 2)
                {
                    offset = (newPolygon[n][cutAdded[n] + 2] - newPolygon[n][cutAdded[n] + 1]);
                }
                else
                {
                    offset = (newPolygon[n][0] - newPolygon[n][newPolygon[n].Count - 1]);
                }
                offset.Normalize();

                if (!offset.IsValid())
                    offset = Vector2.One;

                newPolygon[n][cutAdded[n] + 1] += Settings.Epsilon * offset;
            }

            first = newPolygon[0];
            second = newPolygon[1];

            return true;
        }


        //TODO lets say arm is cut.. there are two joints.   need to find the one that walks to the main body.  ( walking graph)
        //want to be able to leave the body closest to 

        //TODO also be funny to cut torso in half  and have creature keep walking .. mabye.. this would favor cutting joitns marked hip..
        //TODO add joint ID flags.. hip, shoulder, elbow,   temp , etc.. get rid of string tag..  this will help cutting up figures.  set the flags in plugin using idx



        //TODO untested
        /// <summary>
        /// returns true if more joints on the side1 of the cut line ( according to cross product) .. if the same, returns 0, doesnt matter.
        /// </summary>
        /// <param name="b"></param>
        /// <param name="cutLine"></param>
        /// <param name="localEntryPoint"></param>
        /// <returns></returns>
        public static bool MoreJointsOnLeftSide(Body b, Splitter divider)
        {
            int num0 = 0;
            int num1 = 1;

            int numGraphJoints = b.GetNumJointsConnected(true);

            JointEdge je = b.JointList;
            while (je != null)
            {
                JointEdge je0 = je;
                je = je.Next;
                Joint joint = je0.Joint;

                if (numGraphJoints > 0 && joint.Usage == JointUse.Embedded)// if this joint is part of a system, skip stuck  bullets..   however  should not skip them if part has 
                {
                    if (IsJointOnLeft(b, divider, joint))
                        num0++;
                    else
                        num1++;
                }
            }
            return num0 > num1;
        }

        private static bool IsJointOnLeft(Body b, Splitter divider, Joint joint)
        {
            Vector2 localJointPos = joint.GetLocalAnchor(b);
            return divider.IsLeftOf(localJointPos);
        }

        private static void GetLocalEnterAndExit(Body body, Vertices generalVertices, ref Vector2 entryPoint, ref Vector2 exitPoint, out Vector2 localEntryPoint, out Vector2 localExitPoint)
        {
            localEntryPoint = body.GetLocalPoint(ref entryPoint);
            localExitPoint = body.GetLocalPoint(ref exitPoint);

            //Offset the entry and exit points if they are too close to the vertices// TODO consider ditch this an and remove collinear points instead... since we are adding points.. however no sure if the loop will get confused .
            foreach (Vector2 vertex in generalVertices)
            {
                if (vertex.Equals(localEntryPoint))
                    localEntryPoint -= new Vector2(0, Settings.Epsilon);

                if (vertex.Equals(localExitPoint))
                    localExitPoint += new Vector2(0, Settings.Epsilon);
            }
        }


        //shadowplay mod.. break this up.. make biggest min pieices.
        public static bool IsValidPolygonAndBigEnough(Vertices vertices, out float area)
        {
            area = 0;

            if (vertices.Count < 3)
                return false;

//TODO check small pieces here .. Tuning
            const float minsize = 0.02f * 0.02f; // small pieces at low density cause issues and hard to see .  4cm is smallest bit.

            const float minEdge = Settings.Epsilon*2;

            //TODO check for angles too sharp.... not safe cuts..

            //TODO see why small part like leg part is not cut right.. its indented or something

            //then see if bad shapes happen.. randomly.. take not of original shape and examine the parts.  make it reproducible

            area = vertices.GetArea();

            if (area < minsize * minsize)
                return false;

            for (int i = 0; i < vertices.Count; ++i)  //edges too small.
            {
                int i1 = i;
                int i2 = i + 1 < vertices.Count ? i + 1 : 0;
                Vector2 edge = vertices[i2] - vertices[i1];

                if (edge.LengthSquared() < minEdge * minEdge)
                    return false;
            }
            return true;
        }

        // code for original farseer.   with new checks, we still fail to creature simple bodies cut, often
        public static bool SanityCheck(Vertices vertices)
        {

            float area = 0;
            if (!IsValidPolygonAndBigEnough(vertices, out area))
                return false;

            for (int i = 0; i < vertices.Count; ++i)
            {
                int i1 = i;
                int i2 = i + 1 < vertices.Count ? i + 1 : 0;
                Vector2 edge = vertices[i2] - vertices[i1];

                for (int j = 0; j < vertices.Count; ++j)
                {
                    // Don't check vertices on the current edge.
                    if (j == i1 || j == i2)
                        continue;

                    Vector2 r = vertices[j] - vertices[i1];

                    // Your polygon is non-convex (it has an indentation) or
                    // has colinear edges.
                    float s = edge.X * r.Y - edge.Y * r.X;

                    if (s < 0.0f)
                        return false;
                }
            }

            return true;
        }

    }
}
