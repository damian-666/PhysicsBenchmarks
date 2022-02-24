/*
 * Planet class.
 * 
 * 
 * Planet spirit, geom, & joint are part of level, stored on level. Here are references.
 * 
 * TODO: 
 * - When planet is static, all joint positions are not updated.
 * - Behavior when planet piece broken is still not valid.
 * 
 * Copyright Shadowplay Studios, 2010.
 */

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Collision;
using FarseerPhysics.Common;
using FarseerPhysics.Controllers;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Factories;

using Core.Data.Interfaces;
using Core.Data.Geometry;
using System.Diagnostics;
using Core.Data.Collections;

namespace Core.Data.Entity
{

    //TODO move all this to a plugin, dont need a class

   //.Might not be enough justification to have a PlanetEntity, its old code too much generation time code in it.
   //generator stuff is mixed in here, might be refs.. its shouldnt complicate this 
   //class anywasy, its used once to generate
 [DataContract(Name = "Planet", Namespace = "http://ShadowPlay", IsReference = true)]
    public class Planet :  IEntity
    {
        private Spirit _spirit;
        private PointGravity _pointGravity;
        private bool _isStatic;  //TODO this should probably derive from spirit..

        // primarily used for camera orientation
        //private List<Body> _geomsToCheckCollide;

        #region Constructor

        public Planet()
        {
            Initialize();
        }

        public Planet(PlanetShapeGenerator planetShape)
        {
            Initialize();
            // default grid size = 30
            InitializeGeoms(planetShape);
        }

        private void Initialize()
        {
        //    _spirit = new Spirit();   //move this till after we have joints and bodies.
            _pointGravity = new PointGravity();// adds a controller here

            // setup gravity
            _pointGravity.GravityType = GravityType.Linear;
        }

        private void InitializeGeoms(PlanetShapeGenerator shape)
        {
            // initialize planet geoms
            if (shape == null) throw new ArgumentNullException("planetShape");

            // add pieces geoms
            List<Body> bodies = new List<Body>();
            foreach (Vertices verts in shape.Pieces)
            {
                Body body = BodyFactory.CreateBody(
                    World.Instance, Vector2.Zero, verts, 1f);

                //geom.FrictionCoefficient = 1.0f;
                //geom.RestitutionCoefficient = 0.6f;

                // add collision handler, to handle when creature collide with 
                // planet. this is currently disabled.
                //geom.OnCollision += OnPlanetGeomCollided;

                bodies.Add(body);
            }


            // if planet only consist of single piece, associate gravity controller to it.
            if (bodies.Count == 1)
            {
                _pointGravity.AttachedBody = bodies[0];
            }


            _spirit = new Spirit( bodies);



            // initialize joints
            AddJointBetweenPieces(shape);
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            //foreach (Body g in _spirit.Bodies)
            //{
            //   don't forget to set collision handler
            //   g.OnCollision += OnPlanetGeomCollided;
            //}
        }

        #endregion


        #region Public Methods

        public void InsertToPhysics(World physics)
        {
            // because spirit, geom, & joint should be part of level, we only
            // handle gravity controller here
            if (physics.Controllers.Contains(_pointGravity) == false)
                physics.AddController(_pointGravity);
        }

        public void DisposePhysics()
        {
            World.Instance.RemoveController(_pointGravity);
        }

