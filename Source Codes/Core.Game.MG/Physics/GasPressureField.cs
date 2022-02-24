#if OLDBOMB
using Core.Data.Entity;
using Core.Game.Simulation;
using Farseer.Xna.Framework;
using FarseerPhysics.Collision;
using FarseerPhysics.Common;
using FarseerPhysics.Controllers;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Factories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

using FarseerPhysics.Collision.Shapes;

namespace Core.Game.Simulation
{



//#1 visualization ..  more dust writable bitmap.

//or consider the polish ways... using pinned buffer.. how to zoom that.?  on zoom add more ink

//consider linux.. pinned.. raster.. etc.. ink.
//then the french water

//doesnt need more bubbles.    just a game , get it done.


////TODO smoke using vortons, lagrangian method is really best.   so called field particle?  
//
//variational.. use jacobi,  each body calcuates.. alter the vel field with that.
// for vel field near groud, each
// 2 way coupling?  variational?  extra fixtures for thin?  
//  its just a game.
// two phase would be ideal.   panel method is good enough  to couple.. 

//calc center of pressure?
//is it needed?
//isnt vel field enough to define the gass?



    public class GasPressureField :   IField 
    {



        //THIS IS USING THE LA
        //http://www.cs.cornell.edu/courses/cs5643/2014sp/stuff/BridsonFluidsCourseNotes_SPH_pp83-86.pdf     ideally we should get to this and do proper pressure gradients.  
        //PV=nRT\,where the letters denote pressure, volume, amount (in moles), ideal gas constant, and temperature of the gas, respectively.


            //stuff to read may later.. you have enouhg research..
            //navier stokes shadow?..   for 1d?   //TODO later..   


     //   http://www.ddm.org/DD20/proceedings/DD20proc.pdf

       // dE = dQ - pdV     dQheat xhfer

            //PRESSURE VELOCITY == 
            //  PRESSURE VOLUME  = n ( NUM ATOM)  R  T  EMP
            //C = SQRT ( B/P)     P IS DENSITY  B IS STIFFNESS
         

            //  http://galileo.phys.virginia.edu/classes/311/notes/compflu2/node3.html


            //http://www.brighthub.com/education/homework-tips/articles/78088.aspx   FIND THE NATURAL SPEED OF THE GASS.. SEE WHAT HAPPENS IF YO UBREAK IT
            //TRY ATTACHING A SOUND TO IT.. DO LONGITUDON EFFECTS.


            //   https://en.wikipedia.org/wiki/Kinetic_theory_of_gases  another thoery of gasses is here .. 
            //p = k(  p - p0);  p0 is the environmental pressure..  
            // k is a gas constant that depends on the temperature and ρ0 is the environmental pressure.S

            //occasionally allowing the categories ot mix , will allow particles to expand..   


            // but what will provide a pressure from teh ambient?    taking measurements arond several directions... would help.. but if we calcuLate a pressure gradient derivative field
            //CALCULATE A PRESSURE GRADIENT FIELD.. USING A GRID  IN ONE PASS IN PARRALLEL..


            //( in parallel,in one pass)

            //our first goal is to get particles and smoke to spread out, interact iwth the surroudings, fill up caves.. etc.   lets try that using the fast collision we have..

