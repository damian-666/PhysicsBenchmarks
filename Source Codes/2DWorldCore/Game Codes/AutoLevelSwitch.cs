#define ALLOWBACKUPLEVEL
//#define VISUALMARGINTEST    // to display level margin with green and blue line  TODO MG_GRAPHICS doesnt draw anything, cleanou//

namespace _2DWorldCore
{

    using System;
    using System.Windows.Input;
    using System.Collections.Generic;
    using System.Text;
    using System.Windows;
    using System.Diagnostics;

    using Farseer.Xna.Framework;
    using FarseerPhysics.Collision;
    using FarseerPhysics.Dynamics;
    using FarseerPhysics.Dynamics.Particles;

    using Core.Data.Entity;
    using Core.Data;

    using System.Globalization;
    using System.Threading;

    using System.Threading.Tasks;
    using Microsoft.Xna.Framework.Input;
    using Core.Game.MG.Simulation;
    using Core.Game.MG;

    using Core.Data.Geometry;
    using System.IO;
    using Storage;
    using Core.Game.MG.Plugins;
    using MGCore;
    using Core.Game.MG.Graphics;
    using _2DWorldCore.UI;

    public class AutoLevelSwitch : LevelGameCodeBase
    {

        public static string MessageDemoLevelLimit = "Further levels can be tried in the Full Version, now only $2 for a Playable Preorder Single User license, unlimited token";  //todo LOCALIZE XLATE

        private readonly GameUI _gameUI;
        /// <summary>
        /// Id of currently loaded level X, starts at 0
        /// </summary>
        private int _curLevelIDx;
        /// <summary>
        /// Id of currently loaded level depth,starts at 0
        /// </summary>
        private int _curLevelIDy;

        /// <summary>
        /// Current level boundary, cached because only use horizontal distance, 
        /// shouldn't changed much after a level loaded.
        /// </summary>
        private AABB _cachedLevelAabb;

        /// <summary>
        /// Cached AABB for each TravellerMargin side. Left, top, right, bottom.
        /// This is used to speed up traveler object inclusion check.   This AABB is NOT used to trigger level switch.
        /// </summary>
        protected AABB[] _cachedTravellerMarginAABB;

        /// <summary>
        /// Just an index to indicate which margin aabb should be used when switching to next level.
        /// </summary>
        protected LevelExit _exitMarginIdx;



        protected AmbienceController _ambience;

        protected int _numLevels = 15;  // will show TO BE CONTINUED if not reached the END of game Episode 1. setting to 15 for now

        //TODO code clean.. add travelers here.. clean method names for disposing, etc

        /// <summary>
        /// Initial level id to load when starting this game code. default is 1,1
        /// </summary>
        public int InitialLevelIDx { get; set; }
        public int InitialLevelIDy { get; set; }



         public AutoLevelSwitch(GameUI ui) : base()
        {
            _gameUI = ui;
           
            InitialLevelIDx = -1;    // default initial level
            InitialLevelIDy = 1;
        }



        #region Game event handler
        protected override void OnBeginCode()
        {


        
            AllowMouseDragAndSelect = true;

            //  world.PhysicsUpdateInterval = 1 / 60f;//fps  from 50 - 80 work is a good setting,.. 80 very smooth accurate, but needs fast PC
            //NOTE more all the work was don with a fixed step of 1/60.   but, with a simple gain in setBias, based on tuning until walk works
            //a linear parameter was extracted from 2 data points.  60, 80,  100 and , and the bias .   .3   .15 , and 0.11 works for 100.. and 45 work ok as well , just very rough.
            //a small time step makes the game less reliant on CCD , sell wild, more predictable, and visible smoother.. more joint iterations do
            // not seem needed.. since we are effectively getting 2x as many..  seems like Breakpoints aren't affected.. since its on every frame. 

            InitializeCamera();

            // we use ambience controller to control background color
            _ambience = new AmbienceController();

            // load initial level 
            // load what working file saved in shared datastore

            if (CoreGame.OnBeginGameCode != null)
            {
                CoreGame.OnBeginGameCode.Invoke();
            }

            if (Level.Instance == null)
            {
                LoadRootLevel();//this is a planet that has hyperlinks to all ground levels   
            }


        }


