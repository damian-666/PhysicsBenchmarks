using System;
using System.Net;


using System.Collections.Generic;
using System.Linq;
using System.Text;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using System.Diagnostics;

namespace FarseerPhysics.Common.PolygonManipulation
{

    //a class to determine the intersection of a convex polygon ( floating or submerged fixture ) and a wave polygon ( does not need to be convex)
    // This clips the subject polygon against the clip polygon (gets the intersection of the two polygons). Used for bouyancy in waves.
    // Based on the psuedocode from:
    // http://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman
    //adapted from code at   http://rosettacode.org/wiki/Sutherland-Hodgman_polygon_clipping

    public static class SutherlandHodgman
    {

        //shadowply mod from original. was a class, changed to struct , this roseltta  code is not GC optimized at all, 
        /// <summary>
        /// This represents a line segment
        /// </summary>
        private struct Edge
        {
            public Edge(Vector2 from, Vector2 to)
            {
                From = from;
                To = to;
            }

            public readonly Vector2 From;
            public readonly Vector2 To;
        }



//NOTE comment out  POSSIBLE OPTIMZATION

           // could understand this algorith.  
// cant use the faces because they are tesselated faces.
//however could return the whole set of poitns, maybe as stucts with face slope , then use rays to elminate those outside of hull.S

        //NOTE.. list of interesect pots might be high on bump edge on flat planing bottom..we dont want to use inner hull points.. on a horizontal section especiecailly .. highest pits is not the water line..

        //NOTE .. tested a block that had over two separte bumps of going in, somehow the path of verts rendered correctly as 3 regions on test screen.
        //so now plan to get left and right most edges from the collection returned by this.  all attempts to find crossing collecting segments that interesected led to a superset , sometimgs 4 poitns when 2 were expect, and sometimes on on edge of 
        //convex poly, further out.

