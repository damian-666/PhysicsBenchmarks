using Core.Data;
using Core.Data.Collections;
using FarseerPhysics.Common;
using MGCore.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.Timers;
using Storage;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Touch.Joystick.GameObjects;
using Touch.Joystick.Input;

namespace MGCore
{
    public class MGCore : Game
    {
        protected GraphicsDeviceManager _graphicsManager;
        protected SpriteBatch _spriteBatch;

        protected SpriteFont _font;

        public static Action LoadSettings;

        public static Action OnBeginGameCode;

        public static MGCore Instance => _instance;

        /// <summary>
        /// facilitates easy access to the global Content instance for internal classes
        /// </summary>
        internal static MGCore _instance;


        /// <summary>
        /// core emitter. emits only Core level events.
        /// </summary>
        public static Emitter<CoreEvents> Emitter;

        /// <summary>
        /// enables/disables if we should quit the app when escape is pressed
        /// </summary>
        public static bool ExitOnEscapeKeypress = false;//we handing this in game code for Back
        


        public GraphicsDeviceManager GraphicsDeviceManager { get => _graphicsManager; }

        /// <summary>
        /// used to coalesce GraphicsDeviceReset events
        /// </summary>
        ITimer _graphicsDeviceChangeTimer;
        readonly TimerManager _timerManager = new TimerManager();

        FastList<GlobalManager> _globalManagers = new FastList<GlobalManager>();




        public MGCore()
      //  public MGCore(int width = 1280, int height = 720, bool isFullScreen = false, string windowTitle = "2DWorld", string contentDirectory = "Content")
        {

             _graphicsManager = new GraphicsDeviceManager(this)
            {
             //   PreferredBackBufferWidth = width,
           //     PreferredBackBufferHeight = height,
          //      IsFullScreen = isFullScreen,
                SynchronizeWithVerticalRetrace = true
            };

            _graphicsManager.GraphicsProfile = GraphicsProfile.HiDef;

            _graphicsManager.DeviceReset += OnGraphicsDeviceReset;
            _graphicsManager.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;

        //    _graphicsManager.IsFullScreen = isFullScreen;

            Window.ClientSizeChanged += OnGraphicsDeviceReset;
            Window.OrientationChanged += OnOrientationChanged;


            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _instance = this;
            Emitter = new Emitter<CoreEvents>(new CoreEventsComparer());

            Window.Title = "2DWORLD";
          //  Window.Title = windowTitle;


            RegisterGlobalManager(_timerManager);

        }




        public  int Width
        {
            get => _graphicsManager.PreferredBackBufferWidth;
            set => _graphicsManager.PreferredBackBufferWidth= value;
        }

        public bool IsFullScreen { get => _graphicsManager.IsFullScreen; set => _graphicsManager.IsFullScreen = value; }

        /// <summary>
        /// height of the GraphicsDevice back buffer
        /// </summary>
        /// <value>The height.</value>
        public  int Height
        {
            get => _graphicsManager.PreferredBackBufferHeight;
            set => _graphicsManager.PreferredBackBufferHeight = value;
        }



        void OnOrientationChanged(object sender, EventArgs e)
        {
            Emitter.Emit(CoreEvents.OrientationChanged);
        }

        /// <summary>
        /// this gets called whenever the screen size changes
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnGraphicsDeviceReset(object sender, EventArgs e)
        {
            // we coalese these to avoid spamming events
            if (_graphicsDeviceChangeTimer != null)
            {
                _graphicsDeviceChangeTimer.Reset();
            }
            else
            {
                _graphicsDeviceChangeTimer = Schedule(0.05f, false, this, t =>
                {
                    (t.Context as MGCore)._graphicsDeviceChangeTimer = null;
                    
                    
                    Emitter.Emit(CoreEvents.GraphicsDeviceReset);
                });
            }
        }


