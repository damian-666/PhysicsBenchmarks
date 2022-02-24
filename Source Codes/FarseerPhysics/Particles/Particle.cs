using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Collision;



namespace FarseerPhysics.Dynamics.Particles
{
    /// <summary>
    /// A body with a additional properties for effects.. 
    /// </summary>
    [DataContract]
    public class Particle : Body
    {
        #region PROPERTIES

     
        /// <summary>
        /// Life span of this particle in msec
        /// </summary>
        [DataMember]
        public float LifeSpan { get; set; }

        /// <summary>
        /// Age of this particle in MILLISECONDS.  ( Note  should have been seconds like everything else, not a good idea to change it now)
        /// </summary>
        [DataMember]
        public double Age { get; set; }

        public bool IsDead
        {
            get { return Age >= LifeSpan; }
        }

        /// <summary>
        /// Radius of the Particle
        /// </summary>
        [DataMember]
        public float ParticleSize { get; set; }



        /// <summary>
        /// Stickiness of particle.  so balloon sparts don't stick to balloon, but dust does. 0 means don't 1 means always.   maybe later some will be in between. 
        /// </summary>
        [DataMember]
        public float ElectroStatic { get; set; }


        /// <summary>
        /// used for cloud parts to dissipate , shrink
        /// </summary>
        public float ScaleFactorX;
        public float ScaleFactorYMin;//randomize this a bit, change the aspect ratio
        public float ScaleFactorYMax;

        public short FrameCountPerScale;  //skip frames.   dont need to   on oscilliatino blood particles move like .3 meter per frame.. mabye as a functino of speed

        public float MagnitudeAspectOscillation { get; set; }   //sin osciallation for liquid blob
        public float OscillationPeriod { get; set; } // period  in millsec
        public float OscillationPeriodDeviation { get; set; } // period  in millsec

        /// <summary>
        /// measn the size is in pixels, otherwise its world like everyting else
        /// </summary>
        public bool SizeDivideByZoom { get; set; }

        private float _phaseShift;// just a random phase so all particles dont blob in sync

        //this is the diameter of jsut the X coordinate when particle is ellipse and blobbing
        public float CurrentParticleSizeX;

        public bool IsNeedingRedraw = false;

        /// <summary>
        /// if true , will be erase if starting inside an object, otherwise not  .  default is true ..  
        /// </summary>
        public bool CheckInsideOnCollision;

        /// <summary>
        /// The body that contains the emitter this came from.. used  to avoid check collide with parent body on ray collide check..
        /// </summary>
        public Body ParentBody;


        /// <summary>
        ///   if Is is NotCollidable , dont even check using  rays.. faster for  lots of rain  with no creature around..  
        /// </summary>
        public bool SkipRayCollisionCheck;



        public bool UseEulerianWindOnly=false;


        /// <summary>
        /// Applies a force at the center of mass.
        /// </summary>
        /// <param name="force">The force.</param>
        new public void ApplyForce(Vector2 force)  //this hides the body applyForce.  does not include gravity..
        {
            Force += force;  //don't bother with torque on particles or bullets
        }

        public Vector2 RandomForceMax;


        public float DragDeviation { get; set; }

        public bool BubbleMotion { get; set; }

        protected int frameCount;



        #endregion

        public Particle(World world)
            : base(world)
        {
            //TODO look in physics, other optimizations might be possible.  '
            //consdier Body should actually derive from particle.. or a separate entity
            //particles should probably be in separate world.. body list..

            // still calcs rotation stuff like torque
            FixedRotation = true;

            LifeSpan = 1000;
            Age = 0;
            frameCount = 0;
            ScaleFactorYMin = 0;
            ScaleFactorYMax = 0;
            ScaleFactorX = 0;



            CheckInsideOnCollision = false;
            ElectroStatic = 1f;     // default is always sticky

            DragDeviation = 0;

            BubbleMotion = true;





            /*http://nullcandy.com/2d-metaballs-in-xna/


            //    http://fumufumu.q-games.com/gdc2010/shooterGDC.pdf
            //    http://www.somethinghitme.com/2012/06/06/2d-metaballs-with-canvas/
            http://codeartist.mx/tutorials/liquids/
            */

        }

 


        //this happens on UI thread
        public override void Update(double dt)
        {
            if (IsDead)
                return;

            Age += dt *1000 ;

            CalculateAcceleration();

            DoScaling();
            DoAspectOscillation();
            //  randomMotionMag = 2f;//tuned to 1 and 3 //


            if (BubbleMotion)
            { 

                //TODO kill particles if particle is too big rel to screen..  or scale on update..
                if (MathUtils.IsOneIn(3))  //let the body coast some frames.   currently used for bubble, TODO  or snowflake
                {
                    if (RandomForceMax != Vector2.Zero)
                    {
                        ApplyForce(new Vector2(MathUtils.RandomNumber(-RandomForceMax.X, RandomForceMax.X),
                             MathUtils.RandomNumber(-RandomForceMax.Y, RandomForceMax.Y)));
                    }
                }
           }

        }


