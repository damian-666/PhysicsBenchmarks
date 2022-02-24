using System;

namespace VisibilityPolygon
{
    /// <summary>
    ///   A sample point adapter for the sample point .   One like this would goe in the graphics module if you are able to use graphics points directly. for wpf/sl convert to double
    /// </summary>
    public class PointDoubleAdapter : PointAdapter<PointDouble>
    {
        public override double GetX(PointDouble point)
        {
            return point.X;
        }

        public override double GetY(PointDouble point)
        {
            return point.Y;
        }

        public override PointDouble Create(double x, double y)
        {
            return new PointDouble(x, y);
        }



        public override double GetPseudoAngle(PointDouble a, PointDouble b)
        {
            return Math.Atan2(b.Y - a.Y, b.X - a.X) * 180.0 / System.Math.PI;

        }


    }
}