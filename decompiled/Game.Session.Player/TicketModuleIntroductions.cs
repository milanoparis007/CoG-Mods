using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Player;

public static class TicketModuleIntroductions
{
	private struct ModuleBasedIntroCandidate
	{
		public enum Type
		{
			KnownAndTrusting,
			KnownAndUntrusting,
			Unknown
		}

		public Entity otherNpc;

		public Entity building;

		public Entity biz;

		public ModulesUtil.ModuleAndItem playerItem;

		public ModulesUtil.ModuleAndItem othersItem;

		public Type type;

		public bool IsValid => otherNpc != null;

		public bool IsNotValid => otherNpc == null;

		public ModuleBasedIntroCandidate(Entity otherNpc, Entity building, Entity biz, ModulesUtil.ModuleAndItem playerItem, ModulesUtil.ModuleAndItem othersItem, Type type)
		{
			this.otherNpc = otherNpc;
			this.building = building;
			this.biz = biz;
			this.playerItem = playerItem;
			this.othersItem = othersItem;
			this.type = type;
		}
	}

	private const int MAX_DISTANCE_FOR_INTRO = 100;

	public static ConvoDataNPCSelection MakeModuleBasedIntro(VisitState visit)
	{
		ModuleBasedIntroCandidate intro = FindModuleIntroCandidate(visit);
		if (intro.IsNotValid)
		{
			return null;
		}
		ConvoDataNPCSelection.Entry item = MakeModuleBasedIntroEntry(intro, visit.npc);
		return new ConvoDataNPCSelection
		{
			entries = new List<ConvoDataNPCSelection.Entry> { item }
		};
	}

	private static ConvoDataNPCSelection.Entry MakeModuleBasedIntroEntry(ModuleBasedIntroCandidate intro, Entity owner)
	{
		if (intro.playerItem.module is IBizModule playerModule)
		{
			return MakeBizModuleEntry(intro, playerModule, owner);
		}
		return MakeGenericModuleEntry(intro, owner);
	}

	private static ConvoDataNPCSelection.Entry MakeGenericModuleEntry(ModuleBasedIntroCandidate result, Entity owner)
	{
		string flavor = Loc.Get("convo.moduleintro.inventory");
		string rel = IntroductionsUtils.MakeRelFlavor(owner, result.otherNpc, "convo.ticket-intro-flavor.relationship");
		string ooc = MakeModuleBasedOOCExplanation();
		return new ConvoDataNPCSelection.Entry(result.otherNpc.Id, flavor, rel, ooc);
		string MakeModuleBasedOOCExplanation()
		{
			ModuleIntro moduleIntro = Game.serv.globals.ui.moduleIntros.LastOrDefaultFast();
			bool consumed = result.othersItem.item.consumed;
			bool flag = result.type != ModuleBasedIntroCandidate.Type.Unknown;
			string key = ((flag && consumed) ? moduleIntro.locs.knownConsumes : ((flag && !consumed) ? moduleIntro.locs.knownProduces : ((!flag && consumed) ? moduleIntro.locs.unknownConsumes : ((!flag && !consumed) ? moduleIntro.locs.unknownProduces : null))));
			return IntroductionsUtils.MakeRelFlavor(owner, result.otherNpc, key);
		}
	}

	private static ConvoDataNPCSelection.Entry MakeBizModuleEntry(ModuleBasedIntroCandidate result, IBizModule playerModule, Entity owner)
	{
		string flavor = Loc.Get(playerModule.ModuleConfig.Common.display.locintroflavor);
		string rel = IntroductionsUtils.MakeRelFlavor(owner, result.otherNpc, "convo.ticket-intro-flavor.relationship");
		string ooc = MakeModuleBasedOOCExplanation();
		return new ConvoDataNPCSelection.Entry(result.otherNpc.Id, flavor, rel, ooc);
		ModuleIntro FindFirstModuleIntro()
		{
			TagList tags = result.playerItem.module.ModuleConfig.Common.tags;
			foreach (ModuleIntro moduleIntro2 in Game.serv.globals.ui.moduleIntros)
			{
				if (moduleIntro2.tags == null || moduleIntro2.tags.ContainsAtLeastOneOf(tags))
				{
					return moduleIntro2;
				}
			}
			return Game.serv.globals.ui.moduleIntros.LastOrDefaultFast();
		}
		string MakeModuleBasedOOCExplanation()
		{
			ModuleIntro moduleIntro = FindFirstModuleIntro();
			bool consumed = result.othersItem.item.consumed;
			bool flag = result.type != ModuleBasedIntroCandidate.Type.Unknown;
			string key = ((flag && consumed) ? moduleIntro.locs.knownConsumes : ((flag && !consumed) ? moduleIntro.locs.knownProduces : ((!flag && consumed) ? moduleIntro.locs.unknownConsumes : ((!flag && !consumed) ? moduleIntro.locs.unknownProduces : null))));
			return IntroductionsUtils.MakeRelFlavor(owner, result.otherNpc, key);
		}
	}

	public static bool CanProduceModuleIntroCandidate(VisitState visit)
	{
		return FindModuleIntroCandidate(visit).IsValid;
	}

