using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Common;
using Farseer.Xna.Framework;
using System.Diagnostics;


namespace Core.Data.Animations
{

    /// <summary>
    /// causes the dress to change for heart beat.. might also apply moving gradient fill or  simulated blood pressure.
    /// </summary>
    public class OrgansAction : LowFrequencyEffect
    {
        //might refactor this as Beat cycle.. but using floating point not discreate like BeatEffect
        const double _squeezeFraction = 0.22f;  // the percent of the cycle to squeeze the heart.       
        double _expandedHeartPhase;

        public double SpeedFactor = 1;

        public bool IsHeartSqueezed = false;


        public bool AnimateBloodPlasmaBrush { get; set; }

        /// <summary>
        /// number of frames to skip.. if 0 its every cycle, if 1 its every 2nd cyccle  , if 2 its  skip 2  , etc..
        /// </summary>
        public int SkipGradientFillFrames { get; set; }



        public float  JointSoftness { get; set; }



    //TODO gradient fill stuff.. based on same frequency.. 
    // move a radial bruss around all body parts
    //TODO line up feet toops ( careuflly might affect gait.. )  and right side of body.
    //TODO less triange in head.
    //TODO head beat based on zoom..       
    //TODO hear others hearts??

    //NOTEdont spend much time on  TORSO .. will probably become spine soon.

    /// <summary>
    /// Will change dress for beating heart , in main body .
    /// Used the rate as which energy is being burned .. smoothed perhaps to figure it out.
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="name"></param>
    /// <param name="baseFrequency"></param>
    public OrgansAction(Spirit sp, string name, float baseFrequency)
            : base(sp, name, double.PositiveInfinity, 1 / baseFrequency)
        {
            _expandedHeartPhase = Period * (1 - _squeezeFraction);

            AnimateBloodPlasmaBrush = true;

            SkipGradientFillFrames = 2;  //every 3rd frame.. slow it down..

        }


        public override void Update(double dt)
        {
            base.Update(dt);

            //      double adjustHeartPeriod = Period * (2.0 / (Parent.AveragePowerConsumption));/nned to be smoothed out more
            double adjustHeartPeriod = Period;

            //    if (Parent.AveragePowerConsumption > 1)
            //    {
            //        adjustHeartPeriod /= 2;
            //     }

            //commented out since dress of eyes isnt complete.
            //there are dress also on eyes.. could straighten those.
            //its too subtle an effect.. TODO revisit future.
            //    if ( MathUtils.IsOneIn( 100))//random blicks.. todo blick on partlce hit? or sharps near..
            //     {       
            //         Parent.BlinkEyes();
            //     }

            if (!Parent.IsUnconscious)  //TODO show slow heart on unconscious?
            {
                adjustHeartPeriod /= SpeedFactor;
            }
            else
            {
                Parent.CloseEyesDress();
            }

           // phaseChange = IsHeartSqueezed;


            IsHeartSqueezed = ((ElapsedTime % adjustHeartPeriod) > _expandedHeartPhase);
            Parent.MainBody.IsShowingDress2 = IsHeartSqueezed;


            SkipGradientFillFrames = SpeedFactor == 2 ? 1 : 2;
         //   if ( IsHeartSqueezed != phaseChange)
         //   {
        //        phaseChange = true;
        //    }

            // animate gradient brush
            //TODO use this for pulse to move  or factor * factor.. make sharper pulse?
            //   double factor =  Math.Cos(2d * Math.PI * ElapsedTime / Period);
            //  Debug.WriteLine(factor);    //sync with heart


            if (AnimateBloodPlasmaBrush 
                &&
            //    phaseChange
               FrameCount  % (SkipGradientFillFrames + 1) == 1
            )
             { 
               AnimateSpiritGradientBrush();  //snk blood pump with plasma..
            }


            if (IsHeartSqueezed && OnCycleEvent != null)
            {
                OnCycleEvent(this);
            }

            if (Parent.IsDead)
                Finish();
        }


        private void AnimateSpiritGradientBrush()
        {
            foreach (Body b in Parent.Bodies)
            {
                // should only be used for Leg,  | Arm bits.  torso , and neck,  
                // the rest  cant match the dress closly.. ( feet needc to be bigger and hands than dress) .  head is filled..
                if ((b.PartType & PartType.Leg) != 0 ||
                    (b.PartType & PartType.Arm) != 0 ||
                    (b.PartType & PartType.MainBody) != 0 ||
                    (b.PartType & PartType.Neck) != 0)
                {
                    // TODO: be nice to have little Effect that moves the gradient offset back and forth. 
                    // like a pulse every x frames ( with Heartbeat rythm)... but how to speficy the axis i dont know.. mabye between first 2 joint pos

                    AnimateBodyGradientBrush(b);
                }
            }
        }


        double _1topRedStart = 0.35d;     // top red band, always decrement.
        double _1topRedEnd = 0.132065;    // min value of top red band when decrement.