        //TODO this is N sq..  complexitiy.. do a # skip bins param to simplify the water.
        /// <summary>
        /// This clips the subject polygon against the clip polygon (gets the intersection of the two polygons). Used for bouyancy in waves.
        /// NOTE .. to generalize this , should passing in the subjectPolygon as a List of Vectices.    passing in as an Array since this is modified for the special purpose
        /// of a water region with only the top edge changing , and a static buffer could be used.
        /// </summary>
        /// <remarks>
        /// Based on the psuedocode from:
        /// http://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman
        /// </remarks>
        /// <param name="subjectPoly">Can be concave or convex, the water body in our use case</param> 
        /// <param name="startindex">left starting index of the subject body verts of interest ( to save computation) </param> 
        /// <param name="endindex">end index of the subject body verts of interest</param> 
        /// <param name="bottomRight">bottom left of the subject body used for water special case</param>      
        ///  <param name="bottomLeft">bottom left of the subject body used for water special case</param>
        /// <param name="clipPoly">Must be convex</param>
        /// <param name="intersections">a superset of the actual intesections to the outside of the clipCopy  ( floating thing) 
        /// <returns>The polygon verts that defines intersection of the two polygons </returns>
        public static Vertices GetIntersectedPolygon(Vector2[] subjectPoly, int startIndex, int endIndex, Vector2 bottomRight, Vector2 bottomLeft, Vector2[] clipPoly, out Vertices intersections)
        {

            intersections = new Vertices();  //added  to collect only intersections.  //TODO this is not to get a superset of segmenting in water ( subjectpoly that actually intersect clip poly)  sometimes gives edge that extend beyond the subject poly segment.

#if DEBUG
            if (Math.Abs(endIndex - startIndex) < 2)
            {
                //TODO not true just put a water vert at startindex + 1;   this is for small objects floating in one bin. 
                Debug.WriteLine(" need add least two bins to establish a line of water" + (endIndex - startIndex).ToString());
                return null;
            }

            if (clipPoly.Count() < 3)
            {
                Debug.WriteLine("The convex passed in must have at least 3 pts" + clipPoly.Count().ToString());
                return null;
            }
#endif
            //NOTE GC OPTIMIZE to generalize this , once would pass in the Vertices, then just set outputList to that...  That would we we could  use static buffers, less GC
            Vertices outputList = new Vertices(subjectPoly, startIndex, endIndex);// this goes from right to left to be clock wise   , end < start will reverse it
            outputList.Add(bottomLeft);  //NOTE Y positive goes Down ARGHH this is sure to be  clockwise, its describes the body of water but ignoring the verts outside of start and endindex which come from the AABB of the floating body
            outputList.Add(bottomRight);  //Y higher values are down..    Add this last its clockwise.. at least untill a wave is generated.. dont know why then it fails.. results are still ok.   
  
            //TODO comment this check even in debug when its confirmed ,its slow.
#if DEBUG
            // Make sure it's clockwise.  When used with  height field wave.. poly is always going to be clockwise 
            //NOTE but tests seem same but gets a bad result.. clip still seems to work, dont reverve the pts in anycase.
            //NOTE i think it works now that wave pump and data is complete using FULLSPRING

            if (outputList.IsCounterClockWise())  //TESTING this test works with wave, its is good or better.  still might fail but clipping may work
            {
                Debug.WriteLine("subjectPoly ( ocean body)  is not  clockwise, via farseer method");
            }

            //    if (!IsClockwise(outputList)) //  NOTE TODO retest this does not always work with choppy waves..
            //        { 
            //           Debug.WriteLine("subjectPoly ( ocean body)  is not  be clockwise, via rosetta code test");  
            //         outputList.Reverse();  //not that simple .. might not be crossed..   this will break it.
            //     }
#endif

            //	Walk around the clip convex polygon clockwise  ( submerged object)          
           //TODO use array and just iterate like walk code..     
           //if i  could understand this algorithm, i could probable get out just the actuall intersect points ( 2)
// cant use the faces because they are tesselated faces.
//however could return the whole set of poitns, maybe as stucts with face slope , then use rays to elminate those outside of hull.

            foreach (Edge clipEdge in IterateEdgesClockwise(clipPoly))  //TODO OPTIMIZE other language implementatins  don't allocate edges.. this migbt be slow and causing GC 
            {
                List<Vector2> inputList = outputList.ToList();
                outputList.Clear();

                bool edgeIntersect;
                if (inputList.Count == 0)
                    break; //	Sometimes when the polygons don't intersect, this list goes to zero.  Jump out to avoid an index out of range exception

                Vector2 S = inputList[inputList.Count - 1];

                //TODO future cleanup maybe recode in C# from the c++ sample.  this looks more complex , more GC        
                foreach (Vector2 E in inputList)  //iterating the water ( concave)  poly
                {
                    if (IsInside(clipEdge, E))   //TODO..  for full solution.. there is a chance that the pointin water is collinater .. then Add it to the intersection list.  //use    intersectionsList.Add(point.Value);
                    {
                        if (!IsInside(clipEdge, S))
                        {
                            Vector2? point = GetIntersect(S, E, clipEdge.From, clipEdge.To, out edgeIntersect );                
                         //   Vector2? point = LineTools.LineIntersect(S, E, clipEdge.From, clipEdge.To, out edgeIntersect);  //same results
                            if (point == null)
                            {
                                // throw new ArgumentException("Line segments don't intersect");	
                                Debug.WriteLine("Line segments don't intersect, must be parallel"); //this is remotely possible with water and hull inward exactly same angle..  just continue
                                continue;
                            }
                            else
                            {
                                if (edgeIntersect)
                                {
                                    //issue both intersection
                                    //can easily be tested with a triangle in water.
                                    //Vector2 intersectionPt;
                                    //  if (LineTools.LineIntersect2(S, E, clipEdge.From, clipEdge.To, out  intersectionPt))  //double check, still too many verts , must be the algorithm. its not understood
                                    {
                                        intersections.Add(point.Value);
                                    }
                                }
                                outputList.Add(point.Value);
                            }
                        }

                        outputList.Add(E);
                    }
                    else if (IsInside(clipEdge, S))  //TODO if collinear add to intersection list.
                    {
                        //  Vector2? point = GetIntersect(S, E, clipEdge.From, clipEdge.To, out edgeIntersect);  //same result
                        Vector2? point = LineTools.LineIntersect(S, E, clipEdge.From, clipEdge.To, out edgeIntersect);
                        if (point == null)
                        {                      
                            Debug.WriteLine("Line segments don't intersect");
                            continue;
                        }
                        else
                        {
                            if (edgeIntersect)
                            {
                               intersections.Add(point.Value);
                            }
                            outputList.Add(point.Value);
                        }
                    }
                    S = E;
                }
            }

            //TODO ,, NOTES  exact winding does not seem to affect bouyancy  since the first and last verts can be out of place.... can simplify  this area with less bins make it faster...
            //this is an expensive calculation.
            //Plan  to be done with water in may.. distubances have to look good.. now with the splahses they dont.. wind is also too strong.
            //partcies move allot in the win.  at least it can be heard when head is above water..  ( check that) 
            // also make a sound .. perhaps to music to indicate underwater..
            //its should generate some chop as well.
            //interate verts on Boat there are fewer.      clockwise or wateever.   
            // for each vert if over water , then next one is under water.. get the indices of those and do an intersection between the vectors.... <-- tried this.
            //this is quick, food proof and exploits the "function" like nature of the wave data.   it not an arbiraty polygon.

            //NOTE above assumtions  is not good,  cannot handle a small bump penetrating bottom hull.   both versts are on the same side in that case

            //we would need to iterate the subjectPoly  twice.. once for each fixture to get the 4 points.. then figure out if sinking,  then one  for the water clip region around boat 
     
            Debug.WriteLine("intersections" +    intersections.Count());   //should be exactly 2 for a convex body   4 for a simple hull type boat.     //issues is some intersections are not near the boat.  NOTE could remove some of these..   get leftest and right most exclude and not inside  the boats "airfixture(s)" ..using is inside test.. 
            //TO see if its sinking get the pairs furtherest left  and furthest to the right... if either are below water , find the depth, start spraying in water up and left / right. with a line of partices.
            //then quickly apply normal bouyancy without air, boat will sink quickly.

            //TODO TEST perforance with like 3 guys in water.. 2 long boats.   high res water. i think will be ok.   if not should consider walking boat edges and approximating water surface for bouyancy by skipping verts, maybe 10 per of AABB length
            //think.. lot of debris floating about..
            return outputList;
        }