            World World;
        Collection<FieldParticle> FieldParticles = new Collection<FieldParticle>();   //collection of the  FieldParticle..each with its neighbors, determine a field.  This is similar to Superparticles, or Particle in a cell, but we do not intend to fill the region with particles,it would be too processor intensive
                                                                                      //TODO REVISIT.. using WritableBitmap.. this would al be possible.   the partcles could be  tossed in the filed...but a wind fieldregion,
                                                                                      //that is completely filled in using the shadowing lighting   ( visibiilty polygon)   and Superparticle
                                                                                      //the perlin noise texture could be a good fill.. no bragging about 6K pariticles..though using that and the collision ray to handle collision with walls.

//1.   field particles bounced around to establish a field.. no need to stick to neighbors no SPH.    consider this though.. pressure.. and the cold air around willresist its expansion..  threads will force
//such is the nature of hot gasses expanding, they follow the ond who breaks through.. filments..    so we are breaking into groups.. allowing gasses to mix   would a cold heavy air help? 
//now the ambience is that.. but, the neigbors will help it, say another group is hot.. 
//it will tend to go there..untillit gets full from others in its groud colliding..if the field one creates should affect itself,not sure.   to see under the smoke sims that use grids and Navier Stokes
//2.   add some more particles, even mysterious matter.
//3.   tune blasting...size of particles and restitution
//4   calc temp from neighbors, and ambient and time.. and  density with this.  get some basic thermo equation
//5   diffuse temp.. lifespan..  checking temp at this position, or if no neighbors, no impulse..   assume not hot  (other groups, dont collide,so can just check temp there)   .. to get a grient, need to check around.
//6. calc visible regions.. painde the particles as circles or bumpy path outlines.. ( see planet gen)  ist circles
//7



//1st doe rocket

// do emit of super particle by object beign visibly.. pushed by wind..   might replace blocking...

//   The Semi-Lagrangian scheme[edit]
///Semi-Lagrangian schemes use a regular(Eulerian) grid, just like finite difference methods.
The idea is this: at every time step the Vector2 where a parcel originated from is calculated.
An interpolation scheme is then utilized to estimate the value of the dependent variable at the grid Vector2s 
surrounding the Vector2 where the particle originated from
.The interested reader is encouraged to look through the items in the references list for more details 
on how the Semi-Lagrangian scheme is applied.

//lots of dust will be around where gass is...so.. there will be data.

//  and put  superpartiles around all  objects under influence..

//use vorton particles for smoke.. conterrotatinng pairs

//presure solver.. move to areas of least density.. away from higer density.


//using the box2d collider is not bad
//but the hash space collider in chipmunk is supposed to be  alot faster
//for moving objecs.. can be dont in one day on contract by guy who has done it before
//largrangian method for smoke using vortons..


//need a writable bitmap for the screen .. at the screen resolution.

//this might be useable for wind also..particles and raster based collision.

/// <summary>
/// This is a collection of particles..those each have two fixtures interact with the environment using small circles able to get in tight spaces, but with each as either squares ( perfect packing)   or circles ..to turn each other.. but that can be done without .. or.. mix of both..thats the best..
/// The goal is a bunch of smoke that can be the exhaust of the rocket engine.  There are really good published simulations , using grids, but rely on numerous particles. That can still be done in the region defined here.  The important part is a  violent interaction with the environment using rigid body superparticle to determine the filament or the stream direction on bounce, then they more weakly interact with each other if the groups match
/// TBD.. how much the field ,if at all ,should affect the field particles..this is positive feedback can be unstable.
/// Don't care at all if its realistic or physical , wrt theromdynamics.  Just has to look so.  and burn stuff and be reasonably realistic.
/// Physical theory is not used , Navier Stokes,etc.
/// with the environment, a blast..The equations I see are too wispy.. no like rocket exhaust.   This is all made up using some of the principles
/// might add turbulent being authored behind vehicles , or use a method https://www.youtube.com/watch?v=dfka4kRLEXI.   These are said to contain
/// 
/// 
/// 
/// </summary>
/// 


///https://en.wikipedia.org/wiki/Level_set_method  can be used for bubbles.   this is almost sph but we are using the existing tree and collsion to find the pression and on a particle
/// the multtreaded field collection will ask for each body if it is in each field .
/// 

//this is O (n) and no grid is needed..

//pressure moves this apart, we can use collisoin for that
//Viscoisty damps it.. we have that.. we query the ambient ( not including our own contribution to the field).. for damping.  

//damping on the small or big particle..https://en.wikipedia.org/wiki/Level_set_methodhttps:


//density gradient descent

//short rangle repulsion, long range attraction.. not sure..

//https://en.wikipedia.org/wiki/Level_set_method

//
//https://en.wikipedia.org/wiki/Level_set_method

//    http://farsthary.com/2010/03/19/introducing-particle-fluids-parametters-function/

//    http://www.geometry.caltech.edu/pubs/DC_EW96.pdf

//can study blendr for rocket smoke,etc..


//simply //rendering .. distance h from the Vector2s.. hwere H is the radius.. this is the rendering border.... but how to compute.. 2*Wh(h)

//if grid is used, when particle is intergrated, the new grid is known..

///since we have a tree based collide , we dont want this.. and we need to interact with environment..
/// 
//radius of influence should be 2R R isH or radius..

//pair wise there is an attraction.. but only if neighbors agree pressent..

//Real-Time Visual Effects for Game Programming for bubbles

//  https://en.wikipedia.org/wiki/Level_set_method

//idea, smaller particles on the periphery

//   Smoothed Particle Hydrodynamics: A Meshfree Particle Method   two sets.. one for stresses...one for velocities..
//http://www.dgp.toronto.edu/people/stam/reality/Research/pdf/GDC03.pdf.. the BEST for puffs smock...advection idea, grid based tho..

//could tinker in blender , then implement..  or tinkerin here ..   spc is not a big deal, navior stokes neither..


//using this method    MovingParticle
// Semi-Implicit(   i think



//using vortons next... see the BAM code...
#if charsther
        //NOTES.. this field DOE NOT CONSERVE MASS OR ENERGY.  ITS A ROCKET EXHAUST OR BOMB.  EQUATIONS ARE NOT REALLY NEEDED BUT A PHYSICAL FORM IS.
        //
        //
        //
        //∂u∂t+u · 
        //   ∇u = −1ρ∇p+ν∇2u+f,
        //
#endif
        BodyEmitter be;
	    bool unloadThis = false;

