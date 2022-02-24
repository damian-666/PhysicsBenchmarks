using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;

using Vector2 = Farseer.Xna.Framework.Vector2;
using MGGame = Microsoft.Xna.Framework.Game;

namespace Core.Game.MG.Drawing
{
    /// <summary>
    /// Base class for visible objects , has draworder
    public class ObjectView : DrawableGameComponent
    {


        //protected RotateTransform _rotateTransform; //todo remove these.. they are needless copies..og==PHONE  
        //protected ScaleTransform _scaleTransform;
        protected float _x, _y, _rota, _sx, _sy;
        protected bool _isVisible;  //TODO cleanup .. extra state.  unless needed for culling.  //TODO PHONE check.. migth be needed.. also cna have a ref to model.. not need a map..?

        protected byte _r, _g, _b, _a;  //NOTE  this should just be a 32 bit int.    //TODO .. not needed.. just just the body color apply direclty to this. TODDO remove it.


        /// <summary>
        /// To be implemented by derived class. 
        /// Currently only polygon and circle implement this property.
        /// </summary>
        //public virtual Brush Fill { get; set; }  //todoh shader.. tecture  move to derives class, lines dont need this

        //public virtual Brush Stroke { get; set; }

        public float LineThickness;
        public bool IsVisible = true;

        // derived class should reimplement this 
 
        protected virtual void OnUpdate() { }

        public ObjectView( MGGame game):base(game)
        {
            Initialize();

        }

  

        public virtual void Clear()
        {
        
        }


        public void Update(float x, float y, float rotation, bool isVisible, bool useDress2, bool showSplineFit= false)
        {
#if SILVERLIGHT  //consider  autogen of MipMap in monogame instead of this custom stuff

            if (this is GeneralObjectView) //only needed for dress. TODO .. move this to zoom or scale change .. not every update.
            {
                CreateBitmapCache();
            }
#endif


            if (isVisible)   //performance improvement keep stuff out of viewport out of visual tree.  
            {
#if SILVERLIGHT
                UIElement content = GetUIElement(useDress2 ? 1 : 0);
                SetContentToUIElement(content);//doesn't work in tool..

#elif WINDOWS_UWP
                   SetContentToUIElement(useDress2 ? _content2 : _content);
#elif WPF
                PolygonObjectView pv = this as PolygonObjectView;

                 if (showSplineFit && pv != null)
                    pv.ShowFitSpline = true;

                SetContentToUIElement(useDress2 || showSplineFit ? _content2 : _content); 
#endif
            }

                Update(x, y, rotation, isVisible);
        }


        public void Update(float x, float y, float rotation, bool isVisible)
        {
            
            OnUpdate();

        }



#if GRAPHICS_MG
        /// <summary>
        ///  Store bitmaps at multiple zoom ranges.  This is to  prevent missing pixel on zoomed far out, if we just cached one high resolution bitmap,
        ///   that is the result bad resampling in SL..  it just skips pixels.. 
        /// </summary>
        /// <param name="dpm"> image resolution , dots per meter </param>
        public virtual void CreateBitmapCache()
        {

            double zoomFactor = Graphics.Instance.Presentation.Camera.Transform.Zoom;

            //tested zoomed in middle and out, zoomed out no longer missing part of hand, looks crisp.
            //SL 5 does not have high quality downsampling, just omits pixels.  same in windows phone SL .. not as bad tho




            if (zoomFactor > 200)  //use zoom bins to choose a resample factor, dots per meter
            {
                CacheBitmapForZoomInterval(ZoomFactorToDpm(200));
            }
            else  //This three bins are use most often..   TODO make sure they are tuned , stay tuned.. at different screen sizes , devices.    check zoomnig with slider and up and down.
            if (zoomFactor > 60)         //smoother zooms .. every time we hit a new zoom range we cache all the bitmaps for this factor.
            {
                CacheBitmapForZoomInterval(ZoomFactorToDpm(80));
            }
            else
            if (zoomFactor > 40)   // not needed really ?
            {
                CacheBitmapForZoomInterval(50);
            }
            else
             if (zoomFactor > 30) // very fast low ram to do this lower zooms.  tiny pictures..  really bad resampling there, and low speed, memory needs
            {
                CacheBitmapForZoomInterval(ZoomFactorToDpm(30));
            } /*else

           
            if (zoomFactor > 20) // very fast low ram to do this lower zooms.  tiny pictures..  really bad resampling there, and low speed, memory needs
            {
                CacheBitmapForZoomInterval(ZoomFactorToDpm(20));
            }
            else  // below current minimum of 20 ,  future proof..
            {
                CacheBitmapForZoomInterval(ZoomFactorToDpm(10));
            }*/


        }

        private static int ZoomFactorToDpm(double zoomFactor)
        {

  
        }

#endif




    public void UpdateColor(Color color)
        {
          
        }


        public void SetColor(BodyColor bodyColor)
        {
            UpdateColor(bodyColor.R, bodyColor.G, bodyColor.B, bodyColor.A);
        }

        public void UpdateColor(byte r, byte g, byte b, byte a)
        {
        
        }

    }


}
