

using Microsoft.Xna.Framework;


namespace Core.Game.MG
{


    /// <summary>
    /// To change things like background Color for day and night..
    /// </summary>

    public class AmbienceController
    {
        //Took a picture from web and use Gimp software to pickup Color. Then copy paste rgb value.
        //www.gimp.org
        //pictures of sky at different times of day, at one horizon
        //Media\RasterSources\skyMidwest.jpg
        // predefined Color destination for each part of day


        public static Color FromArgb(int a, int r, int g, int b)
        {
            return Color.FromNonPremultiplied(r, g, b, a);
        }


        readonly Color _morningTop = FromArgb(255, 169, 199, 207);
        readonly Color _morningMid = FromArgb(255, 176, 138, 129);
        readonly Color _morningBottom = FromArgb(255, 150, 70, 19);
        readonly Color _noonTop = FromArgb(255, 199, 238, 252);
        readonly Color _noonMid = FromArgb(255, 169, 199, 207);
        readonly Color _noonBottom = FromArgb(255, 185, 148, 77);
        readonly Color _sunsetTop = FromArgb(255, 184, 191, 209);
        readonly Color _sunsetMid = FromArgb(255, 66, 66, 76);
        readonly Color _sunsetBottom = FromArgb(255, 127, 73, 49);
        readonly Color _nightTop = FromArgb(255, 50, 50, 29);
        readonly Color _nightMid = FromArgb(255, 23, 25, 12);
        readonly Color _nightBottom = FromArgb(255, 46, 41, 19);

        public Color CloudEdgeColor = Color.White;//TODO use shadow shader, remove this shite

        public bool IsSunOverhead { get; set; } // set by ambience controller.  not true during sun set, sunrise


        const int UPDATE_INTERVAL = 10;  //tuning , tried 100 value , sunset a bit rough steps, fps not any different..
    
        /// <summary>
        /// BrightnessFactor less thatn 1, will darken color,  0.5 is half brightness
        /// </summary>
        public double BrightnessFactor { get; set; }

        public AmbienceController()
        {
            BrightnessFactor = 1.0;
        }

