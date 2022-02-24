#define EXTRADEBUGKEYS
using System;

using System.Collections.Generic;

using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;


using Core.Game.MG.Graphics;
using Core.Game.MG.Plugins;

using Core.Game.MG.Drawing;


using Core.Game.MG;
using Microsoft.Xna.Framework.Input;
using Vector2 = Farseer.Xna.Framework.Vector2;
using Core.Game.MG.Simulation;
using Graphics = Core.Game.MG.Graphics.Graphics;
using MGCore;
using System.Diagnostics;
using Core.Data.Input;
using System.Runtime.InteropServices;
using Core.Data;
using FarseerPhysicsView;
using _2DWorldCore.UI;

//using Graphics = Nez.Graphics;




namespace _2DWorldCore
{



    /// <summary>
    /// This base class us make a ui core of everythign common to all the playforms, and without basing on the confusiong timer and game loop of monogame, view and game update, and its sync wiht graphics refresh.
    /// its basically allows for a game loop with .net sytem timer, ( or using a tight untimed loop for max updates per sec, start, onupdateframe,   and exit)
    /// </summary>
    public abstract class GameCodeBase : IGameCode
    {

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(SystemMetric smIndex);//in case we need this

        ///  
        /// This Enum and declaration signature was written by Gabriel T. Sharp
        /// ai_productions@verizon.net or osirisgothra@hotmail.com
        /// Obtained on pinvoke.net, please contribute your code to support the wiki!
        /// </summary>
        public enum SystemMetric : int
        {
            /// <summary>
            /// Nonzero if the meanings of the left and right mouse buttons are swapped; otherwise, 0.
            /// </summary>
            SM_SWAPBUTTON = 23,

        }
        #region Vars
        protected SimWorld simworld;
        protected Graphics graphics;
        protected Presentation presentation;
        protected Camera camera;

        protected LineSegment springLine;

        // as for farseer 3.2, seems better use FixedMouseJoint for mouse drag
        protected FixedMouseJoint mousePickJoint;

        private bool isMousePanActive;
        protected bool isShiftDown = false;
        protected bool isCntrlDown = false;

        protected bool isMouseWheelZoomActive = true;



        /// <summary>
        /// Use this to enable dragging object and panning screen using mouse.  Default is false.
        /// When enabled it will disable Spirit mouse UserInput.
        /// </summary>
        public bool AllowMouseDragAndSelect = false;
        #endregion


        #region Constructor

        protected GameCodeBase()
        {
            Initialize();
        }
        #endregion



        protected bool isMousePrimarySwitched = false;

        private void Initialize()
        {


            //  if (SimWorld.IsDirectX)//not needed, Input takes care of this in windows
            //  {
            ///      isMousePrimarySwitched = GetSystemMetrics(SystemMetric.SM_SWAPBUTTON) != 0;
            // }

            graphics = Graphics.Instance;
            simworld = SimWorld.Instance;
            presentation = Graphics.Instance.Presentation;

            //InitEvents();

            isShiftDown = false;
            isCntrlDown = false;

            // create spring line view
            springLine = new LineSegment();
            camera = Graphics.Instance.Presentation.Camera;

            camera.PanSpeed = 9;
            camera.ZoomSpeed = 1.5f;
            camera.RotateSpeed = 0.5f;
        }





        #region Methods



        public void Start()
        {
            OnBeginCode();
        }




 

        // this method always run regardless of simulation pause
        public void Update(object sender, TickEventArgs e)
        {


        
            //this must be called once and first, so that pressed events are current and not pressed for  mutiple frames
            if (InputCommand.IsBKInputUpudate)
            {
                InputCommand.Instance.Update();
            }

            //now do all the virtual crap, 
            OnUpdate(sender, e);

            Scanner.Instance.OnUpdate(e);

      
        }




        /// <summary>
        ///   clear the world and view, and all the UI events.
        /// </summary>
        public void Terminate()
        {
            // clear the world and view, and all the UI events.
            simworld.ShutDown();

            OnTerminate();
        }


        /// <summary>
        /// Return world coordinate (physics coordinate) from current mouse location on the viewport.
        /// </summary>
        protected Vector2 MousePointToWorldPoint(Vector2 pos)
        {
            return graphics.Presentation.Camera.Transform.ViewportToWorld(pos);
        }

    
        private Vector2 lastpos;



        #region Game code event, can be overridden by derived class

        /// <summary>
        /// This method is only called once, when Page is loaded at program
        /// startup. 
        /// </summary>
        protected virtual void OnLoaded(object sender) { }

        protected virtual void OnBeginCode() { }//?


