using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;

using UndoRedoFramework;
using UndoRedoFramework.Collections.Generic;

using Core.Data.Interfaces;

namespace Core.Data.Collections
{
    /// <summary>
    /// Undoable ObservableCollectionS  TODO performance... doing a  brute force search.. 
    /// </summary>
    /// <typeparam name="T">Type of Collection</typeparam>

	[CollectionDataContract]
	public class ObservableCollectionUndoable<T> : ObservableCollectionS<T>,
        IUndoRedoMember,
        INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Property Changed

        public event NotifyUndoRedoCollectionChangedEventHandler<T> UndoRedoChanged;


        #endregion


        #region Ctor

        public ObservableCollectionUndoable()
            : base(new List<T>())
        {
        }

        // wrap a sortable list with this collection, make it
        // observable, since Observable collection can't be directly sorted and searched
        public ObservableCollectionUndoable(List<T> set)
            : base(set)
        {
        }

        #endregion


        #region IUndoRedoMember Members


 
        void IUndoRedoMember.OnCommit(object change)
        {
            Debug.Assert(change != null);
            ((Change<List<T>>)change).NewState = _list;

            if (UndoRedoChanged != null)
                UndoRedoChanged(this, UndoRedoChangedType.CommitChanged, _list, ((Change<List<T>>)change).NewState);
        }

        void IUndoRedoMember.OnUndo(object change)
        {
            Debug.Assert(change != null);
            _list = ((Change<List<T>>)change).OldState;

            List<T> newState = ((Change<List<T>>)change).NewState;

            NotifyCollectionChangedEventArgs e;
            if (newState.Count > _list.Count)
            {
                List<T> objs = new List<T>();
                foreach (T item in newState)
                {

                    //TODO   OPTIMIZE  OPTIMIZATION.. right now its fast enough even with large bodies, slowless can come from the prop sheet or logger   compare this with the implementation in UndoRedoList.. move this there in UndoRedoFramework
                    if (_list.Contains(item) == false)
                    {
                        objs.Add(item);
                    }
                }

                foreach (T item in objs)
                {

                    e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, -1);

                    this.NotifyCollectionChanged(e);
                }
            }
            else if (newState.Count < _list.Count)
            {
                List<T> objs = new List<T>();
                foreach (T item in _list)
                {
                    if (newState.Contains(item) == false)
                    {
                        objs.Add(item);
                    }
                }

                foreach (T item in objs)
                {

                    e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, -1);

                    this.NotifyCollectionChanged(e);
                }
            }

            if (UndoRedoChanged != null)
                UndoRedoChanged(this, UndoRedoChangedType.UndoChanged, _list, ((Change<List<T>>)change).NewState);
        }

        void IUndoRedoMember.OnRedo(object change)
        {
            Debug.Assert(change != null);
            _list = ((Change<List<T>>)change).NewState;

            List<T> oldState = ((Change<List<T>>)change).OldState;

            NotifyCollectionChangedEventArgs e;
            if (oldState.Count < _list.Count)
            {
                List<T> objs = new List<T>();
                foreach (T item in _list)
                {
                    if (oldState.Contains(item) == false)
                    {
                        objs.Add(item);
                    }
                }

                foreach (T item in objs)
                {

                    e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, -1);

                    this.NotifyCollectionChanged(e);
                }
            }
            else if (oldState.Count > _list.Count)
            {
                List<T> objs = new List<T>();
                foreach (T item in oldState)
                {
                    if (_list.Contains(item) == false)  //TODO OPTIMIZE
                    {
                        objs.Add(item);
                    }
                }

                foreach (T item in objs)
                {

                    e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, -1);

                    this.NotifyCollectionChanged(e);
                }
            }

            if (UndoRedoChanged != null)
                UndoRedoChanged(this, UndoRedoChangedType.RedoChanged, ((Change<List<T>>)change).OldState, _list);
        }


        #endregion


        #region List

        private bool _enableUndoRedoFeature = true;
        public bool EnableUndoRedoFeature
        {
            get { return _enableUndoRedoFeature; }
            set { _enableUndoRedoFeature = value; }
        }

        protected override void Enlist()
        {
            Enlist(true);
        }

        protected override void Enlist(bool copyItems)
        {
            if (!EnableUndoRedoFeature) 
                return;

            if (UndoRedoManager.CurrentCommand != null)
                if (!UndoRedoManager.CurrentCommand.ContainsKey(this))
                {
                    Change<List<T>> change = new Change<List<T>>();
                    change.OldState = _list;
                    if (change.NewState == null) change.NewState = new List<T>();
                    UndoRedoManager.CurrentCommand[this] = change;
                    if (copyItems)
                    {
                        _list = new List<T>(_list);
                    }
                    else
                        _list = new List<T>();
                }
          
        }

        [OnDeserialized]
        public new void OnDeserialized(StreamingContext sc)
        {
            EnableUndoRedoFeature = true;
        }

        #endregion
    }
}
