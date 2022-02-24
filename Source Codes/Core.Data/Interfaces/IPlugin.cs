using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FarseerPhysics.Common;

using Core.Data.Entity;
using Core.Data.Input;
using Core.Data.Interfaces;

namespace Core.Data.Plugins
{
    /// <summary>
    /// Plugin Interface for Plugin Script Code and Classed, this plugn extents the spirit class
    /// (TODO now that we have LevelPlugin,  consider renaming to this IPlugin to  ISpiritPlugin or IEntityPlugin?)
    /// consolidate to derive from new IPluginBase wiht the Loaded Unloaded
    /// </summary>
    public interface IPlugin<T>: IPlugin
    {

         T Parent { get; set; }



        /// <summary>
        /// Called when Spirit update its physics
        /// </summary>
        /// <param name="spirit">spirit</param>
        /// <param name="tick">tick</param>
        /// <param name="userData">user data</param>
        void UpdatePhysics(double tick, object userData);

       /// <summary>
        /// Called when Spirits all update in parrelel
        /// </summary>
        /// <param name="spirit">spirit</param>
        /// <param name="tick">tick</param>
        /// <param name="userData">user data</param>
        void UpdatePhysicsBk(double tick, object userData);

        
        void Draw(double ms);

        /// <summary>
        /// Object call this if want to update Animation per frame
        /// </summary>
        /// <param name="spirit">spirit</param>
        /// <param name="tick">tick</param>
        /// <param name="userData">user data</param>
        void PreUpdateAnimation(double tick, object userData);

        void PostUpdateAnimation(double tick, object userData);

        /// <summary>
        /// Object call this if want to update AI per frame
        /// </summary>
        /// <param name="spirit">spirit</param>
        /// <param name="tick">tick</param>
        /// <param name="userData">user data</param>
        void UpdateAI(double tick, object userData);

        /// <summary>
        /// Triggered when input is updated on spirit.
        /// </summary>
        void OnUserInput(GameKeyEventArgs e);

    }

 


}
