using Farseer.Xna.Framework;
using FarseerPhysicsView;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

using Vector2 = Farseer.Xna.Framework.Vector2;

namespace Core.Game.MG.Drawing
{


    //minimal display list primitive for immediate draw

    //TODO make it a struct.. dont use the ray map .. this was a class because we map it by refernce
    //beter practice for hig perf code for SIMD to use immutable struct for things like this
    public class LineSegment  
    {
      public Vector2 X1;//TODO see bepu does he use get;set or it it smarter in performance code to not
        public Vector2 X2;
        public Color Color;


    }
}
