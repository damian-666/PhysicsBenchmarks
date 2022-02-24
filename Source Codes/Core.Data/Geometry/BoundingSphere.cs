/*
 * 2D bounding sphere class for Silverlight.
 * 
 * TODO: should be renamed to BoundingCircle later.
 * 
 * Copyright Shadowplay Studios, 2009.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using FarseerPhysics.Common;
using Farseer.Xna.Framework;

namespace Core.Data.Geometry

{
    public class BoundingSphere
    {
        private Vector2 _center = Vector2.Zero;
        private float _radius;

        public BoundingSphere()
        { }

        // create minimal bounding sphere
        public BoundingSphere(Vertices vertices)
        {
            fastBall(vertices, out _center, out _radius);
        }

        // Create pre-centered bounding sphere. Useful if we have already 
        // calculated the shapes's centroid / center of mass.
        public BoundingSphere(Vertices vertices, Vector2 center)
        {
            centeredBoundingSphere(vertices, center, out _radius);
            _center = center;
        }

        public Vector2 Center
        {
            get { return _center; }
        }

        public float Radius
        {
            get { return _radius; }
        }

        // Code to calculate MINIMAL BOUNDING SPHERE.
        // Code copied from [geometryalgorithms.com], based on algorithm given 
        // by [Jack Ritter, 1990: A fast approximation of the bounding ball for 
        // a point set]. Have O(n) complexity, and the resulting approximation 
        // is estimated to fall within 5% of the actual minimal bounding ball.
        private static void fastBall(Vertices vs, out Vector2 center, out float radius)
        {
            Vector2 C;                              // center of ball
            float rad, rad2;                        // radius and radius squared
            float xmin, xmax, ymin, ymax;           // bounding box extremes
            Vector2 Pxmin, Pxmax, Pymin, Pymax;     // vertices at box extreme

            // find a large diameter to start with
            // first get the bounding box and V[] extreme points for it

            xmin = xmax = vs[0].X;
            ymin = ymax = vs[0].Y;
            Pxmin = Pxmax = Pymin = Pymax = vs[0];
            Vector2 v;

            int max = vs.Count;
            for (int i = 1; i < max; i++)
            {
                v = vs[i];
                if (v.X < xmin)
                {
                    xmin = v.X;
                    Pxmin = v;
                }
                else if (v.X > xmax)
                {
                    xmax = v.X;
                    Pxmax = v;
                }
                if (v.Y < ymin)
                {
                    ymin = v.Y;
                    Pymin = v;
                }
                else if (v.Y > ymax)
                {
                    ymax = v.Y;
                    Pymax = v;
                }
            }

            // select the largest extent as an initial diameter for the ball
            Vector2 dVx = Pxmax - Pxmin;            // diff of Vx max and min
            Vector2 dVy = Pymax - Pymin;            // diff of Vy max and min
            float dx2 = Vector2.Dot(dVx, dVx);       // Vx diff squared
            float dy2 = Vector2.Dot(dVy, dVy);       // Vy diff squared

            if (dx2 >= dy2)                     // x direction is largest extent
            {
                C = Pxmin + (dVx / 2.0f);       // Center = midpoint of extremes
                rad = Vector2.Distance(C, Pxmax);
            }
            else                                // y direction is largest extent
            {
                C = Pymin + (dVy / 2.0f);       // Center = midpoint of extremes
                rad = Vector2.Distance(C, Pymax);
            }

            // now check that all points vs[i] are in the ball
            // and if not, expand the ball just enough to include them
            Vector2 dV;
            float dist, dist2;
            rad2 = rad * rad;
            foreach (Vector2 vert in vs)
            {
                dV = vert - C;
                dist2 = Vector2.Dot(dV, dV);
                if (dist2 <= rad2)                  // vs[i] is inside the ball already
                    continue;

                // vs[i] not in ball, so expand ball to include it
                dist = (float)Math.Sqrt(dist2);

                // Enlarge radius just enough (new radius = rad + 1/2delta), so
                // that we can shift center toward vs[i] by 1/2delta.
                rad = (rad + dist) / 2.0f;
                C = C + (dV * ((dist - rad) / dist));

                rad2 = rad * rad;
            }

            // save the result
            center = C;
            radius = rad;
        }

        // Code to calculate bounding sphere that centered on a coord location.
        // The result probably will NOT be a MINIMAL bounding sphere.
        private static void centeredBoundingSphere(Vertices vs, Vector2 center, 
                                            out float radius)
        {
            // Check that all points vs[i] are in the ball, or else just expand
            // the ball to include them.
            Vector2 dV;
            float dist2;
            float rad2 = 0.0f;
            foreach (Vector2 vert in vs)
            {
                dV = vert - center;
                dist2 = Vector2.Dot(dV, dV);
                if (dist2 <= rad2)                  // vs[i] is inside the ball already
                    continue;

                // vs[i] not in ball, so expand ball to include it
                rad2 = dist2;
            }
            // save the result
            radius = (float)Math.Sqrt(rad2);
        }

    } // end of class
}
