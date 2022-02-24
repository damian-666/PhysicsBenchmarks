using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

using Core.Data.Animations;
using System.Collections.Specialized;
using Core.Data.Interfaces;
using System.IO;
using System.Runtime.Serialization;

namespace Core.Data.Collections
{




    //TODO future could   this be made undoable collection?.. i think so, not much need it , rarely use it..
    //we have UndoRedoList but its not used, its implemented in a portable module and there are differences
    // and this is a Keyedcollection,  so would not be easy.   could use a keyedList  http://www.codeproject.com/KB/collections/KeyedList/KeyedList_src.zip

    /// <summary>
    /// Keyed Behavior Collection for fast lookup of Behavior Name
    /// Undo/Redo not yet supported
    /// </summary>
    public class BehaviorCollection : KeyedCollection<string, Behavior>, INotifyCollectionChanged
    {
        #region INotifyCollectionChanged event

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        private bool _fireCollectionEvent = true;
        protected virtual void NotifyCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_fireCollectionEvent) return;

            if (CollectionChanged != null)
                CollectionChanged(this, e);
        }

        #endregion


        //TODO CODE REVIEW , this might go in a base class
        private static Behavior DataContractClone(object obj)
        {

            try
            {
                MemoryStream s = new MemoryStream();
                DataContractSerializer dcs = new DataContractSerializer(typeof(Behavior));
                dcs.WriteObject(s, obj);
                s.Position = 0;

                Behavior ret = (Behavior)dcs.ReadObject(s);
                s.Position = 0;

                return ret;
            }

            catch (Exception ex)
            {
                Exception exc = new Exception("Error in  DataContractClone: " + ex.Message + "\n");
                throw exc;
            }
        }



        public Behavior CreateClone(Behavior source)
        {
            Behavior clone = new Behavior(source);
            return clone;
        }




        protected override string GetKeyForItem(Behavior item)
        {
            return item.Name;
        }


        public void ChangeName(Behavior item, string newKey)
        {
           
                if (item.Name == newKey)
                    return;

                base.ChangeItemKey(item, newKey);

                item.Name = newKey;

                NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        }

        protected override void SetItem(int index, Behavior item)
        {
            base.SetItem(index, item);
            NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, index));
        }

        protected override void InsertItem(int index, Behavior item)
        {
            base.InsertItem(index, item);
            NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void RemoveItem(int index)
        {
            Behavior behavior = this[index];
            base.RemoveItem(index);
            NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, behavior, -1));

        }

        public void AddRange(IEnumerable<Behavior> items)
        {
            _fireCollectionEvent = false;
            foreach (Behavior b in items)
                this.Add(b);
            _fireCollectionEvent = true;
            NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Reload()
        {
            NotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
