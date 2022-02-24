using System;
using System.Net;
using System.Windows;


using System.Windows.Input;

using Core.Data.Entity;
using System.Diagnostics;



namespace Core.Data.Animations
{

    //TODO.. could generalize  a sin wave in here now.  performs better for chop..  is similar to TroichodalWave on small waves like ripple   especiall if done in sets of one.
    public enum WaveTrainType
    {
        DefaultSets,  //no deviation
        SetDeviation,
        StormyRandom,
        HugeSwell
    }


    /// <summary>
    ///  Class of Ocean like ground swell wave , one or a set of them, a wave train..  Pumps a wave give parametric equation and y and x as function or w ( angle)  like a rolling ball tracing a point.  use xscale to space them out.  could also do a flat sea..  between sets, groups, etc.. not sure what is most realistic..wrt real waves with are Stokes waves.. so they come in sets.. using large x scale.. or scale down Y.. 
    /// //Note on base class is just effect..  measurement/ expermentation is needed for now. only height, binwidth ,  and shape and scale will determine the wavelengh..  with is the height when gamma is 1.    and  periodic effect uses period we use params which determne the wavelenght.. there the perion is uknown, we give the dw ( angle param, it draws the curve) .  Lowfrequency effect is too much.  , has nested effects .. tho.  ( could be used to "wave pulse trains.... not sure here, our seas will probalby we so should only on wave train at a time, using wave count here and long wave length
    /// </summary>
    public class TroichodalWave : Effect
    {

        public Action<Effect> OnCycleBeginEffect;
        public Action<Effect> OnCycleEndEffect; //make sure it goes to zero?

        private double _w = 0;  //parameter to generate the troichoidal waves.   need to bump this untill the next x bin is reached.
        private double _binWidth = 0;

        public double A;

        private double _dw;
        public double YValue;  //the output of this wave generator.  it procedes with each update.

        public double AngleDuration;
        public double WaveLength;  //calculated

        public double XScale;    // this is seen in the ocean at various apects and scales  1/ 7 height to crest is supposed to be the steepest stable
        public double YScale;

        public double Gamma;
        public int _currentHalfCycle;

        public WaveTrainType WaveTrainType;

        public int BinToPump;

        float[] yValues;

        int Numwaves = 0;

        int binindex;


        //TODO breakout ref to ocean parms if this wil be in core
        // we dont have multiple file plugins tho..could be a child class of the Ocean, or require precompilatoin


      //  OceanParams

        /// <summary>
        ///Ocean like ground swell wave.   Specify the name, height, shape  ( 1 is sharp ,  5 is more sine) , count,   then binwidth, bin, and a param ( detail) // TODO remove the last dAngle
        ///This is a parametric wave.  Thing of a rolling ball or radius A.    if B    There is a point in or on the ball.  is the radius  it gets it x and y values from the equation based on _w ( angle).   The resolution (binwidth) may determine the maximum celerity  ( phase speed of the wave) 
        /// If theta = 1  , the height is 2A.   it can be scaled via X , this wave shape is observed in wave tanks  .  a ratio of 1 to 7  for  height to wavelength..  is realistic on a wave gamma =1, anything more steep ,should start to crumble. ( emit particles in celerity direction) 
        /// Class of Ocean like ground swell wave , one or a set of them, a wave train..  Pumps a wave give parametric equation and y and x as function or w ( angle)  like a rolling ball tracing a point.  use xscale to space them out.  could also do a flat sea..  between sets, groups, etc.. not sure what is most realistic..wrt real waves with are Stokes waves.. so they come in sets.. using large x scale.. or scale down Y.. 
        ///  measurement/ expermentation is needed for now. only height, binwidth ,  and shape and scale will determine the wavelengh.. with is the height when gamma is 1.    and  periodic effect uses period we use params which determne the wavelenght.. there the perion is uknown, we give the dw ( angle param, it draws the curve) .  Lowfrequency effect is too much.  , has nested effects .. tho.  ( could be used to "wave pulse trains....     
        /// The trochoid shape does approach the sine curve in shape for small amplitudes.
        /// Real Stokes waves ( modelling deep water ocean long perion  big swells)  are complex equations, but are very similar to these.  thats why we allow stretching..  
        /// http://hyperphysics.phy-astr.gsu.edu/hbase/waves/watwav2.html 
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="name"></param>
        /// <param name="A.. close to waveHeight"></param>
        /// <param name="gamma">must be > 1 for a function of x.   sharpest   wave is 1.   </param>
        /// <param name="xScale"></param>
        /// <param name="yScale"></param> 
        ///  <param name="numWaves"></param>// NOTE  mostly for testing rarely we do more than1.. identical waves are rare in nature 
        /// <param name="binWidth"></param>
        /// <param name="binToPump"></param>
        /// <param name="dAngle">how much the parmaters angle is bumped each update NOTE.. should be auto , needs to be tuned in the tool higher for sharper</param>
        public TroichodalWave(Spirit sp, string name, double a, double gamma, double xScale, double yScale, int numWaves, double binWidth, int binToPump, float dAngle, WaveTrainType wavetrain)
            : base(sp, name)
        {
            A = a;   //TODO better put in terms of wave height
            //     A = waveHeight;   //  //TODO this is wrong, A is not height,  this is (A + A / Gamma);     SEE THE EQUATION

            _currentHalfCycle = 1;
            Numwaves = numWaves;
            SetParams(A, gamma, xScale, yScale, binWidth, binToPump, dAngle);
            WaveTrainType = wavetrain;
        }