	private static ModuleBasedIntroCandidate FindModuleIntroCandidate(VisitState visit)
	{
		PlayerInfo player = visit.GetPlayer();
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(visit.npc.Id);
		if (listOrNull == null)
		{
			return default(ModuleBasedIntroCandidate);
		}
		using (ListPool<ModulesUtil.ModuleAndItem>.PooledBlockList pooledBlockList = ListPool<ModulesUtil.ModuleAndItem>.Allocate())
		{
			PopulateWithPlayerItems(player, pooledBlockList);
			List<Relationship> list = new List<Relationship>(listOrNull.data);
			visit.npc.components.ident.GetIdentityRNGUnchanging().Shuffle(list);
			foreach (Relationship item in list)
			{
				Entity entity = item.to.FindEntity();
				if (entity != visit.npc)
				{
					ModuleBasedIntroCandidate result = FindIntroBasedOnItems(visit, player, pooledBlockList, entity);
					if (result.IsValid)
					{
						return result;
					}
				}
			}
		}
		return default(ModuleBasedIntroCandidate);
	}

	private static void PopulateWithPlayerItems(PlayerInfo player, List<ModulesUtil.ModuleAndItem> playerItems)
	{
		playerItems.Clear();
		foreach (EntityID item in player.territory.GetAllControlledBuildingsUnsafe())
		{
			Entity building = item.FindEntity();
			ModulesUtil.AddUnlockedMfgItems(player, building, playerItems);
			AddPlentifulItems(building, playerItems);
		}
		foreach (EntityID allVehicle in player.crew.AllVehicles)
		{
			AddPlentifulItems(allVehicle.FindEntity(), playerItems);
		}
	}

	private static void AddPlentifulItems(Entity building, List<ModulesUtil.ModuleAndItem> playerItems)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		foreach (ResourceAndQty content in inventory.data.contents)
		{
			if (content.qty >= 10)
			{
				MfgItem item = new MfgItem(content.id, consumed: false);
				if (item.illegal)
				{
					playerItems.Add(new ModulesUtil.ModuleAndItem
					{
						module = inventory,
						item = item
					});
				}
			}
		}
	}

	private static ModuleBasedIntroCandidate FindIntroBasedOnItems(VisitState visit, PlayerInfo player, List<ModulesUtil.ModuleAndItem> playersItems, Entity other)
	{
		Entity entity = BuildingUtil.FindBizForOwner(other);
		if (entity == null)
		{
			return default(ModuleBasedIntroCandidate);
		}
		Entity entity2 = BuildingUtil.FindBuildingForBiz(entity);
		if ((visit.GetBldgNode().pos - entity2.data.board.worldpos).Magnitude > 100f)
		{
			return default(ModuleBasedIntroCandidate);
		}
		if (entity2.components.building.IsControlledByAnyPlayer())
		{
			return default(ModuleBasedIntroCandidate);
		}
		Relationship relationshipFromSourceToPlayer = player.social.GetRelationshipFromSourceToPlayer(other.Id);
		bool flag = relationshipFromSourceToPlayer != null;
		bool flag2 = player.social.AreIllegalItemsLocked(other, entity2);
		ModuleBasedIntroCandidate.Type type = ((!flag) ? ModuleBasedIntroCandidate.Type.Unknown : ((flag && flag2) ? ModuleBasedIntroCandidate.Type.KnownAndUntrusting : ModuleBasedIntroCandidate.Type.KnownAndTrusting));
		if (relationshipFromSourceToPlayer != null && relationshipFromSourceToPlayer.HasBuff(BuffConstants.TICKET_INTRO))
		{
			return default(ModuleBasedIntroCandidate);
		}
		if (type == ModuleBasedIntroCandidate.Type.KnownAndTrusting)
		{
			return default(ModuleBasedIntroCandidate);
		}
		bool onlyIllegal = type == ModuleBasedIntroCandidate.Type.KnownAndUntrusting;
		var (playerItem, othersItem) = FindFirstMatchingItemAndModule(player, playersItems, entity2, onlyIllegal);
		if (othersItem.IsNotValid || playerItem.IsNotValid)
		{
			return default(ModuleBasedIntroCandidate);
		}
		return new ModuleBasedIntroCandidate(other, entity2, entity, playerItem, othersItem, type);
	}

	private static (ModulesUtil.ModuleAndItem playerItem, ModulesUtil.ModuleAndItem ownerItem) FindFirstMatchingItemAndModule(PlayerInfo player, List<ModulesUtil.ModuleAndItem> playersItems, Entity building, bool onlyIllegal)
	{
		using (ListPool<ModulesUtil.ModuleAndItem>.PooledBlockList pooledBlockList = ListPool<ModulesUtil.ModuleAndItem>.Allocate())
		{
			ModulesUtil.AddUnlockedMfgItems(player, building, pooledBlockList);
			foreach (ModulesUtil.ModuleAndItem item2 in pooledBlockList)
			{
				ModulesUtil.ModuleAndItem item = FindFirstMatchingItemAndModule(item2, playersItems, onlyIllegal);
				if (item.IsValid)
				{
					return (playerItem: item, ownerItem: item2);
				}
			}
		}
		return default((ModulesUtil.ModuleAndItem, ModulesUtil.ModuleAndItem));
	}

	private static ModulesUtil.ModuleAndItem FindFirstMatchingItemAndModule(ModulesUtil.ModuleAndItem othersItem, List<ModulesUtil.ModuleAndItem> playersItems, bool onlyIllegal)
	{
		foreach (ModulesUtil.ModuleAndItem playersItem in playersItems)
		{
			MfgItem item = playersItem.item;
			MfgItem item2 = othersItem.item;
			if (item.id == item2.id && item.consumed != item2.consumed && (!onlyIllegal || item2.illegal))
			{
				return playersItem;
			}
		}
		return default(ModulesUtil.ModuleAndItem);
	}
}
