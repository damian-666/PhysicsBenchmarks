using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Factories;
using FarseerPhysics.Common;
using System.Threading.Tasks;

namespace FarseerPhysics.Dynamics.Particles
{
    /// <summary>
    /// Emitter base class for All emitter types
    /// Define your Emitter type as KnownType metadata here
    /// </summary>
    [DataContract]
    [KnownType(typeof(BodyEmitter))]
    [KnownType(typeof(LaserEmitter))]
    abstract public class Emitter : ReferencePoint
    {
        #region MemVars & Props

        /// <summary>
        /// Active means  applying  reaction force and emitting particles 
        /// </summary>
      
        private bool _active = false;

        [DataMember]
        virtual public  bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                FirePropertyChanged();
            }
        }



        private bool visible = true;

        /// <summary>
        /// If true system will draw the emttied items at its emitters place.  Not might just replace UseEmittedBodyAsView wiht this, its more standard.
        /// </summary>
        virtual public bool IsVisible
        {
            get { return visible; }
            set
            {
                visible = value;
                FirePropertyChanged();
            }
        }

        [DataMember]
        public Vector2 Offset { get; set; }

        /// <summary>
        /// OffsetX in WCS , For setting in prop sheet, affects Offset property
        /// </summary>
        public float OffsetX
        {
            get { return Offset.X; }
            set
            {
                Offset = new Vector2(value, Offset.Y);
                NotifyPropertyChanged(nameof(Offset));
                NotifyPropertyChanged(nameof(OffsetX));
            }
        }
        /// <summary>
        /// OffsetY in WCS , For setting in prop sheet affects Offset property
        /// </summary>
        public float OffsetY
        {
            get { return Offset.Y; }
            set
            {
                Offset = new Vector2(Offset.X, value);
                NotifyPropertyChanged(nameof(Offset));
                NotifyPropertyChanged(nameof(OffsetY));
            }
        }



        private float deviationOffsetX;
        /// <summary>
        /// deviation of the emittion offset X, 1 means 100% possible, by the Offset amount. in either direction.. so += Offset /2
        /// </summary>

        [DataMember]
        public float DeviationOffsetX
        {
            get
            { return deviationOffsetX; }
            set
            {
                deviationOffsetX = value;
                NotifyPropertyChanged(nameof(DeviationOffsetX));
            }
        }

        private float deviationOffsetY;
        /// <summary>
        /// deviation of the emittion offset Y in WC, 1 means 100% possible, by the Offset amount. in either direction.. so += Offset /2
        /// </summary>
        [DataMember]
        public float DeviationOffsetY
        {
            get
            { return deviationOffsetY; }
            set
            {
                deviationOffsetY = value;
                NotifyPropertyChanged("DeviationOffsetY");
            }
        }

        /// <summary>
        /// deviation of the elipse particle width / height ,  means 100% change possible + or = 
        /// </summary>
        //   [DataMember]
        //   public float DeviationAspectYX { get; set; }

        /// <summary>
        ///  //for backwards file compatibility.   value will be passed to new base.Direction
        /// </summary>
        [DataMember]
        public Vector2 EmissionDirection { get; set; }

        /// <summary>
        /// specify radiation beam width in radians.. 
        /// </summary>
        [DataMember]
        public float DeviationAngle { get; set; }



        //TODO CODE REVIEW FUTURE this should be a percentage of SIZE.. now it can be set wrong , its an absolute quanity
        /// <summary>
        /// To randomize size, will spawn a particale   Deviate from  specified particle size +- a value in this range
        /// </summary>
        [DataMember]
        public float DeviationSize { get; set; }

        public float _frequency;
        /// <summary>
        /// Frequency (in cycles per sec), default is 3
        /// </summary>
        [DataMember]
        public float Frequency
        {
            get
            { return _frequency; }
            set
            {
                value = Math.Abs(value);
                _frequency = value;
                _currentFrequency = value;

                if (_currentFrequency == 0)
                {
                    _currentPeriod = 0;
                    return;
                }

                _currentPeriod = 1.0f / value;
            }
        }


        /// <summary>
        /// Index in the paret list.. sometimes user for emit order
        /// </summary>
        public int ListIndex
        {
            get
            {
                return Parent.EmitterPoints.IndexOf(this);
            }
        }



        /// <summary>
        ///This is a factor, will always be added to the frequency..
        /// </summary>
        [DataMember]
        public float FrequencyDeviation { get; set; }

        protected float _currentFrequency;
        protected float _currentPeriod;
        protected float _currentProbabiltyCollidable;


        //public float _phase;
        ///// <summary>
        ///// Fraction of the Period 0 to 1.    so .5 means 90 out of phase.. 
        ///// </summary>
        //[DataMember]
        //public float Phase
        //{
        //    get
        //    { return _phase; }
        //    set
        //    {
        //        _phase = value;
        // //       _currentphase = value;
        //    }
        //}


        internal BodyColor _color=new BodyColor(255, 0, 0, 255);
        /// <summary>
        /// Emitted particle color or laser color.
        /// </summary>
        [DataMember]
        public BodyColor Color
        {
            get   {   return _color;    }
            set
            {
                _color = value;
                NotifyPropertyChanged(nameof(Color));
            }
        }

        [DataMember]
        public float EdgeStrokeThickness { get; set; }

        [DataMember]
        public BodyColor EdgeStrokeColor { get; set; }

        /// <summary>
        /// To prevent duplicate emitter, because emitter now fully serialized.
        /// </summary>
        [DataMember]
        public string Name { get; set; }


        #endregion


        #region Ctor

        public Emitter(Body parent, Vector2 localPos) :
            base(parent, localPos)
        {
            Offset = Vector2.Zero;
            Direction = Vector2.UnitY;
        }

        #endregion


        #region Methods

        public abstract void Update(double dt);
      

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            if (Direction == Vector2.Zero)
            {  //for old files..
                Direction = EmissionDirection;
            }
        }


        #endregion
    }
}
