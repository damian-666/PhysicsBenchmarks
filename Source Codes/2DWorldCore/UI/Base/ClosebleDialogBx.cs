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
    public class CloseableDialogBox : DialogBox
    {



        public Button BtnClose;

        const float btnWidth = 64;


        private float Margin = 8f;


        public Action<UIElement> Closed;
        public CloseableDialogBox(AABB bounds = default, string textureName = null, string name= null) : base(bounds, name)
        {

            if (CoreGame.IsAndroid)
            {
                this.Margin = 100;///maybe hide the system ui that gegts in the way.. sometimes it doesnt appear
            }

            if (_content == null && textureName != null)
                throw new ArgumentException("set content manager before loading textures");


            //stick a close button on top right
            BtnClose = new Button(GetRightSideButtonBounds(bounds, btnWidth)
                , "close64");

            AddElement(BtnClose);

        }


        private  AABB GetRightSideButtonBounds(AABB bounds, float butWidth)
        {
            return new AABB(butWidth, butWidth,
                            new Farseer.Xna.Framework.Vector2(bounds.UpperBound.X - butWidth- Margin, bounds.LowerBound.Y + Margin));
        }


        /// <summary>
        /// override  this to make it resizable, will get called on resize or reorient client
        /// </summary>

        public override void UpdateBounds()
        {
            Bounds = GetAABB();
            BtnClose.UpdateBounds(GetRightSideButtonBounds(Bounds, btnWidth));

        }




        public override void OnChildClicked( UIElement sender )
        {

            if (sender == BtnClose)
            {
                Hide();

                if (Closed != null)
                {
                    Closed(this);
                }

                return;
            }

            base.OnChildClicked(sender);

        }



   



    }
}
