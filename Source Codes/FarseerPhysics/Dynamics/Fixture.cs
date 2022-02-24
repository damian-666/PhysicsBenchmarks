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
using System.Diagnostics;
using System.Runtime.Serialization;
using System.ComponentModel;

using Farseer.Xna.Framework;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Particles;


namespace FarseerPhysics.Dynamics
{
    [Flags]
    public enum Category
    {
        None = 0,
        All = int.MaxValue,
        Cat1 = 1,
        Cat2 = 2,
        Cat3 = 4,
        Cat4 = 8,
        Cat5 = 16,
        Cat6 = 32,
        Cat7 = 64,
        Cat8 = 128,
        Cat9 = 256,
        Cat10 = 512,
        Cat11 = 1024,
        Cat12 = 2048,
        Cat13 = 4096,
        Cat14 = 8192,
        Cat15 = 16384,
        Cat16 = 32768,
        Cat17 = 65536,
        Cat18 = 131072,
        Cat19 = 262144,
        Cat20 = 524288,
        Cat21 = 1048576,
        Cat22 = 2097152,
        Cat23 = 4194304,
        Cat24 = 8388608,
        Cat25 = 16777216,
        Cat26 = 33554432,
        Cat27 = 67108864,
        Cat28 = 134217728,
        Cat29 = 268435456,
        Cat30 = 536870912,
        Cat31 = 1073741824
    }

    /// <summary>
    /// This proxy is used internally to connect fixtures to the broad-phase.
    /// </summary>
    public struct FixtureProxy
    {
        public AABB AABB;
        public int ChildIndex;
        public Fixture Fixture;
        public int ProxyId;
    }

    public class CollisionFilter
    {
        private Category _collidesWith;
        private Category _collisionCategories;
        private short _collisionGroup;
        private Dictionary<int, bool> _collisionIgnores = new Dictionary<int, bool>();
        private Fixture _fixture;

        public CollisionFilter(Fixture fixture)
        {
            _fixture = fixture;

            if (Settings.UseFPECollisionCategories)
                _collisionCategories = Category.All;
            else
                _collisionCategories = Category.Cat1;

            _collidesWith = Category.All;
            _collisionGroup = 0;
        }

        /// <summary>
        /// Defaults to 0
        /// 
        /// If Settings.UseFPECollisionCategories is set to false:
        /// Collision groups allow a certain group of objects to never collide (negative)
        /// or always collide (positive). Zero means no collision group. Non-zero group
        /// filtering always wins against the mask bits.
        /// 
        /// If Settings.UseFPECollisionCategories is set to true:
        /// If 2 fixtures are in the same collision group, they will not collide.
        /// </summary>
        [DataMember(Order = 99)]
        public short CollisionGroup
        {
            set
            {
                if (_fixture.Body == null)
                    return;

                if (_collisionGroup == value)
                    return;

                _collisionGroup = value;
                FilterChanged();
            }
            get { return _collisionGroup; }
        }

        /// <summary>
        /// Defaults to Category.All
        /// 
        /// The collision mask bits. This states the categories that this
        /// fixture would accept for collision.
        /// Use Settings.UseFPECollisionCategories to change the behavior.
        /// </summary>
        [DataMember(Order = 99)]
        public Category CollidesWith
        {
            get { return _collidesWith; }

            set
            {
                if (_fixture.Body == null)
                    return;

                if (_collidesWith == value)
                    return;

                _collidesWith = value;
                FilterChanged();
            }
        }

        /// <summary>
        /// The collision categories this fixture is a part of.
        /// 
        /// If Settings.UseFPECollisionCategories is set to false:
        /// Defaults to Category.Cat1
        /// 
        /// If Settings.UseFPECollisionCategories is set to true:
        /// Defaults to Category.All
        /// </summary>
        [DataMember(Order = 99)]
        public Category CollisionCategories
        {
            get { return _collisionCategories; }

            set
            {
                if (_fixture.Body == null)
                    return;

                if (_collisionCategories == value)
                    return;

                _collisionCategories = value;
                FilterChanged();
            }
        }

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category.</param>
        public void AddCollisionCategory(Category category)
        {
            CollisionCategories |= category;
        }

        /// <summary>
        /// Removes the category.
        /// </summary>
        /// <param name="category">The category.</param>
        public void RemoveCollisionCategory(Category category)
        {
            CollisionCategories &= ~category;
        }