        public override void OnPreUpdatePhysics()
        {
            base.OnPreUpdatePhysics();

            // update background
            //   _ambience.Update(simworld.PhysicsThread.ElapsedTime, simworld.PlanetRotationPeriod);

            CheckToChangeLevelWhenActiveSpiritNearEdge();//would make sense to put this in physics thread, after physics update.   note, I  did try it in _simworld_OnBackgroundUpdate, threading issues happened
                                                         //CheckAndRecordEarlyTravelersPassingBottomEdge();//would make sense to put this in physics thread, after physics update.

        }

        // this method always run regardless of simulation pause
        protected override void OnUpdate(object sender, TickEventArgs e)
        {


            if (Input.DualTouchStick != null)
            {
                Input.DualTouchStick.IsEnabled = !AtHomeScreen;
            }
            //TODOD jsut makc a methods donest have to check null an a allactive anyactive prop..
            //check teh pressed state if it works... tto tuck mabye..  fix teh stuck hand thing.. clear any click maybe on activate.. if not other touch or fix proper

            //this are etests for now.. but maek it clean
            //distable teh spirit activate via debug. switch  useful fro testing but not play..need a wahy to fire non gun or rocket gun or 
            //activate.. double tap broke.mabyhe make touch go to mouse.but test w track adn touch on laptop..
            //do virt buttoms for pc players.. analog rigth sticl maybe..  analog jump dir..maybe.. double squat test..
            // fast run maybe.. try again.. camera must work tooo.. pinch during run maybe.. right side zoom maybe   
            // zooom for laptop..key...see how track pad zoom and mouse wheel suppose to wrok..in mac and pc..

            //for this to work need wind via shader tho... adn partices too slow..
            //just a demo fof now tho..  try with mouse only on laptop

            if (!(Input.DualTouchStick != null && Input.DualTouchStick.IsActive()))
            {
                if (Input.Touch.CurrentTouches.Count == 4 && Input.Touch.PreviousTouches.Count < 4)
                {
                    Debug.WriteLine(" 4 touch");

                    if (CoreGame.Instance.IsSettingsActive)
                    {
                        CoreGame.Instance.HideSettings();
                    }
                    else
                    {
                        NavigateBack(true);
                    }

                    return;
                }
                else if (Input.Touch.CurrentTouches.Count == 3 && Input.Touch.PreviousTouches.Count < 3)

                {
                    btnResetLevel_Click(this);
                    return;
                }
            }

            base.OnUpdate(sender, e);  //very import this input update needs be called first or we get multiple pressed
                                       // update energy meter
                                       //   UpdateEnergyBar(_gameUI.BarActiveSpiritEnergy);
                                       //  UpdateInfoPanel();


            //pressed escape.. pause or go back
            if (Input.IsKeyPressed(Keys.Escape) || Input.GamePads[0].IsButtonReleased(Buttons.Back))
            {


                if (CoreGame.Instance.DlgSettings.IsVisible)
                {
                    CoreGame.Instance.HideSettings();
                    return;
                }

               string bkTarget = AtHomeScreen ? "Exit Program" : "return to Interzone";


                //toto 3 finger pause for driod? it will pause when inactive anyways i thinkk
                if (!CoreGame.IsPaused)
                {

                  //  string instructions = CoreGame.HasKeyboard ? 
                    CoreGame.IsPaused = true;
                    ConsoleDlg.Instance.WriteLine("Simulation Paused. Press a game key to resume.  Press Esc again to " + bkTarget);
                }
                else
                {
                    NavigateBack(true);
                };
            }else  //we pressed another key.. maybe resume
            if (CoreGame.IsPaused && InputCommand.Instance.KeyState != 0)
            {
                CoreGame.IsPaused = false;
            }
         


        }

