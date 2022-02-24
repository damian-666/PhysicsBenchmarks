//#define TWOPASSDRAW
#define CLIPTEST

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
using MGCore;
using Core.Game.MG.Graphics;


using Matrix = Microsoft.Xna.Framework.Matrix;
using Core.Game.MG.Simulation;
using System.Threading;
using FarseerPhysics.Diagnostics;
using Core.Data.Entity;
using Core.Trace;
using FarseerPhysicsView;
using System.Reflection;
using FarseerPhysics.Dynamics;
using System.Collections.Generic;
using FarseerPhysics.Dynamics.Particles;
using UndoRedoFramework.LogExec;
using System.Threading.Tasks;
using _2DWorldCore.UI;


using AABB = FarseerPhysics.Collision.AABB;
using System.Linq;
using Core.Data.Geometry;

namespace _2DWorldCore
{


    /// <summary>
    /// Basic UI and core app level functions for 2DOword for mobile and desktop platforms, built over the general purpose MGCore over XNA Game, Tool doesnt use this.
    /// The platfrom dependent parts over this are designed to be as thin as possible, with all shared functionality in here
    /// </summary>
    public class CoreGame : MGCore.MGCore
    {

        public delegate string FileOpenDelegate(); //TOD get this working for window testing
        public static FileOpenDelegate OpenFileDlg;

        //global settings

        internal const int MsaaSampleLimit = 32;

        public static bool IsAndroid = false;

        public static bool ShowDebugInfo = true;

        public static bool ParallelDecodeJpg = true;

        public static bool ShowFPSTitle = true;

        public static bool IsUIMouseVisible = true;

        public static bool IsPaused {
            get => !SimWorld.Instance.PhysicsThread.IsRunning;
            set { SimWorld.Instance.PhysicsThread.IsRunning = !value; }
        }





        public static string PlayInstructions
        {
            get => "Neuralink: " + (IsAndroid ? PointInstructions : "Arrow keys to move, Z X C for hand Actions, or WSAD for move w JKL Action");
        }

        public static string PointInstructions = "Click or Touch to Point or grab, tap twice at target to throw";
        /// <summary>
        /// features to toggle at runtime for debug isolation purpose
        /// </summary>
        public static FeatureSet FeatureSet = new FeatureSet();


        public static new CoreGame Instance;

        public CoreGame()
        {

            Instance = this;
            IsUIMouseVisible = true;
            GraphicsDeviceManager.PreferMultiSampling = true;

            SimWorld.IsDirectX = IsDirectX;

            base.Window.AllowUserResizing = true;

            //this autoincrement
            versionStr = GetCoreVersion();

#if PRODUCTION
          //  ShowDebugInfo = false;
#endif

        }


        private static string GetCoreVersion()
        {
            string val = typeof(CoreGame).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            if (val.EndsWith(".1"))
            {
                val.TrimEnd('1');
                val.TrimEnd('.');
            }

            return val;
        }


        static public bool IsDirectX = false;

        static public bool LooseFiles = false;

        static public bool HasKeyboard = true;


        protected override void Initialize()
        {
            base.Initialize();

            MGCore.MGCore.Emitter.AddObserver(CoreEvents.GraphicsDeviceReset, this.OnGraphicsDeviceReset);
            MGCore.MGCore.Emitter.AddObserver(CoreEvents.OrientationChanged, this.OnGraphicsDeviceReset);

            if (LoadSettings != null)
            {
                LoadSettings.Invoke();
            }

        }

        protected Button btnSettings;

        protected SettingsDlg dlgSettings;

        protected ConsoleDlg dlgConsole;


        public static string[] SwitchLabels = new string[] { "Pause Simulation (Esc)", "Sounds On", "Simulate Wind", "Enable Particles", "Cloud Break" };

        public static string[] FPSvals = new string[] { "Full Throttle", "60 FPS", "100 FPS", "200 FPS", "300 FPS" };

        public SettingsDlg DlgSettings { get => dlgSettings; }

        List<UIElement> rootUIObjects = new List<UIElement>();

        public List<UIElement> RootUIObjects { get => rootUIObjects; }

