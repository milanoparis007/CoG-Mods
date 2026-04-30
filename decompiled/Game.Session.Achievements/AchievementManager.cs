using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Achievements;

public sealed class AchievementManager : AbstractSessionManager, ISaveLoadProvider
{
	private AchievementPersistedData _data = new AchievementPersistedData();

	private List<AchievementDef> _achievements = new List<AchievementDef>();

	private Dictionary<SessionEventType, List<AchievementDef>> _achieveMap = new Dictionary<SessionEventType, List<AchievementDef>>();

	private List<AchievementDef> _cache = new List<AchievementDef>();

	private ResourceCategory _illegalBooze;

	public Dictionary<Resource, int> boozeInfo = new Dictionary<Resource, int>();

	public int boozeTotal;

	public int bootlegTotal;

	public int extortTotal;

	public int tiedTotal;

	private const string HOME_BREW = "home-brew";

	public bool WasArrested => _data.arrested;

	public int UsedTickets => _data.ticketsUsed;

	public int AttendedNo => _data.resAttended;

	public IEnumerable<AchievementDef> Achievements => _achievements;

	private void SetArrested(SessionEvent _)
	{
		_data.arrested = true;
	}

	private void IncrementTickets(SessionEvent _)
	{
		_data.ticketsUsed++;
	}

	private void IncrementRes(SessionEvent _)
	{
		_data.resAttended++;
	}

	public void IncrementBoozeStolen(int stolen)
	{
		_data.boozeStolenTotal += stolen;
	}

