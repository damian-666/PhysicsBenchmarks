using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Game.MG.Graphics
{

    /// <summary>
    /// this class holds the rendered view of a Body..render target is used to make a raster reprensentation , which can be clipped 
    /// </summary>
    public class SpriteView    
    {
        //a view can contain one snapshot or a sequence to represent body states or looped animations
        private List<Texture2D> textures = new List<Texture2D>();

        /// <summary>
        /// access the texture for display
        /// </summary>
        public List<Texture2D> Textures { get => textures; }

        /// <summary>
        /// which image to display
        /// </summary>
        public int CurrentFrame= 0;


        /// <summary>
        ///
        //how many pixels per meter in the body space .  the body is rendered to fit into the AABB of the body , it might have mipmpaps or use a resolution based on curretn zoom level 
        /// </summary>
        readonly public Vector2 TexelScale = default(Vector2);

        /// <summary>
        /// dots per meter body scale.
        /// </summary>
        public float DPM = 0;


        readonly public AABB BodyAABB;  // just a cache when the snapshot was taken so we dont have to recompute the texture is draws at lower bound of this.


        public SpriteView(Texture2D tex, AABB bodyAABB, Vector2 texelScale = default(Vector2))
        {
            this.Textures.Add(tex);
            this.TexelScale = texelScale;
            this.BodyAABB = bodyAABB;
        }


        public Texture2D CurrentTexture => Textures[CurrentFrame];

        
    }
}
