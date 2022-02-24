using MGCore;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

using Vector2 = Farseer.Xna.Framework.Vector2;

namespace FarseerPhysicsView
{

    public struct StringData
    {
        public readonly Color Color;

        public float Scale;


        public string _text;
        public string Text
        {
            get
            {
                if (_text == null)
                {
                    _text = stringBuilderText?.ToString();
                }

                return _text;
            }
        }

        public readonly StringBuilder stringBuilderText;
        public readonly Vector2 Position;



        public StringData(Vector2 position, string text, Color color, float scale = 1f)
        {
            Position = position;
            _text = text;
            stringBuilderText = null;
            Color = color;
            Scale = scale;
        }


        public StringData(Microsoft.Xna.Framework.Vector2 position, string text, Color color)
        {
            Position = position.ToVector2();
            _text = text;
            stringBuilderText = null;
            Color = color;
            Scale = 1.0f;
        }

        public StringData(Vector2 position, StringBuilder text, Color color)
        {
            Position = position;
            _text = null;
            stringBuilderText = text;
            Color = color;
            Scale = 1.0f;
        }


    }


}
