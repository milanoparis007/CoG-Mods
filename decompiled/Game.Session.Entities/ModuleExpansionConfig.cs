using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.Session.Entities;

public sealed class ModuleExpansionConfig
{
	public class Display
	{
		public string locicon;

		public string locname;

		public string locdesc;

		public string locInstallDetail;

		public string GetLocIcon()
		{
			return Loc.Get(locicon);
		}

		public string GetLocName()
		{
			return Loc.Get(locname);
		}

		public string GetLocDesc()
		{
			return Loc.Get(locdesc);
		}

		public string GetLocInstall()
		{
			return Loc.Get(locInstallDetail);
		}
	}

	public class Cost
	{
		public Price cashCost;
	}

	public Label id;

	public ModuleExpansionTarget target;

	public ModValue multiplier;

	public VisitRequirementList visreqs;

	public VisitRequirementList reqs;

	public Display display;

	public Cost purchase;
}
