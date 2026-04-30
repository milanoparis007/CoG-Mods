using Game.Core;
using Game.Session.Data;
using Game.UI.Session.Convo;

namespace Game.Services;

public class NextStateDef
{
	public ConvoButtonRequirementList reqs;

	public Label then;

	public Label @else;

	public Label @goto;

	public virtual Label Evaluate(VisitState visit, ConvoButtonState bstate)
	{
		if (reqs == null)
		{
			return @goto;
		}
		if (!reqs.AllPass(visit, bstate))
		{
			return @else;
		}
		return then;
	}

	public virtual void Verify(ConvoButtonDef button)
	{
		_ = reqs;
		_ = @goto.IsSet;
	}
}
