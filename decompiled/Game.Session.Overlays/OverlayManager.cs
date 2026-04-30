using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Picks;
using SomaSim.SION;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Overlays;

public class OverlayManager : AbstractSessionManager, ISaveLoadProvider
{
	public class ResourceState
	{
		public enum Mode
		{
			None,
			Resource,
			AnyRecent,
			CarShops,
			Fronts,
			PoliceStations,
			Deliveries
		}

		public Dictionary<EntityID, OverlayResDir> buildings = new Dictionary<EntityID, OverlayResDir>(new EntityIDEqualityComparer());

		public List<Resource> res = new List<Resource>();

		public Mode mode;

		public bool IsShowingAny => mode != Mode.None;

		public void Reset()
		{
			Set(Mode.None, null, null);
		}

		internal void SetResources(List<Resource> res, IEnumerable<OverlayResDir> entries)
		{
			Set(Mode.Resource, res, entries);
		}

		internal void SetMode(Mode mode, IEnumerable<OverlayResDir> entries)
		{
			Set(mode, null, entries);
		}

		private void Set(Mode mode, List<Resource> res, IEnumerable<OverlayResDir> entries)
		{
			if (res == null)
			{
				this.res.Clear();
			}
			else
			{
				this.res = res;
			}
			this.mode = mode;
			buildings.Clear();
			if (entries == null)
			{
				return;
			}
			foreach (OverlayResDir entry in entries)
			{
				buildings.Add(entry.eid, entry);
			}
		}

		internal void AddResource(Resource res, IEnumerable<OverlayResDir> entries)
		{
			if (mode == Mode.None)
			{
				mode = Mode.Resource;
			}
			this.res.Add(res);
			if (entries == null)
			{
				return;
			}
			foreach (OverlayResDir entry in entries)
			{
				if (buildings.Keys.Contains(entry.eid))
				{
					buildings[entry.eid].resources.AddRange(entry.resources);
				}
				else
				{
					buildings.Add(entry.eid, entry);
				}
			}
		}

		internal void RemoveResource(Resource res, IEnumerable<OverlayResDir> entries)
		{
			this.res.Remove(res);
			if (entries != null)
			{
				foreach (OverlayResDir entry in entries)
				{
					if (!buildings.Keys.Contains(entry.eid))
					{
						continue;
					}
					foreach (EntityResDir resource in entry.resources)
					{
						List<EntityResDir> resources = buildings[entry.eid].resources;
						if (resources.Contains(resource))
						{
							resources.Remove(resource);
						}
						if (resources.Count == 0)
						{
							buildings.Remove(entry.eid);
						}
					}
				}
			}
			if (this.res.Count == 0)
			{
				Game.ctx.overlays.HideAnyOverlay();
			}
		}

		public OverlayResDir FindBuilding(EntityID id)
		{
			return buildings.FindOrDefault(id);
		}
	}

	public sealed class ResBarPersistedData
	{
		public List<Label> favorites = new List<Label>();
	}

	public enum RelMode
	{
		Default,
		Suppressed
	}

	private static readonly PickHideRequest _summaryPicks = new PickHideRequest(PickType.SummaryPick, typeof(OverlayManager));

	private static readonly PickHideRequest _buildingPicks = new PickHideRequest(PickType.BuildingPick, typeof(OverlayManager));

	private static readonly PickHideRequest _peepPicks = new PickHideRequest(PickType.CrewPick, typeof(OverlayManager));

	private static readonly PickHideRequest _cornerPicks = new PickHideRequest(PickType.CornerPick, typeof(OverlayManager));

	private static readonly ZoneType[] ALL_ZONE_TYPES = Enum.GetValues(typeof(ZoneType)) as ZoneType[];

	private static readonly int OverlayBlend = Shader.PropertyToID("_OverlayBlend");

	public RelMode CurrentRelMode;

	public OverlayArrows arrows;

	public ResourceState resources;

	public ResBarPersistedData data = new ResBarPersistedData();

	private const float SCALE_FACTOR = 0.01f;

