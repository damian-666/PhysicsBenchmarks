using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VisibilityPolygon
{
    public class VisibilityPolygon<TPoint> where TPoint : struct
    {
        public PointAdapter<TPoint> _adapter;  //make public so we can use approximate and faster angle solver


        public VisibilityPolygon(PointAdapter<TPoint> adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            _adapter = adapter;

        }

        private const double Epsilon = 0.000001;

        private TPoint NewPoint(double x, double y)
        {
            return _adapter.Create(x, y);
        }

        private TPoint NewPoint(float x, float y)
        {
            return _adapter.Create(Convert.ToDouble(x), Convert.ToDouble(y));
        }

        private double X(TPoint point)
        {
            return _adapter.GetX(point);
        }

        private double Y(TPoint point)
        {
            return _adapter.GetY(point);
        }


        /// <summary>
        /// Compute the lit region centered a position.  A square using radius will be the bounds, so better fill the whole screen or use a fading circular brush
        /// </summary>
        /// <param name="position">light source center</param>
        /// <param name="bodies">the bodies in the lit region.. sharp shadows will be cast.  use multiple light and transparency to do softer shadows</param>
        /// <param name="radius">light reach, will be flattened to a square</param>
        /// <param name="polygonsOutput">a clockwise wound list of Points, can be use for polygon to draw, or clip</param>
        public void ComputeViewport(Vector2 position, IEnumerable<Body> bodies, float radius, IList<TPoint> polygonsOutput, bool breakCrosses = false)
        {
            ComputeViewport(NewPoint(position.X, position.Y), bodies, radius, polygonsOutput, breakCrosses);
        }



        //this will do a query , and give us a square finite light boundary
        public void ComputeViewport(TPoint position, IEnumerable<Body> bodies, float radius, IList<TPoint> polygonsOutput, bool breakCrosses = false)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            TPoint minCorner = NewPoint(X(position) - radius, Y(position) - radius);
            TPoint maxCorner = NewPoint(X(position) + radius, Y(position) + radius);
            List<Segment<TPoint>> segments = ConvertToSegments(bodies);

            if (breakCrosses)
            {
                segments = BreakIntersections(segments);
            }


            ComputeViewport(position, segments, minCorner, maxCorner, polygonsOutput);
        }




        //We are selecting inside viewport, but this is still useful as it will take the parts of a polygon that are part in 
        //and part  out of the viewport..

        //NOTE  this can be adapted to non rectangular  regions..    say .. the rocket ...which has pieces crossing the boundary
        //still best to use clip region or to pass in  line segment for all the bounds of the  rockets blast.

        //when using random jitter points , this code can help.  and remove intersections
        //use the jitter points to bound the polygon, they can make shadows...
        //or else, insert the pts into the sorted polygon

        //this does a cull, on the viewport.   to specify a radial light source ,one still needs to add two lines for the beam, and some to flatten a circle..
        //using a radial brush this can be quite  rough.

        public void Compute(TPoint position, List<Segment<TPoint>> segments, IList<TPoint> polygonsOutput)
        {

            polygonsOutput.Clear();

            var bounded = new List<Segment<TPoint>>();
            var minX = X(position);
            var minY = Y(position);
            var maxX = X(position);
            var maxY = Y(position);
            for (var i = 0; i < segments.Count; ++i)
            {
                for (var j = 0; j < 2; ++j)
                {
                    minX = Math.Min(minX, X(segments[i][j]));
                    minY = Math.Min(minY, Y(segments[i][j]));
                    maxX = Math.Max(maxX, X(segments[i][j]));
                    maxY = Math.Max(maxY, Y(segments[i][j]));
                }
                //bounded.push([[segments[i][0][0], segments[i][0][1]], [segments[i][1][0], segments[i][1][1]]]);
                bounded.Add(new Segment<TPoint>(segments[i][0], segments[i][1]));
            }
            --minX;
            --minY;
            ++maxX;
            ++maxY;

            bounded.Add(new Segment<TPoint>(NewPoint(minX, minY), NewPoint(maxX, minY)));
            bounded.Add(new Segment<TPoint>(NewPoint(maxX, minY), NewPoint(maxX, maxY)));
            bounded.Add(new Segment<TPoint>(NewPoint(maxX, maxY), NewPoint(minX, maxY)));
            bounded.Add(new Segment<TPoint>(NewPoint(minX, maxY), NewPoint(minX, minY)));

            //   var polygon = new List<TPoint>();

            var polygon = polygonsOutput;

            var sorted = SortPoints(position, bounded);
            var map = new int[bounded.Count];
            for (var i = 0; i < map.Length; ++i)
            {
                map[i] = -1;
            }

            var heap = new List<int>();
            var start = NewPoint(X(position) + 1, Y(position));
            for (var i = 0; i < bounded.Count; ++i)
            {

                //  var a1 = pseudoAngle(bounded[i][0], position);    
                //   a1 = this.VectorAngle(bounded[i][0], position);
                //   a1 = AngleFlpY(bounded[i][0], position);

                //tryVisibilityPolygon.breakIntersections(segments);?  //test files in Media 1/22/2016

                var a1 = Angle(bounded[i][0], position);
                var a2 = Angle(bounded[i][1], position);


                var active = (a1 > -180.0 && a1 <= 0.0 && a2 <= 180.0 && a2 >= 0.0 && a2 - a1 > 180.0) ||
                             (a2 > -180.0 && a2 <= 0.0 && a1 <= 180.0 && a1 >= 0.0 && a1 - a2 > 180.0);
                if (active)
                {
                    Insert(i, heap, position, bounded, start, map);
                }
            }

            for (var i = 0; i < sorted.Length;)
            {
                var extend = false;
                var shorten = false;
                var orig = i;
                var vertex = bounded[sorted[i].SegmentIndex][sorted[i].PointIndex];
                var oldSegment = heap[0];
                do
                {
                    if (map[sorted[i].SegmentIndex] != -1)
                    {
                        if (sorted[i].SegmentIndex == oldSegment)
                        {
                            extend = true;
                            vertex = bounded[sorted[i].SegmentIndex][sorted[i].PointIndex];
                        }
                        Remove(map[sorted[i].SegmentIndex], heap, position, bounded, vertex, map);
                    }
                    else
                    {
                        Insert(sorted[i].SegmentIndex, heap, position, bounded, vertex, map);
                        if (heap[0] != oldSegment)
                        {
                            shorten = true;
                        }
                    }
                    ++i;
                    if (i >= sorted.Length)
                    {
                        break;
                    }
                } while (sorted[i].Angle < sorted[orig].Angle + Epsilon);

                if (extend)
                {
                    polygonsOutput.Add(vertex);
                    var cur = IntersectLines(bounded[heap[0]][0], bounded[heap[0]][1], position, vertex);
                    if (cur.HasValue && !Equal(cur.Value, vertex))
                    {
                        polygon.Add(cur.Value);
                    }
                }
                else if (shorten)
                {
                    var add1 = IntersectLines(bounded[oldSegment][0], bounded[oldSegment][1], position, vertex);
                    if (add1.HasValue)
                    {
                        polygon.Add(add1.Value);
                    }
                    var add2 = IntersectLines(bounded[heap[0]][0], bounded[heap[0]][1], position, vertex);
                    if (add2.HasValue)
                    {
                        polygon.Add(add2.Value);
                    }
                }
            }

        }


        //We are selecting inside viewport, but this is still useful as it will take the parts of a polygon that are part in 
        //and part  out of the viewport.. When zoomed in

        //NOTE  this can be adapted to non rectangular  regions..    say .. the rocket ...which has pieces crossing the boundary
        //still best to use clip region or to use a line segments for all the bounds of the  rockets blast.
        //or take the angle and insert the vert at the right spot..

        //this does a cull, on the viewport.   to specify a radial light source ,one still needs to add two lines for the beam, and some to flatten a circle..
        //using a radial brush this can be quite  rough.

        public void ComputeViewport(TPoint position, List<Segment<TPoint>> segments, TPoint viewportMinCorner,
          TPoint viewportMaxCorner, IList<TPoint> polygonsOutput)
        {

            polygonsOutput.Clear();

            var brokenSegments = new List<Segment<TPoint>>();
            var viewport = new List<TPoint>
            {
            viewportMinCorner,
            NewPoint(X(viewportMaxCorner), Y(viewportMinCorner)),
            viewportMaxCorner,
            NewPoint(X(viewportMinCorner), Y(viewportMaxCorner))
            };
            for (var i = 0; i < segments.Count; ++i)
            {
                if (X(segments[i][0]) < X(viewportMinCorner) && X(segments[i][1]) < X(viewportMinCorner))
                    continue;
                if (Y(segments[i][0]) < Y(viewportMinCorner) && Y(segments[i][1]) < Y(viewportMinCorner))
                    continue;
                if (X(segments[i][0]) > X(viewportMaxCorner) && X(segments[i][1]) > X(viewportMaxCorner))
                    continue;
                if (Y(segments[i][0]) > Y(viewportMaxCorner) && Y(segments[i][1]) > Y(viewportMaxCorner))
                    continue;

                var intersections = new List<TPoint>();
                for (var j = 0; j < viewport.Count; ++j)
                {
                    var k = j + 1;
                    if (k == viewport.Count)
                    {
                        k = 0;
                    }

                    if (DoLineSegmentsIntersect(
                      X(segments[i][0]),
                      Y(segments[i][0]),
                      X(segments[i][1]),
                      Y(segments[i][1]),
                      X(viewport[j]),
                      Y(viewport[j]),
                      X(viewport[k]),
                      Y(viewport[k])))
                    {
                        var intersect = IntersectLines(segments[i][0], segments[i][1], viewport[j], viewport[k]);
                        if (intersect == null)
                        {
                            continue;
                        }
                        if (Equal(intersect, segments[i][0]) || Equal(intersect, segments[i][1]))
                        {
                            continue;
                        }
                        intersections.Add(intersect.Value);
                    }
                }

                //var start = NewPoint(segments[i][0].X, segments[i][0].Y);
                var start = segments[i][0];
                while (intersections.Count > 0)
                {
                    var endIndex = 0;
                    var endDis = Distance(start, intersections[0]);
                    for (var j = 1; j < intersections.Count; ++j)
                    {
                        var dis = Distance(start, intersections[j]);
                        if (dis < endDis)
                        {
                            endDis = dis;
                            endIndex = j;
                        }
                    }
                    brokenSegments.Add(new Segment<TPoint>(start, intersections[endIndex]));
                    //start.X = intersections[endIndex].X;
                    //start.Y = intersections[endIndex].Y;
                    start = intersections[endIndex];
                    //intersections.splice(endIndex, 1);
                    Splice(intersections, endIndex, 1);
                }
                //brokenSegments.push([start, [segments[i][1][0], segments[i][1][1]]]);
                brokenSegments.Add(new Segment<TPoint>(start, segments[i][1]));
            }

            var viewportSegments = new List<Segment<TPoint>>();
            for (var i = 0; i < brokenSegments.Count; ++i)
            {
                if (InViewport(brokenSegments[i][0], viewportMinCorner, viewportMaxCorner) &&
                    InViewport(brokenSegments[i][1], viewportMinCorner, viewportMaxCorner))
                {
                    viewportSegments.Add(new Segment<TPoint>(brokenSegments[i][0], brokenSegments[i][1]));
                }
            }
            var eps = Epsilon * 10.0;
            viewportSegments.Add(new Segment<TPoint>(
              NewPoint(X(viewportMinCorner) - eps, Y(viewportMinCorner) - eps),
              NewPoint(X(viewportMaxCorner) + eps, Y(viewportMinCorner) - eps)
              ));
            viewportSegments.Add(new Segment<TPoint>(
              NewPoint(X(viewportMaxCorner) + eps, Y(viewportMinCorner) - eps),
              NewPoint(X(viewportMaxCorner) + eps, Y(viewportMaxCorner) + eps))
              );
            viewportSegments.Add(new Segment<TPoint>(
              NewPoint(X(viewportMaxCorner) + eps, Y(viewportMaxCorner) + eps),
              NewPoint(X(viewportMinCorner) - eps, Y(viewportMaxCorner) + eps)
              ));
            viewportSegments.Add(new Segment<TPoint>(
              NewPoint(X(viewportMinCorner) - eps, Y(viewportMaxCorner) + eps),
              NewPoint(X(viewportMinCorner) - eps, Y(viewportMinCorner) - eps)
              ));

            Compute(position, viewportSegments, polygonsOutput);
        }


        public bool InViewport(TPoint position, TPoint viewportMinCorner, TPoint viewportMaxCorner)
        {
            if (X(position) < X(viewportMinCorner) - Epsilon)
            {
                return false;
            }
            if (Y(position) < Y(viewportMinCorner) - Epsilon)
            {
                return false;
            }
            if (X(position) > X(viewportMaxCorner) + Epsilon)
            {
                return false;
            }
            if (Y(position) > Y(viewportMaxCorner) + Epsilon)
            {
                return false;
            }
            return true;
        }


        private TPoint? IntersectLines(TPoint a1, TPoint a2, TPoint b1, TPoint b2)
        {
            /*
            double uaT = (b2.X - b1.X) * (a1.Y - b1.Y) - (b2.Y - b1.Y) * (a1.X - b1.X);
            //var ubT = (a2.X - a1.X) * (a1.Y - b1.Y) - (a2.Y - a1.Y) * (a1.X - b1.X);
            double uB = (b2.Y - b1.Y) * (a2.X - a1.X) - (b2.X - b1.X) * (a2.Y - a1.Y);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (uB != 0.0) {
              double ua = uaT / uB;
              //var ub = ubT / uB;
              return NewPoint(a1.X - ua * (a1.X - a2.X), a1.Y - ua * (a1.Y - a2.Y));
            }
            return null;
            */

            // optimized version
            double dbx = X(b2) - X(b1), dby = Y(b2) - Y(b1), dax = X(a2) - X(a1), day = Y(a2) - Y(a1);
            var uB = dby * dax - dbx * day;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (uB != 0.0)
            {
                var ua = (dbx * (Y(a1) - Y(b1)) - dby * (X(a1) - X(b1))) / uB;
                return NewPoint(X(a1) - ua * -dax, Y(a1) - ua * -day);
            }
            return null;
        }


        private double Distance(TPoint a, TPoint b)
        {
            var dx = X(a) - X(b);
            var dy = Y(a) - Y(b);
            return dx * dx + dy * dy;
        }


        private static bool IsOnSegment(double xi, double yi, double xj, double yj, double xk, double yk)
        {
            return (xi <= xk || xj <= xk) && (xk <= xi || xk <= xj) &&
                   (yi <= yk || yj <= yk) && (yk <= yi || yk <= yj);
        }


        private static int ComputeDirection(double xi, double yi, double xj, double yj, double xk, double yk)
        {
            var a = (xk - xi) * (yj - yi);
            var b = (xj - xi) * (yk - yi);
            return a < b ? -1 : a > b ? 1 : 0;
        }


        private static bool DoLineSegmentsIntersect(double x1, double y1, double x2, double y2, double x3, double y3,
          double x4, double y4)
        {
            var d1 = ComputeDirection(x3, y3, x4, y4, x1, y1);
            var d2 = ComputeDirection(x3, y3, x4, y4, x2, y2);
            var d3 = ComputeDirection(x1, y1, x2, y2, x3, y3);
            var d4 = ComputeDirection(x1, y1, x2, y2, x4, y4);
            return (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                    ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) ||
                   (d1 == 0 && IsOnSegment(x3, y3, x4, y4, x1, y1)) ||
                   (d2 == 0 && IsOnSegment(x3, y3, x4, y4, x2, y2)) ||
                   (d3 == 0 && IsOnSegment(x1, y1, x2, y2, x3, y3)) ||
                   (d4 == 0 && IsOnSegment(x1, y1, x2, y2, x4, y4));
        }


        private static int Parent(int index)
        {
            //return Math.Floor((index - 1) / 2);      
            return (index - 1) / 2;
        }


        private static int Child(int index)
        {
            return 2 * index + 1;
        }


        private double Angle2(TPoint a, TPoint b, TPoint c)
        {
            var a1 = Angle(a, b);
            var a2 = Angle(b, c);
            var a3 = a1 - a2;
            if (a3 < 0.0)
            {
                a3 += 360.0;
            }
            if (a3 > 360.0)
            {
                a3 -= 360.0;
            }
            //a3 %= 360.0; // NOT equivalent
            return a3;
        }


        private SegmentPointAngle[] SortPoints(TPoint position, List<Segment<TPoint>> segments)
        {
            var segCount = segments.Count;
            var points = new SegmentPointAngle[segCount * 2];
            for (var i = 0; i < segCount; ++i)
            {
                for (var j = 0; j < 2; ++j)
                {
                    var a = Angle(segments[i][j], position);  //TODO use the Pseudoangle or ..a lookup table
                    points[2 * i + j] = new SegmentPointAngle(i, j, a);
                }
            }

            // sort by angle
            //points.sort(function(a,b) {return a[2]-b[2];});
            Array.Sort(points, (a, b) => a.Angle.CompareTo(b.Angle));

            return points;
        }


        private double Angle(TPoint a, TPoint b)
        {
#pragma warning disable 0162
            return Math.Atan2(Y(b) - Y(a), X(b) - X(a)) * 180.0 / Math.PI;   //angle is used for more than sorting
            return _adapter.GetPseudoAngle(a, b);
        }


        //  private double AngleFlpY(TPoint a, TPoint b)  //doesnt matter
        //  {
        //     return Math.Atan2(-Y(b) + Y(a), X(b) - X(a)) * 180.0 / Math.PI;
        //  }



        public double VectorAngle(TPoint p1, TPoint p2)  //farseer
        {
            double theta1 = Math.Atan2(Y(p1), X(p1));
            double theta2 = Math.Atan2(Y(p2), X(p2));
            double dtheta = theta2 - theta1;
            while (dtheta > Math.PI)
                dtheta -= (2 * Math.PI);
            while (dtheta < -Math.PI)
                dtheta += (2 * Math.PI);

            return (dtheta) * 180.0 / Math.PI;
        }

 
        double pseudoAngle(TPoint a, TPoint b)
        {
            return pseudoAngle1(X(b) - X(a), Y(b) - Y(a));
        }

        double pseudoAngle2(double dx, double dy)  //from optimization on other version of this algorithm
        {
            double ax = Math.Abs(dx);
            double ay = Math.Abs(dy);
            double p = dy / (ax + ay);
            if (dx < 0) p = 2 - p;
            else if (dy > 0) p = 4 + p;
            return p * 180.0 / Math.PI;
        }


        double pseudoAngle1(double dx, double dy)  //from optimization on other version of this algorithm
        {
            double ax = Math.Abs(dx);
            double ay = Math.Abs(dy);
            double p = dy / (ax + ay);
            if (dx < 0) p = 2 - p;

            else if (dy < 0) p = 4 + p;

            return p * 180 / System.Math.PI;
        }


        //	pseudo angle

        // sign(dy)(1-p),                 //this is supposed to be a one line version of above in C
        //	 copysign(1. - dx/(fabs(dx)+fabs(dy)),dy)
        //	 http://stackoverflow.com/questions/16542042/fastest-way-to-sort-vectors-by-angle-without-actually-computing-that-angle



        /*
                //Shadowplay Mod
                /// <summary>
                /// returns angle of incidence between two vectors.. because above function in VectorAngle is too confusing with its positive anticlosewise.. this uses the simple dot rule
                /// http://www.wikihow.com/Find-the-Angle-Between-Two-Vectors   cos angle  = a.b / |a||b|
                /// </summary>
                /// <param name="a">vector a</param>
                /// <param name="normal">normal vector , length must be one</param>
                /// <returns></returns>
                public static double AngleBetweenVectorAndNormal(Vector2 a, Vector2 normal)
                {
                    //  Debug.Assert(Math.Abs(normal.Length() - 1.0f) < 0.0001f);
                    return Math.Acos(Vector2.Dot(a, normal) / a.Length());
                }
        */

        //http://www.java-gaming.org/index.php?topic=14647.0


        //  https://www.researchgate.net/publication/259385247_Fast_computation_of_arctangent_functions_for_embedded_applications_A_comparative_analysis

/*
//SAME AS BELOW
        Well I found a way to do it about 50% faster but it's less accurate. 
The margin of error can be up to 4 degrees, but if speed is important
and accuracy doesn't need to be pin point, this will work. 

        private static final float ATAN2_CF1 = (float)(3.1415927f / 4f);
        private static final float ATAN2_CF2 = 3f * ATAN2_CF1;


       //   This is 50% faster than Math.atan2 in the emulator, but can have 
//an error of u
     // private static float fastatan2(float y, float x)
        {
            float abs_y = Math.abs(y);
            float angle;
            if (x >= 0)
            {
                float r = (x - abs_y) / (x + abs_y);
                angle = ATAN2_CF1 - ATAN2_CF1 * r;
            }
            else
            {
                float r = (x + abs_y) / (abs_y - x);
                angle = ATAN2_CF2 - ATAN2_CF1 * r;
            }
            return y < 0 ? -angle : angle; */

        //http://www.gamedev.net/topic/441464-manually-implementing-atan2-or-atan/   error can be 4 degrees.. 
        /*public double aTan2(double y, double x) {
        double coeff_1 = Math.PI / 4d;
        double coeff_2 = 3d * coeff_1;
        double abs_y = Math.abs(y);
        double angle;
        if (x >= 0d) {
        double r = (x - abs_y) / (x + abs_y);
        angle = coeff_1 - coeff_1 * r;
        } else {
        double r = (x + abs_y) / (abs_y - x);
        angle = coeff_2 - coeff_1 * r;
        }
        return y < 0d ? -angle : angle;
        }
        */
     //   http://math.stackexchange.com/questions/1098487/atan2-faster-approximation
/*
        a := min(|x|, |y|) / max(|x|, |y|)
s := a* a
r := ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s* a + a
if |y| > |x| then r := 1.57079637 - r
if x< 0 then r := 3.14159274 - r
if y< 0 then r := -r
*/

private bool Equal(TPoint? aNull, TPoint? bNull)
{
// fix
if (!aNull.HasValue || !bNull.HasValue)
{
    return false;
}

var a = aNull.Value;
var b = bNull.Value;
// end of fix

return Math.Abs(X(a) - X(b)) < Epsilon && Math.Abs(Y(a) - Y(b)) < Epsilon;
}


private bool LessThan(int index1, int index2, TPoint position, List<Segment<TPoint>> segments,
TPoint destination)
{
var inter1Null = IntersectLines(segments[index1][0], segments[index1][1], position, destination);
var inter2Null = IntersectLines(segments[index2][0], segments[index2][1], position, destination);

// fix
if (!inter1Null.HasValue || !inter2Null.HasValue)
{
    return false;
}
// end of fix

var inter1 = inter1Null.Value;
var inter2 = inter2Null.Value;
if (!Equal(inter1, inter2))
{
    var d1 = Distance(inter1, position);
    var d2 = Distance(inter2, position);
    return d1 < d2;
}

var end1 = 0;
if (Equal(inter1, segments[index1][0]))
{
    end1 = 1;
}

var end2 = 0;
if (Equal(inter2, segments[index2][0]))
{
    end2 = 1;
}

var a1 = Angle2(segments[index1][end1], inter1, position);
var a2 = Angle2(segments[index2][end2], inter2, position);
if (a1 < 180.0)
{
    if (a2 > 180.0)
    {
        return true;
    }
    return a2 < a1;
}
return a1 < a2;
}




public List<Segment<TPoint>> ConvertToSegments(IList<Vector2> verts)
{
var segments = new List<Segment<TPoint>>();
for (var j = 0; j < verts.Count(); ++j)
{
    var k = j + 1;
    if (k == verts.Count())
    {
        k = 0;
    }

    Vector2 v1 = verts[j];
    Vector2 v2 = verts[k];
    segments.Add(new Segment<TPoint>(NewPoint(v1.X, v1.Y), NewPoint(v2.X, v2.Y)));//mod 
}
return segments;
}



public List<Segment<TPoint>> ConvertToSegments(IEnumerable<Body> bodies)
{
if (bodies == null)
{
    throw new ArgumentNullException(nameof(bodies));
}

var segments = new List<Segment<TPoint>>();
foreach (var body in bodies)
{

     if (body.GeneralVertices == null) //circles (TODO this should not happen normally)
         continue;

    var verts = body.GeneralVertices;

    for (var j = 0; j < verts.Count; ++j)
    {

        var k = j + 1;
        if (k == verts.Count)
        {
            k = 0;
        }


        Vector2 v1 = body.GetWorldPoint(verts[j]);
        Vector2 v2 = body.GetWorldPoint(verts[k]);

        // segments.push([[polygons[i][j][0], polygons[i][j][1]], [polygons[i][k][0], polygons[i][k][1]]]);
        //    segments.Add(new Segment<TPoint>(polygon[j], polygon[k])); 
        //   segments.Add(new Segment<TPoint>(NewPoint(v1.X, v1.Y), NewPoint(v2.X, v2.Y)));//mod 


        segments.Add(new Segment<TPoint>(NewPoint(v1.X, v1.Y), NewPoint(v2.X, v2.Y)));//mod 

    }
}
return segments;
}


public bool InPolygon(TPoint position, List<TPoint> polygon)
{
if (polygon == null)
{
    throw new ArgumentNullException(nameof(polygon));
}

var val = X(polygon[0]);
foreach (var point in polygon)
{
    val = Math.Min(X(point), val);
    val = Math.Min(Y(point), val);
}

var edge = NewPoint(val - 1, val - 1);
var parity = 0;
for (var i = 0; i < polygon.Count; ++i)
{
    var j = i + 1;
    if (j == polygon.Count)
    {
        j = 0;
    }
    if (DoLineSegmentsIntersect(X(edge), Y(edge), X(position), Y(position), X(polygon[i]), Y(polygon[i]), X(polygon[j]),
      Y(polygon[j])))
    {
        // intersect should have a value if we got here
        var intersect = IntersectLines(edge, position, polygon[i], polygon[j]);
        if (Equal(position, intersect))
        {
            return true;
        }
        if (Equal(intersect, polygon[i]))
        {
            if (Angle2(position, edge, polygon[j]) < 180.0)
            {
                ++parity;
            }
        }
        else if (Equal(intersect, polygon[j]))
        {
            if (Angle2(position, edge, polygon[i]) < 180.0)
            {
                ++parity;
            }
        }
        else
        {
            ++parity;
        }
    }
}
return parity % 2 != 0;
}


private static void Splice<T>(List<T> source, int index, int count)
{
source.RemoveRange(index, count);
}


//NOTE... TODO if used on distorted or broken stuff.. like clouds.. or pieces.. we should call this...or see 
//if it gracefully fails
public List<Segment<TPoint>> BreakIntersections(List<Segment<TPoint>> segments)
{
var output = new List<Segment<TPoint>>();
for (var i = 0; i < segments.Count; ++i)
{
    var intersections = new List<TPoint>();
    for (var j = 0; j < segments.Count; ++j)
    {
        if (i == j)
        {
            continue;
        }
        if (DoLineSegmentsIntersect(X(segments[i][0]), Y(segments[i][0]), X(segments[i][1]), Y(segments[i][1]),
          X(segments[j][0]), Y(segments[j][0]), X(segments[j][1]), Y(segments[j][1])))
        {
            var intersect = IntersectLines(segments[i][0], segments[i][1], segments[j][0], segments[j][1]);
            if (intersect == null)
            {
                continue;
            }
            if (Equal(intersect.Value, segments[i][0]) || Equal(intersect.Value, segments[i][1]))
            {
                continue;
            }
            intersections.Add(intersect.Value);
        }
    }

    //TPoint start = NewPoint(segments[i][0].X, segments[i][0].Y);
    var start = segments[i][0];
    while (intersections.Count > 0)
    {
        var endIndex = 0;
        var endDis = Distance(start, intersections[0]);
        for (var j = 1; j < intersections.Count; ++j)
        {
            var dis = Distance(start, intersections[j]);
            if (dis < endDis)
            {
                endDis = dis;
                endIndex = j;
            }
        }
        output.Add(new Segment<TPoint>(start, intersections[endIndex]));
        start = intersections[endIndex];
        //intersections.splice(endIndex, 1);
        Splice(intersections, endIndex, 1);
    }
    // output.push([start, [segments[i][1][0], segments[i][1][1]]]);
    output.Add(new Segment<TPoint>(start, segments[i][1]));
}
return output;
}


private static int Pop(List<int> heap)
{
if (heap.Count > 0)
{
    var index = heap.Count - 1;
    var val = heap[index];
    heap.RemoveAt(index);
    return val;
}
//throw new InvalidOperationException();
return 0;
}


private void Remove(int index, List<int> heap, TPoint position, List<Segment<TPoint>> segments,
TPoint destination,
int[] map)
{
map[heap[index]] = -1;
if (index == heap.Count - 1)
{
    Pop(heap);
    return;
}
heap[index] = Pop(heap);
map[heap[index]] = index;
var cur = index;
var parent = Parent(cur);
if (cur != 0 && LessThan(heap[cur], heap[parent], position, segments, destination))
{
    while (cur > 0)
    {
        parent = Parent(cur);
        if (!LessThan(heap[cur], heap[parent], position, segments, destination))
        {
            break;
        }
        map[heap[parent]] = cur;
        map[heap[cur]] = parent;
        var temp = heap[cur];
        heap[cur] = heap[parent];
        heap[parent] = temp;
        cur = parent;
    }
}
else
{
    while (true)
    {
        var left = Child(cur);
        var right = left + 1;
        if (left < heap.Count && LessThan(heap[left], heap[cur], position, segments, destination) &&
            (right == heap.Count || LessThan(heap[left], heap[right], position, segments, destination)))
        {
            map[heap[left]] = cur;
            map[heap[cur]] = left;
            var temp = heap[left];
            heap[left] = heap[cur];
            heap[cur] = temp;
            cur = left;
        }
        else if (right < heap.Count && LessThan(heap[right], heap[cur], position, segments, destination))
        {
            map[heap[right]] = cur;
            map[heap[cur]] = right;
            var temp = heap[right];
            heap[right] = heap[cur];
            heap[cur] = temp;
            cur = right;
        }
        else
        {
            break;
        }
    }
}
}


private void Insert(int index, List<int> heap, TPoint position, List<Segment<TPoint>> segments,
TPoint destination,
int[] map)
{
var intersect = IntersectLines(segments[index][0], segments[index][1], position, destination);
if (intersect == null)
{
    return;
}
var cur = heap.Count;
heap.Add(index);
map[index] = cur;
while (cur > 0)
{
    var parent = Parent(cur);
    if (!LessThan(heap[cur], heap[parent], position, segments, destination))
    {
        break;
    }
    map[heap[parent]] = cur;
    map[heap[cur]] = parent;
    var temp = heap[cur];
    heap[cur] = heap[parent];
    heap[parent] = temp;
    cur = parent;
}
}

private class SegmentPointAngle
{
public SegmentPointAngle(int seg, int point, double a)
{
    SegmentIndex = seg;
    PointIndex = point;
    Angle = a;
}

public int SegmentIndex { get; }
public int PointIndex { get; }
public double Angle { get; }
}
}
}
 