        static Keys[] extrakeysUsed = new Keys[] { Keys.PageDown, Keys.Add, Keys.PageUp,
            Keys.Add,Keys.OemMinus, Keys.OemPlus, Keys.Subtract, Keys.LeftControl, Keys.RightControl,Keys.LeftShift,
            Keys.RightShift, Keys.F2, Keys.F10, Keys.F5, Keys.F3, Keys.M, Keys.F, Keys.R, Keys.D, Keys.N, Keys.P, 
            Keys.S


#if EXTRADEBUGKEYS
, Keys.A
, Keys.C
#endif   
        };




        /// <summary>
        /// Interval in num frames after which idle mouse cursor is hiden
        /// </summary>
        // TODO might use the fps so its more like real time
        public static float MouseCursorHideInterval = 600; // ticks  //timer didnt behave as expected so using game time

        private static float startMouseHideTime = 0;

        protected void WakeUpIdleMouse()
        {
            if (!CoreGame.IsUIMouseVisible)
            {
             //   Debug.WriteLine("mouse vis to true");
                CoreGame.IsUIMouseVisible = true;
                startMouseHideTime = PhysicsThread.ticks;
            }
        }


        protected virtual void OnUpdate(object sender, TickEventArgs e)
        {
            //  Debug.WriteLine("mouse vis" + CoreGame.Instance.IsMouseVisible);  //it doesnt acutalyy hide the mouse


            if (e.Tick - startMouseHideTime > MouseCursorHideInterval)
            {
                if (CoreGame.IsUIMouseVisible)
                {
                    CoreGame.IsUIMouseVisible = false;//set this set the real on on ui thread
                  //  Debug.WriteLine("mouse vis t" + CoreGame.Instance.IsMouseVisible);
                }
            };

            foreach (Keys key in extrakeysUsed)
            {

                if (Input.IsKeyPressed(key))
                {
                    OnKeyDown(sender, key);
                }


                if (Input.IsKeyReleased(key))
                {
                    OnKeyUp(sender, key);
                }
            }

            InputCommand.HandleGamePad(Input.DualTouchStick != null);
            
				

            // You can use GetSystemMetrics with SM_SWAPBUTTON parameter value.
            //SM_SWAPBUTTON 23(winuser.h)

      
            //Input seems to take care of this setting  sometimes, between just check both
            bool primaryMousePressed = Input.LeftMouseButtonPressed || Input.RightMouseButtonPressed;
            bool primaryMouseReleased = Input.LeftMouseButtonReleased|| Input.RightMouseButtonReleased;


            if (primaryMousePressed)
            {
                OnMouseLeftButtonDown(sender, Input.CurrentMouseState.Position.ToVector2().ToVector2());
            }
            else
            if (primaryMouseReleased)
            {
                OnMouseLeftButtonUp(sender, Input.CurrentMouseState.Position.ToVector2().ToVector2());
            }


            //pinch on windowDX might give this
            if (Input.MouseWheelDelta != 0)
            {
                OnMouseWheel(sender, Input.MouseWheelDelta);
            }

            //touch on screen in windows openGL gives this
            if (Input.MousePositionDelta.ToVector2().LengthSquared() > 0)
            {
                OnMouseMove(sender, Input.CurrentMouseState.Position.ToVector2().ToVector2());
            }

            if (Input.GamePads.Length > 0)
            {

                //reverse the zoom in / out// TODO see standard in games like Halo or GTA or COD
                if (Input.GamePads[0].IsRightStickUpPressed())
                {
                    OnKeyDown(sender, Keys.PageDown);
                }
                else
                    if (Input.GamePads[0].IsRightStickUpReleased())
                {

                    OnKeyUp(sender, Keys.PageDown);
                }
                else
                    if (Input.GamePads[0].IsRightStickDownPressed())
                {
                    OnKeyDown(sender, Keys.PageUp);
                }
                else
                    if (Input.GamePads[0].IsRightStickDownReleased())
                {
                    OnKeyUp(sender, Keys.PageUp);
                }

            }


            Core.Game.MG.Graphics.Graphics.Instance.Presentation.Camera.Update(e.Tick);

            if (!AllowMouseDragAndSelect)
                return;

            // update geom pick spring line
            if (mousePickJoint != null)
            {

           //     springLine.X1 = mousePickJoint.WorldAnchorB;
           //     springLine.X2 = mousePickJoint.WorldAnchorA;

               // SimWorld.Instance.RayViews.Add(springLine)
  
            }
            else
            {
                // SimWorld.Instance.RayViews.Add(springLine)//remove 

                //  springLine.Visible = false;
            }

        }



