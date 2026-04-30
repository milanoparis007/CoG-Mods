using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckIsConvoInsideTerritory : AbstractVisitRequirement
{
	public enum Type
	{
		Human,
		AI,
		Goon,
		Gang,
		Any,
		None
	}

	public Type player;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerID pid = visit.GetBldgNode().owner.Get();
		bool flag = false;
		switch (player)
		{
		case Type.Human:
			flag = pid.IsHumanPlayer;
			break;
		case Type.AI:
			flag = pid.IsAIPlayer;
			break;
		case Type.Gang:
			flag = pid.IsAIPlayer && pid.FindPlayer().IsJustGang;
			break;
		case Type.Goon:
			flag = pid.IsAIPlayer && pid.FindPlayer().IsJustGoon;
			break;
		case Type.Any:
			flag = pid.IsAnyPlayer;
			break;
		case Type.None:
			flag = pid.IsNotValid;
			break;
		default:
			Logger.Warning("Unknown player type", player);
			break;
		}
		return flag == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = string.Empty;
		switch (player)
		{
		case Type.Human:
			text += (expected ? Loc.Get("ui.requirements.insideterritory.human.expected") : Loc.Get("ui.requirements.insideterritory.human.unexpected"));
			break;
		case Type.AI:
			text += (expected ? Loc.Get("ui.requirements.insideterritory.ai.expected") : Loc.Get("ui.requirements.insideterritory.ai.unexpected"));
			break;
		case Type.Gang:
			text += (expected ? Loc.Get("ui.requirements.insideterritory.gang.expected") : Loc.Get("ui.requirements.insideterritory.gang.unexpected"));
			break;
		case Type.Goon:
			text += (expected ? Loc.Get("ui.requirements.insideterritory.goon.expected") : Loc.Get("ui.requirements.insideterritory.goon.unexpected"));
			break;
		case Type.Any:
			text += (expected ? Loc.Get("ui.requirements.insideterritory.any.expected") : Loc.Get("ui.requirements.insideterritory.any.unexpected"));
			break;
		case Type.None:
			text += (expected ? Loc.Get("ui.requirements.insideterritory.none.expected") : Loc.Get("ui.requirements.insideterritory.none.unexpected"));
			break;
		}
		return new ReqExplanation(DoesPass(visit), text);
	}
}
