using Nez;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Core.Game.MG;
using System.Diagnostics;
using Core.Data.Interfaces;
using Storage;
using System.IO;
using Core.Data;

namespace _2DWorldCore
{

    //NOTE todo  base clas GameCore is like Nez.Core..iof pro ith  namespace, game and core are issues.. even at Game type conflicts need prefix
    //todo might be easier to rename Game.core to nez, automatically.. by uloading all else..


    //TODO load up and init Nez with font.
    //trace the code, game.core..
    //trace nez and load font.

    //either add a new font like aehter sample or

    //mabye skip fond.  get a drawing to load first.

    /// get the game.core to load and draw stuff.
    /// 
    //or rename to nez... diff nez and game.core   is our game.core just nez and nez core merged?
   //remaneam the namespaces, make nez nez again.




    /// <summary>
    /// This is the UI and the glue just under the EXE level,  that holds  updates teh physics, navigates teh wolrd wiht a  2d camera or entity tracker, the AI togetheer with the simulator, game code, physics Core.MG ( graphcs, presentation) 
    /// It loads leves, some of which can link to others . 
    /// 
    /// the very thin 2Dworld App for android, 2dworldUWP app, 2Dworld.exe, maybe xbox.exe if  uwp has issues, maybe tool if it can overlay it somehow later..
    /// 
    /// it links up to Core.Game.MG which links to Core.Game ( formerly Nez)
    ///     this has the physics thread and display list primitives.
    /// SimWorld, smulation , winddrag , timer 
    /// 

    /// This module: 
    /// all the  impements teh IGameCode..   it has everythig in GAmeUWP , or better example shadowplay dll.
    /// 
    /// has engine.cs  ( gameloop , 2 threaded producer consumer model using display list for physic body data) selector crosshairs,  datastorre,
    /// 
    /// all the sournd resources are reference and packaged in here as mogogame resources if possible, to avoid dublication in the variou s platfrom depdendent packaging .
    /// 
    /// ON mint mhave rotational radial gravity for plants, with places on the surface taht are links to the levels, previously arranged in a N accross by 3 deep 
    /// 
    /// Leves are mapped like this:
    /// with 0 , 1 , 2, 3 going west.
    /// ground a, below ground b , c lower
    /// This will load the main root level, or the curent workikng leve saved in datastore with all the apps.
    /// 
    ///
    /// 
    /// </summary>
    public class NezGame : GameCore
    {
       
        private SpriteBatch _spriteBatch;

      

        public NezGame(): base()
        {
          //  _graphics = new GraphicsDeviceManager(this);
          //  Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }




        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here

        }

        protected override void OnActivated(object sender, EventArgs args)
        {
            base.OnActivated(sender, args);
        }

        protected override void BeginRun()
        {

            base.Window.AllowUserResizing = true;

       
            GameStart.Init(); //load level and start physics loop
          
        }
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here

      //      MouseState mouse =  Input.CurrentMouseState;



            //  ShadowFactory.Engine.World.Physics.ProcessChanges();
            //   ShadowFactory.Engine.World.Physics.Step(1 / 60f);


         //   Debug.WriteLine(mouse);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
           
   if (SimWorld.Instance?.PhysicsThread?.WaitForAccess(100) == false)
            {
                Debug.WriteLine("timeout getting draw lock");//just skip a frame.. todo adjust waitl for thelast  physics update interval

                return;
            };

            GraphicsDevice.Clear(Color.Coral);


            try
            {
                //    Graphics.Instance.Presentation.Camera.Update(gameTime.ElapsedGameTime.Ticks);


                //   Graphics.Instance.Presentation.Camera.ZoomWindow(Level.Instance.BoundsAABB);


             //  (ShadowFactory.Engine.ActiveGameCode as LevelGameCodeBase).StartView(Level.Instance);

                //TODO use a better pattern, when world is changed set it once, not every draw
                GameStart.PhysicsView?.SetWorld(ShadowFactory.Engine.World.Physics);

              //  ShadowFactory.Engine.World.IsParticleOn = false;

             //   GameStart.PhysicsView?.SetWorld(SimWorld.RecentInstance.Physics);
                //TODO lock physicsfor readonly by getting the existing lock, fix probably collection  changed during iteration and other issues.
                // will have to wait here for next physics cycle.    
                // 
                //TODO a producer consumer might prevent physics from doing a cycle if draw is almost done.
                // but copy all the physics views to a display list, lock physics only for the copy step. 
                Nez.Matrix2D View = GameCore.Graphics.Presentation.Camera.TransformMatrix;

                Matrix Projection = GameCore.Graphics.Presentation.Camera.ProjectionMatrix;
             
             //   GameStart.PhysicsView?.AppendFlags(FarseerPhysics.Diagnostics.DebugViewFlags.PerformanceGraph);

                GameStart.PhysicsView?.AppendFlags(FarseerPhysics.Diagnostics.DebugViewFlags.DebugPanel);
                GameStart.PhysicsView?.DrawString(Vector2.Zero.ToVector2(),  "test");

                GameStart.PhysicsView?.DrawString(new Farseer.Xna.Framework.Vector2(30,10), "test sadflk;sksdlfks");


                GameStart.PhysicsView?.RenderDebugData(Projection, View);


                // TODO: Add your drawing code here

                base.Draw(gameTime);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            finally
            {

                SimWorld.Instance?.PhysicsThread?.FinishedAccess(); //_accessEvent.Set();

                //   SimWorld.RecentInstance.PhysicsThread.IsLocked = false; //REVIEW even need this?

            }
        }

        public static bool ReloadLastSaved()
        {
            using (new SimPauser())
            {
                object workingFileName = null;

                if (!DataStore.Instance.TryGetValue(Serialization.WorkingLevelNameKey, out workingFileName))
                    return false;

                string filename = (string)workingFileName;


                if (string.IsNullOrEmpty(filename))
                    return false;


                FileInfo fi = new FileInfo(filename);

                string levelName = fi.Name;


                var level = Storage.Serialization.LoadDataFromFileInfo<Level>(fi);

                if (level == null)
                {
                    level = Storage.Serialization.LoadDataFromAppResource<Level>(filename);
                }


                if (level == null)
                    return false;

                AutoLevelSwitch gamecode = ShadowFactory.Engine.ActiveGameCode as AutoLevelSwitch;

                return gamecode.LoadLevel(filename);



            }
        }

   
    }
}
