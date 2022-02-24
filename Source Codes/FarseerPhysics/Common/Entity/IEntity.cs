using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Collision;
using Core.Data.Collections;

namespace FarseerPhysics.Common
{
    /// <summary>
    /// An abstract of Physical Entity
    /// </summary>
    public interface IEntity //TODO  rename Entity folder TO Interfaces.
    {


        /// <summary>
        ///this is the position of the body reference framespace in the world. 
        ///it might not be a point on the body and does not usually mean the center .
        /// used for copy /paste , editing.   moving vertices or inner body parts should not affect this value
        /// </summary>
        Vector2 Position { get; set; }

        /// <summary>
        /// The transform from body to world given current pos and rotation
        /// </summary>
        Transform Transform { get; }
        /// <summary>
        ///this is the center of the object or system center of mass,  in world coodinates
        /// </summary>
        Vector2 WorldCenter { get; }
        float Rotation { get; set; }
        AABB EntityAABB { get; }
        void UpdateAABB();

        /// <summary>
        /// Synchronous part of update , called from  preupdate physics loop on one tread
        /// </summary>
        /// <param name="dt"> timeslice in sec</param>
        void Update(double dt);
        /// <summary>
        /// Update called from Parallel for loop over all the entitie, update code in here must be thread safe, no different static or globals shared
        /// </summary>
        /// <param name="dt"> timeslice in sec</param>
        void UpdateThreadSafe(double dt);// called from parallel for loop

        bool WasSpawned { get; }
        int ID { get; } //ID number given at birth and persistent.  used to remove dublicates for traveling between levels .. full ID number consists of main body ID + level id .

        /// <summary>
        /// world vel in WCS in meters /sec
        /// </summary>
        Vector2 LinearVelocity { get; }

        /// <summary>
        /// Angular vel of Main body of spirit, or body closest to it (  or body CM mast..
        /// </summary>
        float AngularVelocity { get;  }

        ViewModel ViewModel { get; }

        /// <summary>
        /// called from the UI thread for immediate mode drawning to XNA device MONOGAME
        /// </summary>
        /// <param name="dt">Elapsed time in seconds since last update</param>
        void Draw(double dt);


        /// <summary>
        /// Children entities
        /// </summary>
        IEnumerable<IEntity> Entities { get; }

        /// <summary>
        /// a low res cache image of the entity can be used on zoomed out views in its place
        /// </summary>
        byte[] Thumbnail { get; }


        string Name { get; }
        string PluginName { get; }

    }



    /// <summary>
    /// Gets a Vector from position.. for wind veloclity field, or force field.
    /// </summary>
    public interface IField //TODO  rename this and folder TO intefaces?
    {
        Vector2 GetVelocityField(Vector2 pos, out float density, out float temperature);  //TODO should we add GetNormal.. normal is expensive to calculate.  (TODO use ref on input like farseer does, is it faster?)
        AABB GetSourceAABB();
        IEntity GetSourceEntity();

        bool IsEulerian();

        //TODO  for water of highly viscous stokesDrag equation is Force drag = - B * v  // 
        //    public void  GetFluidDensity(Vector2 pos, out  out float airDrag,  out float  stokesDrag)
        //    {
        //   Force drag = 1/2 density v*v * C ( * A ( our edge length) for 2d.
        //  return 1.5f; //using this value for air  todo ( make it a realistic 2d value?, for now its a  specific gravity (SG) or relative density ) ..   our C is 1  for now.. if below water line do a differnet one..
        //    stokesDrag = x.. airDrag= 0 
        //    }

    }

    public interface ILiquidRegion
    {
        //the region by the exhibitor of this interface must have a boundary defined as a true function  f( x) .
        //means no breaking waves, or perfectly vertical walls. 

        //This is useful for wave propagation using this method
        /// If you want the details behind the wave algorithm see the following:
        /// http://freespace.virgin.net/hugo.elias/graphics/x_water.htm
        /// http://www.gamedev.net/reference/articles/article915.asp

        //the x interval ( perpendicular to gravity) is subdivided into steps, which can be fixed or resampled on zoom in..
        //future...

        //the adaptation comes from Jef Webbers WaterSampleSilverligth 2.1 but uses the getsubmergedArea rather than breaking
        //the bodies into a grid.


        //because we might do particles and bubbles rising up..  or using "IsNotCollidabe" means not in collision tree.. decided not using a query aabb and a separate controller to see whats in the water.
        //as in Buoyancy controller in farseer controllers folder.
        //noncollideable objects like particles are not in this tree  , 

        //so we use a brute force parallel algorithm to determine whats in the ocean.  we need to traverse all these anyways in the windcontroller, vertex by vertex
        //so its a O(n) / num processors regardless.

        //if there are no IFluids in the level should not impact performance.
        //implementors should use AABB first to quickly cull out stuff.

        /// <summary>
        /// Applies the buoyancy force for this body using the submerged area
        /// </summary>
        /// <param name="body"></param>  
        /// <param name="inAirContainer">true  if the body is inside a hull of a boat or in air container</param>
        void ApplyBouyancyForces(Body body, out bool  inAirContainer);
        //   float GetDensity();  //quick way to get density of this body of liquid.  todo cosider separate method for get density, temp.. we rarely need all
        bool AABBIntersect(AABB aabb);  // used for trivial elimination of verts on main body of water to organise water sources.  
        //or optimise in in liquid.

        /// <summary>
        /// if  liquid and wave contains point, quick to determine, since wave is like a function of x
        /// </summary>
        /// <param name="vector">point</param>
        /// <returns>true if contains</returns>
        bool WaterContains( Vector2 vector);
        bool WaterContains(Vector2 vector, out float depth );

    }
}
