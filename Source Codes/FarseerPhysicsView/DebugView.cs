//#define HIDDENLINESTEST
// Copyright (c) 2017 Kastellanos Nikolaos
/* Original source Farseer Physics Engine:
 * Copyright (c) 2014 Ian Qvist, http://farseerphysics.codeplex.com
 * Microsoft Permissive License (Ms-PL) v1.1
 */

//this code used in physics engine samples, extended to draw the physical items for game display
//#define NEZ

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using FarseerPhysics.Collision;
using FarseerPhysics.Controllers;

//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;

using Farseer.Xna.Framework;
using Microsoft.Xna.Framework;

using MathHelper = Farseer.Xna.Framework.MathHelper;


using Vector2 = Farseer.Xna.Framework.Vector2;  //because Vector2 is persistent type used for serialization of Body in level.. TODO design weakness, should be a no copy and using monogame Vector2.. maybe alias it somehow
using Matrix = Microsoft.Xna.Framework.Matrix;
using Transform = FarseerPhysics.Common.Transform;


using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using FarseerPhysics.Dynamics;
using FarseerPhysics.Common;
using FarseerPhysics.Diagnostics;
using FarseerPhysics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics.Contacts;


using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Maths;
using MGCore;
using Core.Game.MG.Simulation;
using Core.Game.MG.Graphics;
using static Core.Game.MG.Graphics.BaseView;
using Core.Data.Collections;
using FarseerPhysics.Dynamics.Particles;
using Core.Game.MG;
using Core.Data.Entity;
using Core.Data;
using Mathf = MGCore.Mathf;
using System.Threading.Tasks;

using System.Linq;
using static Core.Game.MG.SimWorld;
using Core.Trace;
using Core.Game.MG.Drawing;
using FarseerPhysics.Common.Decomposition;

namespace FarseerPhysicsView
{
    /// <summary>
    /// A debug view shows you what happens inside the physics engine. You can view
    /// bodies, joints, collision fixtures and more.
    /// </summary>
    public class DebugView : DebugViewBase, IDisposable
    {

        public static bool LoadThumbnails = true;
        //Todo  not sure if special list needed
        //for threating copy all writable data , maybe xform verts to WCS by XF on copy
        //change List.Capacity if too small
        //copy all the body.XF, and colr and stuff
        //threaded draw maybe.

        //TODO OPTIMIZEDRAW the List can be culled with viewport on filling using the broadphase collider.

        //Drawing
        private IPrimitiveBatch _primitiveBatch;
        private SpriteBatch _batch;
        private SpriteFont _font;
        private GraphicsDevice _device;

        //fixture vertices static buffer
        private Vector2[] _tempVertices = new Vector2[Settings.MaxPolygonVertices];
        private Vector2[] _tempBodyVertices = new Vector2[1024];

        private List<StringData> _stringData = new List<StringData>();

        //these are in place of msg Boxes they dont get cleared every frame
        private List<StringData> _msgStringData = new List<StringData>();


        private Matrix _localProjection;
        private Matrix _localView;
        private Matrix _localWorld;

        //Shapes
        public Color DefaultShapeColor = new Color(0.9f, 0.7f, 0.7f);
        public Color InactiveShapeColor = new Color(0.5f, 0.5f, 0.3f);
        public Color KinematicShapeColor = new Color(0.5f, 0.5f, 0.9f);
        public Color SleepingShapeColor = new Color(0.6f, 0.6f, 0.6f);
        public Color StaticShapeColor = new Color(0.5f, 0.9f, 0.5f);
        public Color TextColor = Color.White;

        //Contacts
        private int _pointCount;
        private const int MaxContactPoints = 2048;
        private ContactPoint[] _points = new ContactPoint[MaxContactPoints];

        //Debug panel
        public Vector2 DebugPanelPosition = new Vector2(10, 100);
        private TimeSpan _min;
        private TimeSpan _max;
        private TimeSpan _avg;
        private readonly StringBuilder _graphSbMax = new StringBuilder();
        private readonly StringBuilder _graphSbAvg = new StringBuilder();
        private readonly StringBuilder _graphSbMin = new StringBuilder();
        private readonly StringBuilder _debugPanelSbObjects = new StringBuilder();
        private StringBuilder _debugPanelSbUpdate = new StringBuilder();

        //Performance graph
        public bool AdaptiveLimits = true;
        public int ValuesToGraph = 500;
        public TimeSpan MinimumValue;
        public TimeSpan MaximumValue = TimeSpan.FromMilliseconds(10);
        private readonly List<TimeSpan> _graphValues = new List<TimeSpan>(500);

        public Rectangle PerformancePanelBounds = new Rectangle(330, 100, 400, 200);
        // public Rectangle PerformancePanelBounds = new Rectangle(330, 100, 200, 100);
        private readonly Vector2[] _background = new Vector2[4];
        public bool Enabled = true;

        public const int CircleSegments = 32;
        private Complex circleSegmentRotation = Complex.FromAngle((float)(Math.PI * 2.0 / CircleSegments));


        BasicEffect _scalingEffect;

        public float TextScale = 1.0f;
        public float TextTransparency = 1.0f;

        public float VectorTransparency = 1.0f;

        public BlendState DefaultBlendState = BlendState.AlphaBlend;

        public BlendState DefaultTextureBlendState = BlendState.AlphaBlend;


        private float fontWidth = 1f;


        /// <summary>
        /// Used for drawing particles zoomed out, we could jsut draw one pixel
        /// </summary>
        public float PixelsPerMeter = 0;



        public DebugView(World world)
            : base(world)
        {
            world.ContactManager.PreSolve += PreSolve;

            //Default flags
            //  AppendFlags(DebugViewFlags.Shape);
            //  AppendFlags(DebugViewFlags.Controllers);
            //   AppendFlags(DebugViewFlags.Joint);
            AppendFlags(DebugViewFlags.Body);
        }


        public void SetWorld(World world)
        {
            //TODO use a better pattern, when world is changed set it once, not every draw
            if (World == world)
                return;

            World.ContactManager.PreSolve -= PreSolve;
            World = world;
            World.ContactManager.PreSolve += PreSolve;
        }

        #region IDisposable Members

        public void Dispose()
        {
            World.ContactManager.PreSolve -= PreSolve;
        }

        #endregion

        private void PreSolve(Contact contact, ref Manifold oldManifold)
        {
            if ((Flags & DebugViewFlags.ContactPoints) == DebugViewFlags.ContactPoints)
            {
                Manifold manifold = contact.Manifold;

                if (manifold.PointCount == 0)
                    return;

                Fixture fixtureA = contact.FixtureA;

                Collision.GetPointStates(out var state1, out var state2, ref oldManifold, ref manifold);

                FixedArray2<Vector2> points;
                Vector2 normal;
                contact.GetWorldManifold(out normal, out points);

                for (int i = 0; i < manifold.PointCount && _pointCount < MaxContactPoints; ++i)
                {
                    if (fixtureA == null)
                        _points[i] = new ContactPoint();

                    ContactPoint cp = _points[_pointCount];
                    cp.Position = points[i];
                    cp.Normal = normal;
                    cp.State = state2[i];
                    _points[_pointCount] = cp;
                    ++_pointCount;
                }
            }
        }






        public static Color GetXNAColor(BodyColor bcolor)
        {
            Color color = default;


            color.R = bcolor.R;
            color.G = bcolor.G;
            color.B = bcolor.B;
            color.A = (byte)bcolor.A;

            return color;
        }


        public void GetXNAColor(Body b, ref Color fill, ref Color edge)
        {

            fill.R = b.Color.R;
            fill.G = b.Color.G;
            fill.B = b.Color.B;
            fill.A = (byte)(b.Color.A * VectorTransparency);

            edge.R = b.EdgeStrokeColor.R;
            edge.G = b.EdgeStrokeColor.G;
            edge.B = b.EdgeStrokeColor.B;
            edge.A = (byte)(b.EdgeStrokeColor.A * VectorTransparency);
        }

        public static void GetXNAColor(out Color fill, out Color edge, Body b)
        {

            //TODO see Monogaem sample that s use Color
            fill = new Color(b.Color.R, b.Color.G, b.Color.B, b.Color.A);
            edge = new Color(b.Color.R, b.Color.G, b.Color.B, b.Color.A);

        }

        Color edge;// reuse this
        Color fill;


        //TODO  hasn't been tested in a while, probably broken   decide if needed
        /// <summary>
        /// Call this to draw shapes and other debug draw data.
        /// </summary>
        private void DrawViewsData(FastList<BaseView> views)
        {


            for (var i = 0; i < views.Length; i++)
            {
                var view = views.Buffer[i];

                IEntityView iview = view as IEntityView;

                Body b = null;
                if (!(iview is BodyView))
                {
                    Debug.WriteLine("unexpected IEntityView " + view.GetType().ToString());
                    continue;

                }

                if ((Flags & DebugViewFlags.DrawInvisible) == 0 && b.IsVisible == false)
                    continue;

                b = (iview as BodyView).Entity;
                if (edge == default(Color))
                {
                    GetXNAColor(out fill, out edge, b);
                }
                else
                {
                    GetXNAColor(b, ref fill, ref edge);
                }

                if (b.GeneralVertices != null && b.GeneralVertices.IsConvex() || b.IsInfoFlagged(BodyInfo.Cloud))
                {
                    DrawPolygon(b, view.GeneralVertices, view.Transform, fill, edge, b.EdgeStrokeThickness);
                }
                else
                {
                    foreach (Fixture f in b.FixtureList)
                    {
                        DrawShape(f, view.Transform, fill, edge);
                    }

                    DrawPolygon(b, view.GeneralVertices, view.Transform, fill, edge, b.EdgeStrokeThickness);
                }
            }

            if ((Flags & DebugViewFlags.DebugPanel) == DebugViewFlags.DebugPanel)
            {
                DrawDebugPanel(World.BodyList);
            }

        }

        /// <summary>
        /// if no fixtures avail or collision data doesnt math the general verts we can tessleate it, cache,  and draw the triangles
        /// </summary>
        static public bool IsTesselateOn = true;

        /// <summary>
        /// Call this to draw shapes and other debug draw data.
        /// </summary>

