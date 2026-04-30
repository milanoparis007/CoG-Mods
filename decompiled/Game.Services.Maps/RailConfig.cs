using System.Collections.Generic;
using Game.Core;

namespace Game.Services.Maps;

public sealed class RailConfig
{
	public Label id;

	public bool random;

	public WorldRect randomRect;

	public List<WorldPos> nodes = new List<WorldPos>();
}