        protected override void LoadContent()
        {

            //TODO move the game content into our 2DWorldCore  .. see how tool migh find a font tho..
            SimWorld.LooseFiles = LooseFiles;

            base.LoadContent();


            //we always have a Device by here
            GraphicsDevice.PresentationParameters.MultiSampleCount           // set to windows limit, if gpu doesn't support it, monogame will autom. scale it down to the next supported level
            = GraphicsDeviceManager.PreferMultiSampling ? MsaaSampleLimit : 0;

            GameStart.Init(GraphicsDevice);

            InitRootUI();

            rootUIObjects.Add(btnSettings);
            btnSettings.Clicked += OnSettingsClicked;

            AvgTimer.OnRecordFPS = OnRecordDrawAvgFPS;


//#if !PRODUCTION
            Core.Trace.TimeExec.SetOutputListener(CoreLogCallback);

            //#endif
            // Debug.Listeners.Add(CoreLogCallback);


            
            Presentation.UseLastFrameLockless = PhysicsThread.Lockless ;

        }


        public void CoreLogCallback(int importance, string category, string message)
        {

            ConsoleDlg.Instance.WriteLine(category + " " + message);

        }


        public const int butSize = 64;

        private void InitRootUI()
        {
            UIElement.InitUI(Window, GraphicsDevice, Content, _font);


            Vector2 settingBtnOffset = new Vector2(6, 6);
            
            if  (IsAndroid)
            {
                settingBtnOffset = new Vector2(36, 26);

                Touch.Joystick.GameObjects.Ball.Scale = 1f;

                HasKeyboard = false;
            }
          
            btnSettings = new Button(new FarseerPhysics.Collision.AABB(butSize, butSize, settingBtnOffset.ToVector2()),
                "gear64");

            dlgSettings = new SettingsDlg();

            dlgSettings.Hide();
            rootUIObjects.Add(dlgSettings);

            dlgSettings.ChildClicked += OnChildClicked;

            dlgSettings.Closed += SettingsClosed;

            InitChecksFromState(dlgSettings);
            dlgConsole = new ConsoleDlg(0.8f);


            lastTargetFPS = PhysicsThread.TargetFrameDT;

            if (lastTargetFPS == -1)
                lastTargetFPS = 1000 / 300;

        }


        int lastTargetFPS = 1000 / 300;




        void InitChecksFromState(CloseableDialogBox dlg)
        {

            foreach (CheckBox x in dlgSettings.GetCheckBoxes())
            {

                string tag = x.Name.ToLower();


                if (tag.Contains("pause"))
                {
                    x.IsChecked = CoreGame.IsPaused;
                }
                if (tag.Contains("sound"))
                {
                    x.IsChecked = AudioManager.Instance.IsSoundOn;
                }
                else if (tag.Contains("wind"))
                {
                    x.IsChecked = SimWorld.IsWindOn;
                }
                else if (tag.Contains("particle"))
                {
                    x.IsChecked = SimWorld.IsParticleOn;
                }
                else if (tag.Contains("cloud break"))
                {
                    x.IsChecked = SimWorld.CloudBreak;
                }


                SetCheckFromFPS(PhysicsThread.TargetFrameDT);
                
               

            }


        }
            private void SetCheckFromFPS( int fps)
            {

            //clear all
             foreach( var x in CoreGame.FPSvals)
            {
                dlgSettings.GetCheckBoxes().First(c => c.Name.Contains(x.ToString())).IsChecked = false; ;
            }

                var tag = "Full";
                    if (fps > 0) {
                    tag = fps.ToString(); }


                dlgSettings.GetCheckBoxes().First(x => x.Name.Contains(tag)).IsChecked = true; ;
    
            }

      

        
        void OnChildClicked(UIElement sender)
        {//TODO use  reflection or somethign to generate this..  do like pro page

            string tag = sender.Name.ToLower();

            CheckBox chkBox = (CheckBox)sender;
            if (chkBox == null)
                return;


            if (tag.Contains("pause"))
            {
                CoreGame.IsPaused = chkBox.IsChecked;
            }
            if (tag.Contains("sound"))
            {
                AudioManager.Instance.IsSoundOn = chkBox.IsChecked;
            }
            else if (tag.Contains("throttle"))
            {

                if (chkBox.IsChecked)
                {
                    PhysicsThread.TargetFrameDT = -1;
                    SetCheckFromFPS(-1);
                }
                else
                {
                    PhysicsThread.TargetFrameDT = lastTargetFPS;
                    SetCheckFromFPS(lastTargetFPS);
                }
            }
            else if (tag.Contains("wind"))
            {
                SimWorld.IsWindOn = chkBox.IsChecked;
            }
            else if (tag.Contains("particle"))
            {
                SimWorld.IsParticleOn = chkBox.IsChecked;
            }
            else if (tag.Contains("cloud"))
            {
                SimWorld.CloudBreak = chkBox.IsChecked;
            }
            else
            if (tag.StartsWith("60"))
            {   

                PhysicsThread.TargetFrameDT = 1000/60;
                SetCheckFromFPS(60);
                 }
            else
            {
                
                var num = sender.Name.Substring(0, 3);
                int result = -1;
                if (int.TryParse(num, out result))
                {
                    PhysicsThread.TargetFrameDT = 1000/result;
                    SetCheckFromFPS(result);

                }
            }       

        }

