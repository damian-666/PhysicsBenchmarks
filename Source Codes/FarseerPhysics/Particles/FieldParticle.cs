using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Collision;
using FarseerPhysics.Controllers;
using System.Diagnostics;

namespace FarseerPhysics.Dynamics.Particles
{
    //TODO UNUSED, its to give some idea of turbulence or ground interaction, probably use a grid based method so we get swirly and pressure

    //NOTE this untested , no IField is added, and as expensive as piling.   a flock of particles could be avected by this and there would be vorticity but Eulerian methods are more promising and dont require particles.
    //they could be emitted as vortons to add some small scall vorticity to the flow, . 
    //ideally the positoin and vel would be rendered onto a grid for lookup to avoid particles from having to query for one of these and determine a velocity of a local pt in it
    //IField colleciont would have to use broadphase to collect these -per- particle, 
    /// <summary>
    /// this is a large smooth colliding SuperParticle that is part of a  wind velocity field, determined based on its velocity.
    /// it represents a cloud of air or hot gas.   A home brewed Particle-in-cell
    /// This can be used to make wind fields that flow around objects and terrain
    /// particles are  generally smooth, collide with each other ( to produce "pressure") and represent a section of air
    /// used by rocket exhaust.. could be used by wind producers on a static terrain and even cache the results on a grid for the level.
    /// </summary>
    public class FieldParticle : Particle // IField
    {
      //  float _fieldSizeFactor = 1f;
     //   float _fieldDistSq = 1f * 1f;


        public float Speed = 0;
        public float DistanceDecaySlope = 10;  //linearly decreases with distance


        public enum Shape { square, circle };

     //   string OnEmitsound;

        private Shape _metashape;


        /// <summary>
        /// related by Thermodynamics.. well do a rough approximation..  t = pv?
        /// </summary>
        public float Temperature = 0; 
        public float Pressure = 0;

        public float TotalImpulse{ get; set; }

        //TODO test
        [DataMember]
        public CollisionFilter MetaGroupFilter { get; set; }   //the particles collide with those is a group. using several .groups might overlap.. for a more full field..

      //  List<Vector2> collideDirVecs = new List<Vector2>(4);  //gradients?? thermal?
//we use mechanical energy for that. for now..more collisions,impulse, higher density, is higher temp,


      //  public float  FactorSurrounded = 0;  //used to determine density , heat dissipation or not,  betwee 0 and 1;  TODO?   dir of impulses after solve


        public int numCollisions;

        [DataMember]
        public Shape MetaShape {
            set {

                if (value == _metashape)
                    return;
                
                _metashape = value;
                FixedRotation = (_metashape == Shape.square);
                NotifyPropertyChanged("MetaShape");
   
                }

                get{

                return _metashape;

                }

            }


        //TODO  cosider just hold a ref to the emitter..

        [DataMember]
        public float SuperSizeDeviation { get; set; }
      

        public float CurrentSuperSize { get; set; }

        [DataMember]
        public float SuperSize { get; set; }



        /// <summary>
        /// Create a particle representing a cloud of particles, a field of gas.. This is instead of the common grid based methods.
        /// More appropriate for blasting high pressure compressible flows.. the Navier Stokes are not used, maybe some ideas.
        /// This is a  incompressible gas,  shock waves can go through it.  it expands and contracts  violently.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="fieldSize"></param>
        /// <param name="shape"></param>
        public FieldParticle(World world)
            : base(world)
        {
            _metashape = Shape.circle;

            SuperSizeDeviation = 1;

            FixedRotation = false;

            LifeSpan = 2000;  //msec,TODO  life span,...   based on temp..or affected by
            Age = 0;
            frameCount = 0;
            ScaleFactorYMin = 0;
            ScaleFactorYMax = 0;
            ScaleFactorX = 0;
            ElectroStatic = 1f;     // default is always sticky

            DragDeviation = 0;

            IgnoreGravity = true;

            DragCoefficient = 0.4f;

            //  BuoyancyController  put force related to relative density..    take 4 pts..  get from neighbor touching? or query..

            //  OscillationPeriod = 0.5f;
            OscillationPeriod = 0;

            AfterCollision += FieldParticle_AfterCollision;



            Temperature = 0;   //if temp is high enough will all a light source..   will help determine the areas under the affect of this gas, in case of thing walls

         //   FactorSurrounded = 0;

        }

        //this can get a rough idea of the pressure it under..

