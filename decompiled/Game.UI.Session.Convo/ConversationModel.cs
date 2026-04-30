using System.Collections.Generic;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Convo;

public sealed class ConversationModel : HUDModel<ConversationModel, ConversationDialog, ConversationController>
{
	public class InterState
	{
		public struct Blurb
		{
			public bool player;

			public string text;

			public Blurb(bool player, string text)
			{
				this.player = player;
				this.text = text;
			}
		}

		public string forcedReaction;

		public int statecount;

		public List<Blurb> blurbs = new List<Blurb>();
	}

	public enum Source
	{
		None,
		ControlledBusiness,
		ControlledGambling,
		CrewInspectPeep,
		Politics,
		Peep
	}

	public VisitState visit;

	public ConvoState state;

	public InterState shared = new InterState();

	public Source source;

	public bool IsBusinessVisitOK
	{
		get
		{
			if (visit != null && visit.npc != null)
			{
				return visit.peep != null;
			}
			return false;
		}
	}

	public bool IsBusinessVisitTooFar
	{
		get
		{
			if (visit != null && visit.npc != null)
			{
				return visit.peep == null;
			}
			return false;
		}
	}

	public bool IsCrewConvo
	{
		get
		{
			if (visit != null && visit.npc == null)
			{
				return visit.peep != null;
			}
			return false;
		}
	}

	public bool HasForcedReaction => shared.forcedReaction != null;

	public ConversationModel()
	{
		Reset();
	}

	public void SetForced(string reaction)
	{
		shared.forcedReaction = reaction;
	}

	public void ResetStateCounter()
	{
		shared.statecount = 0;
	}

	public void IncrementStateCounter()
	{
		shared.statecount++;
	}

	public Entity GetConvoTarget()
	{
		object obj = visit?.npc;
		if (obj == null)
		{
			VisitState visitState = visit;
			if (visitState == null)
			{
				return null;
			}
			obj = visitState.peep;
		}
		return (Entity)obj;
	}

	public override void Reset()
	{
		base.Reset();
		visit = null;
		state = null;
		source = Source.None;
		shared.forcedReaction = null;
		shared.statecount = 0;
		shared.blurbs.Clear();
	}
}
