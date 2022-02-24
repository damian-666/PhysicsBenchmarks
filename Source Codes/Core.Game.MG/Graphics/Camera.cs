/*
 * This camera class is built upon WorldViewportTransform class
 * 
 * Copyright Shadowplay Studios, 2011.
 */

using System;


using System.ComponentModel;
using System.Collections.Specialized;
using System.Collections.Generic;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Collision;


using Microsoft.Xna.Framework.Graphics;
using Core.Data.Collections;
using Core.Data.Interfaces;
using Microsoft.Xna.Framework;

using Vector2 = Farseer.Xna.Framework.Vector2;
using Core.Data.Geometry;
using Core.Game.MG.Drawing;
using System.Diagnostics;

namespace Core.Game.MG.Graphics  
{
    /// <summary>
    /// This Camera has windows dependency, used the tool only
    /// </summary>
    public class Camera : NotifyPropertyBase
    {

       
        #region Variables

  

        protected WorldViewportTransform _wvTransform;
        protected CameraMode _cameraMode;

        protected long _prevTick = -1;
        protected long _deltaTick = 0;  // actual tick diff
        protected const float _tickAdjustment = 0.07f;  // adapt tick value

        protected CameraInput _cameraInput;
        protected float _panSpeed;
        protected float _rotateSpeed;
        protected float _zoomSpeed;

        protected bool _isCameraTrackingEnabled;
        protected EntityCollection _followedObjects;
        protected bool _isIgnoreInputWTracking;
        protected bool _isAutoZoomWTracking;
        protected bool _isAutoRotateWTracking;
        protected float _trackingPanSpeed = 0.24f;
        protected float _trackingRotateSpeed = 0.1f;
        protected float _trackingZoomSpeed = 0.15f;
        protected float _trackWindowFactor = 1;   // multipler of object aabb
        protected float _trackingTargetRotation;

        protected bool _isLazyTracking;
        protected bool _lazyTrackingOnMove;
        protected Vector2 _curLazyTrackingScreenCenter;
        protected bool _prevLazyTracking = false;
        protected long _lastLazyTrackingTick = 0;

        protected long _minDeltaBetweenTrackingSwitch = 1000;   // don't switch between mode too often

        protected bool _isKeepObjectAABBFixed;

        /// <summary>
        /// This is to switch turn between input and tracking update, 
        /// so both update can be done together without blocking.
        /// </summary>
        protected bool _updateTurn0;

        protected Vector2 _lastPos;

        #endregion

        Action ViewChanged= null;

        /// <summary>
        /// By default, this will automatically Activate camera on canvas, replacing 
        /// any existing camera. 
        /// </summary>
        /// <param name="viewport"></param>
        public Camera(GraphicsDevice gr,AABB startView)
        {

            // each camera should have its own wv transform, it's easier to replace
            // render transform on canvas than to switch to another wv transform.
            _wvTransform = new WorldViewportTransform(gr,startView);

            _wvTransform.PropertyChanged += _wvTransform_PropertyChanged;

            _followedObjects = new EntityCollection();
      
            _followedObjects.CollectionChanged +=
                new NotifyCollectionChangedEventHandler(OnFollowedObjects_CollectionChanged);


            //todo eval if we need 
            MarginRight = 0.1f;  //creature usually moves west
            MarginLeft = 0.2f;
            MarginBottom = 0.1f;   //show more sky than ground.
            MarginTop = 0.2f;

            _lastPos = new Vector2(float.NaN, float.NaN);

            IsFrameEnemies = true;


            //if we are tracking two characters engaging in gun battle how far can we zoom out
            MaxFrameObjectDistX = 15.0f;  //TODO use gun range?
            MaxFrameObjectDistY = 2.5f;

           
        }




