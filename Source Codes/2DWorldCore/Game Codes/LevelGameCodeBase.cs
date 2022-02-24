//#define ADDBROKENENTITIESTOLEVEL
//#define FLICKLEVELS

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

using System.Linq;

using System.IO;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Collision;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Dynamics.Joints;
using Core.Data.Entity;
using Core.Data;
using Core.Data.Collections;
using Core.Data.Input;


using UndoRedoFramework;

using Core.Game.MG.Plugins;
using Storage;
using System.Threading.Tasks;

using static Core.Trace.TimeExec;
using System.Diagnostics;
using Core.Game.MG.Simulation;
using Core.Game.MG;
using Microsoft.Xna.Framework.Input;

using Core.Game.MG.Drawing;
using Core.Data.Interfaces;
using Core.Data.Plugins;
using MGCore;
using Core.Data.Geometry;
using Core.Game.MG.Graphics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Input.Touch;
using FarseerPhysics.Factories;


namespace _2DWorldCore
{

    //
    public abstract class LevelGameCodeBase : GameCodeBase
    {



        protected Level level;
        protected SpiritSelection selection;

        /// <summary>
        ///File and path Info for loose files
        /// </summary>
        protected FileInfo lastLevelFileInfo;




        /// <summary>
        /// Cross-level objects. These objects will be transfered to next level.
        /// Currently should only consist of one Spirit at index 0, followed by one or more Bodies.
        /// Usually set by derived class.
        /// </summary>
        protected EntityCollection travelers;


        protected int lastFollowedObjectsCount;
        protected float _lastSingleZoom;

        public bool ShowFileDialogUI = false;

        public bool TouchPointerControl = false;// possible but unfinsiehd.. trying virtula touch  joystick


        protected LevelGameCodeBase() : base()
        {


      //      Body.NotCreateFixtureOnDeserialize = true; 
            selection = new SpiritSelection();
            travelers = new EntityCollection();
            simworld.PrepareParticleCallbacks();
        }

        #region Properties

        /// <summary>
        /// Helper property for _level.ActiveSpirit
        /// </summary>
        protected Spirit ActiveSpirit
        {
            get
            {
                if (level == null)
                {
                    return null;
                }
                else
                {
                    return level.ActiveSpirit;
                }
            }
            set
            {
                if (level != null)
                {
                 
                    level.ActiveSpirit = value;
                }
            }
        }

        protected override void OnBeginCode()
        {
            Input.Touch.EnableTouchSupport();//wil only succeed if its available
        }




        protected override void OnUpdate(object sender, TickEventArgs e)
        {
            HandleTouchInput();

            base.OnUpdate(sender, e);
         
        }


        float start_pinch_dist = 0;
        protected void HandleTouchInput()
        {


  
            if (Input.DualTouchStick == null || !Input.DualTouchStick.IsActive())
            {
                if (Input.Touch.CurrentTouches.Count == 1 && Input.Touch.PreviousTouches.Count == 0)
                {
                    var pos = Input.Touch.CurrentTouches[0].Position;

                    OnMouseLeftButtonDown(null, pos.ToVector2());
                    //   OnPointerDown(pos);
                    //maybe should not do input on bk thread in droid..  we might broken wiht with virutal joystick.. or wireing mouse to plugin...

                }


                if (Input.Touch.CurrentTouches.Count == 0 && Input.Touch.PreviousTouches.Count == 1)
                {

                    var pos = Input.Touch.PreviousTouches[0].Position;

                    Debug.WriteLine("OnPointerUp");

                    OnMouseLeftButtonUp(null, pos.ToVector2());
                    //   OnPointerUp(pos.ToVector2());

                }

                HandlePinch(); //in DirectX touch device we get mousewheel on pinch zooms, even if 	TouchPanel.EnableMouseGestures = false; so not needed


            }

        }



        const float deltaTouchFactor = 1f / 60f;

        const float distTouchFactor = 1f / 240f;

        const float touchToWheelAdjument = 60;

        //todo maybe move this to GameCodeBase

        public bool IsPinching = false;
        protected float pinchInitialDistance = 0;


        protected string rootLevelName = "HomePlanet.wyg";

        public bool AtHomeScreen { get => level != null && level.Filename == rootLevelName; }


        Vector2 centerWCS = Vector2.Zero;



