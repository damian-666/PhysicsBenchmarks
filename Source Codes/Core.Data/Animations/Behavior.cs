using System;
using System.Collections.Generic;
using System.Runtime.Serialization;



using Core.Data.Collections;
using Core.Data.Interfaces;
using Core.Data.Input;


namespace Core.Data.Animations
{
    /// <summary>
    /// An ordered list of Keyframes, sorted by time. NOTE this would  better be named Sequence, this thing is a Sequencer, if a musical instrument.. TODO future (only if tool were to be wide release).. rename Behavior to sequence at least in places where it not serialized, just as instances like ActiveSequence,  but behavior collection, might make it difficult.. .. this is possible leaving Name = "Behavior" along, i beleive, but the   because Behavior implies too much and its a reserved word in a space, i think in c# for new UI.. 
    /// </summary>
    [DataContract(Name = "Behavior", Namespace = "http://ShadowPlay")]
    public class Behavior : NotifyPropertyBase
    {
        KeyframeCollection _keyframes;
        String _strName;
        GameKey _key;
        bool _firstTimeExec;
        double _timeDilateFactor = 1.0;
        double _maxTimeLine = 3;

        public static Behavior Default
        {
            get { return new Behavior("Default"); }
        }

        public Behavior()
        {
            _strName = "";
            _key = GameKey.None;
            _keyframes = new KeyframeCollection();
        }

        public Behavior(string name)
        {
            _strName = name;
            _key = GameKey.None;
            _keyframes = new KeyframeCollection();
        }

        public Behavior(string name, GameKey key)
        {
            _strName = name;
            _key = key;
            _keyframes = new KeyframeCollection();
        }

        public Behavior(Behavior behavior)
        {
            CopyFrom(behavior);
        }

        public void CopyFrom(Behavior behavior)
        {
            this.Name = behavior.Name;
            this.GKey = behavior.GKey;

            _keyframes = new KeyframeCollection();
            foreach (Keyframe keyframe in behavior.Keyframes)
            {
                Keyframe newKeyframe = new Keyframe(keyframe);
                _keyframes.Add(newKeyframe);
            }
        }

        /// <summary>
        /// Delete specific bone index from this behavior. Comes as a result
        /// from deleting a specific body parts.
        /// </summary>
        public void DeleteJointIndex(int index)
        {
            foreach (Keyframe k in _keyframes)
            {
                if (k.Angles.Contains(index))
                {
                    k.Angles.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Set the number of joints  for all existing behavior keyframes. Truncate if higher
        /// </summary>
        public void ValidateJointCount(int num)
        {
                                  
            int deln;
            foreach (Keyframe k in _keyframes)
            {
              //  while (k.Angles.Count < num)
               // {
                 //   k.Angles.Add(0);    adding a placeholder across causes stuf to fly to zero, no longer needed since there is check in interpolate
               // }

     
                if (k.Angles.Count > num)
                {
                    deln = k.Angles.Count - num;

                    if (num == 0)//its resetting all the jiont, wipe all, avoid -1 index.
                        k.Angles.RemoveRange(0, deln);
                    else                             
                        k.Angles.RemoveRange(num - 1, deln);
                }
            }
        }

        /// <summary>
        /// Game key to activate this behavior. Default is GameKey.None .
        /// Renamed from Key to GKey to solve deserialization exception when loading old level.
        /// </summary>
        [DataMember]
        public GameKey GKey
        {
            get { return _key; }
            set
            {
                if (_key != value)
                {
                    _key = value;
                    NotifyPropertyChanged("Key");
                }
            }
        }

        [DataMember]
        public String Name
        {
            get { return _strName; }
            set
            {
                if (_strName != value)
                {
                    _strName = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }

        [DataMember]
        public KeyframeCollection Keyframes
        {
            get { return _keyframes; }
            set
            {
                if (_keyframes != value)
                {
                    _keyframes = value;// required for serialization in silverlight wont be set
                }
            }
        }

        [DataMember]
        public double TimeDilateFactor
        {
            get { return _timeDilateFactor; }
            set
            {
                if (_timeDilateFactor != value)
                {
                    _timeDilateFactor = value;
                    NotifyPropertyChanged("TimeDilateFactor");
                }
            }
        }

        [DataMember]
        public double MaxTimeLine
        {
            get { return _maxTimeLine; }
            set
            {
                if (_maxTimeLine != value)
                {
                    _maxTimeLine = value;
                    NotifyPropertyChanged("MaxTimeLine");
                }
            }
        }

        [DataMember]
        public bool FirstTimeExec
        {
            get { return _firstTimeExec; }
            set
            {
                if (_firstTimeExec != value)
                {
                    _firstTimeExec = value;
                    NotifyPropertyChanged("FirstTimeExec");
                }
            }
        }



        public Keyframe this[int index]
        {
            get { return _keyframes[index]; }
        }

    }
}
