using Farseer.Xna.Framework;



using VisibilityPolygon;

namespace Core.Game.MG.Graphics
{

    public class Vector2Adapter : PointAdapter<Vector2>
    {
        public override double GetX(Vector2 Vector2)
        {
            return Vector2.X;
        }

        public override double GetY(Vector2 Vector2)
        {
            return Vector2.Y;
        }

        public override Vector2 Create(double x, double y)
        {
            return new Vector2((float)x, (float)y);
        }

        public Vector2 Create(float x, float y)
        {
            return new Vector2(x, y);
        }


        //TODO   use to speed up shadows .. didnt work
        public override double GetPseudoAngle(Vector2 a, Vector2 b)
        {
            return (float)System.Math.Atan2(b.Y - a.Y, b.X - a.X) * 180f / (float)System.Math.PI;

            /*    double dx = b.X - a.X;
                double dy = b.Y - a.Y;

               // copysign(1. - dx / (fabs(dx) + fabs(dy)), dy)
                return Math.Sign(dy) * Math.Abs((1.0 - (dx) / (Math.Abs(dx) + Math.Abs(dy)))) * 180 / System.Math.PI; 
        */
        }

    }



    public static class Extensions
    {
        public static int Compare(this Vector2 t1, Vector2 t2)
        {
            if (t1.X < t2.Y)
                return -1;
            else if (t1.X > t2.X)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public static Vector2 Add(this Vector2 t1, Vector2 t2)
        {
            return new Vector2(t1.X + t2.X, t2.X + t2.Y);

        }

        //  public static MyObject Subtract(this MyObject t1, MyObject t2)
        //  {
        //      var newObject = new MyObject();
        //      //do something
        //      return newObject;
        //  }
    }


}

