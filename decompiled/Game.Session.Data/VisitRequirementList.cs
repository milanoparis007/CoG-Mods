using System.Collections.Generic;
using System.Text;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class VisitRequirementList : List<IVisitRequirement>
{
	public bool AllPass(VisitState visit)
	{
		return AllPassHelper(visit).passed;
	}

	public (bool pass, string explanation) AllPassExplainOnFailure(VisitState visit)
	{
		bool num = AllPass(visit);
		string item = (num ? null : Explain(visit));
		return (pass: num, explanation: item);
	}

	public bool AllPassLogging(VisitState visit, string logkey)
	{
		return AllPassHelper(visit).passed;
	}

	public (bool passed, IVisitRequirement failedReq) AllPassHelper(VisitState visit)
	{
		for (int i = 0; i < Count; i++)
		{
			IVisitRequirement visitRequirement = this[i];
			if (!visitRequirement.DoesPass(visit))
			{
				return (passed: false, failedReq: visitRequirement);
			}
		}
		return (passed: true, failedReq: null);
	}

	public string Explain(VisitState visit)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				ReqExplanation reqExplanation = enumerator.Current.Explain(visit);
				if (reqExplanation.message != null)
				{
					stringBuilder.AppendLine(Loc.IconLine(reqExplanation.passed, reqExplanation.message));
				}
			}
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	public int CountPass(VisitState visit)
	{
		int num = 0;
		for (int i = 0; i < Count; i++)
		{
			if (this[i].DoesPass(visit))
			{
				num++;
			}
		}
		return num;
	}
}
