using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class GamblingModule : Module<GamblingModule, GamblingModuleConfig, GamblingModuleData>
{
	public override bool IsEnabled(SimTime time)
	{
		return true;
	}

	public override void Initialize(ModuleInitData init)
	{
		base.Initialize(init);
	}

	public override ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled)
	{
		PlayerInfo player = q.container.data.building.controlled.pid.FindPlayer();
		RunGamblingSim(player, q);
		return ModuleResult.Default;
	}

	public Fixnum FindMinimumOperationalValue(VisitState visit)
	{
		return FindMinimumOperationalValue(visit.pid, visit.building);
	}

	public Fixnum FindMinimumOperationalValue(ModuleQuery q)
	{
		return FindMinimumOperationalValue(q.pid, q.container);
	}

	public Fixnum FindMinimumOperationalValue(PlayerID player, Entity building)
	{
		ModQuery query = new ModQuery(player, building.components.board.GetNodeID());
		Fixnum result = new Fixnum(0);
		foreach (AmenityData amenity in data.amenities)
		{
			AmenityDef amenityDef = Game.serv.globals.settings.gambling.FindAmenityById(amenity.defID);
			result += amenityDef.minOperationCost.Evaluate(query);
		}
		return result;
	}

	public int GetNumberOfActiveDebtors(PlayerInfo player)
	{
		int num = 0;
		foreach (AmenityData amenity in data.amenities)
		{
			foreach (EntityID gambler in amenity.gamblers)
			{
				if (player.gambling.FindGamblerState(gambler.FindEntity()).DebtIsDue)
				{
					num++;
				}
			}
		}
		return num;
	}

	public int GetAmenityOfTypeCount(AmenityDef.AmenityBehavior.BehaviorType type)
	{
		return data.amenities.Count((AmenityData amenity) => amenity.GetAmenityDef().behavior.type == type);
	}

	public ModQuery MakeManagerBasedModQuery(PlayerInfo player, Entity building)
	{
		EntityID entityID = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building)?.Id ?? building.Id;
		return new ModQuery(player.PID, entityID, entityID, building.components.board.GetNodeID());
	}

	private void RunGamblingSim(PlayerInfo player, ModuleQuery q)
	{
		Entity building = q.container;
		PlayerGambling gambling = player.gambling;
		GamblingSettings settings = Game.serv.globals.settings.gambling;
		foreach (AmenityData amenity in data.amenities)
		{
			amenity.lastTurnResults = default(AmenityData.LastTurnResults);
			amenity.cashNeededLastTurn = 0;
			foreach (EntityID gambler in amenity.gamblers)
			{
				gambling.FindGamblerState(gambler).ResetLastCashDelta();
				gambling.ThrowTickerIfRepaymentStatusChanged(gambler);
			}
		}
		PlayerGambling.GamblingHouseStatus gamblingHouseStatus = gambling.GetGamblingHouseStatus(building);
		if (!gamblingHouseStatus.isManagerPresent || !gamblingHouseStatus.isNotDamaged)
		{
			return;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		Fixnum cash = inventory.data.money.cash;
		bool flag = false;
		ModQuery mquery = MakeManagerBasedModQuery(player, building);
		foreach (AmenityData amenity2 in data.amenities)
		{
			if (!amenity2.IsEnabled(Game.ctx.clock.Now))
			{
				continue;
			}
			if (amenity2.WasJustEnabled(Game.ctx.clock.Now))
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.CASINO_AMENITY_DONE, TickerTitle.CASINO_UPDATE, Loc.Get("ui.tickers.casino-amenity-construction-done", "amenityName", Loc.Get(amenity2.GetAmenityDef().locname)), new TickerTarget(building.Id));
				Game.ctx.vfx.PlayOneShotPFX(PFXType.BuildingFX, building.data.board.worldpos, PlayerID.HumanPlayer, 2f);
			}
			Fixnum fixnum = amenity2.GetAmenityDef().minOperationCost.Evaluate(mquery);
			if (cash < fixnum)
			{
				amenity2.cashNeededLastTurn = fixnum - cash;
				flag = true;
				continue;
			}
			cash -= fixnum;
			Price price = new Price(amenity2.GetAmenityDef().weeklyCost.Evaluate(mquery));
			Fixnum fixnum2 = settings.startup.weeklyCostModifier.Evaluate(mquery);
			Price price2 = price * fixnum2;
			if (player.finances.CanChangeMoney(building, price2))
			{
				gambling.PayWeeklyCost(building, price2);
				RequestProcessing(amenity2);
			}
		}
		if (flag && player.IsHuman)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.CASINO_NEEDS_MONEY, TickerTitle.CASINO_NEEDS_MONEY, Loc.Get("ui.tickers.casino-needs-money"), new TickerTarget(building.Id));
		}
		void RequestProcessing(AmenityData amenity)
		{
			AmenityDef amenityDef = settings.FindAmenityById(amenity.defID);
			AmenityDef.AmenityBehavior.BehaviorType type = amenityDef.behavior.type;
			switch (type)
			{
			case AmenityDef.AmenityBehavior.BehaviorType.AOE:
				gambling.RequestAOEProcessing(this, inventory, amenity, mquery);
				break;
			case AmenityDef.AmenityBehavior.BehaviorType.Slots:
			case AmenityDef.AmenityBehavior.BehaviorType.Rake:
			{
				for (int num = amenity.gamblers.Count - 1; num >= 0; num--)
				{
					EntityID gamblerId = amenity.gamblers[num];
					gambling.MaybeLoseGambler(amenity, gamblerId, mquery);
				}
				int num2 = amenityDef.behavior.slots.slotCount.Evaluate(mquery).IntFloor();
				if (amenity.gamblers.Count < num2)
				{
					gambling.RequestNewGambler(building, amenity, mquery);
				}
				if (type == AmenityDef.AmenityBehavior.BehaviorType.Slots)
				{
					gambling.RequestRegularProcessing(this, inventory, amenity, mquery);
				}
				else
				{
					gambling.RequestRakeProcessing(this, inventory, amenity, mquery);
				}
				break;
			}
			default:
				Logger.Warning($"Amenity Behavior Type {type} not implemented yet:");
				break;
			case AmenityDef.AmenityBehavior.BehaviorType.Mod:
				break;
			}
		}
	}
}
