
using FarseerPhysics.Common;
using FarseerPhysics.Collision;

using Core.Data.Geometry;

using Core.Data.Interfaces;
using Core.Game.MG.Drawing;
using Microsoft.Xna.Framework.Graphics;

using Farseer.Xna.Framework;

using Matrix = Microsoft.Xna.Framework.Matrix;

using Vector3 = Microsoft.Xna.Framework.Vector3;
using MG;

namespace Core.Game.MG.Graphics
{
    

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

        private readonly Vector2 _translateCenter;



        #region Constructor

        public WorldViewportTransform(GraphicsDevice gr)
        {
            _graphics = gr;


            _zoom = 1f;
            _windowLimit = new AABB();

            //TODO clean out the ConvertUnits.. look at view and projection actuall used.. MG_GRAPHICS

            //set SetView it doesnt use these and it works tested with mouse pan and  zoom 

            //TODO this works but makes no sense .. we dont use special display units
            // changing scale to 1 doesnt work for zoom windows.. todo see other monogame samples that have zoom and physics
            // see how they create the 2 martix for debug view

            SimProjection = Matrix.CreateOrthographicOffCenter(0f, ConvertUnits.ToSimUnits(_graphics.Viewport.Width),
                ConvertUnits.ToSimUnits(_graphics.Viewport.Height), 0f, 0f,
                1f);
            SimView = Matrix.Identity;
            View = Matrix.Identity;

            _translateCenter = new Vector2(ConvertUnits.ToSimUnits(_graphics.Viewport.Width / 2f),
                ConvertUnits.ToSimUnits(_graphics.Viewport.Height / 2f));

 

            _viewportSize = new Vector2(_graphics.Viewport.Width, _graphics.Viewport.Height);
              

        }
     

        public Matrix View { get; private set; }

        public Matrix SimView { get; private set; }

        public Matrix SimProjection { get; }



        #endregion


        #region Methods





        public Vector2 ViewportToWorld(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Unproject(t, SimProjection, SimView, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }

        public Vector2 WorldToViewport(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Project(t, SimProjection, SimView, Matrix.Identity);

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
            WindowCenter = new Vector2(aabb.Center.X, aabb.Center.Y);
            WindowSize = new Vector2(aabb.Width, aabb.Height);
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

            WindowCenter += new Vector2(viewportIncX, viewportIncY);
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




        /// <summary>
        /// Zoom using arbitrary center in viewport, to a point in world. Zoom center


        public void Reset()
        {
            // we should be able to provide different value for reset later
            WindowCenter = Vector2.Zero;
            Zoom = 1;
            WindowRotation = 0;
            SetView();


        }


        private void UpdateScale()
        {
  
        }


        /// <summary>
        /// The real update to zoom level is performed here, not in Zoom propery.
        /// This is also called from Zoom property. 
        /// So notify property for Zoom is best placed in here.
        /// </summary>
        private void UpdateZoomLevel()
        {
         

            // for zoom slider binding
            NotifyPropertyChanged("Zoom");
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
                float scale = SetWindowSize(value);

                // window size will affect center translate    
                //  TODO MG_GRAPHICS _windowCenterToOriginTranslate.X = _windowSize.X / 2;
                //  _windowCenterToOriginTranslate.Y = _windowSize.Y / 2;

                SetZoom(scale);
                FirePropertyChanged();
                NotifyPropertyChanged("Zoom");


            }
        }

        private float SetWindowSize(Vector2 value)
        {
            // To keep  aspect ratio kept, instead of entirely fit window to viewport, we
            // try to find rectangle that match viewport ratio but still large 
            // enough to fit the window inside.

            if (_limitEnabled)
            {
                Vector2 center = WindowCenter;
                ClampToLimit(ref center, ref _windowSize);
                if (WindowCenter != center)
                {
                    WindowCenter = center;
                }

            }
            float scale = GeomUtility.GetScaleToEnclose(_viewportSize, value);
            if (scale <= 0 || float.IsNaN(scale))
            {
                scale = 1;
            }
            _windowSize = _viewportSize * scale;


     

            return scale;
        }

        Vector2 center;

        /// <summary>
        /// Window center position in world coordinate. this will also become 
        /// center of rotation for window.
        /// </summary>
        public Vector2 WindowCenter
        {
            get
            {
                //   return new Vector2((float)-_worldToWindowTranslate.X,
                //                 (float)-_worldToWindowTranslate.Y);
                return center;


            }
            set
            {
                 center = value;
                if (_limitEnabled)
                {
                    ClampToLimit(ref center, ref _windowSize);
                }

              //  _worldToWindowTranslate.X = -center.X;
              //  _worldToWindowTranslate.Y = -center.Y;

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


        private void SetView()
        {
     

            Matrix matRotation = Matrix.CreateRotationZ(_currentRotation);
            Matrix matZoom = Matrix.CreateScale(Zoom);
            Vector3 translateCenter = new Vector3(_translateCenter.ToVector2(), 0f);

            Vector3 translateBody = new Vector3(-center.ToVector2(), 0f);
    

            SimView = Matrix.CreateTranslation(translateBody) *   
                      matRotation *
                      matZoom *
                      Matrix.CreateTranslation(translateCenter);

   

            View = Matrix.CreateTranslation(translateBody) *
                   matRotation *
                   matZoom *
                   Matrix.CreateTranslation(translateCenter);
        }


        public Vector2 ConvertScreenToWorld(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Unproject(t, SimProjection, SimView, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }

        public Vector2 ConvertScreenToWorld(Microsoft.Xna.Framework.Point location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Unproject(t, SimProjection, SimView, Matrix.Identity);

            return new Vector2(t.X, t.Y);
        }


        public Vector2 ConvertScreenToWorld(Microsoft.Xna.Framework.Vector2 location)
        {
            return ConvertScreenToWorld(location.ToVector2());
        }

        public Vector2 ConvertWorldToScreen(Vector2 location)
        {
            Vector3 t = new Vector3(location.ToVector2(), 0);

            t = _graphics.Viewport.Project(t, SimProjection, SimView, Matrix.Identity);

            return new Vector2(t.X, t.Y);
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

                float newZoom = SetZoom(value);
                SetWindowSize(WindowSize / newZoom);

                NotifyPropertyChanged(nameof(Zoom));

            }
        }

        public float SetZoom( float value)
        {

            float newZoom = value;
            if (newZoom <= 0)
            {
                System.Diagnostics.Debug.WriteLine("invert  neg of  neg zoom to get Zoom scale");
                return _zoom;
            }

            // clamp value, based on allowable zoom level
            newZoom = MathUtils.Clamp(newZoom, _minZoom, _maxZoom);
            _zoom = newZoom;
            return _zoom;


        }

        public float MinZoom
        {
            get { return _minZoom; }
            set
            {
                _minZoom = value;
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
