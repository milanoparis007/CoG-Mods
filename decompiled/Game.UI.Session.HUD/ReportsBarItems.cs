using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.KB;
using Game.UI.Session.Picks;
using Game.UI.Session.Quests;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.UI.Session.HUD;

public static class ReportsBarItems
{
	internal class SkillListItem : ItemListDialog.IEntry, IComparable
	{
		public SkillDef def;

		public SkillListItem(SkillDef def)
		{
			this.def = def;
		}

		public string GetDebug()
		{
			return $"Skill {def.id}";
		}

		public string GetName()
		{
			return Loc.Get(def.locname);
		}

		public string GetIcon()
		{
			return Loc.Get(def.locicon);
		}

		public string GetDescription()
		{
			return Loc.Get("ui.reports.skills.item.desc", "name", def.GetName(), "desc", def.GetDesc());
		}

		public bool ShowGoTo()
		{
			return false;
		}

		public void OnGoTo()
		{
		}

		public int CompareTo(object obj)
		{
			if (!(obj is SkillListItem skillListItem))
			{
				return 0;
			}
			return string.Compare(def.locname, skillListItem.def.locname);
		}
	}

	internal class GangListItem : ItemListDialog.IEntry, IComparable
	{
		public PlayerInfo player;

		public bool met;

		public bool alive;

		public GangListItem(PlayerInfo player)
		{
			this.player = player;
			alive = !player.territory.IsSafehouseVanquished;
			met = Game.ctx.players.Human.meetings.IsPlayerMet(player.PID);
		}

		public string GetDebug()
		{
			return $"Player {player}";
		}

		public string GetName()
		{
			if (!met)
			{
				return Loc.Get("ui.reports.gangs.name-unknown");
			}
			return player.social.PlayerGroupName;
		}

		public string GetIcon()
		{
			string key = ((!met) ? "ui.reports.gangs.icon-unknown" : ((!alive) ? "ui.reports.gangs.icon-dead" : "ui.reports.gangs.icon"));
			return player.social.WrapInPlayerColor(Loc.Get(key), 1f);
		}

		public void OnGoTo()
		{
			PersonInfoUtil.TweenCameraToNode(player.territory.GetHeadquartersNode().id);
		}

		public bool ShowGoTo()
		{
			return met & alive;
		}

		public int CompareTo(object obj)
		{
			if (!(obj is GangListItem gangListItem))
			{
				return 0;
			}
			if (met && !gangListItem.met)
			{
				return -1;
			}
			if (!met && gangListItem.met)
			{
				return 1;
			}
			if (alive && !gangListItem.alive)
			{
				return -1;
			}
			if (!alive && gangListItem.alive)
			{
				return 1;
			}
			return GetName().CompareTo(gangListItem.GetName());
		}

		public string GetDescription()
		{
			string name = GetName();
			string key = ((!met) ? "ui.reports.gangs.info-unknown" : ((!alive) ? "ui.reports.gangs.info-dead" : "ui.reports.gangs.info"));
			string text = name + "\n\n" + Loc.Get(key);
			if (!met || !alive)
			{
				return text;
			}
			Fixnum current = Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(player.PID).Evaluate().current;
			string text2 = Loc.Get("ui.reports.gangs.rel", "pts", Loc.FormatNumberPlusMinus(current));
			(bool isAggro, bool hasTruce) aggroAndTruce = player.ai.combat.GetAggroAndTruce(PlayerID.HumanPlayer);
			bool item = aggroAndTruce.isAggro;
			string text3 = (aggroAndTruce.hasTruce ? Loc.Get("ui.reports.gangs.truce") : (item ? Loc.Get("ui.reports.gangs.aggro") : ""));
			int ownedNodeCount = player.territory.OwnedNodeCount;
			string pluralized = Loc.GetPluralized("ui.reports.gangs.corners", ownedNodeCount, "qty", Loc.FormatNumber(ownedNodeCount));
			int livingCrewCount = player.crew.LivingCrewCount;
			string pluralized2 = Loc.GetPluralized("ui.reports.gangs.crewcount", livingCrewCount, "qty", Loc.FormatNumber(livingCrewCount));
			text = text + "\n\n" + text2 + text3 + "\n\n" + pluralized + "\n" + pluralized2 + "\n";
			foreach (CrewAssignment item2 in player.crew.GetLiving())
			{
				text = text + Loc.Get("ui.reports.gangs.crewline", "name", item2.GetPeep().data.person.ShortName) + "\n";
			}
			return text;
		}
	}

