using Core.Data.Interfaces;
using Core.Data;
using System;

using System.Threading.Tasks;

/// <summary>
/// Custom app starup plugins might be useful someday but now not used, using this 
/// directly referenced class.  This could all be r emoved asnd app goe direclty  to Reloadworking file if it toggled on
/// we used the shared datastore if the filename is null or empty it won't load a file on starup
/// TODO ressurect or deletge all this includding the IAppStartup interface def
/// </summary>

public class AppStartup: IAppStartup
{

    public Action ReloadLastSaved;

    public IApp AppServices { get; set; }


    public Action Loaded()
    {
        //by default app will reload the file in LastSavedLevel.txt in app root folder

        if (AppServices != null)
        {
            AppServices.ReloadWorkingFile();
        }


        return null;
    }

    public void SetApp(IApp app)
    {
        AppServices = app;
    }
}