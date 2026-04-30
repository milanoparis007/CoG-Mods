using System.Collections.Generic;

namespace Game.Session.Data;

public class CheckBuildingType : AbstractVisitRequirement
{
	public enum CheckType
	{
		Business,
		Civic,
		Residence
	}

	public enum Type
	{
		Any,
		All,
		None
	}

	public Type has;

	public List<CheckType> of;

	public override bool DoesPass(VisitState visit)
	{
		int num = ((of != null) ? of.Count : 0);
		int num2 = ((of != null) ? Count(visit) : 0);
		return has switch
		{
			Type.All => num2 == num, 
			Type.Any => num2 > 0, 
			Type.None => num2 == 0, 
			_ => false, 
		};
	}

	protected int Count(VisitState visit)
	{
		int num = 0;
		foreach (CheckType item in of)
		{
			if (DoesMatchExpectedStatus(visit, item))
			{
				num++;
			}
		}
		return num;
	}

	private static bool DoesMatchExpectedStatus(VisitState visit, CheckType type)
	{
		return type switch
		{
			CheckType.Business => visit.biz != null, 
			CheckType.Civic => visit.building.config.civic != null, 
			CheckType.Residence => visit.building.config.residence != null, 
			_ => false, 
		};
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
