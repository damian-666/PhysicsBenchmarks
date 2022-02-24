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
    /// Immediate draw UI for Monogame, modelled a bit like wpf but very light no notification 
    /// Existing UI fraemworks way were too developed and heavy
    /// </summary>
    public class DialogBox : UIElement
    {

        protected List<UIElement> UIElements = new List<UIElement>();

        const float Margin = 16;


        public Action<UIElement> ChildClicked;



        public DialogBox(AABB bounds = default, string textureName = null, string name= null) : base(bounds, name)
        {

            if (_content == null && textureName != null)
                throw new ArgumentException("set content manager before loading textures");

        }


        protected IEnumerable< Pressable> GetPressables()
        {

            foreach( Pressable p in UIElements )
            {
                if (p is Pressable)
                    yield return p as Pressable;
            }
        }

        public  IEnumerable<CheckBox> GetCheckBoxes()
        {

            foreach (UIElement p in UIElements)
            {
                if (p is CheckBox)
                    yield return p as CheckBox;
            }
        }

        private static AABB GetRightSideButtonBounds(AABB bounds, float butWidth)
        {
            return new AABB(butWidth, butWidth,
                            new Farseer.Xna.Framework.Vector2(bounds.UpperBound.X - butWidth- Margin, bounds.LowerBound.Y + Margin));
        }



        /// <summary>
        /// override  this to make it resizable, will get called on resize or reorient client
        /// </summary>
        public virtual void UpdateBounds()
        {
            Bounds = GetAABB();
         
        }

 
        
        protected void AddElement(UIElement val)        {

            val.Parent = this;


            UIElements.Add(val);


            if (val as Pressable  != null ) 
            {
                ((Pressable)val).Clicked += OnChildClicked;
            }

        }

        public override bool OnPointerDown(Vector2 pos)
        {
           
            base.OnPointerDown(pos);

            if (!IsVisible)
                return false;

            foreach (var x in UIElements)
            {
                { 
                    if ( x.OnPointerDown(pos))  
                      return true; 
                };
            }

            return false;


        }

        public override bool OnPointerUp(Vector2 pos)
        {   
            base.OnPointerUp(pos);

            if (!IsVisible)
                return false;

            foreach (var x in UIElements)
            {
                {
                    if (x.OnPointerUp(pos))
                        return true;
                };
            }

            return false;


        }


        public override void Draw(GameTime gameTime)
        {

            if (!IsVisible)
                return;

            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone, null);


            //to draw a rect around it


            foreach ( var x in UIElements)
            {

                if (x.IsVisible)
                {
                    x.Draw(gameTime);
                }
            }

            _batch.End();

        }

        public virtual void OnChildClicked( UIElement sender )
        {

            if (ChildClicked != null)
                ChildClicked(sender);

        }



        public override AABB GetAABB() => GetClientAABB();

        public  AABB GetClientAABB()
        {
            if (_gwindow == null)
                throw new Exception("must set game window first");
            
            return new AABB(_gwindow.ClientBounds.Width, _gwindow.ClientBounds.Height, Farseer.Xna.Framework.Vector2.Zero);
        }




    }
}
