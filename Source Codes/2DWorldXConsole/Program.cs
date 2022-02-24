#define RELOAD
using _2DWorldCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using MGCore;

using Storage;
using System.Diagnostics;


using System.Runtime.InteropServices;
using ConsoleApp;
using Core.Game.MG.Simulation;

namespace _2DWorldXConsole
{

    class Program
    {

        static Program()
        {

            Serialization.IsNet6Folder = true;  ///bin is two levels down on these 
#if RELOAD
            CoreGame.LooseFiles = true;
#else
            DataStore.LooseFiles = false;
#endif

            CoreGame.IsDirectX = false;//


            WindDrag.ViewRays = false;
        }

        static void Main(string[] args)
        {
    
            CoreGame.LoadSettings += Settings.LoadSettings;//must be called from initialization
            CoreGame.OnBeginGameCode += BeginCode;

            using (var game = new CoreGame())
            {
                MGCore.MGCore.Emitter.AddObserver(CoreEvents.GraphicsDeviceReset, Settings.OnGraphicsDeviceSizeChanged);
                game.Run();  //this starts the render loop
            }    
        }

        static void BeginCode()
        {

#if RELOAD
            ConsoleApp.Settings.ReloadLastSaved();  //uncomment this first, trace it.   issue wiht the threading
#endif
        }
    }
}
