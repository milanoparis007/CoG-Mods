using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class GamblingRepayment
{
	public struct RedeemConvo
	{
		public string humanintro;

		public string npcintro;

		public string humanblurb;

		public string humanblurbmo;

		public string npcaccept;

		public string humanaccept;

		public string ticker;
	}

	public Label id;

	public ModValue timeDayz;

	public VisitGrantList grants;

	public VisitRequirementList visreqs;

	public VisitRequirementList reqs;

	public RedeemConvo convo;
}