        /// <summary>
        /// This method always execute and end before physics thread is signaled to progress. 
        /// Any code executed inside this method can safely access physics without thread lock.
        /// </summary>
        protected virtual void OnTerminate() { }



        protected virtual void OnKeyDown(object sender, Keys key)
        {


            switch (key)
            {
               // default:
                //    camera.CameraInput = CameraInput.None;
                 //   break;

                case Keys.PageDown:
                    camera.CameraInput = CameraInput.ZoomIn;
                    camera.ZoomSpeed = 2f;
                    Presentation.Instance.Camera.IsCameraTrackingEnabled = true;
                    break;

                case Keys.Add:
                case Keys.OemPlus:
                    camera.CameraInput = CameraInput.ZoomIn;
                    camera.ZoomSpeed = 1f;
                    Presentation.Instance.Camera.IsCameraTrackingEnabled = true;
                    break;

                //TODO make behavor more like slider, small and large change
                // taping page up twice doest do 2 quick, have to hold it
                case Keys.PageUp:
                    camera.ZoomSpeed = 1f;
                    camera.CameraInput = CameraInput.ZoomOut;
                    Presentation.Instance.Camera.IsCameraTrackingEnabled =true;

                    break;


                case Keys.Subtract:
                case Keys.OemMinus:
                    camera.ZoomSpeed = 2f;
                    camera.CameraInput = CameraInput.ZoomOut;
                    Presentation.Instance.Camera.IsCameraTrackingEnabled = true;
                    break;

                case Keys.RightShift:
                case Keys.LeftShift:
                    isShiftDown = true;
                    break;

                case Keys.RightControl:
                case Keys.LeftControl:
                    isCntrlDown = true;
                    break;


     #if DEBUG
                case Keys.R:
                    // when held shift then 0 pressed, toggle drag mouse state
                    camera.IsAutoRotateWTracking = !camera.IsAutoRotateWTracking;
                    break;
#endif
                case Keys.M:
                    // when held shift then 0 pressed, toggle drag mouse state
                    if (isShiftDown)
                    {
                        AllowMouseDragAndSelect = !AllowMouseDragAndSelect;
                    }
                    break;

                case Keys.NumPad1:
                    if (isShiftDown)
                    {
                        SimWorld.Instance.NitroBoost = !SimWorld.Instance.NitroBoost;
                    }
                    break;


                case Keys.N:

                    if (isShiftDown)
                    {
                        SimWorld.Instance.NitroBoost = !SimWorld.Instance.NitroBoost;
                    }

                    break;

                case Keys.D:

                    if (isShiftDown&& isCntrlDown)
                    {
                        CoreGame.ShowDebugInfo = !CoreGame.ShowDebugInfo;
                    }

                    break;

#if EXTRADEBUGKEYS
                case Keys.A:


                    if (isShiftDown && isCntrlDown)
                    {
                        CoreGame.FeatureSet.ProxyView = !CoreGame.FeatureSet.ProxyView;            
                    }
                    break;


                case Keys.P:

                    if (isShiftDown && isCntrlDown)
                    {
                        SimWorld.IsParticleOn = !SimWorld.IsParticleOn;
                    }
                    break;
#endif



                case Keys.S:

                    if (isShiftDown && isCntrlDown)
                    {
                        AudioManager.Instance.IsSoundOn = !AudioManager.Instance.IsSoundOn;
                    }

                    break;


                case Keys.C:

                    if (isShiftDown && isCntrlDown)
                    {
                        FarseerPhysics.Settings.ContinuousPhysics = !FarseerPhysics.Settings.ContinuousPhysics;
               
                    }

                    break;


#if extracam
                // commented out for ..pan would be usedfull but better use modifier+ arrows..
                //also the tracking needs to go off momentariry for that..

                case VirtualKey.U:
                    _camera.CameraInput = CameraInput.PanUp; break;

                case VirtualKey.O:
                    _camera.CameraInput = CameraInput.PanDown; break;

                case VirtualKey.I:
                    _camera.CameraInput = CameraInput.PanLeft; break;

                case VirtualKey.P:
                    _camera.CameraInput = CameraInput.PanRight; break;

                case VirtualKey.T:
                    _camera.CameraInput = CameraInput.RotateClockwise; break;

                case VirtualKey.Y:
                    _camera.CameraInput = CameraInput.RotateCounterClockwise; break;
                    }


#endif

            }
        }       



