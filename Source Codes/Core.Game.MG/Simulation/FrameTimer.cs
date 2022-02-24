using System;
using System.Timers;
using System.Windows;



namespace Core.Game.MG.Simulation
{


    /// <summary>
    /// Wrapper for System.Timer, that runs on a background thread, with timing calculations not using Stopwatch for silverlight and basic compantibility
    /// Note, the resolution is based on the windows global resolution and power saving setting can be as low as 10 ms or more, resulting in framerate well under system capability
    /// </summary>
    public class FrameTimer
    {
        public event EventHandler<TickEventArgs> OnTick = null;

        Timer _timer;

        protected DateTime _timeStart;
        protected DateTime _timeLastUpdate;
        protected DateTime _timeLastFPSUpdate;
        protected long _tickDivider = 6000;
        protected int _FPS = 0;
        protected int _frameCount = 0;


        public FrameTimer()
        {

            _timer = new System.Timers.Timer();
            _timer.Elapsed += timer_Tick;
        }

        private void timer_Tick(object sender, object e)
        {
            TickUpdate();
        }

        private void TickUpdate()
        {
            // Tick
            TimeSpan totalTime = DateTime.Now - _timeStart;
            if (totalTime.Ticks < 0)
            {
                _timeStart = DateTime.Now;
                totalTime = TimeSpan.Zero;
            }

            long ticks = totalTime.Ticks / _tickDivider;

            // ElapsedTime
            TimeSpan elapsedTime = DateTime.Now - _timeLastUpdate;

            // FPS
            _frameCount++;
            if ((DateTime.Now - _timeLastFPSUpdate).Seconds >= 1)
            {
                _FPS = _frameCount;
                _frameCount = 0;
                _timeLastFPSUpdate = DateTime.Now;
            }

            if (OnTick != null) OnTick(this, new TickEventArgs(
                ticks, elapsedTime.Milliseconds, elapsedTime.Seconds, _FPS));
        }

        public void Reset()
        {
            _timeStart = DateTime.Now;
            _timeLastUpdate = DateTime.Now;
            _timeLastFPSUpdate = DateTime.Now;
        }

        public void Start()
        {
            _timeStart = DateTime.Now;
            _timeLastUpdate = DateTime.Now;
            _timeLastFPSUpdate = DateTime.Now;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>
        /// Timer interval in milliseconds, not realtime, may lag
        /// </summary>
        public double Interval
        {
            get { return _timer.Interval; }
            set { _timer.Interval = value; }
        }

        public bool IsEnabled
        {
            get { return _timer.Enabled; }
        }

        public long TickDivider
        {
            get { return _tickDivider; }
            set { _tickDivider = value; }
        }

    }

}
