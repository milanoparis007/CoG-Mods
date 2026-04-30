using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class DebtorRepaymentStepBlurb : ConvoBlurb
{
	public enum Ctx
	{
		HumanIntro,
		NPCIntro,
		HumanBlurb,
		HumanBlurbMO,
		NPCAccept,
		HumanAccept,
		None
	}

	public Ctx ctx = Ctx.None;

	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		if (!((button?.state.data ?? model.state?.data) is ConvoDataGamblingDebtor convoDataGamblingDebtor))
		{
			return null;
		}
		GamblingRepayment.RedeemConvo convo = Game.serv.globals.settings.gambling.FindRepaymentById(convoDataGamblingDebtor.selected).convo;
		string text = null;
		switch (ctx)
		{
		case Ctx.HumanIntro:
			text = convo.humanintro;
			break;
		case Ctx.NPCIntro:
			text = convo.npcintro;
			break;
		case Ctx.HumanBlurb:
			text = convo.humanblurb;
			break;
		case Ctx.HumanBlurbMO:
			text = convo.humanblurbmo;
			break;
		case Ctx.NPCAccept:
			text = convo.npcaccept;
			break;
		case Ctx.HumanAccept:
			text = convo.humanaccept;
			break;
		default:
			Logger.Warning("No ctx given for DebtorConvoBlurb to generate text");
			break;
		}
		if (text == null)
		{
			return "";
		}
		return Loc.Get(text, replacements);
	}
}
