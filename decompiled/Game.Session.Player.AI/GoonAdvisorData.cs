using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class GoonAdvisorData : BaseAdvisorData
{
	public struct HarassmentRecord
	{
		public EntityID owner;

		public EntityID building;

		public SimTime timestamp;

		public string script;
	}

	public const int MAX_HARASSMENT_AGE_DAYS = 60;

	public List<HarassmentRecord> harassments = new List<HarassmentRecord>();

	public GoonLootTableRewards rewards;

	public EntityID businessToHarass;

	public Label harassTypePlanned;

	public Label goontype;

	public bool IsSpecial => goontype != AIConstants.OBSTACLE_GOON;

	public SpecialGoonConfig GetSpecialGoonConfig()
	{
		return Game.serv.globals.settings.npc.goons.FindSpecialGoonConfig(goontype);
	}

	public void PickGoonType(PlayerInfo player)
	{
		if (goontype.IsSet || !player.IsJustGoon)
		{
			return;
		}
		goontype = AIConstants.OBSTACLE_GOON;
		Fixnum probability = Game.serv.globals.settings.npc.goons.specialGoonChance.Evaluate(player.PID);
		if (rng.CheckProbability(probability))
		{
			BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(player.territory.Safehouse);
			VisitState visit = new VisitState(CrewAssignment.EMPTY, bbdata, Game.ctx.clock.Now, player.PID);
			Label label = Game.serv.globals.settings.npc.goons.FindFirstConfigPassingReqs(visit);
			if (label.IsSet)
			{
				goontype = label;
			}
		}
	}

	private void TrimExpiredHarassments()
	{
		SimTime now = Game.ctx.clock.Now;
		while (harassments.Count > 0 && (now - harassments.FirstOrDefaultFast().timestamp).deltadays > 60)
		{
			harassments.RemoveAt(0);
		}
	}

	public void RecordHarassment(EntityID ownerId, EntityID buildingId, string script)
	{
		TrimExpiredHarassments();
		harassments.Add(new HarassmentRecord
		{
			building = buildingId,
			owner = ownerId,
			timestamp = Game.ctx.clock.Now,
			script = script
		});
	}

	public bool DidHarassOwner(EntityID owner)
	{
		TrimExpiredHarassments();
		int i = 0;
		for (int count = harassments.Count; i < count; i++)
		{
			if (harassments[i].owner == owner)
			{
				return true;
			}
		}
		return false;
	}

	public bool DidHarassAnyone()
	{
		TrimExpiredHarassments();
		return harassments.Count > 0;
	}

	public List<HarassmentRecord> CopyAndClearAllHarassments()
	{
		TrimExpiredHarassments();
		List<HarassmentRecord> result = new List<HarassmentRecord>(harassments);
		harassments.Clear();
		return result;
	}
}