        public GasPressureField(World world , BodyEmitter be)    
        {
            World = world;
            //   Core.Game.Drawing.ShapeFactory.CreateCircleShape( )

            ++collisionCatBit;


            be.OnUnload += OnUnload;

            be.OnSpawnFieldParticle += OnSpawnFieldParticle;

            WindDrag.AddWindField(this);
        }


        void OnUnload()
        {
            unloadThis = true;  //do something like radius to zero..

        }

     

        CollisionFilter cf;


        public void OnSpawnFieldParticle(BodyEmitter be,  FieldParticle fp)
        {

            float gasDensity = 3;
            //   Core.Game.Drawing.ShapeFactory.CreateCircleShape( )

            fp.BubbleMotion = false;

            fp.CurrentSuperSize  = MathUtils.GetRandomValueDeviationFactor(be.SuperSize, be.SuperSizeDeviation);

            ///TODO density will affect motion..maybe should change with temp...bouyancy if smoke looks ok at low speeds.. well let it rise..by normal law of bouyancy..  in water will bubble up..  rise faster..
            Fixture outerCircleFixture = FixtureFactory.CreateCircle(World, fp.CurrentSuperSize,   gasDensity);     //Note setting Density to 1 for now.. gets overriden by mass.

            CollisionFilter cf = new CollisionFilter(outerCircleFixture);

            Category cat = (Category)(  1 << collisionCatBit);

           // cf.CollidesWith = cat;

            //   cf.CollisionGroup = 0;  //positive means they never collide in old way.. safest just set it to zero


            cf.AddCollidesWithCategory(cat);
         


         //   outerCircleFixture.AddCollisionCategory(cf.CollidesWith);

         //   cf.CollisionGroup = BodyEmitter.DefaultParticleGroup;
            cf.CollisionGroup = 0;

            SimWorld.RecentInstance.AddEntityToCurrentLevel(fp);

            FieldParticles.Add(fp);




        }

        static short  collisionCatBit = 1;  //will use collision categories on the super shell.  only collides with members of its group.


        public void SetDefaultGasFieldEmitterProps()
        {

            //  ignoreGravity = false;
            //buoyancy?


        }

