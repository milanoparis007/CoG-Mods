using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class ModulesComponent : BaseComponent, IEntityEventObserverComponent
{
	private List<IModule> _slots;

	public InventoryModule inventory;

	public GamblingModule gambling;

	public List<IBizModule> bizmodules;

	private ModuleResult _lastResult;

	private readonly Fixnum BUY_SELL_DESIRE_MULTIPLIER = 2;

	public ModulesConfig Config => _baseConfig as ModulesConfig;

	internal ModuleResult LastModuleResult => _lastResult;

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
		bizmodules = new List<IBizModule>();
		inventory = null;
		gambling = null;
		bool flag = false;
		if (loaded)
		{
			InstallModulesOnLoad();
			if (_slots.Count != (Config.slots?.Count ?? 0))
			{
				flag = true;
			}
		}
		if (!loaded || flag)
		{
			InitializeEmptySlots(Config);
			if (Config.preinstalled != null)
			{
				InstallModules(Config.preinstalled, Game.ctx.clock.Now);
			}
		}
	}

	private void InitializeEmptySlots(ModulesConfig config)
	{
		if (config.slots == null)
		{
			Logger.Warning($"Module slots not defined in {_entity}");
		}
		int count = config.slots?.Count ?? 0;
		_slots = ListGenerators.ListOfDefaultValues<IModule>(count);
		_entity.data.modules.slotData = ListGenerators.ListOfDefaultValues<IModuleData>(count);
	}

	public override void OnBeforeEntityDestroyed(bool shutdown)
	{
		if (!shutdown)
		{
			for (int i = 0; i < _slots.Count; i++)
			{
				_slots[i]?.Release(shutdown: false);
				_slots[i] = null;
			}
		}
		bizmodules = null;
		inventory = null;
		gambling = null;
		_slots = null;
		base.OnBeforeEntityDestroyed(shutdown);
	}

	public void OnEntityEvent(EntityEventType ev)
	{
		if (!Game.settings.DoEnableEntityLogging || ev != EntityEventType.EntityActivationChanged || !_entity.components.selection.IsActive)
		{
			return;
		}
		if (bizmodules != null && bizmodules.Count > 0)
		{
			bizmodules.SelectToString((IBizModule mod) => mod.BizModuleID.String, ", ");
		}
		if (inventory != null)
		{
			inventory.data.contents.SelectToString((ResourceAndQty item) => $"{item.id}:{item.qty}", " ");
		}
		_ = gambling;
	}

	public List<IModule> GetAllSlotsUnsafe()
	{
		return _slots;
	}

	public TagList GetSkillTagsForAllModules()
	{
		TagList tagList = new TagList();
		foreach (IBizModule bizmodule in bizmodules)
		{
			TagList skills = bizmodule.ModuleConfig.Common.skills;
			if (skills != null)
			{
				tagList.AddRange(skills);
			}
		}
		return tagList;
	}

	public TagList GetIdsOfAllBizModules()
	{
		TagList tagList = new TagList(bizmodules.Select((IBizModule m) => m.ModuleConfig.Id));
		if (gambling != null)
		{
			tagList.Add(gambling.config.Id);
		}
		return tagList;
	}

	private bool HasAnyInstalledSlots()
	{
		int i = 0;
		for (int count = _slots.Count; i < count; i++)
		{
			if (_slots[i] != null)
			{
				return true;
			}
		}
		return false;
	}

	private int FindInstalledModuleIndex(Label id)
	{
		int i = 0;
		for (int count = _slots.Count; i < count; i++)
		{
			IModule module = _slots[i];
			if (module != null && module.ModuleData.Id == id)
			{
				return i;
			}
		}
		return -1;
	}

	private int FindFirstSlotIndexThatCanHouse(IModuleConfig moduleConfig, bool skipAlreadyInstalled = true)
	{
		int i = 0;
		for (int count = _slots.Count; i < count; i++)
		{
			if (!(_slots[i] != null && skipAlreadyInstalled) && Config.slots[i].CanSlotHouseThisModule(moduleConfig))
			{
				return i;
			}
		}
		return -1;
	}

	private void InstallModulesOnLoad()
	{
		List<IModuleData> slotData = _entity.data.modules.slotData;
		_slots = ListGenerators.ListOfDefaultValues<IModule>(slotData.Count);
		int i = 0;
		for (int count = slotData.Count; i < count; i++)
		{
			IModuleData moduleData = slotData[i];
			if (moduleData != null)
			{
				IModuleConfig moduleConfig = ModulesUtil.FindModuleDef(moduleData.Id);
				if (moduleConfig == null)
				{
					Logger.Warning($"Unknown module config at load {moduleData.Id} - removing");
					_entity.data.modules.slotData[i] = null;
				}
				else
				{
					InstallModule(i, new ModuleInitData(moduleConfig, moduleData));
				}
			}
		}
	}

	public void InstallModules(List<Label> ids, SimTime time)
	{
		foreach (Label id in ids)
		{
			InstallModule(id, time, manualInstall: false, updateAllModules: false, alreadyEnabled: true);
		}
		DoUpdate(time, initial: true);
	}

	public bool InstallModuleFromUI(Label id, SimTime time)
	{
		return InstallModule(id, time, manualInstall: true, updateAllModules: true, alreadyEnabled: false);
	}

	public bool InstallModuleManually(Label id, SimTime time)
	{
		return InstallModule(id, time, manualInstall: true, updateAllModules: true, alreadyEnabled: true);
	}

	private bool InstallModule(Label id, SimTime time, bool manualInstall, bool updateAllModules, bool alreadyEnabled)
	{
		IModuleConfig moduleConfig = ModulesUtil.FindModuleDef(id);
		if (moduleConfig == null)
		{
			Logger.Warning($"Unknown module config {id}");
			return false;
		}
		int num = FindFirstSlotIndexThatCanHouse(moduleConfig);
		if (num < 0)
		{
			Entity arg = BuildingUtil.FindBizForBuilding(_entity);
			Logger.Warning($"Failed to install module {id} it doesn't fit any slots in {_entity} / {arg}");
			return false;
		}
		SimTime enable = time;
		PlayerID currentPlayer = Game.ctx.clock.CurrentPlayer;
		if (!alreadyEnabled)
		{
			int deltaDays = moduleConfig.Common.purchase.FindInstallDays(currentPlayer, _entity);
			enable = enable.IncrementDays(deltaDays);
		}
		ModuleInitData init = new ModuleInitData(moduleConfig, _entity.data.ident.rng, enable);
		bool num2 = InstallModule(num, init);
		if (num2 && updateAllModules)
		{
			DoUpdate(time, initial: true);
		}
		if (num2 && manualInstall)
		{
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingModuleInstalled, _entity.Id, currentPlayer));
		}
		return num2;
	}

	public bool HasModuleInstalled(Label id)
	{
		return FindInstalledModuleIndex(id) >= 0;
	}

	public void RemoveModules(List<Label> ids, bool shutdown)
	{
		if (shutdown)
		{
			return;
		}
		foreach (Label id in ids)
		{
			int num = FindInstalledModuleIndex(id);
			if (num >= 0)
			{
				RemoveModule(num, id, shutdown);
			}
		}
	}

	public void RemoveModulesByTag(Label tag, bool shutdown)
	{
		if (shutdown || _slots == null)
		{
			return;
		}
		using ListPool<Label>.PooledBlockList pooledBlockList = ListPool<Label>.Allocate();
		FillModuleIdsByTag(tag, pooledBlockList);
		RemoveModules(pooledBlockList, shutdown);
		_ = pooledBlockList.Count;
		_ = 1;
	}

	public ModuleSlot FindModuleSlotForModule(IModule module)
	{
		if (module != null && _slots.Count == Config.slots.Count)
		{
			int i = 0;
			for (int count = _slots.Count; i < count; i++)
			{
				if (_slots[i] == module)
				{
					return Config.slots[i];
				}
			}
		}
		return null;
	}

	private ModuleSlot FirstModuleSlotByTag(Label tag)
	{
		List<ModuleSlot> slots = Config.slots;
		int i = 0;
		for (int count = slots.Count; i < count; i++)
		{
			if (slots[i].tags.Contains(tag))
			{
				return slots[i];
			}
		}
		return null;
	}

	public ModuleSlot FindBackroomModuleSlot()
	{
		return FirstModuleSlotByTag(TagConstants.TAG_SAFEHOUSE_BACKROOMS);
	}

	public ModuleSlot FindFrontroomModuleSlot()
	{
		return FirstModuleSlotByTag(TagConstants.TAG_SAFEHOUSE_FRONTROOMS);
	}

	public void RemoveBackroomModules()
	{
		RemoveModulesByTag(TagConstants.TAG_SAFEHOUSE_BACKROOMS, shutdown: false);
	}

	public void RemoveFrontroomModules()
	{
		RemoveModulesByTag(TagConstants.TAG_SAFEHOUSE_FRONTROOMS, shutdown: false);
	}

	public bool HasBackroomModules()
	{
		return HasModulesByTag(TagConstants.TAG_SAFEHOUSE_BACKROOMS);
	}

	public bool HasFrontroomModules()
	{
		return HasModulesByTag(TagConstants.TAG_SAFEHOUSE_BACKROOMS);
	}

	internal void InstallSmallInventory()
	{
		Label id = (Label)"inventory-basement-small";
		SimTime now = Game.ctx.clock.Now;
		InstallModuleManually(id, now);
	}

	public int FirstModuleIndexByTag(Label tag)
	{
		int i = 0;
		for (int count = _slots.Count; i < count; i++)
		{
			if (_slots[i]?.ModuleConfig?.Common.tags.Contains(tag) == true)
			{
				return i;
			}
		}
		return -1;
	}

	public bool HasModulesByTag(Label tag)
	{
		return FirstModuleIndexByTag(tag) >= 0;
	}

	public IModule FirstModuleByTag(Label tag)
	{
		return _slots.GetOrDefaultFast(FirstModuleIndexByTag(tag));
	}

	public IModule FindBackroomModule()
	{
		return FirstModuleByTag(TagConstants.TAG_SAFEHOUSE_BACKROOMS);
	}

	public IModule FindFrontroomModule()
	{
		return FirstModuleByTag(TagConstants.TAG_SAFEHOUSE_FRONTROOMS);
	}

	private void FillModuleIdsByTag(Label tag, List<Label> resultModuleIds)
	{
		int i = 0;
		for (int count = _slots.Count; i < count; i++)
		{
			IModule module = _slots[i];
			if (module != null && module.ModuleConfig != null && module.ModuleConfig.Common.tags.Contains(tag))
			{
				resultModuleIds.Add(module.ModuleConfig.Id);
			}
		}
	}

	private bool InstallModule(int index, ModuleInitData init)
	{
		IModule module = init.config.MakeModule();
		_slots[index] = module;
		module.Initialize(init);
		if (init.IsCreated)
		{
			_entity.data.modules.slotData[index] = module.ModuleData;
			ApplyInstallGrants(module);
		}
		TryRegisterInventory(module);
		TryRegisterGambling(module);
		TryRegisterResourceManager(module);
		return true;
	}

	internal void ApplyInstallGrantsAtStartup()
	{
		foreach (IModule slot in _slots)
		{
			if (slot != null)
			{
				ApplyInstallGrants(slot);
			}
		}
	}

	private void ApplyInstallGrants(IModule module)
	{
		VisitGrantList visitGrantList = module.ModuleConfig?.Common?.installgrants;
		if (visitGrantList != null)
		{
			PlayerID pid = _entity.data.building.controlled.Get();
			if (!pid.IsNotAnyPlayer)
			{
				BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(_entity);
				GrantContext ctx = new GrantContext(new VisitState(CrewAssignment.EMPTY, bbdata, Game.ctx.clock.Now, pid));
				visitGrantList.ApplyAll(ctx);
			}
		}
	}

	internal void MaybeShowConstructionFinishedFeedback(Label _)
	{
		PlayerID pid = _entity.data.building.controlled.Get();
		if (pid.IsHumanPlayer)
		{
			string text = TextUtil.ColorWrap(Loc.Get("ui.flyout.construction"), ColorConstants.TEXT_HEX_CONSTRUCTION);
			WorldPos worldpos = _entity.data.board.worldpos;
			Game.ctx.hud.flyouts.MakeSimpleTextFlyout(worldpos, text, 3f);
			Game.ctx.vfx.PlayOneShotPFX(PFXType.BuildingFX, worldpos, PlayerID.HumanPlayer, 2f);
			Game.ctx.simman.hints.ShowConstructionHint();
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingConstructionStateChanged, _entity.Id, pid));
		}
	}

	private bool RemoveModule(int index, Label id, bool shutdown)
	{
		IModule module = _slots[index];
		TryUnregisterInventory(module);
		TryUnregisterGambling(module);
		TryUnregisterResourceManager(module);
		module.Release(shutdown);
		_entity.data.modules.slotData[index] = null;
		_slots[index] = null;
		return true;
	}

	private void TryRegisterInventory(IModule module)
	{
		if (module is InventoryModule inventoryModule)
		{
			inventory = inventoryModule;
		}
	}

	private void TryUnregisterInventory(IModule module)
	{
		if (module is InventoryModule)
		{
			inventory = null;
		}
	}

	private void TryRegisterGambling(IModule module)
	{
		if (module is GamblingModule gamblingModule)
		{
			gambling = gamblingModule;
		}
	}

	private void TryUnregisterGambling(IModule module)
	{
		if (module is GamblingModule)
		{
			gambling = null;
		}
	}

	private void TryRegisterResourceManager(IModule module)
	{
		if (module is IBizModule item)
		{
			bizmodules.Add(item);
		}
	}

	private void TryUnregisterResourceManager(IModule module)
	{
		if (module is IBizModule item)
		{
			bizmodules.Remove(item);
		}
	}

	public bool HasInterestingModule()
	{
		if (bizmodules.Count > 0)
		{
			return bizmodules.Any((IBizModule m) => m.IsInteresting);
		}
		return false;
	}

	public void SetEnabledOn(SimTime time)
	{
		foreach (IBizModule bizmodule in bizmodules)
		{
			bizmodule.ModuleData.EnableTime = time;
		}
	}

	internal void OnActivation(bool _)
	{
	}

	internal void DoUpdate(SimTime time, bool initial)
	{
		ModuleQuery q = ModulesUtil.MakeModuleQuery(_entity);
		bool flag = q.IsBuilding && q.OwnerIsHumanPlayer;
		ModuleResult moduleResult = ModuleResult.Default;
		foreach (IModule slot in _slots)
		{
			if (slot != null)
			{
				bool enabled = slot.IsEnabled(time);
				ModuleResult moduleResult2 = slot.DoUpdate(q, time, initial, enabled);
				if (flag && moduleResult2 != ModuleResult.Default)
				{
					RememberHumanProduction(slot, moduleResult2);
					MaybeProvideHumanBackroomFeedback(slot, moduleResult2);
					MaybeProvideHumanFrontroomFeedback(slot, moduleResult2);
					moduleResult |= moduleResult2;
				}
			}
		}
		if (flag && _lastResult != moduleResult)
		{
			MaybeRefreshBuildingPick(_lastResult, moduleResult, PlayerID.HumanPlayer);
			_lastResult = moduleResult;
		}
	}

	private void RememberHumanProduction(IModule slot, ModuleResult result)
	{
		if ((result & ModuleResult.MfgCompleted) != ModuleResult.Default && slot is ManufactureModule manufactureModule)
		{
			Game.ctx.players.Human.territory.AddToProductionHistory(manufactureModule.data.lastProduced);
		}
	}

	private void MaybeProvideHumanBackroomFeedback(IModule slot, ModuleResult result)
	{
		if (!slot.ModuleConfig.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS))
		{
			return;
		}
		PlayerID humanPlayer = PlayerID.HumanPlayer;
		bool num = (result & ModuleResult.MfgCompleted) != 0;
		bool flag = (result & ModuleResult.MfgOutOfInputs) != 0;
		bool flag2 = (result & ModuleResult.MfgOutOfStorageSpace) != 0;
		bool flag3 = (result & ModuleResult.ConsOutOfInputs) != 0;
		bool flag4 = (result & ModuleResult.BuildingDamaged) != 0;
		if (num)
		{
			Game.ctx.vfx.PlayOneShotPFXOverBuilding(PFXType.ProductionFinishedFX, _entity, humanPlayer, 3f);
			Game.ctx.hud.flyouts.MakeSimpleTextFlyoutOverBuilding(_entity, TextUtil.ColorWrap(Loc.Get("ui.flyout.prod-complete"), ColorConstants.TEXT_HEX_GREEN), 3f);
			Game.ctx.events.EnqueueOnce(SessionEventType.UIProductionComplete, humanPlayer);
		}
		else if (flag || flag2)
		{
			Game.ctx.vfx.PlayOneShotPFXOverBuilding(PFXType.LoseTerritoryFX, _entity, humanPlayer, 3f);
			Game.ctx.hud.flyouts.MakeSimpleTextFlyoutOverBuilding(_entity, TextUtil.ColorWrap(Loc.Get("ui.flyout.prod-stalled"), ColorConstants.TEXT_HEX_RED), 3f);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.STALLED, TickerTitle.STALLED_PROD, Loc.Get(flag2 ? "ui.tickers.production-outofroom" : "ui.tickers.production-stalled"), _entity.Id);
			Game.ctx.events.EnqueueOnce(SessionEventType.UIProductionStalled, humanPlayer);
		}
		else if (flag3)
		{
			Game.ctx.vfx.PlayOneShotPFXOverBuilding(PFXType.LoseTerritoryFX, _entity, humanPlayer, 3f);
			Game.ctx.hud.flyouts.MakeSimpleTextFlyoutOverBuilding(_entity, TextUtil.ColorWrap(Loc.Get("ui.flyout.prod-stalled"), ColorConstants.TEXT_HEX_RED), 3f);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.STALLED, TickerTitle.STALLED_MODULE, Loc.Get("ui.tickers.operation-stalled"), _entity.Id);
			Game.ctx.events.EnqueueOnce(SessionEventType.UIOperationStalled, humanPlayer);
		}
		else if (flag4)
		{
			PlayerInfo playerInfo = humanPlayer.FindPlayer();
			if (playerInfo.territory.IsControlled(_entity) && playerInfo.territory.IsBuildingDamaged(_entity) && !playerInfo.territory.CanBeRepaired(_entity))
			{
				Game.ctx.hud.flyouts.MakeSimpleTextFlyoutOverBuilding(_entity, TextUtil.ColorWrap(Loc.Get("ui.flyout.damaged"), ColorConstants.TEXT_HEX_RED), 3f);
				Game.ctx.events.EnqueueOnce(SessionEventType.UIOperationStalled, humanPlayer);
			}
		}
	}

	private void MaybeProvideHumanFrontroomFeedback(IModule slot, ModuleResult result)
	{
		if (!slot.ModuleConfig.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_FRONTROOMS))
		{
			return;
		}
		bool flag = (result & ModuleResult.MfgLegitOverproduced) != 0;
		if (!(slot is ManufactureModule manufactureModule) || !flag)
		{
			return;
		}
		ResourceAndQtyList lastProduced = manufactureModule.data.lastProduced;
		List<ResourceAndQty> list = new List<ResourceAndQty>();
		foreach (ResourceAndQty item in manufactureModule.CurrentRecipe.produce)
		{
			if (lastProduced.FindIndex(item.id) == -1)
			{
				list.Add(item);
			}
		}
		string text = "";
		foreach (ResourceAndQty item2 in list)
		{
			text += "\n";
			text += item2.FindResource().GetIconAndName();
		}
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.STALLED, TickerTitle.STALLED_PROD, Loc.Get("ui.tickers.production-overproduced", "resources", text), _entity.Id);
	}

	private void MaybeRefreshBuildingPick(ModuleResult lastResult, ModuleResult thisResult, PlayerID pid)
	{
		bool num = (lastResult & ModuleResult.MfgOutOfInputs) != 0;
		bool flag = (thisResult & ModuleResult.MfgOutOfInputs) != 0;
		if (num ^ flag)
		{
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingModuleResultChanged, _entity.Id, pid));
		}
	}

	public (bool playerCanBuy, bool playerCanSell) CanPlayerBuyOrSell(PlayerID pid, Label resId, bool illegalOkay)
	{
		bool item = ProduceAllItemsPlayerCanBuyOrSell(pid, playerBuys: true, playerSells: false).Any((BuySellElement element) => element.item.id == resId && (illegalOkay || !element.item.illegal));
		bool item2 = ProduceAllItemsPlayerCanBuyOrSell(pid, playerBuys: false, playerSells: true).Any((BuySellElement element) => element.item.id == resId && (illegalOkay || !element.item.illegal));
		return (playerCanBuy: item, playerCanSell: item2);
	}

	public IEnumerable<BuySellElement> ProduceAllItemsPlayerCanBuyOrSell(PlayerID pid, bool playerBuys, bool playerSells)
	{
		if (pid.IsHumanPlayer && Game.ctx.players.WithID(pid).territory.IsControlled(_entity))
		{
			yield break;
		}
		ModuleQuery q = new ModuleQuery(pid, _entity, NodeID.INVALID, null, null);
		foreach (IBizModule mfg in bizmodules)
		{
			IEnumerable<MfgItem> enumerable = mfg.ProduceAllItemsInCurrentRecipe();
			foreach (MfgItem item in enumerable)
			{
				if (item.consumed ? playerSells : playerBuys)
				{
					ResourceAndQty stored = inventory.data.Get(item.id);
					Fixnum qty = ComputeDesiredAmountToBuyOrSell(q, item, mfg, stored);
					yield return new BuySellElement
					{
						item = item,
						qty = qty
					};
				}
			}
		}
	}

	public bool HasIllegalBusiness(PlayerID pid)
	{
		foreach (BuySellElement item in ProduceAllItemsPlayerCanBuyOrSell(pid, playerBuys: true, playerSells: true))
		{
			if (item.item.illegal)
			{
				return true;
			}
		}
		return false;
	}

	private Fixnum ComputeDesiredAmountToBuyOrSell(ModuleQuery q, MfgItem item, IBizModule mfg, ResourceAndQty stored)
	{
		if (!item.consumed)
		{
			return stored.qty;
		}
		return mfg.ProduceBuyCap(item.id, q) * BUY_SELL_DESIRE_MULTIPLIER - stored.qty;
	}

	public VehicleModule FindVehicleModuleOrNull()
	{
		for (int i = 0; i < _slots.Count; i++)
		{
			if (_slots[i] is VehicleModule result)
			{
				return result;
			}
		}
		return null;
	}
}
