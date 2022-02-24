using FarseerPhysics.Common;
using Core.Data.Geometry;
using Farseer.Xna.Framework;
using System.Collections.Generic;
//using System.Drawing;

using System;
using Vector2 = Farseer.Xna.Framework.Vector2;
using FarseerPhysics.Collision;

namespace Core.Game.MG.Drawing
{
/// <summary>
///Class to centralize 2D shape creation, by providing visual shape, corner 
 ///vertices, or control Vector2s. Most shapes are created and centered at (0,0),
 ///but not necessarily at their centroid.
/// </summary>
public class ShapeFactory
    {
#region Shapes vertices creation
        /* Vertices are normally used to create polygon shapes. Vertices created
         * here usually make (0,0) as its center or centroid.
         */

       // simple version of Vertices.CreateRectangle
        public static Vertices CreateRectangle(float width, float height)
        {
            Vertices vs = new Vertices();
            float wh = width * 0.5f;
            float hh = height * 0.5f;
            vs.Add(new Vector2(-wh, -hh));
            vs.Add(new Vector2(-wh, hh));
            vs.Add(new Vector2(wh, hh));
            vs.Add(new Vector2(wh, -hh));

            return vs;
        }

        /// <summary>
        /// Create a circle with rough surface, and an optional hole inside.
        /// </summary>
        /// <param name="radius">circle radius</param>
        /// <param name="numberOfEdges">number of edge to create circle shape</param>
        /// <param name="startAngle">start angle of circle arc, in degree, calculated 
        /// from top position (0 degree), clockwise</param>
        /// <param name="endAngle"> end angle of circle arc, in degree, calculated
        /// from top position (0 degree), clockwise</param>
        /// <param name="randSurfaceP">percentage of surface area that will be randomized.
        /// 0-100%. set to 0 to create a perfect circle without noise.</param>
        /// <param name="minElev">minimum elevation value when randomize</param>
        /// <param name="maxElev">maximum elevation value when randomize</param>
        /// <param name="horzDispP">maximum surface shear/horizontal displacement,
        /// precentage of max stepsize. 0-100%</param>
        /// <param name="holeRadius">radius of the hole inside circle, set to 0 
        /// to create a circle without hole.</param>
        /// <returns>vertices that represent the circle</returns>
        public static Vertices CreateCircleWNoise(float radius, int numberOfEdges,
            float startAngle, float endAngle, float randSurfaceP,
            float minElev, float maxElev, float horzDispP, float holeRadius)
        {
            float x, y, angle;
            float hx, hy;
            float surfaceAngle, elevation;
            Vertices vertices = new Vertices();
            Vertices innerVerts = new Vertices();

            // get circle arc to be generated, clamped to 360 degree max
            float arc = endAngle - startAngle;
            arc = MathUtils.Clamp(arc, 0, 360);
            float stepSize = MathHelper.ToRadians(arc) / numberOfEdges;
            startAngle = MathHelper.ToRadians(startAngle);  // convert it to rad

            // maximum horizontal displacement is half of stepsize
            horzDispP = MathUtils.Clamp(horzDispP, 0, 100) * 0.01f;
            horzDispP *= (stepSize * 0.5f);

            // get randomized but unique vertex index number. select a few from all 
            // available surface Vector2s.
            randSurfaceP = MathUtils.Clamp(randSurfaceP, 0, 100) * 0.01f;
            int numOfRandomVector2 = (int)(numberOfEdges * randSurfaceP);
            List<int> randomIdxList = GeomUtility.GetRandListNoDuplicate(
                0, numberOfEdges, numOfRandomVector2);

            // marker to help differentiate between randomized and non-randomized
            // Vector2s in a loop.
            bool[] Vector2Marker = new bool[numberOfEdges];
            for (int i = 0; i < numberOfEdges; i++) 
                Vector2Marker[i] = false;
            foreach (int i in randomIdxList) 
                Vector2Marker[i] = true;

            // to make angle calculation start from top position (0 angle)
            startAngle -= MathHelper.ToRadians(90);

            for (int i = 0; i < numberOfEdges; i++)
            {
                angle = startAngle + stepSize * i;
                if (Vector2Marker[i] == true)
                {
                    // randomize the angle and radius a bit
                    surfaceAngle = angle + MathUtils.RandomNumber(-horzDispP, horzDispP);
                    elevation = radius + MathUtils.RandomNumber(minElev, maxElev);
                }
                else
                {
                    surfaceAngle = angle;
                    elevation = radius;
                }

                // get the surface Vector2
                y = elevation * MathUtils.Sin(surfaceAngle);
                x = elevation * MathUtils.Cos(surfaceAngle);
                vertices.Add(new Vector2(x, y));

                if (holeRadius > 0)
                {
                    // create Vector2 for inner hole area
                    hy = holeRadius * MathUtils.Sin(angle);
                    hx = holeRadius * MathUtils.Cos(angle);
                    innerVerts.Add(new Vector2(hx, hy));
                }
            }

            if (holeRadius > 0)
            {
                // if full circle is used, duplicate first Vector2 to last
                if (arc == 360)
                {
                    vertices.Add(vertices[0]);
                    innerVerts.Add(innerVerts[0]);
                }

                // add vertices from inner to surface, but reverse the inner first 
                // to make a convex polygon later.
                innerVerts.Reverse();
                vertices.AddRange(innerVerts);
            }
            else
            {
                // when circle doesn't have hole but arc is not full 360, add
                // vertex from the circle center
                if (arc != 360) vertices.Add(Vector2.Zero);
            }

            return vertices;
        }


