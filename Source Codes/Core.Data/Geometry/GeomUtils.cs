/*
 * Collections of geometry-related functions.
 */
//#define STATICBUFFER for some reason this fails with invalid polygons like jittered clouds

using System;
using System.Collections.Generic;

using FarseerPhysics;
using FarseerPhysics.Factories;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using Farseer.Xna.Framework;
using System.Linq;

namespace Core.Data.Geometry
{
    // delegate for general 2D position & rotation update
    public delegate void UpdatePosDelegate(float x, float y, float r);


    public class GeomUtility
    {
        /// <summary>
        /// Find angle (in degree) from vector. Interface for 
        /// GetAngleFromVectorCartesian(x,-y). This is for graphics coordinate,
        /// where the y direction is reversed. 
        /// </summary>
        public static float GetAngleFromVector(float x, float y)
        {
            return GetAngleFromVectorCartesian(x, -y);
        }

        /// <summary>
        /// Find angle (in degree) from vector. Automatically converted to 
        /// 360-based degree, always return positive angle. The 0 angle is on
        /// 3 o'clock, positive value is calculated counterclockwise.
        /// </summary>
        public static float GetAngleFromVectorCartesian(float x, float y)
        {
            // here we obtain arctangen using arcsin, because we suspect the 
            // Math.Atan2() in silverlight is buggy. 
            double r = Math.Sqrt(x * x + y * y);
            double angle = MathHelper.ToDegrees((float)Math.Asin(y / r));

            if (x < 0)
                angle = 180 - angle;    // for quadrant 2 and 3
            else if (y < 0)
                angle = 360 + angle;    // for quadrant 4

            // return value is in range: 0 <= angle < 360
            return (float)angle;
        }

        /// <summary>
        /// Get angle (in degrees) between particular vector and vector pointed 
        /// to 12 o'clock. Positive = CCW, Negative = CW.
        /// </summary>
        /// <returns> Value between 0 to 360 </returns>
        public static float GetAngleFrom12ClockVector(float x, float y)
        {
            // here I assume using graphics coordinates
            float angle = GeomUtility.GetAngleFromVector(x, y);

            // obtained angle is still from 3 o'clock. we need from 12 o'clock.
            angle -= 90;

            // for consistency with others, always return value between 0 & 360
            if (angle < 0) angle += 360;

            return angle;
        }

        /// <summary>
        /// Angle is in degrees, calculated from 3 o'clock. 
        /// Positive = CCW, Negative = CW.
        /// </summary>
        public static Vector2 GetUnitVectorFromAngle(float angle)
        {
            double rad = MathHelper.ToRadians(angle);
            float y = (float)-Math.Sin(rad);    // graphics coord
            float x = (float)Math.Cos(rad);
            return new Vector2(x, y);
        }

        // Create 2D rotation matrix, for rotation with arbitrary center.
        // This method can be improved further to be more efficient.
        public static Matrix CreateRotationMatrix(Vector2 center, float dirAngle)
        {
            // rotation matrix. to origin first, then rotate, then translate back
            Matrix tlMatrix1, rotMatrix, tlMatrix2;
            Matrix.CreateTranslation(-center.X, -center.Y, 0, out tlMatrix1);
            Matrix.CreateRotationZ(dirAngle, out rotMatrix);
            Matrix.CreateTranslation(center.X, center.Y, 0, out tlMatrix2);

            // create composite transformation matrix
            Matrix compMatrix2, compMatrix1;
            Matrix.Multiply(ref tlMatrix1, ref rotMatrix, out compMatrix1);
            Matrix.Multiply(ref compMatrix1, ref tlMatrix2, out compMatrix2);

            return compMatrix2;
        }

        // rotate a vertices around its centroid
        public static void RotateVertices(Vertices vertices, float rotationAngle)
        {
            Vector2 center = vertices.GetCentroid();
            RotateVertices(vertices, center, rotationAngle);
        }

