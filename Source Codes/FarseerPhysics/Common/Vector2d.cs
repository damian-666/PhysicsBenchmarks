
using System;
//using System.Runtime.InteropServices;
using System.Text;
//using System.Runtime.Serialization;


//TODO future  taking too long to get this to compile.  will us a shorter version

namespace FarseerPhysics.Common
{

    /// <summary>
    /// Double precision float vector
    /// </summary>
    public struct Vector2d
    {

        // [DataMember]
        public double X;

        // [DataMember]
        public double Y;


        public Vector2d(double x, double y)
        {
            X = x;
            Y = y;
        }

    }

}



#if VectorTemplate


//google templated xna vector or math
//   [StructLayout(LayoutKind.Sequential)]
//   [DataContract(Name = "Vector2d", Namespace = "http://ShadowPlay")]
//
//   public struct Vector2D : Vector2<double>, IEquatable<Vector2>, IComparable<Vector2D>
//   {
//
//   }
//

//TODO   this is templated version for high precision calculation
//on GPU 32 bit might be faster      this is for experimentation
//another solution in Mathnet  , its a massive library almost 1 meg and would need to be cut up.
// using Vector2d = Vector2<double>;   //should make a class?

//TODO  look for a templated one to do double precision ..   data needs to be  cache friendly... but doubple pre might be useful
//cohen uses it..

//#if !(SILVERLIGHT || UNIVERSAL)
//  [Serializable]
//#endif
//   [StructLayout(LayoutKind.Sequential)]
//   [DataContract(Name = "Vector2d", Namespace = "http://ShadowPlay")]     this is not mean for data storage at the moment. it is too bloated


// levels can use multipliers to get a bigger space...