        private void HandlePinch()
        {
            try 
            {

                if (!Input.Touch.IsConnected)
                    return;

                //TODO we get collectgion modified exceptions in Android, could it be making the zoom choppy sometimes
                foreach (GestureSample gesture in Input.Touch.CurrentGestures)
                {

                    if (gesture.GestureType == GestureType.Pinch)
                    {

                        if (Input.IsTouchSticksEnabled)
                        {
                            if (Input.DualTouchStick.BothActive())
                                return;
                        }

                        Input.IsTouchSticksEnabled = false; // note should allow a type of zoom w stick already active tho

                        //since we mighg be directing  gthe creature stop him
                        if (keyPressedForAction != GameKey.None)
                        {
                            InputCommand.Instance.KeyUp(keyPressedForAction);
                        }


                        // current positions

                        float dist = Microsoft.Xna.Framework.Vector2.Distance(gesture.Position, gesture.Position2);
                        Vector2 center = (gesture.Position.ToVector2() + gesture.Position2.ToVector2()) / 2.0f;

                        centerWCS = Presentation.Instance.Camera.Transform.ViewportToWorld(center);//TODO this seem wrong update camera to letterbox, simplify

                  
                        IsPinching = true;
                        base.ProssessMouseDrag = false;

                     
                        //todo allow some kind of vertical drag zoom when using one stick to walk maybfe.. can use the vert drag gesture too

                        isMouseWheelZoomActive = false;

                        float distOld = Microsoft.Xna.Framework.Vector2.Distance(gesture.Position - gesture.Delta, gesture.Position2 - gesture.Delta2);
               

                        float touchdelta = dist- distOld;
                        Debug.WriteLine("touch delta" + touchdelta);


                        if (touchdelta == 0)
                            return;

                        float factor = touchdelta * deltaTouchFactor;
                     
                 
                        float zoomFactor = Graphics.Instance.Presentation.Camera.Zoom * factor;

                        float newZoom =   graphics.Presentation.Camera.Zoom + zoomFactor;/// Graphics.Instance.Presentation.Camera.Zoom;  

                        //NOTE why cant i reuse this.. fuck it not worth the effort
                        //works fine now, wheel and touch about the same

                        //   ZoomCenterOrAroundActiveSpirit(-touchdelta*touchToWheelAdjument, centerWCS);

                        if (Presentation.Instance.Camera.IsCameraTrackingEnabled)
                        {
                            graphics.Presentation.Camera.Zoom = newZoom;
                        }
                        else
                        {
                            graphics.Presentation.Camera.ZoomCenter(centerWCS, newZoom);
                        }

                
                    }
                    else
                    if (gesture.GestureType == GestureType.PinchComplete)
                    {
                        IsPinching = false;
                        base.ProssessMouseDrag = true;
                        Input.IsTouchSticksEnabled = true;
                        isMouseWheelZoomActive = true;

                        Debug.WriteLine("picnch complelte");

                    }
#if FLICKLEVELS
                    else if (gesture.GestureType == GestureType.Flick)
                    {


                        Microsoft.Xna.Framework.Vector2 flickVec = gesture.Position2 - gesture.Position;

                        float totalDist = flickVec.Length();
                        //    if (totalDist > 300)//prevent accidental flick, must be a long one
                        {

                            float distOld = Microsoft.Xna.Framework.Vector2.Distance(gesture.Position - gesture.Delta, gesture.Position2 - gesture.Delta2);

                            float dist = Microsoft.Xna.Framework.Vector2.Distance(gesture.Position, gesture.Position2);
                            float flickDelta = (distOld - dist);



                            if (flickDelta < 100)
                                return;


                            if (Input.Touch.CurrentTouches.Count == 4)
                            {

                                Debug.WriteLine("Flick 4 touch" + flickDelta);
                                Debug.WriteLine("flickVec" + flickVec);

                                //must go to the edge of screen
                                //  if (     gesture.Position2.X < margin || gesture.Position2.X > Graphics.GraphicsDevice.Viewport.Width - margin

                                //      || gesture.Position2.Y < margin || gesture.Position2.Y > Graphics.GraphicsDevice.Viewport.Height - margin)
                                {

                                    //   isShiftDown = flickDelta < 0;
                                    Debug.WriteLine("Flick total dist " + totalDist);

                                    if (flickVec.X > flickVec.Y)
                                    {
                                        bool wasShift = isShiftDown;
                                        isShiftDown = flickVec.X < 0;
                                        OnKeyDown(this, Keys.F2);
                                        isShiftDown = wasShift;
                                    }
                                    else
                                    {
                                        bool wasShift = isShiftDown;
                                        isShiftDown = flickVec.Y < 0;
                                        OnKeyDown(this, Keys.F3);
                                        isShiftDown = wasShift;
                                    }
                                }
                            }
                        }
                    }
#endif
                } 
            }
            catch (Exception exc)
            {
                Debug.WriteLine(" exc in Handle Gestures " + exc);
               
                //note minor issue soemtimes mouse wheel.vert minor because user rarely will use both.  if touching thye will touch
                //breaks after pinching and then using trackpad ( mousewheel on laptop)
                //in windows.. it might be a window isssue tho
                //in dx the touch tries to act like a mouse
                //it sends mousewheell on pich but
                //we handl it ouselves.

                /*
                IsPinching = false;
                base.ProssessMouseDrag = true;
                Input.IsTouchSticksEnabled = true;
                isMouseWheelZoomActive = true;

                Debug.WriteLine("picnch complelte");
                */
            }

       
        }
    



