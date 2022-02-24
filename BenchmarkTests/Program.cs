using System;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;

using FarseerPhysics.Factories;

using Farseer.Xna.Framework;

using Timers;

namespace BenchmarkTests // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Physics comparison");




            Vertices verts  = new Vertices();


            verts.Add(new Vector2(0, 0));
            verts.Add(new Vector2(1, 0));
            verts.Add(new Vector2(0.1f, 0.1f));


            using (new StopWatchTimer("CDTtest"))
            {

                var poly = FarseerPhysics.Common.Decomposition.CDTDecomposer.ConvexPartition(verts);

            }

            Console.WriteLine("dt ticks :" + StopWatchTimer.LastResultTicks);

        }
    }
}