	internal class SkillLearnListItem : ItemListDialog.IEntry, IComparable
	{
		public List<SkillDef> defs;

		public Entity owner;

		public SkillLearnListItem(List<SkillDef> defs, Entity owner)
		{
			this.defs = defs;
			this.owner = owner;
		}

		public string GetDebug()
		{
			return $"Skill Quest {defs[0].id}";
		}

		public string GetName()
		{
			return owner.data.person.FullName;
		}

		public string GetIcon()
		{
			return Loc.Get("ui.pipicon.social-skill");
		}

		public string GetDescription()
		{
			string text = "";
			foreach (SkillDef def in defs)
			{
				text = text + Loc.Get("ui.reports.learnskill.item.desc", "icon", def.GetIcon(), "skillname", def.GetName(), "skilldesc", Loc.Get("ui.reports.learnskill.item.formatting", "item", def.GetDesc())) + "\n\n";
			}
			return text;
		}

		public bool ShowGoTo()
		{
			return owner.Id.IsValid;
		}

		public void OnGoTo()
		{
			PersonInfoUtil.TweenCameraToEntity(owner);
		}

		public int CompareTo(object obj)
		{
			if (!(obj is SkillLearnListItem skillLearnListItem))
			{
				return 0;
			}
			return string.Compare(defs[0].locname, skillLearnListItem.defs[0].locname);
		}
	}

	internal class GamblerListItem : ItemListDialog.IEntry, IComparable
	{
		public GamblerState gambler;

		public GamblerListItem(GamblerState gambler)
		{
			this.gambler = gambler;
		}

		public string GetDebug()
		{
			return $"Gambler: {gambler.gamblerId}";
		}

		public string GetName()
		{
			return gambler.gamblerId.FindEntity().data.person.FullName;
		}

		public string GetIcon()
		{
			if (!gambler.DebtIsDue)
			{
				return Loc.Get(gambler.FindGamblingHouse().components.modules.gambling.config.common.display.locicon);
			}
			return Loc.Get("ui.tickers.icon.debtor-new");
		}

		public string GetDescription()
		{
			PlayerGambling gambling = Game.ctx.players.Human.gambling;
			Fixnum amt = (gambler.DebtIsDue ? gambling.FindCurrentDebtLevel(gambler) : gambling.FindNextDebtLevel(gambler)).EvaluateCashForGambler(PlayerID.HumanPlayer, gambler.FindGambler());
			string text = PersonInfoUtil.GenerateTraitsList(gambler.FindGambler(), showDesc: true, showDescLong: false);
			bool flag = gambler.cash.cash < 0;
			string text2 = Loc.Get(flag ? "ui.ownedcasino.regular.mo.debt" : "ui.ownedcasino.regular.mo.full", "name", "", "money", TextUtil.ColorRedIf(flag, Loc.Money(gambler.cash)), "traits", text, "maxcredit", TextUtil.ColorRedIf(predicate: true, Loc.Money(amt)), "lastturn", TextUtil.ColorGreenRed(gambler.cashDeltaLastTurn.cash, Loc.Money(gambler.cashDeltaLastTurn.AsMoney)));
			return Loc.Get("ui.reports.gamblers.item.desc", "houseName", BuildingUtil.GetGamblingHouseName(gambler.FindGamblingHouse()), "desc", text2);
		}

		public bool ShowGoTo()
		{
			return gambler.gamblerId.IsValid;
		}

		public void OnGoTo()
		{
			PersonInfoUtil.TweenCameraToEntity(gambler.FindGambler().data.person.resassigned.FindEntity() ?? gambler.FindGamblingHouse());
		}

