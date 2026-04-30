using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerThroneData
{
	public List<IdToSignature> trophies = new List<IdToSignature>();

	public List<Label> removed = new List<Label>();

	public List<Label> seen = new List<Label>();

	public Xorshift rng = new Xorshift(1u);

	public Label chosenThrone = Label.NULL;

	public PlayerThroneData()
	{
	}

	public PlayerThroneData(PlayerID pid)
	{
		rng = Game.ctx.scenario.MakeSeededRng(pid);
	}
}