        private void OnPointerDown(Microsoft.Xna.Framework.Vector2 pos)
        {
            base.OnPointerDown(pos.ToVector2());

            if (!TouchPointerControl)          
                return;
            

            Vector2 posWorld = Graphics.Instance.Presentation.Camera.Transform.ConvertScreenToWorld(pos);


            Body hitBody = simworld.HitTestBody(posWorld);

            if (hitBody != null && hitBody.IsStatic == false)
            {
                bodyTouched = hitBody;
                //TODO pick up , aim at, shoot at... or manipulate our dude, set active spirit maybe on debug mode
                return;
            }


            // drag pan if mouse didnt e any fixture
            //   if (hittestfixture == null)  //TODO move to base maybe when we arent controlign a spirit just browsing
            //   {
            // store current viewport pos
            //       isMousePanActive = true;
            //   }
            //  Debug.WriteLine("touch WCS " + posWorld.ToString());


            //TODO hit test.. touch or reach at ojbect pressed..
            //if houlding gun, aim at object touched   or ray cay if hits itmem,  aim arm effect, then  shoot, otherwise walk towards it
             MoveActiveSpiritTowardPointer(posWorld);
           

        }

 



 
        private void MoveActiveSpiritTowardPointer(Vector2 posWorld)
        {

            if (ActiveSpirit == null)
                return;

            float delta = 0.3f;//TODO /zoom


            var xtol = ActiveSpirit.AABB.Width + delta;

            var ytol = ActiveSpirit.AABB.Height + delta;

            ActiveSpirit.PauseOnReleaseKey = false;


            keyPressedForAction = GameKey.None;

            if (posWorld.X < ActiveSpirit.WorldCenter.X - xtol)  //TODO stop whenit gets there?  Add a Update on this for when mouse held mouse or dragged
            {


                keyPressedForAction = GameKey.Left;
                InputCommand.Instance.KeyDown(keyPressedForAction);

                // ActiveSpirit.
                //  ActiveSpirit.Play(GameKey.Left);
            }
            else if (posWorld.X > ActiveSpirit.WorldCenter.X + xtol)
            {

                keyPressedForAction = GameKey.Right;
                InputCommand.Instance.KeyDown(keyPressedForAction);

            }
            else  //we reached the destination, put key back up
            {
                if (InputCommand.Instance.IsGameKeyDown(GameKey.Left))
                    InputCommand.Instance.KeyUp(GameKey.Left);


                if (InputCommand.Instance.IsGameKeyDown(GameKey.Right))
                    InputCommand.Instance.KeyUp(GameKey.Right);

            }


            if (posWorld.Y < ActiveSpirit.WorldCenter.Y - ytol)
            {
                keyPressedForAction = GameKey.Up;
                InputCommand.Instance.KeyDown(keyPressedForAction);


                //    ActiveSpirit.Play(GameKey.Up);//TODO really try jummp onto something here..
            }
            else if (posWorld.Y > ActiveSpirit.WorldCenter.Y + ytol)
            {
                keyPressedForAction = GameKey.Down;
                InputCommand.Instance.KeyDown(keyPressedForAction);

                //   ActiveSpirit.Play(GameKey.Down);
            }
            else
            {
                if (InputCommand.Instance.IsGameKeyDown(GameKey.Up))
                    InputCommand.Instance.KeyUp(GameKey.Up);


                if (InputCommand.Instance.IsGameKeyDown(GameKey.Down))
                    InputCommand.Instance.KeyUp(GameKey.Down);

            }

        }




        //   private void UpdateHandlePointerInput(Microsoft.Xna.Framework.Vector2 pos)






#endregion

#region Event methods
        /// <summary>
        /// Default physics pre-update handler for level-based game code.  
        /// This is run from the Physics  thread, but when the physics thread is locked, and is waiting on this to complete.   This way 
        /// event handlers such as Entity added can be here since physics is not being updated, and can call view creation code since on UI thread.
        /// </summary>
        public override void OnPreUpdatePhysics()
        {

            // Update all spirits
            EntityHelper.UpdateAllEntities(level, simworld.PhysicsUpdateInterval);

            // process cached level entity here, 
            SimWorld.Instance.ProcessLevelDelayedEntity();


            // for selected spirit
            if (ActiveSpirit != null)
            {
                ActiveSpirit.UpdateInput(InputCommand.Instance.KeyState);

                UpdateCameraFollowedObjects();
            }


            Presentation.Instance.Update();
        }

        //TOD add event handler , keys, reacgt, not polling


        protected override void OnMouseLeftButtonDown(object sender, Vector2 pos)
        {

            OnPointerDown(pos.ToVector2());

            base.OnMouseLeftButtonDown(sender, pos);


            // only when mouse drag disabled, redirect mouse event to active spirit.
            // make it available to be used by input command  / spirit input.
            if (!AllowMouseDragAndSelect

             )
            {
                //TODO send mouse event to active spirt

            }
        }


