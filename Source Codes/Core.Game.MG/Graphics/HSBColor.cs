
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;

namespace Core.Game.MG.Graphics
{

    //from  http://www.dev102.com/2009/07/23/changing-brush-brightness-in-wpfsilverlight/
    //prolly in mogogame or better.. prolly repeat.. low pri MG_GRAPHICS

    //if we blend use the newest blend code form the guy phd did the water paper
    public class HSBColor
    {
        public double H { get; set; }
        public double S { get; set; }
        public double B { get; set; }
        public byte A { get; set; }


        public static HSBColor FromBodyColor(BodyColor bodyColor)
        {
            Color color = ToMediaColor(bodyColor);
            return FromColor(color);
        }


        /// <summary>
        /// convert from Data BodyColor to graphics Color struct  ( Data model module doesnt reference or  know about Color, nor presentation stuff) 
        /// </summary>
        /// <param name="bodyColor"></param>
        /// <returns></returns>
        public static Color ToMediaColor(BodyColor bodyColor)
        {
            Color color = Color.Black;
            color.R = bodyColor.R;
            color.G = bodyColor.G;
            color.B = bodyColor.B;
            color.A = bodyColor.A;
            return color;
        }


        static void FromMediaColor(Color color, ref BodyColor bodyColor)
        {
            bodyColor.R = color.R;
            bodyColor.G = color.G;
            bodyColor.B = color.B;
            bodyColor.A = color.A;
        }


        public void ToBodyColor(ref BodyColor bodyColor)
        {
            FromMediaColor(ToColor(), ref bodyColor);
        }


        public static HSBColor FromColor(Color rgbColor)
        {
            HSBColor result = new HSBColor();

            // preserve alpha
            result.A = rgbColor.A;

            // convert R, G, B to numbers from 0 to 1
            double r = rgbColor.R / 255d;
            double g = rgbColor.G / 255d;
            double b = rgbColor.B / 255d;

            double max = System.Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));

            // hue
            if (max == min)
                result.H = 0;
            else if (max == r)
                result.H = (60 * (g - b) / (max - min) + 360) % 360;
            else if (max == g)
                result.H = 60 * (b - r) / (max - min) + 120;
            else
                result.H = 60 * (r - g) / (max - min) + 240;

            // saturation
            if (max == 0)
                result.S = 0;
            else
                result.S = 1 - min / max;

            // brightness
            result.B = max;

            return result;
        }


        public Color ToColor()
        {
            Color result = new Color();

            result.A = this.A;

            int hi = (int)Math.Floor(this.H / 60) % 6;
            double f = this.H / 60 - Math.Floor(this.H / 60);

            double p = this.B * (1 - this.S);
            double q = this.B * (1 - f * this.S);
            double t = this.B * (1 - (1 - f) * this.S);

            switch (hi)
            {
                case 0:
                    result.R = (byte)(this.B * 255);
                    result.G = (byte)(t * 255);
                    result.B = (byte)(p * 255);
                    break;
                case 1:
                    result.R = (byte)(q * 255);
                    result.G = (byte)(this.B * 255);
                    result.B = (byte)(p * 255);
                    break;
                case 2:
                    result.R = (byte)(p * 255);
                    result.G = (byte)(this.B * 255);
                    result.B = (byte)(t * 255);
                    break;
                case 3:
                    result.R = (byte)(p * 255);
                    result.G = (byte)(q * 255);
                    result.B = (byte)(this.B * 255);
                    break;
                case 4:
                    result.R = (byte)(t * 255);
                    result.G = (byte)(p * 255);
                    result.B = (byte)(this.B * 255);
                    break;
                case 5:
                    result.R = (byte)(this.B * 255);
                    result.G = (byte)(p * 255);
                    result.B = (byte)(q * 255);
                    break;
            }

            return result;
        }

    }
}
