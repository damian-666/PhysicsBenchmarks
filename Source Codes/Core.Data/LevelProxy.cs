/*
 * Game level, collection of game  and entities.   A heirarchical object data base.. some entities may contain other entites
 */
#define COLLISIONEFFECTONALL

using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using FarseerPhysics.Common;
using Farseer.Xna.Framework;
using FarseerPhysics.Collision;

public class LevelProxy : IEntity
{

    /// <summary>
    /// just  a proxy that uses a thumnail , for level  hyperlinskso we dont have to load the whole level
    /// </summary>
    byte[] thumbnail;

    /// <summary>
    /// key or path to wyg file
    /// </summary>
    string FileName { get; set; }

    string PluginName { get; set; }

    /// <summary>
    /// pretty name for the level
    /// </summary>
    string Name { get; set; }
    public LevelProxy( byte[] image, string filename)
    {
        thumbnail = image;
        FileName = filename;
    }

    public byte[] Thumbnail => this.thumbnail;


    public Vector2 Position { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public Transform Transform => throw new System.NotImplementedException();

    public Vector2 WorldCenter => throw new System.NotImplementedException();

    public float Rotation { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public AABB EntityAABB => throw new System.NotImplementedException();

    public bool WasSpawned => throw new System.NotImplementedException();

    public int ID => throw new System.NotImplementedException();

    public Vector2 LinearVelocity => throw new System.NotImplementedException();

    public float AngularVelocity => throw new System.NotImplementedException();

    public ViewModel ViewModel => throw new System.NotImplementedException();

    public IEnumerable<IEntity> Entities => null;

    string IEntity.Name => this.Name;

    string IEntity.PluginName => this.PluginName;

    public void Draw(double dt)
    {
        throw new System.NotImplementedException();
    }

    public void Update(double dt)
    {
        throw new System.NotImplementedException();
    }

    public void UpdateAABB()
    {
        throw new System.NotImplementedException();
    }

    public void UpdateThreadSafe(double dt)
    {
        throw new System.NotImplementedException();
    }
}
