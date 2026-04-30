using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class DemandTransition
{
	public bool atOwner;

	public bool atGoon;

	public bool atGang;

	public ModValue requestToComplyPercent;

	public ModValue forceToComplyPercent;

	public ModValue bribeToComplyPercent;

	public ModValue bribeToComplyAmount;

	public Label forceToComplySocial;

	public Label forceToComplyHeatBuff;

	public List<PhotoConfig> photos;

	public bool explainCompliance;
}
