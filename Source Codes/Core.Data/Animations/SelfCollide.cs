using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using FarseerPhysics.Dynamics;

using UndoRedoFramework;

namespace Core.Data.Animations
{
    public class SelfCollide : Effect
    {

        private List<Body> _bodies;

        public SelfCollide(Spirit sp, string name)
            : base(sp, name)
        {
        }

        /// <summary>
        /// Will allow special parts of a sytem like hands or feet to collide with system for a period of time.   used if detected on collision course
        /// //use sparingly for keep framerate high on normal animations.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="duration"></param>
        ///   <param name="name"></param>
        /// <param name="bodies"></param>
        public SelfCollide(Spirit sp, string name, double duration, IEnumerable<Body> bodies)
            : base(sp, name, duration)
        {
            _bodies = new List<Body>(bodies);
        }

        public override void Update(double dt)
        {
            base.Update(dt);
            _bodies.ForEach(x => { if (x != null)  x.CollisionGroup = 0; });
        }


    }
}

