using Game.Core;

namespace Game.Services;

public struct LocPersonReplacements
{
	public const string FIRST = "$firstname";

	public const string LAST = "$lastname";

	public const string ETH = "$eth";

	public const string CITY = "$cityname";

	public const string STATE = "$statename";

	public string first;

	public string last;

	public Label ethid;
}
