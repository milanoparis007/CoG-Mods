using Game.Services;
using Game.Session.Entities;
using Game.UI.Mouseovers;

namespace Game.UI.Session;

public sealed class PersonInfoHeaderMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		EntityHolderContext componentInParent = context.GetComponentInParent<EntityHolderContext>();
		if (componentInParent == null)
		{
			return null;
		}
		Entity targetEntityOrNull = componentInParent.GetTargetEntityOrNull();
		if (targetEntityOrNull == null)
		{
			return null;
		}
		PersonInfoUtil.Overview overview = PersonInfoUtil.GenerateOverview(targetEntityOrNull, details: true);
		string workplaceOrUnemployed = overview.GetWorkplaceOrUnemployed();
		string relAgeAndEth = overview.GetRelAgeAndEth();
		string text = Loc.Get("ui.personinfo.mouseover.short", "name", overview.name, "workplace", workplaceOrUnemployed, "details", relAgeAndEth);
		if (!string.IsNullOrWhiteSpace(overview.traitsShort))
		{
			text = text + "\n\n" + overview.traitsShort;
		}
		if (!string.IsNullOrWhiteSpace(overview.levelups))
		{
			text = text + "\n\n" + overview.levelups;
		}
		return text;
	}
}