        /// <summary>
        /// Determines whether this object has the specified category.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <returns>
        /// 	<c>true</c> if the object has the specified category; otherwise, <c>false</c>.
        /// </returns>
        public bool IsInCollisionCategory(Category category)
        {
            return (CollisionCategories & category) == category;
        }

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category.</param>
        public void AddCollidesWithCategory(Category category)
        {
            CollidesWith |= category;
        }

        /// <summary>
        /// Removes the category.
        /// </summary>
        /// <param name="category">The category.</param>
        public void RemoveCollidesWithCategory(Category category)
        {
            CollidesWith &= ~category;
        }

        /// <summary>
        /// Determines whether this object has the specified category.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <returns>
        /// 	<c>true</c> if the object has the specified category; otherwise, <c>false</c>.
        /// </returns>
        public bool IsInCollidesWithCategory(Category category)
        {
            return (CollidesWith & category) == category;
        }

        /// <summary>
        /// Restores collisions between this fixture and the provided fixture.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        public void RestoreCollisionWith(Fixture fixture)
        {
            if (_collisionIgnores.ContainsKey(fixture.FixtureId))
            {
                _collisionIgnores[fixture.FixtureId] = false;
                FilterChanged();
            }
        }

        /// <summary>
        /// Ignores collisions between this fixture and the provided fixture.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        public void IgnoreCollisionWith(Fixture fixture)
        {
            if (_collisionIgnores.ContainsKey(fixture.FixtureId))
                _collisionIgnores[fixture.FixtureId] = true;
            else
                _collisionIgnores.Add(fixture.FixtureId, true);

            FilterChanged();
        }

        /// <summary>
        /// Determines whether collisions are ignored between this fixture and the provided fixture.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <returns>
        /// 	<c>true</c> if the fixture is ignored; otherwise, <c>false</c>.
        /// </returns>
        public bool IsFixtureIgnored(Fixture fixture)
        {
            if (_collisionIgnores.ContainsKey(fixture.FixtureId))
                return _collisionIgnores[fixture.FixtureId];

            return false;
        }

        /// <summary>
        /// Contacts are persistant and will keep being persistant unless they are
        /// flagged for filtering.
        /// This methods flags all contacts associated with the body for filtering.
        /// </summary>
        private void FilterChanged()
        {
            // Flag associated contacts for filtering.
            ContactEdge edge = _fixture.Body.ContactList;
            while (edge != null)
            {
                Contact contact = edge.Contact;
                Fixture fixtureA = contact.FixtureA;
                Fixture fixtureB = contact.FixtureB;
                if (fixtureA == _fixture || fixtureB == _fixture)
                {
                    contact.FlagForFiltering();
                }

                edge = edge.Next;
            }
        }
    }



    #region ShadowplayMod  
     // A polygon shape that has all   this is a bit of a hack but allows a clean implementation of bouyancy for air filled hulls.  does not do collision
    /// </summary>
    public class WorldPolygonShape : Shape
    {
        public Vector2[] verticesWCS;

        public WorldPolygonShape(Vector2[] verts, float density):base( density)
        {
            verticesWCS = verts;
        }

        public override int ChildCount
        {
            get { throw new NotImplementedException(); }
        }

        public override Shape Clone()
        {
            throw new NotImplementedException();
        }

        public override bool TestPoint(ref Transform transform, ref Vector2 point)
        {
            throw new NotImplementedException();
        }

        public override bool RayCast(out RayCastOutput output, ref RayCastInput input, ref Transform transform, int childIndex)
        {
            throw new NotImplementedException();
        }

        public override void ComputeAABB(out AABB aabb, ref Transform transform, int childIndex)
        {
            throw new NotImplementedException();
        }

        public override void ComputeProperties()
        {  }

        public override float ComputeSubmergedArea(Vector2 normal, float offset, Transform xf, out Vector2 sc)
        {
            throw new NotImplementedException();
        }
    }

    #endregion


    /// <summary>
    /// A fixture is used to attach a Shape to a body for collision detection. A fixture
    /// inherits its transform from its parent. Fixtures hold additional non-geometric data
    /// such as friction, collision filters, etc.
    /// Fixtures are created via Body.CreateFixture.
    /// Warning: You cannot reuse fixtures.
    /// </summary>
    [DataContract(Name = "Fixture", Namespace = "http://ShadowPlay", IsReference = true)]
    public class Fixture 
    {
        private static int _fixtureIdCounter;

