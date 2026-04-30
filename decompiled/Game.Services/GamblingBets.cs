using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class GamblingBets
{
	public ModValue minBetModifier;

	public ModValue maxBetModifier;

	public Fixnum numMinBetsBeforeBan;
}
