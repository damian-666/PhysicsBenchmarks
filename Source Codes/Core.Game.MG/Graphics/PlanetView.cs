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
    //bont uised now use a spirt.. nothgin special mabye tracking differfent
    public class PlanetView : EntityView<Planet>
    {


        new Vertices GeneralVertices { get; }  //TOPDO generate a surrounding sky or get main body verts or viewmodel verts.

        //CRAZY IDEA to spawn a nested thing like Heart, or Hand, and run an Animation while taking samples
        //keep this list of texture to change the look of nested items like Hand that can open close or beating heart

        Dictionary<Behavior, List<Texture2D>> mapBehaviorToTextures = new Dictionary<Behavior, List<Texture2D>>();


        
        public PlanetView(Planet p) : base(p)
        {


            foreach (var body in p.Spirit.Bodies)
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


        public PlanetView(Planet p, Vector2 pos) : base(p, pos)
        {
            localPosition = pos;  //domt support nesting so we wont build a d collection of childrel

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



        public  void DrawVectors(GameTime gameTime)
        {

        }
  
        
        //TODO or use raster and circles , tranparency


    }
}