		public int CompareTo(object obj)
		{
			if (!(obj is GamblerListItem gamblerListItem))
			{
				return 0;
			}
			return string.Compare(gambler.gamblerId.FindEntity().data.person.FullName, gamblerListItem.gambler.gamblerId.FindEntity().data.person.FullName);
		}
	}

	internal class TerritoryListItem : ItemListDialog.IEntry, IComparable
	{
		private const string ICON_SAFEHOUSE = "ui.reports.territory.icon-safehouse";

		private const string ICON_OUTPOST_COLLECT = "ui.reports.territory.icon-front-ready";

		private const string ICON_OUTPOST_SUPPORT = "ui.reports.territory.icon-front-needs";

		private const string ICON_OUTPOST_DEFAULT = "ui.reports.territory.icon-front-default";

		private const string ICON_EXPAND_PRESENT = "ui.reports.territory.icon-expand-present";

		private const string ICON_EXPAND_AVAIL = "ui.reports.territory.icon-expand-available";

		private const string ICON_EXPAND_PUMPING = "ui.reports.territory.icon-expand-pumping";

		public EntityID buildingId;

		public OutpostEntry outpostEntry;

		public bool IsSafehouse => outpostEntry == null;

		public bool IsOutpost => outpostEntry != null;

		public TerritoryListItem(EntityID safehouse)
		{
			buildingId = safehouse;
			outpostEntry = null;
		}

		public TerritoryListItem(OutpostEntry outpost)
		{
			buildingId = outpost.outpostId.buildingId;
			outpostEntry = outpost;
		}

		public string GetDebug()
		{
			return $"Territory {buildingId}, outpost = {outpostEntry}";
		}

		public string GetName()
		{
			if (IsOutpost)
			{
				string text = "";
				text = (outpostEntry.pump.IsPumping ? Loc.Get("ui.reports.territory.icon-expand-pumping") : ((outpostEntry.targetNodes.Where((NodeEntry e) => IsAvailable(e)).Count() != 0) ? Loc.Get("ui.reports.territory.icon-expand-available") : Loc.Get("ui.reports.territory.icon-expand-present")));
				return text + " " + BuildingUtil.FindBizForBuilding(buildingId).data.biz.bizname;
			}
			return BuildingUtil.FindBuildingName(buildingId);
		}

		public string GetIcon()
		{
			if (IsSafehouse)
			{
				return Loc.Get("ui.reports.territory.icon-safehouse");
			}
			return Loc.Get(outpostEntry.money.NeedsCollect ? "ui.reports.territory.icon-front-ready" : (outpostEntry.money.NeedsSupport ? "ui.reports.territory.icon-front-needs" : "ui.reports.territory.icon-front-default"));
		}

		public string GetDescription()
		{
			if (IsSafehouse)
			{
				return Loc.Get("ui.reports.territory.safehouse", "name", GetName());
			}
			Price price = Game.ctx.players.Human.outposts.FindMonthlyOutpostCost(outpostEntry.outpostId);
			string text = Loc.Get("ui.reports.territory.status-cost", "cost", price);
			string text2 = FindStatus(outpostEntry.money.NeedsCollect, outpostEntry.money.NeedsSupport);
			return Loc.Get("ui.reports.territory.front", "name", GetName(), "cost", text, "status", text2) + GetCorners();
			static string FindStatus(bool needsCollect, bool needsSupport)
			{
				if (!needsSupport)
				{
					if (!needsCollect)
					{
						return "";
					}
					return Loc.Get("ui.reports.territory.status-front-ready");
				}
				return Loc.Get("ui.reports.territory.status-front-needs");
			}
			string GetCorners()
			{
				List<NodeEntry> list = (from e in outpostEntry.targetNodes.Skip(1)
					where IsOwned(e)
					select e).ToList();
				List<NodeEntry> list2 = (from e in outpostEntry.targetNodes.Skip(1)
					where IsPumping(e)
					select e).ToList();
				List<NodeEntry> list3 = (from e in outpostEntry.targetNodes.Skip(1)
					where IsAvailable(e)
					select e).ToList();
				return GetCornerList("ui.reports.territory.icon-expand-present", "ui.reports.territory.expanded", list) + GetCornerList("ui.reports.territory.icon-expand-pumping", "ui.reports.territory.expanding", list2) + GetCornerList("ui.reports.territory.icon-expand-available", "ui.reports.territory.canexpand", list3);
			}
		}

