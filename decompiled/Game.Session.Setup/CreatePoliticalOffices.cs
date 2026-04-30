using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Setup;

public class CreatePoliticalOffices
{
	private const int ITERATIONS_PER_FRAME = 1000;

	private static Dictionary<Entity, Deque<Node>> queues = new Dictionary<Entity, Deque<Node>>();

	private SetupOrchestratorContext _ctx;

	private Xorshift _rng;

	private const string CIVIC_POLITICAL_OFFICE = "civic-ward-hq";

	private const string LARGE_CIVIC_POLITICAL_OFFICE = "civic-ward-hq-large";

	internal CreatePoliticalOffices(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		int i = 0;
		_rng = Game.ctx.scenario.MakeSeededRng<CreatePoliticalOffices>();
		EntityConfig smallConfig = Game.ctx.entityman.FindTemplate(new Label("civic-ward-hq"));
		EntityConfig largeConfig = Game.ctx.entityman.FindTemplate(new Label("civic-ward-hq-large"));
		foreach (Entity item in _ctx.copStationData.placed)
		{
			List<EntityID> prio1 = new List<EntityID>();
			List<EntityID> prio2 = new List<EntityID>();
			List<EntityID> prio3 = new List<EntityID>();
			PrecinctID precinctID = item.data.police.precinctID;
			IEnumerable<EntityID> enumerable = CopUtil.FindBuildingsInPrecinct(precinctID);
			List<Node> nodesInPrecinct = CopUtil.FindNodesInPrecinct(precinctID).ToList();
			int num;
			foreach (EntityID item2 in enumerable)
			{
				bool flag = item2.FindEntity().config.board.lotsize.width == 1f;
				if (item2.FindEntity().config.board.lotsize.width == 2f && item2.FindEntity().config.civic != null)
				{
					prio1.Add(item2);
				}
				else if (flag && item2.FindEntity().data.residence != null && !item2.FindEntity().data.residence.IsEventHostingReserved)
				{
					prio2.Add(item2);
				}
				else if (flag && (item2.FindEntity().data.biz?.owner.IsFake ?? false))
				{
					prio3.Add(item2);
				}
				num = i + 1;
				i = num;
				if (num % 1000 == 0)
				{
					yield return null;
				}
			}
			if (prio1.Count > 0)
			{
				EntityID id = _rng.PickElement(prio1);
				Entity entity = Game.ctx.board.ReplaceEntityInPlace(id.FindEntity(), largeConfig);
				Game.ctx.simman.politics.AddWard(precinctID, entity.Id, nodesInPrecinct);
			}
			else if (prio2.Count > 0)
			{
				EntityID id2 = _rng.PickElement(prio2);
				Entity entity2 = Game.ctx.board.ReplaceEntityInPlace(id2.FindEntity(), smallConfig);
				Game.ctx.simman.politics.AddWard(precinctID, entity2.Id, nodesInPrecinct);
			}
			else if (prio3.Count > 0)
			{
				EntityID id3 = _rng.PickElement(prio3);
				Entity business = id3.FindEntity().data.building.business.FindEntity();
				Game.ctx.simman.businesses.DetachBusinessFromBuilding(business, shutdown: false);
				Game.ctx.simman.businesses.DestroyBusinessUnattached(business, shutdown: false);
				Entity entity3 = Game.ctx.board.ReplaceEntityInPlace(id3.FindEntity(), smallConfig);
				Game.ctx.simman.politics.AddWard(precinctID, entity3.Id, nodesInPrecinct);
			}
			num = i + 1;
			i = num;
			if (num % 1000 == 0)
			{
				yield return null;
			}
		}
	}
}
