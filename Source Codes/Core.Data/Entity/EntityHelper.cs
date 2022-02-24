//#define TIGHTENJOINTEXPERIMENT// this is also in YNRD plugin.. but cannot affect ais during tuning becuase its a directed class and a technical
//limitation.  the Level could have a plugin..that could do this for all...control a controller..
#define PARALLELUPDATE

using Farseer.Xna.Framework;
using FarseerPhysics.Collision;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Data.Entity
{
    /// <summary>
    /// Helper to iterate Spirits, update all plugins, etc
    /// Should be accessible 
    /// </summary>
    public class EntityHelper
    {

#if !SILVERLIGHT || PRODUCTION
        /// <summary>
        /// if false then it wont update offscreen stuff like cloulds and dust emitters that slow the tool..
        /// </summary>
        public static bool UpdateMarginEntities = true;
#endif



        static EntityHelper()
        {
            timer.Start();
        }

        static Stopwatch timer = new Stopwatch();

        static int pluginCount = 0;
        static int spiritCount = 0;

        public  static void UpdateAllEntities(Level level, float dt)
        {


            float startime = timer.ElapsedTicks;

            pluginCount = 0;
            spiritCount = 0;

            try
            {


                if (level == null)
                    return;


#if TIGHTENJOINTEXPERIMENT
                //walk needs to be returned if cranking this up.   so try it if nothing walking for now.
                //also had to crank up joint breakpoints in plugin..  
                //might be a worth second look , for now doesnt see to help much, makes action and joint strenght inconsisent
                AdjustIterationsToTightenJoints(level);  // this is also in ynrd plugin.. but should apply to AIs also..
#endif

#if PARALLELUPDATE
                Parallel.ForEach(level.Entities, e =>             
                {
                    if (e == null)
                        return;


                    if (e is Spirit spirit)
                    {
                        spirit.World = World.Instance;
                    }

#if TIGHTENJOINTEXPERIMENT
                    AdjustSoftness(sprt);
#endif

                    e.UpdateThreadSafe(dt);

         
                });
         
#endif      
                
                foreach (IEntity e in level.Entities)
                {
                    Spirit spirit = e as Spirit;


                    if (spirit != null)
                    {
                        spiritCount++;
                        if ( spirit.Plugin != null)
                        {
                            pluginCount++;
                        }
                    }

#if !PARALLELUPDATE
               
                    if (spirit != null)
                    {
                        spirit.World = World.Instance;
     
                    }

#if TIGHTENJOINTEXPERIMENT
                      AdjustSoftness(sprt);
#endif

                   
                   
					 if (e == null)
                    {
                        Debug.WriteLine("unexpted null entity");
                        continue;
                    }

                    e.UpdateThreadSafe(dt);
                  

#endif
                        e.Update(dt);//this modifies level collections that are not concurrent



                    if (!UpdateMarginEntities)  //this is for tool, if slow user cant set dont emitt dust or clouds , dont update spirits  in margin..
                    {
                        if (spirit != null && (spirit.MainBody.Info & BodyInfo.InMargin) != 0)
                        {
                            AABB aabb = e.EntityAABB;
                            AABB levelAABB = Level.Instance.BoundsAABB;
                            bool isInside = AABB.TestOverlap(ref levelAABB, ref aabb);

                            if (!isInside)
                                continue;
                        }
                    }


                    if (e is Particle)  //todo REMOVE BROKEN PIECES IF FAR FROM ACTION.. AND SLOW..
                    {
                        Particle particle = (e as Particle);
                        if (particle.IsDead)
                        {  
                            
                            if (e is FieldParticle)
                            {
                                  (e as FieldParticle).Unlisten();
                            }
                            
                                                
                            // schedule for removal when physics data is not locked as readonly
                            level.CacheRemoveEntity(e);      
                        }
                    }
					else
                    if (spirit != null && spirit.IsExpired)
                    {
                       
                       level.CacheRemoveEntity(e);
                    }
                }   // end of foreach (IEntity e in level.Entities) 
            }


           
            catch (Exception exc)
            {
                Debug.WriteLine(exc.Message);
            }

            finally
            {
               World.Instance.SpiritCount = spiritCount;
               World.Instance.PluginCount = pluginCount;
               World.Instance.PluginsUpdateTime = timer.ElapsedTicks - startime;
            }
        }


        public static void DrawAllEntities(Level level, double dt)
        {
            try
            {
                if (level == null)
                    return;


                foreach (IEntity e in level.Entities)
                {   
                    if (e == null)
                    {
                        Debug.WriteLine(" Unexpected null entity in DrawAllEntites");
                        continue;   
                    }
                      
                    e.Draw(dt);             
                }  
            }

            catch (Exception exc)
            {
                Debug.WriteLine(exc.Message);
            }
        }


        private static void AdjustIterationsToTightenJoints(Level level)
        {
            var walkingSpirit = level.Spirits.FirstOrDefault(x => (x.IsWalking && x.IsSelfCollide == false));

            if (walkingSpirit == null)
            {
                FarseerPhysics.Settings.VelocityIterations = 30;  //need to soften joints
                FarseerPhysics.Settings.PositionIterations = 20;
            }
            else
            {//was tuned for walking
                FarseerPhysics.Settings.VelocityIterations = level.VelocityIterations;
                FarseerPhysics.Settings.PositionIterations = level.PositionIterations;
            }

            FarseerPhysics.Settings.VelocityIterations = 30;  //need to soften joints if this is high.   its to linear an effect
            FarseerPhysics.Settings.PositionIterations = 20;   //seems to make little difference
        }


        private static void AdjustSoftness(Spirit sprt)
        {
#if TIGHTENJOINTEXPERIMENT
            if (sprt != null && !sprt.IsDead)
            {
                if (FarseerPhysics.Settings.VelocityIterations > 14)
                {
                    sprt.JointSoftness = 0.2f;
                }
                else
                {
                    sprt.JointSoftness = 0;
                }
            }
#endif
        }


        /// <summary>
        /// Calculate CM of the system from a given body list.. done by a weighted average of the body cm in system.
        /// </summary>
        /// <param name="bodies">body list</param>
        /// <param name="result">cm result</param>
        ///   <param name="totalMass"></param>

        public static void CalcCM(IEnumerable<Body> bodies, out Vector2 centroid, out float totalMass)
        {
            centroid = Vector2.Zero;
            totalMass = 0;
            //NOTE since complex body is done this way the weighted average of the weighted averages should be same as the weighted average over all the fixtures.
            //however the average of the averages is generally not the average..
            //but since these are weighted averages i think its ok. we don't have to iterate all the fixtures again.
            //TODO FUTURE compare and check this.  and accurate CM  is important for spirit self balancing
            //for creature all the extremities are simple polygon on fixture so its not that important.  
            foreach (Body b in bodies)
            {
                centroid += b.Mass * b.WorldCenter;
                totalMass += b.Mass;
            }
            // update spirit cm pos
            if (totalMass != 0)
            {
                centroid /= totalMass;
            }
            return;
        }

    }
}
