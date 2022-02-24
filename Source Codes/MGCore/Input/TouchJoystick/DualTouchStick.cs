﻿#define RIGHTSTICK
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Diagnostics;
using Touch.Joystick.Abstract;
using Touch.Joystick.GameObjects;

namespace Touch.Joystick.Input
{
    public class DualTouchStick
    {

        // How quickly the touch stick follows in FreeFollow mode
        public readonly float aliveZoneFollowSpeed;
        // How far from the alive zone we can get before the touch stick starts to follow in FreeFollow mode
        public readonly float aliveZoneFollowFactor;
        // If we let the touch origin get too close to the screen edge,
        // the direction is less accurate, so push it away from the edge.
        public readonly float edgeSpacing;
        // Where touches register, if they first land beyond this point,
        // the touch wont be registered as occuring inside the stick
        public readonly float aliveZoneSize;
        // Keeps information of last 4 taps
        private readonly TapStart[] tapStarts = new TapStart[4];
        private int tapStartCount = 0;
        // this keeps counting, no ideia why i cant reset it
        private double totalTime;

        public readonly float deadZoneSize = 5;

        Ball Ball;

        public static bool OneStick = true;



        public DualTouchStick(SpriteFont font, ContentManager cm, float aliveZoneFollowFactor = 1.3f, float aliveZoneFollowSpeed = 0.05f, float edgeSpacing = 25f, float aliveZoneSize = 45f, float deadZoneSize = 15f)
        {
            this.aliveZoneFollowFactor = aliveZoneFollowFactor;
            this.aliveZoneFollowSpeed = aliveZoneFollowSpeed;
            this.edgeSpacing = edgeSpacing;
            this.aliveZoneSize = aliveZoneSize;
            this.deadZoneSize = deadZoneSize;
            this.font = font;

            CreateTouchSticks();
            //TouchPanel.EnabledGestures = GestureType.None;
            //TouchPanel.DisplayOrientation = DisplayOrientation.LandscapeLeft;

            Ball = new Ball(cm);



        }

        public void CreateTouchSticks()
        {
            if (OneStick)
            {
                LeftStick = new Stick(deadZoneSize,
                     new Rectangle(0, 2 * TouchPanel.DisplayHeight / 3, (int)(TouchPanel.DisplayWidth), TouchPanel.DisplayHeight / 3),
                     aliveZoneSize, aliveZoneFollowFactor, aliveZoneFollowSpeed, edgeSpacing)
                {
                    FixedLocation = new Vector2(aliveZoneSize * aliveZoneFollowFactor, TouchPanel.DisplayHeight - aliveZoneSize * aliveZoneFollowFactor)
                };



            }
            else
            {
                LeftStick = new Stick(deadZoneSize,
                     new Rectangle(0, 100, (int)(TouchPanel.DisplayWidth * 0.3f), TouchPanel.DisplayHeight - 100),
                     aliveZoneSize, aliveZoneFollowFactor, aliveZoneFollowSpeed, edgeSpacing)
                {
                    FixedLocation = new Vector2(aliveZoneSize * aliveZoneFollowFactor, TouchPanel.DisplayHeight - aliveZoneSize * aliveZoneFollowFactor)
                };


                RightStick = new Stick(deadZoneSize,
                new Rectangle((int)(TouchPanel.DisplayWidth * 0.5f), 100, (int)(TouchPanel.DisplayWidth * 0.5f), TouchPanel.DisplayHeight - 100),
                aliveZoneSize, aliveZoneFollowFactor, aliveZoneFollowSpeed, edgeSpacing)
                {
                    FixedLocation = new Vector2(TouchPanel.DisplayWidth - aliveZoneSize * aliveZoneFollowFactor, TouchPanel.DisplayHeight - aliveZoneSize * aliveZoneFollowFactor)
                };
            }
        }




        public bool IsEnabled
        {
            get => isEnabled;

            set { 
                
                if (value == false)
                { 
                    LeftStick?.Deactivate(); RightStick?.Deactivate();
                }

                isEnabled = value;
            }
        }

        private  bool isEnabled = true;