        private void FieldParticle_AfterCollision(Fixture fixtureA, Fixture fixtureB, Contacts.Contact contact)
        {

            //try to determine pressure using the forces on this

            if (fixtureB.CollisionFilter != MetaGroupFilter  )
            {
                Debug.WriteLine("unepected collsion " + MetaGroupFilter + "fixA " + fixtureA.CollisionFilter + "fixB" +  fixtureB.CollisionFilter);
            }

            float maxNormalImpulse = 0;

            if (contact.Manifold.PointCount > 1)
            {
                maxNormalImpulse = Math.Max(contact.Manifold.Points[0].NormalImpulse, contact.Manifold.Points[1].NormalImpulse);
            } else
                maxNormalImpulse = contact.Manifold.Points[0].NormalImpulse;


            TotalImpulse += maxNormalImpulse;


            numCollisions++;


            Vector2 vecLocalCollision = contact.Manifold.Points[0].LocalPoint;

            vecLocalCollision.Length();


            Debug.WriteLine("totat imp" + TotalImpulse);


            Debug.WriteLine("numCollisions" + numCollisions);


            Debug.WriteLine(" _totalContactForceOnThis " + _totalContactForceOnThis);


            //   Debug.WriteLine("len collisionvec  " + vecLocalCollision.Length());




            //   vecLocalCollision.Normalize();

            //    collideDirVecs.Add(contact.Manifold.Points[0].LocalPoint);


            //TODO .. make a polygon.. see if it goes around 

            //    if (contact.Manifold.Points[0].NormalImpulse < 0)
            //    {
            //        Debug.WriteLine("unepected - 0 impulse");
            //    }
            //since this is dont after collision, we dont need to account for the directions... if they 
            //are all opposing the impuluse will go up.  (TODO verify) 


            //see if the total angle between the vects   angle to using atan2..
            //note impulse shouldu be enough...


            //when a particle takes of with speed, it might leave vortex..orther effect, Reynolds number or viscosity or
            //friction matters..   but we are modeling that with collision

        }


        public override void Update(double dt)
        {
            base.Update(dt);

            numCollisions = 0;
            TotalImpulse = 0;  // use our impulse based pressure solver 

            ResetMassData();

            CurrentSuperSize = MathUtils.GetRandomValueDeviationFactor(SuperSize, SuperSizeDeviation);

            ResetMassData();

            //TODO oscillation.. stay circle .. allow compression of shocks or longitudinal waves by erro in pos..  this was cosmetic..

        }


        public void Unlisten()
        {
            AfterCollision -= FieldParticle_AfterCollision;
        }


        /// <summary>
        /// This is simulates an incompressibility gas , a bit like SPH but no attraction,or grids..just dispersive,  and for
        /// finding enclosures
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="collisionCat"></param>
        /// <param name="friction"></param>
        public void AddMetaCollider(float MaxRadius, float MinRadius,  short collisionCat, float friction, float oscillationPeriod)
        {

           Fixture groupRepulsion = CreateFixture(new Collision.Shapes.CircleShape(MaxRadius, 0));// some incompressibility 

            OscillationPeriod = oscillationPeriod;  //this can send shock waves if set fast .. must be careful it can mess up contacts,must be continuous


        }


        //   public void AddMetaCollider(float MaxRadius,  short collisionCat)
        //   {
        //       Fixture groupRepulsion = CreateFixture(new Collision.Shapes.CircleShape(MaxRadius, 0));//   
        //   }





        //notes for us in hig pressure rocket thrust..
        //info from JPL rocket PV=nRT ideal gas.. press * volume = amount *constant* Temp
        //pressure 
        //   Pg = (γ−1)ρgϵg
        ///for rocketPg=(γ−1)ρgϵg
        //Turn MathJax on
        //   fD=34CDαpρgd|ug−up|(ug−up).
        //  fD = 34CDαpρgd | ug−up | (ug−up).
        //   fD=34CDαpρgd|ug−up|(ug−up).

        //   D is particle diameter   C is dimensonseldrag ( between hot gass and surrounding)
        //   Re=ρgd|ug−up|μg.
        //   qT=Nu6κgd2αp(Tg−Tp),   thermal conductivity..
        //so.. waves .. waveles..will be generated..oscillatoins.
        //we will just muck around

        //View the MathML source in which γ is the constant ratio of specific heats.Since the density of particles ρp exceeds the gas density ρg by orders of magnitude, the virtual mass force is neglected.The lift force, gravity, and other interfacial effects are also negligible[6] as compared to the viscous drag force defined as

        //one big particle can create a vortex..to add turbulence..to make  them inter act.. can take some sampled for the ar ( excluding our own , or not) and apply a motion..

        //or use collisionand the Gas

        //            var dx = particle.x - vortex.x
        //var dy = particle.y - vortex.y
        //Then we rotate that vector 90 degrees to make an orthogonal one and multiply it by the vortex rotation speed:

        //var vx = -dy * vortex.speed
        //var vy = dx * vortex.speed
        //Great, we have the velocity that the vortex is trying to force on the particles, but how do we apply it ?
        //We need some weighting factor which will be 1 in the center of the vortex and will fade out with distance.
        //We'll use this ad-hoc formula, which is based on inverse square distance, but avoids singularity at the center:

        //var factor = 1 / (1 + (dx * dx + dy * dy) / vortex.scale)

        /// <summary>
        /// the radius in which is has full strength as a factor of supersize , or collision size, ,and vorticity..after that it drops of with distance
        /// </summary>

        [DataMember]
        public float FieldRadiusFactor { get; set; }
       


    }






}
