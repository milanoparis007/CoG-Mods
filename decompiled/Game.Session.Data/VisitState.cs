using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class VisitState
{
	public CrewAssignment crew;

	public Entity peep;

	public Entity vehicle;

	public Entity npc;

	public Entity building;

	public Entity biz;

	public Entity topic;

	public SimTime time;

	public PlayerID pid;

	public bool AtValidBiz
	{
		get
		{
			if (building != null)
			{
				return biz != null;
			}
			return false;
		}
	}

	public VisitState(CrewAssignment crew, BuildingAndBusinessData bbdata, SimTime time, PlayerID pid)
	{
		SetCrew(crew);
		building = bbdata.building;
		biz = bbdata.biz;
		npc = bbdata.owner;
		topic = null;
		this.time = time;
		this.pid = pid;
	}

	public VisitState(CrewAssignment crew, SimTime time, PlayerID pid)
	{
		SetCrew(crew);
		building = null;
		biz = null;
		npc = null;
		topic = null;
		this.time = time;
		this.pid = pid;
	}

	public VisitState(CrewAssignment crew, Entity npcpeep, SimTime time, PlayerID pid)
	{
		SetCrew(crew);
		building = null;
		biz = null;
		npc = npcpeep;
		topic = null;
		this.time = time;
		this.pid = pid;
	}

	public void SetCrew(CrewAssignment crew)
	{
		this.crew = crew;
		peep = crew.GetPeep();
		vehicle = crew.GetVehicle();
	}

	public static VisitState MakeForBuilding(Entity building)
	{
		BuildingAndBusinessData bbdata = new BuildingAndBusinessData
		{
			building = building,
			biz = null,
			owner = null,
			ownerinfo = default(BizOwner)
		};
		return new VisitState(CrewAssignment.EMPTY, bbdata, SimTime.MIN_DATE, PlayerID.INVALID);
	}

	public PlayerInfo GetPlayer()
	{
		return Game.ctx.players.WithID(pid);
	}

	public Node GetCrewNode()
	{
		return peep?.data.agent.nid.FindNode();
	}

	public Node GetBldgNode()
	{
		return building?.components.board.GetNode();
	}

	public NodeID GetCrewNodeID()
	{
		return peep?.data.agent.nid ?? NodeID.INVALID;
	}

	public NodeID GetBldgNodeID()
	{
		return building?.components.board.GetNodeID() ?? NodeID.INVALID;
	}

	public ModQuery MakeCrewModQuery()
	{
		return new ModQuery(pid, peep?.Id ?? EntityID.INVALID, peep?.Id ?? EntityID.INVALID, GetCrewNodeID());
	}

	public ModQuery MakeOwnerModQuery()
	{
		return new ModQuery(pid, npc?.Id ?? EntityID.INVALID, peep?.Id ?? EntityID.INVALID, GetBldgNodeID());
	}

	public int FindVisitCount()
	{
		return GetPlayer().social.GetRelationshipFromSourceToPlayer(this)?.convos ?? 0;
	}

	public bool IsDisplayBuySell()
	{
		return FindVisitCount() >= 1;
	}
}
