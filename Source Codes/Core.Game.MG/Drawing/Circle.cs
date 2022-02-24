

using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using Microsoft.Xna.Framework;
using MGGame = Microsoft.Xna.Framework.Game;

namespace Core.Game.MG.Drawing
{

    public class CircleView:ObjectView
    {

      public CircleView( MGGame game):base(game) { }

#if GRAPHICS_MG
        protected Body _body = null;

    

        public Circle(float radius, double thickness)
        {
            _body = null;
            Initialize(radius, thickness);
        }


        /// <summary>
        /// Specific for CircleObjectView has Body reference, since View can know the model
        /// </summary>
        /// <param name="body">Body Model</param>
        /// <param name="radius">The Radius</param>
        /// <param name="thickness">Thickness</param>
        public CircleObjectView(Body body, float radius, double thickness)
        {
            Initialize(radius, thickness);

            _body = body;

            _scale.CenterX = radius * 0.5f;
            _scale.CenterY = radius * 0.5f;
            _scale.ScaleX = 1f;
            _scale.ScaleY = 1f;

            //_circle.RenderTransform = _scale;   this is set in the CreateEllipseShape in shapeFactor
        }


        /// <summary>
        /// set the ellipse Radius.. this will set the Width and Height to the same, and adjust the Render Transfrom to center it
        /// </summary>
        public float Radius
        {
            set
            {
                if ((float)Ellipse.Width * 0.5f != value)
                {
                    SetEllipseRadius(value, value, Ellipse);
                }
            }

            get
            {
                return (float)Ellipse.Width * 0.5f;
            }
        }
        

        private void Initialize(float radius, double thickness)
        {
            _content = ShapeFactory.CreateCircleShape(radius, thickness);
            this.Children.Add(_content);
        }


        /// <summary>
        /// This one to create Ellipse. 
        /// More straightforward than re-creating shape on derived class. 
        /// Ideally Ellipse ObjectView should be the base class of Circle ObjectView
        /// </summary>
        /// <param name="body">Body Model</param>
        /// <param name="radius">The Radius</param>
        /// <param name="thickness">Thickness</param>
        public CircleObjectView(Body body, float radiusX, float radiusY, double thickness)
        {
            InitializeEllipse(radiusX, radiusY, thickness);

            _body = body;

            _scale.CenterX = radiusX * 0.5f;
            _scale.CenterY = radiusY * 0.5f;
            _scale.ScaleX = 1f;
            _scale.ScaleY = 1f;
        }


        private void InitializeEllipse(float radiusX, float radiusY, double thickness)
        {
            _content = ShapeFactory.CreateEllipseShape(radiusX, radiusY, thickness);
            this.Children.Add(_content);
        }


        // direct content reference
        public Ellipse Ellipse
        {
            get { return _content as Ellipse;}
        }


#if BITMAPCACHE
            //  TODO try this future if particles are slow :  blood and rain..
            //seems fps is the same for now..  and particle is offset a little, needs to be fixed.. maybe when gpu accel mode is fixed..
            //dont remove vector dress to see it..
        public override void CreateBitmapCacheAfterRendered(int dpm)
        {

     //theres a little overhead on emit.. but should be faster  
          //  return;  
            //TODO factor out this repeat code w / General object view.. into base class...        

            Rect bounds = new Rect(0, 0, _circle.ActualWidth, _circle.ActualHeight);

            _imageCache = new Image();

            _imageCache.Width = bounds.Width;
            _imageCache.Height = bounds.Height;


            if ( _writeableBitmap == null)//TODO map by color and size..
                _writeableBitmap= GetWriteableBitmapSnapshot(dpm, bounds);

            _imageCache.Source = _writeableBitmap;

      
            this.Children.Clear();
            Children.Add(_imageCache);

          //  _imageCache.CacheMode = new BitmapCache();  

        }


        WriteableBitmap GetWriteableBitmapSnapshot(int dpm, Rect bounds)
        {

            ScaleTransform scale = new ScaleTransform();
            double orgScaleF = 1;
            double scaleFactor = dpm * orgScaleF;

            scale.ScaleX = scaleFactor; // ??
            scale.ScaleY = scaleFactor;//??

            TranslateTransform translatePaperToImage = new TranslateTransform();
            translatePaperToImage.X = -bounds.Left;
            translatePaperToImage.Y = -bounds.Top;

            TransformGroup tgPaperToImage = new TransformGroup();
            tgPaperToImage.Children.Add(translatePaperToImage);     // 1. translate shape to (0,0) origin
            tgPaperToImage.Children.Add(scale);         // 2. scale to world size   ... order matters here..

            WriteableBitmap writeableBitmap = new WriteableBitmap((int)(_imageCache.Width * dpm * orgScaleF), (int)(_imageCache.Height * dpm * orgScaleF));

            writeableBitmap.Render(_circle, tgPaperToImage);


            writeableBitmap.Invalidate();

            //  Random colorbits = new Random();  //debug code
            //   for (int i = 0; i < writeableBitmap.Pixels.Length; i++)
            //    {
            //      writeableBitmap.Pixels[i] = (int)(colorbits.NextDouble() * int.MaxValue);
            //   }


            return writeableBitmap;

        }

#endif


        public override Brush Fill
        {
            get
            {
                return Ellipse.Fill;
            }
            set
            {
                Ellipse.Fill = value;
            }
        }


        public override Brush Stroke
        {
            get
            {
                return Ellipse.Stroke;
            }
            set
            {
                Ellipse.Stroke = value;
            }
        }


        private byte DecreaseAlpha(byte alpha, float ratio)
        {
            return (byte)(alpha - ratio * alpha);
        }


        protected override void OnUpdate()
        {
            base.OnUpdate();

      
        }


        public void ModifyRadius(float radiusX, float radiusY)
        {
            SetEllipseRadius(radiusX, radiusY, Ellipse);
        }

        public static void SetEllipseRadius(float radiusX, float radiusY, Ellipse c)
        {
            c.Width = radiusX * 2.0f;
            c.Height = radiusY * 2.0f;
            TranslateTransform t = new TranslateTransform();
            t.X = -radiusX;
            t.Y = -radiusY;
            c.RenderTransform = t;
        }
#endif
}

}
