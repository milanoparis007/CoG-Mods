using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Core;

public class HeatPerPlayer : PlayerKeyedList<Heat>
{
	public void ResetPerTurnHeat()
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			data[i].ResetPerTurnValue();
		}
	}

	public void AddViolenceBuff(PlayerID pid, EntityID crewpeep, Node node, bool someoneDied)
	{
		Label id = (someoneDied ? BuffConstants.HEAT_KILLING : BuffConstants.HEAT_VIOLENCE);
		GetOrAdd(pid).AddBuff(id, new ModQuery(pid, EntityID.INVALID, crewpeep, node.id), crewpeep);
		pid.FindPlayer().territory.RecomputeHeat(node, forceCurrent: true);
	}

	public void IncrementUnpopularity(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromUnpopularity += delta;
	}

	public void IncrementFromNeighbors(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromNeighbors += delta;
	}

	public void IncrementFromIllegalBusiness(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromBusinesses += delta;
	}

	public void IncrementFromGambling(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromGambling += delta;
	}

	public void AddTradeHeatBuff(PlayerID pid, Entity building, EntityID crewpeep, Node node, bool recurring)
	{
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
		if (entity != null)
		{
			ModQuery query = new ModQuery(pid, entity.Id, crewpeep, node.id);
			Label id = (recurring ? BuffConstants.HEAT_TRADE_RECURRING : BuffConstants.HEAT_TRADE_ONETIME);
			GetOrAdd(pid).AddBuff(id, query, crewpeep);
			GetOrAdd(pid).AddBuff(BuffConstants.HEAT_FROM_CREW_PERSONALITY, query, crewpeep);
			pid.FindPlayer().territory.RecomputeHeat(node, forceCurrent: true);
		}
	}
}