        /// <summary>
        /// Main update loop calls this with virtual age of world in secs.  On UI thread
        /// </summary>
        public void Update(double ageOfWorld, double lengthOfPlanetRotationCycle)
        {

#if GRAPHICS_MG
            _updateCounter++;

            //silverlight and WPF cache dirty and clean screen regions.  no need to dirty everything every cycle
            // for background bruch, only update every x frames.   
            if (_updateCounter % UPDATE_INTERVAL != 0)
                return;

            if (Level.Instance != null)
            {
                //TODO remove sunset glow effect underground.. maybe from lamps..
                if (   Level.Instance.LevelDepth > 2)  //underground is darker background for now..
                {
                      BrightnessFactor = 0.15f;
                }
                else if ( Level.Instance.LevelDepth > 1)
                {
                    BrightnessFactor = 0.2f;
                }
            }
           
            double timeOfday = ageOfWorld % lengthOfPlanetRotationCycle;
            
#endif
        }

#if GRAPHICS_MG
        public Brush CreateBackgroundBrush(double timeOfDayNightCycle , double lengthOfCycle)
        {
            double dayFraction = timeOfDayNightCycle / (lengthOfCycle / 2);  //half the cycle is night.

            //TODO FUTURE this should be in Simworld but needs to run on UI thread, should to reorganise this, tightly coupled thing
            SimWorld.Instance.IsSunOverhead = false;

            if (dayFraction >= 1.0f  )//&& dayFraction < 1.0f)   // night  when   sun  in behind 2d planet, completely dark for half the cycle
            {
                return BrushNight;      
            }

            LinearGradientBrush verticalGradientBrush = new LinearGradientBrush();
           
            //Looks weird but in 2d world sunset is to left or right.. we'll see ..
            //Also it East / West as the creature sees it will be different as suns sets to its west .   But also up and down will be different Color.
            // so doing diagonal gradient.. 

            verticalGradientBrush.StartVector2 = new Vector2(0, 0);
            verticalGradientBrush.EndVector2 = new Vector2(1, 1);

            GradientStop top = new GradientStop();
            GradientStop middle = new GradientStop();
            GradientStop bottom = new GradientStop();
            top.Offset = 0.0;
            middle.Offset = 0.65d;
            bottom.Offset = 1.0d;

            double noonTime = 0.6;  // 60 percent of the day..
            double lateafternoonTime = 0.30;
       //    double noonTime = 0.7;  // most of the day..
      //     double lateafternoonTime = 0.20;

           double sunsetTime = 0.10;
  
           //TODO review maybe this can be simpler... not 4 sections.
           // maybe well just use a RGB with adding Blue component  w/ time...

           CloudEdgeColor = Color.White;

            //simulate look of  2d planet  rotating  clockwise.. so sun rise in east , set in west. 

            double lambda;  //lamba goes from 0 to 1 between sections..

            if (dayFraction < sunsetTime)        // morning, sun 
            {
                lambda = dayFraction / sunsetTime;
           
           //     top.Color = ColorInterpolator.InterpolateBetween(_nightTop, _morningTop, lambda);
                top.Color = ColorInterpolator.InterpolateBetween(_nightTop, _sunsetTop, lambda, BrightnessFactor);
                middle.Color = ColorInterpolator.InterpolateBetween(_nightMid, _morningMid, lambda, BrightnessFactor);
                bottom.Color = ColorInterpolator.InterpolateBetween(_nightBottom, _morningBottom, lambda, BrightnessFactor);

                //   bottom.Color = ColorInterpolator.InterpolateBetween(_sunsetBottom, _nightBottom, lambda);

             //   top.Color = ColorInterpolator.InterpolateBetween(_sunsetTop, _nightTop, lambda);
              //  middle.Color = ColorInterpolator.InterpolateBetween(_sunsetMid, _nightMid, lambda);
             //   bottom.Color = ColorInterpolator.InterpolateBetween(_sunsetBottom, _nightBottom, lambda);
            //    CloudEdgeColor = Colors.Gold;
 
            }
            else if (dayFraction < sunsetTime + lateafternoonTime)   // morning 
            {
                lambda = (dayFraction - 0.1) / (lateafternoonTime);   //fraction of this section 
                top.Color = ColorInterpolator.InterpolateBetween(_morningTop, _noonTop, lambda, BrightnessFactor);

                middle.Color = ColorInterpolator.InterpolateBetween(_morningMid, _noonMid, lambda, BrightnessFactor);
                bottom.Color = ColorInterpolator.InterpolateBetween(_morningBottom, _noonBottom, lambda, BrightnessFactor);
                SimWorld2.Instance.IsSunOverhead = true;
            }
            else if (dayFraction < noonTime)   // high noon
            {
                //TODO use HSB , this maybe seems too bright cant see swords..
                top.Color = ColorInterpolator.DarkenColor(_noonTop, BrightnessFactor);
                middle.Color = ColorInterpolator.DarkenColor(_noonMid, BrightnessFactor);
                bottom.Color = ColorInterpolator.DarkenColor(_sunsetMid, BrightnessFactor);
    
                verticalGradientBrush.StartVector2 = new Vector2(0, 0);
                verticalGradientBrush.EndVector2 = new Vector2(0, 1);

                SimWorld2.Instance.IsSunOverhead = true;

            }
            else if (dayFraction < (1 - lateafternoonTime))   // late afternoon
            {
                lambda = (dayFraction - noonTime) / ((1 - lateafternoonTime) - (noonTime));
             //   top.Color = ColorInterpolator.InterpolateBetween(_noonTop, _sunsetTop, lambda);
           //     middle.Color = ColorInterpolator.InterpolateBetween(_noonMid, _sunsetMid, lambda);
//bottom.Color = ColorInterpolator.InterpolateBetween(_noonBottom, _sunsetBottom, lambda);
             //   top.Color = ColorInterpolator.InterpolateBetween(_noonTop, _morningTop, lambda);

                top.Color = ColorInterpolator.InterpolateBetween(_noonTop, _sunsetTop, lambda, BrightnessFactor);
                middle.Color = ColorInterpolator.InterpolateBetween(_noonMid, _morningMid, lambda, BrightnessFactor);
                bottom.Color = ColorInterpolator.InterpolateBetween(_noonBottom, _morningBottom, lambda, BrightnessFactor);

                //transition from   0,0.. 1,1 ..
             //   verticalGradientBrush.StartVector2 = new Vector2( 1, 0);
             //   verticalGradientBrush.EndVector2 = new Vector2(0, lambda);
                //    verticalGradientBrush.StartVector2 = new Vector2(1, 0);
                //   verticalGradientBrush.EndVector2 = new Vector2(0, .5);
   
                verticalGradientBrush.StartVector2 = new Vector2(1, 0);
                verticalGradientBrush.EndVector2 = new Vector2(0, 1);

                SimWorld2.Instance.IsSunOverhead = true;
         
            }
            else    // sunset
            {
                lambda = (dayFraction - (1 - lateafternoonTime)) / lateafternoonTime;
              //  top.Color = ColorInterpolator.InterpolateBetween(_sunsetTop, _nightTop, lambda);
             //   middle.Color = ColorInterpolator.InterpolateBetween(_sunsetMid, _nightMid, lambda);
             //   bottom.Color = ColorInterpolator.InterpolateBetween(_sunsetBottom, _nightBottom, lambda);

                top.Color = ColorInterpolator.InterpolateBetween(_sunsetTop, _nightTop, lambda, BrightnessFactor);
             //   top.Color = ColorInterpolator.InterpolateBetween(_morningTop,_nightTop, lambda);
                middle.Color = ColorInterpolator.InterpolateBetween(_morningMid, _nightMid, lambda, BrightnessFactor);
                bottom.Color = ColorInterpolator.InterpolateBetween(_morningBottom, _nightBottom, lambda, BrightnessFactor);
  
                verticalGradientBrush.StartVector2 = new Vector2(1, 0);
                verticalGradientBrush.EndVector2 = new Vector2(0, 1);
                
             //   CloudEdgeColor = Colors.Gold;
            } 
     
            verticalGradientBrush.GradientStops.Add(top);
            verticalGradientBrush.GradientStops.Add(middle);
            verticalGradientBrush.GradientStops.Add(bottom);

            return verticalGradientBrush;
        }
    }


