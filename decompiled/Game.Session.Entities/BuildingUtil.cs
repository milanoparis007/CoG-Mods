using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Crew;
using SomaSim.Util;

namespace Game.Session.Entities;

public static class BuildingUtil
{
	public class PotentialGamblingHouse
	{
		public EntityID targetId;

		public int potentialCustomers;

		public Label mainEth;

		public string name;

		public PotentialGamblingHouse()
		{
		}

		public PotentialGamblingHouse(EntityID targetId, int potentialCustomers, Label mainEth, string name)
		{
			this.targetId = targetId;
			this.potentialCustomers = potentialCustomers;
			this.mainEth = mainEth;
			this.name = name;
		}
	}

	private static readonly List<Resource> CONTAINERS = new List<Resource>
	{
		Resource.Find(new Label("crocks")),
		Resource.Find(new Label("bottles")),
		Resource.Find(new Label("small-bottles"))
	};

	private static readonly List<Resource> CONSTRUCTION = new List<Resource>
	{
		Resource.Find(new Label("pipes")),
		Resource.Find(new Label("bricks")),
		Resource.Find(new Label("sheet-metal")),
		Resource.Find(new Label("lumber"))
	};

	public const int MAX_DISTANCE_GAMBLING = 12;

	public static BuildingAndBusinessData FindDataForBuilding(EntityID buildingId)
	{
		return FindDataForBuilding(buildingId.FindEntity());
	}

	public static BuildingAndBusinessData FindDataForBuilding(Entity building)
	{
		BuildingAndBusinessData result = default(BuildingAndBusinessData);
		result.building = building;
		result.biz = result.building?.data.building.business.FindEntity();
		result.ownerinfo = result.biz?.data.biz?.owner ?? BizOwner.INVALID;
		result.owner = (canHaveRepresentative() ? FindOwnerOrManagerForAnyBuilding(building) : null);
		return result;
		bool canHaveRepresentative()
		{
			BuildingComponent.BuildingTypeFlags buildingType = building.components.building.GetBuildingType();
			if (buildingType != BuildingComponent.BuildingTypeFlags.Business && buildingType != BuildingComponent.BuildingTypeFlags.CivicPolice && buildingType != BuildingComponent.BuildingTypeFlags.Civic)
			{
				return buildingType == BuildingComponent.BuildingTypeFlags.Residence;
			}
			return true;
		}
	}

	public static BuildingAndBusinessData FindDataForBiz(EntityID bizId)
	{
		return FindDataForBiz(bizId.FindEntity());
	}

	public static BuildingAndBusinessData FindDataForBiz(Entity biz)
	{
		BuildingAndBusinessData result = default(BuildingAndBusinessData);
		result.biz = biz;
		result.building = result.biz?.data.biz.building.FindEntity();
		result.ownerinfo = result.biz?.data.biz.owner ?? BizOwner.INVALID;
		result.owner = result.ownerinfo.id.FindEntity();
		return result;
	}

	public static BuildingAndBusinessData FindDataForOwner(EntityID ownerId)
	{
		return FindDataForOwner(ownerId.FindEntity());
	}

	private static BuildingAndBusinessData FindDataForOwner(Entity owner)
	{
		BuildingAndBusinessData result = default(BuildingAndBusinessData);
		result.owner = owner;
		result.biz = result.owner?.data.person?.business.FindEntity();
		result.ownerinfo = result.biz?.data.biz?.owner ?? BizOwner.INVALID;
		result.building = result.biz?.data.biz?.building.FindEntity();
		return result;
	}

	public static BuildingAndBusinessData MakeDataForBuildingAndOwner(Entity building, Entity owner)
	{
		return new BuildingAndBusinessData
		{
			building = building,
			owner = owner,
			biz = null,
			ownerinfo = BizOwner.INVALID
		};
	}

