using System;
using System.Net;

using System.Collections.Generic;
using System.Diagnostics;

namespace FarseerPhysics.Dynamics.Particles
{

//for FUTURE only.. this will  reduce # GC, more consistent frame rate..especiall for things like Clouds with lots of verts treated
//as particles.  otherwise might reduce occasional stutter on mobiles especially
//TODO recycle views also?   //we dont know if this even is a noticeable issue, DONT PREMATURELY OPTIMIZE ALREADY WRITTEN STUFF
    public class ParticleGenerator<T> where T : new()
    {
        const int DEFAULT_SIZE = 100;
        Queue<T> particles;

        public ParticleGenerator()
        {
            particles = new Queue<T>();
            CreateParticles(DEFAULT_SIZE);
        }


        private void CreateParticles(int n)
        {
            for (int i = 0; i < n; i++)
                particles.Enqueue(new T());
        }

        public T GetParticle()
        {

                if (particles.Count == 0)
                {
                    CreateParticles(DEFAULT_SIZE);
                }

                T particle = particles.Dequeue();// TODO if this fails add 10 more?


                if (particle != null)
                    return particle;
                else
                {
                    CreateParticles(DEFAULT_SIZE / 2);  //TODO if large textures see if memory..etc..share texture data..
                    return particles.Dequeue();// TODO if this fails add 10 more?
                }

        }

        /// <summary>
        /// return particle to be reused.  Particle will be dead with bad data, must be refreshed
        /// </summary>
        /// <param name="p"></param>
        public void ReturnParticle(T p)
        {
            particles.Enqueue(p);
        }
    }
}
