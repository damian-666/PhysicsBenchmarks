// Siarhei Arkhipenka (c) 2006-2007. email: sbs-arhipenko@yandex.ru
using System;
using System.Collections.Generic;
using System.Text;

namespace UndoRedoFramework
{
    public interface IUndoRedoMember
    {
        void OnCommit(object change);
        void OnUndo(object change);
        void OnRedo(object change);
    }

    public enum UndoRedoChangedType
    {
        CommitChanged = 0,
        UndoChanged = 1,
        RedoChanged = 2
    }

    public delegate void NotifyUndoRedoCollectionChangedEventHandler<T>(object sender, UndoRedoChangedType type, IList<T> oldCollection, IList<T> newCollection);
    public delegate void NotifyUndoRedoMemberChangedEventHandler<T>(object sender, UndoRedoChangedType type, T oldState, T newState);

}
