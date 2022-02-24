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
using System.Diagnostics;

namespace Core.Data.Collections
{
    /// <summary>
    ///  Collection for fast lookup of Effect by it  Name.   Already checks if already there, if wont add it again..
    /// </summary>
    public class EffectCollection : KeyedCollection<string, Effect>
    {
        protected override string GetKeyForItem(Effect item)
        {
            return item.Name;
        }

        /// <summary>
        /// Adds a new effect to collection .. By default,  if the effect is already there it does nothing
        /// </summary>
        /// <param name="item"></param>
        public new void Add( Effect item)
        {
            if (Contains(item.Name))
            {
                return;
            }

            base.Add(item);
        }


   
        /// Adds a new effect to collection .. By default,  if the effect is already there it does returns false
        /// </summary>
        /// <param name="item"></param>
        /// <returns>false if present and wasn't added</returns>
        public bool CheckAdd(Effect item)
        {
            if (Contains(item.Name))
            {
                return false;
            }

            base.Add(item);
            return true; 
        }



        /// <summary>
        /// Add or replace with a new effect if its .. this will orphan existing reset any delays
        /// </summary>
        /// <param name="item"></param>
        public void AddOrReplace(Effect item)
        {
            if (Contains(item.Name))
            {
                Remove(item.Name);
            }
            base.Add(item);
        }   
    }
}