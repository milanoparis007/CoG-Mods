using Game.Core;
using Game.Services;
using Game.Session.Quests;

namespace Game.Session.Data;

public class AddTicketsWithTarget : VisitGrant
{
	public int count = 1;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitNpc | GrantReq.QuestUUID;

	public override void Apply(GrantContext ctx)
	{
		QuestCompletedRecord questCompletedRecord = Game.ctx.quests.FindCompletedQuestUnsafe(ctx.quuid);
		if (questCompletedRecord == null)
		{
			Logger.Warning($"Count not find completed quest for this grant: {ctx.quuid} / {this}");
			return;
		}
		EntityID target = questCompletedRecord.target;
		if (target.IsNotValid)
		{
			Logger.Warning("Target not set for add ticket quest - failing out");
		}
		else
		{
			ctx.GetPlayer().social.GrantFreebieTickets(target, count);
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addtickets.describe", "count", count);
	}
}
