using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timers;

namespace BenchmarkTests
{

    
    public class PhysicsTests
    {

        public const int ITERATIONS = 100000;

        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint timeBeginPeriod(uint uMilliseconds);


        public struct vecCop
        {
            public float x;
            public  float y;
        }

        public  static void  SetTimerRes(uint ms = 1)
        {
           timeBeginPeriod( ms);

        }

        public static void NormaizeVec2Test(int count = ITERATIONS, bool copyVec = false)
        {



            Vector2 total = Vector2.Zero;

            Vector2 vec = Vector2.Zero;

            Random random = new Random();

            using (new StopWatchTimer("Vec2normalize"))
            {

                for (int i = 0; i < count; i++)
                {

                    vec.X = random.NextSingle();
                    vec.Y = random.NextSingle();

                    total += vec;
                    Vector2.Normalize(vec);
      


                    if (copyVec)
                    {
                        var copy = new vecCop();
                       
                        copy.x = vec.X;

                        copy.y = vec.Y;

                        total.X += copy.x;
                        total.Y += copy.y;

                    }
                    else
                        total += vec;

                }

            }


            Console.WriteLine(" vec2 test  " + StopWatchTimer.LastResultTicks);

            Console.WriteLine("vec2 " + total);  //jut so it wont be optimized out use the result


        }
        public static void CDTDecompose()
        {
            Vertices verts = new Vertices();


            verts.Add(new Vector2(0, 0));
            verts.Add(new Vector2(1, 0));
            verts.Add(new Vector2(0.1f, 0.1f));



            verts.Add(new Vector2(0.1f, 0.2f));
            verts.Add(new Vector2(0.1f, 0.22f));

            verts.Add(new Vector2(0.1f, 0.2f));
            verts.Add(new Vector2(0.1f, 0.18f));


            verts.Add(new Vector2(0, 0));



            List<List<Vertices>> vertsList = new List<List<Vertices>>();


            int count = 0;
            using (new StopWatchTimer("CDTtest"))
            {

                for (int i = 0; i < ITERATIONS; i++)
                {

                    //    var poly = FarseerPhysics.Common.Decomposition.CDTDecomposer.ConvexPartition(verts);


                    //   var poly = FarseerPhysics.Common.Decomposition.SeidelDecomposer.ConvexPartition(verts,0.1f);



                    var poly = FarseerPhysics.Common.Decomposition.EarclipDecomposer.ConvexPartition(verts);

                    //  vertsList.Add(poly);
                    count += poly.Count;
                }
            }

            Console.WriteLine("dt ticks :" + StopWatchTimer.LastResultTicks);

            Console.WriteLine(" parts + " + count);

            Console.WriteLine(" parts + " + vertsList.Count);
        }




        public  class ToiBenchmark
        {
            private PolygonShape _shapeA;
            private PolygonShape _shapeB;
            private Sweep _sweepA;
            private Sweep _sweepB;

            public void Setup()
            {
                _shapeA = new PolygonShape(PolygonTools.CreateRectangle(25.0f, 5.0f), 0);
                _shapeB = new PolygonShape(PolygonTools.CreateRectangle(2.5f, 2.5f), 0);

                _sweepA = new Sweep();
                _sweepA.C0 = new Vector2(24.0f, -60.0f);
                _sweepA.A0 = 2.95f;
                _sweepA.C = _sweepA.C0;
                _sweepA.A = _sweepA.A0;
                _sweepA.LocalCenter = Vector2.Zero;

                _sweepB = new Sweep();
                _sweepB.C0 = new Vector2(53.474274f, -50.252514f);
                _sweepB.A0 = 513.36676f;
                _sweepB.C = new Vector2(54.595478f, -51.083473f);
                _sweepB.A = 513.62781f;
                _sweepB.LocalCenter = Vector2.Zero;
            }


            public   TOIOutput Distance()
            {
                TOIInput input = new TOIInput();
                input.ProxyA = new DistanceProxy();

                TOIOutput output = new TOIOutput();

                input.ProxyA.Set(_shapeA, 0);

              input.ProxyB = new DistanceProxy();

                    input.ProxyB.Set(_shapeB, 0);


                input.SweepA = _sweepA;
                input.SweepB = _sweepB;
                input.TMax = 1.0f;

             //     TimeOfImpact.CalculateTimeOfImpact(out output, ref input);

                TimeOfImpact.CalculateTimeOfImpact(out  output, ref input);


                return output;
            }



            public static void TOItest(int count = ITERATIONS)
            {

                ToiBenchmark benchmark = new ToiBenchmark();
                benchmark.Setup();
              

                TOIOutput output;


                int touches = 0;


                using (new StopWatchTimer("TOIest"))
                { 

                    for (int i = 0; i < count; i++)
                    {
                        output = benchmark.Distance();

                        if (output.State == TOIOutputState.Touching)
                        {
                            touches++;
                        }


                    }
                }


        
                Console.WriteLine(" toi test  " + StopWatchTimer.LastResultTicks);

                Console.WriteLine("toi touches " + touches);  //jut so it wont be optimized out use the result


            }




        }






    }
}
