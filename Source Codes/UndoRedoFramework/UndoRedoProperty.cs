// Siarhei Arkhipenka (c) 2006-2007. email: sbs-arhipenko@yandex.ru
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

/// <summary>
/// This is a property containment framework that does undo found on a public forum.  on  hindsight
/// its not a great method.  it puts a weight of  code  on the very basic elements of a database, one could consinder 100000 items 
/// for game wihtou editiing it must include unnessary code, even if the data has the same weight it is added complexity
/// there is confusion between valueundoable.. collections have to be special
/// the project velcroPhysics has a better bodyTemplate kind of serialization methdo. it does carry a copy weigth
/// but for the purupse of locking the database and loading data data during draw, and a standard game loop pattern
/// producer consumer pattern , its needed .. on may used datacontract but wiht a bodyData class that copies and makes bodies and joints from it.. but safer and better
/// // most or all custom physics can be consolidated with aeither physics or velco on github, instread of our and this class
/// </summary>
namespace UndoRedoFramework
{
    [DebuggerDisplay("{Value}")]
    public class UndoRedo<TValue> : IUndoRedoMember
    {
        public UndoRedo()
        {
            tValue = default(TValue);
        }
        public UndoRedo(TValue defaultValue)
        {
            tValue = defaultValue;
        }

        TValue tValue;
        public TValue Value
        {
            get { return tValue; }
            set 
            {
                tValue = value;
            }
        }

        //TODO CODE REVIEW confusing..why are there both ValueUndoable get and Value get?  they are same?  why not remove ValueUndoable.
      

        public TValue ValueUndoable
        {
            get { return tValue; }
            set
            {       
                if (UndoRedoManager.CurrentCommand != null)
                {
                    if (!UndoRedoManager.CurrentCommand.ContainsKey(this))
                    {
                        Change<TValue> change = new Change<TValue>();
                        change.OldState = tValue;
                        UndoRedoManager.CurrentCommand[this] = change;
                    }
                }

                tValue = value;
            }
        }

        #region IUndoRedoMember Members

        public event NotifyUndoRedoMemberChangedEventHandler<TValue> UndoRedoChanged;

        void IUndoRedoMember.OnCommit(object change)
        {
            Debug.Assert(change != null);
            ((Change<TValue>)change).NewState = tValue;

            if (UndoRedoChanged != null)
                UndoRedoChanged(this, UndoRedoChangedType.CommitChanged, ((Change<TValue>)change).OldState, ((Change<TValue>)change).NewState);
        }

        void IUndoRedoMember.OnUndo(object change)
        {
            Debug.Assert(change != null);
            tValue = ((Change<TValue>)change).OldState;

            if (UndoRedoChanged != null)
                UndoRedoChanged(this, UndoRedoChangedType.UndoChanged, ((Change<TValue>)change).OldState, ((Change<TValue>)change).NewState);
        }

        void IUndoRedoMember.OnRedo(object change)
        {
            Debug.Assert(change != null);
            tValue = ((Change<TValue>)change).NewState;

            if (UndoRedoChanged != null)
                UndoRedoChanged(this, UndoRedoChangedType.RedoChanged, ((Change<TValue>)change).OldState, ((Change<TValue>)change).NewState);
        }

        #endregion  
    
    }


}
