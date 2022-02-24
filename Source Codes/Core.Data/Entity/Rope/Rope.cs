//not used, using   NOWDEPRECATEDUSINGCREATUREONELEG
/*
 * Rope is collection of Bodies connected by Joints. Rope use Spirit internally.
 * 
 * Rope uses bounding sphere to calculate distance between segments.
 * This makes it possible to add new segment in any direction.
 * 
 * The 'spacing' distance can be set to negative, which means either
 * segment or its bounding sphere can overlap with another segment.
 * 
 * 
 * TODO: still buggy when creating ropes, test more later, low priority.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Runtime.Serialization;

using Microsoft.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;

using Core.Data.Interfaces;
using Core.Data.Geometry;
using Core.Data.Collections;


namespace Core.Data.Entity
{
    /// <summary>
    /// This callback will be called everytime a segment must be created in BuildRope method.
    /// Width and height are only hints for callback to determine shape size.
    /// </summary>
    public delegate void RopeShapeCreationCallback(float width, float height, out Body body);


    /// <summary>
    /// RopeBuilder is tool only. The result will become Spirit object.
    /// </summary>
    [DataContract(Name = "Rope", Namespace = "http://ShadowPlay")]
    public class RopeBuilder
    {
        private List<Body> _bodies;
        private PoweredJointCollection _joints;
        private Vector2 _headPos;


        public Spirit BuildRope(RopeShapeCreationCallback callback,
                              int numberOfSegment, Vector2 headPos,
                              Vector2 startSize, Vector2 endSize,
                              float startSpace, float endSpace,
                              float startDirAngle, float endDirAngle)
        {
            if (numberOfSegment <= 0) return null;
            if (callback == null) return null;

            Body b;
            _bodies = new List<Body>();
            _joints = new PoweredJointCollection();
            _headPos = headPos;

            float wIncr = (endSize.X - startSize.X) / numberOfSegment;
            float hIncr = (endSize.Y - startSize.Y) / numberOfSegment;
            float spIncr = (endSpace - startSpace) / numberOfSegment;
            float angleIncr = (endDirAngle - startDirAngle) / numberOfSegment;
            float w = startSize.X;
            float h = startSize.Y;
            float sp = startSpace;
            float angle = startDirAngle;

            for (int i = 0; i < numberOfSegment; i++)
            {
                callback(w, h, out b);
                if (b == null) continue;

                // add segment and joint
                AddSegmentWithJoint(b, sp, angle);

                w += wIncr;
                h += hIncr;
                sp += spIncr;
                angle += angleIncr;
            }

            // build spirit from bodies & joints
            Spirit spirit = new Spirit(_joints);
            spirit.Bodies = _bodies;

            return spirit;
        }


        #region Non-public Methods

        // Add segment on rope end and link using joint. Segment position is 
        // calculated using pre-centered bounding sphere. The dirAngle is 
        // always calculated from object upward direction (12 o'clock).
        private void AddSegmentWithJoint(Body nBody, float spacing, float dirAngle)
        {
            // If object has any rotation, reject it. Creating a joint between
            // rotated body/geom is currrently problematic.
            float rot = nBody.Rotation;
            if (rot != 0 && rot != MathHelper.TwoPi)
            {
                throw new ArgumentException(
                    "Adding body with rotation is not supported", "shape");
            }

            // New segment will be placed after the last shape, except for the 
            // first one.
            if (_bodies.Count <= 0)
            {
                nBody.Position = _headPos;
            }
            else
            {
                // set up new position
                Vector2 jCenterPos, jStartPos, jEndPos;
                AddSegment(nBody, spacing, dirAngle,
                           out jCenterPos, out jStartPos, out jEndPos);

                // add joint to connect shapes
                Body lBody = _bodies[_bodies.Count - 1];
                AddPoweredJoint(lBody, nBody, jCenterPos);
            }
            // finally, add the new segment to rope
            _bodies.Add(nBody);
        }

        // general segment insertion
        private void AddSegment(Body nBody, float spacing, float dirAngle,
            out Vector2 jCenterPos, out Vector2 jStartPos, out Vector2 jEndPos)
        {
            // last segment bounds and center
            Body lBody = _bodies[_bodies.Count - 1];
            BoundingSphere lbs =
                new BoundingSphere(lBody.GeneralVertices, lBody.Position);

            // new segment bounds and center
            BoundingSphere nbs =
                new BoundingSphere(nBody.GeneralVertices, nBody.Position);

            // find new position for the new segment
            // assume upward direction first
            float nDist = lbs.Radius + spacing + nbs.Radius;
            Vector2 nPosUp = lbs.Center + new Vector2(0, -nDist);
            Vector2 nPos;

            // joint center, start, and end
            float jStartDist;
            float jEndDist;
            if (spacing >= 0)
            {
                jStartDist = lbs.Radius;
                jEndDist = lbs.Radius + spacing;
            }
            else    // when space between is negative
            {
                jStartDist = lbs.Radius + spacing;
                jEndDist = lbs.Radius;
            }
            float jCenterDist = (jStartDist + jEndDist) * 0.5f;

            Vector2 jStartPosUp = lbs.Center + new Vector2(0, -jStartDist);
            Vector2 jCenterPosUp = lbs.Center + new Vector2(0, -jCenterDist);
            Vector2 jEndPosUp = lbs.Center + new Vector2(0, -jEndDist);

            // rotate the new segment and joint center
            if (dirAngle != 0)
            {
                Matrix rotate = GeomUtility.CreateRotationMatrix(
                                lbs.Center, dirAngle);
                nPos = Vector2.Transform(nPosUp, rotate);
                jCenterPos = Vector2.Transform(jCenterPosUp, rotate);
                jStartPos = Vector2.Transform(jStartPosUp, rotate);
                jEndPos = Vector2.Transform(jEndPosUp, rotate);
            }
            else
            {
                nPos = nPosUp;
                jCenterPos = jCenterPosUp;
                jStartPos = jStartPosUp;
                jEndPos = jEndPosUp;
            }

            // move new segment to the new position
            nBody.Position = nPos;
        }

        private void AddPoweredJoint(Body lBody, Body nBody, Vector2 jointPos)
        {
            PoweredJoint pj = new PoweredJoint(lBody, nBody, jointPos);
            _joints.Add(pj);
        }

        #endregion

    } // end of Rope class

}

