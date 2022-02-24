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
using static Core.Game.MG.Graphics.BaseView;

namespace Core.Game.MG.Graphics
{

    public class SpiritView : EntityView<Spirit>
    {

        /// <summary>
        /// TODO deep clone if chaning. cuttable or somemthign on on the cut itself it could while is drawing tghe old one should be able to cophy
        /// 
    
        /// </summary>
        public new Vertices  GeneralVertices { get; set; }  //TOPDO generate a surrounding sky or get main body verts or viewmodel verts.

        //CRAZY IDEA to spawn a nested thing like Heart, or Hand, and run an Animation while taking samples
        //keep this list of texture to change the look of nested items like Hand that can open close or beating heart

        Dictionary<Behavior, List<Texture2D>> mapBehaviorToTextures = new Dictionary<Behavior, List<Texture2D>>();

        public override BodyColor EdgeColor => Entity.MainBody.EdgeStrokeColor;

        public override BodyColor FillColor => Entity.MainBody.Color;


        public SpiritView(Spirit sp) : base(sp)
        {


            foreach (var body in sp.Bodies)
            {

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



        }


        public SpiritView(Spirit sp, Vector2 pos) : base(sp, pos)
        {
            localPosition = pos;  //domt support nesting so we wont build a d collection of childrel

            if (sp.ViewModel != null)
            {
                GeneralVertices = sp.ViewModel.GeneralVertices;
            }
        }


        //
        // Summary:
        //     Draw this component.
        //
        // Parameters:
        //   gameTime:
        //     The time elapsed since the last call to Microsoft.Xna.Framework.DrawableGameComponent.Draw(Microsoft.Xna.Framework.GameTime).
        public override void Draw(GameTime gameTime)
        {

            Entity.Draw(gameTime.ElapsedGameTime.TotalSeconds  );


        }


        public  void DrawSkin(GameTime gameTime)
        {

            // plan for jointed bodies

            //    first draw the body by raster, this will be inner organs, cycling, behaviors
            //    like heart beats, fluid circulating
            // fot hands like MakeFist or OpenHand, CloseHand


            //then always draw vector for skin unless maybe very zoomed out

            //either use half circle, tranparentcy.. complex

            //or best plan..
            //placee Joint ViewCircle radius on each joint, set in Tool on model
            //go arround verts, fit spline,
            //cal limb intersect pt.  add to a collection map to body in here 
            //or add a markpoint

            //get next joint in graph, traverse to nerest pt on next body
            //exclude pt in cicle, when first pt found traverse to next body untill main body
            // on main body traverse till point in one of the joints, 
            //set nexbody to be the joint.other body.. etc keep going till closed

            //if vert in circle skip and query main body for limp intersect pt nearest last pt.
            //traverse there then conitnue around the entire thing




        }

        public  void DrawVectors(GameTime gameTime)
        {

        }
        /// <summary>
        ///consider  generate these  baseed on intersectino, simulated a 1D skiin
        /// </summary>
        Vertices SkinVertices { get; }

        //TODO or use raster and circles , tranparency


    }
}
