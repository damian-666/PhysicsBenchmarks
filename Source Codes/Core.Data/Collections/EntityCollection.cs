using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;

using Core.Data.Entity;

namespace Core.Data.Collections
{
    [KnownType(typeof(Spirit))]
    [KnownType(typeof(Body))]
    [KnownType(typeof(Planet))]
    public class EntityCollection : ObservableCollectionUndoable<IEntity>
    {
        public EntityCollection() :
            base(new List<IEntity>())
        {
        }

        public EntityCollection(List<IEntity> source): base(source)
        {
        }



    }
}
