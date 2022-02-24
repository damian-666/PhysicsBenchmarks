



using Farseer.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;

namespace FarseerPhysicsUA.Common
{

    /// <summary>
    /// A related pair of points, from A to B.   for example a cut from ptA to ptB
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [DataContract(Name = "PointPair", Namespace = "http://ShadowPlay")]
    public struct PointPair
    {
        [DataMember]
        public Vector2 A;
        [DataMember]
        public Vector2 B;


        public PointPair(float x, float y, float xb, float yb)
        {
            A.X = x; A.Y = y;
            B.X = xb; B.Y = yb;
        }


        public PointPair(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }

    }
}
     