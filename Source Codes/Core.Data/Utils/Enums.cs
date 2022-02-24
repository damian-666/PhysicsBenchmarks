// common storage location for generic enumslk

namespace Core.Data
{
	/// <summary>
	/// specifies how colums or vertical stacks of UI elements are lined up horizontally.  
	/// </summary>
	public enum HorizontalAlign
	{
		Left,
		Center,
		Right
	}

	///S <summary>
	/// Specifies how elements of different highs are aligned together on a layout.  for UI but can be for arrangement of flying squadrons
	/// </summary>
	public enum VerticalAlign
	{
		Top,
		Center,
		Bottom
	}
	/// <summary>
	/// Edges
	/// </summary>

	public enum Edge
	{
		Top,
		Bottom,
		Left,
		Right
	}

	/// <summary>
	/// Direction relative to ground.  on circle left would mean counterclockwise, as in standing on top fo the world
	/// Moved this from Fez to Core.data, is useful for general api.
	/// </summary>
	/// <remarks>
	/// Instead of bool left as a param to  symmetric methods, a better and more consise way might be
	/// pass in Direction dir = Direction.Left, as last parameter, default being left</remarks>
	public enum Direction
	{
		Up,
		Down,
		Left,
		Right
	}
}