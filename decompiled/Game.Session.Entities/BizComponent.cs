using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class BizComponent : BaseComponent
{
	public struct TradeRestrictions
	{
		public PlayerID tiedHouseLock;

		public PlayerID territoryLock;

		public PlayerID forcedClosedBy;

		public bool IsTerritoryLocked => territoryLock.IsValid;

		public bool IsTiedHouseLocked => tiedHouseLock.IsValid;

		public bool IsForcedClosed => forcedClosedBy.IsValid;

		public bool IsLocked
		{
			get
			{
				if (!tiedHouseLock.IsValid && !territoryLock.IsValid)
				{
					return forcedClosedBy.IsValid;
				}
				return true;
			}
		}

		public TradeRestrictions(PlayerID tiedHouseLock, PlayerID territoryLock, PlayerID forcedClosedBy)
		{
			this.tiedHouseLock = tiedHouseLock;
			this.territoryLock = territoryLock;
			this.forcedClosedBy = forcedClosedBy;
		}

		public override string ToString()
		{
			return $"Trade restrictions: tied = {tiedHouseLock}, terr = {territoryLock}, force = {forcedClosedBy}";
		}
	}

	public BizConfig Config => _baseConfig as BizConfig;

	public bool HasOwner => _entity.data.biz.owner.IsOwnerSet;

	public bool HasBuilding => _entity.data.biz.building.IsValid;

	public EntityID OwnerID => _entity.data.biz.owner.id;

	public EntityID BuildingID => _entity.data.biz.building;

	private BizData Data => _entity.data.biz;

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
		if (!loaded)
		{
			_entity.data.biz.modules = Config.PickModulesForBuilding(_entity);
		}
	}

	public void AssignRealOwner(Entity owner)
	{
		Entity entity = _entity;
		owner.data.person.business = entity.Id;
		entity.data.biz.owner = BizOwner.MakeReal(owner);
		entity.data.biz.InitializeRealName(Config);
	}

	public void AssignFakeOwner(Label eth)
	{
		Entity obj = _entity;
		obj.data.biz.owner = BizOwner.MakeFake(eth);
		obj.data.biz.InitializeFakeName(Config, eth);
	}

	public void ClearOwner(bool shutdown)
	{
		Entity entity = _entity;
		if (!entity.data.biz.owner.IsOwnerSet && shutdown)
		{
			return;
		}
		if (entity.data.biz.owner.IsReal)
		{
			Entity entity2 = entity.data.biz.owner.id.FindEntity();
			if (entity2 == null)
			{
				return;
			}
			entity2.data.person.business = 0uL;
		}
		entity.data.biz.owner = BizOwner.INVALID;
		entity.data.biz.ClearName();
	}

	public bool HasOwnerWithEthnicity(Label eth)
	{
		BizOwner owner = Data.owner;
		if (owner.IsOwnerSet)
		{
			return owner.eth == eth;
		}
		return false;
	}

	public bool HasInterestingModules()
	{
		foreach (Label module in Data.modules)
		{
			IModuleConfig config = ModulesUtil.FindModuleDef(module);
			if (IsInterestingModule(config))
			{
				return true;
			}
		}
		return false;
	}

	private bool IsInterestingModule(IModuleConfig config)
	{
		if (config is ManufactureModuleConfig manufactureModuleConfig)
		{
			return manufactureModuleConfig.interesting;
		}
		return false;
	}

	internal bool CanInstallModuleInSlot(IModuleConfig module, ModuleSlot slotdef)
	{
		if (!CanInstallModuleInBuilding(module))
		{
			return false;
		}
		if (!slotdef.CanSlotHouseThisModule(module))
		{
			return false;
		}
		return true;
	}

	private bool CanInstallModuleInBuilding(IModuleConfig module)
	{
		PlayerModulesConstraints playerModules = Config.playerModules;
		if (playerModules == null)
		{
			return false;
		}
		ModulePurchaseCost modulePurchaseCost = module.Common?.purchase;
		if (modulePurchaseCost == null)
		{
			return false;
		}
		if (!playerModules.size.ContainsAtLeastOneOf(modulePurchaseCost.size))
		{
			return false;
		}
		if (!playerModules.verticals.ContainsAtLeastOneOf(modulePurchaseCost.verticals))
		{
			return false;
		}
		return true;
	}

	public void OnBizActivationChange(Entity building, bool activated)
	{
		if (activated)
		{
			OnBizActivated(building);
		}
		else
		{
			OnBizDeactivated(building);
		}
	}

	public void OnBizDeactivated(Entity building)
	{
		Game.ctx.hud.HideGroup(BaseHUDDialog.GroupType.ConvoGroup);
		Game.ctx.overlays.arrows.HideArrowsForBuilding(building.Id);
	}

	public void OnBizActivated(Entity building)
	{
		if (Game.ctx.players.Human.territory.IsControlled(building))
		{
			BuildingUtil.StartOwnedBuildingInteraction(building, ExecuteOwnedBizInteraction);
			Game.ctx.overlays.arrows.ShowArrowsForBuilding(building);
		}
		else if (building.components.building.IsInteractableByPlayer(PlayerID.HumanPlayer))
		{
			Game.ctx.overlays.arrows.ShowArrowsForBuilding(building);
			StartConversation(_entity, fromBizDialog: false);
		}
		else
		{
			Game.ctx.hud.bldginfo.Toggle(building, show: true);
		}
	}

	private static void ExecuteOwnedBizInteraction(EntityID chosenPeep, Entity building)
	{
		BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(building);
		VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPeep(chosenPeep), bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		Game.ctx.hud.ownedBiz.Show(visit);
	}

	public static void StartConversation(Entity biz, bool fromBizDialog)
	{
		if (KeyUtil.IsShiftDown)
		{
			StartConversation(BuildingUtil.GetQuickTarget(BuildingUtil.FindBuildingForBiz(biz)), biz, fromBizDialog);
			return;
		}
		EntitySelectionPopup.ShowCrewSelector(BuildingUtil.FindBuildingForBiz(biz).components.board.GetNodeID(), Loc.Get("ui.crewinfo.pickone.biz"), delegate(EntityID eid)
		{
			StartConversation(Game.ctx.players.Human.crew.GetCrewForPeep(eid), biz, fromBizDialog);
		}, delegate
		{
			BuildingUtil.DeselectOnNextFrame();
		});
	}

	public static void StartConversation(VisitState visit, bool fromBizDialog)
	{
		StartConversation(visit.crew, visit.biz, fromBizDialog);
	}

	private static void StartConversation(CrewAssignment crew, Entity biz, bool fromBizDialog)
	{
		ConversationModel.Source source = (fromBizDialog ? ConversationModel.Source.ControlledBusiness : ConversationModel.Source.None);
		Game.ctx.hud.convoDialog.Controller.StartBizVisit(biz, crew, source);
	}

	public bool PaysNoTribute()
	{
		return Data.tribute?.accepted.IsNotSet ?? true;
	}

	public bool PaysTributeToAny()
	{
		return Data.tribute?.accepted.IsSet ?? false;
	}

	public bool PaysTributeTo(PlayerID pid)
	{
		return Data.tribute?.accepted.Is(pid) ?? false;
	}

	public bool RejectedTributeTo(PlayerID pid)
	{
		return Data.tribute?.rejected.Get(pid) ?? false;
	}

	public bool HasHumanMentionedTribute()
	{
		return Data.tribute?.humanasked ?? false;
	}

	public void SetHumanMentionedTribute(bool value)
	{
		Data.GetTributeOrAdd().humanasked = value;
	}

	public void StartPayingTributeTo(PlayerID pid, OutpostID outpost, Price amount)
	{
		TributeData tributeOrAdd = Data.GetTributeOrAdd();
		if (tributeOrAdd.accepted.IsSet)
		{
			StopPayingTributeTo(tributeOrAdd.accepted.pid);
		}
		tributeOrAdd.accepted.Set(pid);
		tributeOrAdd.rejected.Set(pid, value: false);
		tributeOrAdd.amount = amount;
		tributeOrAdd.outpost = outpost;
	}

	public void StopPayingTributeTo(PlayerID pid)
	{
		TributeData tributeOrNull = Data.GetTributeOrNull();
		if (tributeOrNull != null)
		{
			if (tributeOrNull.accepted.IsSet)
			{
				tributeOrNull.accepted.Clear();
			}
			tributeOrNull.amount = Price.ZERO;
			tributeOrNull.outpost = OutpostID.INVALID;
		}
	}

	public bool HasDiscount(PlayerID pid, Resource res)
	{
		return GetDiscount(pid, res).exists;
	}

	public (bool exists, Fixnum multiplier) GetDiscount(PlayerID pid, Resource res)
	{
		Discounts.Entry entry = Data.discounts?.GetOrNull(pid, res);
		return (exists: entry != null, multiplier: entry?.multiplier ?? Discounts.Entry.DEFAULT_DISCOUNT);
	}

	public void SetDiscount(PlayerID pid, Resource res, Fixnum multiplier)
	{
		if (multiplier == Discounts.Entry.DEFAULT_DISCOUNT)
		{
			RemoveDiscount(pid, res);
			return;
		}
		Discounts discounts = Data.discounts;
		if (discounts == null)
		{
			discounts = (Data.discounts = new Discounts());
		}
		discounts.GetOrAdd(pid, res).multiplier = multiplier;
	}

	public bool RemoveDiscount(PlayerID pid, Resource res)
	{
		BizData biz = _entity.data.biz;
		if (biz.discounts == null)
		{
			return false;
		}
		biz.discounts.Remove(pid, res);
		if (biz.discounts.data.Count == 0)
		{
			_entity.data.biz.discounts = null;
		}
		return true;
	}

	internal void RecordBuySell(PlayerID pid, Label resid, Fixnum qtyToBuilding)
	{
		Data.GetTradeSummariesOrAdd(pid).Increment(new ResourceAndQty(resid, qtyToBuilding));
	}

	public Fixnum FindBuySellQty(PlayerID pid, Label resid)
	{
		return Data.GetTradeSummariesOrNull(pid)?.Get(resid) ?? Fixnum.ZERO;
	}

	public bool HasAnyTradeHistory()
	{
		if (Data.trades != null)
		{
			foreach (PlayerTradeSummary trade in Data.trades)
			{
				if (trade.HasAnything())
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool HasAnyTradeHistory(PlayerID pid)
	{
		return Data.GetTradeSummariesOrNull(pid)?.HasAnything() ?? false;
	}

	public bool HasAnyTradeHistoryWithAI()
	{
		if (Data.trades != null)
		{
			foreach (PlayerTradeSummary trade in Data.trades)
			{
				if (trade.pid.IsAIPlayer && trade.HasAnything())
				{
					return true;
				}
			}
		}
		return false;
	}

	public IEnumerable<PlayerID> EnumerateAllTradedPlayers()
	{
		if (Data.trades == null)
		{
			yield break;
		}
		foreach (PlayerTradeSummary trade in Data.trades)
		{
			if (trade.HasAnything())
			{
				yield return trade.pid;
			}
		}
	}

	public int RemoveTradeHistoryWithPlayer(PlayerID pid)
	{
		int num = 0;
		if (Data.trades != null)
		{
			for (int num2 = Data.trades.Count - 1; num2 >= 0; num2--)
			{
				if (Data.trades[num2].pid == pid)
				{
					Data.trades.RemoveAt(num2);
					num++;
				}
			}
		}
		return num;
	}

	public void MarkForceClosed(PlayerID originator, PlayerID enemy)
	{
		int days = Game.serv.globals.settings.people.businessSettings.forcedClosure.durationDayz.Evaluate(originator).IntFloor();
		Data.SetForcedClosed(originator, days);
	}

	public IEnumerable<PlayerID> FindTradingAIsAtWarWith(PlayerID pid)
	{
		foreach (PlayerID item in EnumerateAllTradedPlayers())
		{
			if (item.FindPlayer()?.ai?.combat?.IsAggroWithoutTruce(pid) == true)
			{
				yield return item;
			}
		}
	}

	public PlayerID PickTradingAIAtWarWith(PlayerID pid)
	{
		List<PlayerID> list = FindTradingAIsAtWarWith(pid).ToList();
		if (list.Count == 0)
		{
			return PlayerID.INVALID;
		}
		list.StableSort(PlayerID.CompareAscending);
		return _entity.components.ident.GetIdentityRNGUnchanging((uint)Game.ctx.clock.Now.days).PickElementOrDefault(list);
	}

	private (bool isTied, PlayerID pid) GetTiedHouseStatusImpl(bool clearIfExpired)
	{
		if (clearIfExpired)
		{
			TryExpireTiedHouse();
		}
		PlayerID item = Data.GetTiedHouseOrNull()?.pid ?? PlayerID.INVALID;
		return (isTied: item.IsAnyPlayer, pid: item);
	}

	public (bool isTied, PlayerID pid) GetTiedHouseStatus()
	{
		return GetTiedHouseStatusImpl(clearIfExpired: true);
	}

	public bool IsTiedTo(PlayerID pid)
	{
		(bool, PlayerID) tiedHouseStatus = GetTiedHouseStatus();
		if (tiedHouseStatus.Item1)
		{
			return tiedHouseStatus.Item2 == pid;
		}
		return false;
	}

	public bool IsTiedButNotTo(PlayerID pid)
	{
		(bool, PlayerID) tiedHouseStatus = GetTiedHouseStatus();
		if (tiedHouseStatus.Item1)
		{
			return tiedHouseStatus.Item2 != pid;
		}
		return false;
	}

	public bool IsTiedToAnyPlayer()
	{
		return GetTiedHouseStatus().isTied;
	}

	private void TryExpireTiedHouse()
	{
		TiedHouseInfo tiedhouse = Data.tiedhouse;
		if (tiedhouse != null && Game.ctx.clock.Now >= tiedhouse.end)
		{
			ClearTiedHouse();
		}
	}

	public void SetTiedHouse(PlayerID pid, int days)
	{
		Data.tiedhouse = new TiedHouseInfo(pid, days);
		if (pid.IsAnyPlayer)
		{
			Entity entity = BuildingUtil.FindOwnerForBiz(_entity);
			pid.FindPlayer().social.AddBuffFrom(entity.Id, BuffConstants.RELBUFF_TIED_HOUSE);
		}
		if (pid.IsHumanPlayer)
		{
			PlayBuildingVFX(pid);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BIZ_UPDATE, TickerTitle.BIZ_UPDATE, Loc.Get("ui.tickers.bizupdate.tiedhouse.gained", "bizname", Data.bizname, "days", Loc.FormatNumber(days)), _entity.Id);
		}
	}

	public void ClearTiedHouse()
	{
		PlayerID item = GetTiedHouseStatusImpl(clearIfExpired: false).pid;
		Data.tiedhouse = null;
		if (item.IsAnyPlayer)
		{
			Entity entity = BuildingUtil.FindOwnerForBiz(_entity);
			item.FindPlayer().social.RemoveBuffFrom(entity.Id, BuffConstants.RELBUFF_TIED_HOUSE);
		}
		if (item.IsHumanPlayer)
		{
			PlayBuildingVFX(item);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BIZ_UPDATE, TickerTitle.BIZ_UPDATE, Loc.Get("ui.tickers.bizupdate.tiedhouse.lost", "bizname", Data.bizname), _entity.Id);
		}
	}

	public (Price cost, int days) GetTiedHouseParameters(EntityID ownerId, PlayerID askingPlayer, PlayerID? currentPlayer)
	{
		BusinessSettings.TiedHouseSettings tiedHouseSettings = Game.serv.globals.settings.people.businessSettings.tiedHouseSettings;
		NodeID nodeID = BuildingUtil.FindBuildingForBiz(_entity).components.board.GetNodeID();
		ModQuery query;
		ModValue modValue;
		if (currentPlayer.HasValue && currentPlayer.Value.IsHumanPlayer)
		{
			query = new ModQuery(currentPlayer.Value, ownerId, nodeID);
			modValue = tiedHouseSettings.costFromHuman;
		}
		else if (currentPlayer.HasValue && currentPlayer.Value.IsAnyPlayer)
		{
			query = new ModQuery(currentPlayer.Value, ownerId, nodeID);
			modValue = tiedHouseSettings.costFromGang;
		}
		else
		{
			query = new ModQuery(askingPlayer, ownerId, nodeID);
			modValue = tiedHouseSettings.costFromOwner;
		}
		int item = tiedHouseSettings.durationDayz.Evaluate(query).RoundCoarse();
		return (cost: new Price(modValue.Evaluate(query).RoundCoarse()), days: item);
	}

	public TradeRestrictions FindTradeRestrictions(PlayerID candidate)
	{
		PlayerID tiedHouseLock = PlayerID.INVALID;
		PlayerID territoryLock = PlayerID.INVALID;
		PlayerID forcedClosedBy = PlayerID.INVALID;
		PlayerID playerID = BuildingUtil.FindBuildingForBiz(_entity).components.board.GetNode().owner.Get();
		if (playerID.IsPlayerOtherThan(candidate) && !Game.ctx.simman.demands.CanTradeInTerritory(candidate, playerID))
		{
			territoryLock = playerID;
		}
		if (IsTiedButNotTo(candidate))
		{
			tiedHouseLock = Data.GetTiedHouseOrNull().pid;
		}
		if (Data.IsForcedClosed())
		{
			forcedClosedBy = Data.GetForcedClosedOrNull()?.originator ?? PlayerID.INVALID;
		}
		return new TradeRestrictions(tiedHouseLock, territoryLock, forcedClosedBy);
	}

	private void PlayBuildingVFX(PlayerID pid)
	{
		Game.ctx.vfx.PlayOneShotPFXOverBuilding(PFXType.BuildingUpdateFX, BuildingUtil.FindBuildingForBiz(_entity), pid, 3f);
	}

	public void GrantStarterPackToThisBuilding(PlayerInfo player)
	{
		AddToInventory(player, BuildingUtil.FindBuildingForBiz(_entity), Config.starterPack?.building);
	}

	public void GrantStarterPackToCrew(PlayerInfo player, CrewAssignment crew)
	{
		AddToInventory(player, crew.GetVehicle(), Config.starterPack?.crew);
	}

	private bool AddToInventory(PlayerInfo player, Entity target, StarterPack.Entry starters)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(target);
		if (inventory == null)
		{
			return false;
		}
		if (starters?.resources == null)
		{
			return false;
		}
		foreach (KeyValuePair<Label, Fixnum> resource in starters.resources)
		{
			inventory.data.Increment(resource.Key, resource.Value);
		}
		player.finances.DoChangeMoney(inventory.data, new Price(starters.cash), MoneyReason.Other);
		return true;
	}

	[Conditional("UNITY_EDITOR")]
	private void PrintDebugInfo()
	{
		if (Game.settings.DoEnableEntityLogging)
		{
			Data.owner.ToString();
		}
	}
}
