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
    ///  ObservableCollectionS is Serializable
    /// This is to workaround the .net issue  with
    /// ObservableCollection serializing listeners and not portable from one
    /// machine to another   
    /// Behaves like standard ObservableCollection
    ///Changed ancestor from ReadOnlyCollection<T> into IList<>, ICollection<>, IEnumerable<>, IList, ICollection, IEnumerable by Suhendra
    ///The changes are crucial to make it UndoRedo capable, since the internal List  must always updated by IUndoRedoMember
    /// </summary>
    /// <typeparam name="T"></typeparam>
    //TODO UNIVERSAL  future .. was there and INAMEDCLONABLE .. now its all handled in the view model or tool.. all the renaming.. is only for behaviors collection of spirits.... code could be generalized and moved here..  copy of ,  copy2 of copy of blah 
    [CollectionDataContract]
    public class ObservableCollectionS<T> : IList<T>, ICollection<T>, IEnumerable<T>, IList, ICollection, IEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Property Changed

        private bool _enableEvents = true;
        public bool EnableEvents
        {
            get { return _enableEvents; }
            set { _enableEvents = value; }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public void NotifyCollectionChanged(NotifyCollectionChangedAction action, IList obj)
        {

            NotifyCollectionChangedEventArgs e = new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, obj, -1);

            if (CollectionChanged != null && EnableEvents)
                CollectionChanged(this, e);
        }

        public void NotifyCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null && EnableEvents)
                CollectionChanged(this, e);
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext sc)
        {
            EnableEvents = true;
        }

        #endregion


  
  
        [DataMemberAttribute(Name = "List")]
        protected List<T> _list;


        #region Ctor


        public ObservableCollectionS()
            : base()
        {
            _list = new List<T>();
        }

        // wrap a sortable list with this collection, make it
        // observable, since Observable collection can't be directly sorted and searched
        public ObservableCollectionS(List<T> set)
            : base()
        {
            _list = set;
        }

        #endregion


        
        #region List

        protected virtual void Enlist()
        {
        }

        protected virtual void Enlist(bool state)
        {
        }


        #endregion


        #region General Methods

        public int Capacity
        {
            get { return _list.Capacity; }
            set { _list.Capacity = value; }
        }

        public int Count
        {
            get { return _list.Count; }
        }

        public T this[int index]
        {
            get { return _list[index]; }
            set
            {
                Enlist();
                _list[index] = value;
            }
        }

        //the only way to add to this collection
        public void Add(T obj)
        {

            if (obj == null)
                throw new ArgumentException("adding null entity");

            NotifyCollectionChangedEventArgs e = new
                NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, obj, -1);

            Enlist();
            _list.Add(obj);

            NotifyCollectionChanged(e);
        }

        /// <summary>
        /// Force update the property change of all items, this is used when per item update doesn't work
        /// </summary>
        /// <param name="info">the member name to update</param>
        public void RefreshPropertyChanged(string info)
        {
            foreach (T prop in _list)
            {
                if (prop is NotifyPropertyBase)
                    (prop as NotifyPropertyBase).NotifyPropertyChanged(info);
            }
        }

        public void AddRange(IEnumerable<T> collection)
        {

            NotifyCollectionChangedEventArgs e = new
                NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, collection, -1);

            Enlist();
            _list.AddRange(collection);

            NotifyCollectionChanged(e);
        }

#if !(SILVERLIGHT || UNIVERSAL)
        public ReadOnlyCollection<T> AsReadOnly()
        {
            return _list.AsReadOnly();
        }
#endif

        public int BinarySearch(T item)
        {
            return _list.BinarySearch(item);
        }

        public int BinarySearch(T item, IComparer<T> comparer)
        {
            return _list.BinarySearch(item, comparer);
        }

        public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
        {
            return _list.BinarySearch(index, count, item, comparer);
        }

        public void Clear()
        {
            Enlist(false);
            _list.Clear();

            NotifyCollectionChangedEventArgs e = new
                NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

            NotifyCollectionChanged(e);
        }

        public void Insert(int index, T obj)
        {

            NotifyCollectionChangedEventArgs e = new
                NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, obj, index);

            Enlist();
            _list.Insert(index, obj);

            NotifyCollectionChanged(e);
        }

        public bool Remove(T obj)
        {
            Enlist();

            //int index = _list.IndexOf(obj);

            bool ret = _list.Remove(obj);

            // only notify & callback on successful removal
            //dh  //TODO FUTURE CLEANUP, HACK  I removed the return val check below.. in the case of eating body that is part of spirit,
            // its not in the Level entities collection, its in the spirts bodies collectinoonly.. but we  need to remove its body view  and the body from physics
            // since spirit  cannot access view, this is the easiest way  i dont see any harm  in it yet.. 
            //NOTE   TODO should probably start listening to spirits  entities or implement an explode type feature to break up spirit groups or remove one part of a spirit (body sysstem)

if (/*ret == true && */CollectionChanged != null)
            {
                NotifyCollectionChangedEventArgs e = new
                    NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, obj, -1);  //TODO FUTURE set the index but we dont use it evi

                NotifyCollectionChanged(e);
            }

            return ret;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index > _list.Count - 1) return;

            T obj = _list[index];

            Enlist();
            _list.RemoveAt(index);

            NotifyCollectionChangedEventArgs e = new
                NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, obj, index);

            NotifyCollectionChanged(e);
        }

        public void Sort(IComparer<T> comparer)
        {
            Enlist();
            _list.Sort(comparer);
        }

        public bool Contains(T obj)
        {
            return _list.Contains(obj);
        }


