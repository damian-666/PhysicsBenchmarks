global using Farseer.Xna.Framework;


namespace BenchmarkTests // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Physics comparison xna vec");



            PhysicsTests.SetTimerRes(1);

            unsafe
            {
                int size = sizeof(Vector2);
                Console.WriteLine("xna Vector2 Size " + size);

            }


            PhysicsTests.NormaizeVec2Test();
            PhysicsTests.CDTDecompose();
            PhysicsTests.ToiBenchmark.TOItest();

        }

        
    }
}