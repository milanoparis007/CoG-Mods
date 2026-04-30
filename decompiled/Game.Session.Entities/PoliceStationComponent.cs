using System.Collections.Generic;
using Game.Core;
using Game.Session.Player;
using Game.Session.Player.AI;

namespace Game.Session.Entities;

public class PoliceStationComponent : BaseComponent
{
	public bool HasStation => _entity.data.police.precinctID.IsValid;

	public void AssignOfficerToStation(EntityID officerId)
	{
		_entity.data.police.officers.Add(officerId);
	}

	public void AssignBeatToOfficer(EntityID officer, List<NodeID> nodes)
	{
		PrecinctAdvisor.AssignBeatToOfficer(_entity.data.police.copPlayerId.FindPlayer().ai, officer, nodes);
	}

	internal void OnStationActivationChange(bool activated)
	{
		bool flag = _entity.data.building.scoped.Get(Game.ctx.players.Human.PID);
		if (activated && flag)
		{
			Game.ctx.hud.station.Show(_entity, CrewAssignment.EMPTY);
		}
		else
		{
			Game.ctx.hud.station.Hide();
		}
	}
}