        // rotate a vertices on arbitrary center
        public static void RotateVertices(Vertices vertices, Vector2 center,
                                          float rotationAngle)
        {
            Matrix m = CreateRotationMatrix(center, rotationAngle);
            Vector2 v;
            for (int i = 0; i < vertices.Count; i++)
            {
                v = vertices[i];

                #region INLINE: v = Vector2.Transform(v, m);
                float newX = ((v.X * m.M11) + (v.Y * m.M21)) + m.M41;
                float newY = ((v.X * m.M12) + (v.Y * m.M22)) + m.M42;
                #endregion

                vertices[i] = new Vector2(newX, newY);
            }
        }

        // copied from Vector2.SmoothStep, adapted for float value
        /// <summary>
        /// Smoothly increase / decrease from 1st value to 2nd value.
        /// </summary>
        public static float SmoothStep(float value1, float value2, float amount)
        {
            amount = (amount > 1f) ? 1f : ((amount < 0f) ? 0f : amount);
            amount = (amount * amount) * (3f - (2f * amount));
            return (value1 + ((value2 - value1) * amount));
        }

        /// <summary>
        /// Get scale to resize 1st rectangle to enclose 2nd rectangle.
        /// </summary>
        /// <returns>(Enclosing size) / (current size) of 1st rectangle.</returns>
        public static float GetScaleToEnclose(Vector2 r1, Vector2 r2)
        {
            float scaleX = r2.X / r1.X;
            float scaleY = r2.Y / r1.Y;
            return Math.Max(scaleX, scaleY);
        }

        /// <summary>
        /// Get scale to resize 1st rectangle to get enclosed by 2nd rectangle.
        /// </summary>
        /// <returns>(Enclosed size) / (current size) of 1st rectangle.</returns>
        public static float GetScaleToEnclosedBy(Vector2 r1, Vector2 r2)
        {
            float scaleX = r2.X / r1.X;
            float scaleY = r2.Y / r1.Y;
            return Math.Min(scaleX, scaleY);
        }

     
        // convert negative angle to positive, clamped to 360 degrees max.
        // return the converted angle in degree.
        public static float ClampAngle(float angle)
        {
            float cAngle = angle;
            if (cAngle < 0)
            {
                int mul = (int)(cAngle / 360f) + 1;
                cAngle += (360 * mul);
            }
            cAngle %= 360;

            return cAngle;
        }

        /// <summary>
        /// Create a list that contains a 'shuffled' numbers ranged from min to max.
        /// No duplicates. Desired result size is usually smaller than min-max range. 
        /// The amount of number returned is the smallest between min-max range 
        /// and resultSize.
        /// </summary>
        /// <param name="min">minimum, inclusive</param>
        /// <param name="max">maximum, exclusive</param>
        public static List<int> GetRandListNoDuplicate(int min, int max, int resultSize)
        {
            Random r = new Random();

            // min is inclusive, max is exclusive.
            int srcRange = max - min;
            List<int> srcList = new List<int>(srcRange);
            for (int i = 0; i < srcRange; i++) srcList.Add(min + i);

            List<int> result = new List<int>();

            // select a few from all available number
            int randomInt;
            int counter = resultSize;
            while (srcRange > 0 && counter > 0)
            {
                randomInt = r.Next(0, srcRange);
                result.Add(srcList[randomInt]);
                srcList.RemoveAt(randomInt);
                srcRange--;
                counter--;
            }
            return result;
        }

        // get center and radius of a circle, from 3 known points in the circumference.
        // from http://en.wikipedia.org/wiki/Circumcircle#Cartesian_coordinates
        public static bool GetCircleFrom3Point(Vector2 A, Vector2 B, Vector2 C,
            out Vector2 center, out float radius)
        {
            center = Vector2.Zero;
            radius = 0;

            // translate one of the vertices to (0,0), the others follow
            Vector2 a = Vector2.Zero;
            Vector2 b = B - A;
            Vector2 c = C - A;

            float d = 2 * (b.X * c.Y - b.Y * c.X);

            // if points a,b,c are close to collinear (straight line), circle
            // will have infinite radius. return.
            if (d > -0.001f && d < 0.001f)
                return false;
            else
            {
                float bx2by2 = (b.X * b.X) + (b.Y * b.Y);
                float cx2cy2 = (c.X * c.X) + (c.Y * c.Y);
                center.X = (c.Y * bx2by2 - b.Y * cx2cy2) / d;
                center.Y = (b.X * cx2cy2 - c.X * bx2by2) / d;
                radius = center.Length();

                center += A;    // translate back

                return true;
            }
        }




