using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace DebugHelpers
{
    class TraceDataListener : TraceListener
    {
        private TraceEventType _eventType = TraceEventType.Information;
        private DefaultTraceListener _defaultTrace;
        private bool _fail = false;

        public event Action<string> OnFail = null;
        public event Action<string, string, TraceEventType> OnEvent = null;

        private static TraceDataListener _instance = null;
        public static TraceDataListener Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TraceDataListener();

                }
                return _instance;
            }
        }


                 


   
        //TODO later

     //   public TraceDataListener()
     //   {
     //       _defaultTrace = new DefaultTraceListener();
   //
     //       Debug.Listeners.Add(this);
     //   }

 

        public void Stop()
        {
            System.Diagnostics.Trace.Listeners.Remove(this);
        }

        public override void Fail(string message)
        {
            _fail = true;
            if (message.Length == 0)
            {
                message = "Assert with no message";
            }
            base.Fail(message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            _eventType = eventType;
            base.TraceEvent(eventCache, source, eventType, id, message);
        }

        public override void Write(string message)
        {
            if (IndentLevel > 0)
            {
                message = message.PadLeft(IndentLevel + message.Length, '\t');
            }

            if (_fail)
            {
                if (OnFail != null)
                {
                    OnFail(message);
                }
            }
            else
            {
                if (OnEvent != null)
                {
                    OnEvent(message, "", _eventType);
                }
            }

            _fail = false;
            _eventType = TraceEventType.Information;
        }

        public override void WriteLine(string message)
        {
            Write(message + '\n');
        }

        public override void WriteLine(string message, string category)
        {
            Write(message + '\n', category);
        }



        public override void Write(string message, string category)
        {
            if (IndentLevel > 0)
            {
                message = message.PadLeft(IndentLevel + message.Length, '\t');
            }

            if (_fail)
            {
                if (OnFail != null)
                {
                    OnFail(message);
                }
            }
            else
            {
                if (OnEvent != null)
                {
                    OnEvent(message, category, _eventType);
                }
            }

            _fail = false;
            _eventType = TraceEventType.Information;
        }



    }

    
}
