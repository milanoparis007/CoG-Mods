using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataNPCSelection : ConvoData, IConvoDataWithSelector
{
	public class Entry
	{
		public EntityID targetId;

		public string introflavor;

		public string relationship;

		public string outofcharacter;

		public Entry()
		{
		}

		public Entry(EntityID targetId, string flavor, string rel, string ooc)
		{
			this.targetId = targetId;
			introflavor = flavor;
			relationship = rel;
			outofcharacter = ooc;
		}
	}

	public List<Entry> entries;

	public Entry selected;

	public List<EntityID> Entries => entries.Select((Entry x) => x.targetId).ToList();

	public bool HasAny
	{
		get
		{
			if (entries != null)
			{
				return entries.Count > 0;
			}
			return false;
		}
	}

	public bool SelectTarget(EntityID targetId)
	{
		int num = entries.FindIndex((Entry e) => e.targetId == targetId);
		if (num < 0)
		{
			return false;
		}
		selected = entries[num];
		return true;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		string text = selected?.introflavor;
		string text2 = selected?.relationship;
		string text3 = selected?.outofcharacter;
		return new string[6] { "introflavor", text, "relationship", text2, "outofcharacter", text3 };
	}

	public ConvoDataNPCSelection()
	{
	}

	public ConvoDataNPCSelection(IEnumerable<Entry> entries)
	{
		this.entries = new List<Entry>(entries);
		selected = null;
	}
}
