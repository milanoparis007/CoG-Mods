using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class ResidenceTrackerPersistedData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<ResidenceTracker>();

	public int population;

	public string newsSequenceRoot;

	public int newsSequenceNextIndex;
}
