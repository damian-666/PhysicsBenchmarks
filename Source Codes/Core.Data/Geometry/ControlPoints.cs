using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Windows;

#if !(MONOTOUCH || UNIVERSAL|| NETSTANDARD2_0)
using System.Windows.Media;
#endif


using System.Collections.ObjectModel;

using Core.Data.Collections;

using FarseerPhysics.Common;

using UndoRedoFramework;
using Farseer.Xna.Framework;

namespace Core.Data.Geometry
{

    /// <summary>
    /// A Control point , you might use your own model points like the vertices in Body geometry. 
    /// </summary>
    /// we cannot derive from Point it is a sealed struct
    /// TODO  RESEARCH item  is there a standard listenable Point   
    /// 

 // TODO REVIEW FUTURE
    //Control points is in World coordinates.. it should probably be changed to Body local... as soon as body is created.. 

    [DataContract]
    public class ControlPoint : INotifyPropertyChanged
    {

		public ControlPoint( )
		{
            _x.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_x_UndoRedoChanged);
            _y.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_y_UndoRedoChanged);
		}

		public ControlPoint(double x, double y)
		{
			this.X = x;
			this.Y = y;
            _x.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_x_UndoRedoChanged);
            _y.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_y_UndoRedoChanged);
		}
      
        public ControlPoint(ControlPoint pt)
        {
            this.X = pt.X;
            this.Y = pt.Y;
            _x.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_x_UndoRedoChanged);
            _y.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_y_UndoRedoChanged);
        }

#if !(UNIVERSAL || NETSTANDARD2_0)
        public ControlPoint(Point pt)
        {
            this.X = pt.X;
            this.Y = pt.Y;
            _x.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_x_UndoRedoChanged);
            _y.UndoRedoChanged += new NotifyUndoRedoMemberChangedEventHandler<double>(_y_UndoRedoChanged);
        }
#endif

        void _y_UndoRedoChanged(object sender, UndoRedoChangedType type, double oldState, double newState)
        {
            if (type != UndoRedoChangedType.CommitChanged)
            {
                NotifyPropertyChanged("Y");
            }
        }

        void _x_UndoRedoChanged(object sender, UndoRedoChangedType type, double oldState, double newState)
        {
            if (type != UndoRedoChangedType.CommitChanged)
            {
                NotifyPropertyChanged("X");
            }
        }

        bool _locked = false;
        public bool Locked
        {
            get { return _locked; }
            set { _locked = value; }
        }

		UndoRedo<double> _x = new UndoRedo<double>();
		UndoRedo<double> _y = new UndoRedo<double>();

#if !(UNIVERSAL || NETSTANDARD2_0)
        public Point Point
        {
            get { return new Point(_x.Value, _y.Value); }
            set 
            {
                X = value.X;
                Y = value.Y;
            }
        }

        public Point PointUndoable
        {
            get { return new Point(_x.Value, _y.Value); }
            set
            {
                XUndoable = value.X;
                YUndoable = value.Y;
            }
        }
#endif

        /// <summary>
        /// The collection owner of this control point
        /// This is usefull for generic selection tool, where it doesn't know which collection it reside
        /// </summary>
        public object Owner { get; set; }

        object _tag = null;
        public object Tag
        {
            get { return _tag; }
            set { _tag = value; }
        }


        //TODO future.. hover over a vertex to get its index..  then plugin can whip up a special convex fixture
        //to bullet for prevent inner collisions between triangles
        /// <summary>
        /// Index of this vertex in the bodies General Vertex collection
        /// </summary>
        public int GeneralVertexIndex { get; set; }

        public Vector2 Vector2
        {
            get { return new Vector2((float)X, (float)Y); }
        }

        [DataMember]
        public double X
        {
            get { return _x.Value; }
            set
            {
                _x.Value = value;
                NotifyPropertyChanged("X");
            }
        }

        public double XUndoable
        {
            get { return _x.ValueUndoable; }
            set
            {
                _x.ValueUndoable = value;
                NotifyPropertyChanged("X");
            }
        }

        [DataMember]
        public double Y
        {
            get { return _y.Value; }
            set
            {
                _y.Value = value;
                NotifyPropertyChanged("Y");
            }
        }

        public double YUndoable
        {
            get { return _y.ValueUndoable; }
            set
            {
                _y.ValueUndoable = value;
                NotifyPropertyChanged("Y");
            }
        }

        public ControlPoint Clone()
        {
            return new ControlPoint(this);
        }


#if !(UNIVERSAL || NETSTANDARD2_0)
        public static implicit operator Point(ControlPoint f)
        {
            return new Point(f.X, f.Y);
        }
#endif

        public static implicit operator Vector2(ControlPoint f)
        {
            return new Vector2((float)f.X, (float)f.Y);
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
#if MONOTOUCH || UNIVERSAL || NETSTANDARD2_0
                System.Diagnostics.Debug.WriteLine(ex.Message);
				System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#else
                System.Diagnostics.Trace.TraceError(ex.Message);
                System.Diagnostics.Trace.TraceError(ex.StackTrace);
#endif
            }
        }

        private Vector2 _vectorTag;
        public void SetVectorTag(ref Vector2 vector)
        {
            _vectorTag = vector;
        }

        public Vector2 VectorTag()
        {
            return _vectorTag; 
        }
	};



    [DataContract]
    public class ControlPointCollection : ObservableCollectionUndoable<ControlPoint>
    {
        public ControlPointCollection()
            : base(new List<ControlPoint>())
        {
        }
    }
 
    public class SelectionIndexCollection : ObservableCollection<int>
    {
    }

}