	private const string CIVIC_POLITICAL_OFFICE = "civic-ward-hq";

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
		resources = new ResourceState();
		arrows = new OverlayArrows();
		arrows.Initialize();
		CurrentRelMode = RelMode.Default;
	}

	public override void OnInitializeDone()
	{
		Game.ctx.console.Add(this, new DebugConsoleEntry("overlay", "off", ConsoleDisableOverlays));
		Game.ctx.console.Add(this, new DebugConsoleEntry("overlay", "building", "res", (string[] pars) => ConsoleBuildingTypeOverlay(ColorConstants.OVERLAY_RES, pars)));
		Game.ctx.console.Add(this, new DebugConsoleEntry("overlay", "building", "com", (string[] pars) => ConsoleBuildingTypeOverlay(ColorConstants.OVERLAY_COM, pars)));
		Game.ctx.console.Add(this, new DebugConsoleEntry("overlay", "building", "ind", (string[] pars) => ConsoleBuildingTypeOverlay(ColorConstants.OVERLAY_IND, pars)));
		SetOverlayColorFlags(show: false);
	}

	public override void OnInteractive()
	{
		base.OnInteractive();
		HideAnyOverlay();
	}

	public override void OnReleased()
	{
		Game.ctx.console.Remove(this);
		arrows.HideAllArrows();
		arrows.Release();
		arrows = null;
		resources = null;
		base.OnReleased();
	}

	public void HideAnyOverlay()
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			item.components.model.SetOverlayColor(ColorConstants.OVERLAY_DEFAULT);
		}
		if (resources.IsShowingAny)
		{
			HideOverlayForResources();
		}
		arrows.HideAllArrows();
		UnsuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SuppressPicks(_cornerPicks);
		SetOverlayColorFlags(show: false);
	}

	public void ShowHeatOverlay()
	{
		PlayerID humanPlayer = PlayerID.HumanPlayer;
		Color oVERLAY_HEAT = ColorConstants.OVERLAY_HEAT;
		ShowHeatOrRespect(humanPlayer, oVERLAY_HEAT, HeatAtNode);
	}

	public void ShowRespectOverlay()
	{
		PlayerID humanPlayer = PlayerID.HumanPlayer;
		Color oVERLAY_RESPECT = ColorConstants.OVERLAY_RESPECT;
		ShowHeatOrRespect(humanPlayer, oVERLAY_RESPECT, RespAtNode);
	}

	private static void ShowHeatOrRespect(PlayerID pid, Color valcolor, Func<Node, PlayerID, Fixnum> calc)
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			float t = (float)calc(item.components.board.GetNode(), pid) * 0.01f;
			Color overlayColor = Color.Lerp(ColorConstants.OVERLAY_DEFAULT, valcolor, t);
			item.components.model.SetOverlayColor(overlayColor);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		UnsuppressPicks(_cornerPicks);
		SetOverlayColorFlags(show: true);
	}

	private static Fixnum HeatAtNode(Node node, PlayerID pid)
	{
		return node.heat.GetOrNull(pid)?.CalculateBaseValue(new ModQuery(pid, node)) ?? ((Fixnum)0);
	}

	private static Fixnum RespAtNode(Node node, PlayerID pid)
	{
		return node.respect.GetOrNull(pid)?.CalculateBaseValue(new ModQuery(pid)) ?? ((Fixnum)0);
	}

	public void RecolorAllBuildingsToDefault()
	{
		Color oVERLAY_DEFAULT = ColorConstants.OVERLAY_DEFAULT;
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			item.components.model.SetOverlayColor(oVERLAY_DEFAULT);
		}
	}

	public void ShowOverlayForOrderArrows()
	{
		RecolorAllBuildingsToDefault();
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
	}

	public void ShowOverlayForRelationshipArrows(List<EntityID> targets, bool suppress)
	{
		if (targets.Count == 0)
		{
			return;
		}
		RecolorAllBuildingsToDefault();
		foreach (EntityID target in targets)
		{
			Entity item = BoardUtil.FindBoardInfoFor(target).boardEntity;
			if (item?.components.building != null)
			{
				item.components.model.SetOverlayColor(ColorConstants.ARROW_REL);
			}
		}
		if (suppress)
		{
			CurrentRelMode = RelMode.Suppressed;
		}
		else
		{
			CurrentRelMode = RelMode.Default;
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
	}

	private static void SuppressPicks(params PickHideRequest[] requests)
	{
		foreach (PickHideRequest req in requests)
		{
			Game.ctx.hud.picks.AddSuppressRequest(req);
		}
	}

	private static void UnsuppressPicks(params PickHideRequest[] requests)
	{
		foreach (PickHideRequest req in requests)
		{
			Game.ctx.hud.picks.RemoveSuppressRequest(req);
		}
	}

	public List<OverlayResDir> ConvertEntityResToOverlayRes(List<EntityResDir> entityRes)
	{
		List<OverlayResDir> list = new List<OverlayResDir>();
		foreach (EntityResDir entityRe in entityRes)
		{
			if (entityRe.pickColor != default(Color) || entityRe.pickIcon != null || entityRe.pickMO != null)
			{
				list.Add(new OverlayResDir
				{
					eid = entityRe.eid,
					pickColor = entityRe.pickColor,
					pickIcon = entityRe.pickIcon,
					pickMO = entityRe.pickMO,
					resources = new List<EntityResDir>()
				});
			}
			else
			{
				list.Add(new OverlayResDir
				{
					resources = new List<EntityResDir> { entityRe },
					eid = entityRe.eid
				});
			}
		}
		return list;
	}

	public void ShowOverlayForRecentResources()
	{
		using ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate();
		Game.ctx.players.Human.skills.ProduceBusinessesThatTradedRecently(pooledBlockList);
		List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
		resources.SetMode(ResourceState.Mode.AnyRecent, entries);
		ShowOverlayForResourcesHelper();
	}

	public void ShowOverlayForResources(List<Resource> res)
	{
		using ListPool<OverlayResDir>.PooledBlockList pooledBlockList = ListPool<OverlayResDir>.Allocate();
		ProduceBusinessesThatTradeResources(res, pooledBlockList);
		resources.SetResources(res, pooledBlockList);
		ShowOverlayForResourcesHelper();
	}

	public void ToggleResourceInOverlay(List<Resource> listRes)
	{
		foreach (Resource listRe in listRes)
		{
			using ListPool<OverlayResDir>.PooledBlockList pooledBlockList = ListPool<OverlayResDir>.Allocate();
			ProduceBusinessesThatTradeResources(new List<Resource> { listRe }, pooledBlockList);
			if (resources.res.Contains(listRe))
			{
				resources.RemoveResource(listRe, pooledBlockList);
			}
			else
			{
				resources.AddResource(listRe, pooledBlockList);
			}
		}
		ShowOverlayForResourcesHelper();
	}

	private void ShowOverlayForResourcesHelper()
	{
		RecolorAllBuildingsToDefault();
		ShowHumanBuildingsAndOutposts();
		foreach (OverlayResDir value in resources.buildings.Values)
		{
			Color pickColor = value.GetPickColor();
			value.eid.FindEntity().components.model.SetOverlayColor(pickColor);
		}
		SuppressPicks(_buildingPicks, _summaryPicks);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
		SetOverlayColorFlags(show: true);
	}

	public void RefreshResources()
	{
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public void HideOverlayForResources()
	{
		resources.Reset();
		HideAnyOverlay();
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public OverlayResDir FindResourceOverlayForBuilding(Entity building)
	{
		if (!resources.IsShowingAny)
		{
			return OverlayResDir.EMPTY;
		}
		return resources.FindBuilding(building.Id);
	}

	public static void ShowOverlayForZone(ZoneType zoneType, Color desiredColor)
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			Color overlayColor = ((item.config.building.type == zoneType) ? desiredColor : ColorConstants.OVERLAY_DEFAULT);
			item.components.model.SetOverlayColor(overlayColor);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
	}

	public static void ShowOverlayForEthnicity(Label ethnicity, Color desiredColor)
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			Color color = ColorConstants.OVERLAY_ETH_DEFAULT;
			if (item.components.residence != null)
			{
				var (num, num2) = item.components.residence.CountEthnicApartments(ethnicity);
				if (num > 0 && num2 > 0)
				{
					float value = (float)num / (float)num2;
					value = MathUtil.Clamp(value, 0f, 1f);
					color = Color.Lerp(color, desiredColor, value);
					color.a = ((value > 0f) ? desiredColor.a : 0f);
				}
			}
			else if (item.components.building != null && item.data.building.business.IsValid)
			{
				Label label = Label.NULL;
				BizData bizData = BuildingUtil.FindBizForBuilding(item)?.data.biz;
				if (bizData != null)
				{
					if (bizData.owner.IsFake)
					{
						label = bizData.owner.eth;
					}
					if (bizData.owner.IsReal)
					{
						label = BuildingUtil.FindOwnerOrManagerForAnyBuilding(item)?.data.person?.eth ?? Label.NULL;
					}
				}
				if (label == ethnicity)
				{
					color = desiredColor;
				}
			}
			item.components.model.SetOverlayColor(color);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
	}

	private static ZoneType FindZoneTypeByName(string name)
	{
		return ALL_ZONE_TYPES.FirstOrDefault((ZoneType type) => type.ToString().ToLowerInvariant() == name.ToLowerInvariant());
	}

	private static void SetOverlayColorFlags(bool show)
	{
		Shader.SetGlobalFloat(OverlayBlend, show ? 1f : 0f);
		Game.ctx.mapdisplay.SetTerritoryTextAlpha(show ? 0f : 1f);
	}

	public void ShowCarShopsOverlay()
	{
		RecolorAllBuildingsToDefault();
		using (ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate())
		{
			foreach (Entity item2 in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
			{
				EntityComponents components = item2.components;
				if (components.modules?.FindVehicleModuleOrNull() != null && components.building.IsScopedBy(PlayerID.HumanPlayer))
				{
					components.model.SetOverlayColor(ColorConstants.OVERLAY_CARS);
					string item = BuildingPickUtil.GenerateBuildingButtonIcon(item2).icon;
					Color aRROW_BUY = ColorConstants.ARROW_BUY;
					string pickMO = BuildingUtil.FindBizForBuilding(item2).data?.biz?.bizname;
					pooledBlockList.Add(new EntityResDir
					{
						eid = item2.Id,
						pickColor = aRROW_BUY,
						pickIcon = item,
						pickMO = pickMO
					});
				}
			}
			List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
			resources.SetMode(ResourceState.Mode.CarShops, entries);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public void ShowAllFrontsOverlay()
	{
		using (ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate())
		{
			foreach (PlayerInfo item2 in Game.ctx.players.all)
			{
				if ((!item2.IsHuman && !item2.IsJustGang) || (!item2.IsHuman && !item2.meetings.IsPlayerMet(PlayerID.HumanPlayer)))
				{
					continue;
				}
				Color pickColor = item2.territory.colorInfo.GetPlayerColor() + new Color(0.5f, 0.5f, 0.5f);
				string text = item2.social.FindPlayerGroupNameColorized();
				foreach (OutpostEntry item3 in item2.outposts.GetOutpostEntriesUnsafe())
				{
					Entity entity = item3.outpostId.buildingId.FindEntity();
					string text2 = BuildingUtil.FindBizForBuilding(entity)?.data?.biz?.bizname ?? "";
					text2 = (text2 + "\n" + Loc.Get("ui.overlays.fronts.who", "groupname", text)).Trim();
					string item = BuildingPickUtil.GenerateBuildingButtonIcon(entity, forceScoped: true).icon;
					pooledBlockList.Add(new EntityResDir
					{
						eid = entity.Id,
						pickColor = pickColor,
						pickIcon = item,
						pickMO = text2
					});
				}
			}
			List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
			resources.SetMode(ResourceState.Mode.Fronts, entries);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public void ShowDeliveriesOverlay(List<Entity> destinations)
	{
		RecolorAllBuildingsToDefault();
		using (ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate())
		{
			foreach (Entity destination in destinations)
			{
				Color aRROW_SELL = ColorConstants.ARROW_SELL;
				string item = BuildingPickUtil.GenerateBuildingButtonIcon(destination, forceScoped: true).icon;
				string pickMO = BuildingUtil.FindBuildingName(destination) ?? "";
				pooledBlockList.Add(new EntityResDir
				{
					eid = destination.Id,
					pickColor = aRROW_SELL,
					pickIcon = item,
					pickMO = pickMO
				});
			}
			List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
			resources.SetMode(ResourceState.Mode.Deliveries, entries);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public void ShowAllControlledOverlay()
	{
		RecolorAllBuildingsToDefault();
		using (ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate())
		{
			foreach (PlayerInfo item2 in Game.ctx.players.all)
			{
				if ((!item2.IsHuman && !item2.IsGangOrGoon) || (!item2.IsHuman && !item2.meetings.IsPlayerMet(PlayerID.HumanPlayer)))
				{
					continue;
				}
				Color playerColor = item2.territory.colorInfo.GetPlayerColor();
				string text = item2.social.FindPlayerGroupNameColorized();
				foreach (EntityID item3 in item2.territory.GetAllControlledBuildingsUnsafe())
				{
					string text2 = BuildingUtil.FindBuildingName(item3) ?? Loc.Get("ui.overlays.safehouse");
					text2 = (text2 + "\n" + Loc.Get("ui.overlays.controlled.who", "groupname", text)).Trim();
					string item = BuildingPickUtil.GenerateBuildingButtonIcon(item3.FindEntity(), forceScoped: true).icon;
					pooledBlockList.Add(new EntityResDir
					{
						eid = item3,
						pickColor = playerColor,
						pickIcon = item,
						pickMO = text2
					});
				}
			}
			List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
			resources.SetMode(ResourceState.Mode.Fronts, entries);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public void ShowPrecinctsOverlay()
	{
		using (ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate())
		{
			foreach (Entity item3 in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
			{
				if (item3.components.police == null && item3.components.civic == null)
				{
					PrecinctID precinctId = item3.components.board.GetNode().precinctId;
					if (precinctId.IsValid)
					{
						Color overlayColor = FindPrecinctColor(precinctId.id);
						item3.components.model.SetOverlayColor(overlayColor);
					}
				}
				else if (item3.components.police != null)
				{
					item3.components.model.SetOverlayColor(ColorConstants.POLICE_STATION);
					(bool scoped, string icon) tuple = BuildingPickUtil.GenerateBuildingButtonIcon(item3);
					bool item = tuple.scoped;
					string item2 = tuple.icon;
					Color playerBuildingButtonColor = BuildingPickUtil.GetPlayerBuildingButtonColor(item3, scoped: true, crewhere: true);
					string pickMO = (item ? Loc.Get("ui.overlays.precincts.confirmed") : Loc.Get("ui.overlays.precincts.sus"));
					pooledBlockList.Add(new EntityResDir
					{
						eid = item3.Id,
						pickIcon = item2,
						pickColor = playerBuildingButtonColor,
						pickMO = pickMO
					});
				}
			}
			List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
			resources.SetMode(ResourceState.Mode.PoliceStations, entries);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	public void ShowWardsOverlay()
	{
		using (ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate())
		{
			foreach (Entity item3 in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
			{
				if (item3.components.police == null && item3.components.civic == null)
				{
					PrecinctID precinctId = item3.components.board.GetNode().precinctId;
					if (precinctId.IsValid)
					{
						Color overlayColor = FindPrecinctColor(precinctId.id);
						item3.components.model.SetOverlayColor(overlayColor);
					}
				}
				else if (item3.components.civic != null && item3.config.civic.IsWard)
				{
					item3.components.model.SetOverlayColor(ColorConstants.POLICE_STATION);
					(bool scoped, string icon) tuple = BuildingPickUtil.GenerateBuildingButtonIcon(item3);
					bool item = tuple.scoped;
					string item2 = tuple.icon;
					Color pOLICE_STATION = ColorConstants.POLICE_STATION;
					string pickMO = (item ? Loc.GetWithPolUnit("ui.overlays.wards.ward-confirmed") : Loc.GetWithPolUnit("ui.overlays.wards.ward-sus"));
					pooledBlockList.Add(new EntityResDir
					{
						eid = item3.Id,
						pickIcon = item2,
						pickColor = pOLICE_STATION,
						pickMO = pickMO
					});
				}
			}
			List<OverlayResDir> entries = ConvertEntityResToOverlayRes(pooledBlockList);
			resources.SetMode(ResourceState.Mode.PoliceStations, entries);
		}
		ShowHumanBuildingsAndOutposts();
		SuppressPicks(_buildingPicks, _peepPicks, _summaryPicks);
		SetOverlayColorFlags(show: true);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOverlayResourcesChanged);
	}

	private static Color FindPrecinctColor(short precinctId)
	{
		List<Color> precinctColors = ColorConstants.PrecinctColors;
		int index = precinctId % precinctColors.Count;
		int num = precinctId / precinctColors.Count;
		Color color = precinctColors[index];
		float num2 = 0.1f * (float)num;
		return new Color(color.r + num2, color.g + num2, color.b + num2, color.a);
	}

	private static void ShowHumanBuildingsAndOutposts()
	{
		PlayerInfo human = Game.ctx.players.Human;
		Color playerColor = human.territory.colorInfo.GetPlayerColor();
		foreach (OutpostEntry item in human.outposts.GetOutpostEntriesUnsafe())
		{
			item.outpostId.FindBuilding().components.model.SetOverlayColor(playerColor);
		}
		foreach (EntityID item2 in human.territory.GetAllControlledBuildingsUnsafe())
		{
			item2.FindEntity().components.model.SetOverlayColor(playerColor);
		}
	}

	public void ProduceBusinessesThatTradeResources(List<Resource> resources, List<OverlayResDir> results)
	{
		foreach (Entity allKnownBusiness in GetAllKnownBusinesses())
		{
			using ListPool<EntityResDir>.PooledBlockList pooledBlockList = ListPool<EntityResDir>.Allocate();
			foreach (Resource resource in resources)
			{
				GetResourcesStatusAtBiz(resource, allKnownBusiness, pooledBlockList);
			}
			if (pooledBlockList.Count != 0)
			{
				results.Add(new OverlayResDir
				{
					resources = new List<EntityResDir>(pooledBlockList),
					eid = allKnownBusiness.Id
				});
			}
		}
	}

	public List<Entity> GetAllKnownBusinesses()
	{
		IEnumerable<Node> allKnownNodesExpensive = Game.ctx.players.Human.territory.GetAllKnownNodesExpensive();
		List<Entity> list = new List<Entity>();
		foreach (Node item in allKnownNodesExpensive)
		{
			using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
			item.FindBuildingsToShowGuitarPicks(PlayerID.HumanPlayer, pooledBlockList);
			list.AddRange(pooledBlockList);
		}
		return list;
	}

	public void GetResourcesStatusAtBiz(Resource res, Entity biz, List<EntityResDir> resDirs)
	{
		var (flag, toBldg) = Game.ctx.players.Human.skills.CanBuySell(res, biz);
		if (flag)
		{
			resDirs.Add(new EntityResDir
			{
				res = res,
				eid = biz.Id,
				toBldg = toBldg
			});
		}
	}

	private string ConsoleDisableOverlays(params string[] arg)
	{
		HideAnyOverlay();
		return "Disabling overlays";
	}

	private static string ConsoleBuildingTypeOverlay(Color desiredColor, params string[] arg)
	{
		string text = arg[2];
		ShowOverlayForZone(FindZoneTypeByName(text), desiredColor);
		return "Showing overlay: " + text;
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable rawdata)
	{
		SaveLoadUtils.DeserializeSingleKey(rawdata, "data", delegate(ResBarPersistedData result)
		{
			data = result;
		});
		yield break;
	}
}
