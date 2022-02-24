/*
* Farseer Physics Engine based on Box2D.XNA port:
* Copyright (c) 2010 Ian Qvist
* 
* Box2D.XNA port of Box2D:
* Copyright (c) 2009 Brandon Furtwangler, Nathan Furtwangler
*
* Original source Box2D:
* Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using FarseerPhysics.Dynamics;

namespace FarseerPhysics
{
    public static class Settings
    {
        public const float MaxFloat = 3.402823466e+38f;
        public const float Epsilon = 1.192092896e-07f;
        public const float Pi = 3.14159265359f;

        // Common

        /// <summary>
        /// Enabling diagnistics causes the engine to gather timing information.
        /// You can see how much time it took to solve the contacts, solve CCD
        /// and update the controllers.
        /// NOTE: If you are using a debug view that shows performance counters,
        /// you might want to enable this.
        /// </summary>
        public static bool EnableDiagnostics = true;

        /// <summary>
        /// The number of velocity iterations used in the solver.
        /// </summary>
        public static int VelocityIterations = 8;

        /// <summary>
        /// The number of position iterations used in the solver.
        /// </summary>
        public static int PositionIterations = 3;

        /// <summary>
        /// Enable/Disable Continuous Collision Detection (CCD)
        /// </summary>
        public static bool ContinuousPhysics = true;

        /// <summary>
        /// The number of velocity iterations in the TOI solver
        /// </summary>
        public static int TOIVelocityIterations = 8;

        /// <summary>
        /// The number of position iterations in the TOI solver
        /// </summary>
        public static int TOIPositionIterations = 20;

        /// <summary>
        /// Enable/Disable warmstarting
        /// </summary>
        public static bool EnableWarmstarting = true;

        /// <summary>
        /// Enable/Disable sleeping
        /// </summary>
        public static bool AllowSleep = true;



        #region      Shadowplay Mods:
        /// <summary>
        /// The maximum number of vertices on a convex polygon.
        /// </summary>
        public static int MaxPolygonVertices = 18; // was 8; then 18..TODO.. test the effect of putting it lower again to avoid CCD bog.. or better use parallel loops, parallelism to speed up TOI (x during Y intervals maybe) or the engine in general .. tristrips primitives are not used, so stuff can get worked into triangles, tessellation increases can show more bugs.  to allow round overlapping areas on joints without tessellating TODO revisit creates a big hit on CCD and near phase for the benefit only of broad phase (less moving items in tree, tests were done with one creature, just walking with no danger of collisions).. might be better to go back to 8. also use edge for terrain.  i think its cheaper with  less triangles for broad phase but not narrow.  but this increase fixes arrays sizes. tried 1600 with no performance issue seen.   NOTE.. having more that 8 can really slow CCD.. and near phase



        //Shadowplay mods
        public static int ExtraJointVelocityIterations = 0; //  this prevents feet sliding apart  , a second pass just for joints in a joint system
        public static int ExtraContactVelocityIterations = 0; //    to prevent self collide.. gives contact an additional pass after joints.   Not tried or used.. 
        public static int ExtraContactPositionIterations = 0; //    to prevent self collide.. gives contact an additional pass after joints
        public static bool IsContactVelSolveAfter = false; //    to prevent self collide..  TODO try this..  erase if not helping ragdolls not get stuck.. on self collide on 
        public static bool IsContactPosSolveAfter = false; //    to prevent self collide..  TODO try this..  solve it after the joints..make contacts have priority



        #endregion
        /// <summary>
        /// Farseer Physics Engine has a different way of filtering fixtures than Box2d.
        /// We have both FPE and Box2D filtering in the engine. If you are upgrading
        /// from earlier versions of FPE, set this to true.
        /// </summary>
        public static bool UseFPECollisionCategories = false;

        /// <summary>
        /// Conserve memory makes sure that objects are used by reference instead of cloned.
        /// When you give a vertices collection to a PolygonShape, it will by default copy the vertices
        /// instead of using the original reference. This is to ensure that objects modified outside the engine
        /// does not affect the engine itself, however, this uses extra memory. This behavior
        /// can be turned off by setting ConserveMemory to true.
        /// </summary>
        public const bool ConserveMemory = false;// TODO shadowplay mod  changed to true  initial test shows its ok.

        /// <summary>
        /// The maximum number of contact points between two convex shapes.
        /// </summary>
        public const int MaxManifoldPoints = 2;

        /// <summary>
        /// This is used to fatten AABBs in the dynamic tree. This allows proxies
        /// to move by a small amount without triggering a tree adjustment.
        /// This is in meters.
        /// </summary>
        public const float AABBExtension = 0.1f;

        /// <summary>
        /// This is used to fatten AABBs in the dynamic tree. This is used to predict
        /// the future position based on the current displacement.
        /// This is a dimensionless multiplier.
        /// </summary>
        public const float AABBMultiplier = 2.0f;

        /// <summary>
        /// A small length used as a collision and constraint tolerance. Usually it is
        /// chosen to be numerically significant, but visually insignificant.
        /// </summary>
        public const float LinearSlop = 0.005f;  //Shadowplay NOTE tried making this 0.002.. things seems touching more.. but CCD doesnt work as well ( right down dress and lineweights are needed to make things appear to touch


        /// <summary>
        /// A small angle used as a collision and constraint tolerance. Usually it is
        /// chosen to be numerically significant, but visually insignificant.
        /// </summary>
        public const float AngularSlop = (2.0f / 180.0f * Pi);   //cant be make this smaller  ( right down dress and lineweights are needed to make things appear to touch

        /// <summary>
        /// The radius of the polygon/edge shape skin. This should not be modified. Making
        /// this smaller means polygons will have an insufficient buffer for continuous collision.
        /// Making it larger may create artifacts for vertex collision.
        /// </summary>
        public const float PolygonRadius = (2.0f * LinearSlop);

        // Dynamics

        /// <summary>
        /// Maximum number of contacts to be handled to solve a TOI impact.
        /// </summary>
        public const int MaxTOIContacts = 32;

        /// <summary>
        /// A velocity threshold for elastic collisions. Any collision with a relative linear
        /// velocity below this threshold will be treated as inelastic.
        /// </summary>
        public const float VelocityThreshold = 1.0f;

        /// <summary>
        /// The maximum linear position correction used when solving constraints. This helps to
        /// prevent overshoot.
        /// </summary>
        public const float MaxLinearCorrection = 0.2f;

        /// <summary>
        /// The maximum angular position correction used when solving constraints. This helps to
        /// prevent overshoot.
        /// </summary>
        public const float MaxAngularCorrection = (8.0f / 180.0f * Pi);

        /// <summary>
        /// This scale factor controls how fast overlap is resolved. Ideally this would be 1 so
        /// that overlap is removed in one time step. However using values close to 1 often lead
        /// to overshoot.
        /// </summary>
        public const float ContactBaumgarte = 0.2f;

        // Sleep

        /// <summary>
        /// The time that a body must be still before it will go to sleep.
        /// </summary>
        public const float TimeToSleep = 0.5f;

        /// <summary>
        /// A body cannot sleep if its linear velocity is above this tolerance.
        /// </summary>
       
        #region      Shadowplay Mods:
     //   public const float LinearSleepTolerance = 0.01f;
        public const float LinearSleepTolerance = 0.19f;  //tuned that a severed leg can sleep .  tried 0.05  sometimes could go lower..
        #endregion

        /// <summary>
        /// A body cannot sleep if its angular velocity is above this tolerance.
        /// </summary>
        public const float AngularSleepTolerance = (2.0f / 180.0f * Pi);

        /// <summary>
        /// The maximum linear velocity of a body. This limit is very large and is used
        /// to prevent numerical problems. You shouldn't need to adjust this.
        /// </summary>
        
        
        public const float MaxTranslation =

        #region      Shadowplay Mods:
        // 2f;// old value  .dont know what units these are but after 120 meters second, its maximum velocity
         2000.0f;   //this fixes issue with rocket flight .. probably can be set higher.
        #endregion


        public const float MaxTranslationSquared = (MaxTranslation * MaxTranslation);

        /// <summary>
        /// The maximum angular velocity of a body. This limit is very large and is used
        /// to prevent numerical problems. You shouldn't need to adjust this.
        /// </summary>
        public const float MaxRotation = (0.5f * Pi);

        public const float MaxRotationSquared = (MaxRotation * MaxRotation);

        /// <summary>
        /// Maximum number of sub-steps per contact in continuous physics simulation.
        /// </summary>
        public const int MaxSubSteps = 8;


        //VISUALSLOP
        /// <summary>
        /// This is half of the amount of space seen between stacked item..  was observed, same of all bodies, might be related to LinearSlop
        /// used to put strong of pull in clip so that things appear touching.
        /// </summary>
        public const float HalfContactSpacing = 0.015f;   

        /// <summary>
        /// Friction mixing law. Feel free to customize this.
        /// </summary>
        /// <param name="friction1">The friction1.</param>
        /// <param name="friction2">The friction2.</param>
        /// <returns>the combined friction </returns>
        public static float MixFriction(float friction1, float friction2)
        {

            #region Shadowplay Mods:
            //   return (float) Math.Sqrt(friction1 * friction2);//farseer original
            return (float)friction1 * friction2;
            #endregion
        }


        #region Shadowplay Mods:
  //TODO .. pass the world collision point.. figure out relative velocity 

      /// <summary>
        /// MixFrictionSliding  , take into account relatvie velocity.. TODO pass in contact point... find relative vel or impulse..
      /// </summary>
      /// <param name="fixtureA"></param>
      /// <param name="fixtureB"></param>
      /// <returns></returns>
        public static float MixFrictionSliding( Fixture fixtureA, Fixture fixtureB)
        {
            return (float)fixtureA.Body.GetSlidingFriction(fixtureB.Body) * (float)fixtureB.Body.GetSlidingFriction(fixtureA.Body);     
        }
         #endregion


        /// <summary>
        /// Restitution mixing law. Feel free to customize this.
        /// </summary>
        /// <param name="restitution1">The restitution1.</param>
        /// <param name="restitution2">The restitution2.</param>
        /// <returns></returns>
        public static float MixRestitution(float restitution1, float restitution2)
        {
            return restitution1 > restitution2 ? restitution1 : restitution2;
        }


        #region Shadowplay Mods:

        /// <summary>
        /// Determine if breakable Joint is allowed to break. Default is TRUE.
        /// If False, should prevent all breakable joint from break. All joint 
        /// break code should check for this settings.
        /// </summary>
        public static bool IsJointBreakable = true;

        #endregion
    }
}