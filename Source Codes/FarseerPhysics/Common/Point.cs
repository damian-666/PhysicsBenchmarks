﻿namespace Farseer.Xna.Framework
{
    /// <summary>
    /// A Point class like the XNA Point, but using float instead of int, or the Windows.System version for WPF that uses double. 
    /// The coordinated generaly are on the viewport, or canvas in logical space or pixels.  Adds link list refs for fast access for polygon decomposition 
    /// </summary>
    public class Point
    {
        // Pointers to next and previous points in Monontone Mountain
        public Point Next, Prev;
        public float X, Y;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
            Next = null;
            Prev = null;
        }

        public static Point operator -(Point p1, Point p2)
        {
            return new Point(p1.X - p2.X, p1.Y - p2.Y);
        }

        public static Point operator +(Point p1, Point p2)
        {
            return new Point(p1.X + p2.X, p1.Y + p2.Y);
        }

        public static Point operator -(Point p1, float f)
        {
            return new Point(p1.X - f, p1.Y - f);
        }

        public static Point operator +(Point p1, float f)
        {
            return new Point(p1.X + f, p1.Y + f);
        }


        public Vector2 ToVector2( )
        {
            return new Vector2(X, Y);
        }
        public float Cross(Point p)
        {
            return X*p.Y - Y*p.X;
        }

        public float Dot(Point p)
        {
            return X*p.X + Y*p.Y;
        }

        public bool Neq(Point p)
        {
            return p.X != X || p.Y != Y;
        }

        public float Orient2D(Point pb, Point pc)
        {
            float acx = X - pc.X;
            float bcx = pb.X - pc.X;
            float acy = Y - pc.Y;
            float bcy = pb.Y - pc.Y;
            return acx*bcy - acy*bcx;
        }
    }
}