        //NOTE
        // http://www.angusj.com/delphi/clipper.php   this might help.. it has intersection, difference etc.. could use that to clip water..

        //however very hard to integrate, all in one file .. uses doubles and lots of spatial data structures
        //  LineIntersect(ref Vector2 point1, ref Vector2 point2, ref Vector2 point3, ref Vector2 point4,
        //                                  bool firstIsSegment, bool secondIsSegment,
        //                                 out Vector2 point)



        /// <summary>
        /// This clips the subject polygon against the clip polygon (gets the intersection of the two polygons). Used for bouyancy in waves.
        /// NOTE .. to generalize this , should passing in the subjectPolygon as a List of Vectices.    passing in as an Array since this is modified for the special purpose
        /// of a water region with only the top edge changing , and a static buffer could be used.
        /// </summary>
        /// <remarks>
        /// Based on the psuedocode from:
        /// http://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman
        /// </remarks>
        /// <param name="oceanPoly">Can be concave or convex, the water body in our use case</param> 
        /// <param name="startindex">left starting index of the subject body verts of interest ( to save computation) </param> 
        /// <param name="endindex">end index of the subject body verts of interest</param> 
        /// <param name="bottomRight">bottom left of the subject body used for water special case</param>      
        ///  <param name="bottomLeft">bottom left of the subject body used for water special case</param>
        /// <param name="clipPoly">Must be convex</param>
        /// <param name="leftEdge">the left most edge of the intersection</param>    //not working , and not use.. winding around the subject poly instead
        /// <param name="rightEdge">the right most edge of the intersection  </param>
        /// <returns>The polygon verts that defines intersection of the two polygons </returns>
        public static Vertices GetIntersectedPolygon(Vector2[] oceanPoly, int startIndex, int endIndex, Vector2 bottomRight, Vector2 bottomLeft, Vector2[] clipPoly)
        {

#if DEBUG
            if (Math.Abs(endIndex - startIndex) < 1)
            {
                Debug.WriteLine(" need add least two bins to establish a line of water" + (endIndex - startIndex).ToString());
                return null;
            }
            if (clipPoly.Count() < 3)
            {
                Debug.WriteLine("The convex passed in must have at least 3 pts" + clipPoly.Count().ToString());
                return null;
            }
#endif

            //NOTE GC OPTIMIZE to generalize this , once would pass in the Vertices, then just set outputList to that...  That would we we could  use static buffers, less GC
            
            Vertices outputList = new Vertices(oceanPoly, startIndex, endIndex);// this goes from right to left to be clock wise   , end < start will reverse it.   This is the ocean region
            outputList.Add(bottomLeft);  //NOTE Y positive goes Down ARGHH this is sure to be  clockwise, its describes the body of water but ignoring the verts outside of start and endindex which come from the AABB of the floating body
            outputList.Add(bottomRight);  //Y higher values are down..    Add this last its clockwise.. at least untill a wave is generated.. dont know why then it fails.. results are still ok.   
            Vertices intersectionsList = new Vertices();  //added  to collect only intersections.  //TODO this is not shown to work, sometimes gives edge that extend beyond the subject poly segment.

            //TODO comment this check even in debug when its confirmed ,its slow.
#if DEBUG
            // Make sure it's clockwise.  When used with  height field wave.. poly is always going to be clockwise 
            //NOTE but tests seem same but gets a bad result.. clip still seems to work, dont reverve the pts in anycase.
            //NOTE i think it works now that wave pump and data is complete using FULLSPRING

            if (outputList.IsCounterClockWise())  //TESTING this test works with wave, its is good or better.  still might fail but clipping may work
            {
                Debug.WriteLine("subjectPoly ( ocean body)  is not  clockwise, via farseer method");
            }
#endif
            //	Walk around the clip convex polygon clockwise  ( submerged object)
            foreach (Edge clipEdge in IterateEdgesClockwise(clipPoly))  //TODO OPTIMIZE other language implementatins  don't allocate edges.. this is slow.
            {
                List<Vector2> inputList = outputList.ToList();
                outputList.Clear();

                if (inputList.Count == 0)
                    break; //	Sometimes when the polygons don't intersect, this list goes to zero.  Jump out to avoid an index out of range exception

                Vector2 S = inputList[inputList.Count - 1];

                foreach (Vector2 E in inputList)  //iterating the water ( concave)  poly
                {
                    if (IsInside(clipEdge, E))
                    {
                        if (!IsInside(clipEdge, S))
                        {
                            Vector2? point = GetIntersect(S, E, clipEdge.From, clipEdge.To);
                            if (point == null)
                            {
                                Debug.WriteLine("Line segments don't intersect");
                                continue;
                            }
                            else
                            {
                                outputList.Add(point.Value);
                            }
                        }
                        outputList.Add(E);
                    }
                    else if (IsInside(clipEdge, S))
                    {
                        Vector2? point = GetIntersect(S, E, clipEdge.From, clipEdge.To);//,out edgeIntersect);
                        if (point == null)
                        {
                            Debug.WriteLine("Line segments don't intersect");
                            continue;
                        }
                        else
                        {
                            outputList.Add(point.Value);
                        }
                    }
                    S = E;
                }
            }

            //TODO TEST perforance with like 3 guys in water.. 2 long boats.   high res water. i think will be ok.   if not should consider walking boat edges and approximating water surface for bouyancy by skipping verts, maybe 10 per of AABB length
            //think.. lot of debris floating about..
            return outputList;
        }



