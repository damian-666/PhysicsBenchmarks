// Copyright (c) 2017 Kastellanos Nikolaos

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = Farseer.Xna.Framework.Vector2;
using Matrix = Microsoft.Xna.Framework.Matrix;
namespace FarseerPhysicsView
{
    public interface IPrimitiveBatch
    {        
        void Begin(ref Matrix projection, ref Matrix view, ref Matrix world, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, float alpha);
        void End();
        bool IsReady();
        int AddVertex(Vector3 position, Color color, PrimitiveType primitiveType);
        int AddVertex(Vector2 position, Color color, PrimitiveType primitiveType);
        int AddVertex(ref Vector2 position, Color color, PrimitiveType primitiveType);
        int AddVertex(ref Vector3 position, Color color, PrimitiveType primitiveType);        
    }
}
