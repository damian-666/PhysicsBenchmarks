using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


#if CUBIC
namespace FarseerPhysics.Common {


    //  https://stackoverflow.com/questions/9489736/catmull-rom-curve-with-no-cusps-and-no-self-intersections/19283471#19283471
    //Catmull-rom curve with no cusps and no self-intersections

    //P. J. Barry and R. N. Goldman. A recursive evaluation algorithm for a class of catmull-rom splines. SIGGRAPH Computer Graphics, 22(4):199{204, 1988.

    /* This method will calculate the Catmull-Rom interpolation curve, returning
     * it as a list of Coord coordinate objects.  This method in particular
     * adds the first and last control points which are not visible, but required
     * for calculating the spline.
     *
     * @param coordinates The list of original straight line points to calculate
     * an interpolation from.
     * @param pointsPerSegment The integer number of equally spaced points to
     * return along each curve.  The actual distance between each
     * point will depend on the spacing between the control points.
     * @return The list of interpolated coordinates.
     * @param curveType Chordal (stiff), Uniform(floppy), or Centripetal(medium)
     * Clarifies whether the curve should use uniform, chordal
     * or centripetal curve types. Uniform can produce loops, chordal can
     * produce large distortions from the original lines, and centripetal is an
     * optimal balance without spaces.
     * @throws gov.ca.water.shapelite.analysis.CatmullRomException if
     * pointsPerSegment is less than 2.
     */


    using Coord = Vector2d;


     enum CatmullRomType
    {
       
