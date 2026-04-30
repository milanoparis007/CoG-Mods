using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class PoliticsManagerPersistedData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<PoliticsManager>();

	public List<PoliticianData> politicians = new List<PoliticianData>();

	public List<Ward> wards = new List<Ward>();

	public List<Label> laws = new List<Label>();

	public Dictionary<PlayerID, InfluenceWallet> influence = new Dictionary<PlayerID, InfluenceWallet>();
}
