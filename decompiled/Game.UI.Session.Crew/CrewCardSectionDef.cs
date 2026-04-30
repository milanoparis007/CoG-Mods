using System;

namespace Game.UI.Session.Crew;

public sealed class CrewCardSectionDef
{
	public string name;

	public CrewCardType type;

	public Func<CrewCardInfoInitData, CrewCardInfoInitData, bool> comparator;

	public CrewCardSectionDef(CrewCardType type, string name, Func<CrewCardInfoInitData, CrewCardInfoInitData, bool> comparator)
	{
		this.name = name;
		this.type = type;
		this.comparator = comparator;
	}
}
