using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

using FarseerPhysics.Dynamics.Joints;
using Core.Data.Interfaces;
using UndoRedoFramework;


namespace Core.Data.Animations
{

    [DataContract(Name = "Keyframe", Namespace = "http://www.shadowplay.com/classes/")]
    public class Keyframe : NotifyPropertyBase
    {
        protected UndoRedo<double> _time = new UndoRedo<double>();
        protected List<float> _angles = new List<float>();

        public Keyframe()
        {
            _time.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_time_UndoRedoChanged);
        }

        public Keyframe(int numAngles)
        {
            _time.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_time_UndoRedoChanged);

            Allocate(numAngles);
        }

        public Keyframe(Keyframe keyframe)
        {
            CopyFrom(keyframe);
            _time.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_time_UndoRedoChanged);
        }

        public void CopyFrom(Keyframe keyframe)
        {
            Clear();

            foreach (float angle in keyframe.Angles)
            {
                _angles.Add(angle);
            }

            Time = keyframe.Time;
        }

        public void Allocate(int numAngles)
        {
            for (int i = 0; i < numAngles; i++)
            {
                _angles.Add(0);
            }
        }

  
        private void _time_UndoRedoChanged(object sender, UndoRedoChangedType type, double oldState, double newState)
        {
            NotifyPropertyChanged("Time");
        }

        public void Clear()
        {
            _angles.Clear();
            _time.Value = 0;
        }

        [DataMember]
        public double Time
        {
            get
            {
                return _time.Value;
            }


            set
            {
              
                // because we only serialize Value, _time might be null when deserialized
                
                //TODO CODE REVIEW, could we make  UndoRedo<double> serializable class?

                if (_time == null)
                {
                    _time = new UndoRedo<double>();
                    _time.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_time_UndoRedoChanged);
                }

                //TODO CODE REVIEW  shouldnt we check if value already == value first, then dont notify or set if already equal

                //TODO CODE REVIEW ,  this is not very clear what its for..

                if (UndoRedoManager.IsMemberOf("Time"))
                {
                    _time.ValueUndoable = value;
                }
                else
                {
                    _time.Value = value;
                }

                NotifyPropertyChanged("Time");
            }
        }

        [DataMember]
        public List<float> Angles
        {
            get { return _angles; }
            set { _angles = value; }    // for deserialization only, do not access.
        }

        public string Name
        {
            get { return string.Format("Keyframe {0:F}", Time); }
        }
    }
}