        protected override void OnKeyDown(object sender, Keys e)
        {

            switch (e)
            {
                case Keys.H:
                    //   if (_isShiftDown)  //to easy to accidentally hit H when right hand returns from mouse to keyboard ( using WASF) 
                    {
                        //   _gameUI.HelpBox.ToggleVisibility();
                    }
                    break;

                case Keys.NumPad9:
                    // for now Shift+9  will toggle window limit on/off
                    if (isShiftDown)
                    {
                        //   camera.Transform.LimitEnabled = !camera.Transform.LimitEnabled;

                        // update zoom immediately, if currently zoomed out far not limited by boundary
                        //camera.Transform.UpdateZoomLevel();  // didn't work if zoom level not changed.
                        //   camera.Transform.Zoom = camera.Transform.Zoom;    // just to trigger auto clamping
                        ////TODO maybe fix later gives exception .. must be user initiated..
                        //btnLoadLevel_Click(sender, e);
                    }
                    break;


                case Keys.F10:
                    {
                        SimWorld.Instance.PhysicsThread.IsRunning = !SimWorld.Instance.PhysicsThread.IsRunning;
                    }
                    break;

                case Keys.F2:
                    {
                        AdvanceLevel(Device.GetDir(!isShiftDown), 0);
                    }
                    break;

                case Keys.F3:
                    {

                        AdvanceLevel(0, Device.GetDir(!isShiftDown));

                    }
                    break; ;

                case Keys.F5:    // shift + F5 = reset level
                    if (isShiftDown)
                    {
                        btnResetLevel_Click(this);
                    }
                    break;


#if FILEOPENTEST
                    // NOTE: This didn't work.  Cause "Reentrancy detected" error dialog. SecurityException, dialog must be user initiated.
                    // Error on both Debug and Release build of Game.  Tested using other combination (shift btn, numeric btn), still the same result.
                    case Keys.F:     // ctrl + F = load level dialog.  

                    if (isCntrlDown)
                        {
                              ShowFileDialogUI = true;//call it later from UI thread
                        }
                        break;
#endif

            }

            // update game key state

            base.OnKeyDown(sender, e);
        }


        protected override void OnKeyUp(object sender, Keys e)
        {

            base.OnKeyUp(sender, e);
        }



        override protected void OnPointerUp(Vector2 pos)
        {

            // do mouse select when mouse previously hit fixture.
            // set active spirit here on mouse up event, so it won't get dragged accidentally
            // when camera is auto panning and spirit is still pinned by drag joint.
//TODO this seem wrong update camera to letterbox, simplify



            if (IsMousePanActive == false)//&& mousePickJoint != null)
            {
                Vector2 worldPos = Presentation.Instance.Camera.Transform.ViewportToWorld(pos);


                Body bodyHIt = ShadowFactory.Engine.World.HitTestBody(worldPos);
                if (bodyHIt != null)
                {

                    BodyEmitter hyperlink = bodyHIt.Emitters.Find(x => x is BodyEmitter && ((BodyEmitter)x).SpiritResource != null
                                                                                                && ((BodyEmitter)x).SpiritResource.EndsWith("wyg")) as BodyEmitter;

                    if (hyperlink != null)
                    {

                        string path;
                        if (CoreGame.LooseFiles == true)
                        {
                            path = Serialization.GetMediaLevelPath();
                            path = System.IO.Path.Combine(path, hyperlink.SpiritResource);
                        }
                        else
                            path = hyperlink.SpiritResource;


                        this.LoadLevel(path);
                        return;
                    }


                    Spirit selectedSpirit = selection.GetCreature(bodyHIt, worldPos, level);




                    if (selectedSpirit != null)
                    {
                        ActiveSpirit = selectedSpirit;
                        // NOTE: no need, each loop now clear & add followed object
                        //_camera.FollowSingleEntity(ActiveSpirit);   // update tracking camera target
                    }

                    //TODO pick up the thing.. exectue action button i guess
                    // only change spirit focus if it selected spirit not null and have plugin


                }

                base.OnPointerUp(pos);

            }

        }


