using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Data;

public class KillCrew : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		Entity peep = ctx.visit.crew.GetPeep();
		Node node = ctx.GetPlayer().territory.Safehouse.FindEntity().components.board.GetNode();
		Game.ctx.simman.peoplegen.MarkAsDead(peep, Game.ctx.clock.Now);
		if (peep.data.agent.IsInHumanCrew)
		{
			CombatManager.ShowCrewDied(new CombatSummary
			{
				summary = Loc.Get("ui.scheme.death-summary"),
				photos = new List<PhotoConfig> { Game.serv.globals.ui.photos.combat.resultDeath },
				anyoneDied = true,
				humanCrewDied = new CrewDeathEntry
				{
					choice = CrewDeathEntry.PlayerChoice.Ignore,
					perTurn = Game.ctx.players.Human.finances.GetCrewSalary(peep.Id, isDead: true, explain: false).salary,
					peepId = peep.Id
				},
				corner = node.id,
				cornerName = node.GetCornerNameShort(),
				groupName = ctx.GetPlayer().social.FindPlayerGroupNameColorized()
			});
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.kill-crew", "name", ctx.visit.crew.GetPeep().data.person.FullName);
	}
}