        // used on initial creation
        public void MoveAllGeoms(Vector2 displacement)
        {
            if (_spirit == null) return;

            Body body;
            int max = _spirit.Bodies.Count;
            for (int i = 0; i < max; i++)
            {
                body = _spirit.Bodies[i];
                if (body != null) body.Position += displacement;
            }

            //// correct joint position along with body.
            //// to make joints able to update, static body must false.
            //bool stc = _isStatic;
            //MakePlanetStatic(false);
            //foreach (PoweredJoint pj in _spirit.Joints) pj.Active.Update();
            //MakePlanetStatic(stc);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Set gravity position to be at planet center.
        /// </summary>
        private void UpdateGravityCenter()
        {
            // If planet consist only of single piece, gravity controller is already 
            // attached to planet body, so gravity pos has been auto updated.
            // This check is required to reduce extreme sudden movement of planet 
            // when consist of only single piece.
            if (_spirit.Bodies.Count == 1) return;

 
            _pointGravity.Position = _spirit.WorldCenter;

            // TODO (?): if all planet pieces body is disabled, gravity 
            // controller should not generate gravity.
        }

        // to add joint to connect all geoms, we use planet shape's radius and 
        // pieces' angle. note that planet shape and pieces geom here still 
        // centered at (0,0).
        private void AddJointBetweenPieces(PlanetShapeGenerator shape)
        {
            int pcnt = shape.Pieces.Count;
            if (pcnt < 2) return;

            float planetArc = shape.EndAngle - shape.StartAngle;
            float pieceArc = planetArc / pcnt;

            // 0 degree angle. GetAllVertices() return outer followed by inner verts.
            Vector2 v0 = shape.GetAllVertices()[0];
            float angle0 = GeomUtility.GetAngleFromVector(v0.X, v0.Y);
            float endAngle = angle0 + pieceArc;

            // we choose radius (for joint position) located between outer and inner
            float radius = (shape.OuterRadius + shape.InnerRadius) * 0.5f;

            // for all pieces
            PoweredJoint pj;
            Body cur, next;
            for (int i = 0; i < pcnt; i++)
            {
                // get current and next piece
                cur = _spirit.Bodies[i];

                // if full circle 360, connect last piece with the first
                if (i == pcnt - 1)
                {
                    if (planetArc == 360) next = _spirit.Bodies[0];
                    else break;
                }
                else
                {
                    next = _spirit.Bodies[i + 1];
                }

                // connect using joint.
                Vector2 jWPos = PlanetShapeGenerator.CreatePoint(radius, endAngle);
                pj = new PoweredJoint(cur, next, jWPos);

                // broke when pieces exposed to force > 100, or when angle between 
                // 2 pieces is more than 45 degree.
                //pj.RevoluteBreakpoint = 100f;
                //pj.AngularBreakpoint = MathHelper.ToRadians(45);

                _spirit.Joints.Add(pj);

                endAngle += pieceArc;   // next piece
            }
        }

    

        private void MakePlanetStatic(bool enabled)
        {
            if (_spirit == null) return;
            foreach (Body b in _spirit.Bodies) b.IsStatic = enabled;
        }

        #endregion


        #region Properties

        /// <summary>
        /// Spirit for planet structure.
        /// </summary>
        [DataMember]
        public Spirit Spirit
        {
            get { return _spirit; }
            set { _spirit = value; }
        }

        [DataMember]
        public PointGravity PointGravity
        {
            get { return _pointGravity; }
            set { _pointGravity = value; }  // for deserialize only
        }

        //public List<Body> GeomsToCheckCollide
        //{
        //    get { return _geomsToCheckCollide; }
        //    set { _geomsToCheckCollide = value; }
        //}

        /// <summary>
        /// When this is set, all planet pieces will become static body.
        /// </summary>
        public bool IsStatic
        {
            get { return _isStatic; }
            set
            {
                MakePlanetStatic(value);
                _isStatic = value;
            }
        }

        #endregion


        #region IEntity Members

        public void Update( double dt)
        {
            //if (_isStatic == true) return;

            UpdateGravityCenter();
            //UpdatePiecesState();
        }




        #endregion

        #region IEntity Members


        public Vector2 WorldCenter
        {
            get { return Position; }
        }

        #endregion

        #region IEntity Members


        public void UpdateAABB()
        {
            Spirit.UpdateAABB();
        }

        #endregion

        #region IEntity Members



        public Vector2 Position
        {
            get { return this.Spirit.Position; }
            set { this.Spirit.Position = value; }
        }

        public float Rotation
        {
            get { return this.Spirit.Rotation; }
            set { this.Spirit.Rotation = value; }
        }

        public AABB EntityAABB
        {
            get { return this.Spirit.AABB; }
        }
        float IEntity.Rotation
        {
            get
            {
                return Spirit.Rotation;
            }
            set
            {
                Spirit.Rotation = value;
            }
        }

        AABB IEntity.EntityAABB
        {
            get { return Spirit.EntityAABB; }
        }

        void IEntity.UpdateAABB()
        {
        
        }

        void IEntity.Update(double dt)
        {
            Update(dt);
            
        }

        public void UpdateBK(double dt)        {        }

        public void Draw(double dt)
        {
            ((IEntity)Spirit).Draw(dt);
        }

        public void UpdateThreadSafe(double dt)
        {
            ((IEntity)_spirit).UpdateThreadSafe(dt);
        }

        bool IEntity.WasSpawned
        {
            get { return _spirit.WasSpawned; }
        }

        public int ID
        {
            get
            {
                return _spirit.ID;
            }
        }

        public Vector2 LinearVelocity
        {
            get
            {
                return ((IEntity)_spirit).LinearVelocity;
            }
        }



        public float AngularVelocity
        {
            get
            {
                return _spirit.AngularVelocity;
            }
        }

        public ViewModel ViewModel => _spirit.ViewModel;

        public Transform Transform => ((IEntity)_spirit).Transform;

        public  IEnumerable< IEntity> Entities => _spirit.Entities;

        public byte[] Thumbnail => _spirit.Thumbnail;

        public string Name => ((IEntity)Spirit).Name;

        public string PluginName => ((IEntity)Spirit).PluginName;

        #endregion
    }


