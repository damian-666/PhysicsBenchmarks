using Core.Game.MG.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace _2DWorldCore
{
    class PluginRefs {

        //TODO erase, dont think its needed one 
        //set SDK only on xamarin  linker
        static double refer= 0;

        //TODO this is to foil the assembly package trimming happesn in xamarin even though we mark plugin assemb as not trimmable
        static public void traceTypes() { 



            YndrdPlugin yndrdPlugin = new YndrdPlugin();

            JetStreamWind jet = new JetStreamWind();

            Debug.WriteLine(jet.GroundLevelY);
           
            refer =yndrdPlugin.TargetSlope;

    }
       

}
}