        #region Private Methods

        /// <summary>
        /// This iterates through the edges of the polygon, always clockwise
        /// </summary>
        private static IEnumerable<Edge> IterateEdgesClockwise(Vector2[] polygon)
        {
            //if (IsClockwise(polygon)) //TODO double check that we dont need this.   tested with maybe bodies all tessleated by default. optimize by removing this check.. I think our tesselator forces everything counterclockwise .  code verify , test with bunch of stuff, then comment out.
            //{
            //    Debug.WriteLine("shape is wound clockwise.  can't assume in sutherlandhodgman all fixtures are counterclockwise"); 
            //    for (int cntr = 0; cntr < polygon.Length - 1; cntr++)
            //    {
            //        yield return new Edge(polygon[cntr], polygon[cntr + 1]);
            //    }
            //    yield return new Edge(polygon[polygon.Length - 1], polygon[0]);
            //}
            //else
            //   {
            for (int cntr = polygon.Length - 1; cntr > 0; cntr--)  //GC OPTIMIZE.. better to iterate this loop without new Edges.  See box 2d code for this same algorithm
            {
                yield return new Edge(polygon[cntr], polygon[cntr - 1]);
            }
            yield return new Edge(polygon[0], polygon[polygon.Length - 1]);
            //   }
        }


        // http://www.angusj.com/delphi/clipper.php   this might help.. it has intersection, difference etc.. could use that to clip water..
        //however very hard to integrate, all in one file .. uses doubles and lots of spatial data structures
        //  LineIntersect(ref Vector2 point1, ref Vector2 point2, ref Vector2 point3, ref Vector2 point4,
        //                                  bool firstIsSegment, bool secondIsSegment,
        //                                 out Vector2 point)


