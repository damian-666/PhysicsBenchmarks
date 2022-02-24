
using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace _2DWorldCore.UI
{

    //class that activated when touched and release with release poitn stil over the items, so they can slide away and bunt without activating

    public abstract class Pressable : UIElement
    {


        /// <summary>
        /// has been touched in its bouds bout not yet released to activate
        /// </summary>
        protected bool IsTouched = false;

        //how much to offfset it when being pressed
        protected Vector2 pressOffset = new Vector2(4, 4);

        Vector2 hitBoxMargin = new Vector2(6, 6);

        AABB hitBounds;


        public Action<UIElement> Clicked;



        protected Pressable(AABB bounds, string name) : base(bounds, name)
        {
            //y goes down
            UpdateBounds(bounds);
        }

        public virtual void UpdateBounds(AABB newBounds)
        {
            Bounds = newBounds;
            hitBounds = newBounds.Expand(-hitBoxMargin.X, -hitBoxMargin.Y, hitBoxMargin.X, hitBoxMargin.Y);
        }

        /// <summary>
        /// override pointer down event 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>ture if handled</returns>
        public override bool OnPointerDown(Vector2 pos)
        {
            if (!IsVisible)
                return false;

            if (hitBounds.Contains(pos.X, pos.Y))
            {
                IsTouched = true;
            }

            return false;
        }

        /// <summary>
        /// Hook for when but this element is pressed, usually happens when pressed and then released from inside
        /// </summary
        public virtual void OnPressed()
        {
            if (Clicked != null)
            {
                Clicked(this);
            }
        }


        /// <summary>
        /// override pointer up event
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>ture if handled</returns>
        public override bool OnPointerUp(Vector2 pos)
        {
            if (IsTouched && hitBounds.Contains(pos.X, pos.Y))// allow bunting
            {
                IsTouched = false;
                OnPressed();
                return true;
            }

            IsTouched = false;

            return false;
        }
    }
}