        private AABB GetAABB()
        {
            return new AABB(Window.ClientBounds.Width, Window.ClientBounds.Height, Vector2.Zero.ToVector2());
        }


        protected void OnSettingsClicked(object sender)
        {
            ShowSettings();
        }

        protected void SettingsClosed(UIElement sender)
        {
            btnSettings.Show();
        }
        public bool IsSettingsActive { get => dlgSettings == null ? false : dlgSettings.IsVisible; }

        public void ShowSettings()
        {
            dlgSettings.Show();
            btnSettings.Hide();
        }

        public void HideSettings()
        {
            dlgSettings.Hide();
            btnSettings.Show();
        }



        protected  void OnRecordDrawAvgFPS( int i)
        {
           World.Instance.RenderMaxUpdatePerSecond = i;
        }

        protected override void OnActivated(object sender, EventArgs args)
        {
            base.OnActivated(sender, args);
        }



        protected override void BeginRun()
        {
#if TIMERTEST
//see code in Physics thread trying to even out the framerate   NOTE not needed after setting the timeBeginPeriod in Windows
//in Android maybe  we need this, TODO erase 
//https://www.codeproject.com/Articles/61964/Performance-Tests-Precise-Run-Time-Measurements-wi
            Process.GetCurrentProcess().ProcessorAffinity =
   new IntPtr(2); // Uses the second Core or Processor for the Test
            Process.GetCurrentProcess().PriorityClass =
        ProcessPriorityClass.High;      // Prevents "Normal" processes 
                                        // from interrupting Threads
            Thread.CurrentThread.Priority =
        ThreadPriority.Highest;     // Prevents "Normal" Threads 
                                    // from interrupting this thread

#endif
        }


   
        protected override void Update(GameTime gt)
        {

            //TODO put this on the callback from the GameLoop
            base.Update(gt); //updates the Input keys


            if (IsMouseVisible!= IsUIMouseVisible)
            {
                IsMouseVisible = IsUIMouseVisible;  //must set this on UI thread
            }


            //MG_GRAPHICS
            if (!Engine.IsBackgroundThread)
            {
                ShadowFactory.Engine.Update(gt);
            }

            //means update ui on bk thread, click handlers, is nice because we dont need to wait
            if (!Engine.IsGameBKThread)
            {
                SyncGameUpdate(gt);
            }


            //updating input on background thread seems to work fine in windows
            //TODO clean out  IsBKInputUpudate experiments
            if (!InputCommand.IsBKInputUpudate)
                Input.Update();


#if FILEOPENTEST   //this fails to show the dialog, possible threading issue
            LevelGameCodeBase levelUI = ShadowFactory.Engine.ActiveGameCode as LevelGameCodeBase;

            if (levelUI != null)
            {
               if (  levelUI.ShowFileDialogUI)
                {
                    levelUI.ShowFileDialogUI = false;
                    levelUI.LoadLevelUsingFileDialog();
                }

            }
#endif

        }

        int tick = 0;


        private void SyncGameUpdate(GameTime gt)
        {

            TickEventArgs e = new TickEventArgs(tick++,
               (int)gt.ElapsedGameTime.TotalMilliseconds, (int)gt.ElapsedGameTime.TotalSeconds,
               (int)(1.0 / gt.ElapsedGameTime.TotalSeconds)
               ); // TODO    used TargetElapsed time for see if needed goes it cause garbage;

            ShadowFactory.Engine.ActiveGameCode.Update(this, e);
        }



        const int AverageOverFrames = 100;

