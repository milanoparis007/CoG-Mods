using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class QuestMod : BaseDeltaMultiplierModifier
{
	public enum Type
	{
		Completed,
		Active
	}

	public string id;

	public Type @is;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	public override bool DoesPass(ModQuery query)
	{
		bool result = false;
		switch (@is)
		{
		case Type.Active:
			result = Game.ctx.quests.IsQuestActiveByID(id, EntityID.INVALID);
			break;
		case Type.Completed:
			result = Game.ctx.quests.IsQuestCompletedByID(id, EntityID.INVALID);
			break;
		}
		return result;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		string key = ((@is == Type.Completed) ? "mod.quest-completed" : "mod.quest-active");
		string name = Game.ctx.quests.FindQuestDefinition(id).GetName();
		return Loc.Get(key, "delta", AbstractModifier.FormatDelta(delta), "name", name);
	}
}
