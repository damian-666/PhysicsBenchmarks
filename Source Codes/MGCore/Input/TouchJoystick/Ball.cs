using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Touch.Joystick.GameObjects
{
    public class Ball
    {
        public Texture2D BallTexture;

        public Texture2D RingTexture;
        public Vector2 Position;

        public Vector2 StartLocation;

        static public float Scale = 0.6f;
        public float Speed { get; set; }
        public Ball(ContentManager CM)
        {
            BallTexture = CM.Load<Texture2D>("joysticvirtwholeInnerXp");
            RingTexture = CM.Load<Texture2D>("joysticvirtwholeOuterxp");
            Position = new Vector2(TouchPanel.DisplayWidth / 2, TouchPanel.DisplayHeight / 2);
                
        }

        public void Draw(SpriteBatch batch)
        {
            batch.Draw(RingTexture, StartLocation+ RingTexture.Bounds.Size.ToVector2()*Scale/2f, color: Color.White, rotation: 0f, origin: 
                new Vector2(RingTexture.Width , RingTexture.Height)
                , scale: Scale * Vector2.One, effects: SpriteEffects.None, layerDepth: 0f, sourceRectangle: null);
            batch.Draw(BallTexture, Position + BallTexture.Bounds.Size.ToVector2() *Scale/ 2f, color: Color.White, rotation: 0f, origin: 
                new Vector2(BallTexture.Width , BallTexture.Height ), scale: Scale* Vector2.One, effects: SpriteEffects.None, layerDepth: 0f, sourceRectangle: null);
        }
  
    }
}