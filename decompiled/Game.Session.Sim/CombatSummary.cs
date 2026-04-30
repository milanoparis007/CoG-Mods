using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Sim;

public struct CombatSummary
{
	public string summary;

	public List<PhotoConfig> photos;

	public bool anyoneDied;

	public NodeID corner;

	public string cornerName;

	public string groupName;

	public CrewDeathEntry humanCrewDied;
}