    // related methods to create planet-like shape, centered at (0,0).
    // angle is calculated from 6 o'clock, counterclockwise, in degree.
    public class PlanetShapeGenerator
    {
        // private vars
        private Vertices _outerVerts;
        private Vertices _innerVerts;
        private List<Vertices> _pieces;

        // public parameter
        public float StartAngle;
        public float EndAngle;
        public float OuterRadius;
        public float InnerRadius;
        public int NumOfOuterEdges;
        public int NumOfInnerEdges;


        public PlanetShapeGenerator()
        {
            _outerVerts = new Vertices();
            _innerVerts = new Vertices();
            _pieces = new List<Vertices>();
        }


        #region Public Methods

        // create vertices for basic circle shape for planet
        public void CreateBasicShape()
        {
            // get circle arc to be generated, clamped to 360 degree max
            float arc = EndAngle - StartAngle;
            arc = MathUtils.Clamp(arc, 0, 360);

            // to make angle calculation start from 6 o'clock
            float startA = StartAngle - 90;
            float endA = EndAngle - 90;

            // create the arc
            _outerVerts = CreateArc(startA, endA, OuterRadius, NumOfOuterEdges);

            if (InnerRadius > 0) _innerVerts =    CreateArc(startA, endA, InnerRadius, NumOfInnerEdges);

            // if doesn't have hole
            if (InnerRadius == 0)
            {
                // if full circle is used, delete last vertex
                if (arc == 360)
                {
                    _outerVerts.RemoveAt(_outerVerts.Count - 1);
                }
                // when not full circle, inner vertex will only contain circle center
                else
                {
                    _innerVerts.Add(Vector2.Zero);
                }
            }
        }