        /// <summary>
        /// Returns the intersection of the two lines (line segments are passed in, but they are treated like infinite lines)
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/14480124/how-do-i-detect-triangle-and-rectangle-intersection
        /// eedgeIntersect is true means the segement it
        /// </remarks>

/// <param name="line1From"></param>
/// <param name="line1To"></param>
/// <param name="line2From"></param>
/// <param name="line2To"></param>
/// <param name="edgeIntersect"></param>
/// <returns>the intersect point or null if parallel</returns>
        private static Vector2? GetIntersect(Vector2 line1From, Vector2 line1To, Vector2 line2From, Vector2 line2To, out bool edgeIntersect)
        { 
            edgeIntersect = false;
            Vector2 direction1 = line1To - line1From;
            Vector2 direction2 = line2To - line2From;

            float dotPerp = (direction1.X * direction2.Y) - (direction1.Y * direction2.X);

            // If it's 0, it means the lines are parallel so have infinite intersection points
            if (IsNearZero(dotPerp))
                return null;

            Vector2 c = line2From - line1From;
            float t = (c.X * direction2.Y - c.Y * direction2.X) / dotPerp;

            #region Shadowplay Mod
            // from original Sutherland code..   TODO this is not tested.    checked the source above quickly its the same , 
            //this was commented out in the source and its not needed for just the clipping.  now it used to find actual intersection points of the  water to convex object.
            //there should be only  two of these.  There are line intersect tools in LineTools but this is a way to avoid multiple iterations.
            //NOTE .. we get several of these... this is not working.   sometimes one of the points is wrong.
            //there is a faster way probably, by interating the body  edges.. for out of water and in. , but .. this should work.
            //and, this should work regardless of y is up or down.

            //NOTE this does not appear to work.. edges of water are still treated as infinite sometimes.  BUT I THINK ITS THE SUTHERLAND LOOP..
            //maybe its negative 1 .. TODO test   step throug  .. you can see all little wave goes buy other intersections are taken that are further out... do triange and simple wave few bins..
            // there should be exactly 2 poitns but sometimes 3 or 4.    this appears when a bump in the water causes another intersectino, however that bump segment does not intersect the piece.
            //ANYWAY.. we need to walk, wind the subject polyanyways, these points are not always needed, this way might be slower , more wasteful, aborting for now.
            //a solution would be to use the lineintersect in farseer.
            //TODO can try another way, test  LineIntersect
            if (!(t < 0 || t > 1)) // if not lies outside the line segment
            {
                double u = (c.X * direction1.Y - c.Y * direction1.X) / dotPerp;
                if (!(u < 0 || u > 1))
                {
                    edgeIntersect = true;
                }
            }
            #endregion

            //	Return the intersection point
            return line1From + (t * direction1);

        }



