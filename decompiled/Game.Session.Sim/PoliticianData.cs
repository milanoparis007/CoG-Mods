using Game.Core;

namespace Game.Session.Sim;

public sealed class PoliticianData
{
	public EntityID id;

	public EntityID location;

	public PoliticalType type;

	public int incumbencies;

	public Label archetypeId;

	public PoliticalRelationshipType relToHumanInLastElection;

	public PoliticianData()
	{
	}

	public PoliticianData(EntityID id, EntityID location, PoliticalType type, Label archetypeId)
	{
		this.id = id;
		this.location = location;
		this.type = type;
		incumbencies = 0;
		this.archetypeId = archetypeId;
		relToHumanInLastElection = PoliticalRelationshipType.None;
	}
}
