using Game.UI.Session.Convo;

namespace Game.Session.Data;

public interface IConvoButtonRequirement : IRequirement
{
	bool DoesPass(VisitState visit, ConvoButtonState bstate);

	ReqExplanation Explain(VisitState visit, ConvoButtonState bstate);
}
