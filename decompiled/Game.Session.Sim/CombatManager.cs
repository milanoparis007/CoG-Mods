using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Combat;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class CombatManager : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>
{
	private LabelDictionary<WeaponConfig> _allWeaponDefs = new LabelDictionary<WeaponConfig>();

	private LabelDictionary<Resource> _allWeaponResources = new LabelDictionary<Resource>();

	public LabelDictionary<WeaponConfig> GetAllWeaponDefsUnsafe()
	{
		return _allWeaponDefs;
	}

	public WeaponConfig GetFistsWeapon()
	{
		return _allWeaponDefs.FindOrNull(EntityConstants.DEFAULT_WEAPON);
	}

	public void OnSystemTurn()
	{
	}

	public void Initialize(SimulationManager manager)
	{
		foreach (EntityConfig item in Game.ctx.entityman.FindTemplatesByTest((EntityConfig config) => config.weapon != null))
		{
			if (item.ident.IsChild)
			{
				_allWeaponDefs.Add(item.Template, item.weapon);
				Resource resource = item.weapon.FindResource();
				if (resource != null)
				{
					_allWeaponResources.Add(item.Template, resource);
				}
			}
		}
	}

	public void Release()
	{
		_allWeaponDefs.Clear();
		_allWeaponResources.Clear();
	}

	[Conditional("UNITY_EDITOR")]
	private void VerifyDataConsistency()
	{
		foreach (KeyValuePair<Label, WeaponConfig> allWeaponDef in _allWeaponDefs)
		{
			allWeaponDef.Value.FindResource();
		}
	}

	public bool IsWeapon(Label id)
	{
		return _allWeaponDefs.ContainsKey(id);
	}

	public WeaponConfig FindWeaponConfig(Resource res)
	{
		return _allWeaponDefs.FindOrNull(res.entity);
	}

	public WeaponConfig FindWeaponConfig(Label id)
	{
		return _allWeaponDefs.FindOrNull(id);
	}

	public List<WeaponConfig> FindAllWeaponsSorted(CrewAssignment crew, List<WeaponConfig> results = null)
	{
		return FindAllWeaponsSorted(crew.GetVehicle(), results);
	}

	public List<WeaponConfig> FindAllWeaponsSorted(Entity container, List<WeaponConfig> results = null)
	{
		results = results ?? new List<WeaponConfig>();
		results.Clear();
		InventoryModule inventory = ModulesUtil.GetInventory(container);
		if (inventory != null)
		{
			foreach (ResourceAndQty content in inventory.data.contents)
			{
				if (content.qty > 0)
				{
					Resource resource = _allWeaponResources.FindOrNull(content.id);
					if (resource != null)
					{
						results.Add(FindWeaponConfig(resource));
					}
				}
			}
		}
		WeaponConfig item = _allWeaponDefs.FindOrNull(EntityConstants.DEFAULT_WEAPON);
		results.Add(item);
		results.StableSort((WeaponConfig a, WeaponConfig b) => b.high - a.high);
		return results;
	}

	public WeaponConfig FindBestWeapon(CrewAssignment crew)
	{
		return FindBestWeapon(crew.GetVehicle());
	}

	public WeaponConfig FindBestWeapon(Entity container)
	{
		using ListPool<WeaponConfig>.PooledBlockList pooledBlockList = ListPool<WeaponConfig>.Allocate();
		FindAllWeaponsSorted(container, pooledBlockList);
		return pooledBlockList.FirstOrDefaultFast();
	}

	public void AttackBuilding(Entity building, PlayerID attacker)
	{
		PlayerID controllingPlayer = building.components.building.GetControllingPlayer();
		if (BuildingUtil.IsGamblingHouse(building))
		{
			controllingPlayer.FindPlayer().gambling.ProcessCasinoAttack(building, attacker);
		}
		else
		{
			controllingPlayer.FindPlayer().territory.ProcessBuildingAttack(building, attacker);
		}
	}

	public bool CanPayAttackCost(Entity peep)
	{
		CrewCost attackCost = Game.serv.globals.settings.people.social.costs.attackCost;
		return peep.components.agent.CanPay(attackCost);
	}

	internal void DoPayAttackCost(Entity peep)
	{
		CrewCost attackCost = Game.serv.globals.settings.people.social.costs.attackCost;
		peep.components.agent.DoPay(attackCost);
		peep.components.agent.ConsumeAllPoints();
	}

	private (PlayerInfo player, CrewAssignment crew) FindCrew(Entity peep)
	{
		PlayerInfo playerInfo = peep.data.agent.pid.FindPlayer();
		CrewAssignment crewForPeep = playerInfo.crew.GetCrewForPeep(peep.Id);
		return (player: playerInfo, crew: crewForPeep);
	}

	public CombatResults PerformHumanCombat(Entity attacker, Entity target, WeaponConfig attackWeapon)
	{
		CrewAssignment item = FindCrew(attacker).crew;
		var (playerInfo, crew) = FindCrew(target);
		DoPayAttackCost(item.GetPeep());
		WeaponConfig weapon = Game.ctx.simman.combat.FindBestWeapon(target);
		CombatResults combatResults = PerformCombat(item, crew, attackWeapon, weapon);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveKillType, target.Id, playerInfo.PID, combatResults));
		return combatResults;
	}

	public void PerformAICombat(CrewAssignment attacker, CrewAssignment target)
	{
		WeaponConfig weapon = Game.ctx.simman.combat.FindBestWeapon(attacker);
		WeaponConfig weapon2 = Game.ctx.simman.combat.FindBestWeapon(target);
		CombatResults item = PerformCombat(attacker, target, weapon, weapon2);
		if (target.GetPeep().data.agent.pid.IsHumanPlayer)
		{
			ShowCombatResults(new List<CombatResults> { item }, immediate: false, isAttackerAI: true);
		}
	}

	public CombatResults PerformCombat(CrewAssignment crew1, CrewAssignment crew2, WeaponConfig weapon1, WeaponConfig weapon2)
	{
		ModValue vehicleDamageReduction = Game.serv.globals.settings.people.combatSettings.vehicleDamageReduction;
		Entity peep1 = crew1.GetPeep();
		Entity peep2 = crew2.GetPeep();
		PlayerID pid = peep1.data.agent.pid;
		PlayerID pid2 = peep2.data.agent.pid;
		Node node = peep1.components.agent.GetNode();
		Fixnum damage = CalculateDamage(peep1, weapon1, peep2);
		Fixnum damage2 = CalculateDamage(peep2, weapon2, peep1);
		Fixnum fixnum = MathUtil.ClampMax(vehicleDamageReduction.Evaluate(new ModQuery(pid, peep1.Id, peep1.Id)), damage2);
		Fixnum fixnum2 = MathUtil.ClampMax(vehicleDamageReduction.Evaluate(new ModQuery(pid2, peep2.Id, peep2.Id)), damage);
		damage -= fixnum2;
		damage2 -= fixnum;
		bool wasHurt = ApplyDamage(peep2, ref damage);
		bool wasHurt2 = ApplyDamage(peep1, ref damage2);
		Death(peep1, peep2);
		Death(peep2, peep1);
		CombatResults.Entry attacker = new CombatResults.Entry(peep1, weapon1, damage2, wasHurt2, fixnum);
		CombatResults.Entry target = new CombatResults.Entry(peep2, weapon2, damage, wasHurt, fixnum2);
		CombatResults results = new CombatResults
		{
			attacker = attacker,
			target = target,
			nodeId = node.id
		};
		results.attacker.peep.components.agent.IncrementStat(CrewStats.TimesFought, 1);
		results.target.peep.components.agent.IncrementStat(CrewStats.TimesFought, 1);
		bool someoneDied = results.attacker.IsDead || results.target.IsDead;
		node.heat.AddViolenceBuff(pid, peep1.Id, node, someoneDied);
		node.heat.AddViolenceBuff(pid2, peep2.Id, node, someoneDied);
		if (results.target.wasHurt)
		{
			Label socialAction = (results.target.IsDead ? SocialConstants.KILLING : SocialConstants.VIOLENCE);
			pid.FindPlayer().social.PerformSocialActionOn(socialAction, peep2.Id, peep1.Id, Extend);
		}
		if (results.target.IsDead)
		{
			results.attacker.peep.components.agent.IncrementStat(CrewStats.PeepsKilled, 1);
		}
		if (results.attacker.IsDead)
		{
			results.target.peep.components.agent.IncrementStat(CrewStats.PeepsKilled, 1);
		}
		if (pid2.IsAIPlayer)
		{
			pid2.FindPlayer().ai?.social?.OnBeingAttacked(results);
		}
		peep1.components.agent.AddXP(XPSource.FromCombat);
		crew1.GetVehicle()?.components.mobile.ProcessAttack(crew1, weapon2);
		crew2.GetVehicle()?.components.mobile.ProcessAttack(crew2, weapon1);
		if (pid.IsHumanPlayer || pid2.IsHumanPlayer)
		{
			PlayCombatVFX(node, crew1, crew2, damage, damage2);
		}
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.GangWarAction, pid.FindPlayer().crew.GetCrewForPlayerPeep().peepId, pid, pid2));
		return results;
		HistoryLedgerItem Extend(HistoryLedgerItem info)
		{
			info.actor = peep1?.Id ?? default(EntityID);
			info.target = peep2?.Id ?? default(EntityID);
			info.method = weapon1?.resid.String ?? null;
			info.node = results?.nodeId ?? default(NodeID);
			return info;
		}
	}

	public static Fixnum CalculateDamage(Entity attacker, WeaponConfig weapon, Entity target)
	{
		uint num = attacker.data.ident.rng.Generate();
		uint num2 = target.data.ident.rng.Generate();
		uint days = (uint)Game.ctx.clock.Now.days;
		Xorshift rng = new Xorshift(num ^ num2 ^ days);
		Fixnum hitProbability = weapon.GetHitProbability(attacker);
		return rng.CheckProbability(hitProbability) ? weapon.CalculateHitPoints(attacker, rng) : 0;
	}

	private static bool ApplyDamage(Entity target, ref Fixnum damage)
	{
		if (damage.IsZero)
		{
			return false;
		}
		PlayerInfo playerInfo = target.data.agent.pid.FindPlayer();
		if (playerInfo != null && playerInfo.IsInvincibleCheatEnabled)
		{
			return false;
		}
		damage = Fixnum.Min(damage, target.components.agent.CurrentHealth);
		if (playerInfo != null && playerInfo.IsHuman && Game.ctx.tutorial.AreInjuriesSuppressed)
		{
			damage = CapDamage(target.components.agent.CurrentHealth, damage);
		}
		target.components.agent.IncrementHealth(-damage);
		target.components.agent.RememberInjuryAtThisTime();
		Game.ctx.events.EnqueueOnce(SessionEventType.CrewMemberAttacked, playerInfo.PID, target.Id);
		return true;
	}

	private static Fixnum CapDamage(Fixnum currentHealth, Fixnum damage)
	{
		Fixnum fixnum = 75;
		Fixnum b = Fixnum.Max(currentHealth - fixnum, 0);
		return Fixnum.Min(damage, b);
	}

	private static void PlayCombatVFX(Node node, CrewAssignment crew1, CrewAssignment crew2, Fixnum damage1, Fixnum damage2)
	{
		WorldPos pos = node.pos;
		WorldPos pos2 = crew1.GetVehicle()?.data.mobile.worldpos ?? WorldPos.Zero;
		DamageFlyouts(crew2.GetVehicle()?.data.mobile.worldpos ?? WorldPos.Zero, damage1);
		DamageFlyouts(pos2, damage2);
		Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, pos, PlayerID.HumanPlayer, 1f);
	}

	private static void DamageFlyouts(WorldPos pos, Fixnum damage)
	{
		string text = TextUtil.ColorWrap(damage.IsZero ? Loc.Get("ui.fight.damage.miss") : Loc.Get("ui.fight.damage.total", "dealt", damage.ToString()), ColorConstants.TEXT_HEX_RED);
		Game.ctx.hud.flyouts.MakeSimpleTextFlyout(pos, text, 3f);
	}

	private static void Death(Entity attacker, Entity target)
	{
		if (!target.components.agent.HasHealthPointsLeft)
		{
			if (target.data.agent.pid.IsHumanPlayer)
			{
				PassMoney(attacker, target);
			}
			Game.ctx.simman.peoplegen.MarkAsDead(target, Game.ctx.clock.Now);
		}
	}

	private static void PassMoney(Entity attacker, Entity target)
	{
		Entity vehicle = attacker.components.agent.FindCrewAssignment().GetVehicle();
		Entity vehicle2 = target.components.agent.FindCrewAssignment().GetVehicle();
		if (vehicle != null && vehicle2 != null)
		{
			PlayerFinances finances = target.data.agent.pid.FindPlayer().finances;
			PlayerFinances finances2 = attacker.data.agent.pid.FindPlayer().finances;
			Money money = vehicle2.components.modules.inventory.data.money;
			finances.DoChangeMoney(vehicle2, new Price(-1 * money.cash), MoneyReason.CombatDefeat, attacker.Id);
			finances2.DoChangeMoney(vehicle, new Price(money.cash), MoneyReason.CombatVictory, target.Id);
		}
	}

	public static bool CanAttack(CrewAssignment crew)
	{
		return GetAttackTargetsAtSameLocation(crew.GetPeep()) != null;
	}

	public void PerformHealing(PlayerID pid, CrewAssignment crew)
	{
		Fixnum fixnum = Game.serv.globals.settings.people.combatSettings.crewHealingPoints.Evaluate(new ModQuery(pid, crew.peepId, crew.peepId));
		crew.peepId.FindEntity().components.agent.IncrementHealth(+fixnum);
	}

	public Fixnum FindPlayerCrewHitpoints(PlayerInfo player, EntityID? onlySpecificPeep = null)
	{
		Fixnum result = 0;
		foreach (CrewAssignment item in player.crew.GetLiving())
		{
			if (!onlySpecificPeep.HasValue || !(onlySpecificPeep.Value != item.peepId))
			{
				int num = FindBestWeapon(item)?.GetHitpointAverage() ?? 0;
				if (num > 0)
				{
					result += (Fixnum)num;
				}
			}
		}
		return result;
	}

	public Fixnum FindPlayerCrewHealth(PlayerInfo player, EntityID? onlySpecificPeep = null)
	{
		Fixnum result = 0;
		foreach (CrewAssignment item in player.crew.GetLiving())
		{
			if (!onlySpecificPeep.HasValue || !(onlySpecificPeep.Value != item.peepId))
			{
				result += item.GetPeep().data.agent.health;
			}
		}
		return result;
	}

	internal void ShowCombatResults(List<CombatResults> results, bool immediate, bool isAttackerAI)
	{
		CombatSummary summary = PhotoPopupUtils.GenerateCombatSummary(results);
		if (summary.humanCrewDied.IsValid)
		{
			ShowCrewDied(summary);
		}
		if (immediate)
		{
			PhotoPopupUtils.ShowCombatSummary(summary, results, isAttackerAI);
			return;
		}
		if (isAttackerAI)
		{
			Game.ctx.sfx.PlayCombatHappened();
		}
		Game.ctx.hud.tickers.AddTickerCombatResults(summary, results);
	}

	public static List<EntityID> GetAttackTargetsAtSameLocation(Entity peep)
	{
		return GetAttackTargetsAtNode(peep.data.agent.pid, peep.components.agent.NodeID);
	}

	public static List<EntityID> GetAttackTargetsAtNode(PlayerID pid, NodeID nodeId)
	{
		List<EntityID> list = new List<EntityID>();
		foreach (EntityID item in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(nodeId))
		{
			if (CanAttackTarget(pid, item))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static bool CanAttackTarget(PlayerID pid, EntityID target)
	{
		PlayerID pid2 = target.FindEntity().data.agent.pid;
		if (pid == pid2)
		{
			return false;
		}
		PlayerInfo playerInfo = pid2.FindPlayer();
		if (playerInfo.IsCopOrFed)
		{
			return false;
		}
		CrewAssignment crewForPeep = playerInfo.crew.GetCrewForPeep(target);
		bool valueOrDefault = crewForPeep.GetPeep()?.data.person?.IsAlive == true;
		bool isInVehicle = crewForPeep.IsInVehicle;
		if (!(valueOrDefault && isInVehicle))
		{
			return false;
		}
		if (!pid.IsHumanPlayer)
		{
			return pid.FindPlayer().ai.combat.IsAttackAllowed(pid2);
		}
		return true;
	}

	public static void ShowCrewDied(CombatSummary summary)
	{
		bool flag = summary.humanCrewDied.IsValid && summary.humanCrewDied.peepId == Game.ctx.players.Human.social.PlayerPeepId;
		string header = Loc.Get("newspaper-headline.murder", "cornername", summary.cornerName, "groupname", summary.groupName);
		PhotoConfig value = summary.photos.LastOrDefaultFast();
		if (flag)
		{
			Game.serv.ui.AddPopup(new NewspaperPopup(header, value, delegate
			{
				Game.ctx.hud.victory.Controller.ShowGameOver(isDead: true);
			}));
		}
		else
		{
			Game.serv.ui.AddPopup(new PeepDeathPopup(summary.humanCrewDied, ProcessCrewDeathChoice));
		}
	}

	private static void ProcessCrewDeathChoice(CrewDeathEntry entry)
	{
		PlayerInfo player = Game.ctx.players.Human;
		CombatSettings.CrewDeath settings = Game.serv.globals.settings.people.combatSettings.crewDeath;
		EntityID iNVALID = EntityID.INVALID;
		switch (entry.choice)
		{
		case CrewDeathEntry.PlayerChoice.Support:
			player.social.PerformSocialActionOn(settings.supportSocial, entry.peepId, iNVALID, Extend);
			player.crew.AddSupportPayment(entry.peepId, entry.perTurn);
			OkPopup.Show(Loc.Get("ui.fight.crewdeath.support-epilogue", "cost", Loc.Price(entry.perTurn)));
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveSupportFallenFamily, entry.peepId, player.PID));
			break;
		case CrewDeathEntry.PlayerChoice.Quest:
		{
			player.social.PerformSocialActionOn(settings.questSocial, entry.peepId, iNVALID, Extend);
			QuestDefinition questDefinition = StartSupportQuest();
			OkPopup.Show(Loc.Get("ui.fight.crewdeath.quest-epilogue", "name", Loc.Get(questDefinition.locname)));
			break;
		}
		case CrewDeathEntry.PlayerChoice.Ignore:
			player.social.PerformSocialActionOn(settings.ignoreSocial, entry.peepId, iNVALID, Extend);
			OkPopup.Show(Loc.Get("ui.fight.crewdeath.ignore-epilogue"));
			break;
		default:
			Logger.Warning("Unknown crew death type", entry.choice);
			break;
		}
		HistoryLedgerItem Extend(HistoryLedgerItem info)
		{
			info.actor = (player?.social?.PlayerPeepId).GetValueOrDefault();
			return info;
		}
		QuestDefinition StartSupportQuest()
		{
			Entity entity = entry.peepId.FindEntity();
			Label label = entity.data.ident.rng.PickElement(settings.questName);
			QuestDefinition questDefinition2 = Game.ctx.quests.FindQuestDefinition(label.String);
			if (questDefinition2 == null)
			{
				return null;
			}
			EntityID introducer = entity.data.agent.introducer;
			Game.ctx.quests.StartQuest(label.String, introducer.id, fromRequest: false);
			return questDefinition2;
		}
	}
}
