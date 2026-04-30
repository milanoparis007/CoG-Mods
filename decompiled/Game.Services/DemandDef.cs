using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class DemandDef
{
	public sealed class Social
	{
		public Label npcSocial;

		public Label playerSocial;
	}

	public Demand.Type demand;

	public bool atOwner;

	public bool atGoon;

	public bool atGang;

	public bool bribeAllowed;

	public Social onSuccess;
}