        Uniform,
        Centripetal,
        Chordal,
  
    }
    


public static List<Coord> interpolate(List<Coord> coordinates, int pointsPerSegment, CatmullRomType curveType, bool isClosed)
    { 

    List<Coord> vertices = new List<Coord>(coordinates);



    if (pointsPerSegment < 2) {
        throw new ArgumentException("The pointsPerSegment parameter must be greater than 2, since 2 points is just the linear segment.");
    }

    // Cannot interpolate curves given only two points.  Two points
    // is best represented as a simple line segment.
    if (vertices.Count < 3) {
        return vertices;
    }

        // Test whether the shape is open or closed by checking to see if
        // the first point intersects with the last point.  M and Z are ignored.


    //bool  isClosed = vertices[0].intersects2D(vertices[vertices.Count-1]);




    if (isClosed) {
        // Use the second and second from last points as control points.
        // get the second point.
        Coord p2 = vertices[1];
            // get the point before the last point
         Coord pn1 = vertices[vertices.Count - 2];

        // insert the second from the last point as the first point in the list
        // because when the shape is closed it keeps wrapping around to
        // the second point.
        vertices.Insert(0, pn1);
        // add the second point to the end.
        vertices.Insert(vertices.Count,p2);
    } else {
        // The shape is open, so use control points that simply extend
        // the first and last segments

        // Get the change in x and y between the first and second coordinates.
        double dx = vertices[1].X - vertices[0].X;
        double dy = vertices[1].Y - vertices[0].Y;

        // Then using the change, extrapolate backwards to find a control point.
        double x1 = vertices[0].X - dx;
        double y1 = vertices[0].Y - dy;

        // Actaully create the start point from the extrapolated values.


      //  Coord start = new Coord(x1, y1, vertices[0].Z);     TODO lerp    1, 2   -t    (t-t)

        // Repeat for the end control point.
        int n = vertices.Count - 1;
        dx = vertices[X] - vertices.[n - 1].X;  //TODO continue and do TODO above
        dy = vertices[n].Y - vertices.get(n - 1).Y;
        double xn = vertices.get(n).X + dx;
        double yn = vertices.get(n).Y + dy;
        Coord end = new Coord(xn, yn);

        // insert the start control point at the start of the vertices list.
        vertices.add(0, start);

        // append the end control ponit to the end of the vertices list.
        vertices.add(end);
    }

    // Dimension a result list of coordinates. 
    List<Coord> result = new ArrayList<>();
    // When looping, remember that each cycle requires 4 points, starting
    // with i and ending with i+3.  So we don't loop through all the points.
    for (int i = 0; i < vertices.size() - 3; i++) {

        // Actually calculate the Catmull-Rom curve for one segment.
        List<Coord> points = interpolate(vertices, i, pointsPerSegment, curveType);
        // Since the middle points are added twice, once for each bordering
        // segment, we only add the 0 index result point for the first
        // segment.  Otherwise we will have duplicate points.
        if (result.size() > 0) {
            points.remove(0);
        }

        // Add the coordinates for the segment to the result list.
        result.addAll(points);
    }
    return result;

}


>>>>>>
/**
 * Given a list of control points, this will create a list of pointsPerSegment
 * points spaced uniformly along the resulting Catmull-Rom curve.
 *
 * @param points The list of control points, leading and ending with a 
 * coordinate that is only used for controling the spline and is not visualized.
 * @param index The index of control point p0, where p0, p1, p2, and p3 are
 * used in order to create a curve between p1 and p2.
 * @param pointsPerSegment The total number of uniformly spaced interpolated
 * points to calculate for each segment. The larger this number, the
 * smoother the resulting curve.
 * @param curveType Clarifies whether the curve should use uniform, chordal
 * or centripetal curve types. Uniform can produce loops, chordal can
 * produce large distortions from the original lines, and centripetal is an
 * optimal balance without spaces.
 * @return the list of coordinates that define the CatmullRom curve
 * between the points defined by index+1 and index+2.
 */
public static List<Coord> interpolate(List<Coord> points, int index, int pointsPerSegment, CatmullRomType curveType) {
    List<Coord> result = new ArrayList<>();
    double[] x = new double[4];
    double[] y = new double[4];
    double[] time = new double[4];
    for (int i = 0; i < 4; i++) {
        x[i] = points.get(index + i).X;
        y[i] = points.get(index + i).Y;
        time[i] = i;
    }

    double tstart = 1;
    double tend = 2;
    if (!curveType.equals(CatmullRomType.Uniform)) {
        double total = 0;
        for (int i = 1; i < 4; i++) {
            double dx = x[i] - x[i - 1];
            double dy = y[i] - y[i - 1];
            if (curveType.equals(CatmullRomType.Centripetal)) {
                total += Math.pow(dx * dx + dy * dy, .25);
            } else {
                total += Math.pow(dx * dx + dy * dy, .5);
            }
            time[i] = total;
        }
        tstart = time[1];
        tend = time[2];
    }
    double z1 = 0.0;
    double z2 = 0.0;
    if (!Double.isNaN(points.get(index + 1).Z)) {
        z1 = points.get(index + 1).Z;
    }
    if (!Double.isNaN(points.get(index + 2).Z)) {
        z2 = points.get(index + 2).Z;
    }
    double dz = z2 - z1;
    int segments = pointsPerSegment - 1;
    result.add(points.get(index + 1));
    for (int i = 1; i < segments; i++) {
        double xi = interpolate(x, time, tstart + (i * (tend - tstart)) / segments);
        double yi = interpolate(y, time, tstart + (i * (tend - tstart)) / segments);
        double zi = z1 + (dz * i) / segments;
        result.add(new Coord(xi, yi, zi));
    }
    result.add(points.get(index + 2));
    return result;
}

/**
 * Unlike the other implementation here, which uses the default "uniform"
 * treatment of t, this computation is used to calculate the same values but
 * introduces the ability to "parameterize" the t values used in the
 * calculation. This is based on Figure 3 from
 * http://www.cemyuksel.com/research/catmullrom_param/catmullrom.pdf
 *
 * @param p An array of double values of length 4, where interpolation
 * occurs from p1 to p2.
 * @param time An array of time measures of length 4, corresponding to each
 * p value.
 * @param t the actual interpolation ratio from 0 to 1 representing the
 * position between p1 and p2 to interpolate the value.
 * @return
 */
public static double interpolate(double[] p, double[] time, double t) {
    double L01 = p[0] * (time[1] - t) / (time[1] - time[0]) + p[1] * (t - time[0]) / (time[1] - time[0]);
    double L12 = p[1] * (time[2] - t) / (time[2] - time[1]) + p[2] * (t - time[1]) / (time[2] - time[1]);
    double L23 = p[2] * (time[3] - t) / (time[3] - time[2]) + p[3] * (t - time[2]) / (time[3] - time[2]);
    double L012 = L01 * (time[2] - t) / (time[2] - time[0]) + L12 * (t - time[0]) / (time[2] - time[0]);
    double L123 = L12 * (time[3] - t) / (time[3] - time[1]) + L23 * (t - time[1]) / (time[3] - time[1]);
    double C12 = L012 * (time[2] - t) / (time[2] - time[1]) + L123 * (t - time[1]) / (time[2] - time[1]);
    return C12;
}
//cached above?

#endif

#if simplerWAy


