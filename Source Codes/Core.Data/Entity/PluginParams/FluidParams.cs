using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

//params that can be access by other plugins that refer to aux spirits

namespace Core.Data.Entity.PluginParams
{

    [DataContract(Name = "FluidParams", Namespace = "http://ShadowPlay")]
    public class FluidParams
    {
        /// <summary>
        /// the simulation grid cells  to calculate the unit fluid simulation Eulerian grid for the velocity field.  Future to support non square Nu Nv;
        /// </summary>
        [DataMember]
        public int N { get; set; }//todo set defaults here upgrade script compiler to c# 6

        /// <summary>
        /// timeslice of the 1 meter gas unit box simulation .  Best tune so that fastest fluid goes no more than 4 grid pts (Bridson lecture)
        /// So if our physics Dt is 0.016, and this is 0.16,    1 frame of game time in unit box is  10 frames in game, but we keep it at 1 update to 1 update and scale velocities in box to get world cordinates
        /// </summary>
        [DataMember]
        public float dT { get; set; }

        [DataMember]
        public bool doBouyancy { get; set; }

        [DataMember]
        public bool doDiffusion { get; set; }

        /// <summary>
        /// Solver interations, default is 10
        /// </summary>
        [DataMember]
        public int Iterations { get; set; }

        /// <summary>
        /// tunable to adjust formula for hot gas rising, default 0.000625f
        /// </summary> 
        [DataMember]
        public float BuoyancyA { get; set; }

        /// <summary>
        /// tunable to adjust formula for hot gas rising, default 0.025f;
        /// </summary> 
        [DataMember]
        public float BuoyancyB { get; set; }

        [DataMember]
        public float Viscosity { get; set; }


        //if we know the fluid is fully contained by the parent, like balloon, and dont need to cast rays or anythiing to try figure this out with thing objects and large outer grid or no grid
        //and ray blocking
        //bhy default if true we cast one ray to check to add bk wind to the whole fluid and its might need be one ray for each grid element to be accurate..
        [DataMember]
        public bool AddBackGroundWind { get; set; }




        /// <summary>
        /// not tested
        /// </summary>
        [DataMember]
        public float DiffusionFactor { get; set; }

        public FluidParams() //default values
        {
            SetDefaults();
        }

        void SetDefaults()
        {
            N = 60;
            dT = 0.2f;
            doDiffusion = true;
            doBouyancy = true;
            BuoyancyA = 0.000625f;
            BuoyancyB = 0.025f;

            Iterations = 10;
            DiffusionFactor = 0.0f;
            Viscosity = 0.0f;
            AddBackGroundWind = true;
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext c)
        {

            if (BuoyancyA + BuoyancyB == 0)
            { SetDefaults(); }

        }

    }
}