	public static Entity FindBuildingForTargetPerson(Entity target)
	{
		Entity entity = null;
		if (entity == null)
		{
			entity = FindBuildingForBizOwner(target);
		}
		if (entity == null && target.data.agent != null)
		{
			entity = target.components.agent.FindCrewAssignment().GetBuilding();
		}
		if (entity == null)
		{
			_ = target.data.person.resassigned;
			entity = target.data.person.resassigned.FindEntity();
		}
		if (entity == null)
		{
			foreach (PlayerInfo item in Game.ctx.players.all)
			{
				GamblerState gamblerState = item.gambling.FindGamblerState(target);
				if (gamblerState != null)
				{
					entity = gamblerState.FindGamblingHouse();
					break;
				}
			}
		}
		if (entity == null)
		{
			entity = Game.ctx.simman.politics.GetPoliticianData(target.Id)?.location.FindEntity() ?? null;
		}
		return entity;
	}

	public static Entity FindBizForOwner(Entity person)
	{
		return person?.data.person?.business.FindEntity();
	}

	public static Entity FindOwnerForBiz(Entity biz)
	{
		return biz?.data.biz?.owner.id.FindEntity();
	}

	public static Entity FindBuildingForBiz(Entity biz)
	{
		return biz?.data.biz?.building.FindEntity();
	}

	public static Entity FindBizForBuilding(Entity building)
	{
		return building?.data.building?.business.FindEntity();
	}

	public static Entity FindBuildingForBizOwner(Entity owner)
	{
		return FindBuildingForBiz(FindBizForOwner(owner));
	}

	public static Entity FindBizOwnerForBuilding(Entity building)
	{
		return FindOwnerForBiz(FindBizForBuilding(building));
	}

	public static Entity FindBizForOwner(EntityID ownerId)
	{
		return FindBizForOwner(ownerId.FindEntity());
	}

	public static Entity FindOwnerForBiz(EntityID bizId)
	{
		return FindOwnerForBiz(bizId.FindEntity());
	}

	public static Entity FindBuildingForBiz(EntityID bizId)
	{
		return FindBuildingForBiz(bizId.FindEntity());
	}

	public static Entity FindBizForBuilding(EntityID buildingId)
	{
		return FindBizForBuilding(buildingId.FindEntity());
	}

	public static Entity FindBuildingForBizOwner(EntityID ownerId)
	{
		return FindBuildingForBizOwner(ownerId.FindEntity());
	}

	public static Entity FindBizOwnerForBuilding(EntityID buildingId)
	{
		return FindBizOwnerForBuilding(buildingId.FindEntity());
	}

	public static string FindBuildingOwnerOrManagerName(EntityID buildingId)
	{
		return FindOwnerOrManagerForAnyBuilding(buildingId.FindEntity()).data.person.FullName;
	}

	public static Entity FindOwnerForAnyBuilding(EntityID buildingId)
	{
		return FindOwnerOrManagerForAnyBuilding(buildingId.FindEntity());
	}

	public static string FindBuildingOwnerOrManagerName(Entity building)
	{
		return FindOwnerOrManagerForAnyBuilding(building).data.person.FullName;
	}

	public static string FindBuildingIcon(EntityID buildingId)
	{
		return FindBuildingIcon(buildingId.FindEntity());
	}

	public static string FindBuildingIcon(Entity building)
	{
		if (building?.components.building == null)
		{
			return null;
		}
		switch (building.components.building.GetBuildingType())
		{
		case BuildingComponent.BuildingTypeFlags.Business:
			return FindBizForBuilding(building).config.biz.GetIcon();
		case BuildingComponent.BuildingTypeFlags.CivicPolice:
			return Loc.Get("ui.icon.police");
		case BuildingComponent.BuildingTypeFlags.Residence:
			return building.components.residence.GetBuildingIcon();
		case BuildingComponent.BuildingTypeFlags.Civic:
			return Loc.Get("building.ward-hq.icon");
		default:
			Logger.Warning($"Unknown building type in BuildingUtil.FindBuildingName, called on {building.Id}");
			return null;
		}
	}

	public static string FindBuildingName(EntityID buildingId)
	{
		return FindBuildingName(buildingId.FindEntity());
	}

