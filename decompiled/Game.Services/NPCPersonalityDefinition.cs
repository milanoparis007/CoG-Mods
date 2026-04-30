using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class NPCPersonalityDefinition
{
	public Label id;

	public NPCPersonalityCategory npccategory;

	public ModValue aspects;

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
}
