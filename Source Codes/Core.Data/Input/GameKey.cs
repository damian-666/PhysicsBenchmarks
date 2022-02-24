using System;
using System.Collections.Generic;
using System.Reflection;

using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using Core.Data.Plugins;
using Core.Data.Entity;

namespace Core.Data.Input
{

    /// <summary>
    ///     Virtual game key. Allow simultaneous pressing.   can be mapped to keyboard or controller 
    ///     A  pickup/drop
    ///     B action.. punch
    ///     Y raise arm.
    /// </summary>
    [Flags]
    public enum GameKey
    {
        None = 0,

        Left = (1 << 0),
        Right = (1 << 1),
        Up = (1 << 2),
        Down = (1 << 3),

        /// <summary>
        /// pickup/drop
        /// </summary>
        A = (1 << 4), //pickup/drop

        /// <summary>
        /// action.. punch, action on held item as in fire gun
        /// </summary>
        B = (1 << 5),//action.. punch
        X = (1 << 6),//raise arm.
        Y = (1 << 7),//like speed or space..
        Start = (1 << 8),
        Select = (1 << 9),

        Shoot = (1 << 10),  //not used, could be a controller trigger  if the

        MouseClick = (1 << 11),
    //   MouseDblClick = (1 << 12),   confuses the  changed /unchanged bit system .. since the update is not in sync witht he event ..
   

        CapsLock = ( 1 << 12),
        LaserSight = (1 << 13), //easter egg

        NitroBoost = ( 1 << 14) 
        // Last = (1 << 15)   //
        // add additional game key.  maximum is (1 << 31)
    }



    /// <summary>
    /// Used by plugin on OnUserInput() event handler.
    /// </summary>
    public class GameKeyEventArgs
    {
        public GameKeyEventArgs(GameKey changedKey, bool isPressed /*, PartType partType*/)
        {
            Handled = false;
            ChangedKey = changedKey;
            IsPressed = isPressed;
            //PartType = partType;
        }

        /// <summary>
        /// Default is FALSE. Set this to TRUE if this key event is handled by plugin.
        /// </summary>
        public bool Handled;

        /// <summary>
        /// The game key input that changed.
        /// </summary>
        public GameKey ChangedKey;

        /// <summary>
        /// TRUE if key is changed from OFF to ON state. FALSE otherwise.
        /// </summary>
        public bool IsPressed;


        public Vector2 ClickedPt;


        /// <summary>
        /// Object that sent this GameKey command when a device is under control.  Can be NULL.   
            /// </summary>
        public  Spirit Sender { get; set; }


        /// <summary>
        /// Optional data pass any data to the spirit being controlled to help it know how to handle input commands
        /// </summary>
        public object UserData;


   
        /// <summary>
        /// Attach point on object that this input will act upon.
        /// Default is NULL, which means input key targets the whole object.
        /// 
        /// This is commonly used when controller spirit transfers input command to another spirit.
        /// Only controller spirit knows which part of other spirit it act upon.
        /// </summary>
        public AttachPoint ControlPoint;
    }


    public class GameKeyUtils
    {
        //TODO CODE REVIEW ( FUTURE)  dont use names, use bitwise operations.
        //consider removeall?  unless need to populater UI once
        private static List<string> _gameKeyNames;
        /// <summary>
        /// Cached enum names for GameKey, can be used to iterate all game keys.
        /// This is because silverlight doesn't have Enum.GetNames().
        /// </summary>
        public static List<string> GameKeyNames
        {
            get
            {
                if (_gameKeyNames == null)
                {
                    _gameKeyNames = new List<string>();
                    Type gameKeyType = typeof(GameKey);
                    foreach (FieldInfo fi in gameKeyType.GetFields(BindingFlags.Static | BindingFlags.Public))
                    {
                        _gameKeyNames.Add(fi.Name);
                    }
                }
                return _gameKeyNames;
            }
        }
    }


}