        // 2 top transparent band , located about at lung level, static not moving.
        // to widen gap, increase distance between these 2, and also modify red band 1 & 4 start accordingly.
        double _2upperClear = 0.37d;      
        double _3upperClear = 0.42d;        

        double _4upperRedStart = 0.44d;     // upper middle red band, always increment
        double _4upperRedEnd = 0.6;         // max value of upper middle red band when increment.

        double _5lowerRedStart = 0.66d;     // lower middle red band, always decrement
        double _5lowerRedEnd = 0.6;         // min value of lower middle red band when decrement.

        double _6lowerAbdomenStart = 0.68d;     // abd band 1, increment
        double _6lowerAbdomenEnd = 0.80;
        double _7lowerAbdomenStart = 0.68d;     // abd band 2, increment
        double _7lowerAbdomenEnd = 0.84;

        double _8bottomRedStart = 0.8d;     // bottom red band, always increment
        double _8bottomRedEnd = 1d;


        private void AnimateBodyGradientBrush(Body body)
        {

            //NOTE PERFORMANCE .. FOR PHONE THIS IS VERY EXPENSIVE..  CANNOT USE GPU.. 

            //TODO measure impact and at least skip every other frame..
            if (body.GradientBrush == null)
            {
                //For main body must have the 4 stops so lungs remain clear, no blood
                if ((body.PartType & PartType.MainBody) != 0)
                {
                    Create3BandBloodPlasmaBodyLinearGradientBrush(body);
                }
                else  //this could be change to linear 2 stop.. but should have a lag.. blood pulse moves in "wave"
                {
                    CreateBloodPlasmaBodyRadialGradientBrush(body);
                    //   Create3BandBodyLinearGradientBrush(body);  // for now its ok because it skips transparent band if not main body (TODO make this designed )
                }
            }
            else
            {
                body.GradientBrush.GradientStops[0].Color = (Parent.GlowColor != null) ? Parent.GlowColor : _red;

                //FUTURE  DWI- it possible to use > 2  stops to keep the lungs tranparent always?
                //if we have mulple views we can maybe arrange  organ so that lungs arways transparent..  so 2 stops is enough.
                //    if (body.PartType == PartType.MainBody)  // for thorax to have clear lungs .. later.
                //    {
                //        body.GradientBrush.GradientStops[1].Color = _transparent;   //this does work.   but leaves stomach transparent.. looks weird
                //    }          

                //Note this is a cyclic way like chew uses ..  but too smooth..
                //float target = (float)(_origAngle + _magnitude * Math.Sin(2* Settings.Pi * ElapsedTime / Period));            

                //Note this way does give a Squeeze look, so im leaving it..  we do want back and forth tho.. blood to heart , back , and then out again.
                //but in both bands ( apart from lungs, separately..


                double gradientSpeedFactor = 7f;     // normal is 1f, increment this to speed up mainbody gradient cycle
                float baseIncrement = (float)Math.Sin((ElapsedTime * gradientSpeedFactor) % Math.PI);   // base increment is between 0 to 1;


                // only flow when heart squezed.. when   move blow back the other way..
                //       if (!IsHeartSqueezed)
                //      {
                //          increment = -(float)Math.Sin(ElapsedTime % Math.PI) * 0.03f;  //dh mave it flow backwards
                //      }

                if (Parent.GlowColor != null)
                {
                    //increment = 0.02f;
                }
                ///TODO  before (last build)  the speed whent quicker then slower , like a real pumping fluid.. seems to be too uniform now..

                if (body.PartType == PartType.MainBody)     // for main body
                {
                    body.GradientBrush.GradientStops[0].Offset = _1topRedStart - (baseIncrement * (_1topRedStart - _1topRedEnd));  // decrement top red

                    // upper clear is kept static here

                    body.GradientBrush.GradientStops[3].Offset = _4upperRedStart + (baseIncrement * (_4upperRedEnd - _4upperRedStart));    // increment upper red
                    body.GradientBrush.GradientStops[4].Offset = _5lowerRedStart - (baseIncrement * (_5lowerRedStart - _5lowerRedEnd));  // decrement lower red
                    body.GradientBrush.GradientStops[5].Offset = _6lowerAbdomenStart + (baseIncrement * (_6lowerAbdomenEnd - _6lowerAbdomenStart));    // increment abd 1
                    body.GradientBrush.GradientStops[6].Offset = _7lowerAbdomenStart + (baseIncrement * (_7lowerAbdomenEnd - _7lowerAbdomenStart));    // increment abd 2
                    body.GradientBrush.GradientStops[7].Offset = _8bottomRedStart + (baseIncrement * (_8bottomRedEnd - _8bottomRedStart));    // increment bottom red
                }

                else    // for limbs
                {
                    //TODO should it move just one of the stops.. kind of move the most Dense part around?   
                    foreach (BodyBrushGradientStop stop in body.GradientBrush.GradientStops)
                    {
                        //if (body.PartType == PartType.MainBody)  // for now the legs look fine , but the point of the 4 stops was to keep a separate band around heart
                        ////blood moving in opposit directions from heart..  Temporary 
                        //{
                        //    if (stop.Color.A == 0)// dont move the tranaprent bands.. around.. or maybe just a littel, but keep the lungs clear always
                        //        continue;
                        //}
                        //// TODO: be nice to have little Effect that moves the gradient offset back and forth. 
                        //// like a pulse every x frames ( with Heartbeat rythm)... but how to speficy the axis i dont know.. mabye between first 2 joint pos

                        stop.Offset += (baseIncrement / 10);
                        if (stop.Offset > 1f)    // when use linear gradient, must use 1.0f, or will look strange     // half meter .. average body part.
                        { 
                            stop.Offset = 0;
                        }

                        //     else   // just a guess.. have no idea the offsets , center or layout or what.  whould be nice to have an exact inkscape model to play with.
                        //    if (stop.Offset < -0.5f)    // when use linear gradient, must use 1.0f, or will look strange     // half meter .. average body part.
                        //       stop.Offset = 1;

                    }
                }

            }
        }


  