        ///ShadowPlay Mods below..

#if STATICBUFFER 
        const int BUFF_LEN = 3000;  //didnt work, and would have to use a map to thread id if multithreading..
        static Vertices _newVerts = new Vertices(BUFF_LEN+1);
#endif



//TODO use voronoi for this..it looks good..

    //or

        /// <summary>
        ///   subdivide points of polygon randomly.   aftern triangularization... add more points in beween to smooth it out
        ///   coudl   also use smoothing algorith.. on X and Y..... maybe..  
        ///   
        /// the use is to break up clouds.   the resultant pieces should roughtly be the volume of the original cloud.
        /// 
        /// </summary>
        /// <param name="polyVerts"></param>
        /// <param name="minimumEdgeSquaredSize"></param>
        /// <returns></returns>
        public static Vertices RandomlySubdividePolygon(Vertices polyVerts, float minimumEdgeSquaredSize)
        {
            //Vector2 cm = polyVerts.GetCentroid();
            //float varLen = polyVerts.GetRadius() * 0.0f;    // even when still inside original edge line, still throw collinear edge exception.
            //float varLen = polyVerts.GetRadius() * -0.2f;   // this creates more star-shaped.
            // allocate buffer big enough for new vertices

            Vertices newVerts;

#if STATICBUFFER  // might help if Settings.ConserveMemory needs to be left on ..  I tested ConserveMemory is true.
            //also, for some reason this fails with invalid polygons like jittered clouds
            //also need to check with threading.. coud use thread ID to get a buffer
            //clouds work, but regrow does not.   But this also has and issue and needs to be fixed, with simple test first and real cloud.
            if (polyVerts.Count > BUFF_LEN/2)
            {
#if !PRODUCTION 
                        throw new Exception ( "too many Verts in RandomSubdividePolygon, not doing anything");
                        //cloud polygon this big, would take to long.,, not supported..
#endif
                        return polyVerts;
            }
             _newVerts.Clear();
            newVerts = _newVerts;
#else
            newVerts = new Vertices(2 * polyVerts.Count + 1);  //assuming only one vertex added per face..
#endif

            for (int i = 0; i < polyVerts.Count; ++i)
            {
                newVerts.Add(polyVerts[i]);
                //code was adapted from loop in   public void Set(Vertices vertices) on polygon shape
                int i1 = i;
                int i2 = i + 1 < polyVerts.Count ? i + 1 : 0;
                Vector2 edge = polyVerts[i2] - polyVerts[i1];

                float edgeLenSq = edge.LengthSquared();
                if (edgeLenSq < minimumEdgeSquaredSize)
                {
                    continue;
                }

                //if kind of a small edge, do this..  //TODO maybe subdivide larger edges more..  use code in winddrag..

                //TODO to find it object shart triangle got to find perimiter.
                // could do a quick approx permitn, add the Ys and Xs of faces.. and do a max of them or aver..
                // or just do a perim, speed might be neglible..
                //   bool jitterRelToFaceLen = /*(edgeLenSq < minimumEdgeSquaredSize * 2) &&*/ polyVerts.Count == 3;

                // reduce apearance of shards.. TODO sudivide long faces by N... ( then consoidate with wind drag maybe).. minimumEdgeSquaredSize will be based on scale factor.               
                // and of relative to permiter, but this would require anther pass.   
                //    

                //fix  small triangles becoming sharp shards..( dont allow big middle of cloud triangle becoming big diamond), 
                //tested on cloud, only use jitterRelToFaceLen for small triangles to prevent small shards.. big pieces are using  push out relative to radius
                //to avoid major addition of volume:
                bool jitterRelToFaceLen = (edgeLenSq < minimumEdgeSquaredSize * 3) && polyVerts.Count == 3;

                //randomize, don't too close to original vertices.
                //float dist = 0.5f;
                float dist = jitterRelToFaceLen ? MathUtils.RandomNumber(0.45f, 0.55f) : MathUtils.RandomNumber(0.3f, 0.7f);  //close to middle is best..

                Vector2 faceMidpoint = polyVerts[i1] + (dist * edge);

                //increase distance from edge normal
                Vector2 normal = new Vector2(edge.Y, -edge.X);
                // TODO   i think due to winding this might not always point the correct way..   check  on breakable-spirit-simple
                //if this is not reliable .. then but do a dot product to be sure normal point away from centroid , check farseer, or maybe our draw tool 
                //makes bad ones.   but for cloud disperse its not important.  HOWEVER coudl result in bad wind effects, mayb needs to test more..

                Vector2 newPushedOutVertex = Vector2.Zero;

                if (jitterRelToFaceLen)
                {
                    normal *= (float)MathUtils.RandomNumber(0.2f, 0.4f);    //dont normalise for now  this way put short edges out more relative to the face length..  less Sharp triangle shards.                   
                    newPushedOutVertex = faceMidpoint + normal;//this pushed out as a function of radius..normalise above
                }
                else  //push out as a function of radius..
                {
                    float varLen = polyVerts.GetRadius() * (float)MathUtils.RandomNumber(0.2f, 0.4f);
                    normal.Normalize();
                    Vector2 offsetBasedOnRadius = faceMidpoint + (normal * varLen);//this pushed out as a function of radius..normalise above
                    newPushedOutVertex += offsetBasedOnRadius;
                }

                newVerts.Add(newPushedOutVertex);
            }

            // NOTE1: this might create non-convex polygon. perhaps that's why it often failed when create polygon using these vertices.
            // NOTE2: number of vertices must not exceed Settings.MaxPolygonVertices, or will throw exception in PolygonShape.Set()
            // further process might be needed.

            //// CheckPolygon() return true when invalid
            //if (newVerts.CheckPolygon() == true)
            //{
            //    // just return original polys
            //    return polyVerts;
            //}

            return newVerts;
        }