        protected override void Draw(GameTime gameTime)
        {



            World.Instance.RenderDrawPerSecond = (int) (1000f /(float)gameTime.ElapsedGameTime.TotalMilliseconds);
            AvgTimer.RecFrameSkip = AverageOverFrames;

            using (var  x = new AvgTimer())
            {
                try
                {

                    if (Engine.IsBackgroundThread)
                    {

                        if (!PhysicsThread.Lockless)
                        {
                            if (SimWorld.Instance?.PhysicsThread?.WaitForAccess(200) == false)
                            {
                                Debug.WriteLine("timeout getting draw lock");//just skip a frame.. todo adjust waitl for thelast  physics update interval
                                x.UpdateAndRecord(true);  //this should give us a hint
                                return;
                            };
                        }

                    }

                    GraphicsDevice.Clear(Color.Transparent );


                    if (Level.Instance == null)
                        return;

                    DebugView physicsView = GameStart.PhysicsView;

                    if (physicsView == null)
                        return;

                    physicsView.DefaultTextureBlendState = BlendState.NonPremultiplied;


                    physicsView.VectorTransparency = dlgSettings.IsVisible ? 0.25f : 1f;

                    physicsView.TextTransparency = dlgSettings.IsVisible ? 0.25f : 1f;

                    physicsView.DefaultBlendState = dlgSettings.IsVisible ? BlendState.NonPremultiplied : BlendState.AlphaBlend;

                    //    physicsView.DefaultTextureBlendState = dlgSettings.IsVisible ? BlendState.NonPremultiplied : BlendState.Opaque;


                    //TODO use a better pattern, when world is changed set it once, not every draw
                    physicsView.SetWorld(ShadowFactory.Engine.World.Physics);


                    string val = "2DWORLD";

                    if (ShowFPSTitle)
                    {
                        val += "    Sim FPS:" + World.Instance.UpdatePerSecond.ToString();
                        this.Window.Title = val;
                    }
                    //TODO MG_GRAPHICS  weh viewbox xform fixed see if one of the scales is what we want and simplify this

                    float scale = Graphics.Instance.CTransform.PixelsPerMeterDiag();

                    //note THIS IS NOT THE SAME AS TEH ZOOM LEVEL SCALE
                    physicsView.PixelsPerMeter = scale;//X AND Y SHOUD BE SAME WHEN ASPECT BOXING FIXED

                    if (!PhysicsThread.Lockless)//TODO finish this, must draw in both case
                    {
                        EntityHelper.DrawAllEntities(Level.Instance, gameTime.ElapsedGameTime.TotalSeconds);

                    }
                    else
                    {
                        Presentation.Instance.LastFrame.Spirits.ForEach(sp => sp.Draw(gameTime.ElapsedGameTime.TotalSeconds));
                   }
                  


                    //TODO a producer consumer might prevent physics from doing a cycle if draw is almost done.
                    Matrix View = Graphics.Instance.Presentation.Camera.Transform.View;  //or View
                    Matrix Projection = Graphics.Instance.Presentation.Camera.Transform.Projection;

                    //   GameStart.PhysicsView?.AppendFlags(FarseerPhysics.Diagnostics.DebugViewFlags.PerformanceGraph);

                    //TODO try to blend using HDR and float, will improve the gas view
                    physicsView.ClearFlags();


                    if (CoreGame.ShowDebugInfo)
                    {
                        physicsView.AppendFlags(DebugViewFlags.DebugPanel);

                        if (CoreGame.FeatureSet.ProxyView)
                        {
                            physicsView.AppendFlags(DebugViewFlags.AABB);
                        }

                    }



#if !TWOPASSDRAW
                     
                    
                    physicsView.AppendFlags(DebugViewFlags.Body);
                    physicsView.AppendFlags(DebugViewFlags.Edges);

                    //   physicsView.AppendFlags(DebugViewFlags.DrawInvisible);
                    physicsView.AppendFlags(DebugViewFlags.BodyMarks);//note some body marks mihgt
                    //need be draw after fill or befroe
                     if (CoreGame.ShowDebugInfo)
                     {
                        physicsView.AppendFlags(DebugViewFlags.DebugPanel);
                     }

                      physicsView.AppendFlags(DebugViewFlags.FillStatic);//for  spirits using static to pack and sleep
                      physicsView.AppendFlags(DebugViewFlags.Fill);
                      physicsView.AppendFlags(DebugViewFlags.EntityEmitters);



                    if (Presentation.UseViews)
                    {
                        //   using (new TimeExec("RenderViews"))  //todo dont access physics direct , update body refs
                        //   {
                        //     GameStart.PhysicsView?.RenderViews(Presentation.Instance.DisplayList, ref Projection, ref View);
                        // }
                    }
                    else
                    {

#if CLIPTEST

                        List<Body> bodies = PhysicsThread.Lockless ? Presentation.Instance.LastFrame.Bodies : World.Instance.BodyList;

                         if (!Level.Instance.DoneOnceOnLevelLoad)
                        { 
                            //ISSUE might wait for 2dn frme on this.. make sure its a clone..
                            Rasterizer.CaptureTextureForBodies(GraphicsDevice, bodies, physicsView);
                            Level.Instance.DoneOnceOnLevelLoad = true;
                       }

                        //  physicsView.ClearFlags();




                        Core.Game.MG.Graphics.Presentation.Instance.UpdateViews(physicsView, GraphicsDevice);

                        physicsView.Flags |= DebugViewFlags.TextureMap;
                        physicsView.RenderDebugData(Projection, View, bodies);
#endif

                    }



#if !CLIPTEST
                    //draw theese contents after filling

                    //  physicsView.AppendFlags(DebugViewFlags.EntityEmitters);
                    if (PhysicsThread.Lockless)
                    {
                        physicsView.RenderDebugData(Projection, View, Presentation.Instance.LastFrame.Bodies);
                        physicsView.RenderThumbnailImages(ref Projection, ref View, Presentation.Instance.LastFrame.Bodies);
                        
                    }
                    else
                    {
                        physicsView.RenderDebugData(Projection, View); // now draw the emitted items on top
                        physicsView.RenderThumbnailImages(ref Projection, ref View, World.Instance.BodyList);
                    }
#endif

                    physicsView.ClearFlags();
                    physicsView.AppendFlags(DebugViewFlags.MsgStrings);
                    DrawAppInfoText(physicsView);

                    physicsView.RenderDebugData(Projection, View);



#endif


#if TWOPASSDRAW

                    //TODO make sure 2 pass draw doesnt draw particles twice

                   //first draw edges and behidn marks
                    physicsView.AppendFlags(DebugViewFlags.BodyMarks);
                    physicsView.AppendFlags(DebugViewFlags.Edges);//TODO lockless must be copy the shape for concave? on cut..
                    physicsView.AppendFlags(DebugViewFlags.Body);



                    if (PhysicsThread.Lockless)
                    {
                        physicsView.RenderDebugData(Projection, View, Presentation.Instance.LastFrame.Bodies);
                    }
                    else
                    {
                        physicsView.RenderDebugData(Projection, View);
                    }


                    //then fills.
                    //now draw the jointed systems , let them  file  a black background to cover any overlapped areas in joints
                    physicsView.ClearFlags();

                    physicsView.AppendFlags(DebugViewFlags.BodyMarks);
                    physicsView.AppendFlags(DebugViewFlags.FillStatic);//for  spirits using static to pack and sleep
                    physicsView.AppendFlags(DebugViewFlags.Fill);//TODO lockless must be copy the shape for concave? on cut..
                    physicsView.AppendFlags(DebugViewFlags.Body);



                    if (PhysicsThread.Lockless)
                    {
                        physicsView.RenderDebugData(Projection, View, Presentation.Instance.LastFrame.Bodies);
                    }
                    else
                    {
                        physicsView.RenderDebugData(Projection, View);
                    }




                    /*     Level.Instance.Entities.ForEach
                          (x =>
                          {
                              if (x is Spirit &&
                                  !((Spirit)x).MainBody.IsInfoFlagged(FarseerPhysics.Dynamics.BodyInfo.Cloud)
                                   && ((Spirit)x).Joints.Count > 0)
                              {  
                                  physicsView.RenderDebugData(Projection, View, ((Spirit)x).Bodies);
                              };
                          }
                         );**/


                    //draw the contents after filling
                    physicsView.ClearFlags();
                    physicsView.AppendFlags(DebugViewFlags.EntityEmitters);
                    if (PhysicsThread.Lockless)
                    {
                        physicsView.RenderDebugData(Projection, View, Presentation.Instance.LastFrame.Bodies);
                       physicsView.RenderThumbnailImages(ref Projection, ref View, Presentation.Instance.LastFrame.Bodies);
;                    }
                    else
                    {
                        physicsView.RenderDebugData(Projection, View); // now draw the emitted items on top
                        physicsView.RenderThumbnailImages(ref Projection, ref View, World.Instance.BodyList);


                    }

                    physicsView.ClearFlags();
                    physicsView.AppendFlags(DebugViewFlags.MsgStrings);
                    DrawAppInfoText(physicsView);

                    physicsView.RenderDebugData(Projection, View);





#endif

                        DrawRays(physicsView, View, Projection);

                    base.Draw(gameTime);


                    //draw UI last opaque over dimmed
                    dlgSettings?.Draw(gameTime);
                    btnSettings.Draw(gameTime);
                    dlgConsole.Draw(gameTime);



                }

                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                finally
                {
                    if (!PhysicsThread.Lockless) {
                        if (Engine.IsBackgroundThread)
                        {
                            SimWorld.Instance?.PhysicsThread?.FinishedAccess();
                        }
                    }

                }
            } //timer

        }

   

