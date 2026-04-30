using System.Diagnostics;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class GoonLootTableEntry
{
	public Label id;

	public string locdesc;

	public ModValue selectionweight;

	public ModValue readyafterdayz;

	public ModValue expirationdayz;

	public ModValue cooldowndayz;

	public ModValue cashbuyin;

	public RandomRangeF cashbuyinmultiplier;

	public VisitRequirementList visreqs;

	public VisitGrantList grants;

	[Conditional("UNITY_EDITOR")]
	public void ValidateEntry()
	{
		_ = cashbuyinmultiplier;
	}
}
