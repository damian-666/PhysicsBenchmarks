global using Farseer.Xna.Framework;

using System;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;

using FarseerPhysics.Factories;


using Timers;

namespace BenchmarkTests // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Physics comparison xna vec");


            unsafe
            {
                int size = sizeof(Vector2);
                Console.WriteLine("xna Vector2 Size " + size);

            }

                PhysicsTests.CDTDecompose();
            PhysicsTests.ToiBenchmark.TOItest();

            PhysicsTests.NormaizeVec2Test();
        }

        
    }
}