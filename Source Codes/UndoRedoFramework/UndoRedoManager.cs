// Siarhei Arkhipenka (c) 2006-2007. email: sbs-arhipenko@yandex.ru
using System;
using System.Collections.Generic;
using System.Text;

namespace UndoRedoFramework
{
	/// <summary>
	/// 
	/// </summary>
    public static class UndoRedoManager
    {
        private static List<Command> history = new List<Command>();
        private static int currentPosition = -1;
        private static Command currentCommand = null;

        public static int CountUndo
        {
            get { return currentPosition; }
        }

        public static int CountRedo
        {
            get { return history.Count - currentPosition; }
        }

        public static Command CurrentCommand
        {
            get { return currentCommand; }
        }
		/// <summary>
		/// Returns true if history has command that can be undone
		/// </summary>
        public static bool CanUndo
        {
            get { return currentPosition >= 0; }
        }
		/// <summary>
		/// Returns true if history has command that can be redone
		/// </summary>
        public static bool CanRedo
        {
            get { return currentPosition < history.Count - 1; }
        }
		/// <summary>
		/// Undo last command from history list
		/// </summary>
        public static void Undo()
        {
            AssertNoCommand();
            if (CanUndo)
            {
                Command command = history[currentPosition--];
                foreach (IUndoRedoMember member in command.Keys)
                {
                    member.OnUndo(command[member]);
                }
				OnCommandDone(CommandDoneType.Undo, command.Owner, command.UserData);
            }
        }
		/// <summary>
		/// Repeats command that was undone before
		/// </summary>
        public static void Redo()
        {
            AssertNoCommand();
            if (CanRedo)
            {
                Command command = history[++currentPosition];
                foreach (IUndoRedoMember member in command.Keys)
                {
                    member.OnRedo(command[member]);
                }
				OnCommandDone(CommandDoneType.Redo, command.Owner, command.UserData);
            }
        }
		/// <summary>
		/// Start command. Any data changes must be done within a command.
		/// </summary>
		/// <param name="commandCaption"></param>
		/// <returns></returns>
        public static IDisposable Start(string commandCaption)
        {
            AssertNoCommand();
            currentCommand = new Command(commandCaption);
			return currentCommand;
        }

        public static IDisposable Start(string commandCaption, object Owner)
        {
            AssertNoCommand();
            currentCommand = new Command(commandCaption, Owner);
            return currentCommand;
        }

        public static IDisposable Start(string commandCaption, object Owner, object userData)
        {
            AssertNoCommand();
            currentCommand = new Command(commandCaption, Owner, userData);
            return currentCommand;
        }

		/// <summary>
		/// Commits current command and saves changes into history
		/// </summary>
        public static void Commit()
        {
            AssertCommand();

            if (currentCommand.Count < 1)
            {
                Cancel();
                return;
            }

            foreach (IUndoRedoMember member in currentCommand.Keys)
            {
                member.OnCommit(currentCommand[member]);
            }

            // add command to history (all redo records will be removed)
			int count = history.Count - currentPosition - 1;
			history.RemoveRange(currentPosition + 1, count);

            history.Add(currentCommand);
            currentPosition++;
			TruncateHistory(); 

			OnCommandDone(CommandDoneType.Commit, currentCommand.Owner, currentCommand.UserData);

            currentCommand = null;
        }
		/// <summary>
		/// Rollback current command. It does not saves any changes done in current command.
		/// </summary>
        public static void Cancel()
        {
            AssertCommand();
            foreach (IUndoRedoMember member in currentCommand.Keys)
                member.OnUndo(currentCommand[member]);
            currentCommand = null;
        }
		/// <summary>
		/// Clears all history. It does not affect current data but history only. 
		/// It is usefull after any data initialization if you want forbid user to undo this initialization.
		/// </summary>
        public static void FlushHistory()
        {
            currentCommand = null;
            currentPosition = -1;
            history.Clear();
        }

        public static void RemoveHistoryAt(int index, int count)
        {
            for (int i = 0; i < count; i++)
            {
                history.RemoveAt(index--);
                currentPosition--;
            }
        }

