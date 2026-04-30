using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class Ward
{
	public PrecinctID id;

	public EntityID wardOffice;

	public EntityID currentPolitician;

	public List<EntityID> localPoliticians;

	public List<NodeID> wardNodes;

	public Election currElection;

	public Xorshift rng;

	public Election.ElectionResult mostRecentElectionResult;

	public string WardName => Loc.GetWithPolUnit("ui.politics.ward-header.name", "cityname", Game.ctx.session.mapconfig.CityName, "number", Loc.GetPluralized("ui.numberplacement", id.id));

	public string WardNo => Loc.GetWithPolUnit("ui.politics.ward-header.name-no-city", "number", Loc.GetPluralized("ui.numberplacement", id.id));

	public bool ElectionOngoing => currElection != null;

	public PoliticsSettings Settings => Game.serv.globals.settings.politics;

	public Ward()
	{
	}

	public Ward(PrecinctID id, EntityID wardOffice, List<NodeID> wardNodes, Xorshift rng)
	{
		this.id = id;
		this.wardNodes = wardNodes;
		this.wardOffice = wardOffice;
		this.rng = rng;
		currentPolitician = EntityID.INVALID;
		localPoliticians = new List<EntityID>();
	}

	public void OnSystemTurn()
	{
		_ = Game.ctx.clock.Now.ToDate().Month;
		bool flag = currElection == null;
		bool flag2 = localPoliticians.Count > 0;
		if (Game.ctx.simman.politics.IsTimeToStartElection() && flag && flag2)
		{
			StartElection();
		}
		if (currElection != null)
		{
			currElection.TickElection();
		}
		if (Game.ctx.simman.politics.IsTimeToEndElection() && currElection != null)
		{
			EndElection();
		}
	}

	public void SetCurrentPolitician(EntityID politician)
	{
		currentPolitician = politician;
	}

	public void ClearCurrentPolitician()
	{
		currentPolitician = EntityID.INVALID;
	}

	public void GenerateLocalPoliticians()
	{
		localPoliticians = Game.ctx.simman.politics.FindPossiblePoliticiansForPrecinct(id).Take(Settings.elections.numLocalPoliticians).Distinct()
			.ToList();
		foreach (EntityID localPolitician in localPoliticians)
		{
			Game.ctx.simman.politics.CreatePoliticianData(localPolitician, PoliticalType.PoliticianUnassigned);
		}
	}

	public void AddPersonToLocalPoliticians(EntityID newPolitician)
	{
		PoliticsManager politics = Game.ctx.simman.politics;
		if (politics.GetPoliticianData(newPolitician) == null)
		{
			politics.CreatePoliticianData(newPolitician, PoliticalType.PoliticianElected);
		}
		localPoliticians.Add(newPolitician);
	}

	public List<EntityID> PickOneToThreePoliticiansForElection()
	{
		List<EntityID> candidates = new List<EntityID>();
		candidates.Add(currentPolitician);
		int num = rng.DieRoll(Game.serv.globals.settings.politics.elections.maxCandidatesInElection);
		if (isTutorialElection() && num == 0)
		{
			num++;
		}
		for (int i = 0; i < num; i++)
		{
			EntityID item = rng.PickElement(localPoliticians.Where((EntityID x) => !candidates.Contains(x)).ToList());
			candidates.Add(item);
		}
		return candidates;
		bool isTutorialElection()
		{
			bool num2 = Game.ctx.players.Human.territory.Safehouse.FindEntity().components.board.GetNode().precinctId.FindWard().id == id;
			bool flag = Game.ctx.session.mapconfig.id == "new-york";
			bool flag2 = Game.ctx.clock.CurrentTurn == 2 && Game.ctx.clock.Now >= Game.ctx.clock.LastDayOfProcGen;
			return num2 && flag && flag2;
		}
	}

	public void StartElection()
	{
		foreach (EntityID localPolitician in localPoliticians)
		{
			Game.ctx.simman.politics.GetPoliticianData(localPolitician).relToHumanInLastElection = PoliticalRelationshipType.None;
		}
		currElection = new Election(id, PickOneToThreePoliticiansForElection(), GetTotalVotersInWard(), rng);
	}

	public void EndElection()
	{
		currElection.OnElectionPeriodClose();
		currElection = null;
	}

	public int GetTotalVotersInWard()
	{
		int num = 0;
		foreach (NodeID wardNode in wardNodes)
		{
			Node node = Game.ctx.board.nodes.GetNode(wardNode);
			List<Entity> list = new List<Entity>();
			node.FindAllBuildings(list);
			int num2 = 0;
			foreach (Entity item in list)
			{
				ResidenceData residence = item.data.residence;
				if (residence == null || residence.apartments == null || residence.apartments.Count == 0)
				{
					continue;
				}
				foreach (ApartmentData apartment in residence.apartments)
				{
					num2 += apartment.count;
				}
			}
			num += num2;
		}
		return num;
	}
}
