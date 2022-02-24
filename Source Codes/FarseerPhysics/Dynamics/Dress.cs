using System;
using System.Net;

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FarseerPhysics.Dynamics
{


    //TODO   use this.. allow UI to show it.
    // then fix importer to use it.   this is to save tons of memory on broken and clipped dress parts..
    //also add a loop to fix existing files if strings are the same then break out a ref to one of these.. like on rope bridge
    // and on creatures or bullets or boards 
    // then  add a map to it ..   map zoom to Name.    and maybe  to Name + dess Scale and other mods, such as color replacements.

    /// <summary>
    /// A class to avoid repeat references to dress in files that result in bloat.
    /// </summary>
    /// 
    [DataContract(Name = "Dress", Namespace = "http://ShadowPlay", IsReference = true)]
    public class Dress
    {
       // string Name;             //name as a key.
      //  string Canvas;        //XAML canvas to be rendered.
    }
}