        bool AdvanceLevel(int inc, int yinc)
        {

            if (!ChangeToLevel(_curLevelIDx + inc, _curLevelIDy + yinc, true))
            {
                Debug.WriteLine("change to level failed " + _curLevelIDx + inc + " " + _curLevelIDy + yinc);
                return false;
            }

            return true;


        }

        //TODO check this load leaks.. and  bug where creature seems to start dead in last location
        protected override void OnTerminate()
        {
            DisposeCurrentLevel();
        }


        #endregion



        #region Methods

        private void InitializeCamera()
        {
            // initial view setting
            //    Graphics.Instance.Presentation.Camera.SetPosition(  )

            camera.IsCameraTrackingEnabled = true;
            //camera.IgnoreInputWTracking = true;

            camera.IsAutoZoomWTracking = true;

            //   camera.TrackingPanSpeed = 0.25f;
            camera.TrackWindowFactor = 3;
            camera.IsKeepObjectAABBFixed = false;

            // testing lazy tracking camera
            camera.IsLazyTracking = true;
        }

        protected string GetCurrentLevelName()
        {
            return Level.GetLevelNameFromTileCoordinates(_curLevelIDx, _curLevelIDy);
        }


        string levelpassedKey = "RandDLevelPass3.mp3"; //RanDLevelPass2.mp3  //TODO make good one.. .. different for going underground..