        private void DrawBodyData(List<Body> bodyList)
        {

            if ((Flags & DebugViewFlags.Body) == DebugViewFlags.Body)
            {

                foreach (Body b in bodyList)
                {



                    if ((Flags & DebugViewFlags.DrawInvisible) == 0 && b.IsVisible == false)
                        continue;


                    if (Graphics.BodySpriteMap.ContainsKey(b) && Flags.HasFlag(DebugViewFlags.TextureMap))  //skip it we are gonna drawa the sprite instead.
                        continue;


                    if (edge == default)
                    {
                        GetXNAColor(out fill, out edge, b);
                    }

                    GetXNAColor(b, ref fill, ref edge);

                    b.GetTransform(out Transform xf);  //TODO used?




                    if (IsTesselateOn)
                    {

                        if (IsFillPass(b)

                        )
                        {
                            try
                            {
                                if
                                    (
                                NeedsTessForFilling(b)
                                 )


                                {
                                    Vertices verts = b.IsSplineFit ? new Vertices(b.SplineViewVertices) : b.GeneralVertices;



                                    //todo earclip is slow but allows degenerate like cloud... for now its under tge 60 fps ..if max is too low there issue
                                    //should be cahce and done only on jitter verts.. same as spline..iew


                                    //TODO should tesselate fucntio
                                    //n , factored out..
                                    //mabye look at earclip and see if  tol should be zero setting it a bit higher doesnt help ..
                                    //also if has fixtures dont do this... migth be breaking cloud...dont draw edges of shapes if filling ..unless shape is defined
                                    //clean up the draw code in general..

                                    //mabye move tshi all somewhere..  static body also shuldnt drawa edge inside its prolly two pass thaat covers it
                                    //anyways we need to mask next and do a shader  



                                    if (b.TesselationDirty)
                                    {

                                        b.TesselatedVerts = b.IsInfoFlagged(BodyInfo.Cloud) ?

                                           FlipcodeDecomposer.ConvexPartition(verts)//  doesnt draw everything but fast and safe for jittered clouds//TDOO make all new clouds with smoke grids
                                                                                    // or distort drawn clouds differently or shadeer flood fill... amek texure and distore it or something
                                                                                    //  SeidelDecomposer.ConvexPartition(verts, .02f)
                                                                                    //    BayazitDecomposer.ConvexPartition(verts)
                                                                                    //    CDTDecomposer.ConvexPartition(verts)  //this is the best for degenerate cloud after jittered... but relly slow 30 fps draw time
                                                                                    //   EarclipDecomposer.ConvexPartition(verts, 1050, 0, 3f)

                                            : CDTDecomposer.ConvexPartition(verts);
                                    }



                                    foreach (var piece in b.TesselatedVerts)
                                    {
                                        DrawSolidPolygon(b, piece, fill);
                                    }
                                    continue;

                                }

                            }

                            catch
                            (
                                Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                continue;

                            }
                        }


                    }





                    if (b is Particle particle)
                    {


                        int circleChords = CircleSegments;


                        //IN ELLIPSE ITS TAKING SIZE MEAN DIAMETER BUT FIXUTER IS BIGGER T HEN BY TWO.. WHATEVER fOR NOW 
                        float radius = particle.ParticleSize;///2;


                        float radiusX = particle.CurrentParticleSizeX;///2;


                        Vector2 scale = new Vector2(radiusX, radius);

                        scale.Normalize();



                        //   if (particle.ScaleFactorX != 0)//NOTE this isnt set even when particle is meant to oscillate for blood anywasy.. 
                        //    {
                        //        scale.X *= particle.ScaleFactorX;//not safe never tested
                        //    }
                        //todo check the fixtuer.. can we darw that?   does it collide? squashed ?


                        //TODO make sure 2 passs dont drw particles twice
                        //todo zoom in too far.. change the tagged particle to tag in viewport space.. drwa in pixels..mabye a dot
                        //add draw pixel or somethign

                        if (particle.SizeDivideByZoom)
                        {

                            if ((radius) * PixelsPerMeter < 2f) //it will draw less tahn one pixel
                            {
                                radius = 2f / PixelsPerMeter;// make it THREE at least   //draw a sprite  instead.. we cant even chagne the chords to 3
                                scale = Vector2.One;
                            }

                        }

                        //TODO clean we are drawing things twice.. darw code too complex checking flags , primitive should darw solid poly not chieck fill flag
                        scale = Vector2.One;
                        if ((Flags & DebugViewFlags.Fill) == 0 && (DrawMask || particle.EdgeStrokeColor.A > 0))
                        {
                            DrawCircle(particle.WorldCenter, radius, edge, scale, particle.EdgeStrokeThickness, circleChords);
                        }
                        else
                        {
                            DrawSolidCircle(particle.WorldCenter, radius, fill, edge, scale, particle.EdgeStrokeThickness, circleChords);
                        }



                    }

                    bool wasFilled = false;
                    if (b.GeneralVertices != null)
                    {
                        wasFilled = DrawPolygon(b, fill, edge, 										b.EdgeStrokeThickness);  // draw general verts and fill
                    }


                    if ( ! wasFilled &&
                        (Flags & DebugViewFlags.Fill) != 0 
                        && (DrawMask || fill.A > 0)

                        && !b.IsInfoFlagged(BodyInfo.Cloud) // if we want to fill but we didnt in last call because it was not convex or dindt have general vier
                        && (b.GeneralVertices == null || !b.GeneralVertices.IsConvex()))
                    {

                        if ((Flags & DebugViewFlags.Shape) == 0 && (b is Particle))  //with particle we just already draw its shape
                            continue;

                        //    if ((Flags & DebugViewFlags.Edges) != 0)  //removed now we hve tesselation.. we already drew the edges ealier in DrawPolygon
                        /////    {
                        //        DrawPolygon(b, fill, edge, b.EdgeStrokeThickness, false);
                        //    }

                        if (b.FixtureList == null)
                            continue;

                        foreach (Fixture f in b.FixtureList)
                        {
                            if (f.IsSensor)
                                continue;

                            //    DrawShape(f, xf, fill, fill);//we cant use edge color, it will draw each triangle with it

                            DrawShape(f, xf, fill, Color.Transparent);//we cant use edge color, it will draw each triangle with it

                        }

                    }







                    if (!(b is Particle) && (Flags & DebugViewFlags.Fill) == 0 && b.GeneralVertices == null
                        && (Flags & DebugViewFlags.Edges) != 0
                        && (DrawMask || b.EdgeStrokeColor.A > 0))

                    {



                        foreach (Fixture f in b.FixtureList)
                        {
                            if (f.IsSensor)
                                continue;

                            DrawShape(f, xf, fill, edge);
                        }

                    }

                    //TODO revisit ..this didnt work
                    if (Flags.HasFlag(DebugViewFlags.BodyMarks))
                    {

                        //TODO break out the one behind at first.. draw it first pass.
                        //factor out the repeat.. thsi is junk old code ported anyways not worth much effort
                        foreach (MarkPoint mark in b.VisibleMarks)
                        {
                            Mat22 rotMat = new Mat22(mark.Angle);//this is the world angle

                            Color markColor = Color.FromNonPremultiplied(mark.Color.R, mark.Color.G, mark.Color.B, mark.Color.A);

                            float circleFlattenFactor = mark.UseType == MarkPointType.Liquid ? 0.6f : 0.8f;    //dust and snow is rounder than blood   


                            if (mark.UseType == MarkPointType.Bruise || mark.UseType == MarkPointType.Burn)
                            {
                                //this definetly for blood would look better ellispe flattened to 90 degree to normal
                                // flattened to X.


                                ///TWOPASSDRAW .. asusming that

                                if (Flags.HasFlag(DebugViewFlags.Fill))// bruisie  front was in back well try both
                                {
                                    circleFlattenFactor += MathUtils.RandomNumber(-0.1f, 0.1f);
                                    //TODO FUTURE  apply a clip region on mark view  to parent body outline.. sometime bruises stick out .. not rotated perfectly.. its ok if clipped..
                                    //TODO  a random shape or trapezoliod or spline would be better ellipse is too perfect..

                                    // Vector2 scaleLoc = new Vector2(1, circleFlattenFactor);  


                                    //  Vector2 circleScaleWCS = MathUtils.Multiply(ref rotMat, scaleLoc);
                                    DrawSolidCircle(mark.WorldPosition, mark.Radius, markColor, Vector2.One);  //jsut maek something visibuel for now

                                    //   DrawSolidCircle(mark.WorldPosition, mark.Radius, markColor, scaleLoc);

                                    //   DrawSolidCircle(mark.WorldPosition, 1, markColor, circleScaleWCS);

                                }

                                // bruise must appear on behind//todo break this out then.. draw first..TODO MG  separtae by type..two pases
                                //  Canvas.SetZIndex(view, Int16.MinValue + 1);
                            }
                            else if (mark.UseType == MarkPointType.Scar)
                            {


                                if (Flags.HasFlag(DebugViewFlags.Fill))// scar must appear in front.
                                {

                                    float scarWidth = MathUtils.RandomNumber(0.4f, 1.2f);  //already  Radius  is related to impulse .. just randomise the shape a bit
                                    Vertices triangle = ShapeFactory.CreateIsoscelesTriangle(mark.Radius * scarWidth, mark.Radius * 1.5f); // deeper scars

                                    for (int i = 0; i < triangle.Count; i++)
                                    {
                                        _tempBodyVertices[i] = MathUtils.Multiply(ref b.Xf, triangle[i]);
                                    }

                                    DrawSolidPolygon(_tempBodyVertices, 3, markColor);
                                }


                            }
                            else
                            {

                                //TODO test w snow , dust particle marks.. they have to be round to work now
                                if (Flags.HasFlag(DebugViewFlags.Edges))                 //these go behind  so on first pass where edges are draw
                                {
#if ASWRITTEN  //this way should work.. try rotate w textue and aspect scale that   rotate the ovals .. the normal shold be crossing the short size
                            
                                    Vector2 scaleLoc = new Vector2(1, circleFlattenFactor);

                                    scaleLoc.Normalize();
                                    Vector2 circleScaleWCS = MathUtils.Multiply(ref rotMat,scaleLoc );

                                  //  circleScaleWCS.Normalize();//should not be needed
                                      DrawSolidCircle(mark.WorldPosition, 20, markColor, circleScaleWCS);
#endif
                                    Vector2 test = Vector2.One;
                                    // test.X = .6f;
                                    DrawSolidCircle(mark.WorldPosition, mark.Radius, markColor, test);

                                    //   DrawSolidCircle(mark.WorldPosition, mark.Radius, markColor, circleScaleWCS);
                                }
                            }
                        }
                    }

                }
            }

            if ((Flags & DebugViewFlags.Shape) == DebugViewFlags.Shape)
            {

                foreach (Body b in bodyList)
                {

                    if (b.FixtureList == null)
                        continue;

                    b.GetTransform(out var xf);
                    foreach (Fixture f in b.FixtureList)

                    {

                        if (b.Enabled == false)
                            DrawShape(f, xf, InactiveShapeColor);
                        else if (b.BodyType == BodyType.Static)
                            DrawShape(f, xf, StaticShapeColor);
                        else if (b.BodyType == BodyType.Kinematic)
                            DrawShape(f, xf, KinematicShapeColor);
                        else if (b.Awake == false)
                            DrawShape(f, xf, SleepingShapeColor);
                        else
                            DrawShape(f, xf, DefaultShapeColor);
                    }
                }
            }

            if ((Flags & DebugViewFlags.ContactPoints) == DebugViewFlags.ContactPoints)
            {
                const float axisScale = 0.3f;

                for (int i = 0; i < _pointCount; ++i)
                {
                    ContactPoint Vector2 = _points[i];

                    if (Vector2.State == PointState.Add)
                        DrawVector2(Vector2.Position, 0.1f, new Color(0.3f, 0.95f, 0.3f));
                    else if (Vector2.State == PointState.Persist)
                        DrawVector2(Vector2.Position, 0.1f, new Color(0.3f, 0.3f, 0.95f));

                    if ((Flags & DebugViewFlags.ContactNormals) == DebugViewFlags.ContactNormals)
                    {
                        Vector2 p1 = Vector2.Position;
                        Vector2 p2 = p1 + axisScale * Vector2.Normal;
                        DrawSegment(p1, p2, new Color(0.4f, 0.9f, 0.4f));
                    }
                }

                _pointCount = 0;
            }

            if ((Flags & DebugViewFlags.PolygonPoints) == DebugViewFlags.PolygonPoints)
            {

                foreach (Body body in bodyList)
                {
                    foreach (Fixture f in body.FixtureList)
                    {
                        if (!(f.Shape is PolygonShape polygon))
                            continue;

                        body.GetTransform(out Transform xf);

                        for (int i = 0; i < polygon.Vertices.Count; i++)
                        {

                            Vector2 tmp = MathUtils.Multiply(ref xf, polygon.Vertices[i]);
                            DrawVector2(tmp, 0.1f, Color.Red);
                        }
                    }
                }
            }

            if ((Flags & DebugViewFlags.Joint) == DebugViewFlags.Joint)
            {
                foreach (Joint j in World.JointList)
                {
                    DrawJoint(j);
                }
            }



            if ((Flags & DebugViewFlags.AABB) == DebugViewFlags.AABB)
            {
                Color color = new Color(0.9f, 0.3f, 0.9f);
                BroadPhase bp = World.ContactManager.BroadPhase;

                foreach (Body body in bodyList)
                {
                    if (body.Enabled == false)
                        continue;

                    foreach (Fixture f in body.FixtureList)
                    {
                        for (int t = 0; t < f.ProxyCount; ++t)
                        {
                            FixtureProxy proxy = f.Proxies[t];

                            bp.GetFatAABB(proxy.ProxyId, out var aabb);

                            DrawAABB(ref aabb, color);
                        }
                    }
                }
            }

            if ((Flags & DebugViewFlags.CenterOfMass) == DebugViewFlags.CenterOfMass)
            {

                foreach (Body b in bodyList)
                {
                    b.GetTransform(out var xf);

                    xf.Position = b.WorldCenter;
                    DrawTransform(ref xf);
                }
            }

            if ((Flags & DebugViewFlags.Controllers) == DebugViewFlags.Controllers)
            {
                for (int i = 0; i < World.Controllers.Count; i++)
                {
                    Controller controller = World.Controllers[i];

                    BuoyancyController buoyancy = controller as BuoyancyController;
                    if (buoyancy != null)
                    {
                        AABB container = buoyancy.Container;
                        DrawAABB(ref container, Color.LightBlue);
                    }
                }
            }

            if ((Flags & DebugViewFlags.DebugPanel) == DebugViewFlags.DebugPanel)
                DrawDebugPanel(bodyList);
        }

