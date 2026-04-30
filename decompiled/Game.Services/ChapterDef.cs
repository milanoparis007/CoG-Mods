using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class ChapterDef
{
	public enum RandomizeStyle
	{
		None,
		RandomizeSuccess,
		RandomizeFail,
		RandomizeBoth
	}

	public enum GenerateOptions
	{
		None,
		ForTakeover,
		ForStalledProductions,
		ForAggroRivals
	}

	public Label id;

	public string loctitle;

	public string locstartoc;

	public string locstartic;

	public string locendoc;

	public string locendicsuccess;

	public string locendicfail;

	public string bannerImage;

	public ModValue successChance;

	public ModValue timeCostDays;

	public Label script;

	public Label recoveryScript;

	public List<Decision> successChoices = new List<Decision>();

	public List<Decision> failChoices = new List<Decision>();

	public bool selectTarget;

	public RandomizeStyle randomizeChoiceDisplay;

	public GenerateOptions optionGeneration;

	public Decision optionTemplate;
}