		private static string GetCornerList(string iconkey, string lockey, List<NodeEntry> list)
		{
			string text = Loc.Get(iconkey);
			string text2 = "\n\n" + Loc.GetPluralized(lockey, list.Count, "icon", text, "num", list.Count);
			foreach (NodeEntry item in list)
			{
				string cornerNameShort = item.nodeId.FindNode().GetCornerNameShort(addPrefix: false);
				text2 = text2 + "\n" + Loc.Get("ui.reports.territory.corner", "icon", text, "name", cornerNameShort);
			}
			return text2;
		}

		private static bool IsOwned(NodeEntry e)
		{
			return e.nodeId.FindNode()?.owner.Is(PlayerID.HumanPlayer) ?? false;
		}

		private static bool IsPumping(NodeEntry e)
		{
			if (e.IsPumped)
			{
				return !IsOwned(e);
			}
			return false;
		}

		private static bool IsAvailable(NodeEntry e)
		{
			if (!e.IsPumped)
			{
				return !IsOwned(e);
			}
			return false;
		}

		public bool ShowGoTo()
		{
			return true;
		}

		public void OnGoTo()
		{
			PersonInfoUtil.TweenCameraToEntity(buildingId);
		}

		public int CompareTo(object obj)
		{
			if (!(obj is TerritoryListItem territoryListItem))
			{
				return 0;
			}
			if (IsSafehouse && territoryListItem.IsOutpost)
			{
				return -1;
			}
			if (IsOutpost && territoryListItem.IsSafehouse)
			{
				return 1;
			}
			return string.Compare(GetName(), territoryListItem.GetName());
		}
	}

	public const string ID_MAIN_SHELF = "main";

	public static readonly List<HUDDialogItemDefBase> EMPTY = new List<HUDDialogItemDefBase>();

