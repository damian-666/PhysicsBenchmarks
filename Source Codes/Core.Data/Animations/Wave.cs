using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics;
using FarseerPhysics.Dynamics;

namespace Core.Data.Animations
{


  //  enum WaveForm
   // {
    //    Sine,
    //    Sawtooth,
    //    Square
   // }


    //TODO need to be careful not to end waves mide phase should , end a 0, or 2pi , etc.. at zero.

    /// <summary>
    /// A wave function, can be used for anything, such as a wave pump to disturb the water, its a fuction of time as are all Effects.  Takes parameters as input, output is YValue taken from Update Event.  These can be grouped and superimposed.
    /// </summary>
     public class Wave : PeriodicEffect
    {     
        //note waves Y values are in graphics coordinates , down is numeric value going up.
         
        public double YValue;  //the output of this wave generator.  it procedes with each update.
        double OffsetY = 0;
        double Magnitude; //magnitude of the wave           
        public double Phase;   // phase shift in meters..  .. otherwise starts at zero

  
        //TODO inner gust maybe..  look at blowing sand..
        public Wave(Spirit sp, string name, double duration,  double offsetY, double magnitude, double frequency )
            : base(sp, name, duration, 1 / frequency)
        {
            Magnitude = magnitude;
            OffsetY = offsetY;
          
        }

        public override void Update(double dt)
        {
            base.Update(dt);
            //General thoughts on wave riding and generation..
            // in ocean.cs  there is a simple numeric wave propagator.
            //TODO the ultimate goal is to surf an unbroken wave ala Laird Hamiton or a lonngboarder on a swell.
            //     better is to surf a doubleup.   meaning surfing waves going both left or right .   the ocean wave filed

            //this requires the field in on the wave front to move both toward the surfer ( more lift) .. updward relative to the ground, and foward allong 
            // the propagatio ndirection ( x)..  to do that on the generated waves would require 
            //1.    determination of the phase velocity of bigger waves while ignoring smaller. 
            //( NOTE  this is not an important or significant as upward movement)..  updated wovement will drive craft forward, applying drag to avoid the trough should surf the wave.
            // 2..    reflection with free end for waves off tank.. we now have only fixed end reflection.. this inverts the magniture.   

            //http://archive.osc.edu/education/si/projects/wave_motion/index.html   check this for boundary condition to change the model  ( NOTE this wont help if we are using the disturbe spring model) 
            //   http://dice.se/wp-content/uploads/water-interaction-ottosson_bjorn.pdf
            //a)   possible solution.. assume that all waves that are rideable are not reflections.   
            //    b) for the problem of sufing off a wave bouncing off a stepp cliff ( so surfer can pick up speed) Damp out the wave after boundary , then generate a reflection coming from th eother side. 
            // . ( there are other ways to take off .. powered SUP)   ..
            ///powered barge with board on it.    //howerer surfing double ups is so compellling..   

            //what would waves on other planets be like?


            //   https://www.youtube.com/watch?v=4QrH2Tw-emQ

           // https://www.youtube.com/watch?v=gQ7KGhuE1e8

            ///the rope model would give it freely but might be expensive if updating tree....  if using a sorf of partial collision reponse..  however this does not mix with the field model we are using for swim.. ex..
            ///  posslibe mix the rope to the free end of the wave .. anyways water bouncing off a shore is best done with sph , then a big disturbance.

            //3.  possible an equation to describe the circular motions below the surface.   Bascom expermiemtns.   this is not as important as decribing the behavior at the surface.
            //5.  if wave equations are known then the other motions can be added to the field..  
            //then the noise and disturbances added as  a separate buffer
            //SPH is the unlimate solution but can be unstable , expensive to compute and does not leverage any exsiting work with fields and edges.            
            //problems with sph.. determinatino of surface , rendering, bubbles, air trapped areas difficult 
            //also dont know the propagation direction 
            //Putting an SPH later right on top of the water might do a nice effect.

            //       bad for oceans, O (N) perfromance on body of water nice.  / cores..  needs GPU programming to be good.
            //TODO sawtooth, wavy thing.. enum  .. or 
            //TODO dont need to do use a physical  rope for this.. just make wave equation for wave boucing with free end, will reverse direction without inverting is all.
            //TODO consider to put noise in a separate buffer using 
            //TODO when signal to noise goes down.. just randomize the field under water..
            //TODO regardless .. need this wave generator..
            // TODO interesting wave trains.. read one of the nature articles to know the celerity tho. http://en.wikipedia.org/wiki/Group_velocity
            // wave speed is inversely related to wave length in ocean.

            YValue = OffsetY + Magnitude * Math.Sin(Phase + 2 * Settings.Pi * ElapsedTime / Period);

            //OR.. for now we just superimpose 2 sin waves to steepen the wave..
          ///  http://en.wikipedia.org/wiki/Stokes_wave     


        }
    }
}