        private void _wvTransform_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ViewChanged != null)
                ViewChanged();
        }



        public void Reset()
        {
            _wvTransform.WindowCenter = Vector2.Zero;
            _wvTransform.Zoom = 1;
            _wvTransform.WindowRotation = 0;
            TrackingTargetRotation = 0;
            _curLazyTrackingScreenCenter = Vector2.Zero;
        }



        // currently this method is only called from shadowtool.
        public void ResetTracking()
        {
            // dont set to false, so shadowtool can retain Camera Tracking state when reset level.            
            //IsCameraTrackingEnabled = false;

            FollowedObjects.Clear();
        }


        // timer tick is required here
        public void Update(long tick)
        {
            // compute elapsed time
            if (_prevTick < 0)
                _prevTick = tick;

            _deltaTick = tick - _prevTick;
            _prevTick = tick;

            float timeSlice = _deltaTick * _tickAdjustment;
            bool oninput = _cameraInput != CameraInput.None;
            bool ontracking = (_isCameraTrackingEnabled == true && _followedObjects.Count > 0);

            SetCameraMode(oninput, ontracking);

            // update camera movement
            switch (_cameraMode)
            {
                case CameraMode.OnTracking:
                    UpdateOnTracking(timeSlice);
                    break;
                case CameraMode.OnInput:
                    UpdateOnInput(timeSlice);
                    break;
            }
        }


        public float Zoom
        {
            set { this.Transform.Zoom = value; }
            get { return Transform.Zoom; }
        }



    

        /// <summary>
        /// set camera mode Behavior. 
        /// switch turn between camera and input update, between cycle, if necessary.
        /// </summary>
        private void SetCameraMode(bool oninput, bool ontracking)
        {
            if (ontracking && oninput)
            {
                if (_isIgnoreInputWTracking == true)
                {
                    _cameraMode = CameraMode.OnTracking;
                }
                else
                {                    
                    _updateTurn0 = !_updateTurn0;
                    if (_updateTurn0)
                    {
                        _cameraMode = CameraMode.OnInput;
                    }
                    else
                    {
                        _cameraMode = CameraMode.OnTracking;
                    }
                }
            }
            else if (ontracking)
            {
                _cameraMode = CameraMode.OnTracking;
            }
            else if (oninput)
            {
                _cameraMode = CameraMode.OnInput;
            }
            else
            {
                _cameraMode = CameraMode.OnIdle;
            }
        }


        private void UpdateOnInput(float timeSlice)
        {
            float zoomIncrement = timeSlice * _zoomSpeed;
            switch (_cameraInput)
            {
                case CameraInput.ZoomIn:
                    _wvTransform.Zoom += zoomIncrement;
                    break;

                case CameraInput.ZoomOut:
                    _wvTransform.Zoom -= zoomIncrement;
                    break;

                case CameraInput.PanUp:
                    _wvTransform.PanRelativeToWindow(0, timeSlice * -_panSpeed);
                    break;

                case CameraInput.PanDown:
                    _wvTransform.PanRelativeToWindow(0, timeSlice * _panSpeed);
                    break;

                case CameraInput.PanLeft:
                    _wvTransform.PanRelativeToWindow(timeSlice * -_panSpeed, 0);
                    break;

                case CameraInput.PanRight:
                    _wvTransform.PanRelativeToWindow(timeSlice * _panSpeed, 0);
                    break;

             //   case CameraInput.RotateClockwise:
            //        _wvTransform.WindowRotation += (timeSlice * _rotateSpeed);
             //       break;

             //   case CameraInput.RotateCounterClockwise:
              ////      _wvTransform.WindowRotation -= (timeSlice * _rotateSpeed);
                //    break;
            }
        }
        public bool TrackTargetBottom = true;

        public void PanRelativeToWindow(float x, float y) { _wvTransform.PanRelativeToWindow(x, y); }

        private void UpdateOnTracking(float timeSlice)
        {
            try
            {


                if (FollowedObjects.Count == 0 )
                    return;

                AABB aabb = GetFollowedObjectAABB();

                // always get position from current entity aabb, not from cached aabb.
                // save here first, because aabb might get change below.
                Vector2 currentObjPos = TrackTargetBottom ? new Vector2( aabb.Center.X, aabb.UpperBound.Y): aabb.Center;

                float panSpeed = TrackingPanSpeed;
                float aabbVelocity = 0;
                if (_lastPos.IsValid())
                {
                    // pan speed must be the same as tracked object velocity
                    aabbVelocity = (_lastPos - currentObjPos).Length();
                    if (aabbVelocity > panSpeed)
                    {
                        panSpeed = aabbVelocity;
                    }
                }

                _lastPos = currentObjPos;

   

                UpdateAutoZoomOnTracking(aabb);


                // update world position of window
                Vector2 targetPosition = currentObjPos;


                //// if window limit active and followed object outside window limit, cancel window limit.
                //// however this should only temporary, window limit should return back when switch to other
                //// followed object that inside limit.
                //if (_wvTransform.LimitEnabled == true && _wvTransform.WindowLimit.Contains(ref targetPosition) == false)
                //{
                //    _wvTransform.LimitEnabled = false;
                //    return;
                //}


                // prevent switching too often between normal tracking and lazy tracking. prevent jumpy camera.
                // if last lazy tracking is less than 1 second ago, will continue normal trcking for a while.

                // TODO: might need to do this for all tracking, not only for lazy tracking.
                // because switching between mode too often will cause jumpy camera.

                bool allowLazyTracking = false;
                if (_prevLazyTracking ||
                    (_prevTick - _lastLazyTrackingTick) > _minDeltaBetweenTrackingSwitch)
                {
                    allowLazyTracking = true;
                    _prevLazyTracking = false;
                }
                //else
                //{
                //    // switching too soon
                //    int debug = 1;
                //}


                // only use lazy tracking on followed object if object speed is no higher than pan speed
                if (panSpeed <= TrackingPanSpeed && allowLazyTracking)
                {
                    UpdateLazyTracking(aabb, aabbVelocity, ref targetPosition);
                }


                // moving to target position only if distance is not too small
                float tdistX = Math.Abs(targetPosition.X - _wvTransform.WindowCenter.X);
                float tdistY = Math.Abs(targetPosition.Y - _wvTransform.WindowCenter.Y);
                if (tdistX > 0.01f || tdistY > 0.01f) // TODO should use variable later
                {
                    _wvTransform.WindowCenter = Vector2.SmoothStep(
                        _wvTransform.WindowCenter, targetPosition, panSpeed);
                }
                else
                {
                    // reset any moving mode here

                    // TODO: might need to always reset lazy tracking screen center even when normal tracking used,
                    // because sometimes after sudden stop from high speed normal tracking, camera seems jump to nowhere and start lazy tracking.

                    _lazyTrackingOnMove = false;
                    ResetLazyTrackingCenter();
                }

                UpdateAutoRotateOnTracking();
            }
            catch( Exception exc)
            {
                Debug.WriteLine(exc);
            }
        }


        private AABB GetFollowedObjectAABB()
        {
            // get initial aabb, combined from one or more entities. 
            // if entering here means _followedObjects.Content > 0
            List<IEntity>  var= new List<IEntity>(FollowedObjects); //avoid collection modified by bk thread when creature regen


         
            AABB aabb = var[0].EntityAABB;

            foreach (IEntity ent in var)
            {
                ent.UpdateAABB();
                AABB ea = ent.EntityAABB;
                aabb.Combine(ref ea);
            }
            return aabb;
        }


        /// <summary>
        /// shortcut to entity.transform.position
        /// </summary>
        /// <value>The position.</value>
        public Vector2 Position
        {
            get => _wvTransform.WindowCenter;
            set => _wvTransform.WindowCenter = value;
        }


        //TOOD see how done in tool... the viewport point for wcs of zoom center must be same so 
        //cal vect and offset. after

        /// <summary>
        /// This will zoom aroud a new center in wcs. 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="newZoom"></param>
        public void ZoomCenter( Vector2 worldpos, float newZoom)
        {
  
            Vector2 viewportPos = _wvTransform.WorldToViewport(worldpos);

            // save current center
            Vector2 center = Position;

            // first, perform normal zooming. zoom limit applied here.
            Zoom = newZoom;//will update xforms


            // get the drift of center after zooming, in world coord
             Vector2 wp2 = _wvTransform.ViewportToWorld(viewportPos);
            Vector2 diff = wp2 - worldpos;

            // translate current center with the amount of drift correction
            Position = center - diff;

        }





        private void UpdateAutoZoomOnTracking(AABB aabb)
        {
            // moving to target zoom
            if (IsAutoZoomWTracking && IsFrameEnemies )
            {
                Vector2 aabbSize = new Vector2(aabb.Width, aabb.Height) * _trackWindowFactor;

                Vector2 originalWindowSize;

                originalWindowSize = _wvTransform.ViewportSize;
               
                Vector2 targetWindowSize = originalWindowSize * GeomUtility.GetScaleToEnclose(originalWindowSize, aabbSize);

                // this seems to cause issue with current cached bitmap, game will crawl very slow
                //TODO with CACHEDZOOMLEVELS retray maybe.. stil slow..
             //   _wvTransform.WindowSize = Vector2.SmoothStep(_wvTransform.WindowSize, targetWindowSize, _trackingZoomSpeed);
                // just enlarge if aabbSize is larger than world window size (scale > 1)
             
                {

                    float scaleAdjustment = 0.5f;    // don't zoom out too far
                    float scale = GeomUtility.GetScaleToEnclose(_wvTransform.WindowSize, aabbSize) * scaleAdjustment;
                    float tolerance = 0.1f;     // ignore small diff
                    if (scale > 1f + tolerance)
                    {
                        _wvTransform.WindowSize = targetWindowSize * scaleAdjustment;
 					//	_wvTransform._windowSize = targetWindowSize * scaleAdjustment;
                    }
                }
            }
            

        }


        private void UpdateAutoRotateOnTracking()
        {
            // moving to target rotation
            if (_isAutoRotateWTracking == true)
            {

                if (FollowedObjects.Count > 0)
                {
                    TrackingTargetRotation = -FollowedObjects[0].Rotation;  //TODO  figure out gravity direction if tracking a group, maybe do a gravity field
                }

                //this works but each has may have a  different  Up vector, the Yndrd would need an Offset if we track it like this
                _wvTransform.WindowRotation = TrackingTargetRotation;

             //   float curRotation = _wvTransform.WindowRotation;

                //this   doesnt work either
    //            _wvTransform.WindowRotation = GeomUtility.SmoothStep(
//    curRotation, _trackingTargetRotation, _trackingRotateSpeed);
      //          return;


                //this seems too complex, just use radians, we dont care 360
                // search the shortest side of rotation. remember that WindowRotation 
                // positive value goes clockwise, while positive value in our 
                // vector-to-degree goes counterclockwise.
         //       float max = 360 + _trackingTargetRotation;
         //       float curRotation = -_wvTransform.WindowRotation;
          //      if (curRotation < 0) curRotation += 360;

              //  float path1 = Math.Abs(curRotation - _trackingTargetRotation);
              //  float path2 = Math.Abs(max - curRotation);
             //   if (path1 <= path2)
             //   {
            //        _wvTransform.WindowRotation = -GeomUtility.SmoothStep(
           ///             curRotation, _trackingTargetRotation, _trackingRotateSpeed);
              //  }
          //      else
           //     {
           //         _wvTransform.WindowRotation = -GeomUtility.SmoothStep(
           //            curRotation, max, _trackingRotateSpeed);
           //     }
            }
        }


        /// <summary>
        /// Update the tracking camera which pans to prevent target moving off screen
        /// </summary>
        /// <param name="objAABB">AABB of tracked object.</param>
        /// <param name="targetPosition">World position of camera window. 
        /// When entering here it should be the same as aabb center of tracked object.</param>
        private void UpdateLazyTracking(AABB objAABB, float aabbVelocity, ref Vector2 targetPosition)
        {
            if (!_isLazyTracking)  //CODE REVIEW easier to read logic.. dont need to see second brace.
                return;

            Vector2 currentObjPos = targetPosition;

            float screenThresholdLeft = _wvTransform.WindowSize.X * MarginLeft;
            float screenThresholdRight = _wvTransform.WindowSize.X * MarginRight;

            float screenThresholdTop = _wvTransform.WindowSize.Y * MarginTop;
            float screenThresholdBottom = _wvTransform.WindowSize.Y * MarginBottom;


            float winSizeX = _wvTransform.WindowSize.X;
            float winHalfSizeX = winSizeX * 0.5f;
            float winSizeY = _wvTransform.WindowSize.Y;
            float winHalfSizeY = winSizeY * 0.5f;

            float totalMarginWidth = (screenThresholdLeft + screenThresholdRight);
            float totalMarginHeight = (screenThresholdTop + screenThresholdBottom);


            // first, object aabb size must be smaller than screen size + threshold size
            // when object aabb size is larger than screen world size, will use normal tracking. 
            if (objAABB.Width > (winSizeX - totalMarginWidth) || objAABB.Height > (winSizeY - totalMarginHeight))
            {
                return;
            }


            // note: experimental code. good on fast moving object & on sudden stop.
            // but still looks bad if object moving slow, shaking because often switch between normal & lazy tracking.
            // might be revisited again later.

            //// object must cover no more than 1/5 screen per frame.
            //float actualCameraFps = 1000f / _deltaTick;
            //float coveredDistancePerFrame = aabbVelocity * actualCameraFps;
            //float maxCoveredDistancePerFrame = _wvTransform.WindowSize.Length() * 0.2f;
            //if (coveredDistancePerFrame > maxCoveredDistancePerFrame)
            //{
            //    return;
            //}


            // screen boundary world position
            float minX = _wvTransform.WindowCenter.X - winHalfSizeX;
            float maxX = _wvTransform.WindowCenter.X + winHalfSizeX;
            float minY = _wvTransform.WindowCenter.Y - winHalfSizeY;
            float maxY = _wvTransform.WindowCenter.Y + winHalfSizeY;

            // assume min object size for needCentering is 1/5 of screen width or height
            // needCentering is to reduce bouncing back & forth between screen while keep "lazy" camera. 
            // so when large object switching to new screen, place object at screen center instead of edge.
            bool needCenteringX = (objAABB.Width / _wvTransform.WindowSize.X) > 0.2f;
            bool needCenteringY = (objAABB.Height / _wvTransform.WindowSize.Y) > 0.2f;

            // object smaller than 1/5 screen w/h will be placed on edge, add more distance from screen edge.
            float objWidth2 = objAABB.Width * 2;
            float objHeight2 = objAABB.Height * 2;


            // TODO: switching to another center position in lazy tracking also need some relax period,
            // to prevent zig-zag camera, for example, switching screen below followed immediately by switching screen left.
            // in general, don't switch screen center too often in short period because it will make viewer dizzy.


            // check if object pass screen edge, move camera accordingly.
            if (objAABB.LowerBound.X < (minX + screenThresholdLeft))
            {
                if (needCenteringX)
                {
                    // just center screen on object when switching to new screen
                    _curLazyTrackingScreenCenter.X = currentObjPos.X;
                }
                else
                {
                    // center camera to left page screen center, but shift right a bit to include object + margin.
                    // the result object will be located on right edge on new screen.
                    _curLazyTrackingScreenCenter.X = (minX - winHalfSizeX) + objWidth2 + totalMarginWidth;
                }
                _lazyTrackingOnMove = true;
            }
            else if (objAABB.UpperBound.X > (maxX - screenThresholdRight))
            {
                if (needCenteringX)
                {
                    _curLazyTrackingScreenCenter.X = currentObjPos.X;
                }
                else
                {
                    // center camera to right page screen center, but shift left a bit to include object + margin.
                    _curLazyTrackingScreenCenter.X = (maxX + winHalfSizeX) - objWidth2 - totalMarginWidth;
                }
                _lazyTrackingOnMove = true;
            }

            if (objAABB.LowerBound.Y < (minY + screenThresholdTop))
            {
                if (needCenteringY)
                {
                    _curLazyTrackingScreenCenter.Y = currentObjPos.Y;
                }
                else
                {
                    _curLazyTrackingScreenCenter.Y = (minY - winHalfSizeY) + objHeight2 + totalMarginHeight;
                }
                _lazyTrackingOnMove = true;
            }
            else if (objAABB.UpperBound.Y > (maxY - screenThresholdBottom))
            {
                if (needCenteringY)
                {
                    _curLazyTrackingScreenCenter.Y = currentObjPos.Y;
                }
                else
                {
                    _curLazyTrackingScreenCenter.Y = (maxY + winHalfSizeY) - objHeight2 - totalMarginHeight;
                }
                _lazyTrackingOnMove = true;
            }


            // don't reset _lazyTrackingOnMove, value from previous loop might still true.
            if (_lazyTrackingOnMove == true)
            {
                targetPosition = _curLazyTrackingScreenCenter;
            }
            // if didn't pass any screen edge, there should be no change to screen center.
            else
            {
                targetPosition = _wvTransform.WindowCenter;
            }


            _prevLazyTracking = true;
            _lastLazyTrackingTick = _prevTick;
        }


        private void OnFollowedObjects_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Reset:

                    // when any of followed object contents are changed, always clear chached AABB
                 
                    break;
            }
        }


        /// <summary>
        /// Helper method to follow single entity.
        /// This will clear FollowedObjects collection and add entity if it's not null.
        /// </summary>
        public void FollowSingleEntity(IEntity entity)
        {
            FollowedObjects.Clear();
            if (entity != null)
            {
                FollowedObjects.Add(entity);
            }
        }


        // dont forget call this when changing level
        public void ResetLazyTrackingCenter()
        {
            _curLazyTrackingScreenCenter = _wvTransform.WindowCenter;
        }


        public AABB Bounds
        {
            get
            {
                return Transform.GetWorldWindowAABB();

            }
        }




        /// <summary>
        /// Enable or disable WindowLimit.
        /// </summary>
        public bool LimitEnabled
        {
            get => Transform.LimitEnabled;
            set => Transform.LimitEnabled = value;
        }

        /// <summary>
        /// This will limit window placement in world, effectively limit zoom, pan, and rotate
        /// to a specific area. Can be used to prevent camera showing outside level boundary.
        /// </summary>
        public AABB WindowLimit
        {
            get => Transform.WindowLimit;
            set => Transform.WindowLimit = value;
        }


        /// <summary>
        /// Stop tracking FollowedObjects and lazy zooming so we can set the Postion and Zoom directly on the Camera using Transfrom
        ///    Note  not sure if these calls are all needed but its enough to make it stop tracking ActiveSpirit, next key zoom in our our reinstates it all
        /// </summary>
        public void StopCameraFollowing()
        {
           CameraInput = CameraInput.None;
           IsAutoZoomWTracking = false;
           IsCameraTrackingEnabled = false;
           FollowedObjects.Clear();
        }
        #region Properties


        /// <summary>
        /// For lazy tracking , when target reaches this margin will pan.   Margin is a fraction of the respective width or height,
        /// </summary>
        public float MarginTop { get; set; }
        public float MarginLeft { get; set; }
        public float MarginRight { get; set; }
        public float MarginBottom { get; set; }


        public WorldViewportTransform Transform
        {
            get { return _wvTransform; }
        }


        public CameraMode CameraMode
        {
            get { return _cameraMode; }
        }


        // value type, not reference
        public CameraInput CameraInput
        {
            get { return _cameraInput; }
            set { _cameraInput = value; }
        }


        public float PanSpeed
        {
            get { return _panSpeed; }
            set { _panSpeed = value; }
        }

        public float RotateSpeed
        {
            get { return _rotateSpeed; }
            set { _rotateSpeed = value; }
        }

        public float ZoomSpeed
        {
            get { return _zoomSpeed; }
            set { _zoomSpeed = value; }
        }


        /// <summary>
        /// Determine if camera tracking allowed.   This allows camera to autopan or zoom with a list of Spirts combined AABB .
        /// See also IsAutoZoomWTracking
        /// </summary>
        public bool IsCameraTrackingEnabled
        {
            get { return _isCameraTrackingEnabled; }
            set
            {
                _isCameraTrackingEnabled = value;
                NotifyPropertyChanged("IsCameraTrackingEnabled");
            }
        }

        /// <summary>
        /// Add entities to be followed by camera in this collection.
        /// Can follow single or group of entities.
        /// To take effect, condition must be FollowedObjects.Count > 0 and IsCameraTrackingEnabled == TRUE.
        /// </summary>
        public EntityCollection FollowedObjects
        {
            get { return _followedObjects; }
        }

        /// <summary>
        /// Get/set whether ignore camera input when tracking object.
        /// </summary>
        public bool IsIgnoreInputWTracking
        {
            get { return _isIgnoreInputWTracking; }
            set { _isIgnoreInputWTracking = value; }
        }

        /// <summary>
        /// Get/set whether automatically zoom to TrackWindowSize when tracking object.
        /// </summary>
        public bool IsAutoZoomWTracking
        {
            get { return _isAutoZoomWTracking; }
            set { _isAutoZoomWTracking = value; }
        }

        /// <summary>
        /// Get/set whether automatically rotate to target rotation when tracking object.
        /// </summary>
        public bool IsAutoRotateWTracking
        {
            get { return _isAutoRotateWTracking; }
            set { _isAutoRotateWTracking = value; }
        }

        /// <summary>
        /// Speed of auto pan when tracking object. Larger value will track more faster.
        /// </summary>
        public float TrackingPanSpeed
        {
            get { return _trackingPanSpeed; }
            set { _trackingPanSpeed = value; }
        }

        /// <summary>
        /// Speed of auto rotate when tracking object.
        /// </summary>
        public float TrackingRotateSpeed
        {
            get { return _trackingRotateSpeed; }
            set { _trackingRotateSpeed = value; }
        }

        /// <summary>
        /// Speed of auto zoom when tracking object.
        /// </summary>
        public float TrackingZoomSpeed
        {
            get { return _trackingZoomSpeed; }
            set { _trackingZoomSpeed = value; }
        }

        /// <summary>
        /// Set window size when tracking object, as a multiply factor of 
        /// object size. Only affect object that have AABB.
        /// </summary>
        public float TrackWindowFactor
        {
            get { return _trackWindowFactor; }
            set { _trackWindowFactor = value; }
        }

        /// <summary>
        /// If this true, camera will store object AABB size once after it's set. 
        /// After that camera will always use the stored size, not from object AABB.
        /// This will help to reduce continuous zoom in/out when object is moving.
        /// Please note that if object size is changed significantly, this setting
        /// should be disabled and enabled again to update stored AABB.
        /// </summary>
        public bool IsKeepObjectAABBFixed
        {
            get { return _isKeepObjectAABBFixed; }
            set { _isKeepObjectAABBFixed = value; }
        }

        /// <summary>
        /// Camera will try to align its rotation to this angle when tracking object.
        /// Usually converted from Up vector of an object.
        /// </summary>
        public float TrackingTargetRotation
        {
            get { return _trackingTargetRotation; }
            set
            {
                // always save as positive value between 0 & 360
             //   float angle = value % 360;
           //     if (angle < 0) angle += 360;

                _trackingTargetRotation = value;
            }
        }

        /// <summary>
        /// If true, tracking will only move (pan) when object AABB touch the screen edge.
        /// Screen movement depends on which screen edge is touched by object AABB.
        /// To work proper object AABB size must be smaller than screen size.
        /// </summary>
        public bool IsLazyTracking
        {
            get { return _isLazyTracking; }
            set
            {
                _isLazyTracking = value;
                if (_isLazyTracking == true)
                {
                    ResetLazyTrackingCenter();

                    // TODO: lazy tracking currently can't work proper with cached aabb size, 
                    // need to fix cached aabb mechanism later
                    _isKeepObjectAABBFixed = false;
                }
            }
        }


        /// <summary>
        /// Include attacking Enemies of active spirit inside Camera.  Default is TRUE.
        /// Set and used by external module.  Didn't affect camera code internally.
        /// </summary>
        public bool IsFrameEnemies { get; set; }


        /// <summary>
        /// Enemy with X distance above this value will not be included in camera frame.
        /// </summary>
        public float MaxFrameObjectDistX { get; set; }


        /// <summary>
        /// Enemy with Y distance above this value will not be included in camera frame.
        /// </summary>
        public float MaxFrameObjectDistY { get; set; }


        public void ZoomWindow(AABB windowWCS)
        {
            _wvTransform.SetWorldWindow(ShapeUtility.AABBToRect(windowWCS));
    
        }


        #endregion
    }



    public enum CameraMode
    {
        OnIdle = 0,
        OnInput,
        OnTracking,
    }



    public enum CameraInput
    {
        None = 0,
        PanLeft,
        PanRight,
        PanUp,
        PanDown,
        ZoomIn,
        ZoomOut,
        RotateClockwise,
        RotateCounterClockwise,
        Mouse,
    }

}
