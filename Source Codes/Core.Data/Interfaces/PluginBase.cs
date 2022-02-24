using Core.Data.Input;
using Core.Data.Plugins;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Data.Interfaces
{


    public interface IPlugin
    {

        /// <summary>
        /// Called after Parent set and object is loaded.  Parent is the extended object, Level or Spirit now implemented
        /// </summary>
        void Loaded();

        /// <summary>
        /// Called when object is unloaded, a level is changed, releasing resources, removing self can be done here.
        /// </summary>
        void UnLoaded();

        /// <summary>
        /// extensions are classes stored in separate files so they can be shared.. they get included in a Netstandard assembly for deployment and compiled as loose files in development in Tool
        /// the path is coded relative to the tool exe and uses the class name as a subfolder
        /// </summary>
         string Filename { get; set; }


    }




    /// The base for all the extendible object of any type.  
    /// </summary>
    /// <typeparam name="T">The class that this plugin extend. like spirti, entity or Level</typeparam>
    public abstract class PluginBase<T> : IPlugin<T>
    {

        /// <summary>
        /// The object that this plugin extends
        /// </summary>
        public T Parent { get; set; }

        /// <summary>
        /// extensions are classes stored in separate files so they can be shared.. they get included in a Netstandard assembly for deployment and compiled as loose files in development in Tool
        /// the path is coded relative to the tool exe and uses the class name as a subfolder
        /// </summary>
        public string Filename { get; set; }

        public virtual void Draw(double ms){}

        public virtual void Loaded() { }
        public virtual void OnUserInput(GameKeyEventArgs e) { }
        public virtual void PostUpdateAnimation(double tick, object userData) { }
        public virtual void PreUpdateAnimation(double tick, object userData) { }
        public virtual void UnLoaded() { }
        public virtual void UpdateAI(double tick, object userData) { }
        public virtual void UpdatePhysics(double tick, object userData) { }
        public virtual void UpdatePhysicsBk(double tick, object userData) { }
    }
}
