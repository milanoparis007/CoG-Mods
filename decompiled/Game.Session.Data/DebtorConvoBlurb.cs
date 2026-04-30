using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class DebtorConvoBlurb : ConvoBlurb
{
	public enum Ctx
	{
		Intro,
		HumanBlurb,
		NPCBlurb,
		BeforeChoice,
		None
	}

	public Ctx ctx = Ctx.None;

	public override string GetBlurb(ConversationModel model, ConvoButton __, string[] replacements)
	{
		Entity npc = model.visit.npc;
		GamblerState gamblerState = Game.ctx.players.Human.gambling.FindGamblerState(npc);
		if (gamblerState == null)
		{
			Logger.Warning("Attempted to speak to " + npc.data.person.FullName + " at their home, but they're not a gambler?");
			return "";
		}
		DebtLevelDef debtLevelDef = Game.ctx.players.Human.gambling.FindCurrentDebtLevel(gamblerState);
		switch (ctx)
		{
		case Ctx.Intro:
		{
			string npcintro = debtLevelDef.convo.npcintro;
			object[] replacements2 = replacements;
			return Loc.Get(npcintro, replacements2);
		}
		case Ctx.HumanBlurb:
		{
			string humanblurb = debtLevelDef.convo.humanblurb;
			object[] replacements2 = replacements;
			return Loc.Get(humanblurb, replacements2);
		}
		case Ctx.NPCBlurb:
		{
			string npcblurb = debtLevelDef.convo.npcblurb;
			object[] replacements2 = replacements;
			return Loc.Get(npcblurb, replacements2);
		}
		case Ctx.BeforeChoice:
		{
			string beforechoice = debtLevelDef.convo.beforechoice;
			object[] replacements2 = replacements;
			return Loc.Get(beforechoice, replacements2);
		}
		case Ctx.None:
			Logger.Warning("No ctx given for DebtorConvoBlurb to generate text");
			return "";
		default:
			return "";
		}
	}
}