        public void Release()
        {
            WindDrag.RemoveWindField(this);

            if (be != null)
            {
                be.OnUnload -= OnUnload;
                be.OnSpawnFieldParticle -= OnSpawnFieldParticle;
            }
        
        }


    

#region IField Members
        public Vector2 GetVelocityField(Vector2 pos, out float density, out float temperature)
        {

       
            density = WindDragConstants.DefaultAirDensity;
            temperature = WindDragConstants.DefaultTemperature;

            if (unloadThis)
            {
                if (FieldParticles.Count() == 0)//TODO
                {
                    Release();
                }

            
                return Vector2.Zero;
            }


            //two ways .. add a new light source when there is enough heat & pressure... apply force only if lit ,not shadowed.

            //allow outer fixture to oscillate...like shock wave..  and collide with walls  using larger size..


            float sumImpulse = 0;


            //TODO change ths..   parts of a group collide with only each other.  the super part..   other  groups can pass through   ..  this is for coverage, and for efficiency.
            //EXPERIMENT WITH THIS..
            Body body;
            World.RecentInstance.QueryAABBFieldParticle(pos, 0, out sumImpulse,out body, cf);

         //   Debug.WriteLine("sum" + sumImpulse);


            temperature = sumImpulse / 20;






            //after solving, gives a good idea of the presure its under.  dont need to keep track of how many are touch , etc.
            //   AfterCollision += MainBody_AfterCollision;

            //   fp.TotalContactForce

            // if (i % 3) == 0;  //u[date density every frame.. or spending on vel..
            //  i++;

            //    BroadPhase.( )       //here only other field partcles with collide

            //for pressure .. how many other gass are touching.. and consider the totalimpulse

            //   density = Density;


            //  temperature = Temperature;

            //    if ((pos - this.WorldCenter).LengthSquared() > _fieldDistSq)
            //     return Vector2.Zero;

            //would have to move the winddrag out of game .. do something about rays..  

            //NOTES Or, make this  a spirit,  will be slow in tool tho, have to compile every one on emit.

            //allow a one body object to be made a spirit..  or just like two balls together spirt them out..
            //then field can be tuned in tool..

            //or just pass special eddies  out... this is a basic particle.   doest notthing but feel out the environement.. almost could acomplish this with rays.
            //this is a little better due to bunching up, ( pressure) ..  ( path finding out of curved spaces,  by pressure.. ( ball size dependent) ) and could rise or sink with temperature, or  other
            //also special code in our Farseer does not do CCD with particles so we can use alot of these balls.

            //TODO try add higher energy..

            //  Vector2 particleVel = LinearVelocity;
            // particleVel.Normalize();
            //    particleVel *= Speed;
            //   return particleVel;

            //  dist .. factor.. todo.. or else it fade too abruptly .. 
            //  Math.Max(0, (EngineThrustVelField *  - 0.5f * dist));

            //TODO consider use field above and below to detect shear and make vortecies  .. or just vortex on \
            // have to be done in Update

            //detect "curl"   ( accel in circle) possible on slope.. .. better just design using a vortex object.

            //need to sum up the rest.   then add a component
            //TODO cancel out componet tangential to hit?? probably not needed..
            // not needed to do bounce or pressure now.. will happen on its own i think,

            if (body == null)
                return Vector2.Zero; 

            return body.LinearVelocity;   //do vortex



        }

        /// <summary>
        /// if this gas is inside water, or null region.. it wont be  nulled out.. //TODO must be a better way..
        /// </summary>
        /// <returns></returns>
        public AABB GetSourceAABB()
        {

            AABB aabb = AABB.MinAABB();

            foreach( Particle particle in FieldParticles)
            {
                aabb.Combine(ref particle.AABB);
            }
            return aabb; 

        }

#endregion

#region IField Members


        public IEntity GetSourceEntity()
        {

            if (FieldParticles.Count == 0)
                return null;

             return FieldParticles[0];  //just to associate this with an entity,can be expired
        }


#endregion

    }
}
#endif