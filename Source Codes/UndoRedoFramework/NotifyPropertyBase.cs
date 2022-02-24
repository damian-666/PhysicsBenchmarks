using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Core.Data.Interfaces
{
    [DataContract (IsReference=true)]
    public class NotifyPropertyBase : INotifyPropertyChanged, INotifyPropertyChanging
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        public void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            NotifyPropertyChanged(propertyName);
        }

        //TODO refactor rename this to NotifyPropertyChanged, then rename above to NotifyPropertyChanged
        public void NotifyPropertyChanged(string propertyName)
        {
            try
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            catch (Exception ex)
            {
                Debug.WriteLine("exception on notify " + this.GetType() + " " + propertyName );
                Debug.WriteLine(ex);
            }
        }

        public void FirePropertyChanging([CallerMemberName] string propertyName = null)
        {
  
            NotifyPropertyChanging(propertyName);
        }

        public void NotifyPropertyChanging(string propertyName)
        {
          
            if (PropertyChanging != null)
            {
                PropertyChanging(this, new PropertyChangingEventArgs(propertyName));
            }
   
        }
    }


}
