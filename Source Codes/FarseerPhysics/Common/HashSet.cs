using System;
using System.Collections;
using System.Collections.Generic;

//TODO SHADOWPLAY MOD future , should we make this observable?   Or repace with .net 4 hashset , and observable hash set in Datacollection.
//need to consider if we will port this to Mono / ipad.. before commiting data to .net4.
namespace FarseerPhysics.Common
{
    public class HashSet<T> : ICollection<T>
    {
        private Dictionary<T, short> _dict;

        public HashSet(int capacity)
        {
            _dict = new Dictionary<T, short>(capacity);
        }

        public HashSet()
        {
            _dict = new Dictionary<T, short>();
        }

        // Methods

        #region ICollection<T> Members

        public void Add(T item)
        {
            if (item == null)
                return;

            // We don't care for the value in dictionary, Keys matter.
            _dict.Add(item, 0);
        }

        public void CheckAdd(T item)  // shadowplay mod
        {
            if (!Contains(item))
            {
                Add(item);
            }
        }

        public void Clear()
        {
            _dict.Clear();
        }

        public bool Contains(T item)
        {
            return _dict.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _dict.Keys.CopyTo(array,arrayIndex);
       
        }

        public bool Remove(T item)
        {
            return _dict.Remove(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _dict.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _dict.Keys.GetEnumerator();
        }

        // Properties
        public int Count
        {
            get { return _dict.Keys.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public int IEnumerable { get; set; }

        #endregion
    }
}