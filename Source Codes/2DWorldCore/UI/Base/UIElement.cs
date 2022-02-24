using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace _2DWorldCore.UI
{


    
     abstract public class UIElement
    {

        //TODO make a graphics version of this that uses Monogame.XNA vec..
        /// <summary>
        ///  object bounds in Viewport cordinates
        /// </summary>
        protected AABB Bounds;

        public string Name;

        public string Label;

        public bool IsVisible  = true;

        public SpriteFont Font;

        public UIElement Parent = null;

        public Color ForeGroundTextColor = Color.White;

        public Color TouchedHighlightColor  = new Color( 255, 0,0, 100);

        /// <summary>
        /// The defaut font if none is specified
        /// </summary>
        static SpriteFont _defaultFont;


      //  static private IPrimitiveBatch _primitiveBatch;
        static protected SpriteBatch _batch;
        static protected SpriteFont _font;

        static protected float fontHeight = 32;//this we measured by us.. nez has complex font measure but we dont care much


        static public ContentManager _content;

        static public GameWindow _gwindow;



        static public void InitUI(GameWindow win, GraphicsDevice gr, ContentManager cm, SpriteFont font)
        {
            _content = cm;
            _defaultFont = font;
            _font = font;

            _gwindow = win;

            _batch = new SpriteBatch(gr);

        }
        static void SetDefaultFont(SpriteFont x) { _font = x; }

        protected UIElement(AABB bounds = default(AABB), string name= null, SpriteFont font = null)
        {
            Bounds = bounds;
            Name = name;

            if (font == null)
            {
                Font = _defaultFont;
            }
        }


        public void  Show()  { IsVisible = true; }
        public void Hide() { IsVisible = false; }
        /// <summary>
        /// at  least draw something
        /// </summary>
        /// <param name="gameTime"></param>
        public abstract void Draw(GameTime gameTime);



        /// <summary>
        ///Override to handle mouse down or single touch pointer  down
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>true if handled</returns>
        public virtual bool OnPointerDown(Vector2 pos)   { return false; }


        /// <summary>
        ///Override to handle mouse down or single touch pointer up
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>true if handled</returns>
        public virtual bool OnPointerUp(Vector2 pos)    { return false;     }



        /// <summary>
        /// override this and base results  from Game.window.Client area to allowUI to  be resized live
        /// </summary>
        /// <returns></returns>
        public virtual AABB GetAABB() { return Bounds; }



        public virtual void UpdateAABB() { Bounds = GetAABB(); }

    }
}