        public void Kill()
        {
            LifeSpan = 0;
        }

        private void DoAspectOscillation()
        {
            FixedRotation = false;

            if (OscillationPeriod==0)
                return;

            if ( _phaseShift == 0)
            {
                _phaseShift = MathUtils.RandomNumber( 0, Settings.Pi );
            }


             if (AngularVelocity == 0)  //TODO set as constant or variable somewhere
             {
                 AngularVelocity = MathUtils.RandomNumber(3, 10);
             }

            UpdateSize();

        }

        protected  void  UpdateSize()
        {
            CurrentParticleSizeX = ParticleSize + MagnitudeAspectOscillation * ParticleSize * ((float)Math.Sin(_phaseShift + 2 * Settings.Pi * Age / OscillationPeriod));  //sin is from -1 to 1.. we want 
            IsNeedingRedraw = true;
        }


        //TODO do this scaling on every zoom..  as in cache update views.    like we do with images
        //otherwise end up with huge dust balls zooming in ..  ( or.. kill the particles if too big on current scale) 
        private void DoScaling()
        {

            if (ScaleFactorX == 0 || ScaleFactorYMax == 0 )
                return;

            if (ScaleFactorX == 1.0 && ScaleFactorYMax == 1.0 && ScaleFactorYMin==1.0)
                return;
 
            frameCount++;
            if (FrameCountPerScale == 0 || (frameCount % FrameCountPerScale == 0))
            {            
                float scaleFactorY = (ScaleFactorYMin == ScaleFactorYMax) ? ScaleFactorYMax : MathUtils.RandomNumber(ScaleFactorYMin, ScaleFactorYMax);
                ScaleLocal(new Vector2(ScaleFactorX,scaleFactorY)  );
            }

            UpdateAABB();

            ParticleSize = (AABB.Width + AABB.Height) / 2.0f;

            if ((Info & BodyInfo.Cloud) != 0)
            {//HACK
                //TODO REMOVE workaround for clouds we want really low denisty strking body  doesnt  feel  much reaction .. 
                //but to avoid numerical errors, with impulse claming reduce this..  also in update particle
                ParticleSize /= 100f;  //retest rainclouds i guess.. should just put a flag,no reaction or somethign
            }
        }
        //   else
        //   {
        //       System.Diagnostics.Debug.WriteLine("paritcledead");
        //    }




    }


#if FUTURE  // IEntity  TODO  might have an Update called  for all bodies, after Controllers update.  since will update everything on background thread.
        //  currently Ientiti update is called from UI thread, on PreUpdatePhysics, and UI /graphics thread is often more loaded than physics.
       //this is currently done in winddrag on multiple threads
        public override void UpdatePhysics( double dt){
             if (IsNotCollideable)
            {
               
                if (   CheckParticleCollision(dt))  //TODO this might better be called from a separate background thread.., loop through all particles and sync with physics update..
                {
                    LifeSpan = 0;
                    // body.ApplyForce(new Vector2(-200, -2));  //bounce off?
                    //TODO bounce at tangent..?   then set lifspan to 1000;  or depend on angle..  when on groudn should die.
                }
            
            }
        }
        
        bool CheckForwardRayCollide(Vector2 applicationPoint, Vector2 vel)
        {

            Vector2 rayEnd1 = applicationPoint + vel;

            // dont do ray if length == zero, cause tree exception
            if (vel == Vector2.Zero)
            {
                return false;
            }

            // Vector2 normalAtCollision;  for bouncing.

            bool hitSomething = false;

            // RaycastOneHit:  TODO might break this into a function..
            //TODO this ignores shapes that contains the starting point.. see if  we can modify it to not do that
            //particles get insie stuff and stay there.  rather not do another hit test.
            // a graphics backing pixel test would also be nice if we could access backing store..
            World.Instance.RayCast((fixture, point, normal, fraction) =>
            {
                Body body = fixture.Body;

                if (fixture.Body.IsNotCollideable)
                    return 0;

                hitSomething = true;
                //   intersectedFixture = fixture;
                //  normalAtCollision = normal;

                return fraction;
            }, applicationPoint, rayEnd1);

            return hitSomething;

        }


        public  bool CheckParticleCollision( double dt)
        {

            //TODO check why this is soo much slower than  CheckForwardRayCollide   34 ms update time phyisc must optimise this for wind..
            //    bool blockedForward = CheckBlocking(body.WorldCenter, body.LinearVelocity, 0, body.LinearVelocity.Length() * dt, sensor, "particle Collide" + body.GetHashCode().ToString(), null, null, false,false);

            float rayExtension = 1.05f;  //make 5 percent longer.. due to variable  accel , this is not reliable..
            
            if ( CheckForwardRayCollide(WorldCenter, (LinearVelocity * (float)dt * rayExtension)))
                 return true;

            return false;
        } 
#endif

}


