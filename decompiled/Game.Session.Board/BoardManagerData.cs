using SomaSim.Util;

namespace Game.Session.Board;

public sealed class BoardManagerData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<BoardManager>();

	public NodePersistedData nodes = new NodePersistedData();
}