        /// <summary>
        /// schedules a one-time or repeating timer that will call the passed in Action
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds.</param>
        /// <param name="repeats">If set to <c>true</c> repeats.</param>
        /// <param name="context">Context.</param>
        /// <param name="onTime">On time.</param>
        public static ITimer Schedule(float timeInSeconds, bool repeats, object context, Action<ITimer> onTime)
        {
            return _instance._timerManager.Schedule(timeInSeconds, repeats, context, onTime);
        }



        #region Global Managers

        /// <summary>
        /// adds a global manager object that will have its update method called each frame before Scene.update is called
        /// </summary>
        /// <returns>The global manager.</returns>
        /// <param name="manager">Manager.</param>
        public static void RegisterGlobalManager(GlobalManager manager)
        {
            _instance._globalManagers.Add(manager);
            manager.Enabled = true;
        }

        /// <summary>
        /// removes the global manager object
        /// </summary>
        /// <returns>The global manager.</returns>
        /// <param name="manager">Manager.</param>
        public static void UnregisterGlobalManager(GlobalManager manager)
        {
            _instance._globalManagers.Remove(manager);
            manager.Enabled = false;
        }

        /// <summary>
        /// gets the global manager of type T
        /// </summary>
        /// <returns>The global manager.</returns>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static T GetGlobalManager<T>() where T : GlobalManager
        {
            for (var i = 0; i < _instance._globalManagers.Length; i++)
            {
                if (_instance._globalManagers.Buffer[i] is T)
                    return _instance._globalManagers.Buffer[i] as T;
            }

            return null;
        }

        #endregion


        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();

       
        }




   
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);


           
       //   _font = Content.Load<SpriteFont>("Score");// or arial


            //need nez content read for this..
            // _font = Content.Load<SpriteFont>("NezDefaultBMFont");// or arial
             
            //loading a biggest font and scale by .5 seemms to work better
            _font = Content.Load<SpriteFont>("Console32");// or arial


            // TODO: use this.Content to load your game content here

            //     if ( Input.Touch.IsConnected)





        }


        bool once = true;


        protected override void Update(GameTime gameTime)
        {

            if (!Input.SticksReady()&& once && Input.UseVirtSticks)
            {
                 Input.InitVirtualSticks(_font, Content);
                once = false;
            }

            if (ExitOnEscapeKeypress &&
                (Input.IsKeyDown(Keys.Escape) || Input.GamePads[0].IsButtonReleased(Buttons.Back)))
            {

                base.Exit();
                return;
            }

           /*
            //TODP TODO might want to update when on game update, for via callcack from the physics loop, make it more responsive.  avoid thread conflic
            if (DualStick != null)
            {
              

                var relativePostion = new
                {
                    Left = DualStick.LeftStick.GetRelativeVector(DualStick.aliveZoneSize),
                    Right = DualStick.RightStick.GetRelativeVector(DualStick.aliveZoneSize)
                };

                LeftStickBall.Move(relativePostion.Left, gameTime);
            }*/
       

            Time.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            for (var i = _globalManagers.Length - 1; i >= 0; i--)
            {
                if (_globalManagers.Buffer[i].Enabled)
                    _globalManagers.Buffer[i].Update();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (Input.SticksReady() )
            {
                //TODO draw the balls in dual stick where thumb is
             //   _spriteBatch.Begin();
             //   LeftStickBall.Draw(_spriteBatch);
              //  _spriteBatch.End();

              //  _spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque);//cant cuse the border will be blak
              //issue is thumns are slightly xparent
                _spriteBatch.Begin();//xparent in texture by defaut.. mabye we can set all a  to 255 if not zero in the bits..
                Input.DualTouchStick.Draw(_spriteBatch);
                _spriteBatch.End();
            }

            base.Draw(gameTime);//draws the DrawableComponent
        }


        protected override void OnExiting(object sender, EventArgs args)
        {
            base.OnExiting(sender, args);
        }

    }
}
