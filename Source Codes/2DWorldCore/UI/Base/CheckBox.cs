using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace _2DWorldCore.UI
{
    public class CheckBox :  Pressable
    {

        public bool IsChecked = false;

        public Texture2D _t2DUnchecked;

        public Texture2D _t2Checked;


        //offset in px between right edge chekbox and its label
        Vector2 margin = new Vector2(6, 0);



        Vector2 textscale = new Vector2(1, 1);
        public CheckBox(AABB bounds, string label, string name = null, string textureUncheckedName = null, string textureCheckedName = null ) : base(bounds, name)
        {

            Label = label;

            if (_content == null && textureUncheckedName != null)
                throw new ArgumentException("call InitUI before loading textures");

            _t2DUnchecked = _content.Load<Texture2D>(textureUncheckedName);
            _t2Checked = _content.Load<Texture2D>(textureCheckedName);

            textscale.X = textscale.Y = _t2Checked.Height / fontHeight;
        }

   


        static Vector2 pos = Vector2.Zero;

        public override void Draw(GameTime gameTime)
        {

            if (!IsVisible)
                return;

            pos.X=  Bounds.LowerBound.X;
            pos.Y = Bounds.LowerBound.Y;

           if (Parent == null)
           {
              _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone, null);//draws white
           }

            _batch.Draw(IsChecked ? _t2Checked : _t2DUnchecked, pos, 
                null, IsTouched ? TouchedHighlightColor : ForeGroundTextColor , 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);


            _batch.DrawString(_font, Label,  pos + margin + new Vector2(_t2Checked.Width,0) , ForeGroundTextColor,0, Vector2.Zero,
                  textscale, SpriteEffects.None, 0);

       
            if (Parent == null)
            {
               _batch.End();
            }


        }

        public override void OnPressed()
        {
            IsChecked = !IsChecked;
            base.OnPressed();

        }


   


    }
}
