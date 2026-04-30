using System.Collections.Generic;
using System.Text;
using Game.Services;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class ConvoButtonRequirementList : List<IRequirement>
{
	public bool AllPass(VisitState state, ConvoButtonState bstate)
	{
		for (int i = 0; i < Count; i++)
		{
			IRequirement requirement = this[i];
			if (!(requirement is IConvoButtonRequirement convoButtonRequirement))
			{
				if (requirement is IVisitRequirement visitRequirement && !visitRequirement.DoesPass(state))
				{
					return false;
				}
			}
			else if (!convoButtonRequirement.DoesPass(state, bstate))
			{
				return false;
			}
		}
		return true;
	}

	public string Explain(VisitState state, ConvoButtonState bstate)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		for (int i = 0; i < Count; i++)
		{
			ReqExplanation reqExplanation = default(ReqExplanation);
			IRequirement requirement = this[i];
			if (!(requirement is IConvoButtonRequirement convoButtonRequirement))
			{
				if (requirement is IVisitRequirement visitRequirement)
				{
					reqExplanation = visitRequirement.Explain(state);
				}
			}
			else
			{
				reqExplanation = convoButtonRequirement.Explain(state, bstate);
			}
			if (reqExplanation.message != null && reqExplanation.message.Length > 0)
			{
				stringBuilder.AppendLine(Loc.IconLine(reqExplanation.passed, reqExplanation.message));
			}
			else
			{
				_ = reqExplanation.message;
			}
		}
		return stringBuilder.ToStringAndReturnToPool();
	}
}
