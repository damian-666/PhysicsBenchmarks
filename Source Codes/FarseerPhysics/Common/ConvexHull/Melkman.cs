﻿using Farseer.Xna.Framework;

namespace FarseerPhysics.Common.ConvexHull
{
    public static class Melkman
    {
        //Melkman based convex hull algorithm contributed by Cowdozer

        /// <summary>
        /// Creates a convex hull of the Vertices.
        /// Note: Vertices must be of a simple polygon, i.e. edges do not overlap.
        /// </summary>
        /// <remarks>
        /// Implemented using Melkman's Convex Hull Algorithm - O(n) time complexity.
        /// Reference: http://www.ams.sunysb.edu/~jsbm/courses/345/melkman.pdf
        /// Requires that vertices are of a simple polygon. Handles collinear points.
        /// </remarks>
        /// <returns>A convex hull in counterclockwise winding order.</returns>
        public static Vertices GetConvexHull(Vertices vertices)
        {
            //With less than 3 vertices, this is about the best we can do for a convex hull
            if (vertices.Count < 3)
                return vertices;

            //We'll never need a queue larger than the current number of Vertices +1
            //Create double-ended queue
            Vector2[] deque = new Vector2[vertices.Count + 1];
            int qf = 3, qb = 0; //Queue front index, queue back index
            int qfm1, qbm1; //qfm1 = second element, qbm1 = second last element

            //Start by placing first 3 vertices in convex CCW order
            int startIndex = 3;
            float k = MathUtils.Area(vertices[0], vertices[1], vertices[2]);
            if (k == 0)
            {
                //Vertices are collinear.
                deque[0] = vertices[0];
                deque[1] = vertices[2]; //We can skip vertex 1 because it should be between 0 and 2
                deque[2] = vertices[0];
                qf = 2;

                //Go until the end of the collinear sequence of vertices
                for (startIndex = 3; startIndex < vertices.Count; startIndex++)
                {
                    Vector2 tmp = vertices[startIndex];
                    if (MathUtils.Area(ref deque[0], ref deque[1], ref tmp) == 0) //This point is also collinear
                        deque[1] = vertices[startIndex];
                    else break;
                }
            }
            else
            {
                deque[0] = deque[3] = vertices[2];
                if (k > 0)
                {
                    //Is Left.  Set deque = {2, 0, 1, 2}
                    deque[1] = vertices[0];
                    deque[2] = vertices[1];
                }
                else
                {
                    //Is Right. Set deque = {2, 1, 0, 2}
                    deque[1] = vertices[1];
                    deque[2] = vertices[0];
                }
            }

            qfm1 = qf == 0 ? deque.Length - 1 : qf - 1; //qfm1 = qf - 1;
            qbm1 = qb == deque.Length - 1 ? 0 : qb + 1; //qbm1 = qb + 1;

            //Add vertices one at a time and adjust convex hull as needed
            for (int i = startIndex; i < vertices.Count; i++)
            {
                Vector2 nextPt = vertices[i];

                //Ignore if it is already within the convex hull we have constructed
                if (MathUtils.Area(ref deque[qfm1], ref deque[qf], ref nextPt) > 0 &&
                    MathUtils.Area(ref deque[qb], ref deque[qbm1], ref nextPt) > 0)
                    continue;

                //Pop front until convex
                while (!(MathUtils.Area(ref deque[qfm1], ref deque[qf], ref nextPt) > 0))
                {
                    //Pop the front element from the queue
                    qf = qfm1; //qf--;
                    qfm1 = qf == 0 ? deque.Length - 1 : qf - 1; //qfm1 = qf - 1;
                }
                //Add vertex to the front of the queue
                qf = qf == deque.Length - 1 ? 0 : qf + 1; //qf++;
                qfm1 = qf == 0 ? deque.Length - 1 : qf - 1; //qfm1 = qf - 1;
                deque[qf] = nextPt;

                //Pop back until convex
                while (!(MathUtils.Area(ref deque[qb], ref deque[qbm1], ref nextPt) > 0))
                {
                    //Pop the back element from the queue
                    qb = qbm1; //qb++;
                    qbm1 = qb == deque.Length - 1 ? 0 : qb + 1; //qbm1 = qb + 1;
                }
                //Add vertex to the back of the queue
                qb = qb == 0 ? deque.Length - 1 : qb - 1; //qb--;
                qbm1 = qb == deque.Length - 1 ? 0 : qb + 1; //qbm1 = qb + 1;
                deque[qb] = nextPt;
            }

            //Create the convex hull from what is left in the deque
            Vertices convexHull = new Vertices(vertices.Count + 1);
            if (qb < qf)
                for (int i = qb; i < qf; i++)
                    convexHull.Add(deque[i]);
            else
            {
                for (int i = 0; i < qf; i++)
                    convexHull.Add(deque[i]);
                for (int i = qb; i < deque.Length; i++)
                    convexHull.Add(deque[i]);
            }
            return convexHull;
        }
    }
}