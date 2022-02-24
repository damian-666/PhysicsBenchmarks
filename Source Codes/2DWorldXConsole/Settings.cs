using _2DWorldCore;

using Microsoft.Xna.Framework;
using Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace ConsoleApp
{
    class Settings
    {

        //TODO any thing aobut window state ..could check the mg window state more
        //carefull.. we gave upu and used forms but the pos and size is supposed to work
        //mabyhe SDL will work its in the vault in history

        static void BeginCode()
        {
            ReloadLastSaved(); 
        }

     

        static public void LoadSettings()
        {

            //TODO if offset or somethign dont use, happed once
            if (DataStore.Instance.ContainsKey(windowPosXKey))
            {
                Point pos = new Point(
                    int.Parse(DataStore.Instance[windowPosXKey]),
                    int.Parse(DataStore.Instance[windowPosYKey]));

                if (pos.X < 0 || pos.Y < 0)
                    return;

                
                CoreGame.Instance.Window.Position = pos;

                CoreGame.Instance.Width = int.Parse(DataStore.Instance[windowWidthKey]);
                CoreGame.Instance.Height = int.Parse(DataStore.Instance[windowHeightKey]);


              }

            if (DataStore.Instance[windowMaxedKey] == "True")
            {

            //   Form form = (Form)Control.FromHandle(CoreGame.Instance.Window.Handle);
            //   form.WindowState = FormWindowState.Maximized;
            }
        }

        const string windowPosXKey = "WindowPosX";
        const string windowPosYKey = "WindowPosY";
        const string windowWidthKey = "WindowWidth";
        const string windowHeightKey = "WindowHeight";
        const string windowMaxedKey = "WindowMaximized";

        public static void OnGraphicsDeviceSizeChanged()
        {
            SaveWindowState();
        }

        public static void SaveWindowState()
        {
            if (!CoreGame.Instance.Window.AllowUserResizing)
                return;

             bool isMaximized = false;//   form.WindowState == FormWindowState.Maximized;

            if (!isMaximized)
            {
                DataStore.Instance[windowPosXKey] = CoreGame.Instance.Window.Position.X.ToString();
                DataStore.Instance[windowPosYKey] = CoreGame.Instance.Window.Position.Y.ToString();
                DataStore.Instance[windowWidthKey] = CoreGame.Instance.Window.ClientBounds.Width.ToString();
                DataStore.Instance[windowHeightKey] = CoreGame.Instance.Window.ClientBounds.Height.ToString();
            }

            DataStore.Instance[windowMaxedKey] = isMaximized.ToString();
            DataStore.SaveToDisk();
        }

        public static void ReloadLastSaved()
        {
            try
            {
                CoreGame.ReloadLastSaved();  //uncomment this first, trace it.   issue wiht the threading
            }

            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            };
        }
    }
}


