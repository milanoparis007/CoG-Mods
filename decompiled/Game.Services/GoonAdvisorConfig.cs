using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class GoonAdvisorConfig : AIAdvisorConfig
{
	public struct ScriptWeightPair
	{
		public Label id;

		public float weight;
	}

	public float targetRange;

	public ModValue friendlyAt;

	public ModValue nonHostileDayz;

	public List<ScriptWeightPair> scriptWeights;
}