        private static bool NeedsTessForFilling(Body b)
        {
            return b.GeneralVertices != null
                                                 &&

                                              (b.FixtureList == null
                                                     ||

                                               (b.FixtureList.Count == 0 || (b.FixtureList.Count == 1 && b.IsInfoFlagged(BodyInfo.Cloud)))




                                            // &&
                                            // (b.GeneralVertices.Count < 2600) 
                                            ||
                                                (b.IsSplineFit && b.IsCollisionSpline == false)

                                                );
        }

        private bool IsFillPass(Body b)
        {
            return ((Flags & DebugViewFlags.Fill) != 0 || (Flags & DebugViewFlags.FillStatic) != 0);
            //&& b.IsStatic == false 
            //  && b.Color.A != 0 
            //     && (
            //     b.IsInfoFlagged(BodyInfo.Cloud)
            //     || (string)b.UserData == "water");
        }



        /// <summary>
        /// goes over emitter refs... draws vecs and loads thumnails... needs tow passes because be can't batch vec and sprites for draw at same time.. takes two passes to draw all..
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="fill"></param>
        /// <param name="bodyList"></param>
        /// <param name="drawOnlyVectorsButCacheTextures">first pass we patch and load up thumnail data and darw vectors if none avail..catch bacth both at once</param>
        private void DrawEmitters(ref Color edge, ref Color fill, List<Body> bodyList, bool drawOnlyVectorsButCacheTextures)
        {




            if (!drawOnlyVectorsButCacheTextures)
            {
                _batch.Begin(SpriteSortMode.Deferred, DefaultTextureBlendState, null, null, RasterizerState.CullNone, _scalingEffect);//draws white
            }

            DebugViewFlags oldFlags = Flags;

            //we need this to see the body content in case we are doing a multipass draw fist with thisone top
            Flags |= DebugViewFlags.Body;
            Flags |= DebugViewFlags.Edges;
            Flags |= DebugViewFlags.Fill;

            try
            {

                foreach (Body body
                    in bodyList)
                {

                    foreach (Emitter em in body.Emitters)
                    {

                        BodyEmitter bem = em as BodyEmitter;

                        if (bem == null)
                            continue;

                        if (!bem.UseEmittedBodyAsView || bem.IsDead || bem.IsVisible == false)//used by automag gun)//used by automag gun
                            continue;

                        if (string.IsNullOrEmpty(bem.SpiritResource))
                            continue;

                        if (SimWorld.LooseFiles)
                        {

                            if (bem.LastEntityLoaded == null)
                            {
                                bem.LastEntityLoaded = SimWorld.GetEmitterResourceAsEntity(bem);
                            }

                        }

                        IEntity childEnt = bem.LastEntityLoaded;


                        if (LoadThumbnails == true)// should prolly make local textures files if we can, its slow w managed  code to load the first level.
                        {

                            if (childEnt?.Thumbnail != null)//the level proxy or emitter ent view..
                            {

                                Texture2D texture = GetDrawTexture(childEnt);



                                if (texture != null)
                                {
                                    if (childEnt is LevelProxy)
                                    {

                                        if (!drawOnlyVectorsButCacheTextures)
                                        {

                                            //TODO juust use the DrawTextureInBody  i think
                                            DrawTextureInRectBody(bem.Parent, texture, bem.WorldPosition, bem.Angle, true); //dont batch calls since we inside a primitive bathc alreadyoo
                                            continue;
                                        }

                                    }
                                    else
                                    {

                                        //TODO sprit Textures.. as a list maybe sequence...for say heart that beatgs or had that grabs..
                                        Body bodyToEmit = bem.LastEntityLoaded as Body;
                                        if (bodyToEmit != null)
                                        {

                                            if (!drawOnlyVectorsButCacheTextures)
                                            {
                                                DrawTextureInBody(bodyToEmit, texture, bem.WorldPosition, bem.Angle, true);
                                                continue;
                                            }
                                        }
                                    }


                                }

                            }
                        }




                        body.GetTransform(out Transform xf);



                        if (childEnt is LevelProxy || bem.SpiritResource.EndsWith(".wyg"))
                        {

                            //if we didnt get a thumnail, either draw stirng or vectors for levels
                            if (bem.SpiritResource != null && bem.SpiritResource.EndsWith(".wyg"))
                            {
#if DRAWLEVELSASVECTOR   //we dont load entire levels, now, level.instance and weird issue and slow
                            DrawXrefContent(ref edge, ref fill, bem, ref xf, childEnt);
#else


                                if (!drawOnlyVectorsButCacheTextures)  //this means we failed to draw the level thumnanill..so dont draw string on vector pass also
                                {
                                    string levelName = System.IO.Path.GetFileNameWithoutExtension(bem.SpiritResource);

                                    //we dont have wcs text apis , just convert to viewport and scale
                                    Vector2 posViewport = Graphics.Instance.Presentation.Camera.Transform.WorldToViewport(bem.WorldPosition);


                                    DrawString((int)posViewport.X, (int)posViewport.Y, levelName);
                                }
#endif
                            }
                        }



                        if (drawOnlyVectorsButCacheTextures)
                        {

                            if (childEnt == null)
                                childEnt = bem.NextEntityToEmit;//todo we dont preload circles or other simple shape particles

                            //draw any body and spirit emitters
                            DrawXrefVectorContent(ref edge, ref fill, bem, ref xf, childEnt);
                        }

                    }
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine("DrawEmiters " + exc);
            }

            finally
            {
                Flags = oldFlags;

                if (!drawOnlyVectorsButCacheTextures)
                {
                    _batch.End();
                }
            }
        }



        private Texture2D GetDrawTexture(IEntity childEnt)
        {
            Texture2D texture = null;

            //this body could be cloned for lockless draw so dont use the clone it will be new every fr
            IEntity originalEnt = childEnt;


            bool isShowingDress2 = false;

            if (childEnt is Body)
            {

                if ((childEnt as Body).cloneOrg != null)
                {
                    originalEnt = (childEnt as Body).cloneOrg;

                }

                isShowingDress2 = (childEnt as Body).IsShowingDress2;
            }

            var map = isShowingDress2 ? Graphics.EntityThumbnailTextureMap2 : Graphics.EntityThumbnailTextureMap;

            if (map.ContainsKey(originalEnt))
            {
                texture = map[originalEnt];
            }
            else
            {
                texture = Graphics.DecodeThumbnailToTexture(_device, originalEnt);
                map.Add(originalEnt, texture);

            }

            return texture;
        }




        private void DrawXrefVectorContent(ref Color edge, ref Color fill, BodyEmitter bem, ref Transform xf, IEntity childEnt)
        {

            if (childEnt == null)
                return;

            DrawChildEntity(ref edge, ref fill, bem, ref xf, childEnt);

            if (childEnt.Entities != null)
            {
                foreach (var x in childEnt.Entities)  //draw all the bodies in level
                {
                    DrawChildEntity(ref edge, ref fill, bem, ref xf, x, true);
                }
            }
        }

        private void DrawChildEntity(ref Color edge, ref Color fill, BodyEmitter bem, ref Transform xf, IEntity child, bool staticOnly = false)
        {

            Body emitBody = child as Body;

            if (emitBody == null) //only draw bodies
                return;

            if (staticOnly && !emitBody.IsStatic)
                return;

            Vector2 wcsEmitted = MathUtils.Multiply(ref xf, bem.LocalPosition);

            emitBody.Position = wcsEmitted;

            emitBody.Rotation = Mathf.Atan2(bem.WorldDirection.Y, bem.WorldDirection.X);// todo this still doesnt refect body rotation, body DrawPolygon should


            GetXNAColor(emitBody, ref fill, ref edge);


            DrawPolygon(emitBody, fill, edge, emitBody.EdgeStrokeThickness, true, bem.ScaleX, bem.ScaleY);


            //we can only use body as refs for now.. not particles or shapes

            // body is very heavy tho... should add some graphic prims...at least circle or square .. or some basic particle textures.  for detailing..


            //TOD may allow nesting.. draw emitted items with emitters in them recursively..
            ///we dont have anything in here.. this would be like for a shotgun shell w mayb e some particles in it... we dont have tehg shape thll spawned tho..
            //   var body = new List<Body>(1);
            //   body.Add(emitBody);

            //   DrawEmitters(ref edge, ref fill, body, true);  //draw details in the refs.. 

        }


        //TODO move all this Debug stuff to another file

        private void DrawPerformanceGraph()
        {

            _graphValues.Add(TimeSpan.FromMilliseconds(World.UpdateTime));

            if (_graphValues.Count > ValuesToGraph + 1)
                _graphValues.RemoveAt(0);

            float x = PerformancePanelBounds.X;
            float deltaX = PerformancePanelBounds.Width / (float)ValuesToGraph;
            float yScale = PerformancePanelBounds.Bottom - (float)PerformancePanelBounds.Top;

            // we must have at least 2 values to start rendering
            if (_graphValues.Count > 2)
            {
                _min = TimeSpan.MaxValue;
                _max = TimeSpan.Zero;
                _avg = TimeSpan.Zero;

                for (int i = 0; i < _graphValues.Count; i++)
                {
                    var val = _graphValues[i];
                    _min = TimeSpan.FromTicks(Math.Min(_min.Ticks, val.Ticks));
                    _max = TimeSpan.FromTicks(Math.Max(_max.Ticks, val.Ticks));
                    _avg += val;
                }
                _avg = TimeSpan.FromTicks(_avg.Ticks / _graphValues.Count);

                if (AdaptiveLimits)
                {
                    MaximumValue = _max;
                    MinimumValue = TimeSpan.Zero;
                }

                // start at last value (newest value added)
                // continue until no values are left
                for (int i = _graphValues.Count - 1; i > 0; i--)
                {
                    float y1 = PerformancePanelBounds.Bottom - (((yScale * _graphValues[i].Ticks) / (MaximumValue - MinimumValue).Ticks));
                    float y2 = PerformancePanelBounds.Bottom - (((yScale * _graphValues[i - 1].Ticks) / (MaximumValue - MinimumValue).Ticks));

                    Vector2 x1 = new Vector2(MathHelper.Clamp(x, PerformancePanelBounds.Left, PerformancePanelBounds.Right), MathHelper.Clamp(y1, PerformancePanelBounds.Top, PerformancePanelBounds.Bottom));
                    Vector2 x2 = new Vector2(MathHelper.Clamp(x + deltaX, PerformancePanelBounds.Left, PerformancePanelBounds.Right), MathHelper.Clamp(y2, PerformancePanelBounds.Top, PerformancePanelBounds.Bottom));

                    DrawSegment(x1, x2, Color.LightGreen);

                    x += deltaX;
                }
            }

            _graphSbMax.Clear(); _graphSbAvg.Clear(); _graphSbMin.Clear();
            DrawString(PerformancePanelBounds.Right + 10, PerformancePanelBounds.Top, _graphSbMax.Append("Max: ").AppendNumber((float)_max.TotalMilliseconds, 3).Append(" ms"));
            DrawString(PerformancePanelBounds.Right + 10, PerformancePanelBounds.Center.Y - 7, _graphSbAvg.Append("Avg: ").AppendNumber((float)_avg.TotalMilliseconds, 3).Append(" ms"));
            DrawString(PerformancePanelBounds.Right + 10, PerformancePanelBounds.Bottom - 15, _graphSbMin.Append("Min: ").AppendNumber((float)_min.TotalMilliseconds, 3).Append(" ms"));

            //Draw background.
            _background[0] = new Vector2(PerformancePanelBounds.X, PerformancePanelBounds.Y);
            _background[1] = new Vector2(PerformancePanelBounds.X, PerformancePanelBounds.Y + PerformancePanelBounds.Height);
            _background[2] = new Vector2(PerformancePanelBounds.X + PerformancePanelBounds.Width, PerformancePanelBounds.Y + PerformancePanelBounds.Height);
            _background[3] = new Vector2(PerformancePanelBounds.X + PerformancePanelBounds.Width, PerformancePanelBounds.Y);

            DrawSolidPolygon(_background, 4, Color.DarkGray);
        }

        private void DrawDebugPanel(List<Body> bodies)
        {
            int fixtureCount = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].FixtureList == null)
                    continue;

