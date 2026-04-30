using System.Collections.Generic;

namespace Game.Services;

internal sealed class LocLangData : Dictionary<string, List<string>>
{
	public string langid;

	public string langname;
}