        /// <summary>Randomize existing surface points.</summary>
        /// <param name="randSurfaceP">Percentage of surface that will be randomized.</param>
        /// <param name="surfaceDispP">Displacement of a point along the surface,
        /// in percentage of distance (in arc degree) between points.</param>
        public void RandomizeSurfacePoint(float randSurfaceP, float minElev,
            float maxElev, float surfaceDispP)
        {
            if (_outerVerts.Count == 0) return;

            float arc = EndAngle - StartAngle;
            surfaceDispP = MathUtils.Clamp(surfaceDispP, 0, 100) * 0.01f;

            // get list of point (its index only) that will be randomized
            randSurfaceP = MathUtils.Clamp(randSurfaceP, 0, 100) * 0.01f;
            int numOfRandomPoint = (int)(_outerVerts.Count * randSurfaceP);
            List<int> randPoints = GeomUtility.GetRandListNoDuplicate(
                0, _outerVerts.Count, numOfRandomPoint);

            // initial point angle (for case when crossing 0 angle)
            Vector2 point0 = _outerVerts[0];
            float angle0 = GeomUtility.GetAngleFromVector(point0.X, point0.Y);

            float elevation, angle;
            float curAngle, leftAngle, rightAngle;
            Vector2 curPoint, leftPoint, rightPoint;
            float leftArcDist, rightArcDist;

            // for each random point
            foreach (int curIndex in randPoints)
            {
                curPoint = _outerVerts[curIndex];

                // get its degree from center
                curAngle = GeomUtility.GetAngleFromVector(curPoint.X, curPoint.Y);

                // randomize elevation based on OuterRadius
                elevation = OuterRadius + MathUtils.RandomNumber(minElev, maxElev);

                leftArcDist = 0;
                rightArcDist = 0;

                // get distance to point before and after current point
                if (curIndex != 0 && curIndex != _outerVerts.Count - 1)
                {
                    leftPoint = _outerVerts[curIndex - 1];
                    rightPoint = _outerVerts[curIndex + 1];
                    leftAngle = GeomUtility.GetAngleFromVector(leftPoint.X, leftPoint.Y);
                    rightAngle = GeomUtility.GetAngleFromVector(rightPoint.X, rightPoint.Y);

                    // apply distance only when the order is correct, in case 
                    // crossing 0 degree border.
                    if (CorrectAngleOrder(
                        angle0, curAngle, ref leftAngle, ref rightAngle) == true)
                    {
                        leftArcDist = (curAngle - leftAngle) * surfaceDispP;
                        rightArcDist = (rightAngle - curAngle) * surfaceDispP;
                    }
                }

                // for case when reverse direction
                float min = Math.Min(-leftArcDist, rightArcDist);
                float max = Math.Max(-leftArcDist, rightArcDist);

                // randomize displacement along the surface
                angle = curAngle + MathUtils.RandomNumber(min, max);

                // change current point
                float rad = MathHelper.ToRadians(angle);
                curPoint.Y = -(elevation * MathUtils.Sin(rad));
                curPoint.X = elevation * MathUtils.Cos(rad);

                // for full circle to close correctly, last point must have 
                // the same position as the first.
                if (curIndex == _outerVerts.Count - 1 && arc == 360)
                {
                    curPoint.X = _outerVerts[0].X;
                    curPoint.Y = _outerVerts[0].Y;
                }

                _outerVerts.RemoveAt(curIndex);
                _outerVerts.Insert(curIndex, curPoint);
            }
        }

        /// <summary>
        /// Add random crater to the surface.
        /// </summary>
        /// <param name="randSurfaceP">Percentage of surface that will be randomized.</param>
        public void AddRandomCrater(float randSurfaceP, float minArcAngle, float maxArcAngle,
            float minDepth, float maxDepth, int minEdge, int maxEdge)
        {
            float arc = EndAngle - StartAngle;
            Random r = new Random();

            float available = randSurfaceP * 0.01f * arc;
            float spaceAvail = arc - available;
            if (available == 0) return;

            // randomize arc width for crater
            List<float> angles = new List<float>();
            while (available >= minArcAngle)
            {
                float angle = MathUtils.RandomNumber(minArcAngle, maxArcAngle);
                if (angle <= available)
                {
                    available -= angle;
                    angles.Add(angle);
                }
                else    // last available
                {
                    angle = available;
                    available = 0;
                    angles.Add(angle);
                }
            }

            // randomize space between crater
            int max = angles.Count + 1;
            float[] spaces = new float[max];
            float distUnit = spaceAvail / 20;
            if (spaceAvail > 0)
            {
                while (spaceAvail >= distUnit)
                {
                    int slot = r.Next(0, max);
                    spaces[slot] += distUnit;
                    spaceAvail -= distUnit;
                }
                if (spaceAvail > 0)     // last available
                {
                    int slot = r.Next(0, max);
                    spaces[slot] += spaceAvail;
                    spaceAvail = 0;
                }
            }

            // create random crater
            max = angles.Count;
            float startAngle = 0;
            for (int i = 0; i < max; i++)
            {
                // space first then crater
                startAngle += spaces[i];
                float arcAngle = angles[i];

                float depth = MathUtils.RandomNumber(minDepth, maxDepth);
                int numOfEdge = r.Next(minEdge, maxEdge);

                CreateCrater(startAngle, arcAngle, depth, numOfEdge);
                startAngle += arcAngle;
            }
        }

