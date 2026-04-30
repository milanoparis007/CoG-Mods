using System.Collections.Generic;

namespace Game.Core;

public sealed class LabelEqualityComparer : IEqualityComparer<Label>
{
	public bool Equals(Label x, Label y)
	{
		return x.Index == y.Index;
	}

	public int GetHashCode(Label x)
	{
		return x.Index;
	}
}
