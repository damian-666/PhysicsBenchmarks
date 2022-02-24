using Core.Data.Collections;
using Core.Data.Entity;
using Core.Trace;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Game.MG.Graphics
{

    //lock contentionin android is bad..timer res..fast physcis it cant get draw lock
    //we try a lockless rough draw
    //FIRST pass rough and safe..

    //time clone.. two frames ...toggle mutable state on it..
    //clone anthing visible in body.
    //first collections givfe excections..

    //then mutable child ones.. markpoints

    //emitters.


    //color
    //update physics..copy all deep clone verts.
    //try no fill.. then clone fixtures for draw purpose.

    // draw uses last frame..  new frame cloned.. then 

    //copy and set as last..

    //later mabye simd copy or somethign.


    //later only clone if last one already drawn and send frame avial..
    //if physics 4 frame ahead of graphics dont clone?

    //how can it know.. must set copy params in changing places.. collide ..mbye emiter hide.. 
    //view changes.. etc.. flag to update view or somethign..


    //then dont need to maintain views whicl might be ideal
    //since tool does..when cut or add mark invalidate view to clone and only deepclone general verts

    //Lockless view of one visible physics items last calced..
    //todo use mutable flaog or something
    //struct later for simd
    public class LevelFrameView
    {

        //TODo this could be a list of views..
        //but might be easier to manage this.. way

        //potential for REPLAY feature as well  keep these frames till low mem exceprion or something
       
        //.. just kepe them  see how mig it gets..
        //when textures can be held.. body can hold them refs or map them.. we can use refs and its will be light..
        //immediate mode is better thn view model stuf when we need to update every frame anyhways.. dont want useless copy management when body have one look basically
        //meta view of spirit is only reason to realy do tht.. we can have separte fastlist unmananged pool particles maybe and sep body, spirit list to maintian
        //later its fast enough. no premature optimization..
        //   FastList<Body> bodylist = new FastList<Body>;
        List<Body> bodyListDraw = new List<Body>(100);//todo tune if keeping old.. or trim to count..
        List<Spirit> spiritsWithDraw = new List<Spirit>(100);
        List<Drawing.LineSegment> rays = new List<Drawing.LineSegment>();
        public void CloneBodies(List<Body> bodylist)
        {

            bodyListDraw.Clear();

            //todo parallel
            
                foreach (Body body in bodylist)
                {
                    bodyListDraw.Add(body.DeepCloneVisible() as Body);
                }

               

        }


        public void CloneSpiritThatDraw( IEnumerable<Spirit> spirits)
        {

            spiritsWithDraw.Clear();
            foreach (Spirit ent in spirits)
            {// just clone the drawingones they must implemen clone.. not a great architecutre shouod make special interface if mmany do this TOTO MG_GRAPHICS...

                object clonefordraw = ent.Clone();
                if (clonefordraw != null)
                {
                    spiritsWithDraw.Add(clonefordraw as Spirit);
                }
            }
        }


        public void CloneRayViews()
        {
            rays.Clear();
            foreach (var ray in SimWorld.Instance.RayViews)
            {
                rays.Add(ray.Value);
            }
        }

        public List<Body> Bodies => bodyListDraw;
        public List<Spirit> Spirits => spiritsWithDraw;
        public List<Drawing.LineSegment> Rays => rays;

        public void Archive()
        {
            bodyListDraw.Capacity = bodyListDraw.Count;//maybe free mem..

        }
    }
}