        /// <summary>
        /// Divide the planet shape to equal parts based on angle. This will 
        /// create separate collection of vertices for each part, which 
        /// represent a closed polygon.
        /// </summary>
        /// <param name="numOfSlices"></param>
        public void Subdivide(int numOfParts)
        {
            if (numOfParts <= 1) return;

            _pieces.Clear();
            float planetArc = EndAngle - StartAngle;
            float pieceArc = planetArc / numOfParts;
            float startAngle = 0;

            // 0 degree angle refers to _outerVerts[0]. inner verts should be the same.
            Vector2 v0 = _outerVerts[0];
            float angle0 = GeomUtility.GetAngleFromVector(v0.X, v0.Y);
            startAngle += angle0;
            float endAngle = startAngle + pieceArc;

            // when subdividing full 360 circle with no inner vertices, first we 
            // should duplicate the first vertex of outer surface to its end.
            if (InnerRadius == 0 && planetArc == 360) _outerVerts.Add(_outerVerts[0]);

            // for all surface slices
            for (int sn = 0; sn < numOfParts; sn++)
            {
                Vertices vs = new Vertices();
                Vertices outer = GetVerticesBetween(
                    _outerVerts, angle0, startAngle, endAngle);

                Vertices inner;
                if (InnerRadius > 0)
                {
                    inner = GetVerticesBetween(
                          _innerVerts, angle0, startAngle, endAngle);
                }
                else
                {
                    inner = new Vertices();
                    inner.Add(Vector2.Zero);
                }

                vs.AddRange(outer);
                inner.Reverse();  // reverse the inner first
                vs.AddRange(inner);
                _pieces.Add(vs);  // add to planet slices

                // next slice
                startAngle = endAngle;
                endAngle += pieceArc;
            }
        }

        /// <summary>
        /// Add crater to planet surface. Angle based.
        /// </summary>
        /// <param name="startAngle">start. must be greater than or equal 0</param>
        /// <param name="arcAngle">angle width, must be positive. end angle derived from this.</param>
        public void CreateCrater(float startAngle, float arcAngle, float depth, int numOfEdges)
        {
            // clamp angle
            startAngle = MathUtils.Clamp(startAngle, 0, 360);
            arcAngle = MathUtils.Clamp(arcAngle, 0, 360);
            if ((startAngle + arcAngle) > 360) arcAngle = 360 - startAngle;

            if (arcAngle <= 0 || _outerVerts.Count == 0) return;

            // _outerVerts[0] is not always located on 0 degree angle
            Vector2 v0 = _outerVerts[0];
            float angle0 = GeomUtility.GetAngleFromVector(v0.X, v0.Y);
            startAngle += angle0;
            float endAngle = startAngle + arcAngle;

            Vector2 v;
            int start = 0;
            int end = 0;
            int max = _outerVerts.Count;
            int i = 0;

            // find start vertices
            for (; i < max; i++)
            {
                v = _outerVerts[i];
                float angle = GeomUtility.GetAngleFromVector(v.X, v.Y);
                if (angle < angle0) angle += 360;

                if (angle >= startAngle)
                {
                    // insert our start vertices, in case it has already randomized
                    _outerVerts.Insert(i, CreatePoint(OuterRadius, startAngle));

                    start = i++;
                    break;
                }
            }

            // find end vertices
            max = _outerVerts.Count;
            for (; i < max; i++)
            {
                v = _outerVerts[i];
                float angle = GeomUtility.GetAngleFromVector(v.X, v.Y);
                if (angle < angle0) angle += 360;

                if (angle >= endAngle)
                {
                    // insert our own end vertices, in case it has already randomized
                    _outerVerts.Insert(i, CreatePoint(OuterRadius, endAngle));

                    end = i;
                    break;
                }
            }

            // create crater
            if (end > start) CreateCrater(start, end, depth, numOfEdges);
        }