        private static void DrawRays(DebugView physicsView, Matrix View, Matrix Projection)
        {
            physicsView.BeginCustomDraw(Projection, View);


            if (!PhysicsThread.Lockless)  //todo
            {


                foreach (var ray in SimWorld.Instance.RayViews)
                {
                    Color rayColorView = ray.Value.Color * physicsView.VectorTransparency;
                    physicsView.DrawSegment(ray.Value.X1, ray.Value.X2, rayColorView);
                }

            }
            else
            {
                foreach (var ray in Presentation.Instance.LastFrame.Rays)
                {
                    Color rayColorView = ray.Color * physicsView.VectorTransparency;
                    physicsView.DrawSegment(ray.X1, ray.X2, rayColorView);
                }

            }

            physicsView.EndCustomDraw();
        }

        static string versionStr = null;
        private static void DrawAppInfoText(DebugView physicsView)
        {

       

            int textHeight = (int)(16f * physicsView.TextScale);
            int textY = textHeight;


           if (versionStr == null)
                versionStr =  GetCoreVersion();


            int indent = 170;
            physicsView.DrawString(indent, 0, "2DWORLD   " + versionStr + "cores "+ Environment.ProcessorCount, 1.5f, Color.Red);

            textY += 18;

            if (Level.Instance == null)
                return;

            if ( string.IsNullOrEmpty(Level.Instance.Title))
            {
                Level.Instance.Title = Level.Instance.Filename;
            }

            if (LooseFiles)
            {
                physicsView.DrawString(indent, textY, Level.Instance.FilePath, 1, Color.Yellow);
                textY += textHeight;
            }

            physicsView.DrawString(indent, textY, Level.Instance.Title + " rev" + Level.Instance.Version, 1f, Color.Yellow);
        }

    
        

