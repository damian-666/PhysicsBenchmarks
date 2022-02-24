using System;
using System.Collections.Generic;
using System.Diagnostics;
using Core;
using Microsoft.Xna.Framework.Input.Touch;


namespace MGCore
{
	/// <summary>
	/// to enable touch input you must first call enableTouchSupport()
	/// </summary>
	public class TouchInput
	{
		public bool IsConnected => _isConnected;
		public TouchCollection CurrentTouches => _currentTouches;

		public TouchCollection PreviousTouches => _previousTouches;
		public List<GestureSample> PreviousGestures => _previousGestures;
		public List<GestureSample> CurrentGestures => _currentGestures;

		TouchCollection _previousTouches;
		TouchCollection _currentTouches;
		List<GestureSample> _previousGestures = new List<GestureSample>();
		List<GestureSample> _currentGestures = new List<GestureSample>();

		bool _isConnected;


		void OnGraphicsDeviceReset()
		{
			TouchPanel.DisplayWidth = MGCore.Instance.GraphicsDevice.Viewport.Width;
			TouchPanel.DisplayHeight = MGCore.Instance.GraphicsDevice.Viewport.Height;
			TouchPanel.DisplayOrientation = MGCore.Instance.GraphicsDevice.PresentationParameters.DisplayOrientation;

			Input.DualTouchStick?.CreateTouchSticks();
			
		}


		internal void Update()
		{
			if (!_isConnected)
				return;

			_previousTouches = _currentTouches;
			_currentTouches = TouchPanel.GetState();

			_previousGestures.Clear();
			_previousGestures.AddRange(_currentGestures);
			_currentGestures.Clear();
			//TODO this might be a good diea

			//TODO  still get collection modifed exeption makingt this copy
			//also someone advise not doing this more than once per frame.. might consider it on Uppdate or draw if 
			//suspected on destabilitzing
			try
			{
				while (TouchPanel.IsGestureAvailable)
				{
					_currentGestures.Add(TouchPanel.ReadGesture());
				}
			}

			catch(Exception exc)//collection modified.. we might need to update from ui thread
            {
				Debug.WriteLine("TouchPanel update "+ exc.ToString());
            }
		}


		public void EnableTouchSupport()
		{
			_isConnected = TouchPanel.GetCapabilities().IsConnected;


			TouchPanel.EnableMouseGestures = false;
			TouchPanel.EnabledGestures = GestureType.Pinch  |GestureType.PinchComplete | GestureType.Flick |GestureType.VerticalDrag| GestureType.DragComplete;

			//TODO mg_graphics do we need this?  who in nez listens   go over dependencies
			if (_isConnected)
			{
				MGCore.Emitter.AddObserver(CoreEvents.GraphicsDeviceReset, OnGraphicsDeviceReset);
				MGCore.Emitter.AddObserver(CoreEvents.OrientationChanged, OnGraphicsDeviceReset);
				OnGraphicsDeviceReset();
			}
		}
	}
}