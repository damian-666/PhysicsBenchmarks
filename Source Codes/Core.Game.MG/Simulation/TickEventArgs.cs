using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Game.MG.Simulation
{
    public class TickEventArgs : EventArgs
    {
        private long _tick;
        private int _msecElapsed;
        private int _secondsElapsed;
        private int _fps;


        public TickEventArgs() { }

        public TickEventArgs(long tick, int msecElapsed, int secondsElapsed, int fps)
        {
            _tick = tick;
            _msecElapsed = msecElapsed;
            _secondsElapsed = secondsElapsed;
            _fps = fps;
        }

        public int Fps
        {
            get { return _fps; }
        }

        public int MillisecondsElapsed
        {
            get { return _msecElapsed; }
        }

        public int SecondsElapsed
        {
            get { return _secondsElapsed; }
        }

        public long Tick
        {
            get { return _tick; }
        }
    }
}
