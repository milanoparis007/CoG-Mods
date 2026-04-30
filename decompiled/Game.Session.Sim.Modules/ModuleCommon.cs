using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.Session.Sim.Modules;

public sealed class ModuleCommon
{
	public sealed class Display
	{
		public static readonly Label ICON_MISSING = new Label("Module Missing");

		public static readonly Label ICON_ADD = new Label("Module Add");

		public static readonly Label CARD_MISSING = new Label("BG Card - Missing");

		public Label icon = ICON_MISSING;

		public string locicon;

		public string locname;

		public string locdesc;

		public string locInstallDetail;

		public string locintroflavor;

		public string replacementSprite;

		public string accentSprite;

		public string operationSprite;

		public string GetName()
		{
			if (locname == null)
			{
				return null;
			}
			return Loc.Get(locname);
		}

		public string GetDesc()
		{
			if (locdesc == null)
			{
				return null;
			}
			return Loc.Get(locdesc);
		}

		public string GetTextIcon()
		{
			if (locicon == null)
			{
				return null;
			}
			return Loc.Get(locicon);
		}
	}

	public TagList tags;

	public TagList skills;

	public Label upgradetag;

	public VisitRequirementList reqs;

	public VisitRequirementList visreqs;

	public Display display;

	public ModulePurchaseCost purchase;

	public VisitGrantList installgrants;
}
