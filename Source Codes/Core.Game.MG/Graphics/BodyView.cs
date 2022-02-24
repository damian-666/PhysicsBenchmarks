using Core.Data.Animations;
using Core.Data.Collections;
using Core.Data.Entity;
using Core.Data.Interfaces;
using Core.Game.MG;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Particles;
using MGCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Core.Game.MG.Graphics
{
    /// <summary>
    /// Interface to our active View 
    /// </summary>
    public interface IEntityView
    {
        /// <summary>
        /// direct ref to the model , the entity to be drawn
        /// </summary>
        IEntity IEntity { get; set; }

        /// <summary>
        /// the Position in the  physics world
        /// </summary>
        Vector2 WorldPos { get; set; }


        void UpdatePosition();

        void Draw(GameTime gameTime);


        ViewModel GetViewModel();

        Vertices GeneralVertices { get; }


        BodyColor FillColor { get; }

        BodyColor EdgeColor { get; }
    }

    /// <summary>
    /// This a View its connected with graphics  it can be initialized when attaching to a Body 
    /// The serialized Body contains ViewModel info like ViewVertices that can be authored in tol
    /// first spirit we ill map this on the MainBody
    /// the View also may use  Tool-Authors serialized data about the view of a physical body , the ViewModel, maybe spline authoring poitns or similiar
    /// </summary>

    public class BaseView : NotifyPropertyBase, IEntityView
    {
        protected IEntity iEntity;


        protected int zOrder = 0;


        public int ZOrder { get => zOrder; }

        // https://flatredball.com/documentation/api/microsoft-xna-framework/microsoft-xna-framework-graphics/microsoft-xna-framework-graphics-texture2d/microsoft-xna-framework-graphics-texture2d-creating-new-textures-programatically/


        List<Texture2D> CurrentTextures = new List<Texture2D>();
        Texture currentTexture;

        int index = 0;

        public void UpdateTextures() 
        {

            currentTexture = CurrentTextures[index]; ///not sure if we can use this or if used by mipmaps or whaever
            index = (currentTexture == null) ? 0 : index + 1;
   
        }



        public BaseView(IEntity entity)
        {
            iEntity = entity;
        }




        /// <summary>
        /// Update pos from attached entity pos, physics must be Locked only for this copy,  then we can draw this frame while next is computed.
        /// </summary>
        public void UpdatePosition()
        {
            WorldPos = iEntity.WorldCenter.ToVector2();
            Transform = iEntity.Transform;
        }


        protected Vector2 localPosition = new Vector2(); //for child Views for emitters on Bodys, the position of  emitters in  Bodies space

        protected Vector2 worldPos = new Vector2(); //for nested Views, the position of  emitters in Bodies

        public Vector2 LocalPos { get { return localPosition; } }

        IEntity IEntityView.IEntity { get => iEntity; set => iEntity = value; }
        public Vector2 WorldPos { get => worldPos; set { worldPos = value; FirePropertyChanged(); } }

        public virtual Vertices GeneralVertices { get; set; }

        public virtual Transform Transform { get; set; }

        public virtual BodyColor FillColor => throw new NotImplementedException();

        public virtual BodyColor EdgeColor => throw new NotImplementedException();

        public ViewModel GetViewModel()
        {
            return iEntity.ViewModel;
        }

        public virtual void Draw(GameTime gameTime)
        {
           
        }

        public class EntityView<T> : BaseView
        {


            /// <summary>
            ///Cached texture of the vectors, ideally at different scales,  todo mip maps?  cosider renderTarget w mipmap  
            ///this are are generated at Runtime from the Vectors not serialized
            /// </summary>
            protected Texture2D Texture;

            protected readonly Collection<IEntityView> children;


            public Collection<IEntityView> Children { get => children; }

            public T Entity { get; set; }



            bool _fitSpline = false;

            public EntityView(T entity, Vector2 pos) : base(entity as IEntity)
            {

                Entity = entity;
                localPosition = pos;
                List<IEntityView> listchildren = new List<IEntityView>();
                children = new Collection<IEntityView>(listchildren);

            }

            public EntityView(T entity) : base(entity as IEntity)
            {
                Entity = entity;
                List<IEntityView> listchildren = new List<IEntityView>();
                children = new Collection<IEntityView>(listchildren);
            }


            //consider using some or https://github.com/jaquadro/LilyPath


            //TODO remove or use body or move some of body here.. its fine now in body unless we spline skin in spirit
            public bool FitSpline
            {
                get
                {
                    return _fitSpline;
                }
                set
                {
                    if (_fitSpline != value)
                    {
                        _fitSpline = value;
                        FirePropertyChanged();
                    }
                }
            }

        }


        public class BodyView : EntityView<Body>
        {
      
            public override Vertices GeneralVertices { get; set; }//todo get viewmodel or body verts.


            public override BodyColor EdgeColor => Entity.EdgeStrokeColor;

            public override BodyColor FillColor => Entity.Color;

            public BodyView(Body body) : base(body)
            {

                GeneralVertices = Entity.GeneralVertices;
             

                foreach (var em in body.Emitters.OfType<BodyEmitter>())
                {                                                               

                    if (!em.UseEmittedBodyAsView)
                        continue;

                    IEntity childEnt = SimWorld.GetEmitterResourceAsEntity(em);

                    if (childEnt != null)

                    {
                        if (childEnt is Spirit)
                        {
                            Children.Add(new SpiritView(childEnt as Spirit, em.LocalPosition.ToVector2()));
                        }
                        else
                        if (childEnt is Body)
                        {
 
                            Children.Add(new BodyView(childEnt as Body, em.LocalPosition.ToVector2()));
                    
                        }

                    }
                }
            }



            public BodyView(Body body, Vector2 pos) : base(body, pos)
            {
                localPosition = pos;
                if (body.ViewModel != null)
                {
                    GeneralVertices = body.ViewModel.GeneralVertices;
                    zOrder = body.ZIndex;
                }

            }

        }


    }


   
    //when we serialize that we serialize all the Entity Views along with the level becuase they may have tool edits,
    //they can be initialized form body but not  be fully generated.  they are view models..


    //draing a body, see its attached BodyView or use a map

    //the BodyView can be loaded from embededed string or Blob
    //as was the old Dress.

    // TODO replace dress with bodyview//

    //wehn saving Entity, it can have a view 





}




