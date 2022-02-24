// Siarhei Arkhipenka (c) 2006-2007. email: sbs-arhipenko@yandex.ru
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace UndoRedoFramework
{
    public class Command : Dictionary<IUndoRedoMember, object>, IDisposable
    {
        public readonly string Caption;
        public Command(string caption)
        {
            Caption = caption;
        }

        public readonly object Owner;
        public Command(string caption, object owner)
        {
            Caption = caption;
            Owner = owner;
        }

        public readonly object UserData;
        public Command(string caption, object owner, object userData)
        {
            Caption = caption;
            Owner = owner;
            UserData = userData;
        }
        #region IDisposable Members
        //this is so that the Using framework can be used with the framework  
        void IDisposable.Dispose()
        {
            if (UndoRedoManager.CurrentCommand != null)
            {
                if (UndoRedoManager.CurrentCommand == this)
                    UndoRedoManager.Cancel();
                else
                {
                    Debug.WriteLine("There was another command within disposed command");
                }

            }
        }
		#endregion	
   }
}