	public int GetAmtBoozeStolen()
	{
		return _data.boozeStolenTotal;
	}

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
		_achievements = InitializeAchievements();
		foreach (AchievementDef achievement in _achievements)
		{
			if (Game.serv.store.handler.GetAchievement(achievement))
			{
				_cache.Add(achievement);
			}
		}
	}

	public override void OnInitializeDone()
	{
		base.OnInitializeDone();
		_data.cheated = false;
		_data.arrested = false;
		_data.ticketsUsed = 0;
		_data.resAttended = 0;
		boozeTotal = 0;
		bootlegTotal = 0;
		extortTotal = 0;
		tiedTotal = 0;
		_illegalBooze = Resource.Find(new Label("home-brew")).GetCategory();
		foreach (AchievementDef achievement in _achievements)
		{
			foreach (SessionEventType trigger in achievement.triggers)
			{
				if (!_achieveMap.ContainsKey(trigger))
				{
					_achieveMap.Add(trigger, new List<AchievementDef>());
				}
				_achieveMap[trigger].Add(achievement);
			}
		}
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnEnded, UpdateLocalInfo);
		Game.ctx.events.AddListener(SessionEventType.BossArrested, SetArrested);
		Game.ctx.events.AddListener(SessionEventType.AchieveUseTickets, IncrementTickets);
		Game.ctx.events.AddListener(SessionEventType.AchieveResAttend, IncrementRes);
		foreach (SessionEventType key in _achieveMap.Keys)
		{
			Game.ctx.events.AddListener(key, RunTests);
		}
		Game.ctx.events.AddListener(SessionEventType.Cheated, FileCheated);
	}

	public override void OnReleased()
	{
		base.OnReleased();
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnEnded, UpdateLocalInfo);
		Game.ctx.events.RemoveListener(SessionEventType.BossArrested, SetArrested);
		Game.ctx.events.RemoveListener(SessionEventType.AchieveUseTickets, IncrementTickets);
		Game.ctx.events.RemoveListener(SessionEventType.AchieveResAttend, IncrementRes);
		foreach (SessionEventType key in _achieveMap.Keys)
		{
			Game.ctx.events.RemoveListener(key, RunTests);
		}
		Game.ctx.events.RemoveListener(SessionEventType.Cheated, FileCheated);
	}

	private void RunTests(SessionEvent thisEvent)
	{
		if (_data.cheated || Game.ctx.clock.SkipEnd)
		{
			return;
		}
		foreach (AchievementDef item in _achieveMap[thisEvent.type])
		{
			bool flag = isValid(item);
			bool flag2 = false;
			try
			{
				flag2 = item.testFn(thisEvent);
			}
			catch (Exception ex)
			{
				Game.serv.stats.LogException($"Exception on achievement: {item.internalId}");
				Game.serv.stats.LogException(ex);
			}
			if (flag && flag2)
			{
				UnlockAchievement(item);
			}
		}
	}

	private void UpdateLocalInfo(SessionEvent _)
	{
		UpdateBoozeAndBizInfo();
		UpdateBootlegInfo();
	}

	private void UpdateBoozeAndBizInfo()
	{
		HashSet<Entity> cachedEntitiesBizUnsafe = Game.ctx.entityman.GetCachedEntitiesBizUnsafe();
		PlayerID pID = Game.ctx.players.Human.PID;
		boozeInfo = new Dictionary<Resource, int>();
		boozeTotal = 0;
		extortTotal = 0;
		tiedTotal = 0;
		PlayerOutposts outposts = Game.ctx.players.Human.outposts;
		foreach (Entity item in cachedEntitiesBizUnsafe)
		{
			PlayerTradeSummary tradeSummariesOrNull = item.data.biz.GetTradeSummariesOrNull(PlayerID.HumanPlayer);
			if (outposts.IsBizPayingTribute(item))
			{
				extortTotal++;
			}
			if (item.components.biz.IsTiedTo(pID))
			{
				tiedTotal++;
			}
			if (tradeSummariesOrNull == null)
			{
				continue;
			}
			foreach (ResourceAndQty datum in tradeSummariesOrNull.list.data)
			{
				Resource resource = datum.FindResource();
				if (resource.GetCategory() == _illegalBooze || datum.id == new Label("cigarettes"))
				{
					if (!boozeInfo.ContainsKey(resource))
					{
						boozeInfo.Add(resource, 0);
					}
					int num = MathUtil.ClampMin((int)datum.qty, 0);
					boozeInfo[resource] += num;
					boozeTotal += num;
				}
			}
		}
	}

	private void UpdateBootlegInfo()
	{
		List<EntityID> allControlledBuildingsUnsafe = Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe();
		bootlegTotal = 0;
		foreach (EntityID item in allControlledBuildingsUnsafe)
		{
			foreach (IBizModule bizModule in ModulesUtil.GetBizModules(item))
			{
				if (!(bizModule is ConsumerModule consumerModule))
				{
					continue;
				}
				foreach (ResourceAndQty datum in consumerModule.data.lifetimeConsumed.data)
				{
					if (datum.FindResource().GetCategory() == _illegalBooze)
					{
						int num = MathUtil.ClampMin((int)datum.qty, 0);
						bootlegTotal += num;
					}
				}
			}
		}
	}

	private bool isValid(AchievementDef achievement)
	{
		return !_cache.Contains(achievement);
	}

	private void FileCheated(SessionEvent _)
	{
		_data.cheated = true;
	}

	private void UnlockAchievement(AchievementDef achievement)
	{
		_cache.Add(achievement);
		Game.serv.store.handler.SetAchievement(achievement);
	}

	private List<AchievementDef> InitializeAchievements()
	{
		List<AchievementDef> list = new List<AchievementDef>();
		list.Add(new AchievementDef("ach001", "ach001", "ach001", 1, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveGoonRewardTaken));
		list.Add(new AchievementDef("ach002", "ach002", "ach002", 2, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveGoonPaidOff));
		list.Add(new AchievementDef("ach003", "ach003", "ach003", 3, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveScavenged));
		list.Add(new AchievementDef("ach004", "ach004", "ach004", 4, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveGoonHired));
		list.Add(new AchievementDef("ach005", "ach005", "ach005", 5, AchievementTests.AchievementCheckThompsonKill, SessionEventType.AchieveKillType));
		list.Add(new AchievementDef("ach006", "ach006", "ach006", 6, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveEndTutorial));
		list.Add(new AchievementDef("ach007", "ach007", "ach007", 7, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveSupportFallenFamily));
		list.Add(new AchievementDef("ach008", "ach008", "ach008", 8, AchievementTests.AchievementSucceedIfHuman, SessionEventType.PlayerBuildingTakeoverImmediate));
		list.Add(new AchievementDef("ach009", "ach009", "ach009", 9, AchievementTests.AchievementTenBuildings, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach010", "ach010", "ach010", 10, AchievementTests.AchievementHomemadeHooch, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach011", "ach011", "ach011", 11, AchievementTests.AchievementCaptainGain, SessionEventType.CrewLevelUpsChanged));
		list.Add(new AchievementDef("ach012", "ach012", "ach012", 12, AchievementTests.AchievementFiveCrew, SessionEventType.CrewMemberAdded));
		list.Add(new AchievementDef("ach013", "ach013", "ach013", 13, AchievementTests.AchievementThirteenCrew, SessionEventType.CrewMemberAdded));
		list.Add(new AchievementDef("ach014", "ach014", "ach014", 14, AchievementTests.AchievementThreeFronts, SessionEventType.PlayerOutpostChanged));
		list.Add(new AchievementDef("ach015", "ach015", "ach015", 15, AchievementTests.AchievementTwentyCorners, SessionEventType.HumanPlayerTurnStarted));
		list.Add(new AchievementDef("ach016", "ach016", "ach016", 16, AchievementTests.AchievementFiftyCorners, SessionEventType.HumanPlayerTurnStarted));
		list.Add(new AchievementDef("ach017", "ach017", "ach017", 17, AchievementTests.AchievementMoneyDirty, SessionEventType.HumanPlayerTurnStarted));
		list.Add(new AchievementDef("ach018", "ach018", "ach018", 18, AchievementTests.AchievementMoneySizeable, SessionEventType.HumanPlayerTurnStarted));
		list.Add(new AchievementDef("ach019", "ach019", "ach019", 19, AchievementTests.AchievementMoneyConsiderable, SessionEventType.HumanPlayerTurnStarted));
		list.Add(new AchievementDef("ach020", "ach020", "ach020", 20, AchievementTests.AchievementMoneyCopious, SessionEventType.HumanPlayerTurnStarted));
		list.Add(new AchievementDef("ach021", "ach021", "ach021", 21, AchievementTests.AchievementDinnerParty, SessionEventType.BuildingResEventHappened));
		list.Add(new AchievementDef("ach022", "ach022", "ach022", 22, AchievementTests.AchievementPokerParty, SessionEventType.BuildingResEventHappened));
		list.Add(new AchievementDef("ach023", "ach023", "ach023", 23, AchievementTests.AchievementChicagoWin, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach024", "ach024", "ach024", 24, AchievementTests.AchievementPittsburghWin, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach025", "ach025", "ach025", 25, AchievementTests.AchievementCincinnatiWin, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach026", "ach026", "ach026", 26, AchievementTests.AchievementDetroitWin, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach027", "ach027", "ach027", 27, AchievementTests.AchievementAutoSucceed, SessionEventType.PlayerGotIntroImmediate));
		list.Add(new AchievementDef("ach028", "ach028", "ach028", 28, AchievementTests.AchievementGinJoint, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach029", "ach029", "ach029", 29, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveCopDonation));
		list.Add(new AchievementDef("ach030", "ach030", "ach030", 30, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveFedHint));
		list.Add(new AchievementDef("ach031", "ach031", "ach031", 31, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveGoonHitman));
		list.Add(new AchievementDef("ach032", "ach032", "ach032", 32, AchievementTests.AchievementSucceedIfHuman, SessionEventType.AchieveCloseOutpost));
		list.Add(new AchievementDef("ach033", "ach033", "ach033", 33, AchievementTests.AchievementGettingFancy, SessionEventType.HumanPlayerTurnEnded, SessionEventType.PlayerSkillsChangedImmediate));
		list.Add(new AchievementDef("ach034", "ach034", "ach034", 34, AchievementTests.AchievementPuttingOnRitz, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach035", "ach035", "ach035", 35, AchievementTests.AchievementBottledUp, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach036", "ach036", "ach036", 36, AchievementTests.AchievementStillDreaming, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach037", "ach037", "ach037", 37, AchievementTests.AchievementHighEndBooze, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach038", "ach038", "ach038", 38, AchievementTests.AchievementVehiclesNum, SessionEventType.CrewVehicleCreated));
		list.Add(new AchievementDef("ach039", "ach039", "ach039", 39, AchievementTests.AchievementConnectToCop, SessionEventType.AchieveRelBoost));
		list.Add(new AchievementDef("ach040", "ach040", "ach040", 40, AchievementTests.AchievementImprover, SessionEventType.QuestCompletedImmediate, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach041", "ach041", "ach041", 41, AchievementTests.AchievementLevelupDriver, SessionEventType.CrewLevelUpsChanged));
		list.Add(new AchievementDef("ach042", "ach042", "ach042", 42, AchievementTests.AchievementLevelupAction, SessionEventType.CrewLevelUpsChanged));
		list.Add(new AchievementDef("ach043", "ach043", "ach043", 43, AchievementTests.AchievementLevelupFighter, SessionEventType.CrewLevelUpsChanged));
		list.Add(new AchievementDef("ach044", "ach044", "ach044", 44, AchievementTests.AchievementEthnicityQuests, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach045", "ach045", "ach045", 45, AchievementTests.AchievementFamQuests, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach046", "ach046", "ach046", 46, AchievementTests.AchievementGestureQuest, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach047", "ach047", "ach047", 47, AchievementTests.AchievementCanadaQuest, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach048", "ach048", "ach048", 48, AchievementTests.AchievementFirewater, SessionEventType.BuildingConstructionStateChanged));
		list.Add(new AchievementDef("ach049", "ach049", "ach049", 49, AchievementTests.AchievementBourbonBarrels, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach050", "ach050", "ach050", 50, AchievementTests.AchievementBigShoulders, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach051", "ach051", "ach051", 51, AchievementTests.AchievementBoozeBaron, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach052", "ach052", "ach052", 52, AchievementTests.AchievementLiquorLord, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach053", "ach053", "ach053", 53, AchievementTests.AchievementStalwart, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach054", "ach054", "ach054", 54, AchievementTests.AchievementBootlegger, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach055", "ach055", "ach055", 55, AchievementTests.AchievementSpeakeasy, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach056", "ach056", "ach056", 56, AchievementTests.AchievementClubs, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach057", "ach057", "ach057", 57, AchievementTests.AchievementRailroads, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach058", "ach058", "ach058", 58, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveTruceRequest));
		list.Add(new AchievementDef("ach059", "ach059", "ach059", 59, AchievementTests.AchievementExtortEight, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach060", "ach060", "ach060", 60, AchievementTests.AchievementExtortSixteen, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach061", "ach061", "ach061", 61, AchievementTests.AchievementKeepWord, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach062", "ach062", "ach062", 62, AchievementTests.AchievementTiedHouse, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach064", "ach064", "ach064", 64, AchievementTests.AchievementMovingExperiences, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach065", "ach065", "ach065", 65, AchievementTests.AchievementDeterminedDeliveries, SessionEventType.CrewLevelUpsChanged));
		list.Add(new AchievementDef("ach066", "ach066", "ach066", 66, AchievementTests.AchievementAutoSucceed, SessionEventType.AchieveAutomationExecuted));
		list.Add(new AchievementDef("ach067", "ach067", "ach067", 67, AchievementTests.AchievementSteppingItUp, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach068", "ach068", "ach068", 68, AchievementTests.AchievementCopaceticCoordination, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach069", "ach069", "ach069", 69, AchievementTests.AchievementExtraordinaryLogistics, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach070", "ach070", "ach070", 70, AchievementTests.AchievementLayLow, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach071", "ach071", "ach071", 71, AchievementTests.AchievementComplete40Quests, SessionEventType.QuestCompletedImmediate));
		list.Add(new AchievementDef("ach072", "ach072", "ach072", 72, AchievementTests.AchievementKnow15Skills, SessionEventType.PlayerSkillsChangedImmediate));
		list.Add(new AchievementDef("ach073", "ach073", "ach073", 73, AchievementTests.AchievementNetWorthSugar, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach074", "ach074", "ach074", 74, AchievementTests.AchievementNetWorthCentury, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach075", "ach075", "ach075", 75, AchievementTests.AchievementCheckSoftKill, SessionEventType.AchieveKillType));
		list.Add(new AchievementDef("ach076", "ach076", "ach076", 76, AchievementTests.AchievementApplesauce, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach077", "ach077", "ach077", 77, AchievementTests.AchievementMaintenance, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach078", "ach078", "ach078", 78, AchievementTests.AchievementPeoplePerson, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach079", "ach079", "ach079", 79, AchievementTests.AchievementSocialButterfly, SessionEventType.HumanPlayerTurnEnded));
		list.Add(new AchievementDef("ach080", "ach080", "ach080", 80, AchievementTests.AchievementHighPillow, SessionEventType.EndOfGameHappened));
		list.Add(new AchievementDef("ach081", "ach081", "ach081", 81, AchievementTests.AchievementStationMaster, SessionEventType.HumanPlayerTurnEnded));
		return list;
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(_data));
	}

	public IEnumerator Load(Hashtable rawdata)
	{
		SaveLoadUtils.DeserializeSingleKey(rawdata, "data", delegate(AchievementPersistedData result)
		{
			_data = result;
		});
		yield break;
	}
}