        protected override void OnMouseLeftButtonUp(object sender, Vector2 pos)
        {


            base.OnMouseLeftButtonUp(sender, pos);

            OnPointerUp(pos);

            // always restore camera target on mouse up, 
            // in case AllowMouseDragAndSelect state changed while mouse button still held down.
            // NOTE: no need, each loop now clear & add followed object
            //_camera.FollowSingleEntity(ActiveSpirit);

            

            // if mouse drag disabled, make mouse event available as spirit input.
            if (!AllowMouseDragAndSelect)
            {
                return;
            }


            //if not Clipping then it is zero.. TODO clean out why check this is works fine    TODO repeat code with WinPhone TODO consolidate.. maybe  same with SL 
            //    if (Graphics2.Instance.RootCanvas.ActualWidth == 0 ||
            //         Graphics2.Instance.RootCanvas.ActualHeight == 0)
            //     {
            //         return;
            //    }



            // call base last, because _isMousePanActive & _mousePickDrag are reset by base class

        }


        protected override void OnMouseMove(object sender, Vector2 pos) //todo int point?
        {

            //todo
            base.OnMouseMove(sender, pos);

     
        }



        protected override void OnMouseWheel(object sender, float delta) // todo  MAKE A CLASS LIKE IT , PASS IT THORUGH  PointerRoutedEventArgs e)
        {

            base.OnMouseWheel(sender, delta);
        }




        protected void OnLevelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("ActiveSpirit") == true)
            {
                OnActiveSpiritChanged();
            }
        }

        private void OnActiveSpiritChanged()
        {

            camera.FollowSingleEntity(ActiveSpirit);

            selection.SetSelectedSpirit(ActiveSpirit);

            if (simworld.PhysicsSounds != null)
            {
                simworld.PhysicsSounds.ListenerSpirit = level.ActiveSpirit;
            }


            camera.IsCameraTrackingEnabled = ActiveSpirit != null;
        }