        //smoothing.. this is need for water wave edge buffers..  when the wave is disturbed by a roung shapre

        //many ways.. we want to avoid sharp noise that makes progragation unstable.
        //needs to be tight samples mabye only 3 points.  and fast.

        //http://www.mathworks.com/matlabcentral/fileexchange/19998-fast-smoothing-function.. in  mathlab hard to convert to c# quickly..
        //http://en.wikipedia.org/wiki/Exponential_smoothing  /// pseudo code.. for simple moving averge.
        //   http://stackoverflow.com/questions/12884600/how-to-calculate-simple-moving-average-faster-in-c



        // http://rosettacode.org/wiki/Averages/Simple_moving_average
        //    var nums = Enumerable.Range(1, 5).Select(n => (double)n);
        //    nums = nums.Concat(nums.Reverse());

        //     var sma3 = SMA(3);
        //    var sma5 = SMA(5);

        //     foreach (var n in nums)
        //    {
        //        Console.WriteLine("{0}    (sma3) {1,-16} (sma5) {2,-16}", n, sma3(n), sma5(n));
        //    }



        //  static void SmoothSMA( float[]  funt,    int startIndex, int endindex, float Period )

        //   static Func<double, double> SMA(int p)   ///this isi confusing as hell, and dont know how efficient.//typical rosetta code c#
        //   {
        //       Queue<double> s = new Queue<double>(p);
        //       return (x) =>
        //     {
        //           if (s.Count >= p)
        //           {
        //               s.Dequeue();
        //      }
        //           s.Enqueue(x);
        //           return s.Average();
        //    };
        //   }
        // }


      //  static void SmoothSMA(float[] buffer, int startIndex, int endindex, float Period);


    }
}
