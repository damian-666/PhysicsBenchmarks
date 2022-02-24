using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FarseerPhysics.Controllers
{

    //TODO wind drag should broken up, and  be in farseer.. 
    // for now it isi using visisble rays for debuging..
    public class WindDragConstants
    {
        public static float DefaultAirDensity = 1f;
        public static float DefaultTemperature = 21f;  //celcius
        public static float MinLiquidDensity = 10;  // When drag controlller starts to act more like a liquid.. use bouyancy.. skip stream blocking .. stop particles.  ( Acually is tuned) 
             
    }
}