        private void CheckToChangeLevelWhenActiveSpiritNearEdge()
        {
            if (!_cachedLevelAabb.IsValid())
                return;

            if (_cachedLevelAabb.Width < 1.0f || _cachedLevelAabb.Height < 1.0f)// probably a test file , dont do this
                return;

            Spirit travelerSpirit = ActiveSpirit;

            if (travelerSpirit == null || travelerSpirit.World == null)
                return;

            // NOTE: ensure travelerSpirit AABB is updated proper or else it will cause incorrect edge detection.
            // when level is loaded from LevelSelect screen, ActiveSpirit.AABB usually still not updated.
            travelerSpirit.UpdateAABB();

            //TODO localize
            string messageLevelToBeContinued = "To be continued.  Subscribe for updates by pressing Like on our Facebook at facebook/puppetarmyfaction, or follow us on Twitter @PAF_Kontrol\n. ";
            //TODO make a UI dialog with linke  Http:\\www.twitter.com/paf_control  ( futre: mail list subscribe thing?)  not many  uses twitter

            string msgAllLevelsPassed = _gameUI.IsTrial ? MessageDemoLevelLimit : messageLevelToBeContinued;

            //Position returned by spirit  is MainBody CM.. 
            //NOTE: with current margin, there might be an overlap on corner between left-right and top-bottom margin.
            // currently left-right take precedence over top-bottom. will be fixed later only if really necessary. 
            // left edge, just travel to level with higher id
            if (travelerSpirit.AABB.UpperBound.X < _cachedLevelAabb.LowerBound.X + Level.Instance.SwitchMargin.Left)
            {
                if (_curLevelIDx < _numLevels)
                {
                    _exitMarginIdx = LevelExit.Left;

                    int yOffset = 0;

                    //TODO  mark this in level, rid of this HACK
                    if (_curLevelIDy == 2 && _curLevelIDx == 3)//NOTE special logic for tiles not to scale
                    {
                        yOffset = -1;  //diagonal move on this tunnel  from mine to Cliff ... level is tall enough to reach ground.
                    }


                    AudioManager.Instance.PlaySound(levelpassedKey);


                    if (!ChangeToLevel(_curLevelIDx + 1, _curLevelIDy + yOffset))
                    {

                        Debug.WriteLine(msgAllLevelsPassed);
                        //  MainPage.MessageBoxOK(msgAllLevelsPassed);
                    }
                }
                else  // finished levels, for demo  restart the first, for game... put end credits or whatever.
                {
                    //   MainPage.MessageBoxOK("The End.  Thanks for playing.  Check back later for more adventures" );
                    //TODO  end game credits..
                    // level = LoadLevel_curLevelID(Serialization.LoadLevelFromAppResource("endcredits.wyg"));                     
                }
            }

#if ALLOWBACKUPLEVEL
            // right edge, just travel to level with lower id  
            // confirmed issue when there are head winds..   going back  to right side can happen just after exiting on left.
            // TODO fix this.. so that there is a buffer.. and see traveller is placed at left side or old level at least..
            else if (travelerSpirit.AABB.LowerBound.X > _cachedLevelAabb.UpperBound.X - Level.Instance.SwitchMargin.Right)
            {
                if (_curLevelIDx > 0)
                {
                    _exitMarginIdx = LevelExit.Right;
                    if (!ChangeToLevel(_curLevelIDx - 1, _curLevelIDy))
                    {
                        Debug.WriteLine(messageLevelToBeContinued);
                        //TODO MG_GRAPHICS MessageBoxOK(messageLevelToBeContinued);
                    }
                }
            }
#endif
            // bottom edge,  travel to level with higher  Y..  y goes b, c, d..    so level 2b would go under level 2.  
            else if (travelerSpirit.AABB.LowerBound.Y > _cachedLevelAabb.UpperBound.Y - Level.Instance.SwitchMargin.Bottom)
            {
                _exitMarginIdx = LevelExit.Bottom;

                AudioManager.Instance.PlaySound(levelpassedKey);

                if (!ChangeToLevel(_curLevelIDx, _curLevelIDy + 1))
                {
                    Debug.WriteLine(msgAllLevelsPassed);
                    //TODO MG_GRAPHICS  MainPage.MessageBoxOK(msgAllLevelsPassed);
                }
            }
            // top edge,  travel to level with lower Y..  
            else if ((Level.Instance.LevelNumber == 6 || Level.Instance.LevelDepth > 1) &&
                (travelerSpirit.AABB.UpperBound.Y < _cachedLevelAabb.LowerBound.Y + Level.Instance.SwitchMargin.Top))
            {
                _exitMarginIdx = LevelExit.Top;

                //TODO just make this fall gracefully now it stops
                if (!ChangeToLevel(_curLevelIDx, _curLevelIDy - 1))
                {
                    Debug.WriteLine("level pass top failed");
                    //dont don anything if level not present
                }
            }

        }




        //TODO speedometer
        /*
        bool isMetric = false;
        private void UpdateInfoPanel()
        {
            if (ActiveSpirit != null)
            {
                //TODO ye this cannot be polled reliably.. should be compared with plugins value... 
                //plugin can update an atomic value in the spirit?   cna we relay on the plugin value ?   make sure its not critical to be correct.

                // to update any state for achievement next just try subscribe to the 
                //   ActiveSpirit.OnAvgSpeedXUpdat
                // copied from yndrdplugin                
                _sb.Clear();


                double speed = 0;
   
                if (RegionInfo.CurrentRegion.IsMetric)
                {
                    speed = ActiveSpirit.AverageMainBodySpeed / MathHelper.KmhToMs;
                    isMetric = true;

                }
                else
                {
                    speed = ActiveSpirit.AverageMainBodySpeed / MathHelper.KmhToMs * MathHelper.KphToMph; ;


                    if (isMetric == true)
                    {
                      //  _gameUI.TxtSpeedUnits.Text = " mph";
                    }

                    isMetric = false;
                }
                _sb.Append(speed.ToString("N0"));
             //   _gameUI.TxtAverageSpeed.Text = _sb.ToString();
            }

       
        }*/

