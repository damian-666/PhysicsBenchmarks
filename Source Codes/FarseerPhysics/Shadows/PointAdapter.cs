﻿namespace VisibilityPolygon
{
    public abstract class PointAdapter<TPoint>
    {
        public abstract double GetX(TPoint point);
        public abstract double GetY(TPoint point);
        public abstract TPoint Create(double x, double y);
        public abstract double GetPseudoAngle(TPoint a, TPoint b);

    }
}