	public static readonly Dictionary<string, List<HUDDialogItemDefBase>> BUTTONS = new Dictionary<string, List<HUDDialogItemDefBase>> { 
	{
		"main",
		new List<HUDDialogItemDefBase>
		{
			ReportsButtonDef.MakeShelfButton("main", "RL Victory", Loc.Get("ui.reports.shelf.button.victory"), Loc.Get("ui.reports.shelf.button.victory.mo"), delegate
			{
				Game.ctx.simman.victory.Show();
			}, delegate
			{
				Game.ctx.simman.victory.Hide();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Finance", Loc.Get("ui.reports.shelf.button.finances"), Loc.Get("ui.reports.shelf.button.finances.mo"), delegate
			{
				Game.ctx.hud.ledger.Controller.Show();
			}, delegate
			{
				Game.ctx.hud.ledger.Controller.Hide();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Crew", Loc.Get("ui.reports.shelf.button.crewinfolist"), Loc.Get("ui.reports.shelf.button.crewinfolist.mo"), delegate
			{
				Game.ctx.hud.crewinfolist.Show();
			}, delegate
			{
				Game.ctx.hud.crewinfolist.Hide();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Org", Loc.Get("ui.reports.shelf.button.orgchart"), Loc.Get("ui.reports.shelf.button.orgchart.mo"), delegate
			{
				ShowOrgChartPopup();
			}, delegate
			{
				HideOrgChartPopup();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Territory", Loc.Get("ui.reports.shelf.button.territory"), Loc.Get("ui.reports.shelf.button.territory.mo"), delegate
			{
				ShowTerritoryList();
			}, delegate
			{
				HideTerritoryList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Gangs", Loc.Get("ui.reports.shelf.button.gangs"), Loc.Get("ui.reports.shelf.button.gangs.mo"), delegate
			{
				ShowGangsList();
			}, delegate
			{
				HideGangsList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Skills", Loc.Get("ui.reports.shelf.button.skills"), Loc.Get("ui.reports.shelf.button.skills.mo"), delegate
			{
				ShowSkillList();
			}, delegate
			{
				HideSkillList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Learn", Loc.Get("ui.reports.shelf.button.learnskills"), Loc.Get("ui.reports.shelf.button.learnskills.mo"), delegate
			{
				ShowLearnSkillList();
			}, delegate
			{
				HideLearnSkillList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Quests", Loc.Get("ui.reports.shelf.button.quests"), Loc.Get("ui.reports.shelf.button.quests.mo"), delegate
			{
				ShowQuestList();
			}, delegate
			{
				HideQuestList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Quests Available", Loc.Get("ui.reports.shelf.button.requests"), Loc.Get("ui.reports.shelf.button.requests.mo"), delegate
			{
				ShowRequestList();
			}, delegate
			{
				HideRequestList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Events", Loc.Get("ui.reports.shelf.button.events"), Loc.Get("ui.reports.shelf.button.events.mo"), delegate
			{
				Game.ctx.simman.businesses.ShowEventHistoryList();
			}, delegate
			{
				Game.ctx.simman.businesses.HideEventHistoryList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Gambling", Loc.Get("ui.reports.shelf.button.gamblers"), Loc.Get("ui.reports.shelf.button.gamblers.mo"), delegate
			{
				ShowGamblersList();
			}, delegate
			{
				HideGamblersList();
			}, HUDDialogItemDefBase.defaultVisFunc),
			ReportsButtonDef.MakeShelfButton("main", "RL Laws", Loc.Get("ui.reports.shelf.button.laws"), Loc.Get("ui.reports.shelf.button.laws.mo"), delegate
			{
				ShowLawPopup();
			}, delegate
			{
				HideLawPopup();
			}, HUDDialogItemDefBase.hasShadowGovVisFunc)
		}
	} };

	public static List<HUDDialogItemDefBase> GetDefs(string id)
	{
		return BUTTONS.FindOrNull(id);
	}

	public static void ShowQuestList(QuestUUID uuid = default(QuestUUID))
	{
		List<ItemListDialog.IEntry> list = (from uuid2 in Game.ctx.hud.quests.GetQuestsSorted()
			select new QuestListItem(uuid2)).Cast<ItemListDialog.IEntry>().ToList();
		ItemListDialog.IEntry selected = (uuid.IsNotSet ? null : list.FirstOrDefault((ItemListDialog.IEntry e) => e is QuestListItem questListItem && questListItem.uuid == uuid));
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.quests.header"), Loc.Get("ui.reports.quests.desc"), list, selected);
	}

	public static void HideQuestList()
	{
		Game.ctx.hud.itemList.Hide();
	}

	public static void ShowSkillList()
	{
		List<ItemListDialog.IEntry> list = (from skillDef in Game.ctx.players.Human.skills.GetCurrentSkills()
			select new SkillListItem(skillDef)).Cast<ItemListDialog.IEntry>().ToList();
		list.Sort();
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.skills.header"), Loc.Get("ui.reports.skills.desc"), list);
	}

	public static void HideSkillList()
	{
		Game.ctx.hud.itemList.Hide();
	}

	public static void ShowGangsList()
	{
		IEnumerable<GangListItem> source = from player in Game.ctx.players.all
			where player.IsJustGang
			select new GangListItem(player);
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.gangs.header"), Loc.Get("ui.reports.gangs.desc"), source.OfType<ItemListDialog.IEntry>().ToList());
	}

	public static void HideGangsList()
	{
		Game.ctx.hud.itemList.Hide();
	}

	public static void ShowLearnSkillList()
	{
		List<ItemListDialog.IEntry> list = (from pick in Game.ctx.hud.picks.GetContainer(PickType.BuildingPick).picks
			where ((BuildingPick)pick.Value).HasInfoPips
			select pick.Key into building
			where CanLearnSkill(building)
			select MakeListItem(building)).Cast<ItemListDialog.IEntry>().ToList();
		bool flag = list.Count == 0;
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.learnskill.header"), flag ? Loc.Get("ui.reports.learnskill.desc.empty") : Loc.Get("ui.reports.learnskill.desc"), list);
		static bool CanLearnSkill(PickTarget building)
		{
			return Game.ctx.players.Human.kb.GetStatusForBuilding(building.eid, PlayerKBQueryNames.OWNER_CAN_LEARN_SKILL).passed;
		}
		static SkillLearnListItem MakeListItem(PickTarget building)
		{
			return new SkillLearnListItem(FindSkillsGivenBuilding(building.eid), BuildingUtil.FindOwnerForAnyBuilding(building.eid));
		}
	}

	internal static List<SkillDef> FindSkillsGivenBuilding(EntityID building)
	{
		VisitState visit = new VisitState(Game.ctx.players.Human.crew.AllCrew.ToList()[0], BuildingUtil.FindDataForBuilding(building), Game.ctx.clock.Now, PlayerID.HumanPlayer);
		return Game.ctx.players.Human.skills.FindAllDistinctSkillsToLearn(visit);
	}

	public static void HideLearnSkillList()
	{
		Game.ctx.hud.itemList.Hide();
	}

	public static void ShowRequestList()
	{
		List<ItemListDialog.IEntry> entries = (from pick in Game.ctx.hud.picks.GetContainer(PickType.BuildingPick).picks
			where ((BuildingPick)pick.Value).HasInfoPips
			select pick.Key into request
			where ShouldStartQReq(request)
			select MakeListItem(request)).Cast<ItemListDialog.IEntry>().ToList();
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.requests.header"), Loc.Get("ui.reports.requests.desc"), entries);
		static RequestListItem MakeListItem(PickTarget request)
		{
			return new RequestListItem(BuildingUtil.FindOwnerForAnyBuilding(request.eid).Id);
		}
		static bool ShouldStartQReq(PickTarget building)
		{
			return Game.ctx.players.Human.kb.GetStatusForBuilding(building.eid, PlayerKBQueryNames.QREQ_SHOULD_START).passed;
		}
	}

	public static void HideRequestList()
	{
		Game.ctx.hud.itemList.Hide();
	}

	public static void ShowGamblersList()
	{
		List<ItemListDialog.IEntry> entries = (from gambler in Game.ctx.players.Human.gambling.GetAllGamblerStates()
			select MakeListItem(gambler)).Cast<ItemListDialog.IEntry>().ToList();
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.gamblers.header"), Loc.Get("ui.reports.gamblers.desc"), entries);
		static GamblerListItem MakeListItem(GamblerState gambler)
		{
			return new GamblerListItem(gambler);
		}
	}

	public static void HideGamblersList()
	{
		Game.ctx.hud.itemList.Hide();
	}

	public static void ShowOrgChartPopup()
	{
		if (!(Game.serv.ui.TopPopupUnsafe is OrgChartPopup))
		{
			if (Game.serv.ui.ContainsPopup<OrgChartPopup>())
			{
				Game.serv.ui.RemovePopup<OrgChartPopup>();
			}
			Game.serv.ui.AddPopup(new OrgChartPopup());
		}
	}

	public static void HideOrgChartPopup()
	{
	}

	public static void ShowLawPopup()
	{
		if (!(Game.serv.ui.TopPopupUnsafe is OrgChartPopup))
		{
			if (Game.serv.ui.ContainsPopup<LawPopup>())
			{
				Game.serv.ui.RemovePopup<LawPopup>();
			}
			Game.serv.ui.AddPopup(new LawPopup());
		}
	}

	public static void HideLawPopup()
	{
	}

	public static void ShowTerritoryList()
	{
		IEnumerable<TerritoryListItem> source = new List<TerritoryListItem>
		{
			new TerritoryListItem(Game.ctx.players.Human.territory.Safehouse)
		}.Concat(from e in Game.ctx.players.Human.outposts.GetOutpostEntriesUnsafe()
			select new TerritoryListItem(e));
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.reports.territory.header"), Loc.Get("ui.reports.territory.desc"), source.OfType<ItemListDialog.IEntry>().ToList());
	}

	public static void HideTerritoryList()
	{
		Game.ctx.hud.itemList.Hide();
	}
}
