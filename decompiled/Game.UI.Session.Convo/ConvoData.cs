using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Convo;

public abstract class ConvoData
{
	public abstract string[] MakeReplacements(VisitState visit, int index);

	public virtual string MakeIconOrNull()
	{
		return null;
	}

	public virtual bool IsConvoStepEnabled(VisitState visit, ConvoButtonState state)
	{
		return true;
	}

	public virtual string ExplainConvoStepNotEnabled(VisitState visit, ConvoButtonState state)
	{
		return null;
	}

	public virtual Entity GetVisitTopic()
	{
		return null;
	}

	public ConvoData CloneData()
	{
		return Game.serv.serializer.instance.Clone(this);
	}
}