		/// <summary>Checks there is no command started</summary>
        public static void AssertNoCommand()
        {
            if (currentCommand != null)
                throw new InvalidOperationException("Previous command is not completed. Use UndoRedoManager.Commit() to complete current command.");
        }

        public static bool AssertCommandWithOwner(object owner)
        {
            if (currentCommand == null) return true;
            if (currentCommand.Owner != owner)
                return false;

            return true;
        }

		/// <summary>Checks that command had been started</summary>
        public static void AssertCommand()
        {
            if (currentCommand == null)
                throw new InvalidOperationException("Command is not started. Use method UndoRedoManager.Start().");
        }

        public static bool IsCommandStarted
        {
            get { return currentCommand != null; }
        }

		/// <summary>Gets an enumeration of commands captions that can be undone.</summary>
		/// <remarks>The first command in the enumeration will be undone first</remarks>
		public static IEnumerable<string> UndoCommands
		{
			get
			{
				for (int i = currentPosition; i >= 0; i--)
					yield return history[i].Caption;
			}
		}

        public static string CurrentUndoCommand
        {
            get
            {
                return currentPosition > -1 ? history[currentPosition].Caption : "";
            }
        }

		/// <summary>Gets an enumeration of commands captions that can be redone.</summary>
		/// <remarks>The first command in the enumeration will be redone first</remarks>
		public static IEnumerable<string> RedoCommands
		{
			get
			{
				for (int i = currentPosition + 1; i < history.Count; i++)
					yield return history[i].Caption;
			}
		}

        public static string CurrentRedoCommand
        {
            get
            {
                return currentPosition < history.Count - 1 ? history[currentPosition + 1].Caption : "";
            }
        }

		public static event EventHandler<CommandDoneEventArgs> CommandDone;
		static void OnCommandDone(CommandDoneType type)
		{
			if (CommandDone != null)
				CommandDone(null, new CommandDoneEventArgs(type));
		}

        static void OnCommandDone(CommandDoneType type, object Owner)
        {
            if (CommandDone != null)
                CommandDone(null, new CommandDoneEventArgs(type, Owner));
        }

        static void OnCommandDone(CommandDoneType type, object Owner, object userData)
        {
            if (CommandDone != null)
                CommandDone(null, new CommandDoneEventArgs(type, Owner, userData));
        }


        private static int maxHistorySize = 0;

        /// <summary>
        /// Gets/sets max commands stored in history. 
        /// Zero value (default) sets unlimited history size.
        /// </summary>
        public static int MaxHistorySize
        {
            get { return maxHistorySize; }
            set 
            {
				if (IsCommandStarted)
					throw new InvalidOperationException("Max size may not be set while command is run.");
                if (value < 0)
                    throw new ArgumentOutOfRangeException("Value may not be less than 0");
                maxHistorySize = value;
                TruncateHistory();
            }
        }

        private static void TruncateHistory()
        {			
            if (maxHistorySize > 0)
				if (history.Count > maxHistorySize)
				{
					int count = history.Count - maxHistorySize;
					history.RemoveRange(0, count);
					currentPosition -= count;
				}
        }



        //TODO Code review, this is not clear, does it mean "Current Command"?


        /// <summary>
        /// To guard a property value from being set invalidly, use this guard
        /// </summary>
        /// <param name="memberName">The member of the property name is string</param>
        /// <returns>true if the member name is the same, false otherwise</returns>
        public static bool IsMemberOf(string memberName)
        {
            if (CurrentCommand != null)
                if (CurrentCommand.UserData != null)
                    if (CurrentCommand.UserData.Equals(memberName)) return true;

            return false;
        }
	
	}

	public enum CommandDoneType
	{
		Commit, Undo, Redo
	}

	public class CommandDoneEventArgs : EventArgs
	{ 
		public readonly CommandDoneType CommandDoneType;
		public CommandDoneEventArgs(CommandDoneType type)
		{
			CommandDoneType = type;
		}

        public readonly object Owner;
        public CommandDoneEventArgs(CommandDoneType type, object owner)
        {
            CommandDoneType = type;
            Owner = owner;
        }

        public readonly object UserData;
        public CommandDoneEventArgs(CommandDoneType type, object owner, object userData)
        {
            CommandDoneType = type;
            Owner = owner;
            UserData = userData;
        }

	}

}
