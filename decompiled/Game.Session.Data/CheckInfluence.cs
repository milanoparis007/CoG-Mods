using Game.Core;
using Game.Services;
using Game.Services.Store;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckInfluence : VisitWithValueRequirement
{
	public enum Type
	{
		Current,
		Highwater
	}

	public Type type;

	public override bool DoesPass(VisitState visit)
	{
		if (!Game.serv.store.IsPackInstalled(PackID.ShadowGovernment))
		{
			return false;
		}
		return base.DoesPass(visit);
	}

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		PlayerID pid = visit.pid;
		if (type == Type.Current)
		{
			return Game.ctx.simman.politics.GetInfluenceWallet(pid).current;
		}
		return Game.ctx.simman.politics.GetInfluenceWallet(pid).highwater;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		if (type == Type.Highwater)
		{
			return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.check-influence.highwater", "amt", value));
		}
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.check-influence.current", "amt", value));
	}
}
