
using FarseerPhysics.Common;
using FarseerPhysics.Collision;

using Core.Data.Geometry;

using Core.Data.Interfaces;
using Core.Game.MG.Drawing;
using Microsoft.Xna.Framework.Graphics;

using Farseer.Xna.Framework;

using Matrix = Microsoft.Xna.Framework.Matrix;

using Vector3 = Microsoft.Xna.Framework.Vector3;
using MGCore;
using System;

namespace Core.Game.MG.Graphics
{
    //TODO redo and simplify this wht the Viewport Box adapter form Monogame samples

    /// <summary>
    /// Class to represent world->window->viewport transformation on a canvas.
    /// Transform between World Coordinate and Viewport Coordinate.
    /// </summary>
    public class WorldViewportTransform : NotifyPropertyBase
    {
        protected Vector2 _viewportSize;
        protected Vector2 _windowSize;

         protected float _zoom;

 
          //just some sane default limits here , used by tool
        protected float _minZoom = 0.1f;
        protected float _maxZoom = 10000f;  

        private const float _scaleBias = 0.01f;


        protected bool _limitEnabled;
        protected AABB _windowLimit;

        GraphicsDevice _graphics;
        private float _currentRotation;

        private  Vector2 _translateCenter;

    

        #region Constructor

        public WorldViewportTransform(GraphicsDevice gr, AABB startView )
        {
            _graphics = gr;


            _zoom = 1f;
            _windowLimit = new AABB();

            //TODO clean out the ConvertUnits.. look at view and projection actuall used.. MG_GRAPHICS

            //set SetView it doesnt use these and it works tested with mouse pan and  zoom 

            ResetViewportProjection();

            Reset(startView);

     

    }


        public void ResetViewportProjection()
        {

            Projection = Matrix.CreateOrthographicOffCenter(0f, (float)_graphics.Viewport.Width,
             (float)_graphics.Viewport.Height, 0f, 0f,
                100f);  
            
            //why was 1, by demo we see 100 so we can zoom in far without having fills culled
            //TODO this might not be efficient.. should cehck perf..


         
            _translateCenter = new Vector2(_graphics.Viewport.Width / 2f,
                _graphics.Viewport.Height / 2f);


            //todo elim copy if we can
            _viewportSize = new Vector2(_graphics.Viewport.Width, _graphics.Viewport.Height);

            SetView();
        }
        

        /// <summary>
        /// apply camera pos zoom, and rotate
        /// </summary>
        public Matrix View { get; private set; }


        //project from world extents to vieport usifg as sort of dpi.. using the biggest one so aspect is preserved.
        public Matrix Projection { get; private set; }



        #endregion


        #region Methods



        public Vector2 ViewportToWorld(Microsoft.Xna.Framework.Vector2 location)
        {
            return ViewportToWorld(location.ToVector2());
        }

