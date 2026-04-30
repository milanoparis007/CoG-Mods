using Game.Core;

namespace Game.Session.Data;

public class CheckLocationDistrictTags : CheckListOfItems
{
	protected override int Count(VisitState visit)
	{
		Node node = visit.building?.components.board?.GetNode();
		if (node == null)
		{
			Logger.Warning("Passed invalid building", visit.building, "to req", this);
			return 0;
		}
		if (!node.IsInAnyDistrict)
		{
			return 0;
		}
		int num = 0;
		foreach (NodeDistrictData district in node.districts)
		{
			TagList districtTags = district.GetDistrictTags();
			if (districtTags == null)
			{
				continue;
			}
			foreach (Label item in districtTags)
			{
				if (of.Contains(item))
				{
					num++;
				}
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState _)
	{
	}

	protected override string IdToName(Label id)
	{
		return null;
	}

	protected override string MakeExplanationText()
	{
		return null;
	}
}