	public static string FindBuildingName(Entity building)
	{
		if (building?.components.building == null)
		{
			return null;
		}
		switch (building.components.building.GetBuildingType())
		{
		case BuildingComponent.BuildingTypeFlags.Business:
			return FindBizForBuilding(building).data.biz.bizname;
		case BuildingComponent.BuildingTypeFlags.CivicPolice:
			return building.data.police.precinctID.FindPrecinct().social.PlayerFullName;
		case BuildingComponent.BuildingTypeFlags.Civic:
			return building.config.civic.GetName();
		case BuildingComponent.BuildingTypeFlags.Residence:
			return building.components.residence.GetBuildingOrNpcName();
		default:
			Logger.Warning($"Unknown building type in BuildingUtil.FindBuildingName, called on {building.Id}");
			return null;
		}
	}

	public static Entity FindOwnerOrManagerForAnyBuilding(Entity building)
	{
		if (building?.components.building == null)
		{
			return null;
		}
		switch (building.components.building.GetBuildingType())
		{
		case BuildingComponent.BuildingTypeFlags.Business:
			return FindBizForBuilding(building).data.biz.owner.id.FindEntity();
		case BuildingComponent.BuildingTypeFlags.CivicPolice:
			return building.data.police.officers.FirstOrDefaultFast().FindEntity();
		case BuildingComponent.BuildingTypeFlags.Civic:
			return building.data.civic?.npc.FindEntity();
		case BuildingComponent.BuildingTypeFlags.Residence:
			return building.components.residence.GetResidentialNpc();
		default:
			Logger.Warning($"Unknown building type in BuildingUtil.FindOwnerForBuilding, called on {building.Id}");
			return null;
		}
	}

	public static string GenerateBuildingPickMouseover(Entity building, bool brief = false)
	{
		BuildingAndBusinessData buildingAndBusinessData = FindDataForBuilding(building);
		bool flag = building.components.building.IsScopedBy(PlayerID.HumanPlayer);
		if (building.components.building.IsSafehouseNotOf(PlayerID.HumanPlayer))
		{
			return GenerateMouseoverForAISafehouse(building, flag, brief);
		}
		if (building.components.building.IsPoliceStation)
		{
			return Loc.Get("ui.pick.police");
		}
		if (building.components.residence != null)
		{
			return GenerateResidentialMouseover(building, flag, brief);
		}
		if (building.components.civic != null)
		{
			return GenerateCivicMouseover(building, flag, brief);
		}
		string text = FindBuildingName(building);
		string text2 = Loc.Get(flag ? "ui.pick.name.scoped" : "ui.pick.name.unscoped", "name", text);
		PlayerID playerID = building.data.building.controlled.Get();
		if (playerID.IsAnyPlayer)
		{
			text2 += Loc.Get(playerID.IsHumanPlayer ? "ui.pick.controlled.you" : "ui.pick.controlled.else");
		}
		VisitState visitState = new VisitState(CrewAssignment.EMPTY, FindDataForBuilding(building), Game.ctx.clock.Now, PlayerID.HumanPlayer);
		if (flag && visitState.IsDisplayBuySell())
		{
			text2 = text2 + "\n" + GetBuySellText(visitState, mouseover: true);
		}
		PlayerID outpostOwner = building.components.building.OutpostOwner;
		if (outpostOwner.IsAnyPlayer)
		{
			text2 += Loc.Get("ui.pick.front", "groupname", outpostOwner.FindPlayer().social.FindPlayerGroupNameColorized());
		}
		if (playerID.IsHumanPlayer && building.components.modules.FindBackroomModule() != null)
		{
			text2 = text2 + "\n\n" + Loc.Get("ui.pick.production", "production", CrewDialogModuleUtil.DescribeBackModule(building, showProgress: true));
		}
		if (!brief)
		{
			bool flag2 = building.components.building.IsControlledNotBy(PlayerID.HumanPlayer);
			string text4;
			if (flag && flag2)
			{
				string text3 = building.components.building.GetControllingPlayer().FindPlayer().social.FindPlayerGroupNameColorized();
				text4 = Loc.Get("ui.pick.aicontrolled.scoped", "name", text3);
			}
			else if (flag)
			{
				int socialTicketsAvailable = Game.ctx.players.Human.social.GetSocialTicketsAvailable(buildingAndBusinessData.owner.Id);
				string text5 = ((socialTicketsAvailable == 0) ? "" : Loc.GetPluralized("ui.pick.tickets", socialTicketsAvailable, "num", socialTicketsAvailable));
				text4 = Loc.Get("ui.pick.clicktovisit", "ps", text5);
			}
			else if (building.components.building.IsAnyCrewAtThisNode(PlayerID.HumanPlayer))
			{
				CrewCost scopeOutCost = Game.serv.globals.settings.people.social.costs.scopeOutCost;
				text4 = Loc.GetPluralized("ui.pick.scopeout", scopeOutCost.actions, "num", scopeOutCost.actions);
			}
			else
			{
				text4 = Loc.Get("ui.pick.scopelater");
			}
			text2 = text2 + "\n\n" + text4;
		}
		return text2;
	}