        public Vector2 ViewportToWorld(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Unproject(t, Projection, View, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }

        public Vector2 WorldToViewport(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Project(t, Projection, View, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }

        // note that world window obtained here doesn't include window rotation.
        // rotation must be obtained separately.


        private void GetWindowCenterAndDimension(out Vector2 c, out float w2, out float h2)
        {
            c = WindowCenter;
            w2 = _windowSize.X / 2;
            h2 = _windowSize.Y / 2;
        }
    
        public RectangleF GetWorldWindow()
        {
            Vector2 c;
            float w2;
            float h2;
            GetWindowCenterAndDimension(out c, out w2, out h2);
            return new RectangleF(c.X - w2, c.Y - h2, _windowSize.X, _windowSize.Y);
        }

        public AABB GetWorldWindowAABB()
        {
            RectangleF rect = Graphics.Instance.CTransform.GetWorldWindow();
            AABB window = ShapeUtility.RectToAABB(rect);
            return window;
        }

        public void GetWorldWindow(out float left, out float top, out float right, out float bottom)
        {
            RectangleF r = GetWorldWindow();
            left = r.Left;
            top = r.Top;
            right = r.Right;
            bottom = r.Bottom;
        }

        public void SetWorldWindow(RectangleF rect)
        {
            AABB aabb = ShapeUtility.RectToAABB(rect);
            SetWorldWindow(aabb);
     
        }

        public void SetWorldWindow(AABB aabb)
        {
           windowCenter = new Vector2(aabb.Center.X, aabb.Center.Y);
           WindowSize = new Vector2(aabb.Width, aabb.Height);//this will set the matrix on set
        }

        public void SetWorldWindow(float left, float top, float right, float bottom)
        {
            RectangleF r = new RectangleF(new Vector2(left, top), new Vector2(right, bottom));
            SetWorldWindow(r);
        }

        public void PanRelativeToWorld(float worldIncX, float worldIncY)
        {
       
            WindowCenter += new Vector2(worldIncX, worldIncY);
        }

        /// <summary>
        /// Here panning direction is relative to window orientation, so window 
        /// rotation is taken into account.
        /// </summary>
       public void PanRelativeToWindow(float viewportIncX, float viewportIncY)
        {
            //TODO
            throw new NotImplementedException();
           }


        ///// <summary>
        ///// </summary>
        ///// <param name="inc">increment to current zoom</param>
        //public void ZoomRelative(float inc)
        //{
        //    /// NOTE: this might override Zoom limit, it set zoom through WindowSize
        //    Vector2 newScale = new Vector2(
        //        (float)_windowToViewpScale.ScaleX + inc,
        //        (float)_windowToViewpScale.ScaleY + inc);

        //    if (newScale.X > 0 && newScale.Y > 0)
        //        WindowSize = _viewportSize / newScale;
        //}



    
        public void Reset()
        {
            // we should be able to provide different value for reset later
            WindowCenter = Vector2.Zero;
            Zoom = 1;
            WindowRotation = 0;
            SetView();
        }

        public void Reset(AABB worldExtents)
        {
            _currentRotation = 0;

            // we should be able to provide different value for reset later
            windowCenter = worldExtents.Center;
            WindowSize = new Vector2( worldExtents.Width, worldExtents.Height );
            //this will create view matrix


        }




        private void ClampToLimit(ref Vector2 newCenter, ref Vector2 newSize)
        {
            // first adjust size
            float limitX = _windowLimit.Width;
            float limitY = _windowLimit.Height;
            if (newSize.X > limitX || newSize.Y > limitY)
            {
                Vector2 limitSize = new Vector2(limitX, limitY);
                newSize *= GeomUtility.GetScaleToEnclosedBy(newSize, limitSize);

                // if new size still exceed limit, its definititely a no.
                if (newSize.X > limitX || newSize.Y > limitY)
                {
                    return;
                }
            }
            // next, we might need to do some panning to shift center.

            // get new corner pos
            float halfW = newSize.X * 0.5f;
            float halfH = newSize.Y * 0.5f;
            float minX = newCenter.X - halfW;
            float maxX = newCenter.X + halfW;
            float minY = newCenter.Y - halfH;
            float maxY = newCenter.Y + halfH;

            // check if outside limit, shift accordingly
            if (minX < _windowLimit.LowerBound.X)
            {
                newCenter.X += _windowLimit.LowerBound.X - minX;
            }
            else if (maxX > _windowLimit.UpperBound.X)
            {
                newCenter.X -= maxX - _windowLimit.UpperBound.X;
            }
            if (minY < _windowLimit.LowerBound.Y)
            {
                newCenter.Y += _windowLimit.LowerBound.Y - minY;
            }
            else if (maxY > _windowLimit.UpperBound.Y)
            {
                newCenter.Y -= maxY - _windowLimit.UpperBound.Y;
            }
        }

#endregion


#region Properties

       

        // set viewport size explicitly here, normally from viewport canvas
        public Vector2 ViewportSize
        {
            get { return _viewportSize; }
            set
            {
                _viewportSize = value;


                float scale = GeomUtility.GetScaleToEnclose(_viewportSize, _windowSize);
                if (scale <= 0 || float.IsNaN(scale))
                {
                    scale = 1;
                }
                WindowSize = _viewportSize * scale;

            }
        }

    

        /// <summary>
        /// Window size in world coordinate
        /// </summary> 
        public Vector2 WindowSize
        {
            get { return _windowSize; }
            set
            {
             
                    // To keep  aspect ratio kept, instead of entirely fit window to viewport, we
                    // try to find rectangle that match viewport ratio but still large 
                    // enough to fit the window inside.
                    float scale = GeomUtility.GetScaleToEnclose(_viewportSize, value);
                    
                    if (scale <= 0 || float.IsNaN(scale))
                    {
                        scale = 1;
                    }
                    _windowSize = _viewportSize * scale;


                if (_limitEnabled)
                {
                    Vector2 center = WindowCenter;
                    Vector2 clampedWindowSize = _windowSize;

                    ClampToLimit(ref center, ref clampedWindowSize);
                    
                    if (this.windowCenter != center || clampedWindowSize != _windowSize)
                    {
                        windowCenter = center;
                        scale = GeomUtility.GetScaleToEnclose(_viewportSize, clampedWindowSize);
                        _windowSize = clampedWindowSize; 
                    }
                }

 
               _zoom = 1f/scale; 
                SetView();
            }
        }

        Vector2 windowCenter;

        /// <summary>
        /// Window center position in world coordinate. this will also become 
        /// center of rotation for window.
        /// </summary>
        public Vector2 WindowCenter
        {
            get
            {
                  return windowCenter;

            }
            set
            {
                 windowCenter = value;
                if (_limitEnabled)
                {
                    ClampToLimit(ref windowCenter, ref _windowSize);
                }

                  NotifyPropertyChanged(nameof(WindowCenter));
                SetView();
            }
        }

        /// <summary>
        /// Window rotation in world coordinate. In angle degrees. Follow RotateTransform
        /// convention, positive value = window rotates clockwise.
        /// </summary> 
        public float WindowRotation
        {
            get { return _currentRotation; }
            set
            {
                    _currentRotation = value % MathHelper.TwoPi;
                    SetView();
                    NotifyPropertyChanged(nameof(WindowRotation));
            }
        }




        /// </summary> 
        public float WindowRotationRad
        {
            get { return _currentRotation; }
            set
            {
                _currentRotation = value;
                SetView();
                NotifyPropertyChanged(nameof(WindowRotationRad));
            }
        }


        private void SetView()
        {
     

            Matrix matRotation = Matrix.CreateRotationZ(_currentRotation);
            Matrix matZoom = Matrix.CreateScale(Zoom);//TODO inist zoom as 1 means view all 
            Vector3 translateCenter = new Vector3(_translateCenter.ToVector2(), 0f);//this is just the center of viewport in pixels

            Vector3 translateBody = new Vector3(-windowCenter.ToVector2(), 0f);


            View = Matrix.CreateTranslation(translateBody) *
                      matRotation *
                      matZoom
                      *
                      Matrix.CreateTranslation(translateCenter);


        }
        //TODO clean out its the same as world  to viewport...

        public Vector2 ConvertScreenToWorld(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Unproject(t,Projection, View, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }

        public Vector2 ConvertScreenToWorld(Microsoft.Xna.Framework.Point location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Unproject(t, Projection, View, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }

        public float PixelsPerMeterDiag()
        {

            Vector2 pix1 = ConvertWorldToScreen(Vector2.One);
            Vector2 pix0 = ConvertWorldToScreen(Vector2.Zero);

           return (pix1 - pix0).Length();
        }



        public float PixelsPerMeterX(bool x)
        {

            Vector2 pix1 = ConvertWorldToScreen(Vector2.One);
            Vector2 pix0 = ConvertWorldToScreen(x ? Vector2.UnitX: Vector2.UnitY);
            return (pix1 - pix0).Length();

        }


        public float PixelsPerMeter()
        {
            return Math.Max(PixelsPerMeterX(true), PixelsPerMeterX(false));

        }
        public Vector2 ConvertScreenToWorld(Microsoft.Xna.Framework.Vector2 location)
        {
            return ConvertScreenToWorld(location.ToVector2());
        }

        public Vector2 ConvertWorldToScreen(Microsoft.Xna.Framework.Vector2 location)
        {
            return ConvertWorldToScreen(location.ToVector2());
        }


        public Vector2 ConvertWorldToScreen(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Project(t, Projection, View, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }
//TOD wrong imp.. copy similar that tool uses
        /// <summary>
        /// zoom around new  center in wcs
        /// </summary>
        /// <param name="center"></param>
        /// <param name="newZoom"></param>
        public void ZoomCenter( Vector2 center, float newZoom)
        {
            //todo apply limit
            windowCenter = center;
            Zoom = newZoom;// this will set new view

        }

        /// <summary>
        /// Absolute zoom. 
        /// Single value ratio between window and viewport (simplified from WindowScale, which has 2 values). 
        /// Value must be greater than 0. Value = graphics viewport / world window, closer = larger value.
        /// Can have binding with UI.
        /// NOTE: internal _zoom value is updated on UpdateZoomLevel(), not from this property.
        /// </summary>
        public float Zoom
        {
            get { return _zoom; }
            set
            {
                float newZoom = value;
                if (newZoom <= 0)
                    return;

                // clamp value, based on allowable zoom level
                newZoom = MathUtils.Clamp(newZoom, _minZoom, _maxZoom);
                _zoom = newZoom;

                WindowSize = _viewportSize / newZoom;

                NotifyPropertyChanged(nameof(Zoom));

            }
        }


        public float MinZoom
        {
            get { return _minZoom; }
            set
            {
                _minZoom = value;

                if (Zoom < _minZoom)
                    Zoom = _minZoom;

                NotifyPropertyChanged("MinZoom");
            }
        }


        public float MaxZoom
        {
            get { return _maxZoom; }
            set {
                _maxZoom = value;       
                NotifyPropertyChanged("MaxZoom");    
            }
        }


        /// <summary>
        /// This up direction will translate to WindowRotation value. 
        /// Returned vector will be unit vector.
        /// </summary>
        public Vector2 WindowUpVector
        {
            get
            {
               // float angle = -WindowRotation;  // from CW to CCW
               // angle += 90;    // from 3 o'clock origin to 12 o'clock origin

                return GeomUtility.GetUnitVectorFromAngle(0);
            }
            set
            {
                float angle = GeomUtility.GetAngleFrom12ClockVector(value.X, value.Y);

                // Obtained angle are still calculated CCW. Negate this, as 
                // RotateTransform.Angle calculate angle in CW direction.
                WindowRotation = -angle;
            }
        }


        /// <summary>
        /// Enable or disable WindowLimit.
        /// </summary>
        public bool LimitEnabled
        {
            get { return _limitEnabled; }
            set { _limitEnabled = value; }
        }

        /// <summary>
        /// This will limit window placement in world, effectively limit zoom, pan, and rotate
        /// to a specific area. Can be used to prevent camera showing outside level boundary.
        /// TODO: currently only limit zoom and pan. rotation limit still not implemented.
        /// </summary>
        public AABB WindowLimit
        {
            get { return _windowLimit; }
            set { _windowLimit = value; }
        }

        #endregion

    }
}
