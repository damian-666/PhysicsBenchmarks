using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using Farseer.Xna.Framework;

using Core.Data.Collections;

namespace Core.Data.Interfaces
{
    public class SysLog : NotifyPropertyBase
    {
        #region MemVars & Props

        public delegate void OnPrintHandler(object sender, string text);
        public event OnPrintHandler OnPrint = null;

        private ObservableCollectionUndoable<string> _logs = new ObservableCollectionUndoable<string>();
        public ObservableCollectionUndoable<string> Logs
        {
            get { return _logs; }
            set
            {
                NotifyPropertyChanged("Logs");
            }
        }

        private static SysLog _instance = null;
        public static SysLog Instance
        {
            get
            {
                if (_instance == null) _instance = new SysLog();
                return _instance;
            }
        }

        public string Text
        {
            get
            {
                string output = "";
                foreach (string s in _logs)
                {
                    output = string.Format("{0}{1}\n", output, s);
                }

                return output;
            }
        }

        #endregion


        #region Ctor

        public SysLog()
        {
        }

        #endregion


        #region Methods

        public void Clear()
        {
            _logs.Clear();

            NotifyPropertyChanged("Logs");
            NotifyPropertyChanged("Text");

            if (OnPrint != null) OnPrint(this, "");
        }

        public void PrintVertices(IEnumerable<Vector2> verts)
        {
            string log = "";
            foreach (Vector2 v in verts)
            {
                log = log + string.Format("Vector2({0}, {1})\n", v.X, v.Y);
                _logs.Add(log);
            }

            NotifyPropertyChanged("Logs");
            NotifyPropertyChanged("Text");

            if (OnPrint != null) OnPrint(this, log);
        }

        public void Print(string log)
        {
            _logs.Add(log);

            NotifyPropertyChanged("Logs");
            NotifyPropertyChanged("Text");

            if (OnPrint != null) OnPrint(this, log);
        }

        public void DeleteLine(int line)
        {
            if (line > 0 && line < _logs.Count)
            {
                _logs.RemoveAt(line);
            }

            NotifyPropertyChanged("Logs");
            NotifyPropertyChanged("Text");
        }

        public string GetLogAtLine(int line)
        {
            if (line > 0 && line < _logs.Count)
            {
                return _logs[line];
            }

            return "";
        }



 #if !(UNIVERSAL )
        public void SaveToFile(string filename)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string log in _logs)
                builder.Append(log);

            using (StreamWriter sw = new StreamWriter(filename))
            {
                sw.Write(builder.ToString());
            }
        }

#endif
        #endregion
    }
}