        public static Vertices CreateTriangle(float width, float height)
        {
            Vertices vs = new Vertices();
            float wh = width * 0.5f;
            float hh = height * 0.5f;
            vs.Add(new Vector2(wh, hh));
            vs.Add(new Vector2(0, -hh));
            vs.Add(new Vector2(-wh, hh));

            return vs;
        }


        /// <summary>
        /// Create triangle which centered on its bottom-middle Vector2.
        /// </summary>
        /// <param name="width">width of trangle</param>
        /// <param name="height">positive is down</param>
        /// <returns></returns>
        public static Vertices CreateIsoscelesTriangle(float width, float height)
        {
            Vertices vs = new Vertices();
            float wh = width * 0.5f;
            vs.Add(new Vector2(wh, 0));
            vs.Add(new Vector2(0, height));
            vs.Add(new Vector2(-wh, 0));
            return vs;
        }


        public static Vertices CreateRippleRect(float width, float height, int mountainCount, float slope)
        {
            Vertices vs = new Vertices();
            float wh = width * .5f;
            float hh = height * .5f;
            vs.Add(new Vector2(-wh - slope, 0.0f));
            vs.Add(new Vector2(-wh, hh));

            int b = mountainCount - 1;
            int u = b - 1;
            float distance = width / (b + u + 1);
            float xpos = -wh + distance;

            for (int i = 0; i < b + u; i++)
            {
                float h = (i % 2 == 0) ? hh - slope : hh;
                Vector2 vec = new Vector2(xpos, h);
                xpos += distance;
                vs.Add(vec);
            }
            vs.Add(new Vector2(wh, hh));
            vs.Add(new Vector2(wh + slope, 0.0f));
            vs.Add(new Vector2(wh, -hh));

            xpos = wh - distance;
            for (int i = 0; i < b + u; i++)
            {
                float h = (i % 2 == 0) ? -hh + slope : -hh;
                Vector2 vec = new Vector2(xpos, h);
                xpos -= distance;
                vs.Add(vec);
            }
            vs.Add(new Vector2(-wh, -hh));

            return vs;
        }

        public static Vertices CreateDiamond(float width, float height)
        {
            Vertices vs = new Vertices();
            float wh = width * 0.5f;
            float hh = height * 0.5f;
            vs.Add(new Vector2(0, -hh));
            vs.Add(new Vector2(-wh, 0));
            vs.Add(new Vector2(0, hh));
            vs.Add(new Vector2(wh, 0));

            return vs;
        }

        #endregion


        public static PolygonView CreatePolygonView(Microsoft.Xna.Framework.Game game, Vertices vertices, double thickness)
        {
            if (vertices == null) 
                throw new ArgumentNullException("vertices");

            PolygonView p = new PolygonView(game,vertices);
  
            return p;
        }

  


 
        public static Vertices CreateVertices(IList<Vector2> Vector2s)
        {
            Vertices pc = new Vertices();

            foreach (Vector2 v in Vector2s)
            {
                pc.Add(v);
            }
            return pc;
        }

     
/*

        // create circle shape that centered on (0,0)
        public static Ellipse CreateCircleShape(float radius, double thickness)
        {
            return CreateEllipseShape(radius, radius, thickness);
        }


        // create circle shape that centered on (0,0)
        public static Ellipse CreateEllipseShape(float radiusX, float radiusY, double thickness)
        {

        }


        // create rectangle shape that centered on (0,0)
        public static Rectangle CreateRectangleShape(float width, float height, double thickness)
        {
   
            return r;
        }*/

    }
        /// <summary>
        /// This class is similar to GeomUtility, except that any reference to 
        /// System.Windows is allowed here.
        /// </summary> 
        public class ShapeUtility
        {
            /// <summary>
            /// Default line thickness to be used when creating ObjectView. 
            /// </summary>
            public static float DefaultLineThickness = 0.00f;


#if GRAPHICS_MG  //TODO ERASE I THINK GOT MOVED SOMEWEHRE ELSE
        /// <summary>
        /// Get scale to resize 1st rectangle to enclose 2nd rectangle.
        /// </summary>
        /// <param name="s1">Size of 1st rectangle, the one that want to be resized</param>
        /// <param name="s2">Size of 2nd rectangle</param>
        /// <returns>Scale of: (Enclosing size) / (current size), of 1st rectangle.</returns>
        public static double  (Size s1, Size s2)
            {
                double scaleX = s2.Width / s1.Width;
                double scaleY = s2.Height / s1.Height;
                return Math.Max(scaleX, scaleY);
            }


#endif

        public static AABB RectToAABB(RectangleF rect)
        {
            Vector2 min = new Vector2((float)rect.X, (float)rect.Y);
            Vector2 max = new Vector2(
                (float)(rect.X + rect.Width), (float)(rect.Y + rect.Height));
            return new AABB(ref min, ref max);
        }


        // it might be best to not assume that top-left is the minimum, and vice versa
        public static RectangleF AABBToRect(AABB aabb)
        {
            return new RectangleF(aabb.LowerBound.X, aabb.LowerBound.Y,
                            aabb.UpperBound.X-aabb.LowerBound.X, aabb.UpperBound.Y-aabb.LowerBound.Y);
        }



    }
}



