using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UndoRedoFramework;
using UndoRedoFramework.Collections.Generic;

namespace UndoRedoFramework_Tests
{
	[TestFixture]
	public class GeneralTests
	{
		[TearDown]
		public void TearDown()
		{
			UndoRedoManager.FlushHistory();
            UndoRedoManager.MaxHistorySize = 0;
		}

		[Test]
		public void BasicScenario()
		{
			UndoRedo<int> i = new UndoRedo<int>(0);

			UndoRedoManager.Start("");
			i.Value = 1;
			UndoRedoManager.Commit();
			Assert.AreEqual(1, i.Value);
			Assert.IsTrue(UndoRedoManager.CanUndo);
			Assert.IsFalse(UndoRedoManager.CanRedo);

			UndoRedoManager.Undo();
			Assert.AreEqual(0, i.Value);
			Assert.IsFalse(UndoRedoManager.CanUndo);
			Assert.IsTrue(UndoRedoManager.CanRedo);

			UndoRedoManager.Redo();
			Assert.AreEqual(1, i.Value);
		}

		[Test]
		public void FlushHistory()
		{
			UndoRedo<int> i = new UndoRedo<int>(0);
			
			// start + commit + flush
			UndoRedoManager.Start("");
			i.Value = 1;
			UndoRedoManager.Commit();
			Assert.AreEqual(1, i.Value);

			UndoRedoManager.FlushHistory();
			Assert.IsFalse(UndoRedoManager.CanUndo); // history must be empty
			Assert.AreEqual(1, i.Value); // data must be intact
			
			// start + flush
			UndoRedoManager.Start("");
			i.Value = 2;
			UndoRedoManager.FlushHistory();
			Assert.IsFalse(UndoRedoManager.CanUndo); // history must be empty
			Assert.AreEqual(2, i.Value); // data must be intact
		}

		[Test]
		public void ManualCancel()
		{
			UndoRedo<int> i = new UndoRedo<int>(0);
			UndoRedoList<int> list = new UndoRedoList<int>(new int[] {1, 2, 3});
			UndoRedoDictionary<int, string> dict = new UndoRedoDictionary<int, string>();

			UndoRedoManager.Start("");
			i.Value = 1;
			list.Add(4);
			dict[1] = "One";
			UndoRedoManager.Cancel();

			Assert.AreEqual(0, i.Value);
			Assert.AreEqual(3, list.Count);
			Assert.IsFalse(dict.ContainsKey(1));

			// run next command to make sure that framework works well after cancel
			UndoRedoManager.Start("");
			i.Value = 1;
			UndoRedoManager.Commit();

			Assert.AreEqual(1, i.Value);
		}
		
		[Test]
		public void AutoCancel()
		{
			UndoRedo<int> i = new UndoRedo<int>(0);

			// "successful" scenario
			using (UndoRedoManager.Start(""))
			{
				i.Value = 1;
				UndoRedoManager.Commit();
			}
			Assert.AreEqual(1, i.Value);

			// "failed" scenario
			try
			{
				using (UndoRedoManager.Start(""))
				{
					i.Value = 2;
					throw new Exception("Some exception");
                    // this code is never reached in this scenario
					UndoRedoManager.Commit(); 
				}
			}
			catch { }
			Assert.AreEqual(1, i.Value);
		}

		[Test]
		public void CommandsCaptions()
		{
			UndoRedo<int> i = new UndoRedo<int>(0);

			UndoRedoManager.Start("1");
			i.Value = 1;
			UndoRedoManager.Commit();

			UndoRedoManager.Start("2");
			i.Value = 2;
			UndoRedoManager.Commit();

			UndoRedoManager.Start("3");
			i.Value = 3;
			UndoRedoManager.Commit();

			UndoRedoManager.Start("4");
			i.Value = 4;
			UndoRedoManager.Commit();

			UndoRedoManager.Undo();
			UndoRedoManager.Undo();

			List<string> undo = new List<string>(UndoRedoManager.UndoCommands);
			Assert.AreEqual("2", undo[0]);
			Assert.AreEqual("1", undo[1]);
			Assert.AreEqual(2, undo.Count);

			List<string> redo = new List<string>(UndoRedoManager.RedoCommands);
			Assert.AreEqual("3", redo[0]);
			Assert.AreEqual("4", redo[1]);
			Assert.AreEqual(2, redo.Count);
		}
        [Test]
        public void HistorySize()
        {
            UndoRedo<int> i = new UndoRedo<int>(0);

            UndoRedoManager.Start("1");
            i.Value = 1;
            UndoRedoManager.Commit();

            UndoRedoManager.Start("2");
            i.Value = 2;
            UndoRedoManager.Commit();

            UndoRedoManager.Start("3");
            i.Value = 3;
            UndoRedoManager.Commit();

            UndoRedoManager.Start("4");
            i.Value = 4;
            UndoRedoManager.Commit();
            
            Assert.AreEqual(4, new List<string>(UndoRedoManager.UndoCommands).Count);
            
            UndoRedoManager.MaxHistorySize = 3;

            Assert.AreEqual(3, new List<string>(UndoRedoManager.UndoCommands).Count);

            UndoRedoManager.Start("5");
            i.Value = 5;
            UndoRedoManager.Commit();

            Assert.AreEqual(3, new List<string>(UndoRedoManager.UndoCommands).Count);
        }
	}
}
