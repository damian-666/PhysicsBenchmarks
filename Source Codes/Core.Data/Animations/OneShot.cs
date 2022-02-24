using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Data.Animations
{
    public class OneShot
    {
        bool once = true;
        public bool DoOnce()
        {
            bool org = once;
            once = false;
            if (org) return true;
            //no provision for failure  use an effect.. on finish event..
            return false; 
        }

        /// <summary>
        /// Call in case method  failed , it will reset and do ones again
        /// </summary>
        public void DoAgain() { once = true; DoOnce(); }

        public bool DoneOnce()
        { return !DoOnce(); }


    }


    public class OneShotMap
    {

        FarseerPhysics.Common.HashSet<object> map = new FarseerPhysics.Common.HashSet<object>();

        public bool DoOnce(object o)
        {
            bool once = map.Contains(o);
            if (once)
                return false;
            map.Add(o);
            return true;
        }


        public void DoAgain(object obj)
        {
            map.Remove(obj);
            DoOnce(obj);
        }

    }


}
