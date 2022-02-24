using _2DWorldCore;
using Core.Data;
using Core.Game.MG;
using Core.Game.MG.Graphics;
using Core.Game.MG.Plugins;
using Core.Game.MG.Simulation;
using Core.Trace;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysicsView;
using MGCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace _2DWorldCore
{
    static class GameStart
    {

        static GameUI ui;

        
        static IGameCode gameCode;
        static DebugView physicsView;

        public static DebugView PhysicsView { get { return physicsView; } }

        public static void Init(GraphicsDevice gr)
        {

            SimWorld.IsWindOn = true;
            SimWorld.IsParticleOn = true;


            SimWorld.EnableSounds = true;

            Engine.IsBackgroundThread = true;
            PhysicsThread.IsUsingTimer = false;

#if !PRODUCTION
            GameUI.IsBackgroundMusicOn = false;
#endif
            Body.MarkPoints = true;  //draw isnt visible.. TOOD make test files.. test .. collide .. particl like rain cloud .. single emitter from cloud maybe
#if !PRODUCTION
            CoreGame.ShowDebugInfo = true;
#endif



            PhysicsThread.TargetFrameDT = -1; // 1000 / 300;  // 200 is desired FPS   , will  thread sleep and compensate for physics update time

            if (CoreGame.LooseFiles )
            { 
                PhysicsThread.TargetFrameDT = 1000 / 300;
            }

            if (CoreGame.IsAndroid)
            {
                // THIS WAS FOR LOCKLESS.. TIMESR ARENT GOOD ENOUGH PhysicsThread.TargetFrameDT = 1000/200; //100 was smooth.. trying this  if not sleeping at all.. draw never gets a lock on level like planet
                PhysicsThread.TargetFrameDT = -1;
            }
            else
            {
                PhysicsThread.TargetFrameDT = 1000 / 300;
            }



            ShadowFactory.InitEngine(gr);  //todo check, is it too early.
            ui = new GameUI();
            gameCode = new AutoLevelSwitch(ui);

            var SpiritPluginBase = new SpiritPluginBase();// for android linker  ? todo erase and see if plugins still get packaged


            //this switches take out more code so we can isolate faults
            AudioManager.Instance.IsSoundOn = SimWorld.EnableSounds;
            WindDrag.IsWindControllerOn = SimWorld.IsWindOn;

            FarseerPhysics.Settings.EnableDiagnostics = true;

      
            PhysicsThread.EnableFPS = true;

            Input.Touch.EnableTouchSupport();



            AudioManager.Instance.PreLoadSounds() ;




            //TOD
            //70 is ok, 80ok,  but at  90 to 100 we see animation issues seems not stable
            //.. its promising     ..todo, adjust animation DT by this and retry
            //TODO self collidate and tunnelling refine physics.. should add a tuner UI
            //80 causes 
            ShadowFactory.Engine.World.PhysicsUpdateInterval = 1.0f / 80f; 

            //  ShadowFactory.Engine.ActiveGameCode.Start();    this is how the others set up, this set click handlers.
            InitGraphicsPhysicsView();

            SimWorld.Instance.EmitterPreloading += EmitterRefPreloading;

            ShadowFactory.Engine.ActiveGameCode = gameCode;  //this will  load a file and try to draw so after all the engine and graphics are init



            //  ShadowFactory.Engine.ActiveGameCode = new LevelTest1();
            //   ShadowFactory.Engine.ActiveGameCode = new PilingTest03();        
            //ShadowFactory.Engine.ActiveGameCode = new ColliderTest01()
            //ShadowFactory.Engine.ActiveGameCode = new PlanetPilingTest01();
            //ShadowFactory.Engine.ActiveGameCode = new PyramidPilingTest();
        }

  

        public static void InitGraphicsPhysicsView()
        {

            physicsView = new DebugView(ShadowFactory.Engine.World.Physics);

            physicsView.LoadContent(MGCore.MGCore.Instance.GraphicsDevice, MGCore.MGCore.Instance.Content);


            //we dont use the Monogame update or timers, using background thread to update physics and measure frame times instead
            //draw gets a lock and draws with screen vsync
            if (!Engine.IsBackgroundThread)
            {
                if (PhysicsThread.TargetFrameDT >= 0)
                {
                    MGCore.MGCore.Instance.TargetElapsedTime = TimeSpan.FromMilliseconds(PhysicsThread.TargetFrameDT);
                }
            }

            physicsView.TextScale = CoreGame.IsAndroid ? 2 : 1;
           

        }

        public static void EmitterRefPreloading(IEnumerable<BodyEmitter> bems)
        {
            if (!DebugView.LoadThumbnails)
                return;


            //TODO even 4 cores parallel jpg decode,  takes 3 sec extra to launch cause of this.. prolly should cache the texturess as monogame content files in our bin folder or somethign

          //  using (new TimeExec("xaml parallel parse" ,"Thumbnail"))
            {
                Parallel.ForEach(bems, x => SimWorld.PreloadEntity(x)); //its safe to load thumbs in parallel at least
            }

#if OLDWAY

            PhysicsView.BatchDecompressThumbnails(bems, true);//in net6 it takes a sec now for 8 small thumbs

            PhysicsView.BatchDecompressThumbnails(Level.Instance.GetAllBodiesFromEntities(),true);

#endif
        }
    }
    }
