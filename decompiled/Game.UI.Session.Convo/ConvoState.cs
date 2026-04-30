using Game.Services;

namespace Game.UI.Session.Convo;

public sealed class ConvoState
{
	public ConvoState parent;

	public ConvoStateDef def;

	public ConvoData data;

	public ConvoState(ConvoStateDef def, ConvoState parent, ConvoData data)
	{
		this.def = def;
		this.parent = parent;
		this.data = data;
	}

	public string ProduceNPCBlurb(ConversationModel model)
	{
		if (def?.npcsays == null)
		{
			return null;
		}
		string[] replacements = data?.MakeReplacements(model.visit, 0);
		return def.npcsays.GetFirstBlurb(model, null, replacements);
	}
}
