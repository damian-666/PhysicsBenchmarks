//using BitMiracle.LibJpeg;
using Core.Data;
using Core.Data.Geometry;
using Core.Game.MG.Drawing;
using Core.Trace;
using FarseerPhysics.Collision;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using MGCore;
using Microsoft.Xna.Framework.Graphics;

using System; 
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Core.Game.MG.Graphics
{

    public class Graphics  //TODO CODE REVIEW REFACTOR RENAME TO Graphics, erase any old code if its never going to be used.  its not saved so should be easy.
    {


        #region Constructor


        private Graphics(GraphicsDevice gr)
        {
            GraphicsDevice = gr;
        }


        private static Graphics _instance = null;
        public static Graphics Instance
        {
            get
            {
                if (_instance == null)
                    throw new Exception("call Init Graphics with a GraphicsDevice first");

                return _instance;
            }
        }


        public static void InitGraphics(GraphicsDevice gr, AABB startView)
        { 
           var inst = new Graphics(gr);
            
           InitView(gr,startView);
           _instance = inst;

        }


        public static void InitGraphics(GraphicsDevice gr)
        {
            var inst = new Graphics(gr);
            AABB startViewdefault = new AABB(gr.Viewport.Width, gr.Viewport.Height, Farseer.Xna.Framework.Vector2.Zero);

            InitView(gr, startViewdefault);
            _instance = inst;

        }

        #endregion



        static public GraphicsDevice GraphicsDevice { get; set; }
        static Dictionary<Body, Texture2D> bodyTextureMap = new Dictionary<Body, Texture2D>();
        static public Dictionary<Body, Texture2D> BodyTextureMap { get => bodyTextureMap; }


     
        static Dictionary<Body, SpriteView> bodySpriteMap = new Dictionary<Body, SpriteView>();
        static public Dictionary<Body, SpriteView> BodySpriteMap { get => bodySpriteMap; }


#if OLDWAY
        static private ConcurrentDictionary<IEntity, Tuple<UInt32[], int, int>> entityThumnailDataMap = new ConcurrentDictionary<IEntity, Tuple<UInt32[], int, int>>();
        static public ConcurrentDictionary<IEntity, Tuple<UInt32[], int, int>> EntityThumnailDataMap { get => entityThumnailDataMap; }
#endif
        static private Dictionary<IEntity, Texture2D> entityTextureMap = new Dictionary<IEntity, Texture2D>();
      /// <summary>
      /// map for legacy dress view xaml vectors to images if isshowingdress2 is false
      /// </summary>
        static public Dictionary<IEntity, Texture2D> EntityThumbnailTextureMap { get => entityTextureMap; }
        static private Dictionary<IEntity, Texture2D> entityTextureMap2 = new Dictionary<IEntity, Texture2D>();

        /// <summary>
        /// map for legacy dress view xaml vectors to monogame images if isshowingdress2 is true, simple view switch 
        /// </summary>
        static public Dictionary<IEntity, Texture2D> EntityThumbnailTextureMap2 { get => entityTextureMap2; }

        /// <summary>
        /// cache enity for level names , holding on to thumbnails , they take time to decompress
        /// </summary>
        static private ConcurrentDictionary<string, IEntity> levelProxyMap = new ConcurrentDictionary<string, IEntity>();
        static public ConcurrentDictionary<string, IEntity> LevelProxyMap { get => levelProxyMap; }

#region Methods


        static private void InitPresentation(Presentation presentation)
        {
            // default camera setting
            presentation.Camera.TrackWindowFactor = 3;
            presentation.Camera.IsKeepObjectAABBFixed = true;
            presentation.Camera.IsLazyTracking = true;

            //_presentation.Camera.TrackingZoomSpeed = 0.07f;
            //    presentation.Camera.TrackingPanSpeed = 0.25f;
        }

#endregion


    


     private static void InitView(GraphicsDevice gr, AABB startView)
    {
        GraphicsDevice = gr;

        _activePresentation = new Presentation(gr, SimWorld.Instance.Physics, startView);

        InitPresentation(_activePresentation);
          
            //TODO erase
            // set up viewport size here  GRAHICS_MG    whre did this come fromt.... Viewportsize might be the old transofrom on that old pres and cam we dont need it now, check equive in NEz
            // _activePresentation.Camera.Transform.WorldToLocalTransform = new Vector2(

            //    (float)viewport.ActualWidth, (float)viewport.ActualHeight);

        }







        static private Presentation _activePresentation;
        /// <summary>
        /// Current UI active presentation.
        ///Multiple viewport implementation is supported 
        /// </summary>
        public Presentation Presentation
        {
            get { return _activePresentation; }
        }

        /// <summary>
        /// Helper prop for _activePresentation.Camera.Transform. Always changes 
        /// depending on _activePresentation.
        /// </summary>
        public WorldViewportTransform CTransform
        {
            get { return _activePresentation?.Camera.Transform; }
        }


        /// <summary>
        /// This window can be used to limit the graphics being show.  for example, a clip can be merged with with or a large polygon
        /// do a  Union with it
        /// </summary>
        /// <returns></returns>
        public RectangleF GetActiveWindowRect()
        {
            FarseerPhysics.Collision.AABB aabb = CTransform.GetWorldWindowAABB();
            return ShapeUtility.AABBToRect(aabb);
        }

        public AABB GetActiveWindowAABB()
        {
            return CTransform.GetWorldWindowAABB();
        }





        public static Texture2D DecodeThumbnailToTexture(GraphicsDevice dev, IEntity ent)
        {
            if (ent.Thumbnail == null)
                return null;

            try
            {
                using (var stream = new MemoryStream(ent.Thumbnail))
                {
                   return Texture2D.FromStream(dev, stream);
                };
            }

            catch (Exception exc)
            {
                Debug.WriteLine("exc loading thumbnail to texture2d" + exc);
            }

            return null;
        }
        

   

    }
}



