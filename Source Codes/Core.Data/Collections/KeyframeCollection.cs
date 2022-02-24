using System.Collections.Generic;

using Core.Data.Animations;


namespace Core.Data.Collections
{
    public class KeyframeCollection : ObservableCollectionUndoable<Keyframe>
    {
        public KeyframeCollection()
            : base(new List<Keyframe>())
        {
        }
    }
}