        // create circle arc. follow standard cartesian circle equation, angle 
        // is calculated counterclockwise, start from 3 o'clock (x=radius, y=0).
        public static Vertices CreateArc(float startAngle, float endAngle,
            float radius, int numberOfEdges)
        {
            float angle, x, y;
            Vertices verts = new Vertices();

            // get circle arc to be generated, clamped to 360 degree max
            float arc = endAngle - startAngle;

            // when crossing the 0 degree, change end angle to above 360
            if (arc < 0)
            {
                endAngle += 360;
                arc = endAngle - startAngle;
            }

            arc = MathUtils.Clamp(arc, 0, 360);
            if (arc == 0) return verts;

            float stepSize = MathHelper.ToRadians(arc) / numberOfEdges;
            startAngle = MathHelper.ToRadians(startAngle);  // convert it to rad

            for (int i = 0; i <= numberOfEdges; i++)
            {
                angle = startAngle + (stepSize * i);

                // get the surface point. negate y, because graphics -y is upward,
                // while cartesian -y is downward
                y = -(radius * MathUtils.Sin(angle));
                x = radius * MathUtils.Cos(angle);
                verts.Add(new Vector2(x, y));
            }
            return verts;
        }

        /// <summary> Note: graphics oriented, Y direction is reversed. </summary>
        /// <param name="angle">Angle in degree.</param>
        public static Vector2 CreatePoint(float radius, float angle)
        {
            float rad = MathHelper.ToRadians(angle);
            //double rad = angle * MathHelper.RadiansToDegreesRatio;
            float y = (float)-(radius * Math.Sin(rad));
            float x = (float)(radius * Math.Cos(rad));
            return new Vector2(x, y);
        }

        // return all planet vertices without subdivide pieces involved
        public Vertices GetAllVertices()
        {
            Vertices verts = new Vertices();

            // reverse the inner first to make a convex polygon later.
            _innerVerts.Reverse();

            // add vertices from outer surface, then from inner
            verts.AddRange(_outerVerts);
            verts.AddRange(_innerVerts);

            return verts;
        }

        #endregion


        #region Private Methods

        // add crater to planet surface. start & end index refers to surface vertices.
        // remember that this method will modify the contents of surface vertices,
        // which means the index sequence will be different after calling this.
        private void CreateCrater(int startIdx, int endIdx, float depth, int numOfEdges)
        {
            if (startIdx == endIdx || _outerVerts.Count == 0) return;

            Vector2 start = _outerVerts[startIdx];
            Vector2 end = _outerVerts[endIdx];
            Vector2 distance = end - start;
            Vector2 midpoint = start + (distance * 0.5f);
            float diameter = distance.Length();
            float radius = diameter * 0.5f;

            float bottomR = OuterRadius - depth;
            bottomR = MathUtils.Clamp(bottomR, InnerRadius, OuterRadius);
            float midPointR = midpoint.Length();

            // delete original vertices first
            DeleteVerticesBetween(_outerVerts, startIdx, endIdx);

            // if bottom is the same or above the midpoint, a crater cannot
            // be created. a stright line will be created instead.
            if (bottomR >= midPointR) return;

            // get depth point from the midpoint
            Vector2 bottomV = midpoint * (bottomR / midPointR);

            // find a circle or curve that can pass through these 3 point
            // (start, end, bottom)
            Vector2 cCenter;
            float cRadius;
            bool cfound = GeomUtility.GetCircleFrom3Point(
                start, end, bottomV, out cCenter, out cRadius);
            if (cfound == false) return;

            // if a circle exist, get the start & end angle
            Vector2 cStart = start - cCenter;
            Vector2 cEnd = end - cCenter;
            float cStartA = GeomUtility.GetAngleFromVector(cStart.X, cStart.Y);
            float cEndA = GeomUtility.GetAngleFromVector(cEnd.X, cEnd.Y);

            // create the arc. angle must be counterclockwise. reverse start & end.
            Vertices cVerts = CreateArc(cEndA, cStartA, cRadius, numOfEdges);

            // translate it to planet surface
            cVerts.Translate(ref cCenter);

            // reverse the arc vertices so it can be inserted into portion of 
            // planet surface which has reversed arc direction.
            cVerts.Reverse();

            // remove original start and end
            _outerVerts.RemoveRange(startIdx, 2);

            // replace original surface vertices with the arc
            _outerVerts.InsertRange(startIdx, cVerts);
        }

        private static void DeleteVerticesBetween(Vertices verts, int startIdx, int endIdx)
        {
            if (startIdx == endIdx) return;
            int start = Math.Min(startIdx, endIdx);
            int end = Math.Max(startIdx, endIdx);
            int range = end - start;
            if (range == 1) return;
            verts.RemoveRange(start + 1, range - 1);
        }