        private bool ChangeToLevel(int levelIDx, int levelIDy)
        {
            return ChangeToLevel(levelIDx, levelIDy, false);
        }
        /// <summary>
        /// Load level based on level id, and depth
        /// </summary>
        /// <param name="levelIDx">new level ID X, -N to N</param>
        /// <param name="levelIDy">new level ID depth 0 to N</param>
        /// <param name="start">starting at this level, no travellers</param>
        /// <returns>true if level found and deserialized</returns>
        private bool ChangeToLevel(int levelIDx, int levelIDy, bool start)
        {
            using (new SimPauser())
            {
                if (!start)
                {    // save travelers from current level if crossing levels by passing margin
                    SaveTravelersBeforeCurrentLevelDisposed(levelIDx, levelIDy);
                }
                else
                {
                    _gameUI.CheckToStartBackgroundMusic();  //TODO MG_GRAPHICS

                    travelers.Clear();
                }
                // dispose first, serialize will automatically load body in physics
                DisposeCurrentLevel();
            }

            return LoadLevel(levelIDx, levelIDy);
        }




        //TODO rebuild as hyperlink system, physics hyperlink object..
        public bool LoadLevel(int levelIDx, int levelIDy)
        {

            if (Body.NotCreateFixtureOnDeserialize)
            {
                Debug.WriteLine("unexpected state Body.NotCreateFixtureOnDeserialize true");
                Body.NotCreateFixtureOnDeserialize = false;
            }

            GameStart.PhysicsView?.ClearMsgStrings();

            string levelName = Level.GetLevelNameFromTileCoordinates(levelIDx, levelIDy);

            if (levelName == null)
            {
                levelName = rootLevelName;
            }

            LoadEmbeddedLevel(levelName, levelIDx, levelIDy);


            return true;
        }


        private void LoadRootLevel()
        {

            if (Body.NotCreateFixtureOnDeserialize)
            {
                Debug.WriteLine("unexpected state Body.NotCreateFixtureOnDeserialize true");
                Body.NotCreateFixtureOnDeserialize = false;
            }

            GameStart.PhysicsView?.ClearMsgStrings();
            LoadEmbeddedLevel(rootLevelName);
            return;

        }


        public override bool LoadLevel(string filename)
        {

            if (Body.NotCreateFixtureOnDeserialize)
            {
                Debug.WriteLine("unexpected stat Body.NotCreateFixtureOnDeserialize true");
                Body.NotCreateFixtureOnDeserialize = false;
            }

            FileInfo path = new FileInfo(filename);
            if (path.Directory.Exists && CoreGame.LooseFiles)
            {
                base.LoadLevel(filename);
                SetupSwitchLevel(path.Name);
            }
            else
            {
                LoadEmbeddedLevel(filename);
            }

            if( !AtHomeScreen)
                _gameUI.CheckToStartBackgroundMusic();  //TODO MG_GRAPHICS


            return true;
        }


        private void LoadEmbeddedLevel(string levelName, int levelIDx = -1, int levelIDy = -1)
        {
            // dispose current level first
            DisposeCurrentLevel();

            using (new SimPauser())
            {
                try
                {
                    level = Storage.Serialization.LoadDataFromAppResource<Level>(levelName);
                }
                catch (Exception exc)
                {
                    Debug.WriteLine("Error in LoadDataFromAppResource " + exc.Message);

                }

                SetupSwitchLevel(levelName, levelIDx, levelIDy);
            }

        }


      

        public void NavigateBack(bool mayExit = false)
        {
            if ( AtHomeScreen)
            {
                if (mayExit)
                  { CoreGame.Instance.Exit();     }
                
                return;
            }
            else
            {
                LoadRootLevel();
            }
        }


