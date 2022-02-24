using System;
using System.Collections.Generic;
using Farseer.Xna.Framework;


namespace FarseerPhysics.Dynamics
{
    /// <summary>
    /// Brush class for Body.
    /// </summary>
    public class BodyGradientBrush
    {
        public BodyGradientBrush() { }
        
        internal List<BodyBrushGradientStop> _gradientStops;
        public List<BodyBrushGradientStop> GradientStops
        {
            get
            {
                if (_gradientStops == null)
                {
                    _gradientStops = new List<BodyBrushGradientStop>();
                }
                return _gradientStops;
            }
        }
    }


    public class BodyLinearGradientBrush : BodyGradientBrush
    {
        public BodyLinearGradientBrush() { }
        public Vector2 EndPoint;
        public Vector2 StartPoint;
    }


    public class BodyRadialGradientBrush : BodyGradientBrush
    {
        public BodyRadialGradientBrush() { }
        public Vector2 Center;
        public Vector2 GradientOrigin;
        public double RadiusX;
        public double RadiusY;
    }


    public class BodyBrushGradientStop
    {
        public BodyBrushGradientStop() { }

        public BodyBrushGradientStop(BodyColor color, double offset)
        {
            Color = new BodyColor(color);   // prevent modifying original color
            Offset = offset;
        }

        public BodyColor Color;
        public double Offset;
    }


}