        /// <summary>
        /// Returns the intersection of the two lines (line segments are passed in, but they are treated like infinite lines)
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/14480124/how-do-i-detect-triangle-and-rectangle-intersection
        /// </remarks>
        private static Vector2? GetIntersect(Vector2 line1From, Vector2 line1To, Vector2 line2From, Vector2 line2To)
        {

            Vector2 direction1 = line1To - line1From;
            Vector2 direction2 = line2To - line2From;

            float dotPerp = (direction1.X * direction2.Y) - (direction1.Y * direction2.X);
            // If it's 0, it means the lines are parallel so have infinite intersection points
            if (IsNearZero(dotPerp))
                return null;

            Vector2 c = line2From - line1From;
            float t = (c.X * direction2.Y - c.Y * direction2.X) / dotPerp;

            //	Return the intersection point
            return line1From + (t * direction1);

        }


        //TODO we might already have these elsewhere in farseer .. but they are small 
        //see if really necessary
        private static bool IsInside(Edge edge, Vector2 test)
        {
            bool? isLeft = IsLeftOf(edge, test);

            if (isLeft == null)
                return true;   //	Colinear points should be considered inside   

            return !isLeft.Value;
        }

        //NOTE better to use the Vertices.IsCounterClockwise, this thing allocated Edges on the heap ..  these  came from the  Rosetta code for Sutherland
        //not exaclty optimal c# code like farseer , which is meant for phone games.    Also, did not work with simple waves while farseer did report clockwise when in fact it was
        /*     private static bool IsClockwise(Vertices polygon)
             {
                 for (int cntr = 2; cntr < polygon.Count; cntr++)
                 {
                     bool? isLeft = IsLeftOf(new Edge(polygon[0], polygon[1]), polygon[cntr]);
                
                     if (isLeft != null)		//	some of the points may be colinear.  That's ok as long as the overall is a polygon  
                         return !isLeft.Value;
                 }

                 throw new ArgumentException("All the points in the polygon are colinear");
             }
             */
        //NOTE better to use the Vertices.IsCounterClockwise, this thing allocated Edges on the heap ..
        private static bool IsClockwise(Vector2[] polygon)
        {
            for (int cntr = 2; cntr < polygon.Length; cntr++)
            {
                bool? isLeft = IsLeftOf(new Edge(polygon[0], polygon[1]), polygon[cntr]);

                if (isLeft != null)		//	some of the points may be colinear.  That's ok as long as the overall is a polygon  
                    return !isLeft.Value;
            }

            throw new ArgumentException("All the points in the polygon are colinear");
        }
        /// <summary>
        /// Tells if the test point lies on the left side of the edge line
        /// </summary>
        private static bool? IsLeftOf(Edge edge, Vector2 test)
        {
            Vector2 tmp1 = edge.To - edge.From;
            Vector2 tmp2 = test - edge.To;

            double x = (tmp1.X * tmp2.Y) - (tmp1.Y * tmp2.X);		//	dot product of perpendicular?

            if (x < 0)
                return false;
            else if (x > 0)
                return true;
            else
                return null;  //	Colinear points;
        }

        private static bool IsNearZero(double testValue)
        {
            return Math.Abs(testValue) <= .000000001d;
        }

        #endregion
    }
}