using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BotL;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class SocialHistoryData
{
	private const string PROCESS_INFERENCES = "processSocialInferences";

	public List<SocialActionInfo> items = new List<SocialActionInfo>();

	public SocialLink link = SocialLink.Acquaintance;

	private static RelationshipSettings Settings => Game.serv.globals.settings.people.social.relationships;

	public void Initialize(Relationship rel)
	{
		link = ((!rel.IsSelf) ? (rel.IsAnyFamily ? SocialLink.Family : SocialLink.Acquaintance) : SocialLink.Self);
	}

	private void ExpireAndAdd(SocialActionInfo item, SocialActionDef def)
	{
		TrimCanceled(def);
		ExpireOldHistory();
		items.Add(item);
		if (items.Count > 1)
		{
			items.StableSort(SocialActionInfo.CompareExpiryAscending);
		}
	}

	private void TrimCanceled(SocialActionDef def)
	{
		if (def.cancels == null)
		{
			return;
		}
		for (int num = items.Count - 1; num >= 0; num--)
		{
			if (def.cancels.Contains(items[num].defid))
			{
				items.RemoveAt(num);
			}
		}
	}

	public void ExpireOldHistory()
	{
		if (items.Count != 0)
		{
			SimTime now = Game.ctx.clock.Now;
			while (items.Count > 0 && items[0].expires.days < now.days)
			{
				items.RemoveAt(0);
			}
		}
	}

	public void ExpireSpecificAction(Label socialAction)
	{
		int num = IndexOf(socialAction);
		if (num >= 0)
		{
			items.RemoveAt(num);
		}
	}

	internal IEnumerable<SocialActionInfo> GetHistory()
	{
		ExpireOldHistory();
		return items;
	}

	internal SocialActionInfo? GetRandomHistoryOrNull(IRandom rng)
	{
		ExpireOldHistory();
		if (items.Count <= 0)
		{
			return null;
		}
		return rng.PickElement(items);
	}

	public int IndexOf(SocialCategory? category = null, SocialValence? valence = null)
	{
		int i = 0;
		for (int count = items.Count; i < count; i++)
		{
			SocialActionInfo socialActionInfo = items[i];
			if ((!category.HasValue || category.Value == socialActionInfo.category) && (!valence.HasValue || valence.Value == socialActionInfo.valence))
			{
				return i;
			}
		}
		return -1;
	}

	public int IndexOf(Label defId)
	{
		int i = 0;
		for (int count = items.Count; i < count; i++)
		{
			if (items[i].defid == defId)
			{
				return i;
			}
		}
		return -1;
	}

	public void InformOfSocialAction(Label socialAction, Relationship rel, EntityID entityCtx, EntityID sourceId, QuestUUID quuid, bool inferred, ExtendHistoryInfo ext = null)
	{
		SocialActionDef socialActionDefinition = Settings.GetSocialActionDefinition(socialAction);
		if (socialActionDefinition != null)
		{
			SimTime expires = Game.ctx.clock.Now.IncrementDays(socialActionDefinition.dayz);
			SocialActionInfo socialActionInfo = socialActionDefinition.MakeInfoFromDefinition(entityCtx, quuid, expires);
			ExpireAndAdd(socialActionInfo, socialActionDefinition);
			AddBuffs(rel, socialActionInfo, sourceId);
			if (!inferred && HumanInvolved(rel.to, rel.from) && !socialActionDefinition.dontinfer)
			{
				ReprocessInferences(rel, entityCtx, socialAction);
			}
			if (!inferred && socialActionDefinition.flushQuestRequests)
			{
				FlushQuestRequests(rel.to, rel.from);
			}
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.SocialActionPerformed, rel.from, Game.ctx.clock.CurrentPlayer, socialAction));
			PlayerSocial.DebugLogSocialHistory(rel, socialActionInfo, ext);
		}
	}

	public void RemoveAllSocialActions(Relationship rel)
	{
		foreach (SocialActionInfo item in items)
		{
			RemoveBuffs(rel, item);
		}
		items.Clear();
	}

	[Conditional("UNITY_EDITOR")]
	private void DebugSocialAction(Label socialAction, Relationship rel)
	{
	}

	private void FlushQuestRequests(EntityID actor, EntityID target)
	{
		PlayerID pid = actor.FindEntity().data.agent.pid;
		if (pid.IsAnyPlayer)
		{
			Game.ctx.quests.Requests.ClearUnusedRequestsFor(target);
			Entity building = BuildingUtil.FindBuildingForBizOwner(target);
			Game.ctx.players.Human.kb.FlushAllForBuilding(building);
		}
	}

	private void ReprocessInferences(Relationship rel, EntityID ctx, Label socialaction)
	{
		Game.ctx.botl.entityid.Value.SetReference(rel.from);
		Game.ctx.botl.socialaction.Value.SetReference(socialaction);
		Game.ctx.botl.result.Value.SetReference(null);
		Game.ctx.botl.IsTrue("processSocialInferences");
		TaggedValue value = Game.ctx.botl.result.Value;
		if (value.reference == null || !(value.reference is ArrayList arrayList))
		{
			return;
		}
		Xorshift rng = Game.ctx.scenario.MakeSeededRng((uint)(Game.ctx.clock.Now.days + rel.from.index + rel.to.index));
		foreach (object item in arrayList)
		{
			if (item is SocialInferencePayload payload)
			{
				float inferenceSkipChance = Settings.GetInferenceSkipChance(payload.link);
				if (!rng.CheckProbability(inferenceSkipChance))
				{
					InformOfInferredEvent(payload, ctx);
				}
			}
		}
	}

	private void AddBuffs(Relationship rel, SocialActionInfo info, EntityID crewpeep)
	{
		if (info.buffid.IsSet && !rel.HasBuff(info.buffid))
		{
			rel.AddBuff(info.buffid, crewpeep);
		}
	}

	private void RemoveBuffs(Relationship rel, SocialActionInfo info)
	{
		if (info.buffid.IsSet && rel.HasBuff(info.buffid))
		{
			rel.RemoveBuff(info.buffid);
		}
	}

	private void InformOfInferredEvent(SocialInferencePayload payload, EntityID ctx)
	{
		EntityID to = payload.to;
		EntityID sourceId = payload.from;
		Relationship orCreate = Game.ctx.simman.rels.GetOrCreate(sourceId, to, RelationshipType.Acquaintance, warnOnExisting: false);
		orCreate.GetOrCreateHistory().InformOfSocialAction(payload.label, orCreate, ctx, EntityID.INVALID, QuestUUID.EMPTY, inferred: true);
	}

	private static bool HumanInvolved(EntityID e1, EntityID e2)
	{
		AgentData agent = e1.FindEntity().data.agent;
		AgentData agent2 = e2.FindEntity().data.agent;
		if (agent == null || !agent.pid.IsHumanPlayer)
		{
			return agent2?.pid.IsHumanPlayer ?? false;
		}
		return true;
	}

	public int CountActions(List<Label> actions)
	{
		ExpireOldHistory();
		int num = 0;
		foreach (SocialActionInfo item in items)
		{
			if (actions.Contains(item.defid))
			{
				num++;
			}
		}
		return num;
	}

	public bool ContainsAction(Label action)
	{
		return ContainsAction(action.String);
	}

	public bool ContainsAction(string action)
	{
		ExpireOldHistory();
		foreach (SocialActionInfo item in items)
		{
			if (item.defid == (Label)action)
			{
				return true;
			}
		}
		return false;
	}
}
