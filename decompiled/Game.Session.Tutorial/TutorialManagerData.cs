using SomaSim.Util;

namespace Game.Session.Tutorial;

public sealed class TutorialManagerData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<TutorialManager>();

	public TutorialContext ctx = new TutorialContext();
}