        private void SetParams( /*double angleDuration OLD WAY*/ double a, double gamma, double xScale, double yScale, double binWidth, int binToPump, float dAngle)
        {

            _binWidth = binWidth;   //bin width, x length.  this is a fixed amount, the body of water is divided in to N bin

            A = a;
            Gamma = gamma;

            _dw = dAngle;
            ///    AngleDuration = angleDuration;  OLD WAY CALC All curve..
            YScale = yScale;
            XScale = xScale;

            //open ocean wave looks like
            //  http://hyperphysics.phy-astr.gsu.edu/hbase/waves/watwav2.html     7 width  to 1 height  is stable open ocean wave with gamma is one  sharp crest.
            // see alow http://www.engr.mun.ca/murrin/6002_notes_08_L4.pdf
            //in real waves, 1 / 7 ratio height to with is the max stable wave..
            // troichodal waves can be plotted on cyclce at nyquist, since only parametric equations are avallable not  y = f(x)
            //bin width of ocean reagion
            // then pumped..
            //http://en.wikipedia.org/wiki/Cycloid
            //http://en.wikipedia.org/wiki/Trochoid
            //http://users.softlab.ece.ntua.gr/~ttsiod/wavePhysics.html  ( java) 

            //   double B = A / gamma;     // B must be > A..   so Theta must be > 1.     sharpest   wave is 1.    smoother longer wave has higher theta

            // however we will tune this for typical range of ocean waves 10 sec period,  10 ft, etc....  noise or chop will be added using sine waves or throichodal smaller waves         
            // just a guess.. should be related to this.   slope is  never  more that about 70 degrees ..   smaller the radius, its sharper.. i think, so greater  theta means more resolution needed, smaller _dt;
            //one cycle ( Period is 2pi)
            //for theta > 1  its smoother
            //for theta = 1 its sharpest crest.

            if ((gamma) < 1)
            {
                Debug.WriteLine("gamma must be greater than 1, which gives the sharpest crest in TroichodalWave." + gamma.ToString());
                gamma = 1;
            }

            WaveLength = (A * 2 * Math.PI - (A / Gamma)) * XScale;

            AllocateWaveBuffer();

            double waveHeight = A;

            Debug.WriteLine("TroichodalWave  L , H" + WaveLength.ToString() + "  " + waveHeight.ToString());
            BinToPump = binToPump;

            double dxdwMin = (A - (A / Gamma)) * XScale;  // cos ( pi ) =1   // see derivative below
            double dxdwMax = (A) * XScale;  //sin 0 = 0  ..starts of  flat for a while

            Debug.WriteLine("dx/dw Min " + dxdwMin.ToString());
            Debug.WriteLine("dx/dwMax " + dxdwMax.ToString());
            Debug.WriteLine("dAngle" + dAngle.ToString());
            _w = 0;

        }

        private void AllocateWaveBuffer()
        {
            const int padding = 5;//just to be sure
            yValues = new float[(int)Math.Ceiling(WaveLength / _binWidth) + padding];  //padding  //NOTE .. our  equation is value only untill w = 2pi  ( half of the cycle , or the wave peak) then   it develops a ripple when stepping... or the derivative is completely wrong
        }


