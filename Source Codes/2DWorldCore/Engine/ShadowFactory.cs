using Microsoft.Xna.Framework.Graphics;
using System;


namespace _2DWorldCore
{
    // Singleton for the ShadowPlay Silverlight Engine.. handles game UI and puts everything together
    //TODO remove this class
    public class ShadowFactory
    {

        static private Engine _engineInstance = null;

        // Initialization of Engine, bind the Engine into Silverlight Application object
        static public Engine InitEngine( GraphicsDevice gr)
        {
            if (_engineInstance == null)
                _engineInstance = new Engine(gr);

            return _engineInstance;
        }

        static public Engine Engine
        {
            get
            {
                if (_engineInstance == null) throw new ArgumentException(
                    " call ShadowFactory.InitEngine(Application) first.");

                return _engineInstance;
            }
        }
    }
}
