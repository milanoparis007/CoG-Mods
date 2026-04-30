using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Services;

public sealed class DebtLevelDef
{
	public struct DebtConvo
	{
		public string npcintro;

		public string humanblurb;

		public string npcblurb;

		public string beforechoice;
	}

	public Label id;

	public bool initial;

	public ModValue cash;

	public DebtConvo convo;

	public List<GamblingRepayChoice> repayments = new List<GamblingRepayChoice>();

	public Fixnum EvaluateCashForGambler(PlayerID pid, Entity gambler)
	{
		return cash.Evaluate(new ModQuery(pid, gambler.Id, NodeID.INVALID));
	}
}
