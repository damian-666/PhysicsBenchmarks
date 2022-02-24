/* Original source Farseer Physics Engine:
 * Copyright (c) 2014 Ian Qvist, http://farseerphysics.codeplex.com
 * Microsoft Permissive License (Ms-PL) v1.1
 */

/*
* Farseer Physics Engine:
* Copyright (c) 2012 Ian Qvist
*/


using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using Farseer.Xna.Framework;

namespace FarseerPhysics.Diagnostics
{
    /// Implement and register this class with a World to provide debug drawing of physics
    /// entities in your game.
    public abstract class DebugViewBase
    {
        protected DebugViewBase(World world)
        {
            World = world;
        }

        protected World World { get; set; }

        /// <summary>
        /// Gets or sets the debug view flags.
        /// </summary>
        /// <value>The flags.</value>
        public DebugViewFlags Flags { get; set; }

        /// <summary>
        /// Append flags to the current flags.
        /// </summary>
        /// <param name="flags">The flags.</param>
        public void AppendFlags(DebugViewFlags flags)
        {
            Flags |= flags;
        }

        public void ClearFlags()
        {
            Flags = 0;
        }

        /// <summary>
        /// Remove flags from the current flags.
        /// </summary>
        /// <param name="flags">The flags.</param>
        public void RemoveFlags(DebugViewFlags flags)
        {
            Flags &= ~flags;
        }

        /// <summary>
        /// Draw a closed polygon provided in CCW order.
        /// </summary>
      /// <param name="vertices">The body containing the verts</param>
        /// <param name="vertices">The vertices.</param>
        /// <param name="count">The vertex count.</param>
        /// <param name="red">The red value.</param>
        /// <param name="blue">The blue value.</param>
        /// <param name="green">The green value.</param>
        public abstract void DrawPolygon(Body body, Vector2[] vertices, int count, float red, float blue, float green, bool closed = true);

        /// <summary>
        /// Draw a solid closed polygon provided in CCW order.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="count">The vertex count.</param>
        /// <param name="red">The red value.</param>
        /// <param name="blue">The blue value.</param>
        /// <param name="green">The green value.</param>
        public abstract void DrawSolidPolygon(Vector2[] vertices, int count, float red, float blue, float green);

        /// <summary>
        /// Draw a circle.
        /// </summary>
        /// <param name="center">The center.</param>
        /// <pa<param name="radius">The scale.</param>

        /// <param name="red">The red value.</param>
        /// <param name="blue">The blue value.</param>
        /// <param name="green">The green value.</param>
        /// <param name="scale">The scale</param>
        public abstract void DrawCircle(Vector2 center, float radius, float red, float blue, float green, Vector2 scale, float edgeThickness);

        /// <summary>
        /// Draw a solid circle.
        /// </summary>
        /// <param name="center">The center.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="scale">The scale</param>
        /// <param name="red">The red value.</param>
        /// <param name="blue">The blue value.</param>
        /// <param name="green">The green value.</param>
      /// <param name="scale">The scale</param>
        public abstract void DrawSolidCircle(Vector2 center, float radius, float red, float blue,
                                             float green, Vector2 scale, float edgeThickness);

        /// <summary>
        /// Draw a line segment.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <param name="red">The red value.</param>
        /// <param name="blue">The blue value.</param>
        /// <param name="green">The green value.</param>
        public abstract void DrawSegment(Vector2 start, Vector2 end, float red, float blue, float green);

        /// <summary>
        /// Draw a transform. Choose your own length scale.
        /// </summary>
        /// <param name="transform">The transform.</param>
        public abstract void DrawTransform(ref Transform transform);
    }
}