        private void Create3BandBloodPlasmaBodyLinearGradientBrush(Body body)
        {
            // NOTE: Valid range of StartPoint, EndPoint, and GradientStop.Offset is between 0.0 to 1.0 .  
            // Its based on Body Local Coordinates.


            //are the StartPoints in BodySpace? yes
            //Notes guess.. 0.5 is approx center in meters of body in Local Coordinates..
        

            BodyLinearGradientBrush brush = new BodyLinearGradientBrush();

            const  float x = 0.5f;//0.5 is the  midPoint.. doenst really mater since horizontal stops..
            brush.StartPoint = new Vector2(x, 0f);// * Parent.SizeFactor;   //TODO would this help.. all the static stops need scaling.. i think was done in the XAML editor
            brush.EndPoint = new Vector2(x, 1.0f);// * Parent.SizeFactor; 



          // if made this smaller, will result in more repeat, if use GradientSpreadMethod.Repeat

            //I think either repeat it once, or one brush to fill the whole thing..  prefer one brush the height of body, i still dont understand this..

            body.GradientBrush = brush;

            ////use current glow color or lighter rer for background body fluid plasma
            //BodyColor firstStopColor = (Parent.GlowColor != null) ? Parent.GlowColor : _red;

            //what do this points mean ?? can you make the transparent part  stay put?

            // i tried moving grips and offsetins in inkscape but cant figure it out..
            //id expect the middle bands to be transparent.. im confused.

            //NOTE .. SUSPECT THIS WAS DONE IN 

            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_red1, _1topRedStart));
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_transparentRed, _2upperClear));  //I still dont know who to winden or scale the lung band..
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_transparentRed, _3upperClear));
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_red1, _4upperRedStart));
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_red1, _5lowerRedStart));
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_mediumRed, _6lowerAbdomenStart));
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_mediumRed, _7lowerAbdomenStart));

            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_red1, _8bottomRedStart));
        }


        static BodyColor _red1 = new BodyColor(162, 0, 18, 255);
        static BodyColor _mediumRed = new BodyColor(155, 35, 41, 250);
        // transparent red give smoother shade of red,  it progresses towards the transparent .
        static BodyColor _transparentRed = new BodyColor(255, 0, 0, 0);


        static BodyColor _darkRed = new BodyColor(104, 71, 95, 255);
        static BodyColor _red = new BodyColor(200, 7, 10, 255);
        static BodyColor _transparent = new BodyColor(0, 0, 0, 0);


        private void CreateBloodPlasmaBodyRadialGradientBrush(Body body)
        {
            BodyRadialGradientBrush brush = new BodyRadialGradientBrush();
            brush.Center = new Vector2(0.5f, 0.4f);    //indeally ..should be near one bone end, pulse across limb
            
            // brush.Center = new Vector2(0f, 0f)// dint look good
            
            brush.GradientOrigin = new Vector2(0.5f, 0.4f);
            body.GradientBrush = brush;
            brush.RadiusX = 0.2f;
            brush.RadiusY = 0.3f;  //randomise the pulse or track.. like a circuling fluid.. FUTURE

            body.GradientBrush = brush;

            brush.RadiusX = 1.0f;
            brush.RadiusY = 0.83f;  //could randomise in model iwth deviation factor?  
        
            //use current glow color or lighter rer for background body fluid plasma
            BodyColor firstStopColor = (Parent.GlowColor != null) ? Parent.GlowColor : _red;
 
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(firstStopColor, 0d));
         //   body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_transparentRed, 0.3d));
      //      body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_darkRed, 0.3d));   // this is too dark tho
            body.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_mediumRed, 0.3d));   
        }


    }
}