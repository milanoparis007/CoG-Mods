using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session;

namespace Game.Session.Data;

public class GrantCasinoWithAmenities : VisitGrant
{
	public List<Label> amenityIds;

	public Label moduleId;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		List<BuildingUtil.PotentialGamblingHouse> potentialGamblingHousesOnVisit = BuildingUtil.GetPotentialGamblingHousesOnVisit(ctx.visit);
		Entity entity = ((potentialGamblingHousesOnVisit.Count() > 0) ? potentialGamblingHousesOnVisit : BuildingUtil.GetPotentialGamblingHousesOnVisit(new VisitState(ctx.GetPlayer().crew.GetCrewForPlayerPeep(), BuildingUtil.FindDataForBuilding(ctx.GetPlayer().territory.Safehouse), Game.ctx.clock.Now, ctx.pid))).First().targetId.FindEntity();
		Label label = ((Game.ctx.session.mapconfig.id == "atlantic-city") ? new Label("casino-small") : moduleId);
		ctx.pid.FindPlayer().territory.ScopeOutAndTakeOverResidence(entity);
		ctx.pid.FindPlayer().gambling.InstallGamblingModule(entity, label);
		Game.ctx.players.Human.crew.CrewGrowth.OnPlayerTurnStarted(Game.ctx.players.Human.crew);
		GamblingModule gambling = entity.components.modules.gambling;
		GamblingSettings gambling2 = Game.serv.globals.settings.gambling;
		foreach (Label amenityId in amenityIds)
		{
			ctx.pid.FindPlayer().gambling.DoCreateAmenity(gambling2.FindAmenityById(amenityId), new VisitState(ctx.visit.crew, BuildingUtil.FindDataForBuilding(entity), Game.ctx.clock.Now, ctx.pid), gambling, forceFree: true);
		}
		PersonInfoUtil.TweenCameraToEntity(entity.Id);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerBuildingTakeoverImmediate, entity.Id, PlayerID.HumanPlayer));
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.gambling-house-grant");
	}
}
