using Game.Platform;
using Game.Services.Maps;
using Game.Session;

namespace Game.UI;

public sealed class SessionPrepResults
{
	public SaveFileContents savefile;

	public ScenarioConfig scenario;

	public MapConfig custommap;

	public MapConfig mapconfig => custommap;
}