    /// <summary>
    /// From http://stackoverflow.com/questions/1236683/Color-interpolation-between-3-Colors-in-net
    /// </summary>
    class ColorInterpolator
    {
        delegate byte ComponentSelector(Color color);
        static ComponentSelector _alphaSelector = Color => Color.A;
        static ComponentSelector _redSelector = Color => Color.R;
        static ComponentSelector _greenSelector = Color => Color.G;
        static ComponentSelector _blueSelector = Color => Color.B;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="endVector21"></param>
        /// <param name="endVector22"></param>
        /// <param name="lambda"></param>
        /// <param name="brightnessFactor"> if less that1, color will be darkened , 1 to 0.. using HSB</param>
        /// <returns></returns>
        public static Color InterpolateBetween(
            Color endVector21,
            Color endVector22,
            double lambda, double brightnessFactor)
        {
            if (lambda < 0 || lambda > 1)
            {
                throw new ArgumentOutOfRangeException("lambda");
            }
            Color color = FromArgb(
                InterpolateComponent(endVector21, endVector22, lambda, _alphaSelector),
                InterpolateComponent(endVector21, endVector22, lambda, _redSelector),
                InterpolateComponent(endVector21, endVector22, lambda, _greenSelector),
                InterpolateComponent(endVector21, endVector22, lambda, _blueSelector)
            );

            if ( brightnessFactor < 1.0f)
            {
                color = DarkenColor(color,brightnessFactor );
             }

            return color;
        }

        public static Color DarkenColor(Color color,double brightnessFactor )
        {
            HSBColor darkerColor = HSBColor.FromColor(color);
            darkerColor.B *= brightnessFactor;
            color = darkerColor.ToColor();
            return color;
        }


        static byte InterpolateComponent(
            Color endVector21,
            Color endVector22,
            double lambda,
            ComponentSelector selector)
        {
            return (byte)(selector(endVector21)
                + (selector(endVector22) - selector(endVector21)) * lambda);
        }
    }
#endif

    }
}
