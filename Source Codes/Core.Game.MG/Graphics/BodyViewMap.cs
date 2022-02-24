using FarseerPhysics.Dynamics;
using Farseer.Xna.Framework;

using FarseerPhysics.Collision;
using FarseerPhysics.Dynamics.Particles;
using System.Diagnostics;
using System.Collections.Generic;
using static Core.Game.MG.Graphics.BaseView;
using FarseerPhysics.Common;
using Core.Data.Collections;

namespace Core.Game.MG.Graphics
{
    /// <summary>
    /// Class to store mapping between entity and view. 
    /// </summary>
    public class BodyViewMap
    {
        protected Presentation _presentation;

        //mapping via body , because we are using the broadphase that takes bodies for now TODO use the templated Aether version
        protected Dictionary<Body, BaseView> _map;

        protected World _world = null;  //we use the physic world space to query and cull to viewport


        //TODO particles will need be render, listed separatly they area in the collision space
        // todo dont add them then as NotCollidable that is a shitty hack.
        //TODO add a pool of partilces.

        public World World { set { _world = value; } }

        public BodyViewMap(Presentation presentation, World phyics)
        {
            _world = phyics;
            _presentation = presentation;
            _map = new Dictionary<Body, BaseView>();
        }

        
        public void UpdateUsePosition(IEnumerable<BaseView> views)
        {
 
            foreach (var view in views)
            {
                view.UpdatePosition();
            }
        }

        bool hasZ = false;

        bool HasZ { get => hasZ; }

        public IEnumerable<BaseView> GetVisibleViews()
        {
            hasZ = false;
            foreach ( Body body in GetBodiesInWindow())
            {
                BaseView bv = GetView(body);

                if (bv != null)
                {
                    hasZ = bv.ZOrder != 0;
                    yield return bv;
                }

            }
        }


        FastList<BaseView> displayList = new FastList<BaseView>();
        public FastList<BaseView> DisplayList { get => displayList; }

        
        private FarseerPhysics.Common.HashSet<Body> GetBodiesInWindow()
        {
           

            //TODO if impractical to make hiREs Textures when zoomed in 
            //render as vector

            //TODO add Render as vector 
            // view will be culled so much wont be a problem


            //TODO test this with debug view w clouds

            AABB aabb = Graphics.Instance.CTransform.GetWorldWindowAABB();

            //TODO we shold make our own spatial hash for views like Nez..maybe..   add particles also.
            //since here we must query by 

            //or modify farseer query to fast query by body.AABB, and body system AABB;


            FarseerPhysics.Common.HashSet<Body> bodiesInView = new FarseerPhysics.Common.HashSet<Body>();


            //todo MG_GRAPHICS, optimize merge in generic broadphase from Aether, do at spirit level
            _world.QueryAABB(
                fixture =>
                {


                    if (fixture != null && !fixture.IsSensor)
                    {
                        bodiesInView.CheckAdd(fixture?.Body);
                    }

                    return true;// Continue the query.
                }, ref aabb);




            return bodiesInView;

        }




        public void Clear()
        {
            _map.Clear();
        }




        public BaseView AddView(Body body, BaseView view)
        {

            if (_map.ContainsKey(body))
                _map[body] = view;
            else
                _map.Add(body, view);
            return view;
        }

   

        public BaseView GetView(Body body)
        {
            BaseView view = null;
            _map.TryGetValue(body, out view);
            return view;
        }


        public void RemoveView(Body body)
        {

            BaseView view = null;
            if (_map.TryGetValue(body, out view) == true)
            {
                _map.Remove(body);
            }
        }


        public void RemoveView(IEnumerable<IEntity> bodies)
        {
            foreach (Body body in bodies)
            {
                RemoveView(body);
            }
        }


    }
}


