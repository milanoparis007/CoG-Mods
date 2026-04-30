using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player;

public class TransactionHistory
{
	[DebuggerDisplay("{DebugString}")]
	public struct TransactionItem
	{
		public EntityID peep;

		public Label resource;

		public bool consumed;

		public SimTime timestamp;

		private string DebugString => $"{resource} w/ {peep}";
	}

	public List<TransactionItem> historyunsafe = new List<TransactionItem>(32);

	public void AddTransaction(Entity peep, Label resId, QtyAndDir qtyanddir)
	{
		historyunsafe.Add(new TransactionItem
		{
			peep = (peep?.Id ?? EntityID.INVALID),
			resource = resId,
			consumed = !qtyanddir.toBldg,
			timestamp = Game.ctx.clock.Now
		});
		CleanHistory();
	}

	private void CleanHistory()
	{
		SimTime now = Game.ctx.clock.Now;
		Fixnum transactionLifetimeDayz = Game.serv.globals.settings.people.social.intros.transactionLifetimeDayz;
		while (historyunsafe.Count > 0 && !(now.Subtract(historyunsafe[0].timestamp).deltadays <= transactionLifetimeDayz))
		{
			historyunsafe.RemoveAt(0);
		}
	}

	public List<IBizModule> GetPlayerBizzes()
	{
		return (from module in ModulesUtil.GetBizModules(Game.ctx.players.Human.territory.Safehouse)
			where module.ModuleConfig.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS)
			select module).ToList();
	}

	public List<TransactionItem> GetHistory()
	{
		CleanHistory();
		return historyunsafe;
	}
}