    @ted what if I have a y = 5 line and want to calculate the intersection point with the spline? Is this possible with interpolation? Thanks – aledalgrande Feb 5 '14 at 17:51 
 

     
Another question: why is the alpha coefficient 0.25 for the centripetal equation? – aledalgrande Feb 6 '14 at 18:19 
  
 
When calculating the euclidean distance, you already take the square root, like Sqrt(dxdx + dydy). This is the definition of the chordal case, where the euclidean distance is used to calculate the relative time content. This is the same as (dxdx+dydy)^.5. But this actually tends to make the spline too stiff, and gives quite a bit of distortion to the spline. So the centripital case then takes the square root of THAT, making it (dxdx+dydy)^.25, and is a compromise between the stiff chordal and floppy uniform case. – Ted Feb 6 '14 at 18:41 
 



 

There is a much easier and more efficient way to implement this which only requires you to compute your tangents using a different formula, without the need to implement the recursive evaluation algorithm of Barry and Goldman.

If you take the Barry-Goldman parametrization (referenced in Ted's answer) C(t) for the knots (t0,t1,t2,t3) and the control points (P0,P1,P2,P3), its closed form is pretty complicated, but in the end it's still a cubic polynomial in t when you constrain it to the interval (t1,t2). So all we need to describe it fully are the values and tangents at the two end points t1 and t2. If we work out these values (I did this in Mathematica), we find
C(t1)  = P1
C(t2)  = P2
C'(t1) = (P1 - P0) / (t1 - t0) - (P2 - P0) / (t2 - t0) + (P2 - P1) / (t2 - t1)
C'(t2) = (P2 - P1) / (t2 - t1) - (P3 - P1) / (t3 - t1) + (P3 - P2) / (t3 - t2)

We can simply plug this into the standard formula for computing a cubic spline with given values and tangents at the end points and we have our nonuniform Catmull-Rom spline. One caveat is that the above tangents are computed for the interval (t1,t2), so if you want to evaluate the curve in the standard interval (0,1), simply rescale the tangents by multiplying them with the factor (t2-t1).

I put a working C++ example on Ideone: http://ideone.com/NoEbVM

I'll also paste the code below.

using namespace std;

struct CubicPoly
{
    float c0, c1, c2, c3;

    float eval(float t)
    {
        float t2 = t*t;
        float t3 = t2 * t;
        return c0 + c1*t + c2*t2 + c3*t3;
    }
};

/*
 * Compute coefficients for a cubic polynomial
 *   p(s) = c0 + c1*s + c2*s^2 + c3*s^3
 * such that
 *   p(0) = x0, p(1) = x1
 *  and
 *   p'(0) = t0, p'(1) = t1.
 */
void InitCubicPoly(float x0, float x1, float t0, float t1, CubicPoly &p)
{
    p.c0 = x0;
    p.c1 = t0;
    p.c2 = -3*x0 + 3*x1 - 2*t0 - t1;
    p.c3 = 2*x0 - 2*x1 + t0 + t1;
}

// standard Catmull-Rom spline: interpolate between x1 and x2 with previous/following points x0/x3
// (we don't need this here, but it's for illustration)
void InitCatmullRom(float x0, float x1, float x2, float x3, CubicPoly &p)
{
    // Catmull-Rom with tension 0.5
    InitCubicPoly(x1, x2, 0.5f*(x2-x0), 0.5f*(x3-x1), p);
}

// compute coefficients for a nonuniform Catmull-Rom spline
void InitNonuniformCatmullRom(float x0, float x1, float x2, float x3, float dt0, float dt1, float dt2, CubicPoly &p)
{
    // compute tangents when parameterized in [t1,t2]
    float t1 = (x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1;
    float t2 = (x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2;

    // rescale tangents for parametrization in [0,1]
    t1 *= dt1;
    t2 *= dt1;

    InitCubicPoly(x1, x2, t1, t2, p);
}

struct Vec2D
{
    Vec2D(float _x, float _y) : x(_x), y(_y) {}
    float x, y;
};

float VecDistSquared(const Vec2D& p, const Vec2D& q)
{
    float dx = q.x - p.x;
    float dy = q.y - p.y;
    return dx*dx + dy*dy;
}

void InitCentripetalCR(const Vec2D& p0, const Vec2D& p1, const Vec2D& p2, const Vec2D& p3,
    CubicPoly &px, CubicPoly &py)
{
    float dt0 = powf(VecDistSquared(p0, p1), 0.25f);
    float dt1 = powf(VecDistSquared(p1, p2), 0.25f);
    float dt2 = powf(VecDistSquared(p2, p3), 0.25f);

    // safety check for repeated points
    if (dt1 < 1e-4f)    dt1 = 1.0f;
    if (dt0 < 1e-4f)    dt0 = dt1;
    if (dt2 < 1e-4f)    dt2 = dt1;

    InitNonuniformCatmullRom(p0.x, p1.x, p2.x, p3.x, dt0, dt1, dt2, px);
    InitNonuniformCatmullRom(p0.y, p1.y, p2.y, p3.y, dt0, dt1, dt2, py);
}


int main()
{
    Vec2D p0(0,0), p1(1,1), p2(1.1,1), p3(2,0);
    CubicPoly px, py;
    InitCentripetalCR(p0, p1, p2, p3, px, py);
    for (int i = 0; i <= 10; ++i)
        cout << px.eval(0.1f*i) << " " << py.eval(0.1f*i) << endl;
}


 


 
How exactly did you create that simplified formula? Where did t go? – ssb Jun 22 '14 at 10:36 
 

     
@ssb: The values C(t1), C(t2), C'(t1), C'(t2) that I computed are the result of evaluating
C(t) and its derivative, C'(t), at the two end points of the interval (t1,t2).
I computed them in Mathematica. 
Since the curve is a cubic polynomial 
in this interval and therefore has four degrees of freedom, 
these four values are enough to determine the curve completely. So we just need to plug these
four values into the standard formula for a cubic polynomial with given values and derivatives. – cfh Jun 23 '14 at 5:56 
 

       
 
So you put the giant expanded pyramid formula into mathematica with t1 and t2 substituted and it gave you those two formulas? @eriatarka84 – ssb Jun 23 '14 at 6:12  
 

       
 
@ssb: Basically, yes. Look here: gist.github.com/anonymous/0eedb67914f554ee9cb5 Sorry it came out a bit ugly, but you can paste it into Mathematica if you have it. – cfh Jun 23 '14 at 20:05 
 

       
 
I know it will sound silly, but how did you derive the equation for tangent using 3 points? I am referring to: t = (x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1? It seems as if you are doing simple vector sum (a + b = c; a + b - c = 0) but what is left (0) you consider as a tangent. – Red XIII Jan 2 '15 at 8:32 
 

 show 3 more comments 
 
 


 up vote1down vote
 

Here is an iOS version of Ted's code. I excluded the 'z' parts.

>>>
typedef enum {
    CatmullRomTypeUniform,
    CatmullRomTypeChordal,
    CatmullRomTypeCentripetal
} CatmullRomType ;



-(NSMutableArray *)interpolate:(NSArray *)coordinates withPointsPerSegment:(NSInteger)pointsPerSegment andType:(CatmullRomType)curveType {

    NSMutableArray *vertices = [[NSMutableArray alloc] initWithArray:coordinates copyItems:YES];

    if (pointsPerSegment < 3)
        return vertices;

    //start point
    CGPoint pt1 = [vertices[0] CGPointValue];
    CGPoint pt2 = [vertices[1] CGPointValue];

    double dx = pt2.x - pt1.x;
    double dy = pt2.y - pt1.y;

    double x1 = pt1.x - dx;
    double y1 = pt1.y - dy;

    CGPoint start = CGPointMake(x1*.5, y1);

    //end point
    pt2 = [vertices[vertices.count-1] CGPointValue];
    pt1 = [vertices[vertices.count-2] CGPointValue];

    dx = pt2.x - pt1.x;
    dy = pt2.y - pt1.y;

    x1 = pt2.x + dx;
    y1 = pt2.y + dy;

    CGPoint end = CGPointMake(x1, y1);

    [vertices insertObject:[NSValue valueWithCGPoint:start] atIndex:0];
    [vertices addObject:[NSValue valueWithCGPoint:end]];

    NSMutableArray *result = [[NSMutableArray alloc] init];

    for (int i = 0; i < vertices.count - 3; i++) {
        NSMutableArray *points = [self interpolate:vertices forIndex:i withPointsPerSegment:pointsPerSegment andType:curveType];

        if ([points count] > 0)
            [points removeObjectAtIndex:0];

        [result addObjectsFromArray:points];
    }

    return result;
}
>>>>>>>>>>>>>>  check in lerp code if its the same

-(double)interpolate:(double*)p  time:(double*)time t:(double) t {
    double L01 = p[0] * (time[1] - t) / (time[1] - time[0]) + p[1] * (t - time[0]) / (time[1] - time[0]);
    double L12 = p[1] * (time[2] - t) / (time[2] - time[1]) + p[2] * (t - time[1]) / (time[2] - time[1]);
    double L23 = p[2] * (time[3] - t) / (time[3] - time[2]) + p[3] * (t - time[2]) / (time[3] - time[2]);
    double L012 = L01 * (time[2] - t) / (time[2] - time[0]) + L12 * (t - time[0]) / (time[2] - time[0]);
    double L123 = L12 * (time[3] - t) / (time[3] - time[1]) + L23 * (t - time[1]) / (time[3] - time[1]);
    double C12 = L012 * (time[2] - t) / (time[2] - time[1]) + L123 * (t - time[1]) / (time[2] - time[1]);
    return C12;
    }

-(NSMutableArray*)interpolate:(NSArray *)points forIndex:(NSInteger)index withPointsPerSegment:(NSInteger)pointsPerSegment andType:(CatmullRomType)curveType {
    NSMutableArray *result = [[NSMutableArray alloc] init];

    double x[4];
    double y[4];
    double time[4];

    for (int i=0; i < 4; i++) {
        x[i] = [points[index+i] CGPointValue].x;
        y[i] = [points[index+i] CGPointValue].y;
        time[i] = i;
    }

    double tstart = 1;
    double tend = 2;

    if (curveType != CatmullRomTypeUniform) {
        double total = 0;

        for (int i=1; i < 4; i++) {
            double dx = x[i] - x[i-1];
            double dy = y[i] - y[i-1];

            if (curveType == CatmullRomTypeCentripetal) {
                total += pow(dx * dx + dy * dy, 0.25);
            }
            else {
                total += pow(dx * dx + dy * dy, 0.5); //sqrt
            }
            time[i] = total;
        }
        tstart = time[1];
        tend = time[2];
    }

    int segments = pointsPerSegment - 1;

    [result addObject:points[index+1]];

    for (int i =1; i < segments; i++) {
        double xi = [self interpolate:x time:time t:tstart + (i * (tend - tstart)) / segments];
        double yi = [self interpolate:y time:time t:tstart + (i * (tend - tstart)) / segments];
        NSLog(@"(%f,%f)",xi,yi);
        [result addObject:[NSValue valueWithCGPoint:CGPointMake(xi, yi)]];
    }
    [result addObject:points[index+2]];

    return result;
}

<<<<<<<<<<<<<<<<<<

Also, here is a method for turning an array of points into a Bezier path for drawing, using the above
-(UIBezierPath*)bezierPathFromPoints:(NSArray *)points withGranulaity:(NSInteger)granularity
{
    UIBezierPath __block *path = [[UIBezierPath alloc] init];

    NSMutableArray *curve = [self interpolate:points withPointsPerSegment:granularity andType:CatmullRomTypeCentripetal];

    CGPoint __block p0 = [curve[0] CGPointValue];

    [path moveToPoint:p0];

    //use this loop to draw lines between all points
    for (int idx=1; idx < [curve count]; idx+=1) {
        CGPoint c1 = [curve[idx] CGPointValue];

        [path addLineToPoint:c1];
    };

    //or use this loop to use actual control points (less smooth but probably faster)
//    for (int idx=0; idx < [curve count]-3; idx+=3) {
//        CGPoint c1 = [curve[idx+1] CGPointValue];
//        CGPoint c2 = [curve[idx+2] CGPointValue];
//        CGPoint p1 = [curve[idx+3] CGPointValue];
//
//        [path addCurveToPoint:p1 controlPoint1:c1 controlPoint2:c2];
//    };

    return path;
}

/////
using namespace std;

struct CubicPoly
{
	float c0, c1, c2, c3;
	
	float eval(float t)
	{
		float t2 = t*t;
		float t3 = t2 * t;
		return c0 + c1*t + c2*t2 + c3*t3;
	}
};

/*
 * Compute coefficients for a cubic polynomial
 *   p(s) = c0 + c1*s + c2*s^2 + c3*s^3
 * such that
 *   p(0) = x0, p(1) = x1
 *  and
 *   p'(0) = t0, p'(1) = t1.
 */
void InitCubicPoly(float x0, float x1, float t0, float t1, CubicPoly &p)
{
    p.c0 = x0;
    p.c1 = t0;
    p.c2 = -3*x0 + 3*x1 - 2*t0 - t1;
    p.c3 = 2*x0 - 2*x1 + t0 + t1;
}

// standard Catmull-Rom spline: interpolate between x1 and x2 with previous/following points x1/x4
// (we don't need this here, but it's for illustration)
void InitCatmullRom(float x0, float x1, float x2, float x3, CubicPoly &p)
{
	// Catmull-Rom with tension 0.5
    InitCubicPoly(x1, x2, 0.5f*(x2-x0), 0.5f*(x3-x1), p);
}

// compute coefficients for a nonuniform Catmull-Rom spline
void InitNonuniformCatmullRom(float x0, float x1, float x2, float x3, float dt0, float dt1, float dt2, CubicPoly &p)
{
    // compute tangents when parameterized in [t1,t2]
    float t1 = (x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1;
    float t2 = (x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2;

    // rescale tangents for parametrization in [0,1]
    t1 *= dt1;
    t2 *= dt1;

    InitCubicPoly(x1, x2, t1, t2, p);
}

struct Vec2D
{
	Vec2D(float _x, float _y) : x(_x), y(_y) {}
	float x, y;
};

float VecDistSquared(const Vec2D& p, const Vec2D& q)
{
	float dx = q.x - p.x;
	float dy = q.y - p.y;
	return dx*dx + dy*dy;
}

void InitCentripetalCR(const Vec2D& p0, const Vec2D& p1, const Vec2D& p2, const Vec2D& p3,
	CubicPoly &px, CubicPoly &py)
{
    float dt0 = powf(VecDistSquared(p0, p1), 0.25f);
    float dt1 = powf(VecDistSquared(p1, p2), 0.25f);
    float dt2 = powf(VecDistSquared(p2, p3), 0.25f);

	// safety check for repeated points
    if (dt1 < 1e-4f)    dt1 = 1.0f;
    if (dt0 < 1e-4f)    dt0 = dt1;
    if (dt2 < 1e-4f)    dt2 = dt1;

	InitNonuniformCatmullRom(p0.x, p1.x, p2.x, p3.x, dt0, dt1, dt2, px);
	InitNonuniformCatmullRom(p0.y, p1.y, p2.y, p3.y, dt0, dt1, dt2, py);
}


int main()
{
	Vec2D p0(0,0), p1(1,1), p2(1.1,1), p3(2,0);
	CubicPoly px, py;
	InitCentripetalCR(p0, p1, p2, p3, px, py);
	for (int i = 0; i <= 10; ++i)
		cout << px.eval(0.1f*i) << " " << py.eval(0.1f*i) << endl;
}

#endif


#if TODOFUTURE //  eval    Mathnet, and the othe spline spirt using a spline draw tool    use draw tool to test curve key also..
//convert to path..

///note   for drawing we need a general... Centripetal

//for animation it is a functin.. not sure it that helps.
//need it to touch the dot or close

//need to see   and presample..this one is good

//general math lib that can presample buffers, there are some..  for ODE..
//MAth.net...   but consider more visual processing... shaders..etc.
//cudaFy..



namespace FarseerPhysics.Common
{
   

    using Coord = Vector2d;

    class CatmullRomCurve
    {




  
        /**
 * This method will calculate the Catmull-Rom interpolation curve, returning
 * it as a list of Coord coordinate objects.  This method in particular
 * adds the first and last control points which are not visible, but required
 * for calculating the spline.
 *
 * @param coordinates The list of original straight line points to calculate
 * an interpolation from.
 * @param pointsPerSegment The integer number of equally spaced points to
 * return along each curve.  The actual distance between each
 * point will depend on the spacing between the control points.
 * @return The list of interpolated coordinates.
 * @param curveType Chordal (stiff), Uniform(floppy), or Centripetal(medium)
 * @throws gov.ca.water.shapelite.analysis.CatmullRomException if
 * pointsPerSegment is less than 2.
 */
       


///   more robust way ...
        public static List<Coord> interpolate(List<Vector2d> coordinates, int pointsPerSegment, CatmullRomType curveType)  
        {
            List<Vector2d> vertices = new List<Vector2d>();

         
               vertices.AddRange(coordinates);
            
    if (pointsPerSegment< 2) {
        throw new CatmullRomException("The pointsPerSegment parameter must be greater than 2, since 2 points is just the linear segment.");
}

    // Cannot interpolate curves given only two points.  Two points
    // is best represented as a simple line segment.
    if (vertices.Count < 3) {
        return vertices;
    }

    // Test whether the shape is open or closed by checking to see if
    // the first point intersects with the last point.  M and Z are ignored.
    bool isClosed = vertices.get(0).intersects2D(vertices.get(vertices.Count - 1));
    if (isClosed) {
        // Use the second and second from last points as control points.
        // get the second point.
        Coord p2 = vertices[1].copy();
// get the point before the last point
Coord pn1 = vertices[(vertices.Count - 2)].copy();

// insert the second from the last point as the first point in the list
// because when the shape is closed it keeps wrapping around to
// the second point.
vertices.Insert(0, pn1);
        // add the second point to the end.
        vertices.Add(p2);
    } else {
        // The shape is open, so use control points that simply extend
        // the first and last segments

        // Get the change in x and y between the first and second coordinates.
        double dx = vertices[1].X - vertices[0].X;
double dy = vertices.[1].Y - vertices[0].Y;

// Then using the change, extrapolate backwards to find a control point.
double x1 = vertices[0].X - dx;
double y1 = vertices[0].Y - dy;

// Actaully create the start point from the extrapolated values.
Coord start = new Coord(x1, y1, vertices[0].Z);

// Repeat for the end control point.
int n = vertices.Count - 1;
dx = vertices.get(n).X - vertices.get(n - 1).X;
        dy = vertices.get(n).Y - vertices.get(n - 1).Y;
        double xn = vertices.get(n).X + dx;
double yn = vertices.get(n).Y + dy;
Coord end = new Coord(xn, yn);

// insert the start control point at the start of the vertices list.
vertices.add(0, start);

        // append the end control ponit to the end of the vertices list.
        vertices.add(end);
    }

    // Dimension a result list of coordinates. 
    List<Coord> result = new ArrayList<>();
    // When looping, remember that each cycle requires 4 points, starting
    // with i and ending with i+3.  So we don't loop through all the points.
    for (int i = 0; i<vertices.size() - 3; i++) {

        // Actually calculate the Catmull-Rom curve for one segment.
        List<Coord> points = interpolate(vertices, i, pointsPerSegment, curveType);
        // Since the middle points are added twice, once for each bordering
        // segment, we only add the 0 index result point for the first
        // segment.  Otherwise we will have duplicate points.
        if (result.size() > 0) {
            points.remove(0);
        }

        // Add the coordinates for the segment to the result list.
        result.addAll(points);
    }
    return result;

}

/**
 * Given a list of control points, this will create a list of pointsPerSegment
 * points spaced uniformly along the resulting Catmull-Rom curve.
 *
 * @param points The list of control points, leading and ending with a 
 * coordinate that is only used for controling the spline and is not visualized.
 * @param index The index of control point p0, where p0, p1, p2, and p3 are
 * used in order to create a curve between p1 and p2.
 * @param pointsPerSegment The total number of uniformly spaced interpolated
 * points to calculate for each segment. The larger this number, the
 * smoother the resulting curve.
 * @param curveType Clarifies whether the curve should use uniform, chordal
 * or centripetal curve types. Uniform can produce loops, chordal can
 * produce large distortions from the original lines, and centripetal is an
 * optimal balance without spaces.
 * @return the list of coordinates that define the CatmullRom curve
 * between the points defined by index+1 and index+2.
 */
public static List<Coord> interpolate(List<Coord> points, int index, int pointsPerSegment, CatmullRomType curveType)
{
    List<Coord> result = new List<>();
    double[] x = new double[4];
    double[] y = new double[4];
    double[] time = new double[4];
    for (int i = 0; i < 4; i++)
    {
        x[i] = points.get(index + i).X;
        y[i] = points.get(index + i).Y;
        time[i] = i;
    }

    double tstart = 1;
    double tend = 2;
    if (!curveType.equals(CatmullRomType.Uniform))
    {
        double total = 0;
        for (int i = 1; i < 4; i++)
        {
            double dx = x[i] - x[i - 1];
            double dy = y[i] - y[i - 1];
            if (curveType.equals(CatmullRomType.Centripetal))
            {
                total += Math.pow(dx * dx + dy * dy, .25);
            }
            else
            {
                total += Math.pow(dx * dx + dy * dy, .5);
            }
            time[i] = total;
        }
        tstart = time[1];
        tend = time[2];
    }
    double z1 = 0.0;
    double z2 = 0.0;
    if (!Double.isNaN(points.get(index + 1).Z))
    {
        z1 = points.get(index + 1).Z;
    }
    if (!Double.isNaN(points.get(index + 2).Z))
    {
        z2 = points.get(index + 2).Z;
    }
    double dz = z2 - z1;
    int segments = pointsPerSegment - 1;
    result.add(points.get(index + 1));
    for (int i = 1; i < segments; i++)
    {
        double xi = interpolate(x, time, tstart + (i * (tend - tstart)) / segments);
        double yi = interpolate(y, time, tstart + (i * (tend - tstart)) / segments);
        double zi = z1 + (dz * i) / segments;
        result.add(new Coord(xi, yi, zi));
    }
    result.add(points.get(index + 2));
    return result;
}

/**  might be worth trying   ( FUNCTION OF TIME) 
 * Unlike the other implementation here, which uses the default "uniform"
 * treatment of t, this computation is used to calculate the same values but
 * introduces the ability to "parameterize" the t values used in the
 * calculation. This is based on Figure 3 from
 * http://www.cemyuksel.com/research/catmullrom_param/catmullrom.pdf
 *
 * @param p An array of double values of length 4, where interpolation
 * occurs from p1 to p2.
 * @param time An array of time measures of length 4, corresponding to each
 * p value.
 * @param t the actual interpolation ratio from 0 to 1 representing the
 * position between p1 and p2 to interpolate the value.
 * @return
 */
public static double interpolate(double[] p, double[] time, double t)
{
    double L01 = p[0] * (time[1] - t) / (time[1] - time[0]) + p[1] * (t - time[0]) / (time[1] - time[0]);
    double L12 = p[1] * (time[2] - t) / (time[2] - time[1]) + p[2] * (t - time[1]) / (time[2] - time[1]);
    double L23 = p[2] * (time[3] - t) / (time[3] - time[2]) + p[3] * (t - time[2]) / (time[3] - time[2]);
    double L012 = L01 * (time[2] - t) / (time[2] - time[0]) + L12 * (t - time[0]) / (time[2] - time[0]);
    double L123 = L12 * (time[3] - t) / (time[3] - time[1]) + L23 * (t - time[1]) / (time[3] - time[1]);
    double C12 = L012 * (time[2] - t) / (time[2] - time[1]) + L123 * (t - time[1]) / (time[2] - time[1]);
    return C12;
}   
    }
}
#endif

