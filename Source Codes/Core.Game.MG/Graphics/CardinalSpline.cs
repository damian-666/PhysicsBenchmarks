
using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using MGCore;
using System;
using System.Collections.Generic;
using System.Linq;

#if EVERUSEFULL// todo erase

namespace Core.Game.MG.Graphics
{

    public class CardinalSpline
    {


        //TODO we have farseer spine used im body..good enouig
        //for view and or collsion

        //erase if nothing using in this
       
        public CardinalSpline()
        {
            Vertices points = new Vertices();
            points.Add(new Vector2(10, 10));
            points.Add(new Vector2(30, 20));
            points.Add(new Vector2(10, 30));
            Points = points;

            Closed = true;

        }


        void CalcPoints()
        {



            Vertices pnts = cardinalSpline(Points, .5f, Closed);

            //TODO MG_GRAPHICS
            //   sgc.BeginFigure(pnts[0], true, false);
            //   for (int i = 1; i < pnts.Count; i += 3)
            //   {
            //        sgc.BezierTo(pnts[i], pnts[i + 1], pnts[i + 2], true, false);
            //
            // }

            //TODO fit curve using   Bezier 

            //    todo consider a numberics version.. consider myra if it does
        }

#region Properties
        private float tension = 0;

        public float Tension
        {
            set { tension = value; }
            get { return tension; }
        }


        Vertices points;

        public Vertices Points
        {
            set { points = value; }
            get { return points; }
        }

        public bool Closed { get; private set; }



#endregion




        private static void CalcCurve(Vector2[] pts, float tenstion, out Vector2 p1, out Vector2 p2)
        {
            float deltaX, deltaY;
            deltaX = pts[2].X - pts[0].X;
            deltaY = pts[2].Y - pts[0].Y;
            p1 = new Vector2((pts[1].X - tenstion * deltaX), (pts[1].Y - tenstion * deltaY));
            p2 = new Vector2((pts[1].X + tenstion * deltaX), (pts[1].Y + tenstion * deltaY));
        }

        private static void CalcCurveEnd(Vector2 end, Vector2 adj, float tension, out Vector2 p1)
        {
            p1 = new Vector2(((tension * (adj.X - end.X) + end.X)), ((tension * (adj.Y - end.Y) + end.Y)));
        }

        private static Vertices cardinalSpline(Vertices pts, float t, bool closed)
        {
            int i, nrRetPts;
            Vector2 p1, p2;
            float tension = t * (1f / 3f); //we are calculating controlpoints.

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
                CalcCurve(new Vector2[] { pts[i], pts[i + 1], pts[(i + 2) % pts.Count] }, tension, out p1, out p2);
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


    }
}
#endif