        void SetupSwitchLevel(string levelName, int levelIDx = -1, int levelIDy = -1)
        {
            if (level == null)
            {
                level = CreateDefaultLevel();  //for development / debug, load something simple, w space for error msgs
                                               //revert to last level, good for browsing but we wil go to hyperlink / interzone / planet view level
                GameStart.PhysicsView.DrawMsgString("level " + levelIDx + " " + levelIDy + " Failed to Load", Microsoft.Xna.Framework.Color.Red);
                levelName = "Default";
            }
            else
            {

                level.Filename = levelName;

                if (levelIDx == -1)
                {
                    Level.GetTileCoordinatesFromLevelName(levelName, out levelIDx, out levelIDy);
                }

                level.PrevLevelNumber = _curLevelIDx;
                level.PrevLevelDepth = _curLevelIDy;

                level.LevelNumber = levelIDx;
                level.LevelDepth = levelIDy;

                _curLevelIDx = levelIDx;  //TODO CLEAN OUT  duplicate state, why not use level its owned
                _curLevelIDy = levelIDy;
            }

            Debug.WriteLine("loading level to sim:" + levelName);
            LoadLevel(level);
            Debug.WriteLine("loaded level  to sim:" + levelName);
            // if success loading into simulation, _level == level from this point onward
            // set camera margin. camera WindowLimit is set in LoadLevel(), we set it again here, smaller , so that margin area is out of view.
            // negative expand so it will reduce by margin.
            // to clearly see margin line, dont set window limit 

            //TODO something about switch margins     make a hyperlink?

#if !VISUALMARGINTEST

            ZoomInsideSwitchMargins(level);
#endif

            // unlock this level.
            //TODO     Storage.LevelKey.UnlockLevel(levelIDx, levelIDy);
            //     AddHeldItemsFromIso(levelIDx, levelIDy, level);


            if (lastLevelFileInfo == null)
                return;

             if ( level.FilePath != lastLevelFileInfo.FullName)
            {
                lastLevelFileInfo = null;
            }
            //TODO GRAPHICS_MG  _gameUI.UpdateSaveLoadKeys();
        }

        private void ZoomInsideSwitchMargins(Level level)
        {
            AABB bounds = camera.Bounds;

            bounds.Expand(-level.SwitchMargin.Left, -level.SwitchMargin.Top, -level.SwitchMargin.Right, -level.SwitchMargin.Bottom);

            camera.ZoomWindow(bounds);

            camera.ResetLazyTrackingCenter();
        }

#if FUTURE
        //DWI REVIEW .. IS THIS A QUICK JOB.. lets do it.
        private static void AddHeldItemsFromIso(int levelIDx, int levelIDy, Level level)
        {
            // load saved traveler held items from iso storage here ? 
            List<Body> heldBodies = LevelKey.LevelKey.GetTravelersItemFromIso(levelIDx, levelIDy);

            //// if not empty, load held bodies into this level 

            //TODO insert the bodies at absolute position.. after levels are line up using new tool
            //I wrote an issue to do this later..
            foreach (Body b in heldBodies)
            {
                // NOTE: currently still cause minor bugs, when switch level it causes heldbodies added twice in new level.
                // need to only load heldbodies from isostorage if restart level or load level diectly, not traveling from previous level.
                // checking level entities didn't work.
                if (!level.Entities.Contains(b))
                    level.Entities.Add(b);
            }
        }
#endif


        // load, display, and run level
        public override void LoadLevel(Level level, bool reloadActiveSp = false)
        {
            using (new SimPauser())
            {

                base.LoadLevel(level, reloadActiveSp);

                if (base.level != null)
                {
                    // set level aabb
                    _cachedLevelAabb = base.level.CalculateAABB(true);
                    _cachedTravellerMarginAABB = base.level.GetCachedTravellerMargins(_cachedLevelAabb);

#if VISUALMARGINTEST
               //MG_GRAPHICS     Graphics.Instance.Presentation.CreateLevelMarginRectangles(_cachedTravellerMarginAABB, 0.1f);
#endif
                }

            }
        }


