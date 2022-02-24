/*
* Farseer Physics Engine based on Box2D.XNA port:
* Copyright (c) 2010 Ian Qvist
* 
* Box2D.XNA port of Box2D:
* Copyright (c) 2009 Brandon Furtwangler, Nathan Furtwangler
*
* Original source Box2D:
* Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using System.Collections.Generic;

using Farseer.Xna.Framework;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Factories;
using FarseerPhysics.Dynamics.Particles;


namespace FarseerPhysics.Dynamics
{
    /// <summary>
    /// A type of body that supports multiple fixtures that can break apart.
    /// </summary>
    public class BreakableBody //shadowplay mod... 
    {
        public bool Broken;
        public Body MainBody;
        public List<Fixture> Parts = new List<Fixture>(8);
        public float Strength = 500.0f;

        private bool _break;

#if CACHEBODYVEL// this is not needed for clouds and seems not fully implemented
        private Vector2[] _velocitiesCache = new Vector2[8];
        private float[] _angularVelocitiesCache = new float[8];
#endif
        private World _world;

        #region Shadowplay Mod
        /// <summary>
        /// List of new bodies created after this breakable bodies broken.
        /// </summary>
        public List<Particle> NewBodies = new List<Particle>(8);
        #endregion


        public BreakableBody(IEnumerable<Vertices> vertices, World world, float density)
            : this(vertices, world, density, null)
        {
        }

        public BreakableBody(IEnumerable<Vertices> vertices, World world, float density, Object userData)
        {
            _world = world;
            _world.ContactManager.PostSolve += PostSolve;
            MainBody = new Body(_world);
            MainBody.BodyType = BodyType.Dynamic;

            foreach (Vertices part in vertices)
            {
                PolygonShape polygonShape = new PolygonShape(part, density);
                Fixture fixture = MainBody.CreateFixture(polygonShape, userData);
                Parts.Add(fixture);
            }
        }

        private void PostSolve(Contact contact, ContactConstraint impulse)
        {
            if (!Broken)
            {
                if (Parts.Contains(contact.FixtureA) || Parts.Contains(contact.FixtureB))
                {
                    float maxImpulse = GetMaxNormalImpulse(contact, impulse);

                    if (maxImpulse > Strength)
                    {
                        // Flag the body for breaking.
                        _break = true;
                    }
                }
            }
        }

        private static float GetMaxNormalImpulse(Contact contact, ContactConstraint impulse)
        {
            float maxImpulse = 0.0f;
            int count = contact.Manifold.PointCount;

            for (int i = 0; i < count; ++i)
            {
                maxImpulse = Math.Max(maxImpulse, impulse.Points[i].NormalImpulse);
            }
            return maxImpulse;
        }

        public void Update()
        {
            if (_break)
            {
                Decompose();
                Broken = true;
                _break = false;
                _world.RemoveBody(MainBody);
           
            }

            // Cache velocities to improve movement on breakage.
            if (Broken == false)
            {
                //TODO check with latest farseer looks, incomplete.. comment this arrays  out or use cloud mode, to skip it.
                //all the vel are same for clouds in shadowplay..

#if CACHEBODYVEL
                //Enlarge the cache if needed
                if (Parts.Count > _angularVelocitiesCache.Length)
                {
                    _velocitiesCache = new Vector2[Parts.Count];
                    _angularVelocitiesCache = new float[Parts.Count];
                }

                //Cache the linear and angular velocities.
                for (int i = 0; i < Parts.Count; i++)
                {
                    _velocitiesCache[i] = Parts[i].Body.LinearVelocity;  //TODO make more sense to use world velocity at point?
                    _angularVelocitiesCache[i] = Parts[i].Body.AngularVelocity;
                }
#endif
            }
        }

        private void Decompose()
        {
            //Unsubsribe from the PostSolve delegate
            _world.ContactManager.PostSolve -= PostSolve;

            for (int i = 0; i < Parts.Count; i++)
            {
                Fixture fixture = Parts[i];

                Shape shape = fixture.Shape.Clone();
                MainBody.DestroyFixture(fixture);
 
                Particle body = new Particle(_world);//   Shadowplay Mod, used for cloud burst for now, pieces fade out
                //  Body body = BodyFactory.CreateBody(_world);  //farseer version

                body.BodyType = BodyType.Dynamic;

                #region Shadowplay Mod

                body.CopyPropertiesFrom(MainBody);

                if (!MainBody.FixedRotation)//shadowplay mods
                {
#if CACHEBODYVEL
                    body.AngularVelocity = _angularVelocitiesCache[i];
#else
                    body.AngularVelocity = MainBody.AngularVelocity;
#endif
                }

                //to prevent exception in winddrag
                if (shape is PolygonShape)
                {
                    body.GeneralVertices = (shape as PolygonShape).Vertices;
                }
                //  body.Normals = MainBody.Normals;  //TODO looks wrong these should get generated if null. for coujld not used. 

                NewBodies.Add(body);

                #endregion

                body.CreateFixture(shape);

#if CACHEBODYVEL
                body.LinearVelocity = _velocitiesCache[i];
#else
                body.LinearVelocity = MainBody.LinearVelocity;
#endif

                //TODO try collide by deforming verts at intersect area.. 
            }
        }

        public void Break()
        {
            _break = true;
        }


    }
}