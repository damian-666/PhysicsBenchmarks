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
    }
}
