using System;
using FarseerPhysics.Dynamics;
using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Collision.Shapes;
using System.Collections.Generic;
using FarseerPhysics.Common.Decomposition;
using System.Diagnostics;


namespace FarseerPhysics.Factories
{
    /// <summary>
    /// An easy to use factory for creating bodies
    /// </summary>
    public static class BodyFactory
    {
        public static Body CreateBody(World world)
        {
            return CreateBody(world, null);
        }

        public static Body CreateBody(World world, Object userData)
        {
            Body body = new Body(world, userData);
            return body;
        }

        #region ShadowPlay Mods: Subdivide a given concave vertices into convex fixtures body

        /// <summary>
        /// Create a body from a given non-convex vertices -AZA
        /// </summary>
        /// <param name="world">Physics World</param>
        /// <param name="position">Position</param>
        /// <param name="vertices">Non-Convex vertices</param>
        /// <returns></returns>
        public static Body CreateBody(World world, Vector2 position, Vertices vertices, float density)
        {

            Body body = null;
            try
            {
                body = CreateBody(world);
                body.BodyType = BodyType.Dynamic;
                body.GeneralVertices = vertices;
                body.Density = density;
                body.Position = position;
                body.CreateFixtures(vertices, density);

            }
  
            catch (Exception exc)
            {
                Debug.WriteLine( "error in create body" + exc.Message);
				return null;
            } 

            return body; 
        }

        public static Body CreateBody(World world, Vector2 position, Object userData = null)
        {
            Body body = CreateBody(world, userData);
            body.Position = position;
            return body;
        }


        public static Body CreateBody(World world, Vector2 position, Shape collisionShape, Object userData= null)
        {
            Body body = CreateBody(world, userData);
            body.Position = position;
            body.CreateFixture(collisionShape);
            return body;
        }

        /// <summary>
        /// Create a body from a given non-convex vertices.
        /// Vertices centroid will be located on Body (0,0) local position, 
        /// so Body.Position and Body.WorldCenter should be the same.
        /// This can avoid Body.Position located far outside its Fixtures polygon.
        /// </summary>
        public static Body CreateBody(World world, Vertices vertices, float density)
        {
            // get world position first for body
            Vector2 center = vertices.GetCentroid();

            // move vertices local position to be centered at (0,0).
            vertices.Centralize();

            return CreateBody(world, center, vertices, density);
        }

        #endregion
    }
}