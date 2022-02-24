using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;

using Core.Data.Input;
using Core.Game.MG.Graphics;
using Farseer.Xna.Framework;
using MGCore;
using Microsoft.Xna.Framework.Input;
using Touch.Joystick.Input;
using UndoRedoFramework;
using Key = Microsoft.Xna.Framework.Input.Keys;





namespace Core.Game.MG.Simulation  //  OR Core.Game.MG.Simulation if in conflcit wiht tool , wpf   depends on how we integrate MG in tool
{
    public class InputCommand
    {

        private static float stickToArrowsDeadzone = Input.DEFAULT_DEADZONE;
        #region Constructor

        private static InputCommand _instance;

        public static InputCommand Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new InputCommand();
                }

                return _instance;
            }
        }

        protected InputCommand()
        {
            _inputMapping = new Dictionary<Key, GameKey>();
            InitDefaultKeyMapping();
        }

        #endregion


        #region Methods

        /// <summary>
        /// Set system-wide game key down event using keyboard key.
        /// Return mapped game key, or GameKey.None if current keyboard key not mapped.
        /// </summary>
        public GameKey KeyDown(Key k)
        {
            GameKey gk;
            if (_inputMapping.TryGetValue(k, out gk) == true)
            {
                KeyDown(gk);
                return gk;
            }
            return GameKey.None;
        }


        /// <summary>
        /// Set system-wide game key down event directly. There's no key mapping here.
        ///  THINK  this is like KeyPressed.. checks prior state and sends mouse event to plugin  
        ///  so we use this to simulate keypressed with touch. gameapd or monogame polled .  
        ///  Plugins handle GameKey mapped key events.  TODO verify this and test
        /// </summary>
        public void KeyDown(GameKey gk)
        {
            _keyState |= gk;
        }


        /// <summary>
        /// Set system-wide game key up event using keyboard key.
        /// Return mapped game key, or GameKey.None if current keyboard key not mapped.
        /// </summary>
        public GameKey KeyUp(Key k)
        {
            GameKey gk;
            if (_inputMapping.TryGetValue(k, out gk) == true)
            {
                KeyUp(gk);
                return gk;
            }
            return GameKey.None;
        }


        /// <summary>
        /// Set system-wide game key up event directly. There's no key mapping here.
        /// </summary>
        public void KeyUp(GameKey gk)
        {
            _keyState &= ~gk;
        }

        public void SetVibration(float left, float right)
        {
            if (Input.GamePads.Length > 0)
            {
                GamePad.SetVibration(0, left, right);
            }
        }

        /// <summary>
        /// Check state of system-wide single game key.
        /// </summary>
        public bool IsGameKeyDown(GameKey gk)
        {
            return IsGameKeyDown(gk, _keyState);
        }


        int ticks = 0;

        int clickStart = 0;

        /// <summary>
        /// number of ticks or frames.. its variable based on physcis speed ou have to click faster at faster rate
        /// </summary>
        public int DoubleClickDelay = 160;

        public static bool IsBKInputUpudate = true;




        public Microsoft.Xna.Framework.Vector2 firstTouchPos;
        public int doubleTapStart = -1000;

      //  public Microsoft.Xna.Framework.Vector2 firstStickTapPos;
        public int doubleStickTapStart = -1000;
        public int StickTapCount = 1;
        public void Update()
        {
            ticks++;
            Input.Update();
            
            foreach (KeyValuePair<Keys, GameKey> kvp in _inputMapping)
            {
                if (Input.IsKeyPressed(kvp.Key))
                    KeyDown(kvp.Value);

                if (Input.IsKeyReleased(kvp.Key))
                    KeyUp(kvp.Value);  

            }



            if (Input.DualTouchStick != null &&Input.DualTouchStick.IsActive())
            {
                stickToArrowsDeadzone = 0.3f;
            }


            if (Input.DualTouchStick == null || !( Input.DualTouchStick.BothActive()))
           //     || ( DualTouchStick.OneStick && Input.DualTouchStick.IsActive())))//if the sticks are in use dont act like a mouse.. can still pojnt in middle outside stick areas
            {


          
             
                if (Presentation.Instance != null)
                {
                    //plugins expect mouse pos in canvas points which is world
                    InputCommand.Instance.mousePos = Presentation.Instance.Camera.Transform.ViewportToWorld(Input.MousePosition);

               //     Debug.WriteLine("mouse wcs" + InputCommand.Instance.mousePos);
                }



                //make touch act like mouse for hand , pointing controll TODO how to shift..   we could check if last two are near..
                //put shift if 2 near finger  pressed.. its not important ot scratch our balls tho.. currently 
                if (Input.Touch.IsConnected)
                {


                    int touchCount = Input.Touch.CurrentTouches.Count;

                    if (Input.Touch.CurrentTouches.Count > 0)
                    {

                        //current mous pos
                        var latestTouchPos = Input.Touch.CurrentTouches[touchCount - 1].Position;
                        const float tooClosePix = 100;

                        if (Input.DualTouchStick != null && Input.DualTouchStick.IsActive())
                        {


                            if ((Input.DualTouchStick.LeftStick.StartLocation - latestTouchPos).LengthSquared() < tooClosePix * tooClosePix)
                            {     
                                Debug.WriteLine("touchtooclose " + latestTouchPos + " " +Input.DualTouchStick.LeftStick.StartLocation);



                                if (touchCount == Input.Touch.PreviousTouches.Count + 1)
                                {
                                    if (ticks - doubleStickTapStart < DoubleClickDelay/2)
                                    {
                                        StickTapCount++;
                                        Debug.WriteLine("stick" + StickTapCount);

                                    }
                                    else
                                    {
                                        doubleStickTapStart = ticks;
                                        StickTapCount = 1;
                                    }
                                }
                                return;
                            }
                           
                          }

                        if (Presentation.Instance != null)
                        {
                            //plugins expect mouse pos in canvas points which is world
                            //like mouse move. but multitouch.. newest touch
                            InputCommand.Instance.mousePos = Presentation.Instance.Camera.Transform.ViewportToWorld(latestTouchPos);
                          //  Debug.WriteLine("touch newest wcs to InputCommand mousepos" + InputCommand.Instance.mousePos);
                        }


                        if (touchCount == Input.Touch.PreviousTouches.Count + 1) //we may have a second touch.. see if loc is near last one and in time span
                        {
                            KeyDown(GameKey.MouseClick);

                            InputCommand.Instance.LastMouseClickedPos = InputCommand.Instance.mousePos;

                         //   Debug.WriteLine("touch at " + latestTouchPos);

                            if ((latestTouchPos - firstTouchPos).Length() < 20 && ticks - doubleTapStart < DoubleClickDelay) ///near doube tap
                            {
                                InputCommand.Instance.ClickCount++;
                         //       Debug.WriteLine(" double.. new ClickCount " + InputCommand.Instance.ClickCount);

                            }
                            else
                            {
                                firstTouchPos = latestTouchPos;//new start pos, just a click.
                                InputCommand.Instance.ClickCount = 1;
                                doubleTapStart = ticks;//restart timer, 
                            }

                        }
                     
                    }

                  
                     if (Input.Touch.PreviousTouches.Count == touchCount + 1) //we have a release..
                    {

                        var prevlastTouchPos = Input.Touch.PreviousTouches[touchCount].Position;
                        Debug.WriteLine("rel touch at " + prevlastTouchPos);

                        KeyUp(GameKey.MouseClick);
                        if (StickTapCount>1)
                        {
                            StickTapCount = 1;

                        }

                        return;
                    }


                }

                //TODO right here touch can emulate mouse.. but we handles touch down and up in app.. maybe defeat that..
                //dont interfere tho with picch and pick..  if left active mouse can operate on right..  see exclusion zoone ect..
                //  if touch near last joystick pos thne its a double tap  dont havettotouch joyatick but make it work on winand fix itttmaybe exclusionn zonee,,adjustit
                //test on windows too
                if (Input.LeftMouseButtonPressed || Input.RightMouseButtonPressed)
                {
                    KeyDown(GameKey.MouseClick);

                    if (clickStart != 0)
                    {
                        if (ticks - clickStart < DoubleClickDelay)
                        {
                            InputCommand.Instance.ClickCount++;
                        }
                        else
                        {
                            clickStart = 0;//restart timer, missed window
                            InputCommand.Instance.ClickCount=1;
                        }
                    }
                    else
                    { 
                        clickStart = ticks;//start timer.  ///note might be simpler liek above.. leaving for now
                    }
                }else 
                if (clickStart > 0 && (ticks - clickStart > DoubleClickDelay))//NOTE this clause might not be needed
                {
                    clickStart = 0;//reset the clicsk so wont be stuck tracking
                    InputCommand.Instance.ClickCount = 0;
                }

                if (Input.LeftMouseButtonReleased || Input.RightMouseButtonReleased)
                {
                    KeyUp(GameKey.MouseClick);
                }
            }
        }


        //AIs can uses KeyStates as Commands.  Each Spirit has a KeyState.
        // THE ISSUES ARE .. MACHINE GUNS AS AUX SPIRIT OR NESTED ONES DONT GET THE KEY UP MESSAGES. ..THINGS LIKE THAT..  for now fixed by using the global methods.
        //this only means AIs cannot drive tank ustill its fixed.. probably just needed to copy the state to the controllee..



        /// <summary>
        /// Check if a game key from known key state is pressed down.
        /// This usually called to check object KeyState. 
        /// For global KeyState, better call IsGameKeyDown(GameKey) instead.
        /// </summary>
        public static bool IsGameKeyDown(GameKey gk, GameKey keyState)
        {
            if (gk == GameKey.None)
            {
                // return true if none pressed, false otherwise
                return ((keyState | gk) == gk);
            }
            else
            {
                // return true if gk pressed, false otherwise
                return ((keyState & gk) == gk);
            }
        }


        // hardcoded key mapping. this might be modifiable through dialog later.
        private void InitDefaultKeyMapping()
        {
       
            _inputMapping.Add(Key.L, GameKey.A);    // A pick or drop sword
            _inputMapping.Add(Key.J, GameKey.B);    // stab punch
            _inputMapping.Add(Key.K, GameKey.X);    // eat

            _inputMapping.Add(Key.Z, GameKey.A);   // pickup second map for left hand, right hand on arrows , dont have to move fingers 
            _inputMapping.Add(Key.C, GameKey.B);//stab punch
            _inputMapping.Add(Key.X, GameKey.X);//lift arm.. eat

            _inputMapping.Add(Key.Space, GameKey.Y);// charge modifier for left /right.. or alternate function 


            _inputMapping.Add(Key.Up, GameKey.Up);          // jump
            _inputMapping.Add(Key.Left, GameKey.Left);      // move left
            _inputMapping.Add(Key.Right, GameKey.Right);    // move right
            _inputMapping.Add(Key.Down, GameKey.Down);

            _inputMapping.Add(Key.LeftShift, GameKey.Y);// charge modifier for left /right.. or alternate function   causes trouble in tool
            _inputMapping.Add(Key.RightShift, GameKey.Y);// charge modifier for left /right.. or alternate function   causes trouble in tool

            // secondary mapping. this interferes with joint tool A/B		
            _inputMapping.Add(Key.W, GameKey.Up);
            _inputMapping.Add(Key.A, GameKey.Left);
            _inputMapping.Add(Key.D, GameKey.Right);
            _inputMapping.Add(Key.S, GameKey.Down);


            //for rearranged french keyboard.. JKL is same,  but Z is at W.  for arrows its just pickup is closer to arrows..
            _inputMapping.Add(Key.E, GameKey.Up);   //would put Z but Z is used for pickup.. its next to shift dont want ot move it all.
            _inputMapping.Add(Key.Q, GameKey.Left);
            _inputMapping.Add(Key.V, GameKey.A);  //

            _inputMapping.Add(Key.CapsLock, GameKey.CapsLock);
            _inputMapping.Add(Key.D1, GameKey.NitroBoost);  //TODO now implemented in simWorld, can move it out.
            _inputMapping.Add(Key.D2, GameKey.LaserSight);


        }


        #endregion


        #region Properties


        private GameKey _keyState;
        /// <summary>
        /// State of system-wide (catch-all) game key, as bit flag.
        /// This is global state. 
        /// Object in game can have its own KeyState, separate from this global KeyState.
        /// On OnPreUpdatePhysics, this global KeyState is assigned to ActiveSpirit.KeyState .
        /// Other non-ActiveSpirit object can receive this KeyState through Controller mechanism, 
        /// where ActiveSpirit transfer KeyState to other Spirit under its control.
        /// 
        /// This state is for digital input only. Range-based / analog input should use additional table.
        /// </summary>
        /// 




        public GameKey KeyState
        {
            get { return _keyState; }
            set { _keyState = value; }
        }


        private Dictionary<Key, GameKey> _inputMapping;
        /// <summary>
        /// Mapping between keyboard input to game key. Used by KeyUp and KeyDown event.
        /// Multiple keyboard key can be mapped to the same game key.
        /// </summary> 
        public Dictionary<Key, GameKey> InputMapping
        {
            get { return _inputMapping; }
        }

        /// <summary>
        /// simple mouse state. only store last clicked position (in world coordinate).
        /// </summary>
        public Vector2 LastMouseClickedPos { get; set; }





        private Vector2 mousePos = new Vector2();

        /// <summary>
        /// Position in WCS of mouse
        /// </summary>
        public Vector2 MousePos
        {
            get => mousePos;
        }
    

        /// <summary>
        /// to detect double clicks
        /// </summary>
        public int ClickCount;


        #endregion

        static bool inTuck = false;

        static bool inEagle = false;

        //  static bool bothArms = false;
        public static void HandleGamePad(bool virtualTouchPad = false)
        {
            if (Input.GamePads.Length == 0)
                return;

            const float boostLevel = 0.96f;  //todo make proportional, not sure if this is good..maybe skip the max to boost


            #region COMBO

            //NOTE cant check pressed at same time  too rare, we check pressed and one down
            //i THINK  our /   Instance.KeyDown is like KeyPressed.. checks prior state and sends mouse event to plugin
            //TODO check mouse pos to plugin, used to work let you scratch yourself

            //NOTE be careful changes dont break scorpion kick done somehow by both shouders at once, its could be awesome

            //COMBO both arms, or both legs maybe

            //NOTE this doesnt do  like in game left and right arrow and B key for punch but it does  a crazy combo like scorpion kick 
            // so ill leave it   TODO combos like scorpion kick are too hard you cant repro it..
            // should instictively kick out based on rotation speed and if target near.. challege will be to execut the jumb and get good footing
            //damn this game could be addciting

            if (Input.GamePads[0].IsLeftTriggerPressed(boostLevel) && (Input.GamePads[0].IsRightTriggerDown(boostLevel))
                || Input.GamePads[0].IsRightTriggerPressed(boostLevel) && (Input.GamePads[0].IsLeftTriggerDown(boostLevel))) //TODO make jump proportional to trigger
            {
                Instance.KeyDown(GameKey.Left);
                Instance.KeyDown(GameKey.Right);
            }
            else if (Input.GamePads[0].IsLeftTriggerReleased(boostLevel))
            {
                Instance.KeyUp(GameKey.Left);
            }
            else
            if (Input.GamePads[0].IsRightTriggerReleased(boostLevel))
            {
                Instance.KeyUp(GameKey.Left);
            }
            else
            if (Input.GamePads[0].IsButtonPressed(Buttons.LeftStick)
            && Input.GamePads[0].IsButtonDown(Buttons.LeftShoulder))
            {

                Instance.KeyDown(GameKey.Right);//even if this gest called it doesnt make them all pressed in plugin
                Instance.KeyDown(GameKey.Left);
                Instance.KeyDown(GameKey.Up);
                inEagle = true;
                return;
            }
            else if (inEagle &&
                   Input.GamePads[0].IsButtonReleased(Buttons.LeftStick)
                && Input.GamePads[0].IsButtonReleased(Buttons.LeftShoulder)
                )
            {
                Instance.KeyUp(GameKey.Up);
                inEagle = false;
            }
            else


              if (virtualTouchPad)

            {
                if (Instance.StickTapCount > 1)
                {
                    Instance.KeyDown(GameKey.Right);
                    Instance.KeyDown(GameKey.Left);
                    Instance.KeyDown(GameKey.Down);
                    inTuck = true;
                }
                else
               if (inTuck)//&&
                {
                    Instance.KeyUp(GameKey.Right);
                    Instance.KeyUp(GameKey.Left);
                    Instance.KeyUp(GameKey.Down);
                    inTuck = false;
                }

            }

            else
            {//TODO note might be simple to make combos happen in plugin only worry about mapping one stick or button to one gamekey
                if (Input.GamePads[0].IsButtonPressed(Buttons.LeftStick)
                  || Input.GamePads[0].IsButtonPressed(Buttons.LeftShoulder) &&
                  Input.GamePads[0].IsButtonDown(Buttons.RightShoulder)

                  || Input.GamePads[0].IsButtonPressed(Buttons.RightShoulder) &&
                  Input.GamePads[0].IsButtonDown(Buttons.LeftShoulder)
                  )
                {

                    Instance.KeyDown(GameKey.Right);
                    Instance.KeyDown(GameKey.Left);
                    Instance.KeyDown(GameKey.Down);
                    inTuck = true;
                }
                else if (inTuck &&
                       Input.GamePads[0].IsButtonReleased(Buttons.LeftStick)
                    || Input.GamePads[0].IsButtonReleased(Buttons.LeftShoulder)
                    || Input.GamePads[0].IsButtonReleased(Buttons.RightShoulder)
                    )
                {
                    Instance.KeyUp(GameKey.Right);
                    Instance.KeyUp(GameKey.Left);
                    Instance.KeyUp(GameKey.Down);
                    inTuck = false;
                }

            }

            #endregion
            if (Input.GamePads[0].IsRightTriggerPressed()) //TODO make jump proportional to trigger
            {
                Instance.KeyDown(GameKey.B);
            }
            else if (Input.GamePads[0].IsRightTriggerReleased())
            {
                Instance.KeyUp(GameKey.B);
            }

            #region jumping and movement
            if (Input.GamePads[0].IsLeftTriggerPressed()) //TODO make jump proportional to trigger
            {
            Instance.KeyDown(GameKey.Up);
            }
            else if (Input.GamePads[0].IsLeftTriggerReleased())
            {
            Instance.KeyUp(GameKey.Up);
            }



            if (Input.GamePads[0].IsLeftStickUpPressed(stickToArrowsDeadzone))
            {
                Instance.KeyDown(GameKey.Up);
            }
            else  if (Input.GamePads[0].IsLeftStickUpReleased(stickToArrowsDeadzone))
            {
                Instance.KeyUp(GameKey.Up);
            }


            if (Input.GamePads[0].IsLeftStickDownPressed(stickToArrowsDeadzone))
            {
                Instance.KeyDown(GameKey.Down);
            }
            else
            if (Input.GamePads[0].IsLeftStickDownReleased(stickToArrowsDeadzone))
            {
                Instance.KeyUp(GameKey.Down);
            }



            if (Input.GamePads[0].IsLeftTriggerPressed(boostLevel)) //TODO make jump proportional to trigger
            {
                Instance.KeyDown(GameKey.Y);
            }
            else if (Input.GamePads[0].IsLeftTriggerReleased(boostLevel))
            {
                Instance.KeyUp(GameKey.Y);
            }

            if (Input.GamePads[0].IsRightTriggerPressed()) //TODO make jump proportional to trigger
            {
                Instance.KeyDown(GameKey.B);
            }
            else if (Input.GamePads[0].IsRightTriggerReleased())
            {
                Instance.KeyUp(GameKey.B);
            }

            if (Input.GamePads[0].IsButtonPressed(Buttons.RightShoulder))
            {
                Instance.KeyDown(GameKey.B);
            }
            else if (Input.GamePads[0].IsButtonReleased(Buttons.RightShoulder))
            {
                Instance.KeyUp(GameKey.B);
            }



            //TODO probably better Dpad for inventory management .. mabye passing items to other hands or  picking up

            if (Input.GamePads[0].DpadLeftPressed)
            {
                Instance.KeyDown(GameKey.Left);
            }
            else
            if (Input.GamePads[0].DpadLeftReleased)
            {
                Instance.KeyUp(GameKey.Left);
            }
            else
            if (Input.GamePads[0].DpadRightPressed)
            {
                Instance.KeyDown(GameKey.Right);
            }
            else
            if (Input.GamePads[0].DpadRightReleased)
            {
                Instance.KeyUp(GameKey.Right);
            }
            else
             if (Input.GamePads[0].DpadUpPressed)
            {
                Instance.KeyDown(GameKey.Up);
            }
            else
            if (Input.GamePads[0].DpadUpReleased)
            {
                Instance.KeyUp(GameKey.Up);
            }
            else
            if (Input.GamePads[0].DpadDownPressed)
            {
                Instance.KeyDown(GameKey.Down);
            }
            else
            if (Input.GamePads[0].DpadDownReleased)
            {
                Instance.KeyUp(GameKey.Down);
            }


          


            if (Input.GamePads[0].IsLeftStickRightPressed(stickToArrowsDeadzone))
            {
                Instance.KeyDown(GameKey.Right);
            }
            else
            if (Input.GamePads[0].IsLeftStickRightReleased(stickToArrowsDeadzone))
            {
                Instance.KeyUp(GameKey.Right);
            }
            else
                if (Input.GamePads[0].IsLeftStickLeftPressed(stickToArrowsDeadzone))
            {
                Instance.KeyDown(GameKey.Left);
            }
            else
            if (Input.GamePads[0].IsLeftStickLeftReleased(stickToArrowsDeadzone))
            {
                Instance.KeyUp(GameKey.Left);
            }

#endregion

#region RightStick


            //right stick is typically camera
            //its handled in GameCodeBase mapped to Keys.Pageup and down
            //TODO make it lock and limit camera.. or require trigger plus camera.. see halo or other

            //TODO make it proportional to to move hand in space maybe if holding left trigger

            //TODO implement pan camera with look around
            //ADD Vector AnalogStick to GameKey state, like mouse pos

            

#if RIGHTSTICKHANDACTION



//TODO propotional aim for punch and kick

            if (Input.GamePads[0].IsRightStickUpPressed())
            {
                Instance.KeyDown(GameKey.X);
            }
            else
            if (Input.GamePads[0].IsRightStickUpReleased())
            {
                Instance.KeyUp(GameKey.X);
            }
            else
            if (Input.GamePads[0].IsRightStickDownPressed())
            {
                Instance.KeyDown(GameKey.A);
            }
            else
            if (Input.GamePads[0].IsRightStickDownReleased())
            {
                Instance.KeyUp(GameKey.A);
            }
            else
                    if (Input.GamePads[0].IsRightStickLeftPressed())
            {
                Instance.KeyDown(GameKey.B);
            }
            else
                    if (Input.GamePads[0].IsRightStickLeftReleased())
            {
                Instance.KeyUp(GameKey.B);
            }
            if (Input.GamePads[0].IsRightStickRightPressed())
            {
                Instance.KeyDown(GameKey.B);
            }
            else
            if (Input.GamePads[0].IsRightStickRightReleased())
            {
                Instance.KeyUp(GameKey.B);
            }

#endif
#endregion

            //it works ok for jumping to use max postion to boost

            if (Input.GamePads[0].IsLeftStickUpPressed(boostLevel))
            {
                Instance.KeyDown(GameKey.Y);
            }
            else
            if (Input.GamePads[0].IsLeftStickUpReleased(boostLevel))
            {
                Instance.KeyUp(GameKey.Y);
            }


#if MAXTOBOOSTWALK  //this tends to boost when you dont want to even at 0.97 threshold
            if (Input.GamePads[0].IsLeftStickLeftPressed(boostLevel))
            {
                Instance.KeyDown(GameKey.Y); // mapped to space for boost
            }
            else
            if (Input.GamePads[0].IsLeftStickLeftReleased(boostLevel))
            {
                Instance.KeyUp(GameKey.Y);
            }


            if (Input.GamePads[0].IsLeftStickRightPressed(boostLevel))
            {
                Instance.KeyDown(GameKey.Y); // mapped to space for boost
            }
            else
            if (Input.GamePads[0].IsLeftStickRightReleased(boostLevel))
            {
                Instance.KeyUp(GameKey.Y);
            }
#endif


            if (Input.GamePads[0].IsButtonPressed(Buttons.B))
            {
                Instance.KeyDown(GameKey.B);
            }
            else
            if (Input.GamePads[0].IsButtonReleased(Buttons.B))
            {
                Instance.KeyUp(GameKey.B);
            }




            if (Input.GamePads[0].IsButtonPressed(Buttons.A))
            {
                Instance.KeyDown(GameKey.A);
            }
            else
            if (Input.GamePads[0].IsButtonReleased(Buttons.A))
            {
                Instance.KeyUp(GameKey.A);
            }

            if (Input.GamePads[0].IsButtonPressed(Buttons.X))
            {
                Instance.KeyDown(GameKey.X);
            }
            else
        if (Input.GamePads[0].IsButtonReleased(Buttons.X))
            {
                Instance.KeyUp(GameKey.X);
            }


            if (Input.GamePads[0].IsButtonPressed(Buttons.Y))
            {
                Instance.KeyDown(GameKey.Y);
            }
            else
            if (Input.GamePads[0].IsButtonReleased(Buttons.Y))
            {
                Instance.KeyUp(GameKey.Y);
            }



            if (Input.GamePads[0].IsButtonPressed(Buttons.B))
            {
                Instance.KeyDown(GameKey.B);
            }
            else
            if (Input.GamePads[0].IsButtonReleased(Buttons.B))
            {
                Instance.KeyUp(GameKey.B);
            }

        }

    }
}