	public static string GetBuySellText(VisitState visit, bool mouseover, bool explain = false)
	{
		ConvoBuySellDefs convoBuySellDefs = ConvoBlurbUtils.GenerateBuySellDefs(visit);
		List<ConvoBuySellDefs.Item> list = convoBuySellDefs.defs.Where((ConvoBuySellDefs.Item item) => item.playerBuys).ToList();
		List<ConvoBuySellDefs.Item> list2 = convoBuySellDefs.defs.Where((ConvoBuySellDefs.Item item) => !item.playerBuys).ToList();
		if (list2.Count == 0 && list.Count == 0)
		{
			return "";
		}
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		if (list.Count > 0)
		{
			sb.Append(Loc.Get("convodialog.icons.canbuy"));
			if (!explain)
			{
				MakeIconsInline(list);
			}
			else
			{
				MakeIconsWordy(list);
			}
		}
		if (list2.Count > 0 && list.Count > 0 && mouseover)
		{
			sb.Append("\n");
		}
		else if (list2.Count > 0 && list.Count > 0)
		{
			sb.Append("  -  ");
		}
		if (list2.Count > 0)
		{
			sb.Append(Loc.Get("convodialog.icons.cansell"));
			if (!explain)
			{
				MakeIconsInline(list2);
			}
			else
			{
				MakeIconsWordy(list2);
			}
		}
		return sb.ToStringAndReturnToPool();
		void MakeIconsInline(List<ConvoBuySellDefs.Item> list3)
		{
			int num = 0;
			foreach (ConvoBuySellDefs.Item item in list3)
			{
				if (item.cardShowing)
				{
					sb.Append(" ");
					sb.Append(Loc.Get("convodialog.icons.item", "icon", item.elt.item.FindResource().GetIcon()));
				}
				else
				{
					num++;
				}
			}
			for (int i = 0; i < num; i++)
			{
				sb.Append(" ");
				sb.Append(Loc.Get("convodialog.icons.item", "icon", Loc.Get("convodialog.icons.unknown")));
			}
		}
		void MakeIconsWordy(List<ConvoBuySellDefs.Item> list3)
		{
			int num = 0;
			foreach (ConvoBuySellDefs.Item item2 in list3)
			{
				if (item2.cardShowing)
				{
					sb.Append("\n");
					sb.Append(item2.elt.item.FindResource().GetIconAndName());
				}
				else
				{
					num++;
				}
			}
			for (int i = 0; i < num; i++)
			{
				sb.Append("\n");
				sb.Append(Loc.Get("convodialog.icons.unknown.mo"));
			}
		}
	}

	public static string GetBackModuleName(EntityID building)
	{
		string text = (building.FindEntity()?.components.modules?.FindBackroomModule())?.ModuleConfig.Common.display.locname;
		if (text == null)
		{
			return null;
		}
		return Loc.Get(text);
	}

