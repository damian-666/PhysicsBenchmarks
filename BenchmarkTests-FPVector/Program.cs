global using System.Numerics;




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

            PhysicsTests.NormaizeVec2Test();
            PhysicsTests.CDTDecompose();
            PhysicsTests.ToiBenchmark.TOItest();
            

        }

    
    }
}