        public static bool ReloadLastSaved()
        {
            if (!CoreGame.LooseFiles)
                return false;
                
            using (new SimPauser())
            {
                object workingFileName = null;

                if (!DataStore.Instance.TryGetValue(Serialization.WorkingLevelNameKey, out workingFileName))
                    return false;

                string filename = (string)workingFileName;

                LevelGameCodeBase gamecode = ShadowFactory.Engine.ActiveGameCode as LevelGameCodeBase;

                return gamecode.LoadLevel(filename);

            }
        }

        void OnGraphicsDeviceReset()
        {

            Graphics.Instance.Presentation.Camera.Transform.ResetViewportProjection();
            
            if (dlgSettings == null)
                return;

            dlgSettings.UpdateBounds();
            dlgConsole.UpdateBounds();

        }


        protected override void OnExiting(object sender, EventArgs args)
        {

            // SimWorld.Instance.ShutDown();  //TODO this waits forever , could be cause we aren't using the cyle Semaphore in game its too weird in tool
            SimWorld.Instance.PhysicsThread.Exit(); // workaround, tell it to exist, wait for  a lock 


            ShadowFactory.Engine.World.PhysicsThread.WaitForAccess(100);
            ShadowFactory.Engine.World.PhysicsThread.FinishedAccess();

            base.OnExiting(sender, args);
        }


    }
}
