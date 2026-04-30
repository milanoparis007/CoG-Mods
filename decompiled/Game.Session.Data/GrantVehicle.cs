using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class GrantVehicle : VisitGrant
{
	public Label id;

	public Fixnum healthfraction = 1;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		if (FindVehicle()?.mobile != null)
		{
			Node crewNode = ctx.visit.GetCrewNode();
			Entity entity = ctx.GetPlayer().crew.CreateAndTrackVehicle(id, crewNode.pos);
			if (ctx.pid.IsHumanPlayer)
			{
				PlayerMeetings.RevealAllUnitsOfPlayer(ctx.pid);
			}
			if (entity != null)
			{
				healthfraction = Fixnum.Clamp(healthfraction, 0, 1);
				Fixnum fixnum = entity.components.mobile.MaxHealth();
				entity.components.mobile.SetHealth(CrewAssignment.EMPTY, healthfraction * fixnum);
			}
		}
	}

	public override string Describe(GrantContext ctx)
	{
		string text = FindVehicle()?.mobile.locname;
		string text2 = ((text != null) ? Loc.Get(text) : "?");
		return Loc.Get("ui.grants.grantvehicle.describe", "name", text2);
	}

	private EntityConfig FindVehicle()
	{
		return Game.ctx.entityman.FindTemplate(id);
	}
}