        protected virtual void OnKeyUp(object sender, Keys e)
        {


            switch (e)
            {


                case Keys.PageDown:
                case Keys.Add:
                case Keys.OemPlus:
                    if (camera.CameraInput == CameraInput.ZoomIn)
                        camera.CameraInput = CameraInput.None;
                    break;

                case Keys.PageUp:
                case Keys.Subtract:
                case Keys.OemMinus:
                    if (camera.CameraInput == CameraInput.ZoomOut)
                        camera.CameraInput = CameraInput.None;
                    break;

                case Keys.LeftShift:
                case Keys.RightShift:
                    isShiftDown = false;
                    break;

                case Keys.LeftControl:
                case Keys.RightControl:
                    isCntrlDown = false;
                    break;




#if extracam
                // commented out for ..pan would b
                e usedfull but better use modifier+ arrows..
                //also the tracking needs to go off momentariry for that..
                case VirtualKey.I:
                    if (_camera.CameraInput == CameraInput.PanUp) _camera.CameraInput = CameraInput.None;
                    break;

                case VirtualKey.K:
                    if (_camera.CameraInput == CameraInput.PanDown)
                        _camera.CameraInput = CameraInput.None;
                    break;

                case VirtualKey.J://TODO shoujld this be P?  
                    if (_camera.CameraInput == CameraInput.PanLeft)
                        _camera.CameraInput = CameraInput.None;
                    break;

                case VirtualKey.L:  //TODO shoujld this be I?  
                    if (_camera.CameraInput == CameraInput.PanRight) _camera.CameraInput = CameraInput.None;
                    break;

                case VirtualKey.U:
                    if (_camera.CameraInput == CameraInput.RotateClockwise) _camera.CameraInput = CameraInput.None;
                    break;

                case VirtualKey.O:
                    if (_camera.CameraInput == CameraInput.RotateCounterClockwise) _camera.CameraInput = CameraInput.None;
                    break;
                    }
#endif

            }
        }

        protected bool ProssessMouseDrag = true;
        protected virtual void OnMouseMove(object sender, Vector2 pos)
        {

            WakeUpIdleMouse();


            if (!AllowMouseDragAndSelect)
                return;


            if (ProssessMouseDrag == false)
                return;

            // mouse pan mode
            if (IsMousePanActive == true)
            {
                // pan camera using mouse pos difference in viewport coordinate

                Vector2 curpos = graphics.Presentation.Camera.Transform.ViewportToWorld(pos);
                Vector2 diff = curpos - lastpos;

                // direction of pan is the reverse of mouse move like we move the world by
                graphics.Presentation.Camera.PanRelativeToWindow(-diff.X, -diff.Y);

                lastpos = curpos;

            }

            // mouse select mode
            else if (mousePickJoint != null)
            {
                 
                mousePickJoint.WorldAnchorB = MousePointToWorldPoint(pos);
                Debug.WriteLine("mouse joint drag");
                
            }
        }

        protected bool AllowMousePan = false;
        protected virtual void OnMouseLeftButtonDown(object sender, Vector2  pos)
        {

            Debug.WriteLine("mouse left down");


            WakeUpIdleMouse();

            Vector2 posInWorld = MousePointToWorldPoint(pos);
             InputCommand.Instance.LastMouseClickedPos = posInWorld;


            if (!AllowMouseDragAndSelect)
                return;


            // always set false first
            IsMousePanActive = false;

            Body hitBody  = simworld.HitTestBody(posInWorld);
      

            // drag pan if mouse didnt hit any fixture
            if (hitBody == null)
            {

                if (AllowMousePan)
                {
                    // store current viewport pos
                    IsMousePanActive = true;
                }
            }
            // create joint for dragging            
            else
            {
                Vector2 local = hitBody.GetLocalPoint(posInWorld);
                if (mousePickJoint == null)
                {
                    mousePickJoint = new FixedMouseJoint(hitBody, posInWorld);


                    float baseForce = (isShiftDown || isCntrlDown) ? 1000 : 100;
                    mousePickJoint.MaxForce = baseForce * hitBody.Mass;

                    mousePickJoint.Frequency = 100;  //not tuned..  
                    if (simworld.Physics.JointList.Contains(mousePickJoint) == false)
                    {
                        //   if (simworld.PhysicsThread.WaitForAccess(1000))
                        {
                            simworld.Physics.AddJoint(mousePickJoint);
                            //      simworld.PhysicsThread.FinishedAccess();
                        }
                    }
                }
                else
                {
                    mousePickJoint.BodyA = hitBody;
                    mousePickJoint.LocalAnchorA = local;
                    mousePickJoint.WorldAnchorB = posInWorld;

                    Debug.WriteLine("mousejoint drag crerate");
                }
            }
        }


        protected virtual void OnMouseLeftButtonUp(object sender, Vector2 pos)
        {

            Debug.WriteLine("mouse left Up");

            WakeUpIdleMouse();


            // always set false on mouseup
            IsMousePanActive = false;

            if (AllowMouseDragAndSelect && mousePickJoint != null)
            {
                mousePickJoint.IsBroken = true;

           
              //  simworld.PhysicsThread.WaitForAccess(-1);

                if (simworld.Physics.JointList.Contains(mousePickJoint) == true &&
                    simworld.Physics.JointRemoveList.Contains(mousePickJoint) == false)
                {
                    simworld.Physics.RemoveJoint(mousePickJoint);
                }
          //      simworld.PhysicsThread.FinishedAccess();
                mousePickJoint = null;
            }
        }

        public Vector2 MouseToWorldPoint()
        {
            return graphics.Presentation.Camera.Transform.ViewportToWorld(Input.MousePosition);
        }


        protected const float wheelFactor = 70.0f;//up is less zoom per frame

        protected virtual void OnMouseWheel(object sender, float delta)
        {

            WakeUpIdleMouse();

            if (!isMouseWheelZoomActive)
                return;

            // TODO  if issues.   now its good enough... zooming still not proper, sometimes cant keep previous pos under mouse cursor, 
            // especially if we do zoom in/out in rapid succession.
            // either numerical error with small value, or another issue with 
            // different zoom implementation from shadowtools
            //dh leaving this comment because its a bit flaky but works..
            //note couldnt resue this with tocuh.. can asjusrt wheel Factor as much as expectd either


            Vector2 wheelptWCS = MouseToWorldPoint();
            //    int delta =  wheelpt.Properties.MouseWheelDelta;
            //todo

            ZoomCenterOrAroundActiveSpirit(delta, wheelptWCS);

       
        }

        public static void ZoomCenterOrAroundActiveSpirit(float delta, Vector2 wheelptWCS)
        {
            float zoom = CalcZoomLevel(delta);
            if (Level.Instance.ActiveSpirit == null)
            {
                Graphics.Instance.Presentation.Camera.StopCameraFollowing();

                Graphics.Instance.Presentation.Camera.ZoomCenter(wheelptWCS, zoom);
                
            }
            else
            { 
                Graphics.Instance.Presentation.Camera.Zoom = zoom;
            }
        }

        protected virtual void OnPointerDown(Vector2 pos)
        {
            CoreGame.Instance.RootUIObjects.ForEach(x => x.OnPointerDown(pos.ToVector2()));
        }

        static protected GameKey keyPressedForAction;


        protected virtual void OnPointerUp(Vector2 pos)
        {

            CoreGame.Instance.RootUIObjects.ForEach(x => x.OnPointerUp(pos.ToVector2()));


            //   Vector2 posWorld = Graphics.Instance.Presentation.Camera.Transform.ConvertScreenToWorld(pos);

            //TODO this mahy need a timer to avoid pince zoom doing this when first touch goes down
            //or we stop on the pinch

            ////   Fixture hittestfixture = simworld.HitTestFixture(posWorld);

            bodyTouched = null;


            //TODO hit test.. touch or reach at ojbect pressed..
            //if houlding gun, aim at object touched   or ray cay if hits itmem,  aim arm effect, then  shoot, otherwise walk towards it


            //better to do this when creature  gets to the point touched  //TODO

            if (keyPressedForAction != GameKey.None)
                InputCommand.Instance.KeyUp(keyPressedForAction);
            //  ActiveSpirit.Stop();

        }


        protected Body bodyTouched = null;

    

        protected static float CalcZoomLevel(float delta)
        {
            double factor = delta / wheelFactor;

            if (factor < 0)
                factor = -1.0f / factor;    // become a divider


            Debug.WriteLine("factor for zoom" + factor);
          //  Graphics.Instance.Presentation.Camera.Zoom +=  (float)(factor / 100);

            float zoom = Graphics.Instance.Presentation.Camera.Zoom * (float)factor;
            return zoom;
        }





        void IGameCode.PreUpdatePhysics()
        {
            OnPreUpdatePhysics();

        }

        public virtual void OnPreUpdatePhysics() { }



        void IGameCode.PreUpdatePhysicsBk()
        {
            
        }


#endregion



        public virtual string Title
        {
            get { return "Title"; }
        }

        public virtual string Details
        {
            get { return "Details"; }
        }

        public virtual string Info
        {
            get { return "Info"; }
        }

        protected bool IsMousePanActive { get => isMousePanActive; set => isMousePanActive = value; }


        #endregion

    }
}
