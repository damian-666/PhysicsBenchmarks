﻿#if(!XNA)

#region License

/*
MIT License
Copyright © 2006 The Mono.Xna Team

All rights reserved.

Authors:
 * Alan McGovern

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#endregion License

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Farseer.Xna.Framework
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3 : IEquatable<Vector3>
    {
        #region Private Fields

        private static Vector3 zero = new Vector3(0f, 0f, 0f);
        private static Vector3 one = new Vector3(1f, 1f, 1f);
        private static Vector3 unitX = new Vector3(1f, 0f, 0f);
        private static Vector3 unitY = new Vector3(0f, 1f, 0f);
        private static Vector3 unitZ = new Vector3(0f, 0f, 1f);
        private static Vector3 up = new Vector3(0f, 1f, 0f);
        private static Vector3 down = new Vector3(0f, -1f, 0f);
        private static Vector3 right = new Vector3(1f, 0f, 0f);
        private static Vector3 left = new Vector3(-1f, 0f, 0f);
        private static Vector3 forward = new Vector3(0f, 0f, -1f);
        private static Vector3 backward = new Vector3(0f, 0f, 1f);

        #endregion Private Fields

        #region Public Fields

        public float X;
        public float Y;
        public float Z;

        #endregion Public Fields

        #region Properties

        public static Vector3 Zero
        {
            get { return zero; }
        }

        public static Vector3 One
        {
            get { return one; }
        }

        public static Vector3 UnitX
        {
            get { return unitX; }
        }

        public static Vector3 UnitY
        {
            get { return unitY; }
        }

        public static Vector3 UnitZ
        {
            get { return unitZ; }
        }

        public static Vector3 Up
        {
            get { return up; }
        }

        public static Vector3 Down
        {
            get { return down; }
        }

        public static Vector3 Right
        {
            get { return right; }
        }

        public static Vector3 Left
        {
            get { return left; }
        }

        public static Vector3 Forward
        {
            get { return forward; }
        }

        public static Vector3 Backward
        {
            get { return backward; }
        }

        #endregion Properties

        #region Constructors

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }


        public Vector3(float value)
        {
            X = value;
            Y = value;
            Z = value;
        }


        public Vector3(Vector2 value, float z)
        {
            X = value.X;
            Y = value.Y;
            Z = z;
        }

        #endregion Constructors

        #region Public Methods

        public static Vector3 Add(Vector3 value1, Vector3 value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            value1.Z += value2.Z;
            return value1;
        }

        public static void Add(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
        {
            result.X = value1.X + value2.X;
            result.Y = value1.Y + value2.Y;
            result.Z = value1.Z + value2.Z;
        }

        public static Vector3 Barycentric(Vector3 value1, Vector3 value2, Vector3 value3, float amount1, float amount2)
        {
            return new Vector3(
                MathHelper.Barycentric(value1.X, value2.X, value3.X, amount1, amount2),
                MathHelper.Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2),
                MathHelper.Barycentric(value1.Z, value2.Z, value3.Z, amount1, amount2));
        }

        public static void Barycentric(ref Vector3 value1, ref Vector3 value2, ref Vector3 value3, float amount1,
                                       float amount2, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.Barycentric(value1.X, value2.X, value3.X, amount1, amount2),
                MathHelper.Barycentric(value1.Y, value2.Y, value3.Y, amount1, amount2),
                MathHelper.Barycentric(value1.Z, value2.Z, value3.Z, amount1, amount2));
        }

        public static Vector3 CatmullRom(Vector3 value1, Vector3 value2, Vector3 value3, Vector3 value4, float amount)
        {
            return new Vector3(
                MathHelper.CatmullRom(value1.X, value2.X, value3.X, value4.X, amount),
                MathHelper.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount),
                MathHelper.CatmullRom(value1.Z, value2.Z, value3.Z, value4.Z, amount));
        }

        public static void CatmullRom(ref Vector3 value1, ref Vector3 value2, ref Vector3 value3, ref Vector3 value4,
                                      float amount, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.CatmullRom(value1.X, value2.X, value3.X, value4.X, amount),
                MathHelper.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, amount),
                MathHelper.CatmullRom(value1.Z, value2.Z, value3.Z, value4.Z, amount));
        }

        public static Vector3 Clamp(Vector3 value1, Vector3 min, Vector3 max)
        {
            return new Vector3(
                MathHelper.Clamp(value1.X, min.X, max.X),
                MathHelper.Clamp(value1.Y, min.Y, max.Y),
                MathHelper.Clamp(value1.Z, min.Z, max.Z));
        }

        public static void Clamp(ref Vector3 value1, ref Vector3 min, ref Vector3 max, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.Clamp(value1.X, min.X, max.X),
                MathHelper.Clamp(value1.Y, min.Y, max.Y),
                MathHelper.Clamp(value1.Z, min.Z, max.Z));
        }

        public static Vector3 Cross(Vector3 vector1, Vector3 vector2)
        {
            Cross(ref vector1, ref vector2, out vector1);
            return vector1;
        }

        public static void Cross(ref Vector3 vector1, ref Vector3 vector2, out Vector3 result)
        {
            result = new Vector3(vector1.Y*vector2.Z - vector2.Y*vector1.Z,
                                 -(vector1.X*vector2.Z - vector2.X*vector1.Z),
                                 vector1.X*vector2.Y - vector2.X*vector1.Y);
        }

        public static float Distance(Vector3 vector1, Vector3 vector2)
        {
            float result;
            DistanceSquared(ref vector1, ref vector2, out result);
            return (float) Math.Sqrt(result);
        }

        public static void Distance(ref Vector3 value1, ref Vector3 value2, out float result)
        {
            DistanceSquared(ref value1, ref value2, out result);
            result = (float) Math.Sqrt(result);
        }

        public static float DistanceSquared(Vector3 value1, Vector3 value2)
        {
            float result;
            DistanceSquared(ref value1, ref value2, out result);
            return result;
        }

        public static void DistanceSquared(ref Vector3 value1, ref Vector3 value2, out float result)
        {
            result = (value1.X - value2.X)*(value1.X - value2.X) +
                     (value1.Y - value2.Y)*(value1.Y - value2.Y) +
                     (value1.Z - value2.Z)*(value1.Z - value2.Z);
        }

        public static Vector3 Divide(Vector3 value1, Vector3 value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            value1.Z /= value2.Z;
            return value1;
        }

        public static Vector3 Divide(Vector3 value1, float value2)
        {
            float factor = 1/value2;
            value1.X *= factor;
            value1.Y *= factor;
            value1.Z *= factor;
            return value1;
        }

        public static void Divide(ref Vector3 value1, float divisor, out Vector3 result)
        {
            float factor = 1/divisor;
            result.X = value1.X*factor;
            result.Y = value1.Y*factor;
            result.Z = value1.Z*factor;
        }

        public static void Divide(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
        {
            result.X = value1.X/value2.X;
            result.Y = value1.Y/value2.Y;
            result.Z = value1.Z/value2.Z;
        }

        public static float Dot(Vector3 vector1, Vector3 vector2)
        {
            return vector1.X*vector2.X + vector1.Y*vector2.Y + vector1.Z*vector2.Z;
        }

        public static void Dot(ref Vector3 vector1, ref Vector3 vector2, out float result)
        {
            result = vector1.X*vector2.X + vector1.Y*vector2.Y + vector1.Z*vector2.Z;
        }

        public override bool Equals(object obj)
        {
            return (obj is Vector3) ? this == (Vector3) obj : false;
        }

        public bool Equals(Vector3 other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return (int) (X + Y + Z);
        }

        public static Vector3 Hermite(Vector3 value1, Vector3 tangent1, Vector3 value2, Vector3 tangent2, float amount)
        {
            Vector3 result = new Vector3();
            Hermite(ref value1, ref tangent1, ref value2, ref tangent2, amount, out result);
            return result;
        }

        public static void Hermite(ref Vector3 value1, ref Vector3 tangent1, ref Vector3 value2, ref Vector3 tangent2,
                                   float amount, out Vector3 result)
        {
            result.X = MathHelper.Hermite(value1.X, tangent1.X, value2.X, tangent2.X, amount);
            result.Y = MathHelper.Hermite(value1.Y, tangent1.Y, value2.Y, tangent2.Y, amount);
            result.Z = MathHelper.Hermite(value1.Z, tangent1.Z, value2.Z, tangent2.Z, amount);
        }

        public float Length()
        {
            float result;
            DistanceSquared(ref this, ref zero, out result);
            return (float) Math.Sqrt(result);
        }

        public float LengthSquared()
        {
            float result;
            DistanceSquared(ref this, ref zero, out result);
            return result;
        }

        public static Vector3 Lerp(Vector3 value1, Vector3 value2, float amount)
        {
            return new Vector3(
                MathHelper.Lerp(value1.X, value2.X, amount),
                MathHelper.Lerp(value1.Y, value2.Y, amount),
                MathHelper.Lerp(value1.Z, value2.Z, amount));
        }

        public static void Lerp(ref Vector3 value1, ref Vector3 value2, float amount, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.Lerp(value1.X, value2.X, amount),
                MathHelper.Lerp(value1.Y, value2.Y, amount),
                MathHelper.Lerp(value1.Z, value2.Z, amount));
        }

        public static Vector3 Max(Vector3 value1, Vector3 value2)
        {
            return new Vector3(
                MathHelper.Max(value1.X, value2.X),
                MathHelper.Max(value1.Y, value2.Y),
                MathHelper.Max(value1.Z, value2.Z));
        }

        public static void Max(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.Max(value1.X, value2.X),
                MathHelper.Max(value1.Y, value2.Y),
                MathHelper.Max(value1.Z, value2.Z));
        }

        public static Vector3 Min(Vector3 value1, Vector3 value2)
        {
            return new Vector3(
                MathHelper.Min(value1.X, value2.X),
                MathHelper.Min(value1.Y, value2.Y),
                MathHelper.Min(value1.Z, value2.Z));
        }

        public static void Min(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.Min(value1.X, value2.X),
                MathHelper.Min(value1.Y, value2.Y),
                MathHelper.Min(value1.Z, value2.Z));
        }

        public static Vector3 Multiply(Vector3 value1, Vector3 value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            value1.Z *= value2.Z;
            return value1;
        }

        public static Vector3 Multiply(Vector3 value1, float scaleFactor)
        {
            value1.X *= scaleFactor;
            value1.Y *= scaleFactor;
            value1.Z *= scaleFactor;
            return value1;
        }

        public static void Multiply(ref Vector3 value1, float scaleFactor, out Vector3 result)
        {
            result.X = value1.X*scaleFactor;
            result.Y = value1.Y*scaleFactor;
            result.Z = value1.Z*scaleFactor;
        }

        public static void Multiply(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
        {
            result.X = value1.X*value2.X;
            result.Y = value1.Y*value2.Y;
            result.Z = value1.Z*value2.Z;
        }

        public static Vector3 Negate(Vector3 value)
        {
            value = new Vector3(-value.X, -value.Y, -value.Z);
            return value;
        }

        public static void Negate(ref Vector3 value, out Vector3 result)
        {
            result = new Vector3(-value.X, -value.Y, -value.Z);
        }

        public void Normalize()
        {
            Normalize(ref this, out this);
        }

        public static Vector3 Normalize(Vector3 vector)
        {
            Normalize(ref vector, out vector);
            return vector;
        }

        public static void Normalize(ref Vector3 value, out Vector3 result)
        {
            float factor;
            Distance(ref value, ref zero, out factor);
            factor = 1f/factor;
            result.X = value.X*factor;
            result.Y = value.Y*factor;
            result.Z = value.Z*factor;
        }

        public static Vector3 Reflect(Vector3 vector, Vector3 normal)
        {
            Vector3 result;
            Reflect(ref vector, ref normal, out result);
            return result;
        }

        public static void Reflect(ref Vector3 vector, ref Vector3 normal, out Vector3 result)
        {
            float dot = Dot(vector, normal);
            result.X = vector.X - ((2f*dot)*normal.X);
            result.Y = vector.Y - ((2f*dot)*normal.Y);
            result.Z = vector.Z - ((2f*dot)*normal.Z);
        }

        public static Vector3 SmoothStep(Vector3 value1, Vector3 value2, float amount)
        {
            return new Vector3(
                MathHelper.SmoothStep(value1.X, value2.X, amount),
                MathHelper.SmoothStep(value1.Y, value2.Y, amount),
                MathHelper.SmoothStep(value1.Z, value2.Z, amount));
        }

        public static void SmoothStep(ref Vector3 value1, ref Vector3 value2, float amount, out Vector3 result)
        {
            result = new Vector3(
                MathHelper.SmoothStep(value1.X, value2.X, amount),
                MathHelper.SmoothStep(value1.Y, value2.Y, amount),
                MathHelper.SmoothStep(value1.Z, value2.Z, amount));
        }

        public static Vector3 Subtract(Vector3 value1, Vector3 value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            value1.Z -= value2.Z;
            return value1;
        }

        public static void Subtract(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
        {
            result.X = value1.X - value2.X;
            result.Y = value1.Y - value2.Y;
            result.Z = value1.Z - value2.Z;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(32);
            sb.Append("{X:");
            sb.Append(X);
            sb.Append(" Y:");
            sb.Append(Y);
            sb.Append(" Z:");
            sb.Append(Z);
            sb.Append("}");
            return sb.ToString();
        }

        public static Vector3 Transform(Vector3 position, Matrix matrix)
        {
            Transform(ref position, ref matrix, out position);
            return position;
        }

        public static void Transform(ref Vector3 position, ref Matrix matrix, out Vector3 result)
        {
            result =
                new Vector3((position.X*matrix.M11) + (position.Y*matrix.M21) + (position.Z*matrix.M31) + matrix.M41,
                            (position.X*matrix.M12) + (position.Y*matrix.M22) + (position.Z*matrix.M32) + matrix.M42,
                            (position.X*matrix.M13) + (position.Y*matrix.M23) + (position.Z*matrix.M33) + matrix.M43);
        }

        public static void Transform(Vector3[] sourceArray, ref Matrix matrix, Vector3[] destinationArray)
        {
            throw new NotImplementedException();
        }

        public static void Transform(Vector3[] sourceArray, int sourceIndex, ref Matrix matrix,
                                     Vector3[] destinationArray, int destinationIndex, int length)
        {
            throw new NotImplementedException();
        }

        public static void TransformNormal(Vector3[] sourceArray, ref Matrix matrix, Vector3[] destinationArray)
        {
            throw new NotImplementedException();
        }

        public static void TransformNormal(Vector3[] sourceArray, int sourceIndex, ref Matrix matrix,
                                           Vector3[] destinationArray, int destinationIndex, int length)
        {
            throw new NotImplementedException();
        }

        public static Vector3 TransformNormal(Vector3 normal, Matrix matrix)
        {
            TransformNormal(ref normal, ref matrix, out normal);
            return normal;
        }

        public static void TransformNormal(ref Vector3 normal, ref Matrix matrix, out Vector3 result)
        {
            result = new Vector3((normal.X*matrix.M11) + (normal.Y*matrix.M21) + (normal.Z*matrix.M31),
                                 (normal.X*matrix.M12) + (normal.Y*matrix.M22) + (normal.Z*matrix.M32),
                                 (normal.X*matrix.M13) + (normal.Y*matrix.M23) + (normal.Z*matrix.M33));
        }

        #endregion Public methods

        #region Operators

        public static bool operator ==(Vector3 value1, Vector3 value2)
        {
            return value1.X == value2.X
                   && value1.Y == value2.Y
                   && value1.Z == value2.Z;
        }

        public static bool operator !=(Vector3 value1, Vector3 value2)
        {
            return !(value1 == value2);
        }

        public static Vector3 operator +(Vector3 value1, Vector3 value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            value1.Z += value2.Z;
            return value1;
        }

        public static Vector3 operator -(Vector3 value)
        {
            value = new Vector3(-value.X, -value.Y, -value.Z);
            return value;
        }

        public static Vector3 operator -(Vector3 value1, Vector3 value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            value1.Z -= value2.Z;
            return value1;
        }

        public static Vector3 operator *(Vector3 value1, Vector3 value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            value1.Z *= value2.Z;
            return value1;
        }

        public static Vector3 operator *(Vector3 value, float scaleFactor)
        {
            value.X *= scaleFactor;
            value.Y *= scaleFactor;
            value.Z *= scaleFactor;
            return value;
        }

        public static Vector3 operator *(float scaleFactor, Vector3 value)
        {
            value.X *= scaleFactor;
            value.Y *= scaleFactor;
            value.Z *= scaleFactor;
            return value;
        }

        public static Vector3 operator /(Vector3 value1, Vector3 value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            value1.Z /= value2.Z;
            return value1;
        }

        public static Vector3 operator /(Vector3 value, float divider)
        {
            float factor = 1/divider;
            value.X *= factor;
            value.Y *= factor;
            value.Z *= factor;
            return value;
        }

        #endregion
    }
}

#endif