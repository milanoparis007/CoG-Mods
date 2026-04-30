using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public struct AmenityTuning
{
	public Fixnum minBet;

	public Fixnum maxBet;

	public Fixnum winMult;

	public Fixnum heatPer;

	public float winProb;

	public string explainMinBet;

	public string explainMaxBet;

	public string explainWinProb;

	public AmenityTuning(AmenityDef def, ModQuery query)
	{
		minBet = def.behavior.minBetPerPlayer.Evaluate(query);
		explainMinBet = def.behavior.minBetPerPlayer.Explain(query, addHeader: false);
		maxBet = def.behavior.maxBetPerPlayer.Evaluate(query);
		explainMaxBet = def.behavior.maxBetPerPlayer.Explain(query, addHeader: false);
		winProb = (float)(def.behavior.playerWinChancePercent?.Evaluate(query) ?? ((Fixnum)0)) / 100f;
		explainWinProb = def.behavior.playerWinChancePercent?.Explain(query, addHeader: false) ?? "";
		winMult = def.behavior.payoutMultiplier?.Evaluate(query) ?? ((Fixnum)0);
		heatPer = def.behavior.heatPerPlayer.Evaluate(query);
	}
}