        /// <summary>
        /// Fires after two shapes has collided and are solved. This gives you a chance to get the impact force.
        /// </summary>
        public AfterCollisionEventHandler AfterCollision;

        /// <summary>
        /// Fires when two fixtures are close to each other.
        /// Due to how the broadphase works, this can be quite inaccurate as shapes are approximated using AABBs.
        /// </summary>
        public BeforeCollisionEventHandler BeforeCollision;

        public CollisionFilter CollisionFilter { get; private set; }

        /// <summary>
        /// Fires when two shapes collide and a contact is created between them.
        /// Note that the first fixture argument is always the fixture that the delegate is subscribed to.
        /// </summary>
        public OnCollisionEventHandler OnCollision;

        /// <summary>
        /// Fires when two shapes separate and a contact is removed between them.
        /// Note that the first fixture argument is always the fixture that the delegate is subscribed to.
        /// </summary>
        public OnSeparationEventHandler OnSeparation;

        public FixtureProxy[] Proxies;
        public int ProxyCount;


        #region Shadowplay Mod
        
        private Fixture() { }
        /// <summary>
        /// Creates a Fixture that just has a polygon shape and used to create a light fixture just for buoyancy, no collision , convenient for the total bouyancy method.
        /// </summary>
        /// <param name="verts">verts in WCS</param>
        /// <returns></returns>    
        public  static Fixture CreateAirFixture(Vector2[] vertsWCS, float density )
        {        
            Fixture airFixture =  new Fixture();
            airFixture.Shape = new WorldPolygonShape(vertsWCS, density);
            airFixture.Shape.Density = density;
            return airFixture;
        }
        #endregion



        public Fixture(Body body, Shape shape)
            : this(body, shape, null)
        {
        }

        public Fixture(Body body, Shape shape, Object userData)
        {
            CollisionFilter = new CollisionFilter(this);

            //TODO CODE REVIEW  should this be  this shadowplay mods?                    
            //dh added back after merged out
            Friction = body.Friction;
            Restitution = body.Restitution;
            Density = body.Density;
        
            IsSensor = false;
            Body = body;
           //TODO CODE REVIEW , why are we passing density though this.

            // i think we can remove denstiy being passed around..
            UserData = userData;

            if (Settings.ConserveMemory)
                Shape = shape;
            else
                Shape = shape.Clone();

             if (Body.World != null
                && !( Body is Particle && Body.IsNotCollideable)) // Shadowplay Mods IsNotCollideable
            {
                // Reserve proxy space
                int childCount = Shape.ChildCount;
                Proxies = new FixtureProxy[childCount];
                for (int i = 0; i < childCount; ++i)
                {
                    Proxies[i] = new FixtureProxy();
                    Proxies[i].Fixture = null;
                    Proxies[i].ProxyId = BroadPhase.NullProxy;
                }
                ProxyCount = 0;
            }

            FixtureId = _fixtureIdCounter++;

            if (Body.FixtureList == null)
            {
                Body.FixtureList = new List<Fixture>();
            }

            if (Body.FixtureList != null)//// Shadowplay Mods IsNotCollideable
                Body.FixtureList.Add(this);

            //for  IsNotCollideable , particle  fixtures should not even be in the tree.  they will never be allowed to collide orneed to be selected 
            if (Body.World!= null &&
                (Body.Flags & BodyFlags.Enabled) == BodyFlags.Enabled
           
#if SILVERLIGHT || PRODUCTION 
                && !Body.IsNotCollideable   //for clouds and other things dont create proxies unless in Tool where we might need to select
#else
                && !(Body is Particle && Body.IsNotCollideable)
#endif
             )// Shadowplay Mods IsNotCollideable
            {
                BroadPhase broadPhase = Body.World.ContactManager.BroadPhase;
                CreateProxies(broadPhase, ref Body.Xf);
            }


            // Adjust mass properties if needed.
            if (Shape._density > 0.0f)
            {
                Body.ResetMassData();
            }


            // Let the world know we have a new fixture. This will cause new contacts
            // to be created at the beginning of the next time step.

            if (Body.World != null)
            {
                Body.World.Flags |= WorldFlags.NewFixture;

                if (Body.World.FixtureAdded != null)
                {
                    Body.World.FixtureAdded(this);
                }
            }
        }

