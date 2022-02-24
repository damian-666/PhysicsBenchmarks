using System;
using System.Collections.Generic;
using System.Text;

using Core.Data;
using Core.Data.Plugins;


namespace Core.Data.Interfaces
{
    /// <summary>
    /// Extends level, after the level is set as parent, calls for custom initialization code or to run a test or run races like optimization study
    /// TODO if useful add Spirit plugin methods UpdatePhysics,   void OnUserInput(GameKeyEventArgs e);
    /// </summary>
    public interface ILevelPlugin : IPlugin<Level> { }



}
