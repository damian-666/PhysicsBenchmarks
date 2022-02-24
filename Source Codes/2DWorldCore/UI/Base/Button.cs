using FarseerPhysics.Collision;
using FarseerPhysicsView;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace _2DWorldCore.UI
{



   
    public class Button: Pressable
    {


        Texture2D texture;


        public Button(AABB bounds, string textureName = null, string name = null) :base(bounds, name)
        {


            if (_content == null&& textureName!=null)
                throw new ArgumentException("Call InitUI first");

            
            texture = _content.Load<Texture2D>(textureName);
            
        }

     

        override public void Draw(GameTime gameTime)
        {
            if (!IsVisible)
                return;

            Vector2 position;
            position.X = Bounds.LowerBound.X;
            position.Y = Bounds.LowerBound.Y;

            if (IsTouched)
            {
                position += pressOffset;
            }

             Microsoft.Xna.Framework.Vector2 texelScale = new Microsoft.Xna.Framework.Vector2(Bounds.Width / texture.Width, Bounds.Height / texture.Height);


       
            if (Parent == null)
            {
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone, null);//draws white
            }

            _batch.Draw(texture, position, null, Color.White, 0, Vector2.Zero, texelScale, SpriteEffects.None, 0f);

            if (Parent == null)
            {
                _batch.End();
            }
            //  base.Bounds

            //TODO draw in Bounds
            //  if (texture != null)
            //    DebugView.

        }

        public override void OnPressed()
        {
            base.OnPressed();

        }

       
        
        
}
}