                fixtureCount += bodies[i].FixtureList.Count;
            }


            float ticksMs = (float)(1000.0 / (double)Stopwatch.Frequency);

            int x = (int)DebugPanelPosition.X;
            int y = (int)DebugPanelPosition.Y;


            Color old = TextColor;
            TextColor = Color.Red;

            _debugPanelSbObjects.Clear();
            _debugPanelSbObjects.Append("Objects:").AppendLine();
            _debugPanelSbObjects.Append("- Bodies:   ").AppendNumber(World.BodyList.Count).AppendLine();
            _debugPanelSbObjects.Append("- Fixtures: ").AppendNumber(fixtureCount).AppendLine();
            _debugPanelSbObjects.Append("- Contacts: ").AppendNumber(World.ContactCount).AppendLine();

            _debugPanelSbObjects.Append("- Proxies:  ").AppendNumber(World.ProxyCount).AppendLine();
            _debugPanelSbObjects.Append("- Joints:   ").AppendNumber(World.JointList.Count).AppendLine();
            _debugPanelSbObjects.Append("- Cntrlrs:  ").AppendNumber(World.Controllers.Count).AppendLine();
            _debugPanelSbObjects.Append("- Plugins:  ").AppendNumber(World.PluginCount).AppendLine();
            _debugPanelSbObjects.Append("- Entities:  ").AppendNumber(Level.Instance == null ? 0 : Level.Instance.Entities.Count).AppendLine();
            _debugPanelSbObjects.Append("- Spirits:  ").AppendNumber(World.SpiritCount).AppendLine();

            if (PhysicsThread.EnableFPS)
            {
                _debugPanelSbObjects.AppendLine();
                _debugPanelSbObjects.Append("Physics FPS ").AppendLine(World.UpdatePerSecond.ToString());
                _debugPanelSbObjects.Append("Draw FPS    ").AppendLine(World.RenderMaxUpdatePerSecond.ToString());
                _debugPanelSbObjects.Append("RefreshFPS  ").AppendLine(World.RenderDrawPerSecond.ToString());

            }

            DrawString(x, y, _debugPanelSbObjects);



            if (FarseerPhysics.Settings.EnableDiagnostics)
            {
                _debugPanelSbUpdate.Clear();
                _debugPanelSbUpdate.Append("Update time:").AppendLine();
                _debugPanelSbUpdate.Append("- Body:    ").AppendNumber((float)World.SolveUpdateTime * ticksMs, 4).Append(" ms").AppendLine();
                _debugPanelSbUpdate.Append("- Contact: ").AppendNumber((float)World.ContactsUpdateTime * ticksMs, 4).Append(" ms").AppendLine();
                _debugPanelSbUpdate.Append("- CCD:     ").AppendNumber((float)World.ContinuousPhysicsTime * ticksMs, 4).Append(" ms").AppendLine();
                _debugPanelSbUpdate.Append("- Joint:   ").AppendNumber((float)World.Island.JointUpdateTime * ticksMs, 4).Append(" ms").AppendLine();
                _debugPanelSbUpdate.Append("- Cntrlrs: ").AppendNumber((float)World.ControllersUpdateTime * ticksMs, 4).Append(" ms").AppendLine();
                _debugPanelSbUpdate.Append("- Total:   ").AppendNumber((float)World.UpdateTime * ticksMs, 4).Append(" ms").AppendLine();

                _debugPanelSbUpdate.Append("- Plugins: ").AppendNumber((float)World.PluginsUpdateTime * ticksMs, 4).Append(" ms").AppendLine();
                DrawString(x + (int)(120f * fontWidth), y, _debugPanelSbUpdate);

            }
            else
            {
                _debugPanelSbUpdate.Clear();
            }


            TextColor = old;



        }



        public void DrawAABB(ref AABB aabb, Color color)
        {
            Vector2[] verts = new Vector2[4];
            verts[0] = new Vector2(aabb.LowerBound.X, aabb.LowerBound.Y);
            verts[1] = new Vector2(aabb.UpperBound.X, aabb.LowerBound.Y);
            verts[2] = new Vector2(aabb.UpperBound.X, aabb.UpperBound.Y);
            verts[3] = new Vector2(aabb.LowerBound.X, aabb.UpperBound.Y);


            DrawPolygon(null, verts, 4, color);
        }

        private void DrawJoint(Joint joint)
        {
            if (!joint.Enabled)
                return;

            Body b1 = joint.BodyA;
            Body b2 = joint.BodyB;
            b1.GetTransform(out var xf1);

            Vector2 x2 = Vector2.Zero;

            // WIP David
            if (!joint.IsFixedType())
            {
                b2.GetTransform(out var xf2);
                x2 = xf2.Position;
            }

            Vector2 p1 = joint.WorldAnchorA;
            Vector2 p2 = joint.WorldAnchorB;
            Vector2 x1 = xf1.Position;

            Color color = new Color(0.5f, 0.8f, 0.8f);

            switch (joint.JointType)
            {
                case JointType.Distance:
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.Pulley:
                    PulleyJoint pulley = (PulleyJoint)joint;
                    Vector2 s1 = b1.GetWorldPoint(pulley.LocalAnchorA);
                    Vector2 s2 = b2.GetWorldPoint(pulley.LocalAnchorB);
                    DrawSegment(p1, p2, color);
                    DrawSegment(p1, s1, color);
                    DrawSegment(p2, s2, color);
                    break;
                case JointType.FixedMouse:
                    DrawVector2(p1, 0.5f, new Color(0.0f, 1.0f, 0.0f));
                    DrawSegment(p1, p2, new Color(0.8f, 0.8f, 0.8f));
                    break;
                case JointType.Revolute:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    DrawSegment(x2, p2, color);

                    DrawSolidCircle(p2, 0.1f, Color.Red, Vector2.One);
                    DrawSolidCircle(p1, 0.1f, Color.Blue, Vector2.One);
                    break;
                case JointType.FixedAngle:
                    //Should not draw anything.
                    break;
                case JointType.FixedRevolute:
                    DrawSegment(x1, p1, color);
                    DrawSolidCircle(p1, 0.1f, Color.Pink, Vector2.One);
                    break;
                case JointType.FixedLine:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.FixedDistance:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.FixedPrismatic:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.Gear:
                    DrawSegment(x1, x2, color);
                    break;
                default:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    DrawSegment(x2, p2, color);
                    break;
            }
        }




        public bool DrawPolygon(Body body, Color colorFill, Color colorEdge, float edgeThickness = 0f, bool fill = true, float xScale = 1f, float yScale = 1f)
        {
          

            if (body.GeneralVertices == null)
                return false;

            int count = body.IsSplineFit ? body.SplineViewVertices.Count : body.GeneralVertices.Count;

            if (count > _tempBodyVertices.Length)
            {
                _tempBodyVertices = new Vector2[count + 100];
            }

            for (int i = 0; i < count; i++)
            {

                if (xScale != 1 || yScale != 1)
                {
                    Vector2 scaled = body.IsSplineFit ? body.SplineViewVertices[i] : body.GeneralVertices[i] * new Vector2(xScale, yScale);
                    _tempBodyVertices[i] = MathUtils.Multiply(ref body.Xf, scaled);
                }
                else
                {
                    _tempBodyVertices[i] = MathUtils.Multiply(ref body.Xf, body.IsSplineFit ? body.SplineViewVertices[i] : body.GeneralVertices[i]);
                }

            }

            if (
                ((Flags & DebugViewFlags.Fill) != 0 || body.IsStatic && (Flags & DebugViewFlags.FillStatic) != 0)
                 && (DrawMask || colorFill.A > 0)
                 //&& !body.IsInfoFlagged(BodyInfo.Cloud)
                 && body.GeneralVertices != null && body.GeneralVertices.IsConvex()  //fix for spines, cant be filled if not convex..   TODO  if general verts in convex might not g
                 )
            {
                DrawSolidPolygon(body, _tempBodyVertices, count, colorFill, colorEdge, body.EdgeStrokeThickness);
                return true;
            }
            else
            {

                if (body.EdgeStrokeThickness > 0)
                    DrawPolygon(body, _tempBodyVertices, count, colorEdge);


                return false; //means we didnt fill it

            }
        }




        public void DrawPolygon(Body body, Vertices bodyspaceVerts, Transform xf, Color colorFill, Color colorEdge, float edgeThickness = 0f)
        {

            for (int i = 0; i < bodyspaceVerts.Count; i++)
            {
                _tempBodyVertices[i] = MathUtils.Multiply(ref xf, bodyspaceVerts[i]);
            }

            if (
               ((Flags & DebugViewFlags.Fill) != 0 || body.IsStatic && (Flags & DebugViewFlags.FillStatic) != 0)
                && (DrawMask || colorFill.A > 0)

                && bodyspaceVerts != null && bodyspaceVerts.IsConvex()
                )
            {
                DrawSolidPolygon(body, _tempBodyVertices, bodyspaceVerts.Count(), colorFill, colorEdge, edgeThickness);
            }
            else
            {

                DrawPolygon(body, _tempBodyVertices, bodyspaceVerts.Count, edgeThickness > 0f ? colorEdge : colorFill);
            }
        }


        public void DrawSolidPolygon(Body b, Vertices verts, Color colorFill)
        {

            DrawPolygon(b, verts, b.Xf, colorFill, Color.Transparent, 0);

        }


        public void DrawShape(Fixture fixture, Transform xf, Color color)
        {
            DrawShape(fixture, xf, color, color);
        }

        public void DrawShape(Fixture fixture, Transform xf, Color colorFill, Color colorEdge)
        {


            if (fixture == null || fixture.Shape == null)
                return;

            switch (fixture.Shape.ShapeType)
            {


                case ShapeType.Circle:
                    {
                        CircleShape circle = (CircleShape)fixture.Shape;

                        Vector2 center = MathUtils.Multiply(circle.Position, ref xf);
                        float radius = circle.Radius;
                        DrawSolidCircle(center, radius, colorFill, colorEdge);

                    }
                    break;

                case ShapeType.Polygon:
                    {
                        PolygonShape poly = (FarseerPhysics.Collision.Shapes.PolygonShape)fixture.Shape;
                        int vertexCount = poly.Vertices.Count;
                        Debug.Assert(vertexCount <= Settings.MaxPolygonVertices);

                        for (int i = 0; i < vertexCount; ++i)
                        {

                            _tempVertices[i] = MathUtils.Multiply(ref xf, poly.Vertices[i]);
                        }

                        DrawSolidPolygon(null, _tempVertices, vertexCount, colorFill, colorEdge);
                    }
                    break;

#if TODO
                case ShapeType.Edge:
                    {

                        EdgeShape edge = (EdgeShape)fixture.Shape;
                        Vector2 v1 = Matrix.Multiply(ref xf, edge.Vertex1 );
                        Vector2 v2 = Matrix.Multiply(ref xf, edge.Vertex2);

                    ///   Vector2 v1 = Matrix.Multiply(edge.Vertex1, ref xf);
                   //     Vector2 v2 = Matrix.Multiply(edge.Vertex2, ref xf);
                        DrawSegment(v1, v2, color);
                    }

                    break;
#endif
#if TODO
                case ShapeType.Chain:  //todo merge update from aether / velcro physics
                    {
                        ChainShape chain = (ChainShape)fixture.Shape;

                        for (int i = 0; i < chain.Vertices.Count - 1; ++i)
                        {
                            Vector2 v1 = MathUtils.Multiply(chain.Vertices[i], ref xf);
                            Vector2 v2 = MathUtils.Multiply(chain.Vertices[i + 1], ref xf);
                            DrawSegment(v1, v2, color);
                        }
                    }
                    break; 
                
#endif
            }

        }


        public override void DrawPolygon(Body body, Vector2[] vertices, int count, float red, float green, float blue, bool closed = true)
        {

            DrawPolygon(body, vertices, count, new Color(red, green, blue), closed);
        }


        /// <summary>
        /// Get the nearest of all PoweredJoints on this body
        /// </summary>
        /// <param name="body">The body containing the joints</param>
        /// <param name="posWorld"> Position in World </param>
        /// <returns></returns>
        PoweredJoint GetNearestPwrJoint(Body body, Vector2 posWorld)
        {
            JointEdge je = body.JointList;


            if (je == null)
                return null;

            float minDist = float.MaxValue;

            PoweredJoint nearestJoint = null;

            while (je != null)
            {
                JointEdge je0 = je;
                je = je.Next;

                Joint joint = je0.Joint;

                if (joint is PoweredJoint)
                {
                    //do all this in case other code tries to ower up the joint.. is still in spirit joints.
                    if (Vector2.Distance(joint.WorldAnchorA, posWorld) < minDist)
                    {
                        nearestJoint = joint as PoweredJoint;
                    }
                }
            }

            return nearestJoint;
        }


        Body GetOtherBody(Body ours, PoweredJoint pj)
        {
            return (pj.BodyA == ours) ? pj.BodyB : pj.BodyA;

        }


