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

        const int ITERATIONS = 100000;
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

            using (new StopWatchTimer("CDTtest"))
            {

                for (int i = 0; i < ITERATIONS; i++)
                {

                    var poly = FarseerPhysics.Common.Decomposition.CDTDecomposer.ConvexPartition(verts);

                    vertsList.Add(poly);

                }
            }

            Console.WriteLine("dt ticks :" + StopWatchTimer.LastResultTicks);



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

                float dist = 0;

              input.ProxyB = new DistanceProxy();

                    input.ProxyB.Set(_shapeB, 0);


                input.SweepA = _sweepA;
                input.SweepB = _sweepB;
                input.TMax = 1.0f;

             //     TimeOfImpact.CalculateTimeOfImpact(out output, ref input);

                TimeOfImpact.CalculateTimeOfImpact(out  output, ref input);


                return output;
            }



            public static void TOItest(int count)
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