#if !(SILVERLIGHT || UNIVERSAL)
        public List<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        {
            return _list.ConvertAll<TOutput>(converter);
        }
#endif


        public void CopyTo(T[] array)
        {
            _list.CopyTo(array);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            _list.CopyTo(index, array, arrayIndex, count);
        }

#if !(SILVERLIGHT || UNIVERSAL) //  https://msdn.microsoft.com/en-us/library/6sh2ey19(v=VS.100).aspx   https://msdn.microsoft.com/en-us/library/6sh2ey19(v=VS.100).aspx
        // all these list.* method are not available in silverlight  or universal lists, TODO does an editor need these?

        public bool Exists(Predicate<T> match)
        {
            return _list.Exists(match);
        }

        public T Find(Predicate<T> match)
        {
            return _list.Find(match);
        }

        public List<T> FindAll(Predicate<T> match)
        {
            return _list.FindAll(match);
        }

        public int FindIndex(Predicate<T> match)
        {
            return _list.FindIndex(match);
        }

        public int FindIndex(int startIndex, Predicate<T> match)
        {
            return _list.FindIndex(startIndex, match);
        }

        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            return _list.FindIndex(startIndex, count, match);
        }

        public T FindLast(Predicate<T> match)
        {
            return _list.FindLast(match);
        }

        public int FindLastIndex(Predicate<T> match)
        {
            return _list.FindLastIndex(match);
        }

        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            return _list.FindLastIndex(startIndex, match);
        }

        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            return _list.FindLastIndex(startIndex, count, match);
        }
#endif

        public void ForEach(Action<T> action)
        {
            foreach (T x in _list) action(x); // even if action modifies the list, the changes will be caught by appropriate changing member
        }

        public virtual IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public List<T> GetRange(int index, int count)
        {
            return _list.GetRange(index, count);
        }

        public virtual int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public int IndexOf(T item, int index)
        {
            return _list.IndexOf(item, index);
        }

        public int IndexOf(T item, int index, int count)
        {
            return _list.IndexOf(item, index, count);
        }

        public int LastIndexOf(T item)
        {
            return _list.LastIndexOf(item);
        }

        public int LastIndexOf(T item, int index)
        {
            return _list.LastIndexOf(item, index);
        }

        public int LastIndexOf(T item, int index, int count)
        {
            return _list.LastIndexOf(item, index, count);
        }


#endregion


#region ICollection Members

        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return ((ICollection<T>)_list).IsReadOnly;
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_list).GetEnumerator();
        }

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)_list).CopyTo((T[])array, index);
        }

        int ICollection.Count
        {
            get { return _list.Count; }
        }

        bool ICollection.IsSynchronized
        {
            get { return ((ICollection)_list).IsSynchronized; }
        }

        object ICollection.SyncRoot
        {
            get { return ((ICollection)_list).SyncRoot; }
        }

#endregion


#region IList Members

        int IList.Add(object value)
        {
            Enlist();
            return ((IList)_list).Add((T)value);
        }

        void IList.Clear()
        {
            Enlist(false);
            ((IList)_list).Clear();
        }

        bool IList.Contains(object value)
        {
            return ((IList)_list).Contains((T)value);
        }

        int IList.IndexOf(object value)
        {
            return ((IList)_list).IndexOf((T)value);
        }

        void IList.Insert(int index, object value)
        {
            Enlist();
            ((IList)_list).Insert(index, (T)value);
        }

        bool IList.IsFixedSize
        {
            get { return ((IList)_list).IsFixedSize; }
        }

        bool IList.IsReadOnly
        {
            get { return ((IList)_list).IsReadOnly; }
        }

        void IList.Remove(object value)
        {
            Enlist();
            ((IList)_list).Remove((T)value);
        }

        void IList.RemoveAt(int index)
        {
            Enlist();
            _list.RemoveAt(index);
        }

        object IList.this[int index]
        {
            get
            {
                return _list[index];
            }
            set
            {
                Enlist();
                _list[index] = (T)value;
            }
        }

        #endregion


        #region Clone

        // ICloneable is not available in silverlight 
        // NOT USED        /*  todo if to generalize nameing etc .. see class header 

#if !(UNIVERSAL || SILVERLIGHT || NETSTANDARD2_0)
        
        public T CreateClone(T source)
        {
            INamedCloneable icloneable = source as INamedCloneable;

            T clone;

            if (icloneable != null)
            {
                clone = (T)icloneable.Clone();
            }
            else
            {
                clone = DataContractClone(source);
            }

            if (clone is INamedCloneable)
            {
                /*        todo maybe future.. if to generalize nameing etc .. see class header 
                //  clone.ParentCollection = null;
                string basename = (source as INamedCloneable).Name;
                (clone as .Name = "Copy of " + source.Name;
                int suffix = 2;
                string uniquename;

                while (this.Contains(clone.Name))
                {
                    uniquename = "Copy(" + suffix.ToString() + ") of " + basename;
                    suffix++;
                    clone.Name = uniquename;
                }*/
            }

            return clone;
        }
#endif

        public static T DataContractClone(object obj)
        {

            try
            {
                MemoryStream s = new MemoryStream();
                DataContractSerializer dcs = new DataContractSerializer(typeof(T));
                dcs.WriteObject(s, obj);
                s.Position = 0;

                T ret = (T)dcs.ReadObject(s);
                s.Position = 0;

                return ret;
            }

            catch (Exception ex)
            {
                Exception exc = new Exception("Error in  DataContractClone: " + ex.Message + "\n");
                throw exc;
            }
        }

#endregion
    }
}