        private readonly SpriteFont font;
        public Stick RightStick { get; set; }
        public Stick LeftStick { get; set; }
        public void Update(float dt)
        {
            try
            {
                if (!IsEnabled)
                    return;

                totalTime += dt;

                var state = TouchPanel.GetState();
                TouchLocation? leftTouch = null, rightTouch = null;

                if (tapStartCount > state.Count)
                    tapStartCount = state.Count;

 
                foreach (TouchLocation loc in state)
                {
                    if (loc.State == TouchLocationState.Released)
                    {
                        int tapStartId = -1;
                        for (int i = 0; i < tapStartCount; ++i)
                        {
                            if (tapStarts[i].Id == loc.Id)
                            {
                                tapStartId = i;
                                break;
                            }
                        }
                        if (tapStartId >= 0)
                        {
                            for (int i = tapStartId; i < tapStartCount - 1; ++i)
                                tapStarts[i] = tapStarts[i + 1];
                            tapStartCount--;
                        }
                        continue;
                    }
                    else if (loc.State == TouchLocationState.Pressed && tapStartCount < tapStarts.Length)
                    {
                        tapStarts[tapStartCount] = new TapStart(loc.Id, totalTime, loc.Position);
                        tapStartCount++;
                    }

                    if (LeftStick.touchLocation.HasValue && loc.Id == LeftStick.touchLocation.Value.Id)
                    {
                        leftTouch = loc;
                        continue;
                    }
                    if (RightStick != null && RightStick.touchLocation.HasValue && loc.Id == RightStick.touchLocation.Value.Id)
                    {
                        rightTouch = loc;
                        continue;
                    }

                    if (!loc.TryGetPreviousLocation(out TouchLocation locPrev))
                        locPrev = loc;

                    if (!LeftStick.touchLocation.HasValue)
                    {
                        if (LeftStick.StartRegion.Contains((int)locPrev.Position.X, (int)locPrev.Position.Y))
                        {
                            if (LeftStick.Style == TouchStickStyle.Fixed)
                            {
                                if (Vector2.Distance(locPrev.Position, LeftStick.StartLocation) < aliveZoneSize)
                                {
                                    leftTouch = locPrev;
                                }
                            }
                            else
                            {
                                leftTouch = locPrev;
                                LeftStick.StartLocation = leftTouch.Value.Position;
                                if (LeftStick.StartLocation.X < LeftStick.StartRegion.Left + edgeSpacing)
                                    LeftStick.StartLocation.X = LeftStick.StartRegion.Left + edgeSpacing;
                                if (LeftStick.StartLocation.Y > LeftStick.StartRegion.Bottom - edgeSpacing)
                                    LeftStick.StartLocation.Y = LeftStick.StartRegion.Bottom - edgeSpacing;
                            }
                            continue;
                        }
                    }
#if RIGHTSTICK
                    
                    
                    if (!OneStick && !RightStick.touchLocation.HasValue && locPrev.Id != RightStick.lastExcludedRightTouchId)
                    {
                        if (RightStick.StartRegion.Contains((int)locPrev.Position.X, (int)locPrev.Position.Y))
                        {
                            bool excluded = false;
                            foreach (Rectangle r in RightStick.startExcludeRegions)
                            {
                                if (r.Contains((int)locPrev.Position.X, (int)locPrev.Position.Y))
                                {
                                     excluded = true;
                                    RightStick.lastExcludedRightTouchId = locPrev.Id;
                                    continue;
                                }
                            }
                            if (excluded)
                                continue;
                            RightStick.lastExcludedRightTouchId = -1;
                            if (RightStick.Style == TouchStickStyle.Fixed)
                            {
                                if (Vector2.Distance(locPrev.Position, RightStick.StartLocation) < aliveZoneSize)
                                {
                                    rightTouch = locPrev;
                                }
                            }
                            else
                            {
                                rightTouch = locPrev;
                                RightStick.StartLocation = rightTouch.Value.Position;
                                if (RightStick.StartLocation.X > RightStick.StartRegion.Right - edgeSpacing)
                                    RightStick.StartLocation.X = RightStick.StartRegion.Right - edgeSpacing;
                                if (RightStick.StartLocation.Y > RightStick.StartRegion.Bottom - edgeSpacing)
                                    RightStick.StartLocation.Y = RightStick.StartRegion.Bottom - edgeSpacing;
                            }
                            continue;
                        }
                    }


#endif

                }


                LeftStick?.Update(state, leftTouch, dt);
                RightStick?.Update(state, rightTouch, dt);
            }


            catch (Exception exc)
            {
                Debug.WriteLine("update touch stick exc " + exc);
            }
        }


        public bool IsActive()
        {
            return LeftStick.touchLocation.HasValue || RightStick != null && RightStick.touchLocation.HasValue;
        }

        public bool BothActive()
        {
            return LeftStick.touchLocation.HasValue &&( RightStick!=null  && RightStick.touchLocation.HasValue);
        }

        public void Draw(SpriteBatch spriteBatch)
        {

         //   RightStick.SetAsFreefollow();//buggy
//DrawStringCentered($"L", LeftStick.StartLocation, Color.White, spriteBatch);
                if (LeftStick.touchLocation.HasValue)
            {
              //  DrawStringCentered($"L@L", LeftStick.GetPositionVector(aliveZoneSize), Color.GreenYellow, spriteBatch);

                Ball.StartLocation = LeftStick.StartLocation;
                Ball.Position = LeftStick.GetPositionVector(aliveZoneSize);

             ///   Debug.WriteLine("alivezonesize " +aliveZoneSize);
                Ball.Draw(spriteBatch);
             //   Debug.WriteLine("touch leftstick " + LeftStick.GetPositionVector(aliveZoneSize));



                Debug.WriteLine(LeftStick.Magnitude);


            }
            //      DrawStringCentered($"R", RightStick.StartLocation, Color.White, spriteBatch);

            if (RightStick!=null
                &&RightStick.touchLocation.HasValue)
            {
             //   Debug.WriteLine("r pos " + RightStick.Pos);
            }

        }
        private void DrawStringCentered(string text, Vector2 position, Color color, SpriteBatch spriteBatch)
        {
            var size = font.MeasureString(text);
            var origin = size * 0.5f;

            spriteBatch.DrawString(font, text, position, color, 0, origin, 1, SpriteEffects.None, 0);
        }
    }
}