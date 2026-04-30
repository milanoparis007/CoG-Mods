using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Entities;

public class PoliceStationData : BaseData
{
	public PlayerID copPlayerId;

	public PrecinctID precinctID = PrecinctID.INVALID;

	public List<EntityID> officers = new List<EntityID>();
}
