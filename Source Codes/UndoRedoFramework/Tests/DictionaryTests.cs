using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UndoRedoFramework;
using UndoRedoFramework.Collections.Generic;

namespace UndoRedoFramework_Tests
{
	[TestFixture]
	public class DictionaryTests
	{
		[Test]
		public void AddRemove()
		{
			UndoRedoDictionary<int, string> dict = new UndoRedoDictionary<int, string>();

			UndoRedoManager.Start("");
			dict.Add(0, "Zero");
			dict.Add(1, "One");
			UndoRedoManager.Commit();

			UndoRedoManager.Start("");
			dict.Remove(0);
			dict[1] = "First";
			dict[2] = "Second";
			UndoRedoManager.Commit();

			Assert.IsFalse(dict.ContainsKey(0));
			Assert.AreEqual("First", dict[1]);
			Assert.AreEqual("Second", dict[2]);

			UndoRedoManager.Undo();

			Assert.AreEqual("Zero", dict[0]);
			Assert.AreEqual("One", dict[1]);
			Assert.IsFalse(dict.ContainsKey(2));

			UndoRedoManager.Redo();

			Assert.IsFalse(dict.ContainsKey(0));
			Assert.AreEqual("First", dict[1]);
			Assert.AreEqual("Second", dict[2]);
		}
		
		[Test]
		public void Clear()
		{
			UndoRedoDictionary<int, string> dict = new UndoRedoDictionary<int, string>();

			UndoRedoManager.Start("");
			dict.Add(0, "Zero");
			dict.Add(1, "One");
			UndoRedoManager.Commit();

			UndoRedoManager.Start("");
			dict.Add(2, "Two");
			dict.Clear();
			dict.Add(3, "Three");
			UndoRedoManager.Commit();

			Assert.IsFalse(dict.ContainsKey(0));
			Assert.IsFalse(dict.ContainsKey(1));
			Assert.IsFalse(dict.ContainsKey(2));
			Assert.AreEqual("Three", dict[3]);

			UndoRedoManager.Undo();

			Assert.AreEqual("Zero", dict[0]);
			Assert.AreEqual("One", dict[1]);
			Assert.IsFalse(dict.ContainsKey(2));
			Assert.IsFalse(dict.ContainsKey(3));

			UndoRedoManager.Redo();

			Assert.IsFalse(dict.ContainsKey(0));
			Assert.IsFalse(dict.ContainsKey(1));
			Assert.IsFalse(dict.ContainsKey(2));
			Assert.AreEqual("Three", dict[3]);
		}
	}
}
