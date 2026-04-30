using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class TutorialSettings
{
	public uint rngseed;

	public uint demoseed;

	public string mapdef;

	public string ethnicity;

	public string firstName;

	public string lastName;

	public Label safehouseFrontModule;

	public Label ziggyBackModule;

	public Label ziggyFriendBackModule;

	public VisitGrantList grants;
}