public class Vector2<T> : IEquatable<Vector2<T>>, IComparable<Vector2<T>>  //comparable not tested  //TODO
    {
        #region Private Fields

        private static Vector2<T> zeroVector = new Vector2<T>(0, 0);
        private static Vector2<T> unitVector = new Vector2<T>(1, 1);
        private static Vector2<T> unitXVector = new Vector2<T>(1, 0);
        private static Vector2<T> unitYVector = new Vector2<T>(0, 1);

        #endregion Private Fields

        #region Public Fields

        // [DataMember]
        public T X;

        // [DataMember]
        public T Y;

        #endregion Public Fields

        #region Properties

        public static Vector2<T> Zero
        {
            get { return zeroVector; }
        }

        public static Vector2<T> One
        {
            get { return unitVector; }
        }

        public static Vector2<T> UnitX
        {
            get { return unitXVector; }
        }

        public static Vector2<T> UnitY
        {
            get { return unitYVector; }
        }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Constructor foe standard 2D vector.
        /// </summary>
        /// <param name="x">
        /// A <see cref="System.Single"/>
        /// </param>
        /// <param name="y">
        /// A <see cref="System.Single"/>
        /// </param>
        public Vector2<T>(T x, T y)
        {
            X = x;
            Y = y;
         }



    /// <summary>
    /// Constructor for "square" vector.
    /// </summary>
    /// <param name="value">
    /// A <see cref="System.Single"/>
    /// </param>
    public Vector2<T>(T value)
        {
            X = value;
            Y = value;
        }



        override bool IEquatable<Vector2<T>>.Equals(Vector2<T> other)
        {
        return (obj is Vector2<T>) ? this == ((Vector2<T>) obj) : false;
        }

        int IComparable<Vector2<T>>.CompareTo(Vector2<T> other)
        {
        return this == other;
        }



        #endregion Constructors

        #region Public Methods

        public static void Reflect(ref Vector2<T> vector, ref Vector2<T> normal, out Vector2<T> result)
        {
            float dot = Dot(vector, normal);
            result.X = vector.X - ((2f * dot) * normal.X);
            result.Y = vector.Y - ((2f * dot) * normal.Y);
        }

        public static Vector2<T> Reflect(Vector2<T> vector, Vector2<T> normal)
        {
            Vector2<T> result;
            Reflect(ref vector, ref normal, out result);
            return result;
        }

        public static Vector2<T> Add(Vector2<T> value1, Vector2<T> value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            return value1;
        }

        public static void Add(ref Vector2<T> value1, ref Vector2<T> value2, out Vector2<T> result)
        {
            result.X = value1.X + value2.X;
            result.Y = value1.Y + value2.Y;
        }

        public static Vector2<T> Barycentric(Vector2<T> value1, Vector2<T> value2, Vector2<T> value3, float amount1, float amount2)
        {
            return new Vector2<T>(
                MathHelper.Barycentric(value1.X, value2.X, value3.X, amount1, amount2),
                MathHelper.Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2));
        }

        public static void Barycentric(ref Vector2<T> value1, ref Vector2<T> value2, ref Vector2<T> value3, float amount1,
                                       float amount2, out Vector2<T> result)
        {
            result = new Vector2<T>(
                MathHelper.Barycentric(value1.X, value2.X, value3.X, amount1, amount2),
                MathHelper.Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2));
        }

        public static Vector2<T> CatmullRom(Vector2<T> value1, Vector2<T> value2, Vector2<T> value3, Vector2<T> value4, float amount)
        {
            return new Vector2<T>(
                MathHelper.CatmullRom(value1.X, value2.X, value3.X, value4.X, amount),
                MathHelper.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount));
        }

        public static void CatmullRom(ref Vector2<T> value1, ref Vector2<T> value2, ref Vector2<T> value3, ref Vector2<T> value4,
                                      float amount, out Vector2<T> result)
        {
            result = new Vector2<T>(
                MathHelper.CatmullRom(value1.X, value2.X, value3.X, value4.X, amount),
                MathHelper.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount));
        }

        public static Vector2<T> Clamp(Vector2<T> value1, Vector2<T> min, Vector2<T> max)
        {
            return new Vector2<T>(
                MathHelper.Clamp(value1.X, min.X, max.X),
                MathHelper.Clamp(value1.Y, min.Y, max.Y));
        }

        public static void Clamp(ref Vector2<T> value1, ref Vector2<T> min, ref Vector2<T> max, out Vector2<T> result)
        {
            result = new Vector2<T>(
                MathHelper.Clamp(value1.X, min.X, max.X),
                MathHelper.Clamp(value1.Y, min.Y, max.Y));
        }

        /// <summary>
        /// Returns float precison distanve between two vectors
        /// </summary>
        /// <param name="value1">
        /// A <see cref="Vector2<T>"/>
        /// </param>
        /// <param name="value2">
        /// A <see cref="Vector2<T>"/>
        /// </param>
        /// <returns>
        /// A <see cref="System.Single"/>
        /// </returns>
        public static float Distance(Vector2<T> value1, Vector2<T> value2)
        {
            float result;
            DistanceSquared(ref value1, ref value2, out result);
            return (float)Math.Sqrt(result);
        }


        public static void Distance(ref Vector2<T> value1, ref Vector2<T> value2, out float result)
        {
            DistanceSquared(ref value1, ref value2, out result);
            result = (float)Math.Sqrt(result);
        }

        public static float DistanceSquared(Vector2<T> value1, Vector2<T> value2)
        {
            float result;
            DistanceSquared(ref value1, ref value2, out result);
            return result;
        }

        public static void DistanceSquared(ref Vector2<T> value1, ref Vector2<T> value2, out float result)
        {
            result = (value1.X - value2.X) * (value1.X - value2.X) + (value1.Y - value2.Y) * (value1.Y - value2.Y);
        }

        /// <summary>
        /// Divide first vector with the second vector
        /// </summary>
        /// <param name="value1">
        /// A <see cref="Vector2<T>"/>
        /// </param>
        /// <param name="value2">
        /// A <see cref="Vector2<T>"/>
        /// </param>
        /// <returns>
        /// A <see cref="Vector2<T>"/>
        /// </returns>
        public static Vector2<T> Divide(Vector2<T> value1, Vector2<T> value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            return value1;
        }

        public static void Divide(ref Vector2<T> value1, ref Vector2<T> value2, out Vector2<T> result)
        {
            result.X = value1.X / value2.X;
            result.Y = value1.Y / value2.Y;
        }

        public static Vector2<T> Divide(Vector2<T> value1, float divider)
        {
            float factor = 1 / divider;
            value1.X *= factor;
            value1.Y *= factor;
            return value1;
        }

        public static void Divide(ref Vector2<T> value1, float divider, out Vector2<T> result)
        {
            float factor = 1 / divider;
            result.X = value1.X * factor;
            result.Y = value1.Y * factor;
        }

        public static float Dot(Vector2<T> value1, Vector2<T> value2)
        {
            return value1.X * value2.X + value1.Y * value2.Y;
        }

        public static void Dot(ref Vector2<T> value1, ref Vector2<T> value2, out float result)
        {
            result = value1.X * value2.X + value1.Y * value2.Y;
        }

 

        public override int GetHashCode()
        {
            return (int)(X + Y);
        }

        public static Vector2<T> Hermite(Vector2<T> value1, Vector2<T> tangent1, Vector2<T> value2, Vector2<T> tangent2, float amount)
        {
            Vector2<T> result = new Vector2<T>();
            Hermite(ref value1, ref tangent1, ref value2, ref tangent2, amount, out result);
            return result;
        }

        public static void Hermite(ref Vector2<T> value1, ref Vector2<T> tangent1, ref Vector2<T> value2, ref Vector2<T> tangent2,
                                   float amount, out Vector2<T> result)
        {
            result.X = MathHelper.Hermite(value1.X, tangent1.X, value2.X, tangent2.X, amount);
            result.Y = MathHelper.Hermite(value1.Y, tangent1.Y, value2.Y, tangent2.Y, amount);
        }

        public float Length()
        {
            float result;
            DistanceSquared(ref this, ref zeroVector, out result);
            return (float)Math.Sqrt(result);
        }

        public float LengthSquared()
        {
            float result;
            //   DistanceSquared(ref this, ref zeroVector, out result); //ShadowplayMod.. below line looks more optimal than above since zeroVector is not const 
            result = Vector2<T>.Dot(this, this);
            return result;
        }

        public static Vector2<T> Lerp(Vector2<T> value1, Vector2<T> value2, float amount)
        {
            return new Vector2<T>(
                MathHelper.Lerp(value1.X, value2.X, amount),
                MathHelper.Lerp(value1.Y, value2.Y, amount));
        }

        public static void Lerp(ref Vector2<T> value1, ref Vector2<T> value2, float amount, out Vector2<T> result)
        {
            result = new Vector2<T>(
                MathHelper.Lerp(value1.X, value2.X, amount),
                MathHelper.Lerp(value1.Y, value2.Y, amount));
        }

        public static Vector2<T> Max(Vector2<T> value1, Vector2<T> value2)
        {
            return new Vector2<T>(
                MathHelper.Max(value1.X, value2.X),
                MathHelper.Max(value1.Y, value2.Y));
        }

        public static void Max(ref Vector2<T> value1, ref Vector2<T> value2, out Vector2<T> result)
        {
            result = new Vector2<T>(
                MathHelper.Max(value1.X, value2.X),
                MathHelper.Max(value1.Y, value2.Y));
        }

        public static Vector2 Min(Vector2<T> value1, Vector2<T> value2)
        {
            return new Vector2<T>(
                MathHelper.Min(value1.X, value2.X),
                MathHelper.Min(value1.Y, value2.Y));
        }

        public static void Min(ref Vector2<T> value1, ref Vector2<T> value2, out Vector2<T> result)
        {
            result = new Vector2<T>(
                MathHelper.Min(value1.X, value2.X),
                MathHelper.Min(value1.Y, value2.Y));
        }

        public static Vector2<T> Multiply(Vector2<T> value1, Vector2 value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            return value1;
        }

        public static Vector2 Multiply(Vector2 value1, float scaleFactor)
        {
            value1.X *= scaleFactor;
            value1.Y *= scaleFactor;
            return value1;
        }

        public static void Multiply(ref Vector2 value1, float scaleFactor, out Vector2 result)
        {
            result.X = value1.X * scaleFactor;
            result.Y = value1.Y * scaleFactor;
        }

        public static void Multiply(ref Vector2 value1, ref Vector2 value2, out Vector2 result)
        {
            result.X = value1.X * value2.X;
            result.Y = value1.Y * value2.Y;
        }

        public static Vector2 Negate(Vector2 value)
        {
            value.X = -value.X;
            value.Y = -value.Y;
            return value;
        }

        public static void Negate(ref Vector2 value, out Vector2 result)
        {
            result.X = -value.X;
            result.Y = -value.Y;
        }

        public void Normalize()
        {
            Normalize(ref this, out this);
        }

        public static Vector2 Normalize(Vector2 value)
        {
            Normalize(ref value, out value);
            return value;
        }

        public static void Normalize(ref Vector2 value, out Vector2 result)
        {
            float factor;
            DistanceSquared(ref value, ref zeroVector, out factor);
            factor = 1f / (float)Math.Sqrt(factor);
            result.X = value.X * factor;
            result.Y = value.Y * factor;
        }

        public static Vector2 SmoothStep(Vector2 value1, Vector2 value2, float amount)
        {
            return new Vector2(
                MathHelper.SmoothStep(value1.X, value2.X, amount),
                MathHelper.SmoothStep(value1.Y, value2.Y, amount));
        }

        public static void SmoothStep(ref Vector2 value1, ref Vector2 value2, float amount, out Vector2 result)
        {
            result = new Vector2(
                MathHelper.SmoothStep(value1.X, value2.X, amount),
                MathHelper.SmoothStep(value1.Y, value2.Y, amount));
        }

        public static Vector2 Subtract(Vector2 value1, Vector2 value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            return value1;
        }

        public static void Subtract(ref Vector2 value1, ref Vector2 value2, out Vector2 result)
        {
            result.X = value1.X - value2.X;
            result.Y = value1.Y - value2.Y;
        }

        public static Vector2 Transform(Vector2 position, Matrix matrix)
        {
            Transform(ref position, ref matrix, out position);
            return position;
        }

        public static void Transform(ref Vector2 position, ref Matrix matrix, out Vector2 result)
        {
            result = new Vector2((position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M41,
                                 (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M42);
        }

        public static void Transform(Vector2<T>[] sourceArray, ref Matrix matrix, Vector2<T>[] destinationArray)
        {
            throw new NotImplementedException();
        }

        public static void Transform(Vector2<T>[] sourceArray, int sourceIndex, ref Matrix matrix,
                                     Vector2<T>[] destinationArray, int destinationIndex, int length)
        {
            throw new NotImplementedException();
        }

        public static Vector2 TransformNormal(Vector2<T> normal, Matrix matrix)
        {
            TransformNormal(ref normal, ref matrix, out normal);
            return normal;
        }

        public static void TransformNormal(ref Vector2<T> normal, ref Vector2<T> matrix, out Vector2<T> result)
        {
            result = new Vector2((normal.X * matrix.M11) + (normal.Y * matrix.M21),
                                 (normal.X * matrix.M12) + (normal.Y * matrix.M22));
        }

        public static void TransformNormal(Vector2<T>[] sourceArray, ref Matrix matrix, Vector2<T>[] destinationArray)
        {
            throw new NotImplementedException();
        }

        public static void TransformNormal(Vector2<T>[] sourceArray, int sourceIndex, ref Matrix matrix,
                                           Vector2<T>[] destinationArray, int destinationIndex, int length)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(24);
            sb.Append("{X:");
            sb.Append(X);
            sb.Append(" Y:");
            sb.Append(Y);
            sb.Append("}");
            return sb.ToString();
        }


        public string ToStringShort(string format)//shadowplay Mod
        {
            StringBuilder sb = new StringBuilder(24);
            sb.Append(X.ToString(format));
            sb.Append(", ");
            sb.Append(Y.ToString(format));
            return sb.ToString();
        }

        #endregion Public Methods


        #region Shadowplay Mods 

        //not in XNA but we are extending it and now using Farseer namespace
        /// <summary>
        /// Give mirrored position using vertical axis. 
        /// Cannot use ref on pos param because some caller supply property.
        /// This should be DEPRECATED later, we'd better use Vector2.Reflect(), it includes rotation.
        /// </summary>
        /// <returns>Return mirrored posiition.</returns>
        public static Vector2 MirrorHorizontal(Vector2 pos, float verticalAxisPosX)
        {
            float newXPos = pos.X - verticalAxisPosX;       // align axis to origin first, get new x pos
            newXPos = -newXPos;                             // mirror new x pos
            newXPos += verticalAxisPosX;                    // restore to original axis
                                                            //  float newpos = axis - pos.X;   FUTUREREVISIT  check this formula above..  w/Body local around Body center or body position? 
            return new Vector2(newXPos, pos.Y);
        }

        public int CompareTo(Vector2<T> other)
        {

            if (LengthSquared() > other.LengthSquared())
                return 1;
            else
                if (this == other)
                return 0;
            else return -1;
        }


        #endregion


        #region Operators

        public static Vector2<T> operator -(Vector2 value)
        {
            value.X = -value.X;
            value.Y = -value.Y;
            return value;
        }


        public static bool operator ==(Vector2<T> value1, Vector2<T> value2)
        {
            return value1.X == value2.X && value1.Y == value2.Y;
        }


        public static bool operator !=(Vector2<T> value1, Vector2<T> value2)
        {
            return value1.X != value2.X || value1.Y != value2.Y;
        }


        public static Vector2<T> operator +(Vector2<T> value1, Vector2<T> value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            return value1;
        }


        public static Vector2<T> operator -(Vector2<T> value1, Vector2<T> value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            return value1;
        }


        public static Vector2<T> operator *(Vector2<T> value1, Vector2<T> value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            return value1;
        }


        public static Vector2<T> operator *(Vector2<T> value, Vector2<T> scaleFactor)
        {
            value.X *= scaleFactor;
            value.Y *= scaleFactor;
            return value;
        }


        public static Vector2<T> operator *(Vector2<T> scaleFactor, Vector2<T> value)
        {
            value.X *= scaleFactor;
            value.Y *= scaleFactor;
            return value;
        }


        public static Vector2<T> operator /(Vector2<T> value1, Vector2<T> value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            return value1;
        }


        public static Vector2<T> operator /(Vector2<T> value1, float divider)
        {
            float factor = 1 / divider;
            value1.X *= factor;
            value1.Y *= factor;
            return value1;
        }

        #endregion Operators
    }
}


#endif