        public override void Update(double dt)
        {
            double x = 0;
            int index = 0;

            //http://en.wikipedia.org/wiki/Trochoid
            //PARAMETRIC EQUATIONS for Trochoid wave.., ON _w..  _w goes around the clock.
            //_w is T ( linear time)   
            //formula  is x = A* _w- B sin ( _w + phaseshift);        ,,, _w t in steps..  have to choose a good step based on # bins.   determine X bin then Y.
            // x = A* _w - A/ gamma *  sin ( _w + phaseshift);        ,,, _w t in steps..  have to choose a good step based on # bins.   determine X bin then Y.
            //y   = A - B cos(_w + phaseshift);
            // Gamma = A/B;  so    B = A /Gamma 

            //NOTE these wave forms can be scaled.. x is 7 x is max stable observed.
            //for sharpest wave   //x = r ( t - sin t); y = r(1 - cost );
            //NOTE y = f(x) equations for common or extreme troichodal waves don't exist as standard functions
            const double phaseShift = Math.PI;   // this will start the wave at bottom  zero.
            ///   const double phaseShift = 0;  // this will start the wave at bottom  zero.   as in wiki graph
            //    const double phaseShift = Math.P;   // this will start the wave at bottom  zero.
            // we need to know the rate of change of x wrt w
            //  http://www.sosmath.com/calculus/diff/der03/der03.html d/dx sin' x= cosx     cos'X = -sin x  ( todo see the calculus with the fucking phase shift and with the other thing..   .. TODO go back to old way.. skip for noow .. this is right tho need ANTOHER FUCKING WHOLE DAY YOU ARE STUDIPD
            //d dx A * _w = A

            // partial dervative formula =  wrt  _w  is   dx/dw  =  A  - Bcos ( _w + phaseshift)
            // so it is   in terms of gamma and A  is     (    A -   A / gamma * cos ( _w + phaseshift)   )   *   X scale.

            //EQUATIONS      NEEDED TO GRAPH:  
            //y   =(  A -  A / gamma * cos(_w + phaseshift)  ) * Xscale 
            //dx/dw = A - A / gamma * cos ( _w + phaseshift  ) * Xscale

            if (_w + phaseShift < Math.PI + phaseShift) //TODO phase shift _w + phase < Math + phase?
            {
                double dxdw;
                dxdw = (A - A / Gamma * Math.Cos(_w + phaseShift)) * XScale;  // take the partial derivatitve wrt w.. change in X for change in angle, we want X to be binwidth.

                double dw = _binWidth / dxdw;

                _w += dw;

                index = (int)Math.Floor((x) / _binWidth);// need to advance parameter _t until x is in the next bin.   then we have  y = f( x)   (if _DX is too small, takes too long, too big and skips a bin.. will elogate wave

                YValue = A - (A / Gamma) * Math.Cos(_w + phaseShift);
                YValue -= (A + A / Gamma);   //zero it


                if (binindex < yValues.Length)  //TODO this shouldn't  happen but it does on the boat level..
                {
                    yValues[binindex] = (float)YValue; //storing only floats.. we are using  float precision for the wave to save memory ( on say phones)
                    binindex++;
                }

            }
            else   // just use the stored values, mirror it back for downslop
            {

                binindex--;
                if (binindex > 0)
                {
                    YValue = yValues[binindex];
                }
                else
                {
                    _currentHalfCycle += 1;  //prepare to start calcuatating again
                    _w = 0;

                }
            }


            //solved on to find h ( offset)  such that Y = 0 when w = 0.
            //h =  -a - a/ theta
            YValue *= YScale;

            if (OnUpdateEffect != null)
            {
                OnUpdateEffect(this);
            }
            else
            {
                Debug.WriteLine("handle OnUpdateEffect to receive  the YValue for this wave, then displace the water or other thing");
            }


            if (_currentHalfCycle > Numwaves)
            {
                OnCycleBeginEffect = null;   //  to prevent a leak.. it is still listening
                //After this 
                if (OnCycleEndEffect != null)
                {
                    OnCycleEndEffect(this);
                    OnCycleEndEffect = null;
                }
                Finish();
            }

            if (OnCycleBeginEffect != null)
            {
                if (binindex == 0) // 
                {
                    OnCycleBeginEffect(this);  // this can allow the Yscale to be modified.. other params wont change anything since the wave is fully calculated.
                    AllocateWaveBuffer();  //if Xcale, gama, or A are change.. need a new buffer here.
                }
            }
        }
    }
}