#endregion
#region Methods

        /// <summary>
        /// load, display, and run level
        /// </summary>
        public virtual void LoadLevel(Level level, bool reloadActiveSp = false)
        {
            this.level = level;
            // change current level

            // Level Cache AABB will be handled by SimWorld,
            // Can be access by world.LevelChachedAABB
       
     		simworld.SetLevel(level); 

            // apply physics setting from level
            FarseerPhysics.Settings.VelocityIterations = this.level.VelocityIterations;
            FarseerPhysics.Settings.PositionIterations = this.level.PositionIterations;

            IEnumerable<Body> bodies = this.level.GetAllBodiesFromEntities();


            // insert to physics sim

            using (var sim = new SimPauser())
            {
                this.level.InsertToPhysics(simworld.Physics);
                simworld.Physics.ProcessChanges();
            }

           
            // limit camera view to level. must be done first before initial view set.
            // level are limited by static body as an optimization.
            // this is to avoid any stray falling body getting included ( tdod not necessary they should be cleaned)
  
            AABB levelBoundary = this.level.CalculateAABB(level.Gravity != Vector2.Zero);

            //on space levels might not be any static ground

          //This is not longer done also because of tunnels.. sky will have a static cloude to mark the top.   
            levelBoundary = this.level.ExpandLevelAABBIfNoSkyMarker(levelBoundary);


    
            // window limit is currently disabled because it cause shaking issue when following 
            // object in high speed moving further away from level, expanding level limit.
            // hit window limit


            Presentation.Instance.Camera.WindowLimit = levelBoundary;
            Presentation.Instance.Camera.LimitEnabled = true;


         
            StartView(level);


            Graphics.Instance.CTransform.WindowRotation = level.StartViewRotation;


            // align target rotation with initial view rotation
            camera.TrackingTargetRotation = Graphics.Instance.CTransform.WindowRotation;


            //TODO need to offset of ynrd UP rotation, add get up rotation to IEntity

            //TODO if rotating space station tracking is too slow
            camera.IsAutoRotateWTracking = (level.Gravity == Vector2.Zero);
            camera.LimitEnabled = (level.Gravity != Vector2.Zero);

            // add level listener, must be _before_ import traveler
            this.level.PropertyChanged += OnLevelPropertyChanged;
            this.level.Entities.CollectionChanged += OnLevelEntities_CollectionChanged;
            this.level.Joints.CollectionChanged += OnLevelJoints_CollectionChanged;

            //TODO consider moving this out of here..
            ImportTravelersToCurrentLevelToReplaceActiveSpirit(travelers);   //causes the bug  with  reset, should not be called then.....  have to reseach what was changing.. 

            // always focus on active spirit when loading new level
            OnActiveSpiritChanged();


            // init spirit      // NOTE: this code is duplicated in CreateSpiritPhysicsAndView(), but that is for spawning and copy paste, so we check if its been done as in the case of traveller spirits
            IEnumerable<Spirit> spirits = new List<Spirit>(this.level.GetSpiritEntities());
            foreach (Spirit spirit in spirits)
            {
                spirit.World = simworld.Physics;

                if (spirit.PluginName != null && spirit.PluginName != ""
                    && spirit.Plugin == null)//plugin for travellers were already placed in entity collection changed after import travellers)
                {
                    var plugin = PluginHelper.InstantiatePlugin(spirit.PluginName) as IPlugin<Spirit>;
                    PluginHelper.PrepareSpiritPlugin(spirit, plugin);
                }


            }


            this.level.SetLevelNumberForTesting();
         //  MGCore.Instance.LoadThumbnail(level); load it just when we need it to draw emitter

            simworld.OnLevelLoaded(travelers);

        }



        public virtual bool LoadLevel(string path)
        {

            // dispose current level first
            DisposeCurrentLevel();

            bool ret = true;
            FileInfo fi = new FileInfo(path);

            GameStart.PhysicsView?.ClearMsgStrings();

            Level level = null;

            Stream stream = File.OpenRead(path);
            level = Serialization.LoadDataFromStream<Level>(stream, false);

            if (level == null)
            {
                ret = false;
                level = CreateDefaultLevel();
                path = null;
                fi = null;

                GameStart.PhysicsView.DrawMsgString(path + "Failed to Load");
            }

            LoadLevel(level);
            SetNewLevelFileNames(fi);

            return ret;

        }



        public void StartView(Level level)
        {
            // set up initial view.
            
            if (this.level.StartView.Width > 0 && this.level.StartView.Height > 0)
            {
                camera.ZoomWindow(level.StartView);

                //TODO fix aabb exposed in camera.. test with level, fix bounds..
                camera.ResetLazyTrackingCenter();

                

                AABB bounds = camera.Bounds;

                Debug.WriteLine(camera.Bounds);

            }
        }


        /// <summary>
        /// In here we clear level, reset its view, and physics when finished using a level.
        /// </summary>
        protected virtual void DisposeCurrentLevel()
        {
            if (level == null)
                return;

            AudioManager.Instance.StopAndClearSoundEffectInstances();


            camera.FollowedObjects.Clear();

            // reset camera rotation
            camera.TrackingTargetRotation = 0;

            selection.SetSelectedSpirit(null);

            // remove level listener first before level cleared
            level.PropertyChanged -= OnLevelPropertyChanged;
            level.Entities.CollectionChanged -= OnLevelEntities_CollectionChanged;
            level.Joints.CollectionChanged -= OnLevelJoints_CollectionChanged;

            // list on to xref on level, sent level loaded event from simworld with level

            //TODO put the new xref listern on level.. clear it here.. 


            // clear level
            level.Unload();
            level = null;

            // reset physics world 
            simworld.Reset();
            simworld.ClearLevelViewsAndTextures();
         

        }


        /// <summary>
        /// Reload _lastDialogLevel. Used by leveltest1 also
        /// </summary>
        protected virtual void ReloadLastDialogLevel()
        {
            if (this.level == null || lastLevelFileInfo == null)
                return;

            DisposeCurrentLevel();
            Level level = Storage.Serialization.LoadDataFromFileInfo<Level>(lastLevelFileInfo, false);

            if (level != null)
            {
                level.Filename = lastLevelFileInfo.Name;
                LoadLevel(level, true);
            }
        }



        /// <summary>
        /// Load level from file using open file dialog.
        /// </summary>
        /// <returns>True if level successfully loaded from storage.</returns>
        public bool LoadLevelUsingFileDialog()
        {

            bool loadSuccess = false;


            if (CoreGame.OpenFileDlg == null)
                return false;
            using (SimPauser sp = new SimPauser())
            {

                string path = CoreGame.OpenFileDlg();

                if (string.IsNullOrEmpty(path))
                    return false;

                loadSuccess = LoadLevel(path);
            }

            return loadSuccess;
        }




        public void SetNewLevelFileNames(FileInfo levelFileInfo)
        {

            if (level != null)
            {
                level.FilePath = levelFileInfo.FullName;
                level.Filename = levelFileInfo.Name;

                lastLevelFileInfo = levelFileInfo;


            }
        }

        /// <summary>
        /// Save level into file using save file dialog.
        /// </summary>
        public

        /// <summary>
        /// Save level into file using save file dialog.
        /// </summary>
        bool SaveLevelUsingFileDialog()
        {

            /*  todo    using (SimPauser sp = new SimPauser())
                 {
                     try
                     {
                         FileSavePicker sfd = new FileSavePicker();

                         sfd.FileTypeChoices.Add("Level File", new List<string>() { ".wyg" });
                         sfd.SuggestedStartLocation = PickerLocationId.Desktop;


                         StorageFile file = await sfd.PickSaveFileAsync();



                         // should check null level here, not in serialization module, _before_ 
                         // the stream (file) created. it's because SaveFileDialog is quite 
                         // problematic when used in debugger. if SaveFileDialog.OpenFile() 
                         // is called and Stream is obtained, then exception occurs before 
                         // stream is closed, entire app will be shutdown.
                         if (file != null && _level != null)
                         {
                             // code below is similar to ShadowTool.MainController.SaveLevel
                             // save current world aabb
                             _level.StartView = ShapeUtility.RectToAABB(Graphics.Instance.CTransform.GetWorldWindow());
                        //     _level.StartViewRotation = Graphics.Instance.CTransform.WindowRotation;

                             // save level

                             Stream writeFile = await file.OpenStreamForWriteAsync();
                             Storage.Serialization.SaveDataToStream<Level>(writeFile, _level, false);

                         }
                     }

                     catch (Exception ex)
                     {
                         Logger.Trace(" Error in Save Level" + ex.Message);

                     }

                 }
            */
            return false;
        }


        /// <summary>
        /// Update Camera.FollowedObjects .
        /// Always clear followed objects, then filled with avaliable active spirit + enemies.
        /// Will return to last zoom if enemies are no more.
        /// 
        /// Don't include enemies or return to last zoom if  active spirit is already dead, 
        /// or not a living spirit, for example  airship & balloon.
        /// </summary>
        private void UpdateCameraFollowedObjects()
        {
            camera.FollowedObjects.Clear();

            if (ActiveSpirit == null || !camera.IsCameraTrackingEnabled)
                return;

            camera.FollowedObjects.Add(ActiveSpirit);

            if (ActiveSpirit.IsDead || !ActiveSpirit.IsMinded)
                return;

            if (camera.IsFrameEnemies)
            {
                ActiveSpirit.Mind.GetEnemySpiritsInFrame(camera.MaxFrameObjectDistX, camera.MaxFrameObjectDistY).
                    ForEach(x => camera.FollowedObjects.Add(x));
            }


            camera.TrackTargetBottom = (Input.DualTouchStick != null && Input.DualTouchStick.IsActive());
            UpdateLastZoomAndFollowedObjects();
        }


        private void UpdateLastZoomAndFollowedObjects()
        {
            int currentFollowedObjectsCount = camera.FollowedObjects.Count;

            if (currentFollowedObjectsCount == 1)
            {
                if (lastFollowedObjectsCount > 1 && _lastSingleZoom > 0f)
                {
                    // back to previous zoom
                    camera.Zoom = _lastSingleZoom;
                }
                else
                {
                    _lastSingleZoom = camera.Zoom;
                }
            }

            lastFollowedObjectsCount = currentFollowedObjectsCount;
        }