#if HIDDENLINESTEST
        RayInfo rayInfo = new RayInfo();

        Tuple< Vector2 , Vector2> GetNextVisibleSegment(Body body, Vector2[] vertices,  int i, int iNext , out bool skip)
        {
            Tuple<Vector2, Vector2> ret = null;

            skip = false;

            if (body == null)
                return null;

            PoweredJoint pj1 = GetNearestPwrJoint(body, vertices[i]);

            PoweredJoint pj2 = GetNearestPwrJoint(body, vertices[iNext]);

      

          //  Debug.Assert(pj == pj2);

            if (pj1 == null || pj2==null)
                return null;

            //    bool iInside = Vector2.Distance(pj.WorldAnchorA, vertices[i]) < pj.LimbRadius;



            //    SimWorld.Instance.HitTestFixture( )
            if (pj1.BodyB == null || pj2.BodyB==null)
                return null;


            Body b1 = GetOtherBody(body, pj1);
            Body b2 = GetOtherBody(body, pj2);

            if (b1 == null || b1.FixtureList == null || b2== null || b2.FixtureList == null)
                return null;

            //is first point inside connected body?    //NOTE case with segment enter and exit shape snot supported, must add enoug pts so at least on fits inside at any bend angle within joint limits
            bool iInside =// SimWorld.Instance.HitTestFixture pj2.BodyB.FixtureList[1].Shape.
                              default(Fixture) != b1.FixtureList.Find(x => true == x.TestPoint(ref vertices[i]));


            bool iNextInside =// SimWorld.Instance.HitTestFixture pj2.BodyB.FixtureList[1].Shape.
                      default(Fixture) != b2.FixtureList.Find(x => true == x.TestPoint(ref vertices[iNext]));




            // bool iInside = Vector2.Distance(pj1.WorldAnchorA, vertices[i]) < pj1.LimbRadius;
            //   bool iNextInside = Vector2.Distance(pj2.WorldAnchorA, vertices[iNext]) < pj2.LimbRadius;




            if (iInside && iNextInside)
            {
                skip = true;
                return null; // don't draw this segment at all, we determine its  inside other body as marked by the LimbRadius metadata, the circle is the intersection zone between the limbs
            }

            //new
            // if (iInside && !iNextInside)
            //       return null; // don't draw it , to aviod drawing duplicate segments as we got aroudn clockwise each only draw segments that intersect as they approach joined body not starting inside leaving it



            //  if (!iInside && iNextInside)
            if (iInside ^ iNextInside)
            {

                 int outsideIdx = iInside ? iNext : i;
                 int iInsideIdx = iInside ? i : iNext;

                rayInfo.Start = vertices[outsideIdx];
                rayInfo.End = vertices[iInsideIdx];


                rayInfo.Start = vertices[iInsideIdx];
                rayInfo.End = vertices[outsideIdx];

                if (rayInfo.IgnoredBodies == null)
                {
                    rayInfo.IgnoredBodies = new FarseerPhysics.Common.HashSet<Body>();
                }
                 
                rayInfo.IgnoredBodies.Clear();
                rayInfo.IgnoredBodies.Add(body);

              //  rayInfo=                SimWorld.Instance.Sensor.AddRay(vertices[outsideIdx], vertices[iInsideIdx], "hideline" + iInside.ToString(), body);
                SimWorld.Instance.Sensor.Raycast(rayInfo, true, true);  //why doesnt this work?  the laser right on the vertex works fine
                 

           //     RayCastInput input = new RayCastInput();
            //     input.Point1 = vertices[outsideIdx];
            //    input.Point2 = vertices[iInsideIdx];
            //    RayCastOutput output = new RayCastOutput();
          //      b2.FixtureList[0].RayCast(out output, ref input,0);

         //       Debug.WriteLine(output.Fraction);

                //Vector2 intersectionPt; // in WCS
         
                //     Debug.Assert(rayInfo.IsIntersect); // if exactly one is inside it must intersect
                if (!rayInfo.IsIntersect)
                {
                    Debug.WriteLine("exactly one vert was inside but ray cast is not intersecting ");
                    return null;
                }else
                {
                    Debug.WriteLine("Intersected " + rayInfo.Intersection);
                }

                 ret = new Tuple<Vector2, Vector2>(iInside ? rayInfo.Intersection : vertices[outsideIdx], iInside ? vertices[outsideIdx] : rayInfo.Intersection);

                //   ret = new Tuple<Vector2, Vector2>( vertices[i], rayInfo.Intersection);

                return ret;
            }

   
            return ret;

        }
