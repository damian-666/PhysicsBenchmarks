using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Core.Game.MG.Graphics
{//this looks liek gthe easy one

  //  https://www.gamedev.net/forums/topic/636883-xna-generate-mip-maps-with-texturefromstream/#entry5018728
 /*   public class simplerendertargetMipmap
    {
        static Texture2D MakeMipMaps(byte[] png)
        {

            // Input Image is a byte[] from your Png
            System.IO.MemoryStream memoryStream = new System.IO.MemoryStream(png);
            memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
            Texture2D intermediateTexture = Texture2D.FromStream(deviceAccessor.GetCurrentGraphicsDevice(), memoryStream);

            Texture2D texture = null;
            RenderTarget2D renderTarget = new RenderTarget2D(deviceAccessor.GetCurrentGraphicsDevice(), InputImageSize.Width, InputImageSize.Height, mipMap: true, preferredFormat: surfaceFormat, preferredDepthFormat: DepthFormat.None);

            BlendState blendState = BlendState.Opaque;

            currentGraphicsDevice.SetRenderTarget(renderTarget);
            using (SpriteBatch sprite = new SpriteBatch(currentGraphicsDevice))
            {
                sprite.Begin(SpriteSortMode.Immediate, blendState, samplerState, DepthStencilState.None, RasterizerState.CullNone,
                effect: null);
                sprite.Draw(this.IntermediateTexture, new Vector2(0, 0), Color.White);
                sprite.End();
            }

            texture = (Texture2D)renderTarget;
            currentGraphicsDevice.SetRenderTarget(null);
            intermediateTexture.Dispose();
            return texture;
        }
    }
 */
    //NOTE might not work all platfromslie driod


    /// <summary>
    /// Extensions to the Microsoft.Xna.Framework.Content.ContentManager class.
    /// </summary>
    static class CContentManager
    {

        /// <summary>
        /// Loads the asset asynchronously on another thread.
        /// Only one type of asset can be loaded, but can be multiple instances of the same type.
        /// </summary>
        /// <typeparam name="T">The type of the asset to load</typeparam>
        /// <param name="contentManager">The content manager that will load the asset</param>
        /// <param name="assetName">Array containint the paths and names of the assets (without the extension) relative to the root directory of the content manager</param>
        /// <param name="action">Callback that is called when the asset is loaded</param>
        public static void Load<T>(this ContentManager contentManager, List<string> assetName, Action<List<T>> action)
        {


            IGraphicsDeviceService serv = contentManager.ServiceProvider.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;

            List<T> assests = new List<T>();
            //load assets 
            for (int i = 0; i < assetName.Count; i++)
            {

                assests.Add(contentManager.Load<T>(assetName[i]));
            }



        }
        /// <summary>
        /// Load single asset into the content manager, using a worker thread
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="contentManager"></param>
        /// <param name="assetName"></param>
        /// <param name="action"></param>
        public static void Load<T>(this ContentManager contentManager, string assetName, Action<T> action)
        {


            T assests;
            //load assets

            assests = contentManager.Load<T>(assetName);




        }

        /*
        /// <summary>
        /// Load a texture into the content manager with a worker thread, using Cpu mipmap generation
        /// </summary>
        /// <param name="contentManager"></param>
        /// <param name="assetName"></param>
        /// <param name="action"></param>
        public static void LoadTexture(this ContentManager contentManager, string assetName, Action<Texture2D> action)
        {

            Texture2D assest;
            //load assets

            assest = BuildMipMapCPU(contentManager.Load<Texture2D>(assetName));



        }
        
        
        
       
        /// <summary>
        /// Load textures into the content manager with a worker thread, using Cpu mipmap generation
        /// </summary>
        /// <param name="contentManager"></param>
        /// <param name="assetName"></param>
        /// <param name="action"></param>
        public static void LoadTextures(this ContentManager contentManager, List<string> assetName, Action<List<Texture2D>> action)
        {


            IGraphicsDeviceService serv = contentManager.ServiceProvider.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;

            List<Texture2D> assests = new List<Texture2D>();
            //load assets 

            for (int i = 0; i < assetName.Count; i++)
                assests.Add(BuildMipMapCPU(contentManager.Load<Texture2D>(assetName[i])));





        }
        /// <summary>
        /// generates mipmap for a texture
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private static Texture2D BuildMipMapCPU(Texture2D original)
        {
        //no source posted
            return CMipMapGenerator.AddMipMaps(original);
        }*/

       

        /// <summary>
        /// Generates a mipmap for a texture with the gpu, using rendertargets, might not work in all graphics cards
        /// </summary>
        /// <param name="contentManager"></param>
        /// <param name="original"></param>
        /// <param name="gd"></param>
        /// <param name="sp"></param>
        /// <param name="rt"></param>
        /// <returns></returns>
        public static Texture2D BuildMipMapGPU(this ContentManager contentManager, Texture2D original, GraphicsDevice gd,
                                                SpriteBatch sp, RenderTarget2D rt)
        {
            // create mip mapped texture
            SamplerState oldSS = gd.SamplerStates[0];
            RasterizerState oldrs = gd.RasterizerState;

            SamplerState newss = SamplerState.LinearClamp; // todo which is best?

            gd.SetRenderTarget(rt);

            sp.Begin(SpriteSortMode.Immediate, BlendState.Opaque, newss, DepthStencilState.None, RasterizerState.CullNone, effect: null);
            sp.Draw(original, new Vector2(0, 0), Color.White);
            sp.End();

            gd.SetRenderTarget(null);

            gd.DepthStencilState = DepthStencilState.Default;
            gd.BlendState = BlendState.Opaque;
            gd.SamplerStates[0] = oldSS;
            gd.RasterizerState = oldrs;

            // since rendertarget textures are volatile (contents get lost on device) we have to copy data in new texture
            Texture2D mergedTexture = new Texture2D(gd, original.Width, original.Height, true, SurfaceFormat.Color);
            Color[] content = new Color[original.Width * original.Height];

            for (int i = 0; i < rt.LevelCount; i++)
            {
                int n = rt.Width * rt.Height / ((1 << i) * (1 << i));
                rt.GetData<Color>(i, null, content, 0, n);
                mergedTexture.SetData<Color>(i, null, content, 0, n);
            }

            return mergedTexture;
        }
    }
    
}

        
  