        /// <summary>
        /// Get the type of the child Shape. You can use this to down cast to the concrete Shape.
        /// </summary>
        /// <value>The type of the shape.</value>
        public ShapeType ShapeType
        {
            get { return Shape.ShapeType; }
        }

        /// <summary>
        /// Get the child Shape. You can modify the child Shape, however you should not change the
        /// number of vertices because this will crash some collision caching mechanisms.
        /// </summary>
        /// <value>The shape.</value>
        [DataMember(Order = 3)]
        public Shape Shape
        {
            get;
            //private set;
            set;    // for deserialization only, do not access.
        }

        /// <summary>
        /// Gets or sets a value indicating whether this fixture is a sensor.
        /// </summary>
        /// <value><c>true</c> if this instance is a sensor; otherwise, <c>false</c>.</value>
        [DataMember(Order = 1)]
        public bool IsSensor { get; set; }

        /// <summary>
        /// Get the parent body of this fixture. This is null if the fixture is not attached.
        /// </summary>
        /// <value>The body.</value>
        [DataMember(Order = 2)]
        public Body Body
        {
            get;
            //internal set;
            set;    // for deserialization only, do not access.
        }

        /// <summary>
        /// Set the user data. Use this to store your application specific data.
        /// </summary>
        /// <value>The user data.</value>
        public object UserData { get; set; }


        private float _density = 1;
        /// <summary>
        /// Gets or sets the density.
        /// </summary>
        /// <value>The density.</value>
        [DataMember(Order = 4)]
        public float Density
        {
            get 
            {
                if (Shape != null)
                {
                    _density = Shape._density;
                    return Shape._density;
                }
                return _density;
            }
            set
            {
                if (Shape != null)
                {
                    Shape._density = value;
                    // Adjust mass properties if needed.
                    if (Shape._density > 0.0f)
                    {
                        Body.ResetMassData();
                    }
                }

                _density = value;
            }
        }

        /// <summary>
        /// Get or set the coefficient of friction.
        /// </summary>
        /// <value>The friction.</value>
        [DataMember(Order = 0)]
        public float Friction { get; set; }

        /// <summary>
        /// Get or set the coefficient of restitution.
        /// </summary>
        /// <value>The restitution.</value>
        [DataMember]
        public float Restitution { get; set; }

        /// <summary>
        /// Gets a unique ID for this fixture.
        /// </summary>
        /// <value>The fixture id.</value>
        public int FixtureId { get; private set; }

    
 

        /// <summary>
        /// Test a point for containment in this fixture.
        /// </summary>
        /// <param name="point">A point in world coordinates.</param>
        /// <returns></returns>
        public bool TestPoint(ref Vector2 point)
        {
            return Shape.TestPoint(ref Body.Xf, ref point);
        }

        #region ShadowPlay Mod
#if ACCESS_LAST_FRAME
        /// <summary>
        /// Test a point for containment in this as it appeared last frame.   Useful for on collide handlers, since solver has already moved the body
        /// </summary>
        /// <param name="point">A point in world coordinates.</param>
        /// <returns></returns>
        public bool TestPointLastFrame(ref Vector2 point)
        {
            return Shape.TestPoint(ref Body.XfLastFrame, ref point);
        }

        public bool RayCastLastFrame(out RayCastOutput output, ref RayCastInput input, int childIndex)
        {
            return Shape.RayCast(out output, ref input, ref Body.XfLastFrame, childIndex);
        }
#endif

        #endregion


        /// <summary>
        /// Cast a ray against this Shape.
        /// </summary>
        /// <param name="output">The ray-cast results.</param>
        /// <param name="input">The ray-cast input parameters.</param>
        /// <param name="childIndex">Index of the child.</param>
        /// <returns></returns>
        public bool RayCast(out RayCastOutput output, ref RayCastInput input, int childIndex)
        {
            return Shape.RayCast(out output, ref input, ref Body.Xf, childIndex);
        }

        /// <summary>
        /// Get the mass data for this fixture. The mass data is based on the density and
        /// the Shape. The rotational inertia is about the Shape's origin.
        /// </summary>
        public MassData GetMassData()
        {
            return Shape.MassData;
        }

