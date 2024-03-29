using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UndoRedoFramework;
using UndoRedoFramework.Collections.Generic;

namespace UndoRedoFramework_Tests
{
	[TestFixture]
	public class ListTest
	{
		[Test]
		public void AddRemove()
		{
			UndoRedoList<int> list = new UndoRedoList<int>();
			// Add
			UndoRedoManager.Start("");
			list.Add(1);
			UndoRedoManager.Commit();

			Assert.AreEqual(1, list[0]);
			Assert.AreEqual(1, list.Count);
			// AddRange
			UndoRedoManager.Start("");
			list.AddRange(new int[] { 2, 3, 4, 5, 6});
			UndoRedoManager.Commit();

			Assert.AreEqual(6, list.Count);
			// RemoveRange
			UndoRedoManager.Start("");
			list.RemoveRange(4, 2);
			UndoRedoManager.Commit();

			// RemoveAt
			UndoRedoManager.Start("");
			list.RemoveAt(0);
			UndoRedoManager.Commit();

			// Remove
			UndoRedoManager.Start("");
			list.Remove(2);
			UndoRedoManager.Commit();

			Assert.AreEqual(3, list[0]);
			Assert.AreEqual(4, list[1]);
			Assert.AreEqual(2, list.Count);

			UndoRedoManager.Undo();
			UndoRedoManager.Undo();
			UndoRedoManager.Undo();
			Assert.AreEqual(6, list.Count);

			UndoRedoManager.Undo(); // undo AddRange
			Assert.AreEqual(1, list.Count);

			UndoRedoManager.Undo(); // undo Add
			Assert.AreEqual(0, list.Count);

			UndoRedoManager.Redo(); // redo Add
			UndoRedoManager.Redo(); // redo AddRange
			Assert.AreEqual(6, list.Count);
			Assert.IsTrue(list[0] == 1 && list[1] == 2 && list[2] == 3 && list[3] == 4 && list[4] == 5 && list[5] == 6);

			UndoRedoManager.Redo(); // redo RemoveRange
			UndoRedoManager.Redo(); // redo RemoveAt
			UndoRedoManager.Redo(); // redo Remove
			Assert.AreEqual(2, list.Count);
		}

		[Test]
		public void Clear()
		{
			UndoRedoList<int> list = new UndoRedoList<int>(new int[] {1, 2, 3});
			Assert.AreEqual(3, list.Count);
			// clear
			UndoRedoManager.Start("");
			list.Clear();
			UndoRedoManager.Commit();
			Assert.AreEqual(0, list.Count);
			// add 
			UndoRedoManager.Start("");
			list.Add(1);
			UndoRedoManager.Commit();
			Assert.AreEqual(1, list.Count);

			UndoRedoManager.Undo(); // undo add
			Assert.AreEqual(0, list.Count);
			UndoRedoManager.Undo(); // undo clear
			Assert.AreEqual(3, list.Count);

			UndoRedoManager.Redo(); // redo clear
			Assert.AreEqual(0, list.Count);
			UndoRedoManager.Redo(); // redo add
			Assert.AreEqual(1, list.Count);
		}
	}
}
