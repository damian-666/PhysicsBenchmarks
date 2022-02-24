using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace _2DWorldCore.UI
{

    /// <summary>
    /// draws a row of remaining guys arcade sytle
    /// </summary>
    public class ManCounter: UIElement
    {

        /// <summary>
        /// number of guys left.. NumberIcons-- to decrease
        /// </summary>
        public int NumberIcons;

        /// <summary>
        /// space in pixels between guys
        /// </summary>
        public int MarginX;


        Texture2D texture;


      //  bool drawBorder = false; // for debug

        public ManCounter(AABB bounds, int numberIcons, Texture2D texture, string name = null):base(bounds,name)
        {
            NumberIcons = numberIcons;
            this.texture = texture;
        }


        /// <summary>
        /// Draw a bunch of icons to represent a count of thigs liek men ( lives) left, 80s game like
        /// </summary>
        /// <param name="bounds">location in pixel, upper left to lower right, increasing Y goes down the screen</param>
        /// <param name="name">optional name this control to find it later</param>
        /// <param name="cm"></param>
        /// <param name="textureName"></param>
        public ManCounter(AABB bounds ,string name = null, ContentManager cm=null, string textureName = null):base(bounds, name)
        {

            if (!string.IsNullOrEmpty(textureName))
            {
                texture = cm.Load<Texture2D>(textureName);
            }

        }


        public override void Draw(GameTime gameTime)
        {

            float X =  base.Bounds.LowerBound.X;
            float Y = base.Bounds.LowerBound.Y;
            for (int i  = 0;  i <NumberIcons; i++)
            {
                DrawOne(X, Y);

                X += texture.Width + MarginX;
            }
        }

        public void DrawOne(float X, float Y)
        {


          
        }
    }

}
