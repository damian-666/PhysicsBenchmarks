using FarseerPhysics.Collision;
using FarseerPhysicsView;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace _2DWorldCore.UI
{
    public class ConsoleDlg : DialogBox
    {
      

        const float yMargin = 4;

        const float xMargin = 10;

        Vector2 pos;

        Color TextColor = Color.Green;


        List<StringData> stringData = new List<StringData>();


        //start console ver client height 90 percent down the page
        float relStartPos = 0.9f;

        Vector2 margin = new Vector2(6, 0);

        float Textscale = 1f;


        float Tranparency = 1;//fonts look best not scaled i think

        public static ConsoleDlg instance;

        static public ConsoleDlg Instance { get => instance; }

        Vector2 curPos = Vector2.Zero;


        //TODO add text commands like H or i for inventory.. s for scan or somehting 
        //inventory will give the wounds

        const string outprompt = ">:";
        //TODO take anoher look at nez debug console.. put some of its code in here

        /// <summary>
        /// Make a console text like dialog near button, starting at height * start at startClientHeightScale x height
        /// this is a singleton, it can spit out text like an old adventure game..  speak to the Yndrd charactero ver his nueral resonance link or somethig
        /// </summary>
        /// <param name="startClientHeightScale"></param>
        public ConsoleDlg(float startClientHeightScale) : base()
        {
            relStartPos = startClientHeightScale;
            base.Bounds = GetAABB();

            instance = this;
        }


        public void WriteLine(string text, Color color)
        {
            WriteLine( text, Vector2.Zero, color);
        }

        public void WriteLine(string text, Vector2 pos = default, Color color = default(Color))
        {
            //Dept on height
            if (color == default(Color))
            {
                color = TextColor;
            }

            stringData.Add(new StringData(pos, outprompt + text, color));


            int numStringsFit = (int) Math.Floor(Bounds.Height / (fontHeight + yMargin) * Textscale);

            if (stringData.Count > numStringsFit) {
                stringData.RemoveAt(0); }
        }
           


        public override AABB GetAABB()
        {  
            return new AABB(_gwindow.ClientBounds.Width, _gwindow.ClientBounds.Height *( 1f-relStartPos) ,
                          Farseer.Xna.Framework.Vector2.UnitY* _gwindow.ClientBounds.Height * relStartPos);
         
        }


        public override void Draw(GameTime gameTime)
        {

            if (!IsVisible)
                return;

            pos.X = Bounds.LowerBound.X;
            pos.Y = Bounds.LowerBound.Y;
          

            //poitn claim is supposed to remove antialias artifacts but a1 .5 scale i thinkg its worse
          //  _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, RasterizerState.CullNone, null);//draws white
              _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone, null);//draws white

            //   _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, null, RasterizerState.CullNone, null);//draws white


            Color textColor;

            // draw any strings we have
            for (int i = 0; i < stringData.Count; i++)
            {

                pos.X = stringData[i].Position.X;
                 
                textColor = stringData[i].Color;
                textColor.A = (byte)(255 * Tranparency);
                _batch.DrawString(_font, stringData[i].Text, pos,
                    textColor, 0, Microsoft.Xna.Framework.Vector2.Zero, stringData[i].Scale * Textscale, SpriteEffects.None, 0);

                pos.Y += ((fontHeight + yMargin) * Textscale);
            }


            _batch.End();
            


        }



    }



}






