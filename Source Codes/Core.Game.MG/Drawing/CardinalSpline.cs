using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Farseer.Xna.Framework;
using FarseerPhysics.Common;

using System.Numerics;
//using MathNet.Numerics.Interpolation;

namespace Core.Game.MG.Drawing
{

//try this first  https://ilnumerics.net/spline-interpolation-net.html, see size of 
//https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/Interpolation/CubicSpline.cs
    //see module size, take just some if too big

    //NOTE will need another calc for this, this one depends on Window and the StreamGeometryContext.BezierTo to draw the spline from the control pts.
    //plenty of libs for this.
#if GRAPHICS_MG


see polygonView in here forf java examples

//see numerics lib

spline is in Nez or mgextended or myra.
    public class CardinalSpline : Shape
    {
    #region Fields

        StreamGeometry _StreamGeometry;

    #endregion

    #region Constructors

        public CardinalSpline()
        {
            Vertices _Vector2s = new Vertices();
            _Vector2s.Add(new Vector2(10, 10));
            _Vector2s.Add(new Vector2(30, 20));
            _Vector2s.Add(new Vector2(10, 30));
            Vector2s = _Vector2s;

            Closed = true;
            //this.Pen = new Pen(new SolidColorBrush(Colors.Red), 3);
        }

         
    #endregion

    #region Dependency Props


        public static readonly DependencyProperty Vector2sProperty =
            Polyline.Vector2sProperty.AddOwner(typeof(CardinalSpline),
                new FrameworkPropertyMetadata(null, OnMeasurePropertyChanged));

        public static readonly DependencyProperty TensionProperty =
            DependencyProperty.Register("Tension",
            typeof(double),
            typeof(CardinalSpline),
            new FrameworkPropertyMetadata(0.5, OnMeasurePropertyChanged));

        public static readonly DependencyProperty ClosedProperty =
            DependencyProperty.Register("Closed",
            typeof(bool),
            typeof(CardinalSpline),
            new FrameworkPropertyMetadata(false, OnMeasurePropertyChanged));


        static void OnMeasurePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            (obj as CardinalSpline).OnMeasurePropertyChanged(args);
        }

        static void OnRenderPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            (obj as CardinalSpline).OnRenderPropertyChanged(args);
        }

    #endregion

    #region Event

        void OnMeasurePropertyChanged(DependencyPropertyChangedEventArgs args)
        {
            if (_StreamGeometry == null)
                _StreamGeometry = new StreamGeometry();

            using (StreamGeometryContext sgc = _StreamGeometry.Open())
            {
                // Get Bezier Spline Control Vector2s.

                Vertices pnts = cardinalSpline(Vector2s, .5, Closed);

                sgc.BeginFigure(pnts[0], true, false);
                for (int i = 1; i < pnts.Count; i += 3)
                {
                    sgc.BezierTo(pnts[i], pnts[i + 1], pnts[i + 2], true, false);
                }
            }
            InvalidateMeasure();
            OnRenderPropertyChanged(args);
        }

        void OnRenderPropertyChanged(DependencyPropertyChangedEventArgs args)
        {
            InvalidateVisual();
        }

    #endregion

    #region Properties

        public Vertices Vector2s
        {
            set { SetValue(Vector2sProperty, value); }
            get { return (Vector2Collection)GetValue(Vector2sProperty); }
        }

        public double Tension
        {
            set { SetValue(TensionProperty, value); }
            get { return (double)GetValue(TensionProperty); }
        }

        public bool Closed
        {
            set { SetValue(ClosedProperty, value); }
            get { return (bool)GetValue(ClosedProperty); }

        }

    #endregion

    #region DefiningGeometry

        protected override System.Windows.Media.Geometry DefiningGeometry
        {
            get { return _StreamGeometry; }
        }

    #endregion

        /*
         * 
         * This is what you are after!
         * Below:
         */

    #region Calculation of Spline

        private static void CalcCurve(Vector2[] pts, double tenstion, out Vector2 p1, out Vector2 p2)
        {
            double deltaX, deltaY;
            deltaX = pts[2].X - pts[0].X;
            deltaY = pts[2].Y - pts[0].Y;
            p1 = new Vector2((pts[1].X - tenstion * deltaX), (pts[1].Y - tenstion * deltaY));
            p2 = new Vector2((pts[1].X + tenstion * deltaX), (pts[1].Y + tenstion * deltaY));
        }

        private static void CalcCurveEnd(Vector2 end, Vector2 adj, double tension, out Vector2 p1)
        {            
            p1 = new Vector2(((tension * (adj.X - end.X) + end.X)), ((tension * (adj.Y - end.Y) + end.Y)));
        }

        private static Vertices CardinalSpline(Vertices pts, double t, bool closed)
        {
            int i, nrRetPts;
            Vector2 p1, p2;
            double tension = t * (1d / 3d); //we are calculating contolVector2s.

            if (closed)
                nrRetPts = (pts.Count + 1) * 3 - 2;
            else
                nrRetPts = pts.Count * 3 - 2;

            Vector2[] retPnt = new Vector2[nrRetPts];
            for (i = 0; i < nrRetPts; i++)
                retPnt[i] = new Vector2();

            if (!closed)
            {
                CalcCurveEnd(pts[0], pts[1], tension, out p1);
                retPnt[0] = pts[0];
                retPnt[1] = p1;
            }
            for (i = 0; i < pts.Count - (closed ? 1 : 2); i++)
            {
                CalcCurve(new Vector2[] { pts[i], pts[i + 1], pts[(i + 2) % pts.Count] }, tension, out  p1, out p2);
                retPnt[3 * i + 2] = p1;
                retPnt[3 * i + 3] = pts[i + 1];
                retPnt[3 * i + 4] = p2;
            }
            if (closed)
            {
                CalcCurve(new Vector2[] { pts[pts.Count - 1], pts[0], pts[1] }, tension, out p1, out p2);
                retPnt[nrRetPts - 2] = p1;
                retPnt[0] = pts[0];
                retPnt[1] = p2;
                retPnt[nrRetPts - 1] = retPnt[0];
            }
            else
            {
                CalcCurveEnd(pts[pts.Count - 1], pts[pts.Count - 2], tension, out p1);
                retPnt[nrRetPts - 2] = p1;
                retPnt[nrRetPts - 1] = pts[pts.Count - 1];
            }
            return new Vertices(retPnt);
        }

    #endregion

    }
#endif
}
