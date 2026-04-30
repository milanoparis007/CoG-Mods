using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class ConvoButtonDef
{
	public sealed class ConvoSimpleText
	{
		public ConvoBlurbList dynkey;

		public ConvoBlurbList dyndis;

		public ConvoBlurbList dynicon;

		public ConvoBlurbList dynmo;

		public ConvoBlurbList dynnpc;

		public string key;

		public string dis;

		public string icon;

		public string mo;

		public string npc;

		public bool addmo;
	}

	public ConvoButtonRequirementList visreqs;

	public ConvoButtonRequirementList reqs;

	public ConvoSimpleText text;

	public string onShow;

	public string onClick;

	public string onPreshow;

	public NextStateDef next;

	public Fixnum multiplier;

	public Label definition;

	public VisitGrantList grants;

	public QuickInfoType quickType;
}
