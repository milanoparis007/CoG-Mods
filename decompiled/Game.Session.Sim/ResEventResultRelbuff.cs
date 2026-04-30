using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public class ResEventResultRelbuff : ResEventResultBase
{
	public bool relbuffenemy;

	public override ResEventResultData GenerateResultData(VisitState visit)
	{
		Entity entity = FindMostLukewarmConnection(visit, relbuffenemy, fallback: false);
		if (entity == null)
		{
			FindMostLukewarmConnection(visit, selectEnemy: false, fallback: true);
		}
		ResEventResultData resEventResultData = new ResEventResultData
		{
			resultId = id,
			targetNpc = entity.Id
		};
		PopulateEventMessage(resEventResultData);
		return resEventResultData;
	}

	private Entity FindMostLukewarmConnection(VisitState visit, bool selectEnemy, bool fallback)
	{
		return (from rel in (from rel in (from rel in visit.GetPlayer().social.GetAllPlayerRelationshipsUnsafe()
					where rel.to.FindEntity()?.data.person?.IsAlive == true
					select Game.ctx.simman.rels.GetOrNull(rel.to, rel.@from)).Where(delegate(Relationship rel)
				{
					Fixnum fixnum = rel?.Evaluate().current ?? Fixnum.ZERO;
					return (!fallback) ? ((!selectEnemy) ? (fixnum >= 0) : (fixnum < 0)) : fixnum.IsNotZero;
				})
				where !rel.HasBuff(BuffConstants.TICKET_NPCBOOST)
				select rel).ToList()
			orderby rel.Evaluate().current descending
			select rel).FirstOrDefault()?.from.FindEntity();
	}

	public override void AcceptResult(VisitState visit, ResEventData data)
	{
		EntityID targetNpc = data.chosenResult.targetNpc;
		visit.GetPlayer().social.TicketActionPerformBoost(targetNpc);
		string peepFullName = NameUtils.GetPeepFullName(targetNpc);
		string message = Loc.Get("convo.select.boost.result", "name", peepFullName);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.EVENT_UPDATE, TickerTitle.EVENT_UPDATE, message, targetNpc);
	}
}