#endif


        public void DrawPolygon(Body body, Vector2[] vertices, int count, Color color, bool closed = true)
        {




            //todo put the polygon in the Displalist fistlist array .

            if (!_primitiveBatch.IsReady())
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");

            if (!Flags.HasFlag(DebugViewFlags.Edges)) //todo make primitives not care about state or high level stuff,
                return;

            //TODO OPTIMIZE      PrimitiveType.LineStrip use prolly faster
            for (int i = 0; i < count - 1; i++)
            {

                //this gives back only if segment is going from outside to inside, since we wind counterclockwise around and 
                // want to avoid duplicate edges;

#if HIDDENLINESTEST

                    //this might not be the only version, see cleanpup3 and 4
                    //was working better last i test. now RayIntersect fails

         
                    bool skip;//skip w
                    Tuple<Vector2, Vector2> segment = GetNextVisibleSegment(body, vertices, i, i + 1, out skip );

                    if (skip )//skip this segment, both verts are inside joined body, its hidden
                        continue;

                    if (segment != null)
                    {
                        _primitiveBatch.AddVertex(segment.Item1, color, PrimitiveType.LineList);
                        _primitiveBatch.AddVertex(segment.Item2, color, PrimitiveType.LineList);// this is the intersection point, just draw to this
                     }
                     else
#endif
                {
                    _primitiveBatch.AddVertex(vertices[i], color, PrimitiveType.LineList);
                    _primitiveBatch.AddVertex(vertices[i + 1], color, PrimitiveType.LineList);
                }
            }

            if (closed)
            {

#if HIDDENLINESTEST
                   bool skip;
                    Tuple<Vector2, Vector2> segment = GetNextVisibleSegment(body, vertices, count -1, 0, out skip);

                    if (skip)
                        return;

                    //todo refactor above code to method taking 2 indeces, consolidate this with above
                    if (segment != null)    
                    {
                        _primitiveBatch.AddVertex(segment.Item1, color, PrimitiveType.LineList);
                        _primitiveBatch.AddVertex(segment.Item2, color, PrimitiveType.LineList);
                    }else
#endif
                {
                    _primitiveBatch.AddVertex(vertices[count - 1], color, PrimitiveType.LineList);
                    _primitiveBatch.AddVertex(vertices[0], color, PrimitiveType.LineList);
                }
            }
        }


        public void DrawPolygon(Vertices vertices, Color color, bool closed = false)
        {
            for (int i = 0; i < vertices.Count() - 1; i++)
            {
                _primitiveBatch.AddVertex(vertices[i], color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(vertices[i + 1], color, PrimitiveType.LineList);

            }

            if (closed)
            {
                _primitiveBatch.AddVertex(vertices[vertices.Count() - 1], color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(vertices[0], color, PrimitiveType.LineList);

            }
        }

        public void DrawPolygon(Vector2[] vertices, Color color, bool closed = false)
        {
            for (int i = 0; i < vertices.Count() - 1; i++)
            {
                _primitiveBatch.AddVertex(vertices[i], color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(vertices[i + 1], color, PrimitiveType.LineList);

            }

            if (closed)
            {
                _primitiveBatch.AddVertex(vertices[vertices.Count() - 1], color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(vertices[0], color, PrimitiveType.LineList);

            }
        }

        public void DrawLineSegment(LineSegment seg, Color color)
        {
            _primitiveBatch.AddVertex(seg.StartPt, color, PrimitiveType.LineList);
            _primitiveBatch.AddVertex(seg.EndPt, color, PrimitiveType.LineList);
        }


        public override void DrawSolidPolygon(Vector2[] vertices, int count, float red, float green, float blue)
        {
            DrawSolidPolygon(vertices, count, new Color(red, green, blue));
        }

        public void DrawSolidPolygon(Vector2[] vertices, int count, Color color)
        {
            DrawSolidPolygon(null, vertices, count, color * 0.5f, color);
        }


        public void DrawSolidPolygon(Body body, Vector2[] vertices, int count, Color colorFill, Color colorEdge, float edgeThickness = 0f)
        {
            if (!_primitiveBatch.IsReady())
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");

            if (count == 2)
            {
                Debug.Write("polygon has only 2 verts");
                //  DrawPolygon(null, vertices, count, colorEdge, false);
                return;
            }

            for (int i = 1; i < count - 1; i++)
            {
                _primitiveBatch.AddVertex(vertices[0], colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(vertices[i], colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(vertices[i + 1], colorFill, PrimitiveType.TriangleList);
            }

            if (Flags.HasFlag(DebugViewFlags.Edges) && edgeThickness > 0)
            {

                if (!(DrawMask || colorEdge.A > 0) && edgeThickness > 0)
                {
                    DrawPolygon(body, vertices, count, colorEdge);  //should be a line list
                }
            }
        }

        public bool DrawMask = false;

        public override void DrawCircle(Vector2 center, float radius, float red, float green, float blue, Vector2 scale, float edgeThickness)
        {
            DrawCircle(center, radius, red, green, blue, scale, edgeThickness);
        }

        public void FillSolidPolygon(Vector2[] vertices, int count, Color colorFill)
        {
            if (!_primitiveBatch.IsReady())
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");


            for (int i = 1; i < count - 1; i++)
            {
                _primitiveBatch.AddVertex(vertices[0], colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(vertices[i], colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(vertices[i + 1], colorFill, PrimitiveType.TriangleList);
            }
        }

        //NOTE i dont think scale works  TODO MG_GRAPHICS.. either ifx or back out..

        //do we need vector ellipse?

        //must maek a test and test the complex multiply to fix
        //for particles best to do a circle textures anyways..
        public void DrawCircle(Vector2 center, float radius, Color color, Vector2 scale, float edgeThickness, int circleSegments = CircleSegments)
        {

            if (edgeThickness == 0)
                return;

            if (!_primitiveBatch.IsReady())
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");

            Vector2 v2 = new Vector2(radius, 0) * scale; ;
            var center_vS = center + v2;

            for (int i = 0; i < circleSegments - 1; i++)
            {
                Vector2 v1 = v2;
                v2 = scale * Complex.Multiply(ref v1, ref circleSegmentRotation);

                _primitiveBatch.AddVertex(center + v1, color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(center + v2, color, PrimitiveType.LineList);
            }

            // Close Circle
            _primitiveBatch.AddVertex(center + v2, color, PrimitiveType.LineList);
            _primitiveBatch.AddVertex(center_vS, color, PrimitiveType.LineList);
        }

        public override void DrawSolidCircle(Vector2 center, float radius, float red, float green, float blue, Vector2 scale, float edgeThickness = 0)
        {
            DrawSolidCircle(center, radius, new Color(red, green, blue), scale, edgeThickness);
        }


        public void DrawSolidCircle(Vector2 center, float radius, Color color, float edgeThickness = 0)
        {
            DrawSolidCircle(center, radius, color, Vector2.One, edgeThickness);
        }

        public void DrawSolidCircle(Vector2 center, float radius, Color color, Vector2 scale, float edge = 0)
        {
            DrawSolidCircle(center, radius, color, Color.Transparent, scale, edge);
        }

        public void DrawSolidCircle(Vector2 center, float radius, Color colorFill, Color colorEdge)
        {
            DrawSolidCircle(center, radius, colorFill, colorEdge);
        }

        //NOTE i dont think scale works  TODO MG_GRAPHICS.. either ifx or back out..

        //do we need vector ellipse?

        //must maek a test and test the complex multiply to fix
        //for particles best to do a circle textures anyways..

        public void DrawSolidCircle(Vector2 center, float radius, Color colorFill, Color colorEdge, Vector2 scale, float edgethickness, int circleSegments = CircleSegments)
        {

            //TODO record some kind of batch here, see nez , mg extentions.
            //or record the cirlce or spline as hight level in dl , at draw time tesselate..
            //issure is we can sdo this in parallel or simd fast, write to a big flat fixed array
            ////one array for the polygons, bodys, 
            /////on for particels.. see the queue and the fast partcle lists in Nez 

            if (!_primitiveBatch.IsReady())
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");

            Vector2 v2 = new Vector2(radius, 0) * scale;
            var center_vS = center + v2;


            Complex segRotation = circleSegmentRotation;
            if (circleSegments != CircleSegments)
            {
                segRotation = Complex.FromAngle((float)(Math.PI * 2.0 / CircleSegments));
            }



            for (int i = 0; i < circleSegments - 1; i++)
            {
                Vector2 v1 = v2;
                v2 = scale * Complex.Multiply(ref v1, ref segRotation);

                // Draw Circle

                if (DrawMask || colorEdge.A > 0)
                {
                    _primitiveBatch.AddVertex(center + v1, colorEdge, PrimitiveType.LineList);
                    _primitiveBatch.AddVertex(center + v2, colorEdge, PrimitiveType.LineList);
                }


                // Draw Solid Circle
                if (i > 0)
                {
                    _primitiveBatch.AddVertex(center_vS, colorFill, PrimitiveType.TriangleList);
                    _primitiveBatch.AddVertex(center + v1, colorFill, PrimitiveType.TriangleList);
                    _primitiveBatch.AddVertex(center + v2, colorFill, PrimitiveType.TriangleList);
                }

            }


            if (DrawMask || colorEdge.A > 0)
            {
                // Close Circle
                _primitiveBatch.AddVertex(center + v2, colorEdge, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(center_vS, colorEdge, PrimitiveType.LineList);
            }



        }

        public override void DrawSegment(Vector2 start, Vector2 end, float red, float green, float blue)
        {
            DrawSegment(start, end, new Color(red, green, blue));
        }

        public void DrawSegment(Vector2 start, Vector2 end, Color color)
        {

            Color colorView = color;
            colorView.A = (byte)(colorView.A * VectorTransparency);

            if (!_primitiveBatch.IsReady())
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");

            _primitiveBatch.AddVertex(start, colorView, PrimitiveType.LineList);
            _primitiveBatch.AddVertex(end, colorView, PrimitiveType.LineList);
        }


        public override void DrawTransform(ref Transform transform)
        {
#if TODO
            //TODO merge teh mapth with complex thing in aether..


            const float axisScale = 0.4f;
            Vector2 p1 = transform.Position;

            var xAxis = transform.q.ToVector2();
            Vector2 p2 = p1 + axisScale * xAxis;
            DrawSegment(p1, p2, Color.Red);
            
            var yAxis = new Vector2(-transform.q.Imaginary, transform.q.Real);
            p2 = p1 + axisScale * yAxis;
            DrawSegment(p1, p2, Color.Green);

#endif
        }


        public void DrawVector2(Vector2 p, float size, Color color)
        {
            Vector2[] verts = new Vector2[4];
            float hs = size / 2.0f;
            verts[0] = p + new Vector2(-hs, -hs);
            verts[1] = p + new Vector2(hs, -hs);
            verts[2] = p + new Vector2(hs, hs);
            verts[3] = p + new Vector2(-hs, hs);

            DrawSolidPolygon(verts, 4, color);
        }

        public void DrawString(int x, int y, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            DrawString(new Vector2(x, y), text);
        }

        //TODO measure the text , take a ref current text pos or something, see Nez

        public void DrawString(int x, int y, string text, float scale = 1f, Color color = default)
        {

            if (string.IsNullOrEmpty(text))
                return;

            if (color == default(Color))
            {
                color = TextColor;
            }

            _stringData.Add(new StringData(new Vector2(x, y), text, color, scale));
        }

        public void DrawString(Vector2 position, string text)
        {

            if (string.IsNullOrEmpty(text))
                return;

            _stringData.Add(new StringData(position, text, TextColor));
        }


        public void DrawString(Microsoft.Xna.Framework.Vector2 position, string text)
        {

            if (string.IsNullOrEmpty(text))
                return;

            _stringData.Add(new StringData(position, text, TextColor));
        }



        public void DrawMsgString(Microsoft.Xna.Framework.Vector2 position, string text, Color color = default(Color))
        {

            if (color == default(Color))
            {
                color = TextColor;
            }

            _msgStringData.Add(new StringData(position, text, color));
            currentMsgY += (int)(position.Y + 20 * TextScale); //TODO better font height est
        }


        int currentMsgY = 0;
        int currentMsgX = 10;
        public void DrawMsgString(Vector2 position, string text, Color color = default(Color))
        {
            DrawMsgString(position.ToVector2(), text, color);
        }

        public void ClearMsgStrings()
        {
            this._msgStringData.Clear();
            currentMsgY = (int)(55 * TextScale);/// start below titles
            currentMsgX = 10;  //left align
        }

        public void DrawMsgString(string text, Color color = default(Color))
        {
            DrawMsgString(new Vector2(currentMsgX, currentMsgY), text, color);
        }

        public void DrawString(int x, int y, StringBuilder text)
        {
            DrawString(new Vector2(x, y), text);
        }

        public void DrawString(Vector2 position, StringBuilder text)
        {
            _stringData.Add(new StringData(position, text, TextColor));
        }

#if TODO
        public void DrawArrow(Vector2 start, Vector2 end, float length, float width, bool drawStartIndicator, Color color)
        {

        th
            // Draw connection segment between start- and end-Vector2
            DrawSegment(start, end, color);

            // Precalculate halfwidth
            float halfWidth = width / 2;

            // Create directional reference
            Vector2 rotation = (start - end);
            rotation.Normalize();

            // Calculate angle of directional vector
            float angle = (float)Math.Atan2(rotation.X, -rotation.Y);
            // Create matrix for rotation
            Matrix rotMatrix = Matrix.CreateRotationZ(angle);
            // Create translation matrix for end-Vector2
            Matrix endMatrix = Matrix.CreateTranslation(end.X, end.Y, 0);

            // Setup arrow end shape
            Vector2[] verts = new Vector2[3];
            verts[0] = new Vector2(0, 0);
            verts[1] = new Vector2(-halfWidth, -length);
            verts[2] = new Vector2(halfWidth, -length);

            // Rotate end shape
            Vector2.Transform(verts, ref rotMatrix, verts);
            // Translate end shape
            Vector2.Transform(verts, ref endMatrix, verts);

            // Draw arrow end shape
            DrawSolidPolygon(verts, 3, color, false);

            if (drawStartIndicator)
            {
                // Create translation matrix for start
                Matrix startMatrix = Matrix.CreateTranslation(start.X, start.Y, 0);
                // Setup arrow start shape
                Vector2[] baseVerts = new Vector2[4];
                baseVerts[0] = new Vector2(-halfWidth, length / 4);
                baseVerts[1] = new Vector2(halfWidth, length / 4);
                baseVerts[2] = new Vector2(halfWidth, 0);
                baseVerts[3] = new Vector2(-halfWidth, 0);

                // Rotate start shape
                Vector2.Transform(baseVerts, ref rotMatrix, baseVerts);
                // Translate start shape
                Vector2.Transform(baseVerts, ref startMatrix, baseVerts);
                // Draw start shape
                DrawSolidPolygon(baseVerts, 4, color, false);
            }
        }

#endif

        public void BeginCustomDraw(Matrix projection, Matrix view,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            BeginCustomDraw(ref projection, ref view, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }

        public void BeginCustomDraw(Matrix projection, Matrix view, Matrix world,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            BeginCustomDraw(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }

        public void BeginCustomDraw(ref Matrix projection, ref Matrix view,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            Matrix world = Matrix.Identity;
            _primitiveBatch.Begin(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }

        public void BeginCustomDraw(ref Matrix projection, ref Matrix view, ref Matrix world,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            _primitiveBatch.Begin(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }

        public void EndCustomDraw()
        {
            _primitiveBatch.End();
        }

        public void RenderDebugData(Matrix projection, Matrix view, List<Body> bodyList = null,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            RenderDebugData(ref projection, ref view, bodyList, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }


        public void RenderDebugData(Matrix projection, Matrix view, Body body,
                                  BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            var bodies = new List<Body>(1);
            bodies.Add(body);
            RenderDebugData(ref projection, ref view, bodies, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }


        public void RenderDebugData(Matrix projection, Matrix view, Matrix world, List<Body> bodyList = null,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            RenderDebugData(ref projection, ref view, ref world, bodyList, blendState, samplerState, depthStencilState, rasterizerState, alpha);
        }

        public void RenderDebugData(ref Matrix projection, ref Matrix view, List<Body> bodyList = null,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            if (!Enabled)
                return;

            Matrix world = Matrix.Identity;

            try
            {
                RenderDebugData(ref projection, ref view, ref world, bodyList, blendState, samplerState, depthStencilState, rasterizerState, alpha);
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Render Debug Data exc" + exc);
            }
        }

        public void RenderDebugData(ref Matrix projection, ref Matrix view, ref Matrix world, List<Body> bodyList = null,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            if (!Enabled)
                return;

            //Nothing is enabled - don't draw the debug view.
            if (Flags == 0 || _primitiveBatch == null)
                return;

            try
            {

                if (blendState == null)
                    blendState = DefaultBlendState;

                if (Flags.HasFlag(DebugViewFlags.TextureMap))//some of the bodies have mapped textures
                {
                    //    if (bodyList == null)//TODO do we call this two often..make sepatate call.
                    {
                        DrawTextureMap(projection, view, world);
                    }
                }

                if (Flags.HasFlag(DebugViewFlags.Body))
                {

                    ///isssie about polygon fill not workingon zoomed in attempt might be usedful later
                    //  RasterizerState rast = new RasterizerState();
                    //  rast.DepthClipEnable = false;  //didhtn help it seems.. se the depth to 100 i projection matrix instead of 1 did work
                    //  rast.FillMode = FillMode.Solid;

                    //  _primitiveBatch.Begin(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rast, alpha);



        

                    _primitiveBatch.Begin(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rasterizerState, alpha);


                    if (bodyList == null)
                    {
                        bodyList = World.BodyList;
                    }

                    DrawBodyData(bodyList);
                    _primitiveBatch.End();
                }


                if (Flags.HasFlag(DebugViewFlags.EntityEmitters))
                {


                    _primitiveBatch.Begin(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rasterizerState, alpha);


                    DrawEmitters(ref edge, ref fill, bodyList == null ? World.BodyList : bodyList, true);// emitter draw will batch thumbnails first then draw vecs


                    _primitiveBatch.End();

                    DrawEmitters(ref edge, ref fill, bodyList == null ? World.BodyList : bodyList, false);// emitter draw will batch thumbnails first then draw vecs




                }


                if ((Flags & DebugViewFlags.PerformanceGraph) == DebugViewFlags.PerformanceGraph)
                {
                    _primitiveBatch.Begin(ref _localProjection, ref _localView, ref _localWorld, blendState, samplerState, depthStencilState, rasterizerState, alpha);
                    DrawPerformanceGraph();
                    _primitiveBatch.End();
                }



                if (Flags.HasFlag(DebugViewFlags.MsgStrings))
                {
                    Color drawColor = new Color();

                    if (_font != null)
                    {

                        if (blendState == null)
                            blendState = DefaultBlendState;

                        _batch.Begin(SpriteSortMode.BackToFront, blendState);

                        //   _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                        // draw any strings we have
                        for (int i = 0; i < _stringData.Count; i++)
                        {
                            drawColor = _stringData[i].Color;
                            drawColor.A = (byte)(255 * TextTransparency);
                            _batch.DrawString(_font, _stringData[i].Text, ToXNAVec(_stringData[i].Position), drawColor, 0, Microsoft.Xna.Framework.Vector2.Zero, _stringData[i].Scale * TextScale, SpriteEffects.None, 0);
                        }

                        for (int i = 0; i < _msgStringData.Count; i++)
                        {

                            drawColor = _msgStringData[i].Color;
                            drawColor.A = (byte)(255 * TextTransparency);

                            _batch.DrawString(_font, _msgStringData[i].Text, ToXNAVec(_msgStringData[i].Position), _msgStringData[i].Color, 0, Microsoft.Xna.Framework.Vector2.Zero, _msgStringData[i].Scale * TextScale, SpriteEffects.None, 0);
                        }

                        _batch.End();

                    }
                    _stringData.Clear();
                }
            }

            catch (Exception exc)
            {
                Debug.Write("RenderDebugData " + exc.ToString());

                if (_primitiveBatch.IsReady())  // this should help being robust to exceptions thrown mid draw
                    _primitiveBatch.End();
            }


        }



        public void RenderViews(FastList<BaseView> views, ref Matrix projection, ref Matrix view,
                            BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            if (!Enabled)
                return;

            //Nothing is enabled - don't draw the debug view.
            if (Flags == 0 || _primitiveBatch == null)
                return;


            Matrix world = Matrix.Identity;

            try
            {
                DrawTextureMap(projection, view, world);

                _primitiveBatch.Begin(ref projection, ref view, ref world, blendState, samplerState, depthStencilState, rasterizerState, alpha);
                DrawViewsData(views);
                _primitiveBatch.End();


                if ((Flags & DebugViewFlags.PerformanceGraph) == DebugViewFlags.PerformanceGraph)
                {
                    _primitiveBatch.Begin(ref _localProjection, ref _localView, ref _localWorld, blendState, samplerState, depthStencilState, rasterizerState, alpha);
                    DrawPerformanceGraph();
                    _primitiveBatch.End();
                }

                // begin the sprite batch effect

                //TODO is deferred the right mode for our purpose, and we prolly dont need  blend
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);


                // draw any strings we have
                for (int i = 0; i < _stringData.Count; i++)
                {
                    if (_stringData[i].Text != null)
                        _batch.DrawString(_font, _stringData[i].Text, ToXNAVec(_stringData[i].Position), _stringData[i].Color);
                    else
                        _batch.DrawString(_font, _stringData[i].stringBuilderText.ToString(), ToXNAVec(_stringData[i].Position), _stringData[i].Color);
                }


                // end the sprite batch effect
                _batch.End();

                _stringData.Clear();

            }

            catch (Exception exc)
            {
                Debug.Write("RenderDebugData " + exc.ToString());
            }


        }
        public void RenderThumbnailImages(Matrix Projection, Matrix View)
        {
            RenderThumbnailImages(ref Projection, ref View, World.BodyList);
        }




        public void RenderThumbnailImage(Matrix projection, Matrix view, Body b,
              BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {

            var bodies = new List<Body>();
            bodies.Add(b);

            RenderThumbnailImages(ref projection, ref view, bodies);

            return;


        }


        public void RenderThumbnailImages(ref Matrix projection, ref Matrix view, List<Body> bodyList = null,
                                  BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {

            _scalingEffect.Projection = projection;
            _scalingEffect.View = view;
            _scalingEffect.World = Matrix.Identity;
            _scalingEffect.TextureEnabled = true;
            //AnisotropicClamp

            //    samplerState = SamplerState.LinearClamp; ansio

            _batch.Begin(SpriteSortMode.Deferred, DefaultTextureBlendState, samplerState, null, RasterizerState.CullNone, _scalingEffect);//draws white

            foreach (Body body in bodyList)
            {

                if (!body.IsVisible)
                    continue;

                if ((body as IEntity).Thumbnail != null)//the level proxy
                {

                    Texture2D texture = GetDrawTexture(body);

                    if (texture != null)

                    {
                        //TODO erase resave all

                        if (body.OffsetToBodyOrigin == Vector2.Zero)  //means legacy thum saved rel to cm which can change on cut
                        {

                            Debug.WriteLine("WARNING RESAVE DRESS", "DRESS");
                        }

                        var offsetToOrgBC = body.IsShowingDress2 ? body.OffsetToBodyOrigin2 : body.OffsetToBodyOrigin;



                      //  var dressScale = body.IsShowingDress2 ? body.DressScale2 : body.DressScale;
                         offsetToOrgBC  *= body.DressScale;//this is a fix for legacy files..it only uses dresscale1 anyways..we need this for regrow and scale at runtime to work. to really fix means go over and look for wrong scales in tool by using dressscale2 if isshowingdress2
             

                        var offsetToOrgWCS = body.GetWorldVector(offsetToOrgBC);

                        DrawTextureInBody(body, texture, body.Position + offsetToOrgWCS,
                                body.Xf.Angle, true);


                    }

                }

            }
            _batch.End();
        }

        private void DrawTextureMap(Matrix projection, Matrix view, Matrix world)
        {
            _scalingEffect.Projection = projection;
            _scalingEffect.View = view;
            _scalingEffect.World = world;
            _scalingEffect.TextureEnabled = true;

            _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, RasterizerState.CullNone, _scalingEffect);//draws white
            DrawTextureBatch();
            DrawSpriteBatch();
            _batch.End();
        }





        void DrawSpriteBatch()
        {

            try
            {
                //note dont think we can have _primitivebactch and _sprint open at same time.. if so consolidate to one pass over bodylist
                foreach (KeyValuePair<Body, SpriteView> kvp in Graphics.BodySpriteMap)
                {
                    Body b = kvp.Key;


                    kvp.Value.CurrentFrame = b.IsShowingDress2 ? 1 : 0;

                    Texture2D texture = kvp.Value.CurrentTexture;


                    var bAABB = kvp.Value.BodyAABB;


                    var bodyTextureOrg = b.GetWorldPoint(bAABB.LowerBound);

                    var dpm = kvp.Value.DPM;

                    var texelScale = kvp.Value.TexelScale;

                    DrawTextureInBody
                        (b, texture,
                        bodyTextureOrg, b.Xf.Angle, true, texelScale);//source pos  on emit is in wcs we want local.. dont use the emit pos, use emitter pos 
                }
            }

            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }



        void DrawTextureBatch()
        {

            try
            {
                //note dont think we can have _primitivebactch and _sprint open at same time.. if so consolidate to one pass over bodylist
                foreach (KeyValuePair<Body, Texture2D> kvp in Graphics.BodyTextureMap)
                {
                    Body b = kvp.Key;
                    Texture2D texture = kvp.Value;

                    DrawTextureInRectBody(b, texture, b.Position, b.Rotation, true);//source pos  on emit is in wcs we want local.. dont use the emit pos, use emitter pos 
                }
            }

            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }


        private void DrawTextureInRectBody(Body b, Texture2D texture, float rot = 0f)
        {
            DrawTextureInRectBody(b, texture, b.Position, rot);
        }



        //TODO this is just used for the gas box...should be generalized.
        /// <summary>
        /// Draw thumbnails or other representative of body content in its position and to fill its rect, body must have 4 verts
        /// </summary>
        /// <param name="b"></param>
        /// <param name="texture"></param>
        /// <param name="posWCS"></param>
        /// <param name="rot">rotation in wcs</param>
        /// <param name="batchCalls">if true u must call batch.Begin and End aroudn this call, default is false</param>
        private void DrawTextureInRectBody(Body b, Texture2D texture, Vector2 posWCS, float rot = 0f, bool batchCalls = false)
        {


            //TODO TEXTURECLEAN remove and use the other one like spring in gun
            if (b.GeneralVertices.Count != 4)
            //TEXTURECLEAN
            {
                Debug.WriteLine("expected 4 verts to contain thumbnail image, have " + b.GeneralVertices.Count);

                //todo do whit in body space...not aabb like we do thumbs.
                DrawTextureInAABB(b, texture, posWCS, rot, batchCalls);
                return;

            };


            Vector2 bodySize = b.GeneralVertices[2] - b.GeneralVertices[0];

            //   bodySize.X = Math.Abs(bodySize.X);

            Microsoft.Xna.Framework.Vector2 texelScale = new Microsoft.Xna.Framework.Vector2(bodySize.X / texture.Width, bodySize.Y / texture.Height);


            //TODO  next see why rope image square..
            //  texelScale.X = 1;


            //TODO revisit gas and consolidate..TEXTURECLEAN rmeove this if we can remove t eh whole thing
            texelScale.Y = texelScale.X;// image aspect ratio must be preserved  and likely wont match the parent



            if (!batchCalls)
                _batch.Begin(SpriteSortMode.Deferred, DefaultTextureBlendState, null, null, RasterizerState.CullNone, _scalingEffect);//draws white

            Color imageColr = Color.Transparent;
            imageColr.A = (byte)(imageColr.A * VectorTransparency);
            _batch.Draw(texture, posWCS.ToVector2(), null, imageColr, rot, ToXNAVec(Vector2.Zero), texelScale, SpriteEffects.None, 0f);

            if (!batchCalls)
                _batch.End();
        }


        //TODO i think we dont need it TEXTURECLEAN
        private void DrawTextureInAABB(Body b, Texture2D texture, Vector2 posWCS, float rot = 0f, bool batchCalls = false)
        {

            AABB aabb = b.AABB;
            Vector2 bodySize = new Vector2(aabb.Width, aabb.Height);


            var texelScale = new Microsoft.Xna.Framework.Vector2(bodySize.X / texture.Width, bodySize.Y / texture.Height);

            //  var texelScale = Microsoft.Xna.Framework.Vector2.One;
            //TODO  next see why rope image square..
            //  texelScale.X = 1;
            //   texelScale.Y = texelScale.X;// image aspect ratio must be preserved  and likely wont match the parent

            if (!batchCalls)
                _batch.Begin(SpriteSortMode.Deferred, DefaultTextureBlendState, null, null, RasterizerState.CullNone, _scalingEffect);//draws white

            Color imageColr = Color.Transparent;
            imageColr.A = (byte)(imageColr.A * VectorTransparency);

            //    _batch.Draw(texture, posWCS.ToVector2(), null, imageColr, rot, Microsoft.Xna.Framework., texelScale, SpriteEffects.None, 0f);
            //  _batch.Draw(texture, posWCS.ToVector2(), null, imageColr, rot, ToXNAVec(Vector2.Zero), texelScale, SpriteEffects.None, 0f);

            Rectangle rectdest = new Rectangle();

            rectdest.Size = new Microsoft.Xna.Framework.Point((int)b.AABB.Width, (int)b.AABB.Height);
            rectdest.Location = new Microsoft.Xna.Framework.Point((int)b.AABB.LowerBound.X, (int)b.AABB.LowerBound.Y);

            _batch.Draw(texture, rectdest, null, Color.White, rot, ToXNAVec(Vector2.Zero), SpriteEffects.None, 0);


            if (!batchCalls)
                _batch.End();
        }

        /// <summary>
        /// Draw thumbnails or other representative of body content in its position and to fill its rect, body must have 4 verts
        /// </summary>
        /// <param name="b"></param>
        /// <param name="texture"></param>
        /// <param name="posWCS"></param>
        /// <param name="rot">rotation in wcs</param>
        /// <param name="batchCalls">if true u must call batch.Begin and End aroudn this call, default is false</param>
        private void DrawTextureInBody(Body b, Texture2D texture, Vector2 posWCS, float rot = 0f, bool batchCalls = false, Microsoft.Xna.Framework.Vector2 texelScale = default(Microsoft.Xna.Framework.Vector2))
        {

            if (texelScale == default(Microsoft.Xna.Framework.Vector2))
            {
                texelScale = b.IsShowingDress2 ? b.TexelScale2.ToVector2() : b.TexelScale.ToVector2();
                texelScale *= b.DressScale.ToVector2();//this is a fix for legacy files... to really fix means go over and look for wrong scales in tool by using dressscale2 if isshowingdress2
                                                       //LEGACY  MG_GRAPHIC
            }


            if (!batchCalls)
                _batch.Begin(SpriteSortMode.Deferred, DefaultTextureBlendState, null, null, RasterizerState.CullNone, _scalingEffect);//draws white


            Color imageColr = Color.White;
            imageColr.A = (byte)(imageColr.A * VectorTransparency);


            _batch.Draw(texture, posWCS.ToVector2(), null, imageColr, rot,
                    Microsoft.Xna.Framework.Vector2.Zero, texelScale, SpriteEffects.None, 0f);


            //need wcs of top left.. 
            if (!batchCalls)
                _batch.End();
        }


        /// <summary>
        /// draw a texture to screen no scaling at pos in pix with optional shader
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="posUL"> pos upper left, y + is down, 0,0 is top left, in viewport pix</param>
        ///<param name="shader"> pixel shader</param>
        public void DrawTextureScreen(Texture2D tex, Microsoft.Xna.Framework.Vector2 posUL, Effect shader = null, BlendState blend =null)
        {


            if (blend == null)
                blend = BlendState.NonPremultiplied;

         //   _batch.Begin(SpriteSortMode.Immediate, blend, null, null, RasterizerState.CullNone, shader);

            _batch.Begin(SpriteSortMode.Deferred, blend, null, null, RasterizerState.CullNone, shader);

          //     shader?.CurrentTechnique.Passes[0].Apply();
            _batch.Draw(tex, Vector2.Zero.ToVector2(),
                null, Color.White, 0, posUL, Microsoft.Xna.Framework.Vector2.One, SpriteEffects.None, 0f); ;

            _batch.End();
        }


        /// <summary>
        /// draw a texture to screen no scaling at pos i pix
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="posUL"> pos upper left, y + is down, 0,0 is top left, in viewport pix</param>

        public void DrawTextureScreen( Texture2D tex, Vector2 posUL)
        {
            DrawTextureScreen(tex, posUL.ToVector2());
           
        }


        /// <summary>
        /// draw texture at origin, no scaling 
        /// </summary>
        /// <param name="tex"></param>
        ///<param name="effect">Shader like AlpahTestEffect</param>
        public void DrawTextureScreen(Texture2D tex, Effect effect = null, BlendState blend = null)
        {
            DrawTextureScreen(tex, Microsoft.Xna.Framework.Vector2.Zero, effect, blend);

        }


        AlphaTestEffect alphaTestEffect = null;

        /// <summary>
        /// draw two tex overlaid unscaled at zero, bledn alpha with a shader or aplahtest effect.  
        /// viewport expected to be size of the textures
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="texture"></param>
     
        public void DrawCombinedTexturesScreen(Texture2D mask, Texture2D texture)
        {

            if ( alphaTestEffect == null )
            {
                alphaTestEffect = new AlphaTestEffect(_device);

                alphaTestEffect.VertexColorEnabled = true;
                alphaTestEffect.DiffuseColor = Color.White.ToVector3();
                alphaTestEffect.AlphaFunction = CompareFunction.Equal;
                alphaTestEffect.ReferenceAlpha = 255;
                alphaTestEffect.World = Matrix.Identity;
                alphaTestEffect.View = Matrix.Identity;

              //  alphaTestEffect.Projection = _localProjection;

              //  alphaTestEffect.Projection = Matrix.CreateOrthographicOffCenter(0f, texture.Width,texture.Height, 0f, 0f, 1f);

                alphaTestEffect.Projection = Matrix.CreateOrthographicOffCenter(0f, _device.Viewport.Width, _device.Viewport.Height, 0f, 0f, 1f);
            }



            //important both stencil states be created in their own object, cannot modify once set for some reason.
            DepthStencilState lState = new DepthStencilState();
            lState.StencilEnable = true;
            lState.StencilFunction = CompareFunction.Always;
            lState.ReferenceStencil = 1;
            lState.StencilPass = StencilOperation.Replace;
            lState.DepthBufferEnable = false;
            _device.Clear(ClearOptions.Target | ClearOptions.Stencil,
                                      new Color(0, 0, 0, 1), 0, 0);


            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, lState, null, alphaTestEffect);
            //draw whatever you want "visible" anything in the texture with an alpha of 0 will be allowed to draw.
                _batch.Draw(mask, Microsoft.Xna.Framework.Vector2.Zero, Color.White);
            

        _batch.End();

        
            DepthStencilState lState2 = new DepthStencilState();
            lState2.StencilEnable = true;
            lState2.StencilFunction = CompareFunction.Greater;
            lState2.ReferenceStencil = 0;
            lState2.StencilPass = StencilOperation.Keep;
            lState2.DepthBufferEnable = false;

            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, lState2, null);
            _batch.Draw(texture, Microsoft.Xna.Framework.Vector2.Zero, Color.Black);

            _batch.End();
            //done drawing to the render target



            /*

            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone, _alphatestShader);//draws white


            _batch.Draw(mask, Vector2.Zero.ToVector2(),
                null, Color.White, 0, Vector2.Zero.ToVector2(), Vector2.One.ToVector2(), SpriteEffects.None, 0f); ;

            _batch.Draw(texture, Vector2.Zero.ToVector2(),
                null, Color.White, 0, Vector2.Zero.ToVector2(), Vector2.One.ToVector2(), SpriteEffects.None, 0f); ;

            _batch.End();*/


            /*graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
graphics.ApplyChanges();

AlphaTestEffect alphaTestEffect = new AlphaTestEffect(pGraphicsDevice);
alphaTestEffect.VertexColorEnabled = true;
alphaTestEffect.DiffuseColor = Color.White.ToVector3();
alphaTestEffect.AlphaFunction = CompareFunction.Equal;
alphaTestEffect.ReferenceAlpha = 0;
alphaTestEffect.World = Matrix.Identity;
alphaTestEffect.View = Matrix.Identity;
Matrix projection = Matrix.CreateOrthographicOffCenter(0, 400,400, 0, 0, 1);
alphaTestEffect.Projection = projection;
        // Create fog of war mask
        if (mFogOfWarRT == null)
        {
            mFogOfWarRT = new RenderTarget2D(pGraphics.GraphicsDevice, MapSize, MapSize, false, SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8);
            pGraphics.GraphicsDevice.SetRenderTarget(mFogOfWarRT);

        }
        else
        {
            pGraphics.GraphicsDevice.SetRenderTarget(mFogOfWarRT);
        }
        //important both stencil states be created in their own object, cannot modify once set for some reason.
        DepthStencilState lState = new DepthStencilState();
        lState.StencilEnable = true;
        lState.StencilFunction = CompareFunction.Always;
        lState.ReferenceStencil = 1;
        lState.StencilPass = StencilOperation.Replace;
        lState.DepthBufferEnable = false;
        pGraphicsDevice.Clear(ClearOptions.Target | ClearOptions.Stencil,
                                  new Color(0, 0, 0, 1), 0, 0);


        pSpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, lState, null, alphaTestEffect);
        foreach (ClearArea lArea in mDrawQueue)
        {
//draw whatever you want "visible" anything in the texture with an alpha of 0 will be allowed to draw.
            pSpriteBatch.Draw(mAlphaMask, new Rectangle((int)lArea.X, lArea.Y, lArea.Diameter, lArea.Diameter), Color.White);
        }

        pSpriteBatch.End();

        // Draw minimap texture
        DepthStencilState lState2 = new DepthStencilState();
        lState2.StencilEnable = true;
        lState2.StencilFunction = CompareFunction.Equal;
        lState2.ReferenceStencil = 0;
        lState2.StencilPass = StencilOperation.Keep;
        lState2.DepthBufferEnable = false;

        pSpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, lState2, null);
        pSpriteBatch.Draw(mDot, new Rectangle(0, 0, 400, 400), Color.Black);

        pSpriteBatch.End();
        //done drawing to the render target
        pGraphicsDevice.SetRenderTarget(null);
        pGraphicsDevice.Clear(Color.Gray);
        pSpriteBatch.Begin();
        pSpriteBatch.Draw(mDot, new Rectangle(0, 0, 400, 400), Color.Blue);
        pSpriteBatch.Draw(mFogOfWarRT,Vector2.Zero,Color.White);
        pSpriteBatch.End();*/

        }


        //TODO MG_GRAPHICS either switch to this class, serialize Vector2 some other way or make an operator on our Vector2
        // or copy teh code for sprite bathc in here  todo mg_graphics consolidate this
        Microsoft.Xna.Framework.Vector2 ToXNAVec(Vector2 vec) { return new Microsoft.Xna.Framework.Vector2(vec.X, vec.Y); }

        //operator ?  or switch to xna from farseer and serializer w json ..anyways thye might go to Numberics Vector class 


        public void RenderDebugData(ref Matrix projection, List<Body> bodyList = null,
                                    BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, float alpha = 1.0f)
        {
            if (!Enabled)
                return;

            Matrix view = Matrix.Identity;
            Matrix world = Matrix.Identity;
            RenderDebugData(ref projection, ref view, ref world, bodyList, blendState, samplerState, depthStencilState, rasterizerState, alpha);

        }

        public void LoadContent(GraphicsDevice device, ContentManager content, IPrimitiveBatch primitiveBatch = null)
        {


            //    GraphicsDevice = device;

            LoadContentForDevice(device, content, primitiveBatch);
        }

        

        private void LoadContentForDevice(GraphicsDevice device, ContentManager content, IPrimitiveBatch primitiveBatch)
        {
            _device = device;

            // Create a new SpriteBatch, which can be used to draw textures.
            // does this conflict with our MGGame instance, shoud we look at Nez proxy

            try
            {
                _font = content.Load<SpriteFont>("Console16");

                fontWidth = 1.5f;
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc.ToString());

            }

            _batch = new SpriteBatch(_device);

            _primitiveBatch = (primitiveBatch != null) ? primitiveBatch : new PrimitiveBatch(_device, 10000);


            //  var font = Content.Load<BitmapFont>("Nez.Content.NezDefaultBMFont.xnb");

            if (_stringData == null)
                _stringData = new List<StringData>();

            _stringData.Clear();

            _localProjection = Matrix.CreateOrthographicOffCenter(0f, _device.Viewport.Width, _device.Viewport.Height, 0f, 0f, 1f);
            _localView = Matrix.Identity;
            _localWorld = Matrix.Identity;

            _scalingEffect = new BasicEffect(device);

            _cm = content;



            try
            {
                _clipper = content.Load<Effect>("ClipShader");
             //   _clipper = content.Load<Effect>("MultiTextureOverlay");

            }
            catch (Exception exc)
            {

                Debug.WriteLine("fialed to load clipShaper" + exc);
            }

        }


        ContentManager _cm;
        public Effect Clipper
        {
            get => _clipper;
       
        }

        Effect _clipper;

        #region Nested type: ContactPoint

        private struct ContactPoint
        {
            public Vector2 Normal;
            public Vector2 Position;
            public PointState State;
        }

        //TODO see core.game..graphics_mg
        public struct LineSegment
        {

            /// <summary>
            /// Start Vector2 of this line segment in WCS
            /// </summary>
            public Vector2 StartPt;
            /// <summary>
            /// End Vector2 of the segment in WCS
            /// </summary>
            public Vector2 EndPt;


        }

        #endregion

    }
}