//Shadowplay Studios , added  by Damian Hallbauer
//all rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;


namespace FarseerPhysics.Dynamics
{

    //TODO the ground body  should have  one of this effect..

    /// <summary>
    /// This class contains the design data paramters that affect what sounds a body has
    /// N bodies can share the same data, for piles of stuff.  Note .. using copy to clipboard via serialization and paste of the body referenc  makes a copy ( deep clone) and will not resuse this object
    /// </summary>


 //   [DataContract(Name = "BodySoundEffectParams", Namespace = "http://ShadowPlay", IsReference = true)]  

    //NOTE <SoundEffect"http://schemas.datacontract.org/2004/07/FarseerPhysics.Dynamics">  this is what the name of class ref appears  without Namespace =
     [DataContract(IsReference = true)]  //should be above, name is cleaner and smaller, but then old files break.  IsReference is so that one param can be shared after save and reload, as in segments of a chain.   
    public  class BodySoundEffectParams
    {

       [DataMember]
       public string StreamName { get; set; }

       [DataMember]
       public float Volume { get; set; }
       
       [DataMember]
       public float DopplerFactor  { get; set; }
       
         /// <summary>
         /// distance at which it fads to zero, should be around 1/ Limit
         /// </summary>
       [DataMember]
       public float VolumeDistanceFactor  { get; set; }
       
       [DataMember]
       public float  VolumeDistanceLimit  { get; set; }    //dont play if beyond this dist from main ear location
       
       [DataMember]
       public float ImpulseTangentialLimit  { get; set; }
       
       

      //raise the pitch if noise is due to sliding on frictino object.
       [DataMember]
       public float ImpulseTangentialPitchFactor { get; set; }
       

		//raise the pitch if noise is due to sliding faster on bumpy shape (impulse will still be  normal)
       [DataMember]
       public float SpeedTangentialPitchFactor { get; set; }

       [DataMember]
       public float SpeedTangentialForMaxPitch { get; set; }//impulse required for max pitch.    soft scrapes might rather use a relative speed + normal factor

       
        /// <summary>
        /// Minimum Normal Impuse to make the sound 
        /// </summary>
       [DataMember]
       public float ImpulseNormalLimit { get; set; }
       
     //  [DataMember]
      //public float ImpulseNormalFactor { get; set; }//slope as impulse is reduced, will default to 1.  not used, just use ImpulseNormalForMax , its linear to zero

       [DataMember]
       public float ImpulseNormalForMax { get; set; }//impulse required for max sound.    positve difference will reduce shound by impulse value * impulse normal factor


 
        /// <summary>
       ///  Maximum Tangential Impulse to make the sound 
        /// </summary>
       [DataMember]
       public float ImpulseTangentialMax { get; set; }

       /// <summary>
       ///  Mininum Tangential Impulse to make the sound 
       /// </summary>
       [DataMember]
       public float ImpulseTangentialMin { get; set; }

       [DataMember]
       public float ImpulseTangentialFactor { get; set; }

        /// <summary>
        /// Raise or lower the Pitch at runtime  +1 faster..  -1 slower.  
       ///  Pitch adjustment, ranging from -1.0f (down one octave) to 1.0f (up one octave).
        /// 0.0f is unity (normal) pitch.
        /// </summary>
       [DataMember]
       public double PitchShiftFactor { get; set; }

       [DataMember]
       public float AngularVelFactor { get; set; }

       [DataMember]
       public float AngularVelLimit { get; set; }

       [DataMember]
       public float LinearVelFactor { get; set; }


       /// <summary>
       ///Minimum relative velocity between Listener and Body, for continuous sounds 
       /// </summary>
      [DataMember]
      public float MinVelocityBody { get; set; }


       /// <summary>
       ///Minimum relative velocity between bodies in Normal Direction .  Note this is not averaged.  Objects under gravity on groudn will show a value.
       /// </summary>
      [DataMember]
      public float RelVelocityNormalMin { get; set; }

       /// <summary>
       /// Minimum relavite  velocity between rubbing bodies in Tangential  Direction 
       /// </summary>
       [DataMember]
       public float RelVelocityTangentialMin { get; set; }

        /// <summary>
        /// means 1/XFactor meters to pan all the way, using x component distance only.   so 0.1 means 10 metess will be a full pan
        /// </summary>
       [DataMember]
       public float PanXFactor { get; set; }

        /// <summary>
       /// will adjust pitch by sin of this body angle * BodyAnglePitchFactor.. From 0 to 1
        /// </summary>
       [DataMember]
       public float BodyAnglePitchFactor { get; set; }

        /// <summary>
        /// random pitch change by fraction..
        /// </summary>
       [DataMember]
       public float PitchFactorDeviation { get; set; }

         /// <summary>
         /// if this is true, if a collision on a single body has a change due to speed , angle , rotion ,  pan due to ear positionetc.. a new soundEffectinstance
         /// will not be played simulateously for that body.
         /// usefull for things like rain drop, hail, etc to make chain sound not overfull , and about over loading sound effect limits.
         /// </summary>
       [DataMember]
       public bool OneEffectPerBody { get; set; }
  
       [DataMember]
       public bool ContinuousPlay  { get; set; }



       public BodySoundEffectParams()
       {
           //set defaults
           VolumeDistanceFactor = 1 / 20f;  // 50 meters fades out to zero
           VolumeDistanceLimit = 40; // dont even play sound if so far.
           PanXFactor = 1 / 10f; //means 10 meters to pan all the way.. 
           ImpulseTangentialMax = float.MaxValue;    
           ImpulseTangentialMin = float.MinValue;                
           RelVelocityNormalMin = float.MinValue;
           RelVelocityTangentialMin = float.MinValue;
           Volume = 0.6f;            
       }
    }




}