#endregion

#region Add remove entity from Level

        // this should be placed on AutoLevelSwitch but active spirit must be processed here on LoadLevel(), so here it is.
        protected void ImportTravelersToCurrentLevelToReplaceActiveSpirit(EntityCollection travelers)
        {
            if (travelers.Count <= 0)
                return;

        
            // import saved traveler to this new level, replacing current ActiveSpirit.      
            // add  loose body first, then spirit,  joint should be processed last.
            IEnumerable<Body> bodies = travelers.OfType<Body>();

            //TODO FUTURE save these to local storage, load on reload level.       
            bodies.ForEach(x => { x.ResetStateForTransferBetweenPhysicsWorld(); level.CacheAddEntity(x); });

            List<Spirit> auxSpirits = new List<Spirit>();

            IEnumerable<Spirit> spirits = travelers.OfType<Spirit>();
            foreach (Spirit sp in spirits)
            {
                sp.ResetTravelerPhysicsWorldState();

                //   _level.Entities.Add(sp);    causes weird issues , still not fully understood why.. collide connected does not prevent jointed items collidected, etc., joint list is there but CCD still collides joined bodies   
                level.CacheAddEntity(sp);

     //TODO FUTURE on the collection changed handler, it finds out if its a traveller and recreates the attach joints for its held items there?... REVISIT  might be a simpler way. 
                // TODO: perhaps merge this aux spirit later with similar case on ProcessLevelDelayedEntity()
                foreach (Spirit auxsp in sp.AuxiliarySpirits)
                {
                    auxsp.ResetTravelerPhysicsWorldState();
                    level.CacheAddEntity(auxsp);//TODO  consider put this in entity added.. but careful about circular ref.. see how tool paste works.
                    auxSpirits.Add(auxsp);
                }
            }


            //TODO make each spirt handle its own aux joints

            // do this in separate loop, after all spirits are  inserted into level
            foreach (Spirit sp in spirits)
            {
                foreach (Joint j in sp.AuxiliarySpiritJoints)
                {
                    // this is required to make joined system of  spirits like airship & ballon assembly , connected when switching level
                    j.ResetStateForTransferBetweenPhysicsWorld();
                    AddJointToLevel(j);
                }
            }

            // include aux spirit into travelers, so it will be included in repositioning next
            travelers.AddRange(auxSpirits);
            SetActiveSpiritFromTraveler(travelers);

  
        }


        /// <summary>
        /// each level should have ActiveSpirit or a spawning emitter, so it can  tested or played separately.. replace this with the traveler
        /// if none then just set current traveller as active spirit.
        /// </summary>
        private void SetActiveSpiritFromTraveler(EntityCollection travelers)
        {
            Spirit spiritTraveler = travelers[0] as Spirit;

            if (spiritTraveler == null)
                return;

            if (ActiveSpirit != null)
            {
                SimWorld.Instance.ReplaceSpirit(ActiveSpirit, spiritTraveler, false, null);
            }
            else    // now levels are WCS aligned, just dont spawn active spirit
            {
                ActiveSpirit = spiritTraveler;
            }
        }

        /// <summary>
        /// Handle when Level.Entities collection are modified _during_ gameplay.
        /// </summary>
        protected void OnLevelEntities_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // how to know which level we operate on ?
            // for now we assume this operates on _level ...

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (IEntity entity in e.NewItems)
                    {
                        if (entity is Body)
                        {

                            Body body = entity as Body;


                            CreateBodyPhysicsAndView(body);

                            //TODO test check with clouds after skewed..    they can self.. cross might fail..
                            if (!(entity is Particle))
                            {
                                body.OnCollision -= CollisionEffects.OnCollisionEventHandler;
                                body.OnCollision += CollisionEffects.OnCollisionEventHandler;
                            }

                            IField windField = (entity as IField);
                            if (windField != null)
                            {
                                WindDrag.AddWindField(windField);
                            }


                        }
                        else if (entity is Spirit)
                        {
                            CreateSpiritPhysicsAndView(entity as Spirit);  //collisions listeners are set inside sprit
                            Level.Instance.ClearSpiritsCache();
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (IEntity entity in e.OldItems)
                    {
                        if (entity is Body)
                        {
                            DestroyBody(entity as Body);

                            IField windField = (entity as IField);
                            if (windField != null)
                            {
                                WindDrag.RemoveWindField(windField);
                            }
                        }
                        else if (entity is Spirit)
                        {
                            DestroySpirit(entity as Spirit);
                            Level.Instance.ClearSpiritsCache();
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // sender entity collection already empty when reach here... can do nothing..
                    break;
            }
        }

        /// <summary>
        /// Entitiy Creation Code, used on entity added for each body..  NOTE some important setup is done on Body Deserialized.   Rebuild Fixtures is needed to add a body to a new world
        /// </summary>
        protected void CreateBodyPhysicsAndView(Body b)
        {


            // insert into physics
            if (simworld.PhysicsThread.WaitForAccess(2000))
            {

                if (simworld.Physics.BodyList.Contains(b) == false)
                    simworld.Physics.AddBody(b);
                {
                    Debug.WriteLine("dublicate body"
                        + b.BodyType + " " + b.PartType + " " + b.ToString());

                }

                simworld.PhysicsThread.FinishedAccess();
            }

            presentation.CreateView(b);
        }

        protected void DestroyBody(Body b)
        {

            // remove body from physics
            if (simworld.PhysicsThread.WaitForAccess(2000))
            {
                SimWorld.Instance.RemoveBody(b);

                simworld.PhysicsThread.FinishedAccess();
            }

            simworld.Sensor.RayMap.Values.ForEach(ray => ray.IgnoredBodies.Remove(b));

            presentation.RemoveView(b);


        }


        //TODO code review FUTURE    should separate create view from adding to physics, would be easier to run levels without view  ( preanimate) 
        /// <summary>
        /// Insert all bodies and joints in Spirit into physics. 
        /// This will call creator for Body and its view.
        /// Assumed we operate on currently active level (i.e. _level)
        /// </summary>
        /// 
        protected void CreateSpiritPhysicsAndView(Spirit spirit)
        {
            foreach (Body body in spirit.Bodies)
            {
                CreateBodyPhysicsAndView(body);

                if (body.PartType == PartType.MainBody && !level.MapBodyToSpirits.ContainsKey(body))
                {
                    level.MapBodyToSpirits.Add(body, spirit);
                }

                body.OnCollision -= CollisionEffects.OnCollisionEventHandler;
                body.OnCollision += CollisionEffects.OnCollisionEventHandler;
            }

            // insert joints and attachable to both physics and level.Joints
            foreach (PoweredJoint pj in spirit.Joints)
            {
                AddJointToLevel(pj);  //bodies are  added before joints in the World .ProcessChanges 

                //fix for spawned creatures cant cut joints easily.  on deserialized is when the listeners were set but there are no fixtures to listen to
                pj.ResetStateForTransferBetweenPhysicsWorld();  //note this rebuilds joint edges.. is it necessary?  
            }
            foreach (Joint j in spirit.FixedJoints)
            {
                AddJointToLevel(j);
            }
            foreach (Joint j in spirit.AuxiliarySpiritJoints)
            {
                AddJointToLevel(j);
            }

            AddHeldOrStuckItemJoints(spirit);
            simworld.Physics.ProcessChanges();

            // set spirit level
            spirit.SetParentLevel(level);
            spirit.World = simworld.Physics;

            // rebuild data model graph, joints and bodies.. NOTE not sure why  tried commenting it out.. no change seens
            spirit.RebuildSpiritInternalGraph(false);

            if (spirit.PluginName != null && spirit.PluginName != "")
            {
                var plugin = PluginHelper.InstantiatePlugin(spirit.PluginName) as IPlugin<Spirit>;
                PluginHelper.PrepareSpiritPlugin(spirit, plugin);
            }

            presentation.CreateView(spirit);

        }


        private void AddHeldOrStuckItemJoints(Spirit spirit)
        {
            // only for minded spirit
            if (!spirit.IsMinded)
                return;

            foreach (Body b in spirit.Bodies)
            {
                foreach (AttachPoint ap in b.AttachPoints)
                {
                    if (ap.Joint != null)
                    {
                        AddJointToPhysics(ap.Joint);
                    }
                }
            }
        }


        protected void DestroySpirit(Spirit sp)
        {
            if (level.MapBodyToSpirits.ContainsValue(sp))
            {
                // this will properly remove sensor fixture and its collision event
                if (sp.SensorFixture != null)
                {
                    sp.SensorRadius = 0f;
                }
            }

            sp.ReleaseListeners();

            level.Joints.EnableUndoRedoFeature = false;

            // remove all joints from spirit and clear its event 
            foreach (PoweredJoint joint in sp.Joints)
            {
                level.Joints.Remove(joint);
            }

            if (sp.Plugin != null)
            {
                sp.Plugin.UnLoaded();
                sp.Plugin.Parent = null;
                sp.Plugin = null;
            }

            if (simworld.PhysicsThread.WaitForAccess(1000) == true)
            {
                // ensure any deleted joint is updated by world immediately, so
                // other module that need to delete joint can immediately know if 
                // specific joint is already deleted.
                simworld.Physics.ProcessChanges();

                simworld.PhysicsThread.FinishedAccess();
            }

            level.Joints.EnableUndoRedoFeature = true;

            // remove all bodies from the spirits map and from physics
            foreach (Body body in sp.Bodies)
            {
                if (level.MapBodyToSpirits.ContainsKey(body))
                {
                    level.MapBodyToSpirits.Remove(body);
                }
                DestroyBody(body);
            }


            // remove all other reference to spirit
            if (ActiveSpirit == sp)
            {
                ActiveSpirit = null;
            }

            camera.FollowedObjects.Remove(sp);


            presentation.RemoveView(sp.MainBody);
        }



        /// <summary>
        /// Handle when Level.Joints collection are modified _during_ gameplay.
        /// </summary>
        protected void OnLevelJoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (Joint joint in e.NewItems)
                    {
                        AddJointToPhysics(joint);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (Joint joint in e.OldItems)
                    {
                        DestroyJoint(joint);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    break;
            }
        }

        // copied from tools MainLogic
        protected void AddJointToLevel(Joint joint)
        {
            if (joint == null || level == null) return;

            if (level.Joints.Contains(joint) == false)
            {
                level.Joints.Add(joint);
                level.IsDirty = true;
            }
            // physics uses collection changed event
        }

        // called from collection change event
        protected void AddJointToPhysics(Joint joint)
        {
            // insert into physics.
            if (simworld.PhysicsThread.WaitForAccess(2000))
            {
                if (simworld.Physics.JointList.Contains(joint) == false)
                {
                    simworld.Physics.AddJoint(joint);
                }

                simworld.PhysicsThread.FinishedAccess();
            }
        }

        // called from collection change event
        protected void DestroyJoint(Joint joint)
        {
            if (joint is PoweredJoint)
            {
                (joint as PoweredJoint).ReleaseListeners();
            }

            joint.Breaking = null;

            if (simworld.PhysicsThread.WaitForAccess(2000))
            {
                simworld.Physics.RemoveJoint(joint);
                simworld.PhysicsThread.FinishedAccess();
            }

            // other reference to Joint (from spirit, planet, etc) should be cleared by caller.
        }

#endregion


        protected Level CreateDefaultLevel()
        {
            Level level = new Level();
            level.Gravity = new Vector2(0, 10);

            Vector2 min = new Vector2(-10, -5);
            Vector2 max = new Vector2(10, 5);
            level.StartView = new AABB(ref min, ref max);
            level.StartViewRotation = 0;



            //Fixture ground = FixtureFactory.CreateRectangle(_world.Physics, 1000.0f, 0.2f, 1.0f);
            Fixture ground = FixtureFactory.CreateRectangle(World.Instance, 100.0f, 0.2f, 10.0f);
            ground.Body.Position = new Vector2(0, 4.0f);
            ground.Body.BodyType = BodyType.Static;
            ground.Body.Friction = 0.6f;
            level.Entities.Add(ground.Body);


            ground = FixtureFactory.CreateRectangle(World.Instance, 0.2f, 30f, 10.0f);
            ground.Body.Position = new Vector2(50, 4.0f);
            ground.Body.BodyType = BodyType.Static;
            ground.Body.Friction = 0.6f;
            level.Entities.Add(ground.Body);


            Fixture r = FixtureFactory.CreateRectangle(World.Instance, 0.4f, 0.2f, 1.5f);
            r.Body.Position = new Vector2(-10, -18.0f);
            r.Body.BodyType = BodyType.Static;
            r.Body.Density = 100.0f;
            r.Body.Friction = 0.6f;
            r.Body.IsNotCollideable = true;
            level.Entities.Add(r.Body);  //   just a static body for marking AABB top to reasonable      

            return level;
        }

    }
}
