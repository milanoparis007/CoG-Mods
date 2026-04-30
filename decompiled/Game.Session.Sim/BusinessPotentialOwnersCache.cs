using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class BusinessPotentialOwnersCache
{
	private sealed class Entry
	{
		public FamilyTree fam;

		public List<Entity> peeps = new List<Entity>();

		public int tmpDistScore;
	}

	private List<Entry> _all = new List<Entry>();

	public void Clear()
	{
		_all.Clear();
	}

	public Entity Find(Node node, IRandom rng, Entity excluded = null)
	{
		if (_all.Count == 0)
		{
			Regenerate();
		}
		var (famindex, entity) = FindHelper(node, rng, excluded);
		if (entity == null)
		{
			return null;
		}
		RemoveFromList(famindex, entity);
		return entity;
	}

	private void RemoveFromList(int famindex, Entity peep)
	{
		Entry entry = _all[famindex];
		int num = entry.peeps.IndexOf(peep);
		if (num < 0)
		{
			Logger.Warning("Found a peep that's not in a family list?");
			return;
		}
		entry.peeps.SwapRemoveAt(num);
		if (entry.peeps.Count == 0)
		{
			_all.SwapRemoveAt(famindex);
		}
	}

	private void Regenerate()
	{
		SimTime now = Game.ctx.clock.Now;
		using (new BlockStopwatch("Regenerating potential owners cache"))
		{
			List<Entity> list = new List<Entity>();
			Game.ctx.simman.peoplegen.ProducePeopleWhere((Entity person) => BusinessTracker.CanPersonOwnBusiness(person, now), list, clearFirst: true);
			Dictionary<FamilyTree, List<Entity>> dictionary = new Dictionary<FamilyTree, List<Entity>>();
			foreach (Entity item in list)
			{
				FamilyTree key = item.components.person.FindFamilyTree();
				dictionary.AddToList(key, item);
			}
			_all = (from e in dictionary
				orderby e.Key.famId
				select new Entry
				{
					fam = e.Key,
					peeps = e.Value
				}).ToList();
		}
	}

	private (int famindex, Entity peep) FindHelper(Node node, IRandom rng, Entity excluded = null)
	{
		int i = 0;
		for (int count = _all.Count; i < count; i++)
		{
			Entry entry = _all[i];
			entry.tmpDistScore = (int)(node.pos - entry.fam.anchor.pos).MagnitudeSquared;
		}
		_all.Sort((Entry a, Entry b) => a.tmpDistScore - b.tmpDistScore);
		int num = MathUtil.ClampMax(rng.Generate(0, 3), _all.Count);
		Entity item = PickCandidate(_all[num].peeps, excluded);
		return (famindex: num, peep: item);
	}

	private static Entity PickCandidate(List<Entity> peeps, Entity excluded)
	{
		if (peeps != null)
		{
			int i = 0;
			for (int count = peeps.Count; i < count; i++)
			{
				if (peeps[i] != excluded)
				{
					return peeps[i];
				}
			}
		}
		return null;
	}
}
