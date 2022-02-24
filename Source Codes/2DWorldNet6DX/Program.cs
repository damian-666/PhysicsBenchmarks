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

using FarseerPhysicsView;
using Core.Game.MG.Simulation;
using DesktopApp;

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

            CoreGame.IsDirectX = true;//
            WindDrag.ViewRays = false;

            Input.UseVirtSticks=true;
        }

        static void Main(string[] args)
        {


            CoreGame.IsDirectX = true;//
           
            DebugView.LoadThumbnails = true;// TODO in netCore the method decompress doesnt return

            Serialization.IsNet6Folder = true;  ///bin is two levels down on these 


            CoreGame.LoadSettings += Settings.LoadSettings;//must be called from initialization
            CoreGame.OnBeginGameCode += BeginCode;

            using (var game = new CoreGame())
            {
                try
                {
                    MGCore.MGCore.Emitter.AddObserver(CoreEvents.GraphicsDeviceReset, Settings.OnGraphicsDeviceSizeChanged);
                    game.Run();  //this starts the render loop
                }catch (Exception ex)
                {

                    Debug.WriteLine("Main "+ ex.ToString());

                }
            }    
        }

        static void BeginCode()
        {

            WindDrag.ViewRays = false;
#if !DEBUG
          //  PhysicsThread.TargetFrameDT = 1000 / 300;
#endif

#if RELOAD
            DesktopApp.Settings.ReloadLastSaved();  //uncomment this first, trace it.   issue wiht the threading
#endif
        }
    }
}