        protected override void DisposeCurrentLevel()
        {
            if (level == null)
                return;


            base.DisposeCurrentLevel();

            _curLevelIDx = 0;
            _curLevelIDy = 0;
            _cachedLevelAabb = new AABB();
        }


        /// <summary>
        /// when level is about to switch, save any traveler object inside _travelers, 
        /// _travelers will be re-inserted in new level.
        /// </summary>
        protected void SaveTravelersBeforeCurrentLevelDisposed(int levelIDx, int levelIDy)
        {
            // always clear traveler before fill
            travelers.Clear();  //TODO this should not be a member of level ( NOTE: clearing everything but this in level is weird, should belong to AutolevelSwitch or above it..  

            // save current travelers before level disposed, we want to transfer them to next level.
            // save current travelers only if current level is different with previous,
            // (i.e. not resetting current level).
            if (ActiveSpirit != null && (_curLevelIDx != levelIDx || _curLevelIDy != levelIDy))
            {
                // active spirit first, at index 0
                travelers.Add(ActiveSpirit);

                // add all dynamic non-particle Body in current margin
                AABB margin = _cachedTravellerMarginAABB[(int)_exitMarginIdx];

                // NOTE   _level.Entities does not contain Body parts of spirit which were broken off, like feet , so we do this query from the Geometry tree
                FarseerPhysics.Common.HashSet<Body> bodiesInMargin = ActiveSpirit.GetOtherBodiesAndSpiritsInAABB(margin, out var spiritsInMargin);

                // to avoid duplicates, aux spirit must not be included in travelers, entity added will add the aux spirits
                //TODO HACK CLEANUP, remove duplicates, GENERALIZE, dont care what is auxilliasry or whatever

                FarseerPhysics.Common.HashSet<Spirit> auxSpiritsToRemoveFromTravelers = new FarseerPhysics.Common.HashSet<Spirit>();
                foreach (Spirit spirit in spiritsInMargin)
                {
                    // enumerate aux spirit to avoid being included as travelers
                    spirit.AuxiliarySpirits.ForEach(x => auxSpiritsToRemoveFromTravelers.Add(x));
                }

                foreach (Spirit spirit in spiritsInMargin)
                {
                    if (!auxSpiritsToRemoveFromTravelers.Contains(spirit))
                    {
                        travelers.Add(spirit);     // dont worry about dublicate body in body set and inside spirits.  AddBody in Level will check that..
                    }
                }

                foreach (Body b in bodiesInMargin)
                {
                    if (b.IsStatic || b is Particle)
                        continue;

                    travelers.Add(b);
                }

                // store traveler held items in isostorage here.   TODO FUTURE.. this is so on restart we might have what we brought with us to level
                //try
                //{
                //   Storage.LevelKey.RecordTravelersItemToIso(ActiveSpirit.HeldBodies, levelIDx, levelIDy);
                //}
                //catch( Exception exc)
                //{
                //    MessageBox.Show( 
                //        "error saving to local Storage, check silvight application limits, right click on game to access Silverlight settings and increase limit" +  exc.Message);
                //}

            }
        }


        #endregion



        #region Debug buttons event handler


        // reset current level
        private void btnResetLevel_Click(object sender)
        {


            if (lastLevelFileInfo != null)
            {
                ReloadLastDialogLevel();
            }
            else
            {
                ChangeToLevel(_curLevelIDx, _curLevelIDy, true);
            }


            //   _gameUI.CheckToStartBackgroundMusic();
        }


        public void btnLoadLevel_Click(object sender)
        {
            // note: this will call base.LoadLevel() directly, bypassing LoadLevel() on this class


            if (base.LoadLevelUsingFileDialog())
            {
                _curLevelIDx = -1;

            }

        }


 
        #endregion
    }


    public enum LevelExit
    {
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3,
    }



}
