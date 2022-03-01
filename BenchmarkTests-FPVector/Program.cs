global using System.Numerics;
using System;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;

using FarseerPhysics.Factories;



using System.Timers;




namespace BenchmarkTests // Note: actual namespace depends on the project name.
{
    internal class Program
    {



        static void Main(string[] args)
        {
            Console.WriteLine("Physics comparison Numerics");


            unsafe
            {
                int size = sizeof(Vector2);
                Console.WriteLine("Vector2 Size " + size);

            }
            PhysicsTests.CDTDecompose();
            PhysicsTests.ToiBenchmark.TOItest();
            PhysicsTests.NormaizeVec2Test();

        }

    
    }
}