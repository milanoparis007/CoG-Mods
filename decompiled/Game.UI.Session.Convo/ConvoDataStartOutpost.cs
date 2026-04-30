using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataStartOutpost : ConvoData
{
	public bool inside;

	public Price install;

	public Price monthly;

	public string expinstall;

	public string expmonthly;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4]
		{
			"install",
			Loc.Price(install, abs: true),
			"monthly",
			Loc.Price(monthly, abs: true)
		};
	}

	public ConvoDataStartOutpost()
	{
	}

	public ConvoDataStartOutpost(bool inside, Fixnum install, Fixnum monthly, string expinstall, string expmonthly)
	{
		this.inside = inside;
		this.install = new Price(install);
		this.monthly = new Price(monthly);
		this.expinstall = expinstall;
		this.expmonthly = expmonthly;
	}
}