        /// <summary>
        /// Get the fixture's AABB. This AABB may be enlarge and/or stale.
        /// If you need a more accurate AABB, compute it using the Shape and
        /// the body transform.
        /// </summary>
        /// <param name="aabb">The aabb.</param>
        /// <param name="childIndex">Index of the child.</param>
        public void GetAABB(out AABB aabb, int childIndex)
        {
            Debug.Assert(0 <= childIndex && childIndex < ProxyCount);
            aabb = Proxies[childIndex].AABB;
        }

        internal void Destroy()
        {
            // The proxies must be destroyed before calling this.
            Debug.Assert(ProxyCount == 0);

            // Free the proxy array.
            Proxies = null;

            Shape = null;

            if (Body.World.FixtureRemoved != null)
            {
                Body.World.FixtureRemoved(this);
            }
        }

        // These support body activation/deactivation.
        internal void CreateProxies(BroadPhase broadPhase, ref Transform xf)
        {
            Debug.Assert(ProxyCount == 0);

            // Create proxies in the broad-phase.
            ProxyCount = Shape.ChildCount;

            for (int i = 0; i < ProxyCount; ++i)
            {
                FixtureProxy proxy = Proxies[i];
                Shape.ComputeAABB(out proxy.AABB, ref xf, i);
                proxy.Fixture = this;
                proxy.ChildIndex = i;
                proxy.ProxyId = broadPhase.CreateProxy(ref proxy.AABB, ref proxy);

                Proxies[i] = proxy;
            }
        }

        internal void DestroyProxies(BroadPhase broadPhase)
        {
            // Destroy proxies in the broad-phase.
            for (int i = 0; i < ProxyCount; ++i)
            {
                broadPhase.DestroyProxy(Proxies[i].ProxyId);
                Proxies[i].ProxyId = BroadPhase.NullProxy;
            }

            ProxyCount = 0;
        }

        internal void Synchronize(BroadPhase broadPhase, ref Transform transform1, ref Transform transform2)
        {
            if (ProxyCount == 0)
            {
                return;
            }

            for (int i = 0; i < ProxyCount; ++i)
            {
                FixtureProxy proxy = Proxies[i];

                // Compute an AABB that covers the swept Shape (may miss some rotation effect).
                AABB aabb1, aabb2;
                Shape.ComputeAABB(out aabb1, ref transform1, proxy.ChildIndex);
                Shape.ComputeAABB(out aabb2, ref transform2, proxy.ChildIndex);

                proxy.AABB.Combine(ref aabb1, ref aabb2);

                Vector2 displacement = transform2.Position - transform1.Position;

                broadPhase.MoveProxy(proxy.ProxyId, ref proxy.AABB, displacement);
            }
        }

        #region ShadowPlay Mods

        //clear any listeners, who cares who or what is listeing dangling listeners can mess up GC.
        public void ClearOnCollisionListeners()
        {
            OnCollision = null;  // supposed to be done inside call.. will compile but supposedly doesnt work  
            //  http://stackoverflow.com/questions/153573/how-can-i-clear-event-subscriptions-in-c
            //NOTE this dont not work.. needs to be inside the class TODO look for other mistakes like this.
            //http://stackoverflow.com/questions/8892579/does-assigning-null-remove-all-event-handlers-from-an-object
        }

        /*
         * Note: 
         * Not all Fixture is deserialized. If Body.GeneralVertices is available, 
         * Fixture will be rebuilt on deserialization.
         * 
         * Note: 
         * Fixture de/serialization should only being called from Body de/serialization.
         * So Body should deserialize first, then it will call Fixture deserialization 
         * automatically. 
         * The reverse, which may happen when any Fixture is marked as [DataMember], 
         * will throw exception. This is the current limitation.
         */

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            
            // Reserve proxy space
            int childCount = Shape.ChildCount;
            Proxies = new FixtureProxy[childCount];
            for (int i = 0; i < childCount; ++i)
            {
                Proxies[i] = new FixtureProxy();
                Proxies[i].Fixture = null;
                Proxies[i].ProxyId = BroadPhase.NullProxy;
            }

            FixtureId = _fixtureIdCounter++;
            CollisionFilter = new CollisionFilter(this);
        }

        #endregion
    }
}