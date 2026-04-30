using System.Diagnostics;
using Game.Core;
using Game.Services;

namespace Game.Session.Sim;

[DebuggerDisplay("{DebugString}")]
public sealed class FamilyTree
{
	[DebuggerDisplay("{DebugString}")]
	public struct Anchor
	{
		public NodeID nodeId;

		public WorldPos pos;

		public float ethscore;

		public bool IsSet => nodeId.IsValid;

		public bool IsNotSet => nodeId.IsNotValid;

		private string DebugString => $"A:{nodeId}/{pos}/{ethscore}";

		public override string ToString()
		{
			return DebugString;
		}
	}

	public int famId;

	public Label eth;

	public Skin skin;

	public Anchor anchor;

	private string DebugString => $"FamTree {famId}/{eth} @ {anchor}";

	public override string ToString()
	{
		return DebugString;
	}
}
