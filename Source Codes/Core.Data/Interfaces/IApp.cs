using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Core.Data.Interfaces
{
    public interface IApp
    {
        void ReloadWorkingFile();
    }

    
    public interface IAppStartup
    {
        //gets called once on startup for custom initialize behavior to speed debug cycle between tool and game
        //default calls back to apps ReloadWorkingFile
        Action Loaded();
        void SetApp(IApp app);
    }




}