	internal static string DescribeBuildingAtScopeOut(Entity building)
	{
		List<IBizModule> list = building.components.modules?.bizmodules;
		if (list == null)
		{
			return "";
		}
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		List<string> list2 = new List<string>();
		foreach (IBizModule item in list)
		{
			string text = item.LocData?.locScopeOut;
			if (text != null && !list2.Contains(text))
			{
				list2.Add(text);
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(" ");
				}
				stringBuilder.Append(Loc.Get(text));
			}
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	private static string GenerateMouseoverForAISafehouse(Entity building, bool scoped, bool brief)
	{
		if (!scoped)
		{
			if (!brief)
			{
				return Loc.Get("ui.pick.aicontrolled.unscoped");
			}
			return Loc.Get("ui.pick.aicontrolled.unscoped.brief");
		}
		if (!SafehouseUtils.WasSafehouseRaided(building))
		{
			string playerGroupName = building.components.building.SafehouseOwner.FindPlayer().social.PlayerGroupName;
			if (brief)
			{
				return Loc.Get("ui.pick.safehouse.scoped.brief", "name", playerGroupName);
			}
			return Loc.Get(SafehouseUtils.CanRaidSafehouse(building, PlayerID.HumanPlayer) ? "ui.pick.scavenge" : "ui.pick.safehouse.scoped", "name", playerGroupName);
		}
		if (brief)
		{
			return Loc.Get("ui.pick.safehouse.ready.brief");
		}
		List<CrewAssignment> list = Game.ctx.players.Human.crew.FindAllDriversAtNode(building.components.board.GetNodeID());
		return Loc.GetPluralized("ui.pick.safehouse.ready.crew", list.Count);
	}

	private static string GenerateResidentialMouseover(Entity building, bool scoped, bool _)
	{
		if (!scoped)
		{
			return Loc.Get("ui.pick.res.unscoped");
		}
		if (building.components.residence.IsAssigned)
		{
			var (flag, text, text2) = building.components.residence.GetResMouseoverNameAndDesc();
			if (!flag)
			{
				return Loc.Get("ui.pick.res.scoped", "msg", text).Trim();
			}
			return Loc.Get("ui.pick.res.namedesc", "name", text, "desc", text2);
		}
		return Loc.Get("ui.pick.res.unscoped");
	}

	private static string GenerateCivicMouseover(Entity building, bool scoped, bool _)
	{
		if (!scoped)
		{
			return Loc.Get("ui.pick.civic.unscoped", "name", Loc.Get(building.config.civic.locname));
		}
		if (building.components.civic.IsAssigned)
		{
			var (flag, text, text2) = building.components.civic.GetCivicMouseoverNameAndDesc();
			if (!flag)
			{
				return Loc.Get("ui.pick.civic.scoped", "msg", text).Trim();
			}
			return Loc.Get("ui.pick.civic.namedesc", "name", text, "desc", text2);
		}
		return Loc.Get("ui.pick.civic.unscoped", "name", Loc.Get(building.config.civic.locname));
	}

	internal static (bool valid, bool damaged, Fixnum current, Fixnum max) GetBuildingHealth(Entity building)
	{
		BuildingData.Health healthDataOrNull = building.components.building.GetHealthDataOrNull();
		if (healthDataOrNull == null)
		{
			return (valid: false, damaged: false, current: 0, max: 0);
		}
		return (valid: true, damaged: healthDataOrNull.IsNotMax, current: healthDataOrNull.current, max: healthDataOrNull.max);
	}

	public static EntityID GetQuickTargetId(Entity building)
	{
		return GetQuickTarget(building).peepId;
	}

	public static CrewAssignment GetQuickTarget(Entity building)
	{
		NodeID nodeID = building.components.board.GetNodeID();
		Entity previousActive = Game.ctx.selection.PreviousActive;
		EntityID entityID = EntityID.INVALID;
		if (previousActive != null && previousActive.components.mobile != null)
		{
			entityID = Game.ctx.players.Human.crew.FindPeepAssignedToVehicle(previousActive.Id);
		}
		List<EntityID> list = Game.ctx.players.Human.crew.FindAllDriversAtNode(nodeID).SelectIntoNewList((CrewAssignment crew) => crew.peepId);
		if (list.Contains(entityID))
		{
			return Game.ctx.players.Human.crew.GetCrewForPeep(entityID);
		}
		if (list.Count != 0)
		{
			return Game.ctx.players.Human.crew.GetCrewForPeep(list[0]);
		}
		return CrewAssignment.EMPTY;
	}

	public static void StartOwnedBuildingInteraction(Entity building, Action<EntityID, Entity> posCallback)
	{
		if (KeyUtil.IsShiftDown)
		{
			posCallback(GetQuickTargetId(building), building);
			return;
		}
		EntitySelectionPopup.ShowCrewSelector(building.components.board.GetNodeID(), Loc.Get("ui.crewinfo.pickone.safehouse"), delegate(EntityID eid)
		{
			posCallback(eid, building);
		}, delegate
		{
			DeselectOnNextFrame();
		});
	}

	public static void DeselectOnNextFrame()
	{
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.selection.ClearActive();
		});
	}

	public static List<EntityID> FindUnscopedContainerSellers(Entity building)
	{
		return FindUnscopedSellersAroundBuilding(building, CONTAINERS);
	}

	public static List<EntityID> FindUnscopedConstructionSellers(Entity building)
	{
		return FindUnscopedSellersAroundBuilding(building, CONSTRUCTION);
	}

	public static List<EntityID> FindUnscopedSellersAroundBuilding(Entity building, List<Resource> resources)
	{
		List<EntityID> list = new List<EntityID>();
		foreach (EntityID item in FindNeighborsWithinDistance(building.components.board.GetNode(), 64))
		{
			Entity entity = item.FindEntity();
			if (!entity.data.building.scoped.Get(PlayerID.HumanPlayer) && DoesSell(entity, resources))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static bool DoesSell(Entity building, List<Resource> resources)
	{
		foreach (BuySellElement item2 in building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys: true, playerSells: false))
		{
			MfgItem item = item2.item;
			if (resources.Contains(item.FindResource()))
			{
				return true;
			}
		}
		return false;
	}

	internal static List<EntityID> FindNeighborsWithinDistance(Node start, int maxNodes)
	{
		List<EntityID> buildings = new List<EntityID>();
		Game.ctx.board.nodes.VisitNeighborhoodBFS(start, maxNodes, delegate(Node node)
		{
			buildings.AddRange(node.interesting);
		}, null, null, null, onlyBizNodes: true);
		return buildings;
	}

	public static bool IsGamblingHouse(EntityID buildingId)
	{
		return IsGamblingHouse(buildingId.FindEntity());
	}

	public static bool IsGamblingHouse(Entity building)
	{
		return building?.components.modules?.gambling != null;
	}

	public static List<PotentialGamblingHouse> GetPotentialGamblingHousesOnVisit(VisitState visit)
	{
		PrecinctID precinct = visit.GetBldgNode().precinctId;
		List<Node> nodes = new List<Node>();
		Game.ctx.board.nodes.VisitNeighborhoodBFS(visit.GetBldgNode(), 12, delegate(Node node)
		{
			nodes.Add(node);
		}, (Node node) => node.precinctId == precinct);
		List<PotentialGamblingHouse> list = new List<PotentialGamblingHouse>();
		_ = Game.serv.globals.settings.gambling.aoe.estimatedStartAOE;
		foreach (Node item in nodes)
		{
			if (!(item.owner.pid != Game.ctx.players.Human.PID))
			{
				List<Entity> list2 = new List<Entity>();
				item.FindAllBuildings(list2);
				EntityID? entityID = list2.Where((Entity x) => x.data.residence != null && Game.ctx.players.Human.gambling.CanBecomeGamblingHouse(x)).FirstOrDefault()?.Id;
				if (entityID.HasValue)
				{
					list.Add(new PotentialGamblingHouse(entityID.Value, GetPotentialGamblingCustomers(PlayerID.HumanPlayer, item, entityID.Value.FindEntity()), item.maineth, item.GetCornerNameShort(addPrefix: false)));
				}
			}
		}
		return list;
	}

	public static string GetGamblingHouseName(Entity building)
	{
		return Loc.Get("ui.ownedcasino.name", "corner", building.components.board.GetNode().GetCornerNameShort(addPrefix: false));
	}

	public static int GetPotentialGamblingCustomers(PlayerID pid, Node node, Entity building)
	{
		int estimatedStartAOE = Game.serv.globals.settings.gambling.aoe.estimatedStartAOE;
		return GetPotentialGamblingCustomers(node, estimatedStartAOE, new ModQuery(pid, building.Id, node.id));
	}

	public static int GetPotentialGamblingCustomers(Entity gamblingHouse, ModQuery q)
	{
		GamblingModuleConfig gamblingModuleConfig = gamblingHouse.components.modules?.gambling?.config;
		if (gamblingModuleConfig == null)
		{
			return 0;
		}
		Node node = gamblingHouse?.components.board.GetNode();
		Fixnum radius = gamblingModuleConfig.gambling.aoeRadius.Evaluate(q);
		return GetPotentialGamblingCustomers(node, radius, q);
	}

	private static int GetPotentialGamblingCustomers(Node node, Fixnum radius, ModQuery q)
	{
		Fixnum fixnum = Game.serv.globals.settings.gambling.aoe.customersPerPop.Evaluate(q);
		return (GetApartmentResidentsInRadius(node, radius) * fixnum).IntFloor();
	}

	public static Dictionary<AmenityData, int> GetNumCustomersForAmenities(Entity building, ModQuery query)
	{
		GamblingModule gambling = building.components.modules.gambling;
		Fixnum radius = gambling.config.gambling.aoeRadius.Evaluate(query);
		List<AmenityData> listOfActiveAOEAmenities = GetListOfActiveAOEAmenities(gambling, building, query);
		float num = 0f;
		List<float> list = new List<float>();
		foreach (AmenityData item in listOfActiveAOEAmenities)
		{
			float num2 = (float)item.GetAmenityDef().behavior.maxCustomers.Evaluate(query);
			num += num2;
			list.Add(num2);
		}
		list.Normalize();
		int potentialGamblingCustomers = GetPotentialGamblingCustomers(building.components.board.GetNode(), radius, query);
		bool num3 = (float)potentialGamblingCustomers > num;
		Dictionary<AmenityData, int> dictionary = new Dictionary<AmenityData, int>();
		float num4 = (num3 ? num : ((float)potentialGamblingCustomers));
		for (int i = 0; i < listOfActiveAOEAmenities.Count; i++)
		{
			AmenityData key = listOfActiveAOEAmenities[i];
			float num5 = list[i];
			dictionary.Add(key, (int)Math.Floor(num4 * num5));
		}
		return dictionary;
	}

	public static List<AmenityData> GetListOfActiveAOEAmenities(GamblingModule module, Entity building, ModQuery query)
	{
		PlayerGambling.GamblingHouseStatus houseStatus = Game.ctx.players.Human.gambling.GetGamblingHouseStatus(building);
		return module.data.amenities.Where((AmenityData amenity) => amenity.GetAmenityDef().behavior.type == AmenityDef.AmenityBehavior.BehaviorType.AOE && amenity.IsEnabled(Game.ctx.clock.Now) && Game.ctx.players.Human.gambling.AmenityIsFunded(building, amenity, query) && houseStatus.isManagerPresent && houseStatus.isNotDamaged).ToList();
	}

	public static int GetApartmentResidentsInRadius(Node start, Fixnum radius)
	{
		List<Node> list = Game.ctx.board.nodes.FindAndSortNodesInRadius(start.pos, (float)radius);
		int num = 0;
		foreach (Node item in list)
		{
			List<Entity> list2 = new List<Entity>();
			item.FindAllBuildings(list2);
			int num2 = 0;
			foreach (Entity item2 in list2)
			{
				ResidenceData residence = item2.data.residence;
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
