using Game.Core;

namespace Game.Services;

public sealed class NPCDefinition
{
	public PlayerType type;

	public Label units;

	public Label social;

	public Label safehouse;

	public Label territory;

	public Label business;

	public Label precinct;

	public Label feds;

	public Label goon;

	public Label combat;

	public Label attack;

	public static NPCDefinition UnifyLabelsWithPersonality(NPCDefinition baseDef, NPCPersonalityDefinition personality)
	{
		NPCDefinition nPCDefinition = Game.serv.serializer.instance.Clone(baseDef);
		if (personality.units.IsSet)
		{
			nPCDefinition.units = personality.units;
		}
		if (personality.social.IsSet)
		{
			nPCDefinition.social = personality.social;
		}
		if (personality.safehouse.IsSet)
		{
			nPCDefinition.safehouse = personality.safehouse;
		}
		if (personality.territory.IsSet)
		{
			nPCDefinition.territory = personality.territory;
		}
		if (personality.business.IsSet)
		{
			nPCDefinition.business = personality.business;
		}
		if (personality.precinct.IsSet)
		{
			nPCDefinition.precinct = personality.precinct;
		}
		if (personality.feds.IsSet)
		{
			nPCDefinition.feds = personality.feds;
		}
		if (personality.combat.IsSet)
		{
			nPCDefinition.combat = personality.combat;
		}
		if (personality.attack.IsSet)
		{
			nPCDefinition.attack = personality.attack;
		}
		if (personality.goon.IsSet)
		{
			nPCDefinition.goon = personality.goon;
		}
		return nPCDefinition;
	}
}
