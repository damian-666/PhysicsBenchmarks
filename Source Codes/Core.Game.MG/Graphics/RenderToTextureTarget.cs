using Core.Data.Geometry;
using Core.Game.MG.Simulation;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysicsView;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Core.Game.MG.Graphics
{

    public class Rasterizer
    {
        public static void CaptureTextureForBodies(GraphicsDevice gr, List<Body> bodies, DebugView physicsView)
        {


           // float dpm = Presentation.Instance.Camera.Transform.PixelsPerMeter() ;
           ///// dpm *= 2;


            float dpm = 960; //same as thumbs.. no need to worry about currnet scale.. lets usse mipmaps and sample it in one pass

            //for particles will make a map by size round to pixel or something..

            BlendState blendState = BlendState.AlphaBlend;
            
            float worldWidthWCS = Presentation.Instance.Camera.Transform.WindowSize.X;

            float worldHeightWCS = Presentation.Instance.Camera.Transform.WindowSize.Y;

            var renderTargetViewport = gr.RenderTargetCount == 0 ? null : gr.GetRenderTargets()[0].RenderTarget;




            foreach (Body b in bodies)
            {

                if (b is Particle)
                    continue;

                if (b.IsStatic)
                    continue;

       
                if (PhysicsThread.Lockless && b.cloneOrg == null)
                {

                    Debug.WriteLine("UNEXPECGED, CLONE WO ORG REF:" + b.PartType + b.ID);
                    continue;
                };  //we gotta map to the original body not any frame clone or have dupblicates.



                var bodyAABB = b.ComputeBodySpaceAABB();   //here we assume its alinged to y or x at rot 0 for best efficiency...designers responsibilty



                ////  if (bodyAABB.Width > worldWidthWCS)
                //    continue;  //TODO later


                //  if (bodyAABB.Height > worldHeightWCS)//clip to vp or just limit res..
                //      continue;  //TODO later

                int w = (int)(bodyAABB.Width * dpm);
                int h = (int)(bodyAABB.Height * dpm);




                //TODO limit dpm if needed


                //    https://gamedev.stackexchange.com/questions/38118/best-way-to-mask-2d-sprites-in-xna/38150#38150




                if (w < 2 || h < 2)
                    continue;


                RenderTarget2D currentRenderTarget;

          

                SetNewRenderTarget(gr, w, h, out currentRenderTarget);

                WorldViewportTransform xfrom = new WorldViewportTransform(gr, bodyAABB);

                var orgxf = b.Xf;

                FarseerPhysics.Common.Mat22 mat = new Mat22(0);
                b.Xf = new FarseerPhysics.Common.Transform(Farseer.Xna.Framework.Vector2.Zero, mat);  //just use identity render in body space.. even if viewport ratio will match..


                Texture2D ClipMask = null;

                if (b.IsInfoFlagged(BodyInfo.ClipDressToGeom))

             //    if (false)


                {

                    gr.Clear(Color.Transparent);  //so alpah rtest will be draw  only less.. even white pixel outside the clear region wont draw.

                    //it can draw xparent.. only things passing will go through the traparent body maks we are drawing here

                    physicsView.DrawMask = true;//measn filll and drwa even if A i s zero

                    var prevedgeThcik = b.EdgeStrokeThickness;
                    b.EdgeStrokeThickness = 0;
                    var prevColro = b.Color;

                    b.Color = BodyColor.White;

                    BlendState blend = BlendState.AlphaBlend;


                    //need a wayh to force it to overrite the bk..

                    //see does opaue igore alpah?

                    //seeems to
                    //  blend.ColorWriteChannels = ColorWriteChannels.Alpha;
                    physicsView.RenderDebugData(xfrom.Projection, xfrom.View, b, blend);


                    ClipMask = currentRenderTarget;

                    SetNewRenderTarget(gr, w, h, out currentRenderTarget);

                    gr.Clear(Color.Transparent);

                   b.Color = prevColro;
                   b.EdgeStrokeThickness = prevedgeThcik;

                }
                else
                {

                    physicsView.RenderDebugData(xfrom.Projection, xfrom.View, b, blendState);

                }



                var orgDressState =
                b.IsShowingDress2;


                b.IsShowingDress2 = false;

                RenderDressWClipping(gr, physicsView, b, w, h, ref currentRenderTarget, ref blendState, xfrom, ClipMask);



                //we are gonna rerender this  thum at different scale .. we could make mipmaps here too..
                //because clip needs to be mabye higher res.. or we could clip using mask at same res as the other but lets lkeep it general..
                //we got emitters to cut too...
                //emitters might be nested raster... me might need to capture all that on first pass but dirty it..
                SpriteView sprite = null;

                sprite = new SpriteView((Texture2D)currentRenderTarget, bodyAABB, new Vector2(1f / dpm, 1f / dpm));


                if (b.Thumnail2 != null)
                {



                    SetNewRenderTarget(gr, w, h, out currentRenderTarget);



                    gr.Clear(Color.Transparent);
                    b.IsShowingDress2 = true;


                    if (ClipMask == null)
                    {
                        physicsView.RenderDebugData(xfrom.Projection, xfrom.View, b, blendState);
                 //   physicsView.RenderThumbnailImage(xfrom.Projection, xfrom.View, b, blendState);
                    }

                    //this check if clipping needed adn will add the thum
                    RenderDressWClipping(gr, physicsView, b, w, h, ref currentRenderTarget, ref blendState, xfrom, ClipMask);


                     sprite.Textures.Add(currentRenderTarget as Texture2D);

                     b.IsShowingDress2 = orgDressState;


                }


                sprite.CurrentFrame = b.IsShowingDress2 ? 1 : 0;


                //  https://community.monogame.net/t/solved-drawing-a-texture-over-a-mask-texture/8337/14
                //  https://community.monogame.net/t/how-to-make-lightsources-torch-fire-campfire-etc-in-dark-area-2d-pixel-game/8058/21


                var key = b.cloneOrg != null ? b.cloneOrg : b;

                if (!Graphics.BodySpriteMap.ContainsKey(key))
                    Graphics.BodySpriteMap.Add(key, sprite);
                else
                    Graphics.BodySpriteMap[key] = sprite;

                b.Xf = orgxf;
            }

            gr.SetRenderTarget(renderTargetViewport as RenderTarget2D);//th TODOus is only needed to keep setting back since.. size we take the size 


        }

        private static void RenderDressWClipping(GraphicsDevice gr, DebugView physicsView, Body b, int w, int h, ref RenderTarget2D currentRenderTarget, ref BlendState blendState, WorldViewportTransform xfrom, Texture2D ClipMask)
        {
         

        //  ffirst take the dress..
            physicsView.RenderThumbnailImage(xfrom.Projection, xfrom.View, b, blendState);

            Texture2D dressTex = null;

            if (ClipMask != null)
            {
                dressTex = currentRenderTarget;

                //so now we gotta make another render target using the alphatest
        
                //wwe should now two sprites we can compine w/o xforms..same res like two sprites

                SetNewRenderTarget(gr, w, h, out currentRenderTarget);

                  gr.Clear(Color.Transparent);
               //   physicsView.DrawTextureScreen(dressTex);
            //    physicsView.DrawTextureScreen(ClipMask);
                ///


      

                ///     physicsView.DrawTextureScreen(dressTex);
                //   physicsView.DrawTextureScreen(ClipMask);

                Effect clip = physicsView.Clipper;


                    var effectName = GetEffectParamName("", "ClipTexture");

            
                    var param = clip?.Parameters[effectName];

                 param?.SetValue(ClipMask as Texture2D);


                //note this param wlll be optimized out if usi
                //
                    effectName = GetEffectParamName("", "DrawTexture");//ng s0.. that lets us draw teh dress through...using two samplers is confusing hte system
                //either way it doesnt work on Gl bugt 9
                param = clip?.Parameters[effectName];

               param?.SetValue(dressTex as Texture2D);


             //     physicsView.DrawTextureScreen(dressTex, Vector2.Zero, clip, blendState);  //now works proper..omryimrd depednign on how samledm usng tex2 rowhat ARGGH the other work w clip as black only
               physicsView.DrawTextureScreen(ClipMask, Vector2.Zero, clip, blendState);  //why ????? we should only have to look draw the dress though the clipper..donno what batcher is doign so 
                                                                                          //this is fine our clipper will sample the whole dress..  whatever..


                Texture2D clippedDress = currentRenderTarget;







             

#if !SHADERTEST 
                SetNewRenderTarget(gr, w, h, out currentRenderTarget);



                gr.Clear(Color.Transparent);
                physicsView.RenderDebugData(xfrom.Projection, xfrom.View, b, blendState, null);  //now fill the damn thing.. we should do this first but we dont know its alpah it might be zero.
                //if we combine that wed have to use a magic bk color, like a blue screen..


                var   blend =new BlendState();
                blend.AlphaSourceBlend = Blend.SourceAlpha;

                //    blendMask.AlphaSourceBlend = Blend.DestinationAlpha; //donno any this stuff best make our own shader...
                blendState = BlendState.AlphaBlend;

                     physicsView.DrawTextureScreen(clippedDress, Vector2.Zero, null, blendState);
         //       physicsView.DrawTextureScreen(clippedDress, Vector2.Zero, null, blend);




#endif

                //  physicsView.DrawCombinedTexturesScreen(ClipMask, dressTex);  early atttempt

                // physicsView.DrawTextureScreen(dressTex, null)  //sandity tests
                //  physicsView.DrawCombinedTexturesScreen(ClipMask, dressTex);
                //  physicsView.DrawCombinedTexturesScreen(ClipMask,dressTex,a


            }


        }

        private static string GetEffectParamName(string samplerName, string textureName)
        {
             string effectnamebase = "";

            if  (!SimWorld.IsDirectX && !string.IsNullOrEmpty(samplerName))  //in open gl samplers and textures are coupled together so names are combined
            {
                effectnamebase = samplerName+ "+" ;
            }


            string effectName = effectnamebase + textureName;
            return effectName;
        }



        private static void SetNewRenderTarget(GraphicsDevice gr, int width, int height, out RenderTarget2D renderTarget)
        {
            //mip maps work this way also.. alspah channel doesnt
            renderTarget = new RenderTarget2D(gr, width, height,
           //     false,


               SimWorld.IsDirectX,  ///use mip maps in dx tho.. in gl it not working, alos cnat antialialis line w mipmap on
           

               SimWorld.IsDirectX
               ? SurfaceFormat.Bgra32
                
                :
               SurfaceFormat.Color,
            

                  DepthFormat.Depth24Stencil8,
              //  DepthFormat.None,
              0, //not suppported in GL
                 //    4,

                RenderTargetUsage.DiscardContents,


           //   RenderTargetUsage.PreserveContents, 
              true); ;
            ;

            gr.SetRenderTarget(renderTarget);
        }
    }
}