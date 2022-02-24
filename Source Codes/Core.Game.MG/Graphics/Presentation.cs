using System;
using System.Linq;
using System.Collections.Generic;

using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Collision;

using System.Diagnostics;
using Microsoft.Xna.Framework;
using Core.Data;
using Core.Data.Entity;
using static Core.Game.MG.Graphics.BaseView;
using Core.Data.Collections;
using Core.Trace;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysicsView;

namespace Core.Game.MG.Graphics
{

    /// </summary>
    public class Presentation
    {


        private Camera _camera;

        public static Presentation Instance = null;

        #region Constructor


        public static bool UseViews = false;

        public BodyViewMap get => bodyViewMap;
        BodyViewMap bodyViewMap;


        public List<Body> bodySpriteViewDirtyList = new List<Body>();

        public Presentation(GraphicsDevice dev, World world, AABB startWCSWindow)
        {


            Graphics.GraphicsDevice = dev;
            // default camera
            _camera = new Camera(dev, startWCSWindow);
            Instance = this;
            bodyViewMap = new BodyViewMap(this, world);


        }
        #endregion


        #region Methods


        public void Clear()
        {
            bodyViewMap.Clear();
        }


        List<BaseView> currentViews = new List<BaseView>();
        public void CreateViewsForLevel(Level level)
        {
            if (!UseViews)
                return;

            bodyViewMap.Clear();

            bodyViewMap.World = SimWorld.Instance.Physics;

            foreach (IEntity entity in level.Entities)
            {
                CreateView(entity);
            }


        }



        public void UpdateViews(DebugView fv, GraphicsDevice gr )
        {

            if (UseViews)
            {
               throw new NotImplementedException();
            }
                 
       
            Rasterizer.CaptureTextureForBodies(gr, bodySpriteViewDirtyList, fv);
            bodySpriteViewDirtyList.Clear();

        }
        /// <summary>
        /// mapped by Body for now.. todo change broadphase to Templated
        /// </summary>
        /// <param name="body"></param>
        public void RemoveView(Body body)
        { 
            
            Graphics.BodySpriteMap.Remove(body);
           
            if (!UseViews)
                return;

            bodyViewMap.RemoveView(body);

        }

        public void CreateView(IEntity entity)
        {


            if (entity is Body)
            {
                Core.Game.MG.Graphics.Graphics.Instance.Presentation.bodySpriteViewDirtyList.Add(entity as Body);
            }


            if (!UseViews)
                return;


            BaseView entityView = null;



            if (entity is Body)
            {
                entityView = new BodyView(entity as Body);
                bodyViewMap.AddView(entity as Body, entityView);
            }
            else
             if (entity is Spirit)
            {
                entityView = new SpiritView(entity as Spirit);
                bodyViewMap.AddView((entity as Spirit).MainBody, entityView);  //TODO modify mainbody AABB to be the whole spirits aabb, set if issues.. maybe PRODUCTION only
            }
            else
            if (entity is Planet)
            {
                entityView = new PlanetView(entity as Planet);
                bodyViewMap.AddView((entity as Planet).Spirit.MainBody, entityView);  //TODO modify mainbody AABB to be the whole spirits aabb, set if issues.. maybe PRODUCTION only
            }
        }


        public FastList<BaseView>  DisplayList        { get => bodyViewMap.DisplayList; }



        static public bool UseLastFrameLockless = true;
        private LevelFrameView lastFrame = new LevelFrameView();
        private LevelFrameView currentFrame = new LevelFrameView();


        public LevelFrameView LastFrame => lastFrame;

        //cant do this.. could be drawing last still.. need to keep at least 10 ..if pysics is mch faster, etc..

       //TODO test how many back.. use queue or pooll.. frmes we cant hold and measure some..
   //     public void SwapFrames() { LevelFrameView temp = currentFrame; currentFrame = lastFrame; lastFrame = temp; }
          
        // graphics update
        public void Update()
        {


            if (UseLastFrameLockless)
            {

                currentFrame = new LevelFrameView();

                currentFrame.CloneBodies(World.Instance.BodyList);
                if (Level.Instance != null)
                {
                    currentFrame.CloneSpiritThatDraw(Level.Instance.Spirits);
                }
                currentFrame.CloneRayViews();
                lastFrame = currentFrame;
           //     SwapFrames();

            }

            if (!UseViews)
                return;

  
              

            using (new TimeExec("UpdateUsePosition"))
            {
                IEnumerable<BaseView> views = bodyViewMap.GetVisibleViews();

                bodyViewMap.UpdateUsePosition(views);
          

                using (new TimeExec("DisplayList"))
                {
                    DisplayList.Clear();
                    DisplayList.AddRange(views);
                }
             }

#if LEGACY

            //update each spirits body color based on the spirit energy level.   give it a blue tint to make it look sick.
            if (level == null)
                return;

            foreach (Spirit sp in level.Spirits)
            {

                if (_camera.Transform.LimitEnabled == true)
                {
                    AdjustLevelLimit(sp);
                        
            }
        }


        /// <summary>
        /// When spirit step outside level boundary, expand camera limit so spirit still viewable.
        /// </summary>
        protected void AdjustLevelLimit(Spirit sp)
   

            if (Camera.Transform.WindowLimit.Contains(ref sp.AABB) == false)
            {
                // check spirit speed first, predict spirit position on nexct cycle.
                Farseer.Xna.Framework.Vector2 disp = sp.MainBody.LinearVelocity * Simulation.SimWorld.Instance.PhysicsUpdateInterval;
                Farseer.Xna.Framework.Vector2 nextWorldPos = sp.MainBody.WorldCenter + disp;

                // enlarge spirit aabb on new position, based on velocity and world window size
                float moveDist = disp.Length() * 10f;
                Farseer.Xna.Framework.Vector2 newSPsize = Camera.Transform.WindowSize * moveDist;
                Farseer.Xna.Framework.Vector2 newTopLeft = nextWorldPos - (newSPsize * 0.5f);
                AABB spiritAABB = new AABB(newSPsize.X, newSPsize.Y, newTopLeft);

                AABB both = Camera.Transform.WindowLimit;
                both.Combine(ref spiritAABB);

                Camera.Transform.WindowLimit = both;
            }
     

        
#endif


        }



        #endregion


        /// <summary>
        /// Current camera. Camera can be obtained by using presentation canvas 
        /// as constructor param, i.e: .. new Camera(Presentation.Canvas);
        /// By default, setting this property will automatically switch camera.
        /// Use Camera.Activate() to further switch between camera.
        /// </summary>
        public Camera Camera
        {
            get { return _camera; }
           
        }

        Level _level;
        public Level Level
        {
            set
            {
                _level = value;

                if (_level != null)
                {
                    _level.UpdateSpiritsCache();
                }
            }
        }
    }
}