        private Vertices GetVerticesBetween(Vertices vertices, float angle0,
            float startAngle, float endAngle)
        {
            Vertices verticesBetween = new Vertices();

            Vector2 prev = Vector2.Zero;
            float prevAngle = 0;
            int max = vertices.Count;
            int i = 0;

            // find start vertices
            for (; i < max; i++)
            {
                Vector2 v = vertices[i];
                float angle = GeomUtility.GetAngleFromVector(v.X, v.Y);
                if (angle < angle0) angle += 360;

                if (angle >= startAngle)
                {
                    float radius = v.Length();
                    if (angle > startAngle)
                        radius = GetMidpointRadius(prev, v, prevAngle, startAngle, angle);

                    // insert start vertices
                    verticesBetween.Add(CreatePoint(radius, startAngle));
                    break;
                }
                else
                {
                    prev = v;
                    prevAngle = angle;
                }
            }

            // insert vertices between, stop when encounter end vertices
            max = vertices.Count;
            for (; i < max; i++)
            {
                Vector2 v = vertices[i];
                float angle = GeomUtility.GetAngleFromVector(v.X, v.Y);
                if (angle < angle0) angle += 360;

                // insert vertices
                if (angle >= endAngle)
                {
                    float radius = v.Length();
                    if (angle > endAngle)
                        radius = GetMidpointRadius(prev, v, prevAngle, endAngle, angle);

                    verticesBetween.Add(CreatePoint(radius, endAngle));
                    break;
                }
                else
                {
                    verticesBetween.Add(v);
                    prev = v;
                    prevAngle = angle;
                }
            }

            return verticesBetween;
        }

        // find radius of a point between two surface vertices that has a specific angle.
        // here we use angle proportion to find the midpoint radius.
        private float GetMidpointRadius(Vector2 prev, Vector2 next,
            float prevAngle, float midAngle, float nextAngle)
        {
            float prevR = prev.Length();    // previous radius
            float nextR = next.Length();
            float radiusDiff = nextR - prevR;
            if (radiusDiff != 0)
            {
                float angleDiff = nextAngle - prevAngle;
                float angleDiffStart = midAngle - prevAngle;
                if (angleDiff != 0)
                {
                    float radius = prevR + (radiusDiff * angleDiffStart / angleDiff);
                    return radius;
                }
            }
            // default is to use previous point radius
            return prevR;
        }

        // check angle order, must be l<=cur<=r, or l>=cur>=r. 
        // curAngle must be between l & r, with the same distance.
        private bool CorrectAngleOrder(float angle0, float curAngle,
            ref float leftAngle, ref float rightAngle)
        {
            // check order
            if ((rightAngle <= curAngle && curAngle <= leftAngle) ||
                (leftAngle <= curAngle && curAngle <= rightAngle))
            { return true; }

            // get shortest distance
            float ld = Math.Abs(curAngle - leftAngle);
            float rd = Math.Abs(curAngle - rightAngle);
            float sd = MathHelper.Min(ld, rd);

            // get the correct angle point
            float cp1, cp2;
            if (ld == sd) cp1 = leftAngle;
            else if (rd == sd) cp1 = rightAngle;
            else return false;
            if (curAngle - cp1 >= 0) cp2 = curAngle + sd;
            else cp2 = curAngle - sd;

            // correct previous angle
            if (leftAngle == cp1) rightAngle = cp2;
            else if (leftAngle == cp2) rightAngle = cp1;
            else if (rightAngle == cp1) leftAngle = cp2;
            else if (rightAngle == cp2) leftAngle = cp1;

            // re-check order
            if ((rightAngle <= curAngle && curAngle <= leftAngle) ||
                (leftAngle <= curAngle && curAngle <= rightAngle))
            { return true; }

            // something's wrong here
            return false;
        }

        #endregion


        #region Properties

        public List<Vertices> Pieces
        {
            get
            {
                // pieces value must be at least 1
                if (_pieces.Count == 0)
                {
                    _pieces.Add(GetAllVertices());
                }
                return _pieces;
            }
        }

        #endregion

    }

}

