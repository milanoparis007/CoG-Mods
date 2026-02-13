using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("CopKilling")]

namespace GameplayTweaks
{

internal static class G
{
	private static readonly Type GameType;

	private static readonly FieldInfo CtxField;

	private static FieldInfo _simmanField;

	private static FieldInfo _relsField;

	private static FieldInfo _peoplegenField;

	private static FieldInfo _playersField;

	private static FieldInfo _clockField;

	private static PropertyInfo _humanProp;

	private static PropertyInfo _nowProp;

	private static MethodInfo _getPoliticianMethod;

	public static dynamic ctx => CtxField?.GetValue(null);

	static G()
	{
		GameType = typeof(GameClock).Assembly.GetType("Game.Game");
		if (GameType != null)
		{
			CtxField = GameType.GetField("ctx", BindingFlags.Static | BindingFlags.Public);
		}
	}

	public static object GetCtx()
	{
		return CtxField?.GetValue(null);
	}

	private static void EnsureReflectionCached(object ctxObj)
	{
		if (!(_simmanField != null))
		{
			Type type = ctxObj.GetType();
			_simmanField = type.GetField("simman");
			_playersField = type.GetField("players");
			_clockField = type.GetField("clock");
			if (_simmanField != null)
			{
				Type fieldType = _simmanField.FieldType;
				_relsField = fieldType.GetField("rels");
				_peoplegenField = fieldType.GetField("peoplegen");
			}
		}
	}

	public static RelationshipTracker GetRels()
	{
		object obj = GetCtx();
		if (obj == null)
		{
			return null;
		}
		EnsureReflectionCached(obj);
		object obj2 = _simmanField?.GetValue(obj);
		object obj3 = _relsField?.GetValue(obj2);
		return obj3 as RelationshipTracker;
	}

	public static PeopleTracker GetPeopleGen()
	{
		object obj = GetCtx();
		if (obj == null)
		{
			return null;
		}
		EnsureReflectionCached(obj);
		object obj2 = _simmanField?.GetValue(obj);
		object obj3 = _peoplegenField?.GetValue(obj2);
		return obj3 as PeopleTracker;
	}

	public static PlayerCrew GetHumanCrew()
	{
		object obj = GetCtx();
		if (obj == null)
		{
			return null;
		}
		EnsureReflectionCached(obj);
		object obj2 = _playersField?.GetValue(obj);
		if (obj2 == null)
		{
			return null;
		}
		if (_humanProp == null)
		{
			_humanProp = obj2.GetType().GetProperty("Human");
		}
		object obj3 = _humanProp?.GetValue(obj2);
		return (obj3 as PlayerInfo).crew;
	}

	public static SimTime GetNow()
	{

		object obj = GetCtx();
		if (obj == null)
		{
			return default(SimTime);
		}
		EnsureReflectionCached(obj);
		object obj2 = _clockField?.GetValue(obj);
		if (obj2 == null)
		{
			return default(SimTime);
		}
		if (_nowProp == null)
		{
			_nowProp = obj2.GetType().GetProperty("Now");
		}
		return (SimTime)_nowProp.GetValue(obj2);
	}

	public static object GetPoliticianData(EntityID id)
	{
		object obj = GetCtx();
		if (obj == null)
		{
			return null;
		}
		EnsureReflectionCached(obj);
		object obj2 = _simmanField?.GetValue(obj);
		if (obj2 == null)
		{
			return null;
		}
		object obj3 = obj2.GetType().GetField("politics")?.GetValue(obj2) ?? obj2.GetType().GetProperty("politics")?.GetValue(obj2);
		if (obj3 == null)
		{
			return null;
		}
		if (_getPoliticianMethod == null)
		{
			_getPoliticianMethod = obj3.GetType().GetMethod("GetPoliticianData");
		}
		return _getPoliticianMethod?.Invoke(obj3, new object[1] { id });
	}

	public static Entity GetManager(Entity building)
	{
		try
		{
			return BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
		}
		catch
		{
			return null;
		}
	}

	public static object GetPlayers()
	{
		object obj = GetCtx();
		if (obj == null)
		{
			return null;
		}
		EnsureReflectionCached(obj);
		return _playersField?.GetValue(obj);
	}

	public static IEnumerable<PlayerInfo> GetAllPlayers()
	{
		object players = GetPlayers();
		if (players == null)
		{
			yield break;
		}
		IEnumerable<PlayerInfo> enumerable = null;
		PropertyInfo property = players.GetType().GetProperty("all", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (property != null)
		{
			enumerable = property.GetValue(players) as IEnumerable<PlayerInfo>;
		}
		if (enumerable == null)
		{
			FieldInfo field = players.GetType().GetField("all", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				enumerable = field.GetValue(players) as IEnumerable<PlayerInfo>;
			}
		}
		if (enumerable == null)
		{
			PropertyInfo property2 = players.GetType().GetProperty("All", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property2 != null)
			{
				enumerable = property2.GetValue(players) as IEnumerable<PlayerInfo>;
			}
		}
		if (enumerable == null)
		{
			yield break;
		}
		foreach (PlayerInfo item in enumerable)
		{
			yield return item;
		}
	}

	public static PlayerInfo GetHumanPlayer()
	{
		object players = GetPlayers();
		if (players == null)
		{
			return null;
		}
		if (_humanProp == null)
		{
			_humanProp = players.GetType().GetProperty("Human");
		}
		object obj = _humanProp?.GetValue(players);
		return obj as PlayerInfo;
	}
}
public enum WantedLevel
{
	None,
	Low,
	Medium,
	High
}
[Serializable]
public class CrewModState
{
	public float StreetCreditProgress;

	public int StreetCreditLevel;

	public WantedLevel WantedLevel;

	public float WantedProgress;

	public bool MayorBribeActive;

	public bool JudgeBribeActive;

	public long BribeExpiresRaw;

	public float HappinessValue = 1f;

	public int TurnsUnhappy;

	public bool OnVacation;

	public long VacationReturnsRaw;

	public bool IsUnderboss;

	public bool AwaitingChildBirth;

	public int LastFutureKidsCount;

	public int LastBoozeSoldCount;

	public bool IsDefector;

	public long DefectorIdRaw;

	public bool VacationPending;

	public int VacationDuration = 1;

	public int FedArrivalCountdown;

	public bool FedsIncoming;

	public bool InJail;

	public int DaysInJail;

	public int TrialDaysRemaining;

	public int LawyerRetainer;

	public bool CaseDismissed;

	public bool HasWitness;

	public bool WitnessThreatenedSuccessfully;

	public bool WitnessThreatAttempted;

	public int ExtraJailYears;

	public int LastBoozeSellTurn;

	public SimTime BribeExpires
	{
		get
		{
			return new SimTime((int)BribeExpiresRaw);
		}
		set
		{
			BribeExpiresRaw = value.days;
		}
	}

	public SimTime VacationReturns
	{
		get
		{
			return new SimTime((int)VacationReturnsRaw);
		}
		set
		{
			VacationReturnsRaw = value.days;
		}
	}
}
[Serializable]
public class AlliancePact
{
	public string PactId;

	public string PactName;

	public int ColorIndex;

	public int LeaderGangId;

	public List<int> MemberIds = new List<int>();

	public float ColorR;

	public float ColorG;

	public float ColorB;

	public int FormedDays;

	public bool PlayerInvited;

	public bool IsPending;

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(PactName))
			{
				return PactName;
			}
			return $"Pact {ColorIndex + 1}";
		}
	}

	public Color SharedColor
	{
		get
		{
			return new Color(ColorR, ColorG, ColorB);
		}
		set
		{

			ColorR = value.r;
			ColorG = value.g;
			ColorB = value.b;
		}
	}

	public SimTime Formed
	{
		get
		{
			return new SimTime(FormedDays);
		}
		set
		{
			FormedDays = value.days;
		}
	}

	public bool IsActive
	{
		get
		{
			if (!IsPending)
			{
				return MemberIds.Count > 0;
			}
			return false;
		}
	}

	public bool IsMember(PlayerID pid)
	{

		if (!MemberIds.Contains(pid.id))
		{
			return LeaderGangId == pid.id;
		}
		return true;
	}
}
[Serializable]
public class ModSaveData
{
	public Dictionary<long, CrewModState> CrewStates = new Dictionary<long, CrewModState>();

	public List<AlliancePact> Pacts = new List<AlliancePact>();

	public int NextPactId;

	public int PlayerPactId = -1;

	public int PlayerJoinedPactIndex = -1;

	public int LastPactJoinDay = -1;

	public Dictionary<int, int> PactJoinCooldowns = new Dictionary<int, int>();

	public bool NeverAcceptPacts;

	public List<string> GrapevineEvents = new List<string>();

	public bool CopWarActive;

	public int CopWarWitnessCount;

	public int LastCopKillDay = -1;
}
internal static class ModConstants
{
	public const float STREET_CREDIT_PER_FIGHT = 0.05f;

	public const float STREET_CREDIT_PER_KILL = 0.3f;

	public const float STREET_CREDIT_PER_25_BOOZE = 0.1f;

	public const int STREET_CREDIT_REWARD_COUNT = 7;

	public const float HAPPINESS_LOSS_PER_LEVEL = 0.1f;

	public const int VACATION_DAYS = 7;

	public const int GIFT_BASE_COST = 50;

	public const int TURNS_FOR_DEFECTION = 5;

	public const float UNHAPPY_THRESHOLD = 0.2f;

	public const int FAMILY_REHIRE_COST = 3500;

	public const int TOP_GANG_COUNT = 6;

	public const int TOP_GANG_CREW_CAP = 40;

	public const float INCOME_SHARE_PERCENT = 0.05f;

	public const int BASE_BRIBE_COST = 520;

	public const int BRIBE_DURATION_DAYS = 7;

	public const int MAYOR_BRIBE_COST = 10000;

	public const int MAYOR_BRIBE_DURATION_DAYS = 180;

	public const int KILLS_FOR_MAX_WANTED = 5;

	public const int MAX_PACTS = 7;

	public const int PLAYER_PACT_SLOT = 6;

	public const int FED_ARRIVAL_HIGH = 2;

	public const int FED_ARRIVAL_MEDIUM = 5;

	public const int FED_ARRIVAL_LOW = 10;

	public const int PLAYER_PACT_TERRITORY_COST = 24;

	public const int PLAYER_PACT_SC_COST = 2;

	public const int PACT_JOIN_COOLDOWN_DAYS = 180;

	public static readonly Color[] PACT_COLORS = (Color[])(object)new Color[7]
	{
		new Color(0.9f, 0.2f, 0.2f),
		new Color(0.2f, 0.5f, 0.9f),
		new Color(0.9f, 0.7f, 0.1f),
		new Color(0.9f, 0.4f, 0.1f),
		new Color(0.6f, 0.3f, 0.8f),
		new Color(0.2f, 0.7f, 0.7f),
		new Color(0.4f, 0.8f, 0.4f)
	};

	public static readonly string[] PACT_COLOR_NAMES = new string[7] { "Red", "Blue", "Gold", "Orange", "Purple", "Teal", "Your Pact" };

	public const float COP_KILL_WITNESS_CHANCE = 0.5f;

	public const float NORMAL_WITNESS_CHANCE = 0.3f;

	public static readonly string DIRTY_CASH_LABEL = "dirty-cash";

	public static readonly float[] MOOD_THRESHOLDS = new float[4] { 0.75f, 0.5f, 0.25f, 0f };

	public static readonly string[] MOOD_LABELS = new string[4] { "Happy", "Content", "Unhappy", "Miserable" };

	public static readonly Color[] MOOD_COLORS = (Color[])(object)new Color[4]
	{
		new Color(0.3f, 0.85f, 0.3f),
		new Color(0.7f, 0.8f, 0.3f),
		new Color(0.9f, 0.6f, 0.2f),
		new Color(0.85f, 0.2f, 0.2f)
	};
}
[BepInPlugin("com.mods.gameplaytweaks", "Gameplay Tweaks", "1.0.0")]
public class GameplayTweaksPlugin : BaseUnityPlugin
{
	[HarmonyPatch(typeof(PeopleTracker), "FindMatchAndLinkCouple")]
	private static class SpouseEthnicityLinkPatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			if (!EnableSpouseEthnicity.Value)
			{
				ForceSameEthnicity = false;
			}
			else
			{
				ForceSameEthnicity = SharedRng.NextDouble() < (double)SpouseEthnicityChance.Value;
			}
		}
	}

	[HarmonyPatch(typeof(PeopleTracker), "ScoreCandidate")]
	private static class SpouseEthnicityCandidatePatch
	{
		[HarmonyPrefix]
		private static bool Prefix(Entity current, Entity other, ref float __result)
		{

			if (!EnableSpouseEthnicity.Value)
			{
				return true;
			}
			PersonData person = current.data.person;
			PersonData person2 = other.data.person;
			SimTimeSpan val = person.born.Subtract(person2.born);
			float num = Math.Abs(val.YearsFloat);
			float num2 = Mathf.Clamp(10f - num, 0f, 10f);
			if (ForceSameEthnicity)
			{
				__result = ((person.eth == person2.eth) ? (3f * num2) : 0f);
			}
			else
			{
				__result = ((person.eth == person2.eth) ? 3f : 1f) * num2;
			}
			return false;
		}
	}

	private static class FindRandoToMarryPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(PeopleTracker), "FindRandoToMarry", (Type[])null, (Type[])null);
				if (!(methodInfo == null))
				{
					harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(FindRandoToMarryPatch), "Prefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] FindRandoToMarryPatch failed: {arg}");
			}
		}

		private static bool Prefix(PeopleTracker __instance, Entity peep, ref Entity __result)
		{

			try
			{
				RelationshipTracker rels = G.GetRels();
				PersonData person = peep.data.person;
				Gender val = (Gender)(((int)person.g == 2) ? 1 : 2);
				SimTime now = G.GetNow();
				int value = MarriageMinAge.Value;
				int value2 = MarriageMaxAgeDiff.Value;
				List<(Entity, float)> list = new List<(Entity, float)>();
				foreach (Entity allTrackedPerson in __instance.GetAllTrackedPeople())
				{
					PersonData person2 = allTrackedPerson.data.person;
					if (person2.g != val || !person2.IsAlive)
					{
						continue;
					}
					SimTimeSpan age = person2.GetAge(now);
					if (age.YearsFloat < (float)value)
					{
						continue;
					}
					float num = Math.Abs(person2.born.YearsFloat - person.born.YearsFloat);
					if (num > (float)value2 || person2.business.IsValid)
					{
						continue;
					}
					RelationshipList val2 = ((rels != null) ? rels.GetListOrNull(allTrackedPerson.Id) : null);
					if (val2 == null || !val2.HasSpouse())
					{
						RelationshipList val3 = ((rels != null) ? rels.GetListOrNull(peep.Id) : null);
						if (val3 == null || !val3.HasAny(allTrackedPerson.Id))
						{
							list.Add((allTrackedPerson, (float)value2 - num + 1f));
						}
					}
				}
				if (list.Count == 0)
				{
					__result = null;
					return false;
				}
				float num2 = list.Sum<(Entity, float)>(((Entity entity, float score) c) => c.score);
				float num3 = (float)SharedRng.NextDouble() * num2;
				float num4 = 0f;
				foreach (var item3 in list)
				{
					Entity item = item3.Item1;
					float item2 = item3.Item2;
					num4 += item2;
					if (num3 <= num4)
					{
						__result = item;
						return false;
					}
				}
				__result = list[0].Item1;
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	private static class HireableAgePatch
	{
		private delegate bool IsEligibleDelegate(SimTime now, Entity person);

		private static IsEligibleDelegate _originalTrampoline;

		private static Detour _detour;

		public static void ApplyManualDetour()
		{

			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(PlayerSocial), "IsEligibleCrewMember", (Type[])null, (Type[])null);
				MethodInfo methodInfo2 = AccessTools.Method(typeof(HireableAgePatch), "Replacement", (Type[])null, (Type[])null);
				_detour = new Detour((MethodBase)methodInfo, (MethodBase)methodInfo2);
				_originalTrampoline = _detour.GenerateTrampoline<IsEligibleDelegate>();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] HireableAgePatch failed: {arg}");
			}
		}

		private static bool Replacement(SimTime now, Entity person)
		{

			if (!EnableHireableAge.Value && _originalTrampoline != null)
			{
				return _originalTrampoline(now, person);
			}
			try
			{
				PersonData person2 = person.data.person;
				if (person2.IsAlive)
				{
					SimTimeSpan age = person2.GetAge(now);
					if (age.YearsFloat >= HireableMinAge.Value && person2.business.IsNotValid && person2.resassigned.IsNotValid && G.GetPoliticianData(person.Id) == null)
					{
						return person.data.agent.pid.id == 0;
					}
				}
				return false;
			}
			catch
			{
				return false;
			}
		}
	}

	private static class CrewRelationshipHandlerPatch
	{
		private static FieldInfo _selectedField;

		private static FieldInfo _goField;

		private static FieldInfo _entryCrewField;

		private static FieldInfo _entryBuildingField;

		private static GameObject _handlerPopup;

		private static bool _popupVisible;

		private static Entity _selectedPeep;

		private static Text _titleText;

		private static Image _streetCreditFill;

		private static Image _wantedFill;

		private static Image _happinessFill;

		private static Text _streetCreditLevelText;

		private static Text _wantedLevelText;

		private static Text _happinessMoodText;

		private static Text _dirtyCashText;

		private static Button _btnUnderboss;

		private static Button _btnMarryChild;

		private static Button _btnHireFamily;

		private static Button _btnVacation;

		private static Button _btnGift;

		private static Button _btnBribeMayor;

		private static Button _btnBribeJudge;

		private static Text _txtUnderboss;

		private static Text _txtMarryChild;

		private static Text _txtHireFamily;

		private static Text _txtVacation;

		private static Text _txtGift;

		private static Text _txtBribeMayor;

		private static Text _txtBribeJudge;

		private static Text _txtLawyerStatus;

		private static Text _txtPayLawyer;

		private static Button _btnPayLawyer;

		private static GameObject _lawyerSection;

		private static GameObject _lawyerRow;

		private static Button _btnThreatenWitness;

		private static Text _txtThreatenWitness;

		private static Button _btnSellBooze;

		private static Text _txtSellBooze;

		private static GameObject _boozeSellRow;

		private static GameObject _renameRow;

		private static GameObject _nicknameRow;

		private static InputField _inputFirstName;

		private static InputField _inputLastName;

		private static InputField _inputNickname;

		private static Button _btnRename;

		private static Button _btnAge;

		private static Button _btnNickname;

		private static Text _txtRename;

		private static Text _txtAge;

		private static Text _txtNickname;

		private static Button _btnPepTalk;

		private static Text _txtPepTalk;

		private static int _pepTalkUsesThisTurn = 0;

		private static int _pepTalkLastTurnDay = -1;

		private static Button _btnXpMinus;

		private static Button _btnXpPlus;

		private static Text _txtXpDisplay;

		private static int _xpAdjustAmount = 100;

		private static GameObject _boozeSellPopup;

		private static bool _boozeSellVisible;

		private static Transform _boozeSellContent;

		private static Text _boozeSellStatusText;

		private static readonly Dictionary<string, int> _boozePrices = new Dictionary<string, int>
		{
			{ "beer-bottled", 5 },
			{ "wine-bottled", 8 },
			{ "whiskey-bottled", 12 },
			{ "brandy-bottled", 12 },
			{ "vodka-bottled", 10 },
			{ "rum-bottled", 10 },
			{ "beer-keg", 20 },
			{ "whiskey-barrel", 40 },
			{ "brandy-barrel", 40 },
			{ "vodka-barrel", 35 },
			{ "rum-barrel", 35 },
			{ "champagne", 20 },
			{ "scotch-whiskey", 18 },
			{ "bourbon", 15 },
			{ "canadian-whiskey", 14 },
			{ "gin", 10 },
			{ "english-gin", 15 },
			{ "bathtub-gin", 4 },
			{ "moonshine", 3 },
			{ "home-brew", 3 },
			{ "fake-beer", 2 },
			{ "fake-wine", 3 },
			{ "cider", 4 },
			{ "absinthe", 18 },
			{ "ethnic-alcohol", 6 }
		};

		private static GameObject _gangPactsBtnGo;

		private static GameObject _safeboxBtnGo;

		private static GameObject _safeboxPopup;

		private static bool _safeboxVisible;

		private static Text _safeboxCleanText;

		private static Text _safeboxDirtyText;

		private static Text _safeboxTotalText;

		private static Transform _safeboxContent;

		private static GameObject _grapevineBtnGo;

		private static GameObject _grapevinePopup;

		private static bool _grapevineVisible;

		private static Transform _grapevineContent;

		private static GameObject _gangPactsPopup;

		private static bool _gangPactsVisible;

		private static Transform _gangPactsContent;

		private static ScrollRect _gangPactsScroll;

		private static Text _gangPactsStatusText;

		private static int _selectedPactSlot = -1;

		private static bool _pactEditorMode = false;

		private static Text _pactTabLabel;

		private static GameObject[] _pactSlotRows = (GameObject[])(object)new GameObject[7];

		private static Text[] _pactSlotNameTexts = (Text[])(object)new Text[7];

		private static Text[] _pactSlotMemberTexts = (Text[])(object)new Text[7];

		private static Image[] _pactSlotColorSwatches = (Image[])(object)new Image[7];

		private static Button[] _pactSlotSelectBtns = (Button[])(object)new Button[7];

		private static Button[] _pactSlotDeleteBtns = (Button[])(object)new Button[7];

		private static Text _selectedPactLabel;

		private static GameObject _gangRenameRow;

		private static InputField _gangRenameInput;

		private static PlayerInfo _gangRenameTarget;

		private static Text _gangRenameNameText;

		private static GameObject _myPactPopup;

		private static bool _myPactVisible;

		private static Transform _myPactContent;

		private static GameObject _myPactBtnGo;

		internal static GameObject _outingPopup;

		internal static Text _outingText;

		internal static int _lastOutingDay = -1;

		internal static int _outingIntervalDays = 56;

		internal static bool _globalMayorBribeActive;

		internal static int _globalMayorBribeExpireDay = -1;

		private static GameObject _familyHirePopup;

		private static bool _familyHireVisible;

		private static Transform _familyHireContent;

		private static List<Entity> _filteredRelatives = new List<Entity>();

		private static string _filterEthnicity = "All";

		private static string _filterTrait = "All";

		private static int _filterMinAge = 0;

		private static int _filterMaxAge = 100;

		private static Text _familyHireStatusText;

		private static ScrollRect _familyHireScroll;

		private static Button _btnPrevCrew;

		private static Button _btnNextCrew;

		private static int _currentCrewIndex = 0;

		private static List<CrewAssignment> _crewList = new List<CrewAssignment>();

		private static object _popupInstance;

		private static readonly string[] ETHNICITY_FILTERS = new string[8] { "All", "Irish", "Italian", "Jewish", "Chinese", "African", "German", "Polish" };

		private static readonly string[] TRAIT_FILTERS = new string[7] { "All", "Strong", "Fast", "Smart", "Tough", "Lucky", "Charming" };

		private static int _ethFilterIndex = 0;

		private static int _traitFilterIndex = 0;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.UI.Session.Crew.CrewManagementPopup");
				if (!(type == null))
				{
					_selectedField = type.GetField("_selected", BindingFlags.Instance | BindingFlags.NonPublic);
					_goField = type.BaseType.GetField("_go", BindingFlags.Instance | BindingFlags.NonPublic);
					Type nestedType = type.GetNestedType("Entry", BindingFlags.NonPublic);
					if (nestedType != null)
					{
						_entryCrewField = nestedType.GetField("crew");
						_entryBuildingField = nestedType.GetField("building");
					}
					MethodInfo method = type.GetMethod("RefreshInfoPanel", BindingFlags.Instance | BindingFlags.NonPublic);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(CrewRelationshipHandlerPatch), "RefreshInfoPanelPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Crew Relationship Handler initialized");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] CrewRelationshipHandlerPatch failed: {arg}");
			}
		}

		private static Entity GetSelectedPeep()
		{

			if (_popupInstance == null || _selectedField == null || _entryCrewField == null)
			{
				return null;
			}
			object value = _selectedField.GetValue(_popupInstance);
			if (value == null)
			{
				return null;
			}
			object value2 = _entryCrewField.GetValue(value);
			if (value2 == null)
			{
				return null;
			}
			CrewAssignment val = (CrewAssignment)value2;
			return val.GetPeep();
		}

		private static void RefreshInfoPanelPostfix(object __instance)
		{

			try
			{
				_popupInstance = __instance;
				_selectedPeep = GetSelectedPeep();
				EnsureHandlerButtonCreated();
				if (_popupVisible)
				{
					RefreshHandlerUI();
				}
				if ((UnityEngine.Object)(object)_gangPactsBtnGo != (UnityEngine.Object)null)
				{
					bool active = false;
					if (_selectedPeep != null)
					{
						PlayerCrew humanCrew = G.GetHumanCrew();
						CrewAssignment val = (CrewAssignment)((humanCrew != null) ? humanCrew.GetCrewForIndex(0) : default(CrewAssignment));
						Entity val2 = (val.IsValid ? val.GetPeep() : null);
						active = val2 != null && val2.Id == _selectedPeep.Id;
					}
					_gangPactsBtnGo.SetActive(active);
					if ((UnityEngine.Object)(object)_myPactBtnGo != (UnityEngine.Object)null)
					{
						_myPactBtnGo.SetActive(active);
					}
					if ((UnityEngine.Object)(object)_grapevineBtnGo != (UnityEngine.Object)null)
					{
						_grapevineBtnGo.SetActive(active);
					}
					if ((UnityEngine.Object)(object)_safeboxBtnGo != (UnityEngine.Object)null)
					{
						_safeboxBtnGo.SetActive(active);
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] RefreshInfoPanelPostfix: {arg}");
			}
		}

		private static void EnsureHandlerButtonCreated()
		{

			if ((UnityEngine.Object)(object)_handlerPopup != (UnityEngine.Object)null)
			{
				return;
			}
			object obj = _goField?.GetValue(_popupInstance);
			GameObject val = obj as GameObject;
			if ((UnityEngine.Object)(object)val == (UnityEngine.Object)null)
			{
				return;
			}
			Transform val2 = val.transform.Find("Panel/Info/Viewport/Content") ?? val.transform.Find("Info/Viewport/Content");
			if (!((UnityEngine.Object)(object)val2 == (UnityEngine.Object)null))
			{
				GameObject val3 = new GameObject("BtnCrewRelations", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(LayoutElement)
				});
				val3.transform.SetParent(val2, false);
				val3.transform.SetAsLastSibling();
				LayoutElement component = val3.GetComponent<LayoutElement>();
				component.minHeight = 35f;
				component.preferredHeight = 35f;
				Image component2 = val3.GetComponent<Image>();
				((Graphic)component2).color = new Color(0.4f, 0.25f, 0.15f, 0.95f);
				Button component3 = val3.GetComponent<Button>();
				((Selectable)component3).targetGraphic = (Graphic)(object)component2;
				((UnityEvent)component3.onClick).AddListener(new UnityAction(ToggleHandlerPopup));
				GameObject val4 = new GameObject("Text", new Type[2]
				{
					typeof(RectTransform),
					typeof(Text)
				});
				val4.transform.SetParent(val3.transform, false);
				RectTransform component4 = val4.GetComponent<RectTransform>();
				component4.anchorMin = Vector2.zero;
				component4.anchorMax = Vector2.one;
				component4.offsetMin = Vector2.zero;
				component4.offsetMax = Vector2.zero;
				Text component5 = val4.GetComponent<Text>();
				component5.text = "Crew Relations";
				component5.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				component5.fontSize = 14;
				((Graphic)component5).color = Color.white;
				component5.alignment = (TextAnchor)4;
				component5.fontStyle = (FontStyle)1;
				GameObject val5 = (_gangPactsBtnGo = new GameObject("BtnGangPacts", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(LayoutElement)
				}));
				val5.transform.SetParent(val2, false);
				val5.transform.SetAsLastSibling();
				LayoutElement component6 = val5.GetComponent<LayoutElement>();
				component6.minHeight = 35f;
				component6.preferredHeight = 35f;
				Image component7 = val5.GetComponent<Image>();
				((Graphic)component7).color = new Color(0.35f, 0.2f, 0.15f, 0.95f);
				Button component8 = val5.GetComponent<Button>();
				((Selectable)component8).targetGraphic = (Graphic)(object)component7;
				((UnityEvent)component8.onClick).AddListener(new UnityAction(ToggleGangPactsPopup));
				GameObject val6 = new GameObject("Text", new Type[2]
				{
					typeof(RectTransform),
					typeof(Text)
				});
				val6.transform.SetParent(val5.transform, false);
				RectTransform component9 = val6.GetComponent<RectTransform>();
				component9.anchorMin = Vector2.zero;
				component9.anchorMax = Vector2.one;
				component9.offsetMin = Vector2.zero;
				component9.offsetMax = Vector2.zero;
				Text component10 = val6.GetComponent<Text>();
				component10.text = "Gang Pacts (Boss)";
				component10.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				component10.fontSize = 14;
				((Graphic)component10).color = Color.white;
				component10.alignment = (TextAnchor)4;
				component10.fontStyle = (FontStyle)1;
				GameObject val7 = (_myPactBtnGo = new GameObject("BtnMyPact", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(LayoutElement)
				}));
				val7.transform.SetParent(val2, false);
				val7.transform.SetAsLastSibling();
				LayoutElement component11 = val7.GetComponent<LayoutElement>();
				component11.minHeight = 35f;
				component11.preferredHeight = 35f;
				Image component12 = val7.GetComponent<Image>();
				((Graphic)component12).color = new Color(0.15f, 0.35f, 0.15f, 0.95f);
				Button component13 = val7.GetComponent<Button>();
				((Selectable)component13).targetGraphic = (Graphic)(object)component12;
				((UnityEvent)component13.onClick).AddListener(new UnityAction(ToggleMyPactPopup));
				GameObject val8 = new GameObject("Text", new Type[2]
				{
					typeof(RectTransform),
					typeof(Text)
				});
				val8.transform.SetParent(val7.transform, false);
				RectTransform component14 = val8.GetComponent<RectTransform>();
				component14.anchorMin = Vector2.zero;
				component14.anchorMax = Vector2.one;
				component14.offsetMin = Vector2.zero;
				component14.offsetMax = Vector2.zero;
				Text component15 = val8.GetComponent<Text>();
				component15.text = "My Pact";
				component15.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				component15.fontSize = 14;
				((Graphic)component15).color = new Color(0.6f, 1f, 0.6f);
				component15.alignment = (TextAnchor)4;
				component15.fontStyle = (FontStyle)1;
				val7.SetActive(false);
				GameObject val9 = (_grapevineBtnGo = new GameObject("BtnGrapevine", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(LayoutElement)
				}));
				val9.transform.SetParent(val2, false);
				val9.transform.SetAsLastSibling();
				LayoutElement component16 = val9.GetComponent<LayoutElement>();
				component16.minHeight = 35f;
				component16.preferredHeight = 35f;
				Image component17 = val9.GetComponent<Image>();
				((Graphic)component17).color = new Color(0.25f, 0.2f, 0.3f, 0.95f);
				Button component18 = val9.GetComponent<Button>();
				((Selectable)component18).targetGraphic = (Graphic)(object)component17;
				((UnityEvent)component18.onClick).AddListener(new UnityAction(ToggleGrapevinePopup));
				GameObject val10 = new GameObject("Text", new Type[2]
				{
					typeof(RectTransform),
					typeof(Text)
				});
				val10.transform.SetParent(val9.transform, false);
				RectTransform component19 = val10.GetComponent<RectTransform>();
				component19.anchorMin = Vector2.zero;
				component19.anchorMax = Vector2.one;
				component19.offsetMin = Vector2.zero;
				component19.offsetMax = Vector2.zero;
				Text component20 = val10.GetComponent<Text>();
				component20.text = "The Grapevine";
				component20.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				component20.fontSize = 14;
				((Graphic)component20).color = new Color(0.85f, 0.75f, 0.95f);
				component20.alignment = (TextAnchor)4;
				component20.fontStyle = (FontStyle)1;
				val9.SetActive(false);
				if (EnableDirtyCash.Value)
				{
					GameObject val11 = (_safeboxBtnGo = new GameObject("BtnSafebox", new Type[4]
					{
						typeof(RectTransform),
						typeof(Image),
						typeof(Button),
						typeof(LayoutElement)
					}));
					val11.transform.SetParent(val2, false);
					val11.transform.SetAsLastSibling();
					LayoutElement component21 = val11.GetComponent<LayoutElement>();
					component21.minHeight = 35f;
					component21.preferredHeight = 35f;
					Image component22 = val11.GetComponent<Image>();
					((Graphic)component22).color = new Color(0.2f, 0.3f, 0.15f, 0.95f);
					Button component23 = val11.GetComponent<Button>();
					((Selectable)component23).targetGraphic = (Graphic)(object)component22;
					((UnityEvent)component23.onClick).AddListener(new UnityAction(ToggleSafeboxPopup));
					GameObject val12 = new GameObject("Text", new Type[2]
					{
						typeof(RectTransform),
						typeof(Text)
					});
					val12.transform.SetParent(val11.transform, false);
					RectTransform component24 = val12.GetComponent<RectTransform>();
					component24.anchorMin = Vector2.zero;
					component24.anchorMax = Vector2.one;
					component24.offsetMin = Vector2.zero;
					component24.offsetMax = Vector2.zero;
					Text component25 = val12.GetComponent<Text>();
					component25.text = "Safebox (Boss)";
					component25.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
					component25.fontSize = 14;
					((Graphic)component25).color = new Color(0.7f, 0.95f, 0.7f);
					component25.alignment = (TextAnchor)4;
					component25.fontStyle = (FontStyle)1;
					val11.SetActive(false);
				}
				CreateHandlerPopup(val);
			}
		}

		private static void CreateHandlerPopup(GameObject parent)
		{

			_handlerPopup = new GameObject("CrewRelationsPopup", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup)
			});
			_handlerPopup.transform.SetParent(parent.transform, false);
			RectTransform component = _handlerPopup.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0.5f, 0.5f);
			component.anchorMax = new Vector2(0.5f, 0.5f);
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(380f, 520f);
			((Graphic)_handlerPopup.GetComponent<Image>()).color = new Color(0.12f, 0.1f, 0.08f, 0.98f);
			VerticalLayoutGroup component2 = _handlerPopup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component2).padding = new RectOffset(10, 10, 10, 10);
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 6f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
			GameObject obj = CreateHorizontalRow(_handlerPopup.transform, "HeaderRow");
			LayoutElement component3 = obj.GetComponent<LayoutElement>();
			component3.minHeight = 20f;
			component3.preferredHeight = 20f;
			_btnPrevCrew = CreateButton(obj.transform, "PrevCrew", "<", OnPrevCrew);
			LayoutElement component4 = ((Component)_btnPrevCrew).GetComponent<LayoutElement>();
			component4.preferredWidth = 20f;
			component4.minHeight = 18f;
			component4.preferredHeight = 18f;
			_titleText = CreateLabel(obj.transform, "Title", "Crew Relations", 11, (FontStyle)1);
			_titleText.alignment = (TextAnchor)4;
			((Component)_titleText).GetComponent<LayoutElement>().minHeight = 16f;
			((Component)_titleText).GetComponent<LayoutElement>().preferredHeight = 16f;
			_btnNextCrew = CreateButton(obj.transform, "NextCrew", ">", OnNextCrew);
			LayoutElement component5 = ((Component)_btnNextCrew).GetComponent<LayoutElement>();
			component5.preferredWidth = 20f;
			component5.minHeight = 18f;
			component5.preferredHeight = 18f;
			LayoutElement component6 = ((Component)CreateButton(obj.transform, "Close", "X", ClosePopup)).GetComponent<LayoutElement>();
			component6.preferredWidth = 18f;
			component6.minHeight = 18f;
			component6.preferredHeight = 18f;
			GameObject val = new GameObject("HandlerScroll", new Type[3]
			{
				typeof(RectTransform),
				typeof(ScrollRect),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_handlerPopup.transform, false);
			val.GetComponent<LayoutElement>().flexibleHeight = 1f;
			GameObject val2 = new GameObject("Viewport", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component7 = val2.GetComponent<RectTransform>();
			component7.anchorMin = Vector2.zero;
			component7.anchorMax = Vector2.one;
			component7.offsetMin = Vector2.zero;
			component7.offsetMax = Vector2.zero;
			((Graphic)val2.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
			val2.GetComponent<Mask>().showMaskGraphic = false;
			GameObject val3 = new GameObject("Content", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter)
			});
			val3.transform.SetParent(val2.transform, false);
			RectTransform component8 = val3.GetComponent<RectTransform>();
			component8.anchorMin = new Vector2(0f, 1f);
			component8.anchorMax = new Vector2(1f, 1f);
			component8.pivot = new Vector2(0.5f, 1f);
			component8.sizeDelta = new Vector2(0f, 0f);
			val3.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
			VerticalLayoutGroup component9 = val3.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component9).spacing = 6f;
			((HorizontalOrVerticalLayoutGroup)component9).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component9).childForceExpandHeight = false;
			((LayoutGroup)component9).padding = new RectOffset(2, 2, 2, 2);
			ScrollRect component10 = val.GetComponent<ScrollRect>();
			component10.viewport = component7;
			component10.content = component8;
			component10.horizontal = false;
			component10.vertical = true;
			component10.scrollSensitivity = 30f;
			component10.movementType = (ScrollRect.MovementType)2;
			Transform transform = val3.transform;
			if (EnableDirtyCash.Value)
			{
				_dirtyCashText = CreateLabel(transform, "DirtyCash", "Dirty Cash: $0", 11, (FontStyle)1);
				((Graphic)_dirtyCashText).color = new Color(0.85f, 0.6f, 0.2f);
				((Component)_dirtyCashText).GetComponent<LayoutElement>().minHeight = 18f;
			}
			CreateLabel(transform, "SCLabel", "Street Credit", 12, (FontStyle)1);
			CreateStatBar(transform, "StreetCreditBar", new Color(0.85f, 0.7f, 0.2f), out _streetCreditFill);
			_streetCreditLevelText = CreateLabel(transform, "SCLevel", "Level: 0", 11, (FontStyle)0);
			CreateLabel(transform, "WLabel", "Wanted Level", 12, (FontStyle)1);
			CreateStatBar(transform, "WantedBar", new Color(0.8f, 0.2f, 0.2f), out _wantedFill);
			_wantedLevelText = CreateLabel(transform, "WLevel", "Status: None", 11, (FontStyle)0);
			GameObject obj2 = CreateHorizontalRow(transform, "BribeRow");
			_btnBribeMayor = CreateButton(obj2.transform, "BribeMayor", "Bribe Mayor", OnBribeMayor);
			_btnBribeJudge = CreateButton(obj2.transform, "BribeJudge", "Bribe Judge", OnBribeJudge);
			_txtBribeMayor = ((Component)_btnBribeMayor).GetComponentInChildren<Text>();
			_txtBribeJudge = ((Component)_btnBribeJudge).GetComponentInChildren<Text>();
			_btnThreatenWitness = CreateButton(CreateHorizontalRow(transform, "WitnessRow").transform, "ThreatenWitness", "Threaten Witness", OnThreatenWitness);
			_txtThreatenWitness = ((Component)_btnThreatenWitness).GetComponentInChildren<Text>();
			_lawyerSection = ((Component)CreateLabel(transform, "LawyerStatus", "", 11, (FontStyle)0)).gameObject;
			_txtLawyerStatus = _lawyerSection.GetComponent<Text>();
			_lawyerRow = CreateHorizontalRow(transform, "LawyerRow");
			_btnPayLawyer = CreateButton(_lawyerRow.transform, "PayLawyer", "Hire Lawyer ($1000)", OnPayLawyer);
			_txtPayLawyer = ((Component)_btnPayLawyer).GetComponentInChildren<Text>();
			_lawyerSection.SetActive(false);
			_lawyerRow.SetActive(false);
			_boozeSellRow = CreateHorizontalRow(transform, "BoozeSellRow");
			_btnSellBooze = CreateButton(_boozeSellRow.transform, "SellBooze", "Sell Booze (3x)", OnOpenBoozeSell);
			_txtSellBooze = ((Component)_btnSellBooze).GetComponentInChildren<Text>();
			_boozeSellRow.SetActive(false);
			CreateLabel(transform, "HLabel", "Happiness", 12, (FontStyle)1);
			CreateStatBar(transform, "HappinessBar", new Color(0.2f, 0.75f, 0.3f), out _happinessFill);
			_happinessMoodText = CreateLabel(transform, "HMoodText", "Content", 11, (FontStyle)0);
			_happinessMoodText.alignment = (TextAnchor)4;
			GameObject obj3 = CreateHorizontalRow(transform, "HappyRow");
			_btnVacation = CreateButton(obj3.transform, "Vacation", "Vacation", OnVacation);
			_btnGift = CreateButton(obj3.transform, "Gift", "Gift", OnGift);
			_btnPepTalk = CreateButton(obj3.transform, "PepTalk", "Pep Talk", OnPepTalk);
			_txtVacation = ((Component)_btnVacation).GetComponentInChildren<Text>();
			_txtGift = ((Component)_btnGift).GetComponentInChildren<Text>();
			_txtPepTalk = ((Component)_btnPepTalk).GetComponentInChildren<Text>();
			CreateLabel(transform, "ActionsLabel", "Crew Actions", 12, (FontStyle)1);
			GameObject obj4 = CreateHorizontalRow(transform, "ActionsRow");
			_btnUnderboss = CreateButton(obj4.transform, "Underboss", "Underboss", OnUnderboss);
			_txtUnderboss = ((Component)_btnUnderboss).GetComponentInChildren<Text>();
			_btnMarryChild = CreateButton(obj4.transform, "MarryChild", "Spouse", OnMarryChild);
			_txtMarryChild = ((Component)_btnMarryChild).GetComponentInChildren<Text>();
			_btnHireFamily = CreateButton(obj4.transform, "HireFamily", "Hire Family", OnHireFamily);
			_txtHireFamily = ((Component)_btnHireFamily).GetComponentInChildren<Text>();
			GameObject obj5 = CreateHorizontalRow(transform, "RenameAgeRow");
			_btnRename = CreateButton(obj5.transform, "Rename", "Rename", OnToggleRename);
			_txtRename = ((Component)_btnRename).GetComponentInChildren<Text>();
			_btnAge = CreateButton(obj5.transform, "AgeUp", "Age +1yr", OnAgeUp);
			_txtAge = ((Component)_btnAge).GetComponentInChildren<Text>();
			_btnNickname = CreateButton(obj5.transform, "Nickname", "Nickname", OnToggleNickname);
			_txtNickname = ((Component)_btnNickname).GetComponentInChildren<Text>();
			_renameRow = new GameObject("RenameRow", new Type[3]
			{
				typeof(RectTransform),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			_renameRow.transform.SetParent(transform, false);
			_renameRow.GetComponent<LayoutElement>().minHeight = 28f;
			_renameRow.GetComponent<LayoutElement>().preferredHeight = 28f;
			HorizontalLayoutGroup component11 = _renameRow.GetComponent<HorizontalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component11).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component11).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component11).childForceExpandHeight = true;
			GameObject val4 = new GameObject("FirstInput", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(InputField),
				typeof(LayoutElement)
			});
			val4.transform.SetParent(_renameRow.transform, false);
			((Graphic)val4.GetComponent<Image>()).color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
			val4.GetComponent<LayoutElement>().flexibleWidth = 1f;
			GameObject val5 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val5.transform.SetParent(val4.transform, false);
			RectTransform component12 = val5.GetComponent<RectTransform>();
			component12.anchorMin = Vector2.zero;
			component12.anchorMax = Vector2.one;
			component12.offsetMin = new Vector2(4f, 0f);
			component12.offsetMax = new Vector2(-4f, 0f);
			Text component13 = val5.GetComponent<Text>();
			component13.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component13.fontSize = 11;
			((Graphic)component13).color = Color.white;
			component13.alignment = (TextAnchor)3;
			component13.supportRichText = false;
			_inputFirstName = val4.GetComponent<InputField>();
			_inputFirstName.textComponent = component13;
			_inputFirstName.characterLimit = 20;
			val4.AddComponent<InputFieldBlocker>();
			GameObject val6 = new GameObject("LastInput", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(InputField),
				typeof(LayoutElement)
			});
			val6.transform.SetParent(_renameRow.transform, false);
			((Graphic)val6.GetComponent<Image>()).color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
			val6.GetComponent<LayoutElement>().flexibleWidth = 1f;
			GameObject val7 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val7.transform.SetParent(val6.transform, false);
			RectTransform component14 = val7.GetComponent<RectTransform>();
			component14.anchorMin = Vector2.zero;
			component14.anchorMax = Vector2.one;
			component14.offsetMin = new Vector2(4f, 0f);
			component14.offsetMax = new Vector2(-4f, 0f);
			Text component15 = val7.GetComponent<Text>();
			component15.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component15.fontSize = 11;
			((Graphic)component15).color = Color.white;
			component15.alignment = (TextAnchor)3;
			component15.supportRichText = false;
			_inputLastName = val6.GetComponent<InputField>();
			_inputLastName.textComponent = component15;
			_inputLastName.characterLimit = 20;
			val6.AddComponent<InputFieldBlocker>();
			Button obj6 = CreateButton(_renameRow.transform, "SaveName", "Save", OnSaveRename);
			((Graphic)((Component)obj6).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
			((Component)obj6).GetComponent<LayoutElement>().preferredWidth = 50f;
			_renameRow.SetActive(false);
			_nicknameRow = new GameObject("NicknameRow", new Type[3]
			{
				typeof(RectTransform),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			_nicknameRow.transform.SetParent(transform, false);
			_nicknameRow.GetComponent<LayoutElement>().minHeight = 28f;
			_nicknameRow.GetComponent<LayoutElement>().preferredHeight = 28f;
			HorizontalLayoutGroup component16 = _nicknameRow.GetComponent<HorizontalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component16).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component16).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component16).childForceExpandHeight = true;
			GameObject val8 = new GameObject("NicknameInput", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(InputField),
				typeof(LayoutElement)
			});
			val8.transform.SetParent(_nicknameRow.transform, false);
			((Graphic)val8.GetComponent<Image>()).color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
			val8.GetComponent<LayoutElement>().flexibleWidth = 1f;
			GameObject val9 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val9.transform.SetParent(val8.transform, false);
			Text component17 = val9.GetComponent<Text>();
			component17.font = Font.CreateDynamicFontFromOSFont("Arial", 11);
			component17.fontSize = 11;
			((Graphic)component17).color = Color.white;
			component17.alignment = (TextAnchor)3;
			component17.supportRichText = false;
			RectTransform component18 = val9.GetComponent<RectTransform>();
			component18.anchorMin = Vector2.zero;
			component18.anchorMax = Vector2.one;
			component18.offsetMin = new Vector2(4f, 0f);
			component18.offsetMax = new Vector2(-4f, 0f);
			_inputNickname = val8.GetComponent<InputField>();
			_inputNickname.textComponent = component17;
			_inputNickname.characterLimit = 20;
			val8.AddComponent<InputFieldBlocker>();
			Button obj7 = CreateButton(_nicknameRow.transform, "SaveNickname", "Save", OnSaveNickname);
			((Graphic)((Component)obj7).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
			((Component)obj7).GetComponent<LayoutElement>().preferredWidth = 50f;
			Button obj8 = CreateButton(_nicknameRow.transform, "ClearNickname", "Clear", OnClearNickname);
			((Graphic)((Component)obj8).GetComponent<Image>()).color = new Color(0.4f, 0.2f, 0.2f, 0.95f);
			((Component)obj8).GetComponent<LayoutElement>().preferredWidth = 50f;
			_nicknameRow.SetActive(false);
			GameObject obj9 = CreateHorizontalRow(transform, "XPRow");
			obj9.GetComponent<LayoutElement>().minHeight = 28f;
			((Component)CreateLabel(obj9.transform, "XPLabel", "Adjust XP:", 11, (FontStyle)0)).GetComponent<LayoutElement>().preferredWidth = 70f;
			_btnXpMinus = CreateButton(obj9.transform, "XpMinus", $"-{_xpAdjustAmount}", OnXpMinus);
			((Graphic)((Component)_btnXpMinus).GetComponent<Image>()).color = new Color(0.5f, 0.25f, 0.2f, 0.95f);
			((Component)_btnXpMinus).GetComponent<LayoutElement>().preferredWidth = 60f;
			_txtXpDisplay = CreateLabel(obj9.transform, "XpDisplay", "XP: 0", 11, (FontStyle)1);
			_txtXpDisplay.alignment = (TextAnchor)4;
			((Component)_txtXpDisplay).GetComponent<LayoutElement>().flexibleWidth = 1f;
			_btnXpPlus = CreateButton(obj9.transform, "XpPlus", $"+{_xpAdjustAmount}", OnXpPlus);
			((Graphic)((Component)_btnXpPlus).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
			((Component)_btnXpPlus).GetComponent<LayoutElement>().preferredWidth = 60f;
			Button obj10 = CreateButton(obj9.transform, "XpCycle", "Amt", OnXpCycleAmount);
			((Graphic)((Component)obj10).GetComponent<Image>()).color = new Color(0.3f, 0.3f, 0.4f, 0.95f);
			((Component)obj10).GetComponent<LayoutElement>().preferredWidth = 40f;
			AddDragHandler(_handlerPopup, component);
			_handlerPopup.SetActive(false);
			CreateFamilyHirePopup(parent);
		}

		private static void CreateFamilyHirePopup(GameObject parent)
		{

			_familyHirePopup = new GameObject("FamilyHirePopup", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup)
			});
			_familyHirePopup.transform.SetParent(parent.transform, false);
			RectTransform component = _familyHirePopup.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0.5f, 0.5f);
			component.anchorMax = new Vector2(0.5f, 0.5f);
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(420f, 480f);
			component.anchoredPosition = new Vector2(200f, 0f);
			((Graphic)_familyHirePopup.GetComponent<Image>()).color = new Color(0.1f, 0.12f, 0.08f, 0.98f);
			VerticalLayoutGroup component2 = _familyHirePopup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component2).padding = new RectOffset(8, 8, 8, 8);
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
			GameObject obj = CreateHorizontalRow(_familyHirePopup.transform, "FH_Header");
			obj.GetComponent<LayoutElement>().minHeight = 30f;
			CreateLabel(obj.transform, "FH_Title", "Hire Family Member", 14, (FontStyle)1);
			((Component)CreateButton(obj.transform, "FH_Close", "X", CloseFamilyHirePopup)).GetComponent<LayoutElement>().preferredWidth = 30f;
			GameObject obj2 = CreateHorizontalRow(_familyHirePopup.transform, "FH_FilterRow1");
			obj2.GetComponent<LayoutElement>().minHeight = 28f;
			((Component)CreateButton(obj2.transform, "FH_EthFilter", "Ethnicity: All", CycleEthnicityFilter)).GetComponentInChildren<Text>().fontSize = 10;
			((Component)CreateButton(obj2.transform, "FH_TraitFilter", "Trait: All", CycleTraitFilter)).GetComponentInChildren<Text>().fontSize = 10;
			GameObject obj3 = CreateHorizontalRow(_familyHirePopup.transform, "FH_FilterRow2");
			obj3.GetComponent<LayoutElement>().minHeight = 28f;
			((Component)CreateButton(obj3.transform, "FH_AgeDown", "Min Age -", delegate
			{
				_filterMinAge = Math.Max(0, _filterMinAge - 5);
				RefreshFamilyHireList();
			})).GetComponentInChildren<Text>().fontSize = 10;
			((Component)CreateButton(obj3.transform, "FH_AgeUp", "Min Age +", delegate
			{
				_filterMinAge = Math.Min(90, _filterMinAge + 5);
				RefreshFamilyHireList();
			})).GetComponentInChildren<Text>().fontSize = 10;
			((Component)CreateButton(obj3.transform, "FH_MaxDown", "Max Age -", delegate
			{
				_filterMaxAge = Math.Max(10, _filterMaxAge - 5);
				RefreshFamilyHireList();
			})).GetComponentInChildren<Text>().fontSize = 10;
			((Component)CreateButton(obj3.transform, "FH_MaxUp", "Max Age +", delegate
			{
				_filterMaxAge = Math.Min(100, _filterMaxAge + 5);
				RefreshFamilyHireList();
			})).GetComponentInChildren<Text>().fontSize = 10;
			_familyHireStatusText = CreateLabel(_familyHirePopup.transform, "FH_Status", "Showing all relatives", 10, (FontStyle)2);
			GameObject val = new GameObject("FH_ScrollArea", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(ScrollRect),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_familyHirePopup.transform, false);
			LayoutElement component3 = val.GetComponent<LayoutElement>();
			component3.minHeight = 300f;
			component3.flexibleHeight = 1f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.08f, 0.08f, 0.06f, 0.9f);
			GameObject val2 = new GameObject("Viewport", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component4 = val2.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = new Vector2(2f, 2f);
			component4.offsetMax = new Vector2(-2f, -2f);
			((Graphic)val2.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
			val2.GetComponent<Mask>().showMaskGraphic = false;
			GameObject val3 = new GameObject("Content", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter)
			});
			val3.transform.SetParent(val2.transform, false);
			RectTransform component5 = val3.GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0f, 1f);
			component5.anchorMax = new Vector2(1f, 1f);
			component5.pivot = new Vector2(0.5f, 1f);
			component5.sizeDelta = new Vector2(0f, 0f);
			val3.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
			VerticalLayoutGroup component6 = val3.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component6).spacing = 2f;
			((HorizontalOrVerticalLayoutGroup)component6).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component6).childForceExpandHeight = false;
			((LayoutGroup)component6).padding = new RectOffset(4, 4, 4, 4);
			_familyHireContent = val3.transform;
			_familyHireScroll = val.GetComponent<ScrollRect>();
			_familyHireScroll.viewport = component4;
			_familyHireScroll.content = component5;
			_familyHireScroll.horizontal = false;
			_familyHireScroll.vertical = true;
			_familyHireScroll.scrollSensitivity = 30f;
			_familyHireScroll.movementType = (ScrollRect.MovementType)2;
			_familyHirePopup.SetActive(false);
		}

		private static void ClosePopup()
		{
			_popupVisible = false;
			_handlerPopup.SetActive(false);
			if ((UnityEngine.Object)(object)_boozeSellPopup != (UnityEngine.Object)null)
			{
				_boozeSellVisible = false;
				_boozeSellPopup.SetActive(false);
			}
		}

		private static void OnPrevCrew()
		{

			RefreshCrewList();
			if (_crewList.Count != 0)
			{
				_currentCrewIndex = (_currentCrewIndex - 1 + _crewList.Count) % _crewList.Count;
				CrewAssignment val = _crewList[_currentCrewIndex];
				_selectedPeep = val.GetPeep();
				RefreshHandlerUI();
			}
		}

		private static void OnNextCrew()
		{

			RefreshCrewList();
			if (_crewList.Count != 0)
			{
				_currentCrewIndex = (_currentCrewIndex + 1) % _crewList.Count;
				CrewAssignment val = _crewList[_currentCrewIndex];
				_selectedPeep = val.GetPeep();
				RefreshHandlerUI();
			}
		}

		private static void RefreshCrewList()
		{

			_crewList.Clear();
			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null)
			{
				return;
			}
			foreach (CrewAssignment item in humanCrew.GetLiving())
			{
				_crewList.Add(item);
			}
			if (_selectedPeep == null)
			{
				return;
			}
			for (int i = 0; i < _crewList.Count; i++)
			{
				CrewAssignment val = _crewList[i];
				Entity peep = val.GetPeep();
				EntityID? val2 = ((peep != null) ? new EntityID?(peep.Id) : ((EntityID?)null));
				EntityID id = _selectedPeep.Id;
				if (val2.HasValue && val2.GetValueOrDefault() == id)
				{
					_currentCrewIndex = i;
					break;
				}
			}
		}

		private static void AddDragHandler(GameObject popup, RectTransform rt)
		{
			popup.AddComponent<PopupDragHandler>().rectTransform = rt;
		}

		private static void ToggleHandlerPopup()
		{
			_popupVisible = !_popupVisible;
			_handlerPopup.SetActive(_popupVisible);
			if (_popupVisible)
			{
				RefreshHandlerUI();
			}
		}

		internal static void ToggleCrewRelationsPopup()
		{

			if ((UnityEngine.Object)(object)_handlerPopup == (UnityEngine.Object)null)
			{
				return;
			}
			if (_selectedPeep == null && _crewList != null && _crewList.Count > 0)
			{
				CrewAssignment val = _crewList.FirstOrDefault((CrewAssignment c) => c.IsValid);
				if (val.IsValid)
				{
					_selectedPeep = val.GetPeep();
					_currentCrewIndex = 0;
				}
			}
			ToggleHandlerPopup();
		}

		private static void RefreshHandlerUI()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			string jailStatusString = JailSystem.GetJailStatusString(_selectedPeep.Id);
			if (!string.IsNullOrEmpty(jailStatusString))
			{
				_titleText.text = _selectedPeep.data.person.ShortName + "\n<size=10>" + jailStatusString + "</size>";
			}
			else
			{
				_titleText.text = "Relations: " + _selectedPeep.data.person.ShortName;
			}
			if ((UnityEngine.Object)(object)_dirtyCashText != (UnityEngine.Object)null && EnableDirtyCash.Value)
			{
				int totalDirtyCash = GetTotalDirtyCash();
				int playerCleanCash = GetPlayerCleanCash();
				_dirtyCashText.text = $"Clean: ${playerCleanCash} | Dirty: ${totalDirtyCash}";
			}
			_streetCreditFill.fillAmount = orCreateCrewState.StreetCreditProgress;
			_streetCreditLevelText.text = $"Level: {orCreateCrewState.StreetCreditLevel}";
			_wantedFill.fillAmount = orCreateCrewState.WantedProgress;
			string text = $"Status: {orCreateCrewState.WantedLevel}";
			bool flag = JailSystem.IsInJail(_selectedPeep.Id);
			if (flag)
			{
				var (num, _, flag2) = JailSystem.GetArrestInfo(_selectedPeep.Id);
				if (num > 0)
				{
					text = (flag2 ? $"IN JAIL - Trial: {num}d (Bribed)" : $"IN JAIL - Trial: {num}d");
				}
				else
				{
					int item = JailSystem.GetImprisonmentInfo(_selectedPeep.Id).daysRemaining;
					if (item > 0)
					{
						text = $"PRISON - {item} days left";
					}
				}
			}
			else if (orCreateCrewState.FedsIncoming)
			{
				text += $" (Feds in {orCreateCrewState.FedArrivalCountdown} days!)";
			}
			_wantedLevelText.text = text;
			_txtBribeMayor.text = (_globalMayorBribeActive ? "Mayor (Active - All Crew)" : $"Mayor (${10000})");
			int num2 = GetBribeCost(orCreateCrewState.WantedLevel) * 2;
			_txtBribeJudge.text = (orCreateCrewState.JudgeBribeActive ? "Judge (Active)" : $"Judge (${num2})");
			((Selectable)_btnBribeMayor).interactable = !_globalMayorBribeActive;
			((Selectable)_btnBribeJudge).interactable = !orCreateCrewState.JudgeBribeActive && orCreateCrewState.WantedLevel != WantedLevel.None;
			if ((UnityEngine.Object)(object)_btnThreatenWitness != (UnityEngine.Object)null)
			{
				bool active = orCreateCrewState.HasWitness || orCreateCrewState.WitnessThreatAttempted;
				((Component)_btnThreatenWitness).gameObject.SetActive(active);
				((Selectable)_btnThreatenWitness).interactable = orCreateCrewState.HasWitness && !orCreateCrewState.WitnessThreatAttempted;
				if (orCreateCrewState.WitnessThreatenedSuccessfully)
				{
					_txtThreatenWitness.text = "Witness Silenced";
				}
				else if (orCreateCrewState.WitnessThreatAttempted)
				{
					_txtThreatenWitness.text = "Witness Scared Off";
				}
				else if (orCreateCrewState.HasWitness)
				{
					_txtThreatenWitness.text = "Threaten Witness";
				}
				else
				{
					_txtThreatenWitness.text = "No Witness";
				}
			}
			if ((UnityEngine.Object)(object)_lawyerSection != (UnityEngine.Object)null && (UnityEngine.Object)(object)_lawyerRow != (UnityEngine.Object)null)
			{
				_lawyerSection.SetActive(flag);
				_lawyerRow.SetActive(flag);
				if (flag)
				{
					string text2 = ((orCreateCrewState.LawyerRetainer > 0) ? $"Lawyer Retainer: ${orCreateCrewState.LawyerRetainer}" : "No lawyer hired");
					_txtLawyerStatus.text = text2;
					_txtPayLawyer.text = "Add Retainer ($1000)";
				}
			}
			if ((UnityEngine.Object)(object)_boozeSellRow != (UnityEngine.Object)null)
			{
				_boozeSellRow.SetActive(flag);
				if (flag)
				{
					int days = G.GetNow().days;
					bool flag3 = orCreateCrewState.LastBoozeSellTurn == 0 || days - orCreateCrewState.LastBoozeSellTurn >= 28;
					((Selectable)_btnSellBooze).interactable = flag3;
					if (!flag3)
					{
						int num3 = 28 - (days - orCreateCrewState.LastBoozeSellTurn);
						int num4 = Math.Max(1, (int)Math.Ceiling((double)num3 / 7.0));
						_txtSellBooze.text = $"Sell Booze ({num4}w cd)";
					}
					else
					{
						_txtSellBooze.text = "Sell Booze (3x)";
					}
				}
			}
			_happinessFill.fillAmount = orCreateCrewState.HappinessValue;
			if ((UnityEngine.Object)(object)_happinessMoodText != (UnityEngine.Object)null)
			{
				int num5 = ModConstants.MOOD_THRESHOLDS.Length - 1;
				for (int i = 0; i < ModConstants.MOOD_THRESHOLDS.Length; i++)
				{
					if (orCreateCrewState.HappinessValue >= ModConstants.MOOD_THRESHOLDS[i])
					{
						num5 = i;
						break;
					}
				}
				_happinessMoodText.text = ModConstants.MOOD_LABELS[num5];
				((Graphic)_happinessMoodText).color = ModConstants.MOOD_COLORS[num5];
				((Graphic)_happinessFill).color = ModConstants.MOOD_COLORS[num5];
			}
			if (orCreateCrewState.OnVacation)
			{
				_txtVacation.text = "On Vacation";
			}
			else if (orCreateCrewState.VacationPending)
			{
				_txtVacation.text = "Vacation Pending";
			}
			else
			{
				_txtVacation.text = "Vacation ($1500)";
			}
			((Selectable)_btnVacation).interactable = !orCreateCrewState.OnVacation && !orCreateCrewState.VacationPending;
			int num6 = 50;
			_txtGift.text = $"Gift (${num6})";
			PlayerCrew humanCrew = G.GetHumanCrew();
			CrewAssignment val = (CrewAssignment)((humanCrew != null) ? humanCrew.GetCrewForIndex(0) : default(CrewAssignment));
			Entity val2 = (val.IsValid ? val.GetPeep() : null);
			bool flag4 = val2 != null && val2.Id == _selectedPeep.Id;
			((Component)_btnUnderboss).gameObject.SetActive(!flag4);
			_txtUnderboss.text = (orCreateCrewState.IsUnderboss ? "Is Underboss" : "Promote to Underboss");
			((Selectable)_btnUnderboss).interactable = !orCreateCrewState.IsUnderboss;
			RelationshipTracker rels = G.GetRels();
			RelationshipList val3 = ((rels != null) ? rels.GetListOrNull(_selectedPeep.Id) : null);
			if (val3 != null && val3.HasSpouse())
			{
				_txtMarryChild.text = (orCreateCrewState.AwaitingChildBirth ? "Awaiting Birth..." : "Have Child");
				((Selectable)_btnMarryChild).interactable = !orCreateCrewState.AwaitingChildBirth;
			}
			else
			{
				_txtMarryChild.text = "Find Spouse";
				((Selectable)_btnMarryChild).interactable = true;
			}
			if ((UnityEngine.Object)(object)_btnRename != (UnityEngine.Object)null)
			{
				_txtRename.text = (((UnityEngine.Object)(object)_renameRow != (UnityEngine.Object)null && _renameRow.activeSelf) ? "Cancel" : "Rename");
			}
			if ((UnityEngine.Object)(object)_btnAge != (UnityEngine.Object)null)
			{
				SimTimeSpan age = _selectedPeep.data.person.GetAge(G.GetNow());
				float yearsFloat = age.YearsFloat;
				_txtAge.text = $"Age +1yr ({(int)yearsFloat})";
			}
			if ((UnityEngine.Object)(object)_btnNickname != (UnityEngine.Object)null)
			{
				string nickname = _selectedPeep.data.person.nickname;
				_txtNickname.text = (string.IsNullOrEmpty(nickname) ? "Nickname" : ("Nick: " + nickname));
			}
			if ((UnityEngine.Object)(object)_btnPepTalk != (UnityEngine.Object)null)
			{
				int days2 = G.GetNow().days;
				if (days2 != _pepTalkLastTurnDay)
				{
					_pepTalkUsesThisTurn = 0;
					_pepTalkLastTurnDay = days2;
				}
				int num7 = 3 - _pepTalkUsesThisTurn;
				_txtPepTalk.text = $"Pep Talk ({num7})";
				((Selectable)_btnPepTalk).interactable = num7 > 0;
			}
			if (!((UnityEngine.Object)(object)_txtXpDisplay != (UnityEngine.Object)null))
			{
				return;
			}
			try
			{
				int num8 = 0;
				XP val4 = _selectedPeep.data.agent?.xp;
				if (val4 != null)
				{
					Type type = ((object)val4).GetType();
					PropertyInfo propertyInfo = type.GetProperty("TotalXP") ?? type.GetProperty("totalXP");
					if (propertyInfo != null)
					{
						num8 = Convert.ToInt32(propertyInfo.GetValue(val4));
					}
					else
					{
						FieldInfo fieldInfo = type.GetField("totalXP") ?? type.GetField("TotalXP");
						if (fieldInfo != null)
						{
							num8 = Convert.ToInt32(fieldInfo.GetValue(val4));
						}
					}
				}
				_txtXpDisplay.text = $"XP: {num8}";
			}
			catch
			{
				_txtXpDisplay.text = "XP: ???";
			}
		}

		private static void OnUnderboss()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null || orCreateCrewState.IsUnderboss)
			{
				return;
			}
			try
			{
				XP xp = _selectedPeep.data.agent.xp;
				if (xp == null)
				{
					Debug.LogWarning("[GameplayTweaks] xp is null");
					return;
				}
				Label val = default(Label);
				val = new Label("underboss");
				Type type = ((object)xp).GetType();
				MethodInfo method = type.GetMethod("SetCrewRole", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					method.Invoke(xp, new object[1] { val });
					orCreateCrewState.IsUnderboss = true;
					TriggerCrewRoleGivenEvent();
					Debug.Log(("[GameplayTweaks] " + _selectedPeep.data.person.FullName + " promoted to Underboss!"));
					RefreshHandlerUI();
					return;
				}
				FieldInfo field = type.GetField("crewRole", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					field.SetValue(xp, val);
					orCreateCrewState.IsUnderboss = true;
					TriggerCrewRoleGivenEvent();
					Debug.Log(("[GameplayTweaks] " + _selectedPeep.data.person.FullName + " promoted to Underboss via crewRole field"));
					RefreshHandlerUI();
					return;
				}
				Debug.LogWarning(("[GameplayTweaks] Could not set underboss role for " + _selectedPeep.data.person.FullName));
				orCreateCrewState.IsUnderboss = true;
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] OnUnderboss failed: {arg}");
			}
			RefreshHandlerUI();
		}

		private static void TriggerCrewRoleGivenEvent()
		{
			try
			{
				dynamic ctx = G.ctx;
				if (ctx == null)
				{
					return;
				}
				object obj = null;
				dynamic field = ctx.GetType().GetField("events");
				if (field != null)
				{
					obj = field.GetValue(ctx);
				}
				else
				{
					dynamic property = ctx.GetType().GetProperty("events");
					if (property != null)
					{
						obj = property.GetValue(ctx);
					}
				}
				if (obj == null)
				{
					return;
				}
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.SessionEventType");
				if (type == null)
				{
					return;
				}
				FieldInfo field2 = type.GetField("CrewRoleGiven", BindingFlags.Static | BindingFlags.Public);
				if (!(field2 == null))
				{
					object value = field2.GetValue(null);
					MethodInfo method = obj.GetType().GetMethod("EnqueueOnce", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						method.Invoke(obj, new object[1] { value });
						Debug.Log("[GameplayTweaks] Triggered CrewRoleGiven event");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning(("[GameplayTweaks] Could not trigger CrewRoleGiven event: " + ex.Message));
			}
		}

		private static void OnMarryChild()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			RelationshipTracker rels = G.GetRels();
			RelationshipList val = ((rels != null) ? rels.GetListOrNull(_selectedPeep.Id) : null);
			if (val != null && val.HasSpouse())
			{
				Entity spouse = val.GetSpouse();
				Entity val2 = (((int)_selectedPeep.data.person.g == 2) ? _selectedPeep : spouse);
				SimTime now = G.GetNow();
				val2.data.person.futurekids.Add(now.IncrementDays(-1));
				val2.data.person.futurekids.Sort((Comparison<SimTime>)SimTime.CompareDescending);
				CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
				if (orCreateCrewState != null)
				{
					orCreateCrewState.AwaitingChildBirth = true;
					orCreateCrewState.LastFutureKidsCount = val2.data.person.futurekids.Count;
				}
			}
			else
			{
				PeopleTracker peopleGen = G.GetPeopleGen();
				Entity val3 = ((peopleGen != null) ? peopleGen.FindRandoToMarry(_selectedPeep) : null);
				if (val3 != null)
				{
					peopleGen.ForceMarry(_selectedPeep, val3);
				}
			}
			RefreshHandlerUI();
		}

		private static void OnThreatenWitness()
		{
			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null || !orCreateCrewState.HasWitness || orCreateCrewState.WitnessThreatAttempted)
			{
				return;
			}
			orCreateCrewState.WitnessThreatAttempted = true;
			bool flag = false;
			try
			{
				List<PlayerInfo> source = (from p in G.GetAllPlayers()
					where p.IsJustGang && !p.crew.IsCrewDefeated
					orderby CalculateGangPower(p) descending
					select p).Take(4).ToList();
				PlayerInfo human = G.GetHumanPlayer();
				flag = source.Any((PlayerInfo g) => g.PID == human.PID);
			}
			catch
			{
			}
			float num = (flag ? 0.25f : 0.5f);
			if (SharedRng.NextDouble() >= (double)num)
			{
				orCreateCrewState.WitnessThreatenedSuccessfully = true;
				orCreateCrewState.HasWitness = false;
				if (orCreateCrewState.WantedLevel > WantedLevel.None)
				{
					orCreateCrewState.WantedLevel = (WantedLevel)Math.Max(0, (int)(orCreateCrewState.WantedLevel - 1));
					orCreateCrewState.WantedProgress = Mathf.Clamp01(orCreateCrewState.WantedProgress - 0.25f);
				}
				orCreateCrewState.FedsIncoming = false;
				orCreateCrewState.FedArrivalCountdown = 0;
				Debug.Log(("[GameplayTweaks] Successfully threatened witness for " + _selectedPeep.data.person.FullName));
			}
			else
			{
				orCreateCrewState.ExtraJailYears = SharedRng.Next(1, 6);
				orCreateCrewState.WantedProgress = Mathf.Clamp01(orCreateCrewState.WantedProgress + 0.15f);
				if (orCreateCrewState.WantedProgress >= 0.75f)
				{
					orCreateCrewState.WantedLevel = WantedLevel.High;
				}
				else if (orCreateCrewState.WantedProgress >= 0.5f)
				{
					orCreateCrewState.WantedLevel = WantedLevel.Medium;
				}
				Debug.Log($"[GameplayTweaks] Failed! Witness scared off. {_selectedPeep.data.person.FullName} faces {orCreateCrewState.ExtraJailYears} extra years!");
			}
			RefreshHandlerUI();
		}

		private static void ToggleGrapevinePopup()
		{
			if ((UnityEngine.Object)(object)_grapevinePopup == (UnityEngine.Object)null)
			{
				CreateGrapevinePopup();
			}
			_grapevineVisible = !_grapevineVisible;
			_grapevinePopup.SetActive(_grapevineVisible);
			if (_grapevineVisible)
			{
				RefreshGrapevine();
			}
		}

		private static void CreateGrapevinePopup()
		{

			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if (!((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null))
			{
				_grapevinePopup = new GameObject("GrapevinePopup", new Type[5]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(VerticalLayoutGroup),
					typeof(Canvas),
					typeof(GraphicRaycaster)
				});
				_grapevinePopup.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
				Canvas component = _grapevinePopup.GetComponent<Canvas>();
				component.overrideSorting = true;
				component.sortingOrder = 997;
				RectTransform component2 = _grapevinePopup.GetComponent<RectTransform>();
				component2.anchorMin = new Vector2(0.5f, 0.5f);
				component2.anchorMax = new Vector2(0.5f, 0.5f);
				component2.pivot = new Vector2(0.5f, 0.5f);
				component2.sizeDelta = new Vector2(480f, 500f);
				component2.anchoredPosition = new Vector2(0f, 0f);
				((Graphic)_grapevinePopup.GetComponent<Image>()).color = new Color(0.12f, 0.1f, 0.15f, 0.98f);
				VerticalLayoutGroup component3 = _grapevinePopup.GetComponent<VerticalLayoutGroup>();
				((LayoutGroup)component3).padding = new RectOffset(10, 10, 8, 8);
				((HorizontalOrVerticalLayoutGroup)component3).spacing = 3f;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
				GameObject obj = CreateHorizontalRow(_grapevinePopup.transform, "GV_Header");
				obj.GetComponent<LayoutElement>().minHeight = 28f;
				((Graphic)CreateLabel(obj.transform, "GV_Title", "The Grapevine", 14, (FontStyle)1)).color = new Color(0.85f, 0.75f, 0.95f);
				((Component)CreateButton(obj.transform, "GV_Close", "X", delegate
				{
					_grapevineVisible = false;
					_grapevinePopup.SetActive(false);
				})).GetComponent<LayoutElement>().preferredWidth = 28f;
				Text obj2 = CreateLabel(_grapevinePopup.transform, "GV_Sub", "Word on the street...", 10, (FontStyle)2);
				((Graphic)obj2).color = new Color(0.6f, 0.55f, 0.7f);
				obj2.alignment = (TextAnchor)4;
				GameObject val = new GameObject("GV_ScrollArea", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(ScrollRect),
					typeof(LayoutElement)
				});
				val.transform.SetParent(_grapevinePopup.transform, false);
				LayoutElement component4 = val.GetComponent<LayoutElement>();
				component4.flexibleHeight = 1f;
				component4.minHeight = 340f;
				((Graphic)val.GetComponent<Image>()).color = new Color(0.08f, 0.07f, 0.1f, 0.9f);
				GameObject val2 = new GameObject("Viewport", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Mask)
				});
				val2.transform.SetParent(val.transform, false);
				RectTransform component5 = val2.GetComponent<RectTransform>();
				component5.anchorMin = Vector2.zero;
				component5.anchorMax = Vector2.one;
				component5.offsetMin = new Vector2(2f, 2f);
				component5.offsetMax = new Vector2(-2f, -2f);
				((Graphic)val2.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
				val2.GetComponent<Mask>().showMaskGraphic = false;
				GameObject val3 = new GameObject("Content", new Type[3]
				{
					typeof(RectTransform),
					typeof(VerticalLayoutGroup),
					typeof(ContentSizeFitter)
				});
				val3.transform.SetParent(val2.transform, false);
				RectTransform component6 = val3.GetComponent<RectTransform>();
				component6.anchorMin = new Vector2(0f, 1f);
				component6.anchorMax = new Vector2(1f, 1f);
				component6.pivot = new Vector2(0.5f, 1f);
				component6.sizeDelta = new Vector2(0f, 0f);
				val3.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
				VerticalLayoutGroup component7 = val3.GetComponent<VerticalLayoutGroup>();
				((HorizontalOrVerticalLayoutGroup)component7).spacing = 2f;
				((HorizontalOrVerticalLayoutGroup)component7).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component7).childForceExpandHeight = false;
				((LayoutGroup)component7).padding = new RectOffset(4, 4, 4, 4);
				_grapevineContent = val3.transform;
				ScrollRect component8 = val.GetComponent<ScrollRect>();
				component8.viewport = component5;
				component8.content = component6;
				component8.horizontal = false;
				component8.vertical = true;
				component8.scrollSensitivity = 30f;
				component8.movementType = (ScrollRect.MovementType)2;
				_grapevinePopup.SetActive(false);
			}
		}

		private static void RefreshGrapevine()
		{

			if ((UnityEngine.Object)(object)_grapevineContent == (UnityEngine.Object)null)
			{
				return;
			}
			for (int num = _grapevineContent.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_grapevineContent.GetChild(num)).gameObject);
			}
			if (SaveData.GrapevineEvents.Count == 0)
			{
				((Graphic)CreateLabel(_grapevineContent, "Empty", "No news yet. Check back later.", 11, (FontStyle)2)).color = new Color(0.5f, 0.5f, 0.55f);
				return;
			}
			Color color = default(Color);
			Color color2 = default(Color);
			foreach (string grapevineEvent in SaveData.GrapevineEvents)
			{
				GameObject val = new GameObject("GV_Entry", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(LayoutElement)
				});
				val.transform.SetParent(_grapevineContent, false);
				val.GetComponent<LayoutElement>().minHeight = 28f;
				if (grapevineEvent.StartsWith("KILL:") || grapevineEvent.StartsWith("DEATH:"))
				{
					color = new Color(0.18f, 0.1f, 0.1f, 0.9f);
					color2 = new Color(0.9f, 0.6f, 0.6f);
				}
				else if (grapevineEvent.StartsWith("WAR:"))
				{
					color = new Color(0.2f, 0.12f, 0.08f, 0.9f);
					color2 = new Color(0.95f, 0.7f, 0.4f);
				}
				else if (grapevineEvent.StartsWith("PACT:"))
				{
					color = new Color(0.1f, 0.15f, 0.1f, 0.9f);
					color2 = new Color(0.6f, 0.9f, 0.6f);
				}
				else if (grapevineEvent.StartsWith("FRONT:"))
				{
					color = new Color(0.1f, 0.12f, 0.18f, 0.9f);
					color2 = new Color(0.5f, 0.7f, 0.95f);
				}
				else
				{
					color = new Color(0.12f, 0.12f, 0.14f, 0.9f);
					color2 = new Color(0.75f, 0.75f, 0.7f);
				}
				((Graphic)val.GetComponent<Image>()).color = color;
				Text obj = CreateLabel(val.transform, "Text", grapevineEvent, 13, (FontStyle)0);
				((Graphic)obj).color = color2;
				RectTransform component = ((Component)obj).GetComponent<RectTransform>();
				component.anchorMin = Vector2.zero;
				component.anchorMax = Vector2.one;
				component.offsetMin = new Vector2(6f, 2f);
				component.offsetMax = new Vector2(-6f, -2f);
			}
		}

		private static void ToggleSafeboxPopup()
		{
			if ((UnityEngine.Object)(object)_safeboxPopup == (UnityEngine.Object)null)
			{
				CreateSafeboxPopup();
			}
			_safeboxVisible = !_safeboxVisible;
			_safeboxPopup.SetActive(_safeboxVisible);
			if (_safeboxVisible)
			{
				RefreshSafeboxInfo();
			}
		}

		private static void CreateSafeboxPopup()
		{

			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if (!((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null))
			{
				_safeboxPopup = new GameObject("SafeboxPopup", new Type[5]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(VerticalLayoutGroup),
					typeof(Canvas),
					typeof(GraphicRaycaster)
				});
				_safeboxPopup.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
				Canvas component = _safeboxPopup.GetComponent<Canvas>();
				component.overrideSorting = true;
				component.sortingOrder = 998;
				RectTransform component2 = _safeboxPopup.GetComponent<RectTransform>();
				component2.anchorMin = new Vector2(0.5f, 0.5f);
				component2.anchorMax = new Vector2(0.5f, 0.5f);
				component2.pivot = new Vector2(0.5f, 0.5f);
				component2.sizeDelta = new Vector2(400f, 500f);
				component2.anchoredPosition = new Vector2(0f, 0f);
				((Graphic)_safeboxPopup.GetComponent<Image>()).color = new Color(0.1f, 0.12f, 0.08f, 0.98f);
				VerticalLayoutGroup component3 = _safeboxPopup.GetComponent<VerticalLayoutGroup>();
				((LayoutGroup)component3).padding = new RectOffset(10, 10, 8, 8);
				((HorizontalOrVerticalLayoutGroup)component3).spacing = 4f;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
				Outline obj = _safeboxPopup.AddComponent<Outline>();
				((Shadow)obj).effectColor = new Color(0.4f, 0.7f, 0.3f, 0.8f);
				((Shadow)obj).effectDistance = new Vector2(2f, -2f);
				GameObject obj2 = CreateHorizontalRow(_safeboxPopup.transform, "SB_Header");
				obj2.GetComponent<LayoutElement>().minHeight = 28f;
				((Graphic)CreateLabel(obj2.transform, "SB_Title", "Safebox", 14, (FontStyle)1)).color = new Color(0.7f, 0.95f, 0.7f);
				((Component)CreateButton(obj2.transform, "SB_Close", "X", delegate
				{
					_safeboxVisible = false;
					_safeboxPopup.SetActive(false);
				})).GetComponent<LayoutElement>().preferredWidth = 28f;
				_safeboxCleanText = CreateLabel(_safeboxPopup.transform, "SB_Clean", "Clean Cash: $0", 12, (FontStyle)1);
				((Graphic)_safeboxCleanText).color = new Color(0.3f, 0.9f, 0.3f);
				_safeboxDirtyText = CreateLabel(_safeboxPopup.transform, "SB_Dirty", "Dirty Cash: $0", 12, (FontStyle)1);
				((Graphic)_safeboxDirtyText).color = new Color(0.9f, 0.65f, 0.2f);
				_safeboxTotalText = CreateLabel(_safeboxPopup.transform, "SB_Total", "Total: $0", 12, (FontStyle)1);
				((Graphic)_safeboxTotalText).color = new Color(0.95f, 0.95f, 0.8f);
				((Graphic)CreateLabel(_safeboxPopup.transform, "SB_Div", "--- Building Inventories ---", 10, (FontStyle)2)).color = new Color(0.5f, 0.6f, 0.5f);
				GameObject val = new GameObject("SB_ScrollArea", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(ScrollRect),
					typeof(LayoutElement)
				});
				val.transform.SetParent(_safeboxPopup.transform, false);
				LayoutElement component4 = val.GetComponent<LayoutElement>();
				component4.flexibleHeight = 1f;
				component4.minHeight = 300f;
				((Graphic)val.GetComponent<Image>()).color = new Color(0.08f, 0.09f, 0.06f, 0.9f);
				GameObject val2 = new GameObject("Viewport", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Mask)
				});
				val2.transform.SetParent(val.transform, false);
				RectTransform component5 = val2.GetComponent<RectTransform>();
				component5.anchorMin = Vector2.zero;
				component5.anchorMax = Vector2.one;
				component5.offsetMin = new Vector2(2f, 2f);
				component5.offsetMax = new Vector2(-2f, -2f);
				((Graphic)val2.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
				val2.GetComponent<Mask>().showMaskGraphic = false;
				GameObject val3 = new GameObject("Content", new Type[3]
				{
					typeof(RectTransform),
					typeof(VerticalLayoutGroup),
					typeof(ContentSizeFitter)
				});
				val3.transform.SetParent(val2.transform, false);
				RectTransform component6 = val3.GetComponent<RectTransform>();
				component6.anchorMin = new Vector2(0f, 1f);
				component6.anchorMax = new Vector2(1f, 1f);
				component6.pivot = new Vector2(0.5f, 1f);
				component6.sizeDelta = new Vector2(0f, 0f);
				val3.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
				VerticalLayoutGroup component7 = val3.GetComponent<VerticalLayoutGroup>();
				((HorizontalOrVerticalLayoutGroup)component7).spacing = 3f;
				((HorizontalOrVerticalLayoutGroup)component7).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component7).childForceExpandHeight = false;
				((LayoutGroup)component7).padding = new RectOffset(4, 4, 4, 4);
				_safeboxContent = val3.transform;
				ScrollRect component8 = val.GetComponent<ScrollRect>();
				component8.viewport = component5;
				component8.content = component6;
				component8.horizontal = false;
				component8.vertical = true;
				component8.scrollSensitivity = 30f;
				component8.movementType = (ScrollRect.MovementType)2;
				AddDragHandler(_safeboxPopup, component2);
				_safeboxPopup.SetActive(false);
			}
		}

		private static void RefreshSafeboxInfo()
		{

			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					return;
				}
				int playerCleanCash = GetPlayerCleanCash();
				int totalDirtyCash = GetTotalDirtyCash();
				_safeboxCleanText.text = $"Clean Cash: ${playerCleanCash}";
				_safeboxDirtyText.text = $"Dirty Cash: ${totalDirtyCash}";
				_safeboxTotalText.text = $"Total: ${playerCleanCash + totalDirtyCash}";
				if ((UnityEngine.Object)(object)_safeboxContent != (UnityEngine.Object)null)
				{
					for (int num = _safeboxContent.childCount - 1; num >= 0; num--)
					{
						UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_safeboxContent.GetChild(num)).gameObject);
					}
				}
				EntityID safehouse = humanPlayer.territory.Safehouse;
				if (!safehouse.IsNotValid)
				{
					Entity val = EntityIDExtensions.FindEntity(safehouse);
					if (val != null)
					{
						int dirtyCash = ReadInventoryAmount(val, ModConstants.DIRTY_CASH_LABEL);
						CreateSafeboxEntry("Safehouse", dirtyCash, val);
					}
				}
				try
				{
					PlayerTerritory territory = humanPlayer.territory;
					FieldInfo fieldInfo = ((object)territory).GetType().GetField("_buildings", BindingFlags.Instance | BindingFlags.NonPublic) ?? ((object)territory).GetType().GetField("buildings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (fieldInfo != null)
					{
						if (fieldInfo.GetValue(territory) is IEnumerable enumerable)
						{
							foreach (object item in enumerable)
							{
								try
								{
									EntityID val2;
									if (item is EntityID)
									{
										val2 = (EntityID)item;
										goto IL_01af;
									}
									PropertyInfo propertyInfo = item.GetType().GetProperty("Id") ?? item.GetType().GetProperty("id");
									if (propertyInfo != null)
									{
										val2 = (EntityID)propertyInfo.GetValue(item);
										goto IL_01af;
									}
									continue;
									IL_01af:
									if (!(val2 == safehouse))
									{
										Entity val3 = EntityIDExtensions.FindEntity(val2);
										if (val3 != null)
										{
											string name = ((object)val3).ToString() ?? "Building";
											int dirtyCash2 = ReadInventoryAmount(val3, ModConstants.DIRTY_CASH_LABEL);
											CreateSafeboxEntry(name, dirtyCash2, val3);
										}
									}
									;
								}
								catch
								{
								}
							}
						}
					}
					else
					{
						PropertyInfo propertyInfo2 = ((object)territory).GetType().GetProperty("OwnedBuildings") ?? ((object)territory).GetType().GetProperty("ownedBuildings");
						if (propertyInfo2 != null && propertyInfo2.GetValue(territory) is IEnumerable enumerable2)
						{
							foreach (object item2 in enumerable2)
							{
								try
								{
									Entity val4 = item2 as Entity;
									if (val4 != null)
									{
										goto IL_02a4;
									}
									if (item2 is EntityID)
									{
										val4 = EntityIDExtensions.FindEntity((EntityID)item2);
										goto IL_02a4;
									}
									continue;
									IL_02a4:
									string name2 = ((object)val4).ToString() ?? "Building";
									int dirtyCash3 = ReadInventoryAmount(val4, ModConstants.DIRTY_CASH_LABEL);
									CreateSafeboxEntry(name2, dirtyCash3, val4);
									;
								}
								catch
								{
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning(("[GameplayTweaks] Safebox building list: " + ex.Message));
				}
				if (_safeboxContent.childCount == 0)
				{
					((Graphic)CreateLabel(_safeboxContent, "Empty", "No buildings found.", 11, (FontStyle)2)).color = new Color(0.5f, 0.5f, 0.5f);
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] RefreshSafeboxInfo: {arg}");
			}
		}

		private static void CreateSafeboxEntry(string name, int dirtyCash, Entity entity)
		{

			GameObject val = new GameObject("SB_Entry", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_safeboxContent, false);
			val.GetComponent<LayoutElement>().minHeight = 24f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.12f, 0.14f, 0.1f, 0.9f);
			HorizontalLayoutGroup component = val.GetComponent<HorizontalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandHeight = true;
			((LayoutGroup)component).padding = new RectOffset(6, 6, 2, 2);
			Text obj = CreateLabel(val.transform, "Name", name, 10, (FontStyle)0);
			((Graphic)obj).color = new Color(0.85f, 0.85f, 0.8f);
			((Component)obj).GetComponent<LayoutElement>().flexibleWidth = 3f;
			Text obj2 = CreateLabel(text: (dirtyCash > 0) ? $"${dirtyCash} dirty" : "No dirty cash", parent: val.transform, name: "Cash", size: 10, style: (FontStyle)0);
			((Graphic)obj2).color = ((dirtyCash > 0) ? new Color(0.9f, 0.65f, 0.2f) : new Color(0.5f, 0.5f, 0.5f));
			obj2.alignment = (TextAnchor)5;
			((Component)obj2).GetComponent<LayoutElement>().flexibleWidth = 2f;
		}

		private static void ToggleMyPactPopup()
		{
			if ((UnityEngine.Object)(object)_myPactPopup == (UnityEngine.Object)null)
			{
				CreateMyPactPopup();
			}
			_myPactVisible = !_myPactVisible;
			_myPactPopup.SetActive(_myPactVisible);
			if (_myPactVisible)
			{
				RefreshMyPactInfo();
			}
		}

		private static void CreateMyPactPopup()
		{

			object obj = _goField?.GetValue(_popupInstance);
			GameObject val = obj as GameObject;
			if (!((UnityEngine.Object)(object)val == (UnityEngine.Object)null))
			{
				_myPactPopup = new GameObject("MyPactPopup", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(VerticalLayoutGroup)
				});
				_myPactPopup.transform.SetParent(val.transform, false);
				RectTransform component = _myPactPopup.GetComponent<RectTransform>();
				component.anchorMin = new Vector2(0.5f, 0.5f);
				component.anchorMax = new Vector2(0.5f, 0.5f);
				component.pivot = new Vector2(0.5f, 0.5f);
				component.sizeDelta = new Vector2(480f, 480f);
				component.anchoredPosition = new Vector2(200f, 0f);
				((Graphic)_myPactPopup.GetComponent<Image>()).color = new Color(0.1f, 0.14f, 0.1f, 0.98f);
				VerticalLayoutGroup component2 = _myPactPopup.GetComponent<VerticalLayoutGroup>();
				((LayoutGroup)component2).padding = new RectOffset(10, 10, 8, 8);
				((HorizontalOrVerticalLayoutGroup)component2).spacing = 4f;
				((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
				GameObject obj2 = CreateHorizontalRow(_myPactPopup.transform, "MP_Header");
				obj2.GetComponent<LayoutElement>().minHeight = 30f;
				((Graphic)CreateLabel(obj2.transform, "MP_Title", "My Pact", 14, (FontStyle)1)).color = new Color(0.5f, 1f, 0.5f);
				((Component)CreateButton(obj2.transform, "MP_Close", "X", delegate
				{
					_myPactVisible = false;
					_myPactPopup.SetActive(false);
				})).GetComponent<LayoutElement>().preferredWidth = 30f;
				GameObject val2 = new GameObject("MP_ScrollArea", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(ScrollRect),
					typeof(LayoutElement)
				});
				val2.transform.SetParent(_myPactPopup.transform, false);
				LayoutElement component3 = val2.GetComponent<LayoutElement>();
				component3.flexibleHeight = 1f;
				component3.minHeight = 300f;
				((Graphic)val2.GetComponent<Image>()).color = new Color(0.08f, 0.1f, 0.08f, 0.9f);
				GameObject val3 = new GameObject("Viewport", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Mask)
				});
				val3.transform.SetParent(val2.transform, false);
				RectTransform component4 = val3.GetComponent<RectTransform>();
				component4.anchorMin = Vector2.zero;
				component4.anchorMax = Vector2.one;
				component4.offsetMin = new Vector2(2f, 2f);
				component4.offsetMax = new Vector2(-2f, -2f);
				((Graphic)val3.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
				val3.GetComponent<Mask>().showMaskGraphic = false;
				GameObject val4 = new GameObject("Content", new Type[3]
				{
					typeof(RectTransform),
					typeof(VerticalLayoutGroup),
					typeof(ContentSizeFitter)
				});
				val4.transform.SetParent(val3.transform, false);
				RectTransform component5 = val4.GetComponent<RectTransform>();
				component5.anchorMin = new Vector2(0f, 1f);
				component5.anchorMax = new Vector2(1f, 1f);
				component5.pivot = new Vector2(0.5f, 1f);
				component5.sizeDelta = new Vector2(0f, 0f);
				val4.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
				VerticalLayoutGroup component6 = val4.GetComponent<VerticalLayoutGroup>();
				((HorizontalOrVerticalLayoutGroup)component6).spacing = 3f;
				((HorizontalOrVerticalLayoutGroup)component6).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component6).childForceExpandHeight = false;
				((LayoutGroup)component6).padding = new RectOffset(4, 4, 4, 4);
				_myPactContent = val4.transform;
				ScrollRect component7 = val2.GetComponent<ScrollRect>();
				component7.viewport = component4;
				component7.content = component5;
				component7.horizontal = false;
				component7.vertical = true;
				component7.scrollSensitivity = 30f;
				component7.movementType = (ScrollRect.MovementType)2;
				_myPactPopup.SetActive(false);
			}
		}

		private static void RefreshMyPactInfo()
		{

			if ((UnityEngine.Object)(object)_myPactContent == (UnityEngine.Object)null)
			{
				return;
			}
			for (int num = _myPactContent.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_myPactContent.GetChild(num)).gameObject);
			}
			AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == 6);
			if (alliancePact == null && SaveData.PlayerJoinedPactIndex >= 0)
			{
				alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
			}
			if (alliancePact == null)
			{
				((Graphic)CreateLabel(_myPactContent, "NoInfo", "No pact yet. Join or create one.", 12, (FontStyle)2)).color = new Color(0.6f, 0.6f, 0.55f);
				return;
			}
			Text obj = CreateLabel(_myPactContent, "PactName", alliancePact.DisplayName, 13, (FontStyle)1);
			((Graphic)obj).color = alliancePact.SharedColor;
			((Component)obj).GetComponent<LayoutElement>().minHeight = 22f;
			int num2 = alliancePact.MemberIds.Count + ((alliancePact.LeaderGangId >= 0) ? 1 : 0);
			Text obj2 = CreateLabel(_myPactContent, "MemberCount", $"Members: {num2}", 11, (FontStyle)0);
			((Graphic)obj2).color = new Color(0.8f, 0.8f, 0.75f);
			((Component)obj2).GetComponent<LayoutElement>().minHeight = 18f;
			GameObject val = new GameObject("Divider", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_myPactContent, false);
			val.GetComponent<LayoutElement>().minHeight = 2f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.3f, 0.5f, 0.3f, 0.5f);
			int num3 = 0;
			List<int> list = new List<int>();
			if (alliancePact.LeaderGangId >= 0)
			{
				list.Add(alliancePact.LeaderGangId);
			}
			list.AddRange(alliancePact.MemberIds);
			foreach (int item in list)
			{
				PlayerInfo val2 = null;
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer != null && humanPlayer.PID.id == item)
				{
					val2 = humanPlayer;
				}
				else
				{
					foreach (PlayerInfo trackedGang in TrackedGangs)
					{
						if (trackedGang.PID.id == item)
						{
							val2 = trackedGang;
							break;
						}
					}
				}
				object obj3;
				if (val2 == null)
				{
					obj3 = null;
				}
				else
				{
					PlayerSocial social = val2.social;
					obj3 = ((social != null) ? social.PlayerGroupName : null);
				}
				if (obj3 == null)
				{
					obj3 = $"Gang #{item}";
				}
				string text = (string)obj3;
				int? obj4;
				if (val2 == null)
				{
					obj4 = null;
				}
				else
				{
					PlayerCrew crew = val2.crew;
					obj4 = ((crew != null) ? new int?(crew.LivingCrewCount) : ((int?)null));
				}
				int? num4 = obj4;
				int valueOrDefault = num4.GetValueOrDefault();
				int num5 = ((val2 != null) ? CalculateGangPower(val2) : 0);
				int num6 = 0;
				try
				{
					if (val2 != null)
					{
						num6 = (int)val2.finances.GetMoneyTotal();
					}
				}
				catch
				{
				}
				int num7 = (int)((float)num5 * 0.05f * 10f);
				num3 += num7;
				GameObject val3 = new GameObject("MP_Entry", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(HorizontalLayoutGroup),
					typeof(LayoutElement)
				});
				val3.transform.SetParent(_myPactContent, false);
				val3.GetComponent<LayoutElement>().minHeight = 48f;
				((Graphic)val3.GetComponent<Image>()).color = new Color(0.12f, 0.16f, 0.12f, 0.9f);
				HorizontalLayoutGroup component = val3.GetComponent<HorizontalLayoutGroup>();
				((LayoutGroup)component).padding = new RectOffset(6, 6, 3, 3);
				((HorizontalOrVerticalLayoutGroup)component).spacing = 4f;
				((HorizontalOrVerticalLayoutGroup)component).childForceExpandWidth = false;
				((HorizontalOrVerticalLayoutGroup)component).childForceExpandHeight = true;
				GameObject val4 = new GameObject("Swatch", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(LayoutElement)
				});
				val4.transform.SetParent(val3.transform, false);
				val4.GetComponent<LayoutElement>().preferredWidth = 6f;
				((Graphic)val4.GetComponent<Image>()).color = alliancePact.SharedColor;
				GameObject val5 = new GameObject("Info", new Type[3]
				{
					typeof(RectTransform),
					typeof(VerticalLayoutGroup),
					typeof(LayoutElement)
				});
				val5.transform.SetParent(val3.transform, false);
				val5.GetComponent<LayoutElement>().flexibleWidth = 1f;
				VerticalLayoutGroup component2 = val5.GetComponent<VerticalLayoutGroup>();
				((HorizontalOrVerticalLayoutGroup)component2).spacing = 1f;
				((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
				Text obj6 = CreateLabel(val5.transform, "Name", text, 11, (FontStyle)1);
				((Graphic)obj6).color = new Color(0.9f, 0.95f, 0.85f);
				((Component)obj6).GetComponent<LayoutElement>().minHeight = 16f;
				Text obj7 = CreateLabel(val5.transform, "Details", $"Crew: {valueOrDefault} | Power: {num5} | Cash: ${num6}", 9, (FontStyle)0);
				((Graphic)obj7).color = new Color(0.65f, 0.7f, 0.6f);
				((Component)obj7).GetComponent<LayoutElement>().minHeight = 14f;
				GameObject val6 = new GameObject("RightCol", new Type[3]
				{
					typeof(RectTransform),
					typeof(VerticalLayoutGroup),
					typeof(LayoutElement)
				});
				val6.transform.SetParent(val3.transform, false);
				val6.GetComponent<LayoutElement>().preferredWidth = 90f;
				VerticalLayoutGroup component3 = val6.GetComponent<VerticalLayoutGroup>();
				((HorizontalOrVerticalLayoutGroup)component3).spacing = 2f;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
				Text obj8 = CreateLabel(val6.transform, "Earnings", $"${num7}/turn", 11, (FontStyle)1);
				((Graphic)obj8).color = new Color(0.4f, 0.85f, 0.4f);
				obj8.alignment = (TextAnchor)5;
				if (val2 == null)
				{
					continue;
				}
				PlayerID pID = val2.PID;
				if (!pID.IsHumanPlayer)
				{
					Button obj9 = CreateButton(val6.transform, "Give", "Give $500", null);
					((Graphic)((Component)obj9).GetComponent<Image>()).color = new Color(0.2f, 0.35f, 0.2f, 0.9f);
					((Component)obj9).GetComponent<LayoutElement>().minHeight = 18f;
					((Component)obj9).GetComponent<LayoutElement>().preferredHeight = 18f;
					((Component)obj9).GetComponentInChildren<Text>().fontSize = 9;
					PlayerInfo capturedGang = val2;
					string capturedName = text;
					((UnityEvent)obj9.onClick).AddListener((UnityAction)delegate
					{
						OnGiveCashToGang(capturedGang, capturedName);
					});
				}
			}
			GameObject val7 = new GameObject("Divider2", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(LayoutElement)
			});
			val7.transform.SetParent(_myPactContent, false);
			val7.GetComponent<LayoutElement>().minHeight = 2f;
			((Graphic)val7.GetComponent<Image>()).color = new Color(0.3f, 0.5f, 0.3f, 0.5f);
			GameObject obj10 = CreateHorizontalRow(_myPactContent, "MP_Total");
			obj10.GetComponent<LayoutElement>().minHeight = 28f;
			((Graphic)CreateLabel(obj10.transform, "TotalLabel", "Total Pact Earnings:", 12, (FontStyle)1)).color = new Color(0.9f, 0.9f, 0.8f);
			Text obj11 = CreateLabel(obj10.transform, "TotalValue", $"${num3}/turn", 13, (FontStyle)1);
			((Graphic)obj11).color = new Color(0.3f, 0.9f, 0.3f);
			obj11.alignment = (TextAnchor)5;
			List<string> list2 = new List<string>();
			PlayerInfo humanPlayer2 = G.GetHumanPlayer();
			foreach (int item2 in list)
			{
				PlayerInfo val8 = null;
				if (humanPlayer2 != null && humanPlayer2.PID.id == item2)
				{
					val8 = humanPlayer2;
				}
				else
				{
					foreach (PlayerInfo trackedGang2 in TrackedGangs)
					{
						if (trackedGang2.PID.id == item2)
						{
							val8 = trackedGang2;
							break;
						}
					}
				}
				if (val8 != null)
				{
					PlayerSocial social2 = val8.social;
					if (((social2 != null) ? social2.PlayerGroupName : null) != null)
					{
						list2.Add(val8.social.PlayerGroupName);
					}
				}
			}
			List<string> list3 = new List<string>();
			foreach (string grapevineEvent in SaveData.GrapevineEvents)
			{
				foreach (string item3 in list2)
				{
					if (grapevineEvent.IndexOf(item3, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list3.Add(grapevineEvent);
						break;
					}
				}
				if (list3.Count >= 5)
				{
					break;
				}
			}
			if (list3.Count <= 0)
			{
				return;
			}
			GameObject val9 = new GameObject("NewsDiv", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(LayoutElement)
			});
			val9.transform.SetParent(_myPactContent, false);
			val9.GetComponent<LayoutElement>().minHeight = 2f;
			((Graphic)val9.GetComponent<Image>()).color = new Color(0.4f, 0.4f, 0.6f, 0.5f);
			Text obj12 = CreateLabel(_myPactContent, "NewsHeader", "Pact News", 12, (FontStyle)1);
			((Graphic)obj12).color = new Color(0.75f, 0.7f, 0.9f);
			((Component)obj12).GetComponent<LayoutElement>().minHeight = 20f;
			Color color = default(Color);
			foreach (string item4 in list3)
			{
				color = new Color(0.7f, 0.7f, 0.65f);
				if (item4.StartsWith("KILL:") || item4.StartsWith("DEATH:"))
				{
					color = new Color(0.9f, 0.4f, 0.3f);
				}
				else if (item4.StartsWith("WAR:"))
				{
					color = new Color(0.9f, 0.65f, 0.3f);
				}
				else if (item4.StartsWith("PACT:"))
				{
					color = new Color(0.4f, 0.85f, 0.4f);
				}
				else if (item4.StartsWith("FRONT:"))
				{
					color = new Color(0.5f, 0.7f, 0.95f);
				}
				string text2 = item4;
				int num8 = item4.IndexOf(':');
				if (num8 > 0 && num8 < 7)
				{
					text2 = item4.Substring(num8 + 2);
				}
				Text obj13 = CreateLabel(_myPactContent, "News", text2, 13, (FontStyle)0);
				((Graphic)obj13).color = color;
				((Component)obj13).GetComponent<LayoutElement>().minHeight = 22f;
			}
		}

		private static void OnGiveCashToGang(PlayerInfo gangPlayer, string gangName)
		{

			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || gangPlayer == null)
				{
					return;
				}
				if (humanPlayer.finances == null)
				{
					Debug.LogWarning("[GameplayTweaks] Human player finances is null");
					return;
				}
				if (gangPlayer.finances == null)
				{
					Debug.LogWarning(("[GameplayTweaks] Gang " + gangName + " finances is null - trying safehouse inventory"));
					try
					{
						PlayerTerritory territory = gangPlayer.territory;
						if (territory == null)
						{
							return;
						}
						EntityID safehouse = territory.Safehouse;
						if (!safehouse.IsValid)
						{
							return;
						}
						InventoryModule inventory = ModulesUtil.GetInventory(EntityIDExtensions.FindEntity(safehouse));
						if (inventory != null)
						{
							int num = 500;
							if (!humanPlayer.finances.CanChangeMoneyOnSafehouse(new Price((Fixnum)(-num))))
							{
								Debug.Log("[GameplayTweaks] Can't afford to give $500");
								return;
							}
							humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-num)), (MoneyReason)1);
							inventory.ForceAddResourcesRegardlessOfSpace(new Label("cash"), num);
							LogGrapevine($"PACT: Your outfit donated ${num} to {gangName}");
							RefreshMyPactInfo();
							Debug.Log($"[GameplayTweaks] Added ${num} to {gangName} safehouse inventory");
						}
						return;
					}
					catch (Exception arg)
					{
						Debug.LogError($"[GameplayTweaks] Safehouse fallback failed: {arg}");
						return;
					}
				}
				int num2 = 500;
				if (!humanPlayer.finances.CanChangeMoneyOnSafehouse(new Price((Fixnum)(-num2))))
				{
					Debug.Log("[GameplayTweaks] Can't afford to give $500");
					return;
				}
				humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-num2)), (MoneyReason)1);
				gangPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(num2)), (MoneyReason)1);
				LogGrapevine($"PACT: Your outfit donated ${num2} to {gangName}");
				RefreshMyPactInfo();
			}
			catch (Exception arg2)
			{
				Debug.LogError($"[GameplayTweaks] Give cash failed: {arg2}");
			}
		}

		private static void ToggleGangPactsPopup()
		{
			if (G.GetHumanPlayer() != null)
			{
				if ((UnityEngine.Object)(object)_gangPactsPopup == (UnityEngine.Object)null)
				{
					CreateGangPactsPopup();
				}
				_gangPactsVisible = !_gangPactsVisible;
				_gangPactsPopup.SetActive(_gangPactsVisible);
				if (_gangPactsVisible)
				{
					RefreshGangTracker();
					RefreshGangPactsList();
				}
			}
		}

		private static void CreateGangPactsPopup()
		{

			object obj = _goField?.GetValue(_popupInstance);
			GameObject val = obj as GameObject;
			if ((UnityEngine.Object)(object)val == (UnityEngine.Object)null)
			{
				return;
			}
			_gangPactsPopup = new GameObject("GangPactsPopup", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup)
			});
			_gangPactsPopup.transform.SetParent(val.transform, false);
			RectTransform component = _gangPactsPopup.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0.5f, 0.5f);
			component.anchorMax = new Vector2(0.5f, 0.5f);
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(460f, 680f);
			component.anchoredPosition = new Vector2(-200f, 0f);
			((Graphic)_gangPactsPopup.GetComponent<Image>()).color = new Color(0.12f, 0.08f, 0.06f, 0.98f);
			VerticalLayoutGroup component2 = _gangPactsPopup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component2).padding = new RectOffset(8, 8, 8, 8);
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
			GameObject obj2 = CreateHorizontalRow(_gangPactsPopup.transform, "GP_Header");
			obj2.GetComponent<LayoutElement>().minHeight = 30f;
			CreateLabel(obj2.transform, "GP_Title", "Gang Pacts - Diplomacy", 14, (FontStyle)1);
			((Component)CreateButton(obj2.transform, "GP_Close", "X", delegate
			{
				_gangPactsVisible = false;
				_gangPactsPopup.SetActive(false);
			})).GetComponent<LayoutElement>().preferredWidth = 30f;
			GameObject obj3 = CreateHorizontalRow(_gangPactsPopup.transform, "GP_TabRow");
			obj3.GetComponent<LayoutElement>().minHeight = 28f;
			((Graphic)((Component)CreateButton(obj3.transform, "GP_MainTab", "Main", delegate
			{
				_pactEditorMode = false;
				RefreshGangPactsList();
			})).GetComponent<Image>()).color = new Color(0.25f, 0.2f, 0.15f, 0.95f);
			((Graphic)((Component)CreateButton(obj3.transform, "GP_EditorTab", "Editor", delegate
			{
				_pactEditorMode = true;
				RefreshGangPactsList();
			})).GetComponent<Image>()).color = new Color(0.15f, 0.2f, 0.25f, 0.95f);
			_pactTabLabel = CreateLabel(obj3.transform, "GP_TabLabel", "Main", 10, (FontStyle)2);
			_pactTabLabel.alignment = (TextAnchor)4;
			((Component)_pactTabLabel).GetComponent<LayoutElement>().preferredWidth = 80f;
			CreateLabel(_gangPactsPopup.transform, "GP_EditorLabel", "Alliance Slots", 12, (FontStyle)1);
			for (int num = 0; num < 7; num++)
			{
				CreatePactSlotRow(num);
			}
			GameObject toggleRow = CreateHorizontalRow(_gangPactsPopup.transform, "GP_ToggleRow");
			toggleRow.GetComponent<LayoutElement>().minHeight = 22f;
			((Graphic)CreateLabel(toggleRow.transform, "GP_ToggleLabel", "Never Accept Pact Invites:", 10, (FontStyle)0)).color = new Color(0.7f, 0.7f, 0.65f);
			Button obj4 = CreateButton(toggleRow.transform, "GP_ToggleBtn", SaveData.NeverAcceptPacts ? "ON" : "OFF", delegate
			{
				SaveData.NeverAcceptPacts = !SaveData.NeverAcceptPacts;
				Transform obj5 = toggleRow.transform.Find("GP_ToggleBtn");
				Text val5 = ((obj5 != null) ? ((Component)obj5).GetComponentInChildren<Text>() : null);
				if ((UnityEngine.Object)(object)val5 != (UnityEngine.Object)null)
				{
					val5.text = (SaveData.NeverAcceptPacts ? "ON" : "OFF");
				}
			});
			((Component)obj4).GetComponent<LayoutElement>().preferredWidth = 45f;
			((Graphic)((Component)obj4).GetComponent<Image>()).color = new Color(0.3f, 0.25f, 0.2f, 0.95f);
			_selectedPactLabel = CreateLabel(_gangPactsPopup.transform, "GP_SelectedPact", "Select a pact slot above, then invite gangs below", 10, (FontStyle)2);
			((Graphic)_selectedPactLabel).color = new Color(0.7f, 0.7f, 0.6f);
			_selectedPactLabel.alignment = (TextAnchor)4;
			_gangPactsStatusText = CreateLabel(_gangPactsPopup.transform, "GP_Status", "", 10, (FontStyle)0);
			_gangPactsStatusText.alignment = (TextAnchor)4;
			GameObject val2 = new GameObject("GP_ScrollArea", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(ScrollRect),
				typeof(LayoutElement)
			});
			val2.transform.SetParent(_gangPactsPopup.transform, false);
			LayoutElement component3 = val2.GetComponent<LayoutElement>();
			component3.minHeight = 240f;
			component3.flexibleHeight = 1f;
			((Graphic)val2.GetComponent<Image>()).color = new Color(0.08f, 0.06f, 0.04f, 0.9f);
			GameObject val3 = new GameObject("Viewport", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask)
			});
			val3.transform.SetParent(val2.transform, false);
			RectTransform component4 = val3.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = new Vector2(2f, 2f);
			component4.offsetMax = new Vector2(-2f, -2f);
			((Graphic)val3.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
			val3.GetComponent<Mask>().showMaskGraphic = false;
			GameObject val4 = new GameObject("Content", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter)
			});
			val4.transform.SetParent(val3.transform, false);
			RectTransform component5 = val4.GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0f, 1f);
			component5.anchorMax = new Vector2(1f, 1f);
			component5.pivot = new Vector2(0.5f, 1f);
			component5.sizeDelta = new Vector2(0f, 0f);
			val4.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
			VerticalLayoutGroup component6 = val4.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component6).spacing = 3f;
			((HorizontalOrVerticalLayoutGroup)component6).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component6).childForceExpandHeight = false;
			((LayoutGroup)component6).padding = new RectOffset(4, 4, 4, 4);
			_gangPactsContent = val4.transform;
			_gangPactsScroll = val2.GetComponent<ScrollRect>();
			_gangPactsScroll.viewport = component4;
			_gangPactsScroll.content = component5;
			_gangPactsScroll.horizontal = false;
			_gangPactsScroll.vertical = true;
			_gangPactsScroll.scrollSensitivity = 30f;
			_gangPactsScroll.movementType = (ScrollRect.MovementType)2;
			_gangPactsPopup.SetActive(false);
		}

		private static void CreatePactSlotRow(int slotIndex)
		{

			GameObject val = new GameObject($"GP_PactSlot_{slotIndex}", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_gangPactsPopup.transform, false);
			val.GetComponent<LayoutElement>().minHeight = 34f;
			val.GetComponent<LayoutElement>().preferredHeight = 34f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.16f, 0.14f, 0.1f, 0.9f);
			HorizontalLayoutGroup component = val.GetComponent<HorizontalLayoutGroup>();
			((LayoutGroup)component).padding = new RectOffset(4, 4, 3, 3);
			((HorizontalOrVerticalLayoutGroup)component).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandWidth = false;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandHeight = true;
			_pactSlotRows[slotIndex] = val;
			GameObject val2 = new GameObject("Swatch", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(LayoutElement)
			});
			val2.transform.SetParent(val.transform, false);
			val2.GetComponent<LayoutElement>().preferredWidth = 22f;
			val2.GetComponent<LayoutElement>().minWidth = 22f;
			_pactSlotColorSwatches[slotIndex] = val2.GetComponent<Image>();
			((Graphic)_pactSlotColorSwatches[slotIndex]).color = ModConstants.PACT_COLORS[slotIndex];
			Text val3 = CreateLabel(val.transform, "Name", ModConstants.PACT_COLOR_NAMES[slotIndex] + " - Empty", 11, (FontStyle)1);
			((Component)val3).GetComponent<LayoutElement>().flexibleWidth = 1f;
			_pactSlotNameTexts[slotIndex] = val3;
			Text val4 = CreateLabel(val.transform, "Members", "", 9, (FontStyle)0);
			((Component)val4).GetComponent<LayoutElement>().preferredWidth = 65f;
			val4.alignment = (TextAnchor)4;
			((Graphic)val4).color = new Color(0.7f, 0.7f, 0.65f);
			_pactSlotMemberTexts[slotIndex] = val4;
			int capturedIdx = slotIndex;
			Button val5 = CreateButton(val.transform, "Select", "Create", delegate
			{
				OnSelectPactSlot(capturedIdx);
			});
			((Component)val5).GetComponent<LayoutElement>().preferredWidth = 55f;
			((Component)val5).GetComponent<LayoutElement>().minWidth = 55f;
			_pactSlotSelectBtns[slotIndex] = val5;
			Button val6 = CreateButton(val.transform, "Delete", "X", delegate
			{
				OnDeletePactSlot(capturedIdx);
			});
			((Component)val6).GetComponent<LayoutElement>().preferredWidth = 26f;
			((Component)val6).GetComponent<LayoutElement>().minWidth = 26f;
			((Graphic)((Component)val6).GetComponent<Image>()).color = new Color(0.5f, 0.15f, 0.15f, 0.95f);
			_pactSlotDeleteBtns[slotIndex] = val6;
			((Component)val6).gameObject.SetActive(false);
		}

		private static void RefreshPactSlots()
		{

			int num = 6;
			bool flag = SaveData.PlayerJoinedPactIndex >= 0;
			SimTime now = G.GetNow();
			int i;
			for (i = 0; i < 7; i++)
			{
				if ((UnityEngine.Object)(object)_pactSlotRows[i] == (UnityEngine.Object)null)
				{
					continue;
				}
				AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == i);
				bool flag2 = _selectedPactSlot == i;
				if (i == num)
				{
					if (alliancePact != null)
					{
						bool flag3 = alliancePact.MemberIds.Count > 0 || alliancePact.LeaderGangId >= 0;
						_pactSlotNameTexts[i].text = (flag3 ? alliancePact.DisplayName : (alliancePact.DisplayName + " (Need 1 gang)"));
						int num2 = alliancePact.MemberIds.Count + ((alliancePact.LeaderGangId >= 0) ? 1 : 0);
						_pactSlotMemberTexts[i].text = $"{num2} gang(s)";
						((Component)_pactSlotSelectBtns[i]).GetComponentInChildren<Text>().text = (flag2 ? "Active" : "Select");
						((Component)_pactSlotDeleteBtns[i]).gameObject.SetActive(true);
					}
					else
					{
						_pactSlotNameTexts[i].text = "Your Pact - Not Created";
						_pactSlotMemberTexts[i].text = $"{24}T+{2}SC";
						((Component)_pactSlotSelectBtns[i]).GetComponentInChildren<Text>().text = "Create";
						((Component)_pactSlotDeleteBtns[i]).gameObject.SetActive(false);
					}
				}
				else if (alliancePact != null)
				{
					_pactSlotNameTexts[i].text = alliancePact.DisplayName;
					int num3 = alliancePact.MemberIds.Count + ((alliancePact.LeaderGangId >= 0) ? 1 : 0);
					_pactSlotMemberTexts[i].text = $"{num3} gang(s)";
					if (_pactEditorMode)
					{
						((Component)_pactSlotSelectBtns[i]).GetComponentInChildren<Text>().text = (flag2 ? "Active" : "Select");
					}
					else if (SaveData.PlayerJoinedPactIndex == i)
					{
						((Component)_pactSlotSelectBtns[i]).GetComponentInChildren<Text>().text = "Joined";
					}
					else
					{
						SaveData.PactJoinCooldowns.TryGetValue(i, out var value);
						bool flag4 = ((value > 0) ? (now.days - value) : 9999) >= 180;
						((Component)_pactSlotSelectBtns[i]).GetComponentInChildren<Text>().text = (flag4 ? "Join" : "Wait");
					}
					((Component)_pactSlotDeleteBtns[i]).gameObject.SetActive(_pactEditorMode);
				}
				else
				{
					_pactSlotNameTexts[i].text = ModConstants.PACT_COLOR_NAMES[i] + " - Empty";
					_pactSlotMemberTexts[i].text = "";
					((Component)_pactSlotSelectBtns[i]).GetComponentInChildren<Text>().text = (_pactEditorMode ? "Create" : "--");
					((Component)_pactSlotDeleteBtns[i]).gameObject.SetActive(false);
				}
				bool flag5 = flag2 || (SaveData.PlayerJoinedPactIndex == i && !_pactEditorMode);
				((Graphic)_pactSlotRows[i].GetComponent<Image>()).color = (flag5 ? new Color(0.25f, 0.2f, 0.15f, 0.95f) : new Color(0.16f, 0.14f, 0.1f, 0.9f));
				((Graphic)_pactSlotColorSwatches[i]).color = ModConstants.PACT_COLORS[i];
			}
			if (_pactEditorMode && _selectedPactSlot >= 0)
			{
				string text = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == _selectedPactSlot)?.DisplayName ?? (ModConstants.PACT_COLOR_NAMES[_selectedPactSlot] + " Alliance");
				_selectedPactLabel.text = "Editing: " + text;
				((Graphic)_selectedPactLabel).color = ModConstants.PACT_COLORS[_selectedPactSlot];
			}
			else if (flag && SaveData.PlayerPactId >= 0 && SaveData.PlayerJoinedPactIndex == 6)
			{
				AlliancePact alliancePact2 = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
				_selectedPactLabel.text = "Leader of: " + (alliancePact2?.DisplayName ?? "Your Pact");
				((Graphic)_selectedPactLabel).color = ModConstants.PACT_COLORS[SaveData.PlayerJoinedPactIndex];
			}
			else if (flag)
			{
				AlliancePact alliancePact3 = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
				_selectedPactLabel.text = "Member of: " + (alliancePact3?.DisplayName ?? "Unknown Pact");
				((Graphic)_selectedPactLabel).color = ModConstants.PACT_COLORS[SaveData.PlayerJoinedPactIndex];
			}
			else
			{
				_selectedPactLabel.text = (_pactEditorMode ? "Select a pact slot to edit" : "Join an AI pact or create your own");
				((Graphic)_selectedPactLabel).color = new Color(0.7f, 0.7f, 0.6f);
			}
		}

		private static void OnSelectPactSlot(int slotIndex)
		{

			int playerSlot = 6;
			AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == slotIndex);
			if (_pactEditorMode)
			{
				if (alliancePact == null && slotIndex != playerSlot)
				{
					int num = SaveData.NextPactId++;
					AlliancePact alliancePact2 = new AlliancePact
					{
						PactId = $"pact_{num}",
						PactName = ModConstants.PACT_COLOR_NAMES[slotIndex] + " Alliance",
						ColorIndex = slotIndex,
						LeaderGangId = -1,
						MemberIds = new List<int>(),
						SharedColor = ModConstants.PACT_COLORS[slotIndex],
						Formed = G.GetNow(),
						IsPending = true
					};
					SaveData.Pacts.Add(alliancePact2);
					Debug.Log(("[GameplayTweaks] Created pact slot: " + alliancePact2.PactName));
				}
				else if (alliancePact == null && slotIndex == playerSlot && !TryCreatePlayerPact())
				{
					return;
				}
				_selectedPactSlot = ((_selectedPactSlot == slotIndex) ? (-1) : slotIndex);
			}
			else if (slotIndex == playerSlot)
			{
				if (alliancePact == null && !TryCreatePlayerPact())
				{
					return;
				}
				_selectedPactSlot = ((_selectedPactSlot == slotIndex) ? (-1) : slotIndex);
			}
			else if (alliancePact != null)
			{
				if (SaveData.PlayerJoinedPactIndex == slotIndex)
				{
					return;
				}
				SimTime now = G.GetNow();
				SaveData.PactJoinCooldowns.TryGetValue(slotIndex, out var value);
				int num2 = ((value > 0) ? (now.days - value) : 9999);
				if (num2 < 180)
				{
					int num3 = 180 - num2;
					if ((UnityEngine.Object)(object)_selectedPactLabel != (UnityEngine.Object)null)
					{
						_selectedPactLabel.text = $"Must wait {num3} more days before asking {alliancePact.DisplayName}!";
						((Graphic)_selectedPactLabel).color = new Color(0.9f, 0.3f, 0.3f);
					}
					return;
				}
				SaveData.PactJoinCooldowns[slotIndex] = now.days;
				float num4 = CalculateJoinAcceptance(alliancePact);
				if (SharedRng.NextDouble() < (double)num4)
				{
					if (SaveData.PlayerJoinedPactIndex >= 0)
					{
						LeaveCurrentAIPact();
					}
					AlliancePact alliancePact3 = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == playerSlot);
					if (alliancePact3 != null)
					{
						LeavePlayerPactForWar(alliancePact3);
					}
					SaveData.PlayerJoinedPactIndex = slotIndex;
					PlayerInfo humanPlayer2 = G.GetHumanPlayer();
					if (humanPlayer2 != null && !alliancePact.MemberIds.Contains(humanPlayer2.PID.id))
					{
						alliancePact.MemberIds.Add(humanPlayer2.PID.id);
					}
					RefreshPactCache();
					TerritoryColorPatch.RefreshAllTerritoryColors();
					Debug.Log(("[GameplayTweaks] Player joined " + alliancePact.DisplayName + "!"));
					if ((UnityEngine.Object)(object)_selectedPactLabel != (UnityEngine.Object)null)
					{
						_selectedPactLabel.text = "Joined " + alliancePact.DisplayName + "!";
						((Graphic)_selectedPactLabel).color = new Color(0.3f, 0.9f, 0.3f);
					}
					if ((UnityEngine.Object)(object)_myPactBtnGo != (UnityEngine.Object)null)
					{
						_myPactBtnGo.SetActive(true);
					}
				}
				else
				{
					Debug.Log(("[GameplayTweaks] " + alliancePact.DisplayName + " rejected player!"));
					if ((UnityEngine.Object)(object)_selectedPactLabel != (UnityEngine.Object)null)
					{
						_selectedPactLabel.text = alliancePact.DisplayName + " rejected your request!";
						((Graphic)_selectedPactLabel).color = new Color(0.9f, 0.3f, 0.3f);
					}
				}
			}
			RefreshGangPactsList();
		}

		private static bool TryCreatePlayerPact()
		{

			int num = 6;
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return false;
			}
			int num2 = 0;
			int num3 = 0;
			try
			{
				PlayerTerritory territory = humanPlayer.territory;
				if (territory != null)
				{
					num2 = territory.OwnedNodeCount;
				}
				CrewAssignment crewForIndex = humanPlayer.crew.GetCrewForIndex(0);
				if (crewForIndex.IsValid)
				{
					Entity peep = crewForIndex.GetPeep();
					if (peep != null)
					{
						CrewModState crewStateOrNull = GetCrewStateOrNull(peep.Id);
						if (crewStateOrNull != null)
						{
							num3 = crewStateOrNull.StreetCreditLevel;
						}
					}
				}
			}
			catch
			{
			}
			if (num2 < 24 || num3 < 2)
			{
				if ((UnityEngine.Object)(object)_selectedPactLabel != (UnityEngine.Object)null)
				{
					_selectedPactLabel.text = $"Need {24} territories (have {num2}) and {2} SC levels (have {num3})!";
					((Graphic)_selectedPactLabel).color = new Color(0.9f, 0.3f, 0.3f);
				}
				return false;
			}
			if (SaveData.PlayerJoinedPactIndex >= 0)
			{
				AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
				if (alliancePact != null)
				{
					LeavePlayerPactForWar(alliancePact);
				}
				SaveData.PlayerJoinedPactIndex = -1;
			}
			int num4 = SaveData.NextPactId++;
			AlliancePact alliancePact2 = new AlliancePact
			{
				PactId = $"pact_{num4}",
				PactName = "Your Pact",
				ColorIndex = num,
				LeaderGangId = humanPlayer.PID.id,
				MemberIds = new List<int> { humanPlayer.PID.id },
				SharedColor = ModConstants.PACT_COLORS[num],
				Formed = G.GetNow(),
				IsPending = true
			};
			SaveData.Pacts.Add(alliancePact2);
			SaveData.PlayerPactId = num4;
			SaveData.PlayerJoinedPactIndex = num;
			alliancePact2.IsPending = false;
			RefreshPactCache();
			TerritoryColorPatch.RefreshAllTerritoryColors();
			LogGrapevine("PACT: Your outfit established a new alliance!");
			Debug.Log("[GameplayTweaks] Created player pact - auto-joined!");
			if ((UnityEngine.Object)(object)_selectedPactLabel != (UnityEngine.Object)null)
			{
				_selectedPactLabel.text = "Your pact has been established!";
				((Graphic)_selectedPactLabel).color = new Color(0.3f, 0.9f, 0.3f);
			}
			if ((UnityEngine.Object)(object)_myPactBtnGo != (UnityEngine.Object)null)
			{
				_myPactBtnGo.SetActive(true);
			}
			return true;
		}

		private static float CalculateJoinAcceptance(AlliancePact pact)
		{
			float num = 0.4f;
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer != null)
			{
				int num2 = CalculateGangPower(humanPlayer);
				if (num2 >= 100)
				{
					num += 0.2f;
				}
				else if (num2 >= 50)
				{
					num += 0.1f;
				}
				if (pact.MemberIds.Count + ((pact.LeaderGangId >= 0) ? 1 : 0) >= 3)
				{
					num -= 0.15f;
				}
			}
			return Mathf.Clamp01(num);
		}

		internal static void LeaveCurrentAIPact()
		{
			if (SaveData.PlayerJoinedPactIndex >= 0)
			{
				AlliancePact pact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
				if (pact != null)
				{
					PlayerInfo humanPlayer = G.GetHumanPlayer();
					if (humanPlayer != null)
					{
						pact.MemberIds.Remove(humanPlayer.PID.id);
					}
				}
			}
			SaveData.PlayerJoinedPactIndex = -1;
			Debug.Log("[GameplayTweaks] Player left AI pact.");
		}

		internal static void LeavePlayerPactForWar(AlliancePact playerPact)
		{
			Debug.Log(("[GameplayTweaks] Player left " + playerPact.DisplayName + " - gangs now hostile!"));
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer != null)
			{
				playerPact.MemberIds.Remove(humanPlayer.PID.id);
			}
			SaveData.PlayerJoinedPactIndex = -1;
		}

		private static void OnDeletePactSlot(int slotIndex)
		{
			AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == slotIndex);
			if (alliancePact != null)
			{
				SaveData.Pacts.Remove(alliancePact);
				if (SaveData.PlayerPactId >= 0 && alliancePact.PactId == $"pact_{SaveData.PlayerPactId}")
				{
					SaveData.PlayerPactId = -1;
				}
				if (_selectedPactSlot == slotIndex)
				{
					_selectedPactSlot = -1;
				}
				if (slotIndex == 6 && (UnityEngine.Object)(object)_myPactBtnGo != (UnityEngine.Object)null)
				{
					_myPactBtnGo.SetActive(false);
				}
				Debug.Log(("[GameplayTweaks] Deleted pact: " + alliancePact.DisplayName));
				TerritoryColorPatch.RefreshAllTerritoryColors();
				RefreshGangPactsList();
			}
		}

		private static void RemoveGangFromPact(PlayerInfo gang, int slotIndex)
		{
			AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == slotIndex);
			if (alliancePact == null)
			{
				return;
			}
			int id = gang.PID.id;
			if (alliancePact.LeaderGangId == id)
			{
				if (alliancePact.MemberIds.Count > 0)
				{
					alliancePact.LeaderGangId = alliancePact.MemberIds[0];
					alliancePact.MemberIds.RemoveAt(0);
				}
				else
				{
					alliancePact.LeaderGangId = -1;
					alliancePact.IsPending = true;
				}
			}
			else
			{
				alliancePact.MemberIds.Remove(id);
			}
			PlayerSocial social = gang.social;
			Debug.Log(("[GameplayTweaks] Removed " + ((social != null) ? social.PlayerGroupName : null) + " from " + alliancePact.DisplayName));
			TerritoryColorPatch.RefreshAllTerritoryColors();
			RefreshGangPactsList();
		}

		private static float CalculatePactAcceptance(PlayerInfo aiGang, PlayerInfo human)
		{
			float num = 0.3f;
			int num2 = CalculateGangPower(human);
			int num3 = CalculateGangPower(aiGang);
			num = ((num2 <= num3) ? (num - Math.Min(0.2f, (float)(num3 - num2) / 200f)) : (num + Math.Min(0.3f, (float)(num2 - num3) / 200f)));
			PlayerCrew crew = aiGang.crew;
			int num4 = ((crew != null) ? crew.LivingCrewCount : 0);
			if (num4 > 10)
			{
				num -= (float)(num4 - 10) * 0.01f;
			}
			if (num4 < 5)
			{
				num += 0.2f;
			}
			if (GetPactForPlayer(aiGang.PID) != null)
			{
				num = 0f;
			}
			return Mathf.Clamp01(num);
		}

		private static void RefreshGangPactsList()
		{
			if ((UnityEngine.Object)(object)_gangPactsContent == (UnityEngine.Object)null)
			{
				return;
			}
			RefreshPactSlots();
			_gangRenameRow = null;
			_gangRenameTarget = null;
			_gangRenameNameText = null;
			for (int num = _gangPactsContent.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_gangPactsContent.GetChild(num)).gameObject);
			}
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return;
			}
			int num2 = SaveData.Pacts.Count((AlliancePact p) => p.LeaderGangId >= 0 || p.MemberIds.Count > 0);
			_gangPactsStatusText.text = $"{num2} active alliance(s) on the map";
			if (TrackedGangs.Count == 0)
			{
				RefreshGangTracker();
			}
			List<PlayerInfo> list = (from p in TrackedGangs
				where p != null && p.crew != null && !p.crew.IsCrewDefeated && p.crew.LivingCrewCount > 0 && p.IsJustGang
				orderby CalculateGangPower(p) descending
				select p).ToList();
			if ((UnityEngine.Object)(object)_pactTabLabel != (UnityEngine.Object)null)
			{
				_pactTabLabel.text = (_pactEditorMode ? "Editor" : "Main");
			}
			if (_pactEditorMode)
			{
				CreateGangPactEntry(humanPlayer, humanPlayer);
			}
			foreach (PlayerInfo item in list)
			{
				CreateGangPactEntry(item, humanPlayer);
			}
		}

		private static void CreateGangPactEntry(PlayerInfo gang, PlayerInfo human)
		{

			PlayerSocial social = gang.social;
			string text = ((social != null) ? social.PlayerGroupName : null) ?? $"Gang #{gang.PID.id}";
			int num = CalculateGangPower(gang);
			PlayerCrew crew = gang.crew;
			int num2 = ((crew != null) ? crew.LivingCrewCount : 0);
			AlliancePact pactForPlayer = GetPactForPlayer(gang.PID);
			bool flag = pactForPlayer != null;
			bool flag2 = _selectedPactSlot >= 0 && pactForPlayer != null && pactForPlayer.ColorIndex == _selectedPactSlot;
			GameObject val = new GameObject("GP_Entry", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_gangPactsContent, false);
			val.GetComponent<LayoutElement>().minHeight = 45f;
			if (flag2)
			{
				((Graphic)val.GetComponent<Image>()).color = new Color(0.15f, 0.22f, 0.15f, 0.9f);
			}
			else if (flag)
			{
				((Graphic)val.GetComponent<Image>()).color = new Color(0.15f, 0.12f, 0.18f, 0.9f);
			}
			else
			{
				((Graphic)val.GetComponent<Image>()).color = new Color(0.16f, 0.14f, 0.1f, 0.9f);
			}
			HorizontalLayoutGroup obj = val.AddComponent<HorizontalLayoutGroup>();
			((LayoutGroup)obj).padding = new RectOffset(6, 6, 4, 4);
			((HorizontalOrVerticalLayoutGroup)obj).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)obj).childForceExpandWidth = false;
			((HorizontalOrVerticalLayoutGroup)obj).childForceExpandHeight = true;
			if (flag)
			{
				GameObject val2 = new GameObject("Swatch", new Type[3]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(LayoutElement)
				});
				val2.transform.SetParent(val.transform, false);
				val2.GetComponent<LayoutElement>().preferredWidth = 8f;
				((Graphic)val2.GetComponent<Image>()).color = pactForPlayer.SharedColor;
			}
			GameObject val3 = new GameObject("Info", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(LayoutElement)
			});
			val3.transform.SetParent(val.transform, false);
			val3.GetComponent<LayoutElement>().flexibleWidth = 1f;
			VerticalLayoutGroup component = val3.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component).spacing = 1f;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandHeight = false;
			Text val4 = CreateLabel(val3.transform, "Name", text, 12, (FontStyle)1);
			((Graphic)val4).color = (flag2 ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.95f, 0.9f, 0.75f));
			((Component)val4).GetComponent<LayoutElement>().minHeight = 16f;
			int num3 = 0;
			try
			{
				num3 = (int)gang.finances.GetMoneyTotal();
			}
			catch
			{
			}
			string text2 = (flag2 ? "(Member)" : (flag ? ("(" + pactForPlayer.DisplayName + ")") : ""));
			string text3 = $"Crew: {num2} | Power: {num} | ${num3} {text2}";
			Text obj3 = CreateLabel(val3.transform, "Details", text3, 9, (FontStyle)0);
			((Graphic)obj3).color = new Color(0.65f, 0.65f, 0.6f);
			((Component)obj3).GetComponent<LayoutElement>().minHeight = 14f;
			if (_pactEditorMode)
			{
				if (_selectedPactSlot < 0)
				{
					Text obj4 = CreateLabel(val.transform, "Hint", "---", 10, (FontStyle)2);
					((Graphic)obj4).color = new Color(0.5f, 0.5f, 0.45f);
					obj4.alignment = (TextAnchor)4;
					((Component)obj4).GetComponent<LayoutElement>().preferredWidth = 55f;
				}
				else if (flag2)
				{
					GameObject val5 = new GameObject("RmBtn", new Type[4]
					{
						typeof(RectTransform),
						typeof(Image),
						typeof(Button),
						typeof(LayoutElement)
					});
					val5.transform.SetParent(val.transform, false);
					val5.GetComponent<LayoutElement>().preferredWidth = 65f;
					val5.GetComponent<LayoutElement>().minHeight = 28f;
					((Graphic)val5.GetComponent<Image>()).color = new Color(0.5f, 0.15f, 0.15f, 0.95f);
					Button component2 = val5.GetComponent<Button>();
					((Selectable)component2).targetGraphic = (Graphic)(object)val5.GetComponent<Image>();
					PlayerInfo capturedGang = gang;
					int capturedSlot = _selectedPactSlot;
					((UnityEvent)component2.onClick).AddListener((UnityAction)delegate
					{
						RemoveGangFromPact(capturedGang, capturedSlot);
					});
					GameObject val6 = new GameObject("Text", new Type[2]
					{
						typeof(RectTransform),
						typeof(Text)
					});
					val6.transform.SetParent(val5.transform, false);
					RectTransform component3 = val6.GetComponent<RectTransform>();
					component3.anchorMin = Vector2.zero;
					component3.anchorMax = Vector2.one;
					component3.offsetMin = Vector2.zero;
					component3.offsetMax = Vector2.zero;
					Text component4 = val6.GetComponent<Text>();
					component4.text = "Remove";
					component4.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
					component4.fontSize = 10;
					((Graphic)component4).color = Color.white;
					component4.alignment = (TextAnchor)4;
					component4.fontStyle = (FontStyle)1;
				}
				else if (flag)
				{
					Text obj5 = CreateLabel(val.transform, "OtherPact", pactForPlayer.DisplayName, 10, (FontStyle)2);
					((Graphic)obj5).color = pactForPlayer.SharedColor;
					obj5.alignment = (TextAnchor)4;
					((Component)obj5).GetComponent<LayoutElement>().preferredWidth = 65f;
				}
				else
				{
					GameObject val7 = new GameObject("AddBtn", new Type[4]
					{
						typeof(RectTransform),
						typeof(Image),
						typeof(Button),
						typeof(LayoutElement)
					});
					val7.transform.SetParent(val.transform, false);
					val7.GetComponent<LayoutElement>().preferredWidth = 55f;
					val7.GetComponent<LayoutElement>().minHeight = 28f;
					((Graphic)val7.GetComponent<Image>()).color = new Color(0.2f, 0.35f, 0.5f, 0.95f);
					Button component5 = val7.GetComponent<Button>();
					((Selectable)component5).targetGraphic = (Graphic)(object)val7.GetComponent<Image>();
					PlayerInfo capturedGang2 = gang;
					int capturedSlot2 = _selectedPactSlot;
					((UnityEvent)component5.onClick).AddListener((UnityAction)delegate
					{
						RequestPactWithGang(capturedGang2, 1f, capturedSlot2);
					});
					GameObject val8 = new GameObject("Text", new Type[2]
					{
						typeof(RectTransform),
						typeof(Text)
					});
					val8.transform.SetParent(val7.transform, false);
					RectTransform component6 = val8.GetComponent<RectTransform>();
					component6.anchorMin = Vector2.zero;
					component6.anchorMax = Vector2.one;
					component6.offsetMin = Vector2.zero;
					component6.offsetMax = Vector2.zero;
					Text component7 = val8.GetComponent<Text>();
					component7.text = "Add";
					component7.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
					component7.fontSize = 11;
					((Graphic)component7).color = Color.white;
					component7.alignment = (TextAnchor)4;
					component7.fontStyle = (FontStyle)1;
				}
				GameObject val9 = new GameObject("RenameBtn", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(LayoutElement)
				});
				val9.transform.SetParent(val.transform, false);
				val9.GetComponent<LayoutElement>().preferredWidth = 55f;
				val9.GetComponent<LayoutElement>().minHeight = 28f;
				((Graphic)val9.GetComponent<Image>()).color = new Color(0.35f, 0.3f, 0.2f, 0.95f);
				Button component8 = val9.GetComponent<Button>();
				((Selectable)component8).targetGraphic = (Graphic)(object)val9.GetComponent<Image>();
				PlayerInfo capturedGangForRename = gang;
				Text capturedNameText = val4;
				((UnityEvent)component8.onClick).AddListener((UnityAction)delegate
				{
					ShowGangRenameInput(capturedGangForRename, capturedNameText);
				});
				GameObject val10 = new GameObject("Text", new Type[2]
				{
					typeof(RectTransform),
					typeof(Text)
				});
				val10.transform.SetParent(val9.transform, false);
				RectTransform component9 = val10.GetComponent<RectTransform>();
				component9.anchorMin = Vector2.zero;
				component9.anchorMax = Vector2.one;
				component9.offsetMin = Vector2.zero;
				component9.offsetMax = Vector2.zero;
				Text component10 = val10.GetComponent<Text>();
				component10.text = "Rename";
				component10.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				component10.fontSize = 10;
				((Graphic)component10).color = Color.white;
				component10.alignment = (TextAnchor)4;
				component10.fontStyle = (FontStyle)1;
			}
			else if (_selectedPactSlot < 0)
			{
				Text obj6 = CreateLabel(val.transform, "Hint", "---", 10, (FontStyle)2);
				((Graphic)obj6).color = new Color(0.5f, 0.5f, 0.45f);
				obj6.alignment = (TextAnchor)4;
				((Component)obj6).GetComponent<LayoutElement>().preferredWidth = 55f;
			}
			else if (flag2)
			{
				Text obj7 = CreateLabel(val.transform, "Member", "MEMBER", 11, (FontStyle)1);
				((Graphic)obj7).color = new Color(0.3f, 0.8f, 0.3f);
				obj7.alignment = (TextAnchor)4;
				((Component)obj7).GetComponent<LayoutElement>().preferredWidth = 65f;
			}
			else if (flag)
			{
				Text obj8 = CreateLabel(val.transform, "OtherPact", pactForPlayer.DisplayName, 10, (FontStyle)2);
				((Graphic)obj8).color = pactForPlayer.SharedColor;
				obj8.alignment = (TextAnchor)4;
				((Component)obj8).GetComponent<LayoutElement>().preferredWidth = 65f;
			}
			else
			{
				float num4 = CalculatePactAcceptance(gang, human);
				int num5 = Mathf.RoundToInt(num4 * 100f);
				Color color = ((num5 >= 50) ? new Color(0.3f, 0.8f, 0.3f) : ((num5 >= 25) ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.8f, 0.3f, 0.3f)));
				Text obj9 = CreateLabel(val.transform, "Pct", $"{num5}%", 12, (FontStyle)1);
				((Graphic)obj9).color = color;
				obj9.alignment = (TextAnchor)4;
				((Component)obj9).GetComponent<LayoutElement>().preferredWidth = 40f;
				GameObject val11 = new GameObject("InvBtn", new Type[4]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(Button),
					typeof(LayoutElement)
				});
				val11.transform.SetParent(val.transform, false);
				val11.GetComponent<LayoutElement>().preferredWidth = 55f;
				val11.GetComponent<LayoutElement>().minHeight = 28f;
				((Graphic)val11.GetComponent<Image>()).color = new Color(0.25f, 0.4f, 0.2f, 0.95f);
				Button component11 = val11.GetComponent<Button>();
				((Selectable)component11).targetGraphic = (Graphic)(object)val11.GetComponent<Image>();
				PlayerInfo capturedGang3 = gang;
				float capturedAcceptance = num4;
				int capturedSlot3 = _selectedPactSlot;
				((UnityEvent)component11.onClick).AddListener((UnityAction)delegate
				{
					RequestPactWithGang(capturedGang3, capturedAcceptance, capturedSlot3);
				});
				GameObject val12 = new GameObject("Text", new Type[2]
				{
					typeof(RectTransform),
					typeof(Text)
				});
				val12.transform.SetParent(val11.transform, false);
				RectTransform component12 = val12.GetComponent<RectTransform>();
				component12.anchorMin = Vector2.zero;
				component12.anchorMax = Vector2.one;
				component12.offsetMin = Vector2.zero;
				component12.offsetMax = Vector2.zero;
				Text component13 = val12.GetComponent<Text>();
				component13.text = "Invite";
				component13.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				component13.fontSize = 11;
				((Graphic)component13).color = Color.white;
				component13.alignment = (TextAnchor)4;
				component13.fontStyle = (FontStyle)1;
			}
		}

		private static void RequestPactWithGang(PlayerInfo gang, float acceptance, int slotIndex)
		{

			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return;
			}
			AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == slotIndex);
			if (alliancePact == null)
			{
				return;
			}
			bool flag = gang.PID == humanPlayer.PID;
			if (flag || SharedRng.NextDouble() < (double)acceptance)
			{
				if (flag)
				{
					if (SaveData.PlayerJoinedPactIndex >= 0 && SaveData.PlayerJoinedPactIndex != slotIndex)
					{
						Debug.Log("[GameplayTweaks] Player leaving old pact to join new one");
					}
					SaveData.PlayerJoinedPactIndex = slotIndex;
					if (!alliancePact.MemberIds.Contains(gang.PID.id))
					{
						alliancePact.MemberIds.Add(gang.PID.id);
					}
					alliancePact.IsPending = false;
					Debug.Log(("[GameplayTweaks] Human player joined " + alliancePact.DisplayName + " via editor!"));
					LogGrapevine("PACT: Your outfit joined " + alliancePact.DisplayName + "!");
					if ((UnityEngine.Object)(object)_myPactBtnGo != (UnityEngine.Object)null)
					{
						_myPactBtnGo.SetActive(true);
					}
					RefreshPactCache();
				}
				else
				{
					if (alliancePact.LeaderGangId < 0)
					{
						alliancePact.LeaderGangId = gang.PID.id;
					}
					else
					{
						alliancePact.MemberIds.Add(gang.PID.id);
					}
					alliancePact.IsPending = false;
					string[] obj = new string[5] { "[GameplayTweaks] ", null, null, null, null };
					PlayerSocial social = gang.social;
					obj[1] = ((social != null) ? social.PlayerGroupName : null);
					obj[2] = " joined ";
					obj[3] = alliancePact.DisplayName;
					obj[4] = "!";
					Debug.Log(string.Concat(obj));
				}
				TerritoryColorPatch.RefreshAllTerritoryColors();
			}
			else
			{
				string[] obj2 = new string[5] { "[GameplayTweaks] ", null, null, null, null };
				PlayerSocial social2 = gang.social;
				obj2[1] = ((social2 != null) ? social2.PlayerGroupName : null);
				obj2[2] = " declined invite to ";
				obj2[3] = alliancePact.DisplayName;
				obj2[4] = ".";
				Debug.Log(string.Concat(obj2));
			}
			RefreshGangPactsList();
		}

		private static void OnHireFamily()
		{
			ToggleFamilyHirePopup();
		}

		private static void ToggleFamilyHirePopup()
		{
			if (!((UnityEngine.Object)(object)_familyHirePopup == (UnityEngine.Object)null))
			{
				_familyHireVisible = !_familyHireVisible;
				_familyHirePopup.SetActive(_familyHireVisible);
				if (_familyHireVisible)
				{
					_filterEthnicity = "All";
					_filterTrait = "All";
					_filterMinAge = 0;
					_filterMaxAge = 100;
					RefreshFamilyHireList();
				}
			}
		}

		private static void CloseFamilyHirePopup()
		{
			_familyHireVisible = false;
			if ((UnityEngine.Object)(object)_familyHirePopup != (UnityEngine.Object)null)
			{
				_familyHirePopup.SetActive(false);
			}
		}

		private static void CycleEthnicityFilter()
		{
			_ethFilterIndex = (_ethFilterIndex + 1) % ETHNICITY_FILTERS.Length;
			_filterEthnicity = ETHNICITY_FILTERS[_ethFilterIndex];
			RefreshFamilyHireList();
		}

		private static void CycleTraitFilter()
		{
			_traitFilterIndex = (_traitFilterIndex + 1) % TRAIT_FILTERS.Length;
			_filterTrait = TRAIT_FILTERS[_traitFilterIndex];
			RefreshFamilyHireList();
		}

		private static void RefreshFamilyHireList()
		{

			if ((UnityEngine.Object)(object)_familyHireContent == (UnityEngine.Object)null)
			{
				return;
			}
			for (int num = _familyHireContent.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_familyHireContent.GetChild(num)).gameObject);
			}
			if (_selectedPeep == null)
			{
				_familyHireStatusText.text = "No crew member selected";
				return;
			}
			List<Entity> list = new List<Entity>();
			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew != null)
			{
				foreach (CrewAssignment item in humanCrew.GetLiving())
				{
					CrewAssignment current = item;
					Entity peep = current.GetPeep();
					if (peep == null)
					{
						continue;
					}
					foreach (Entity rel in FindAllRelatives(peep))
					{
						if (!list.Any((Entity r) => r.Id == rel.Id))
						{
							list.Add(rel);
						}
					}
				}
			}
			foreach (Entity rel2 in FindAllRelatives(_selectedPeep))
			{
				if (!list.Any((Entity r) => r.Id == rel2.Id))
				{
					list.Add(rel2);
				}
			}
			SimTime now = G.GetNow();
			_filteredRelatives = (from r in list.Where(delegate(Entity r)
				{

					PersonData person = r.data.person;
					SimTimeSpan age = person.GetAge(now);
					float yearsFloat = age.YearsFloat;
					if (yearsFloat < (float)_filterMinAge || yearsFloat > (float)_filterMaxAge)
					{
						return false;
					}
					if (_filterEthnicity != "All" && !person.eth.ToString().Equals(_filterEthnicity, StringComparison.OrdinalIgnoreCase))
					{
						return false;
					}
					if (_filterTrait != "All")
					{
						string value = _filterTrait.ToLower();
						bool flag = false;
						try
						{
							XP xp = r.data.agent.xp;
							if (xp != null)
							{
								FieldInfo field = ((object)xp).GetType().GetField("roles", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								if (field != null && field.GetValue(xp) is IEnumerable enumerable)
								{
									foreach (object item2 in enumerable)
									{
										if (item2.ToString().ToLower().Contains(value))
										{
											flag = true;
											break;
										}
									}
								}
								FieldInfo field2 = ((object)xp).GetType().GetField("traits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								if (field2 != null && field2.GetValue(xp) is IEnumerable enumerable2)
								{
									foreach (object item3 in enumerable2)
									{
										if (item3.ToString().ToLower().Contains(value))
										{
											flag = true;
											break;
										}
									}
								}
							}
							if (!flag && ((object)person).ToString().ToLower().Contains(value))
							{
								flag = true;
							}
						}
						catch
						{
							flag = true;
						}
						if (!flag)
						{
							return false;
						}
					}
					return true;
				})
				orderby r.data.person.FullName
				select r).ToList();
			string arg = $"Eth: {_filterEthnicity} | Trait: {_filterTrait} | Age: {_filterMinAge}-{_filterMaxAge}";
			_familyHireStatusText.text = $"{_filteredRelatives.Count} found | {arg}";
			Transform val = _familyHirePopup.transform.Find("FH_FilterRow1");
			if ((UnityEngine.Object)(object)val != (UnityEngine.Object)null)
			{
				Transform val2 = val.Find("FH_EthFilter");
				if ((UnityEngine.Object)(object)val2 != (UnityEngine.Object)null)
				{
					((Component)val2).GetComponentInChildren<Text>().text = "Eth: " + _filterEthnicity;
				}
				Transform val3 = val.Find("FH_TraitFilter");
				if ((UnityEngine.Object)(object)val3 != (UnityEngine.Object)null)
				{
					((Component)val3).GetComponentInChildren<Text>().text = "Trait: " + _filterTrait;
				}
			}
			Transform val4 = _familyHirePopup.transform.Find("FH_FilterRow2");
			if ((UnityEngine.Object)(object)val4 != (UnityEngine.Object)null)
			{
				Transform val5 = val4.Find("FH_AgeDown");
				if ((UnityEngine.Object)(object)val5 != (UnityEngine.Object)null)
				{
					((Component)val5).GetComponentInChildren<Text>().text = $"Min:{_filterMinAge}";
				}
				Transform val6 = val4.Find("FH_MaxUp");
				if ((UnityEngine.Object)(object)val6 != (UnityEngine.Object)null)
				{
					((Component)val6).GetComponentInChildren<Text>().text = $"Max:{_filterMaxAge}";
				}
			}
			foreach (Entity filteredRelative in _filteredRelatives)
			{
				CreateFamilyHireEntry(filteredRelative, now);
			}
		}

		private static void CreateFamilyHireEntry(Entity relative, SimTime now)
		{

			PersonData person = relative.data.person;
			SimTimeSpan age = person.GetAge(now);
			float yearsFloat = age.YearsFloat;
			string arg = person.eth.ToString();
			string arg2 = (((int)person.g == 2) ? "F" : "M");
			string text = "";
			try
			{
				XP xp = relative.data.agent.xp;
				if (xp != null)
				{
					string text2 = ((object)xp.GetCrewRole()).ToString();
					if (!string.IsNullOrEmpty(text2) && text2 != "None" && text2 != "0")
					{
						text = text2;
					}
				}
			}
			catch
			{
			}
			GameObject val = new GameObject("FH_Entry", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_familyHireContent, false);
			val.GetComponent<LayoutElement>().minHeight = 52f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.18f, 0.16f, 0.12f, 0.9f);
			HorizontalLayoutGroup component = val.GetComponent<HorizontalLayoutGroup>();
			((LayoutGroup)component).padding = new RectOffset(6, 4, 3, 3);
			((HorizontalOrVerticalLayoutGroup)component).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandWidth = false;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandHeight = true;
			GameObject val2 = new GameObject("Info", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(LayoutElement)
			});
			val2.transform.SetParent(val.transform, false);
			val2.GetComponent<LayoutElement>().flexibleWidth = 1f;
			VerticalLayoutGroup component2 = val2.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 1f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
			Text obj2 = CreateLabel(val2.transform, "Name", person.FullName, 12, (FontStyle)1);
			((Graphic)obj2).color = new Color(0.95f, 0.9f, 0.75f);
			((Component)obj2).GetComponent<LayoutElement>().minHeight = 16f;
			Text obj3 = CreateLabel(text: $"Age: {(int)yearsFloat} | {arg2} | {arg}", parent: val2.transform, name: "Details", size: 10, style: (FontStyle)0);
			((Graphic)obj3).color = new Color(0.7f, 0.7f, 0.65f);
			((Component)obj3).GetComponent<LayoutElement>().minHeight = 13f;
			Text obj4 = CreateLabel(text: (!string.IsNullOrEmpty(text)) ? ("Role: " + text) : "No role", parent: val2.transform, name: "Traits", size: 9, style: (FontStyle)2);
			((Graphic)obj4).color = ((!string.IsNullOrEmpty(text)) ? new Color(0.85f, 0.75f, 0.4f) : new Color(0.5f, 0.5f, 0.45f));
			((Component)obj4).GetComponent<LayoutElement>().minHeight = 12f;
			GameObject val3 = new GameObject("HireBtn", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Button),
				typeof(LayoutElement)
			});
			val3.transform.SetParent(val.transform, false);
			val3.GetComponent<LayoutElement>().preferredWidth = 50f;
			val3.GetComponent<LayoutElement>().minHeight = 40f;
			((Graphic)val3.GetComponent<Image>()).color = new Color(0.2f, 0.45f, 0.2f, 0.95f);
			Button component3 = val3.GetComponent<Button>();
			((Selectable)component3).targetGraphic = (Graphic)(object)val3.GetComponent<Image>();
			Entity capturedRelative = relative;
			((UnityEvent)component3.onClick).AddListener((UnityAction)delegate
			{
				HireSpecificRelative(capturedRelative);
			});
			GameObject val4 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val4.transform.SetParent(val3.transform, false);
			RectTransform component4 = val4.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = Vector2.zero;
			component4.offsetMax = Vector2.zero;
			Text component5 = val4.GetComponent<Text>();
			component5.text = "Hire";
			component5.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component5.fontSize = 12;
			((Graphic)component5).color = Color.white;
			component5.alignment = (TextAnchor)4;
			component5.fontStyle = (FontStyle)1;
		}

		private static void HireSpecificRelative(Entity relative)
		{

			if (relative == null)
			{
				return;
			}
			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null)
			{
				Debug.LogWarning("[GameplayTweaks] HireSpecificRelative: Could not get player crew");
				return;
			}
			if (!humanCrew.CanAddCrew(1))
			{
				Debug.Log("[GameplayTweaks] Crew is full, cannot hire more");
				return;
			}
			try
			{
				Entity val = _selectedPeep;
				if (val == null)
				{
					foreach (CrewAssignment item in humanCrew.GetLiving())
					{
						CrewAssignment current = item;
						Entity peep = current.GetPeep();
						if (peep == null)
						{
							continue;
						}
						RelationshipTracker rels = G.GetRels();
						RelationshipList val2 = ((rels != null) ? rels.GetListOrNull(peep.Id) : null);
						if (val2 != null)
						{
							foreach (Relationship datum in val2.data)
							{
								if (datum.to == relative.Id)
								{
									val = peep;
									break;
								}
							}
						}
						if (val != null)
						{
							break;
						}
					}
				}
				humanCrew.HireNewCrewMemberUnassigned(relative, val ?? _selectedPeep);
				Debug.Log(("[GameplayTweaks] Hired " + relative.data.person.FullName + " as new crew member"));
				RefreshFamilyHireList();
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Failed to hire relative: {arg}");
			}
		}

		private static void OnVacation()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null || orCreateCrewState.OnVacation || orCreateCrewState.VacationPending)
			{
				return;
			}
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer != null)
			{
				try
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-1500)), (MoneyReason)1);
				}
				catch
				{
					return;
				}
				orCreateCrewState.VacationPending = true;
				orCreateCrewState.VacationDuration = 7;
				orCreateCrewState.HappinessValue = 1f;
				orCreateCrewState.TurnsUnhappy = 0;
				Debug.Log(("[GameplayTweaks] " + _selectedPeep.data.person.FullName + " will go on vacation ($1500)"));
				RefreshHandlerUI();
			}
		}

		private static void OnGift()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			int num = 50;
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer != null)
			{
				try
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-num)), (MoneyReason)1);
					orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue + 0.2f);
					orCreateCrewState.TurnsUnhappy = 0;
				}
				catch
				{
				}
				RefreshHandlerUI();
			}
		}

		private static void OnToggleRename()
		{
			if ((UnityEngine.Object)(object)_renameRow == (UnityEngine.Object)null || _selectedPeep == null)
			{
				return;
			}
			bool flag = !_renameRow.activeSelf;
			_renameRow.SetActive(flag);
			if (!flag)
			{
				return;
			}
			try
			{
				_inputFirstName.text = _selectedPeep.data.person.first ?? "";
				_inputLastName.text = _selectedPeep.data.person.last ?? "";
			}
			catch
			{
			}
		}

		private static void OnSaveRename()
		{
			if (_selectedPeep == null || (UnityEngine.Object)(object)_inputFirstName == (UnityEngine.Object)null || (UnityEngine.Object)(object)_inputLastName == (UnityEngine.Object)null)
			{
				return;
			}
			string text = _inputFirstName.text?.Trim();
			string text2 = _inputLastName.text?.Trim();
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2))
			{
				return;
			}
			try
			{
				PersonData person = _selectedPeep.data.person;
				person.first = text;
				person.last = text2;
				Type type = ((object)person).GetType();
				string[] array = new string[4] { "_fullname", "_shortname", "_fullName", "_shortName" };
				foreach (string name in array)
				{
					FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
					if (field != null && field.FieldType == typeof(string))
					{
						field.SetValue(person, null);
					}
				}
				_renameRow.SetActive(false);
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Rename failed: {arg}");
			}
		}

		private static void OnAgeUp()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			try
			{
				PersonData person = _selectedPeep.data.person;
				string fullName = person.FullName;
				int days = person.born.days;
				person.born = new SimTime(days - 365);
				int num = 0;
				RelationshipTracker rels = G.GetRels();
				if (rels != null)
				{
					RelationshipList listOrNull = rels.GetListOrNull(_selectedPeep.Id);
					if (listOrNull != null)
					{
						foreach (Relationship datum in listOrNull.data)
						{
							if ((int)datum.type != 20 && (int)datum.type != 50 && (int)datum.type != 10 && (int)datum.type != 30 && (int)datum.type != 40 && (int)datum.type != 90)
							{
								continue;
							}
							Entity val = EntityIDExtensions.FindEntity(datum.to);
							if (val != null)
							{
								try
								{
									PersonData person2 = val.data.person;
									int days2 = person2.born.days;
									person2.born = new SimTime(days2 - 365);
									num++;
								}
								catch
								{
								}
							}
						}
					}
				}
				SimTimeSpan age = person.GetAge(G.GetNow());
				float yearsFloat = age.YearsFloat;
				string arg = ((num > 0) ? $" ({num} family members also aged)" : "");
				LogGrapevine($"CREW: {fullName}'s family aged by 1 year (now {(int)yearsFloat}){arg}");
				RefreshHandlerUI();
			}
			catch (Exception arg2)
			{
				Debug.LogError($"[GameplayTweaks] Age up failed: {arg2}");
			}
		}

		private static void OnXpMinus()
		{
			if (_selectedPeep == null)
			{
				return;
			}
			try
			{
				XP val = _selectedPeep.data.agent?.xp;
				if (val == null)
				{
					return;
				}
				int num = Math.Max(0, val.current - _xpAdjustAmount);
				val.current = num;
				Debug.Log($"[GameplayTweaks] Reduced XP by {_xpAdjustAmount} to {num}");
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] XP minus failed: {arg}");
			}
		}

		private static void OnXpPlus()
		{
			if (_selectedPeep == null)
			{
				return;
			}
			try
			{
				AgentComponent agent = _selectedPeep.components?.agent;
				if (agent != null)
				{
					agent.AddXP(_xpAdjustAmount);
					Debug.Log($"[GameplayTweaks] Added {_xpAdjustAmount} XP via AddXP");
				}
				else
				{
					XP val = _selectedPeep.data.agent?.xp;
					if (val != null)
					{
						val.current += _xpAdjustAmount;
						Debug.Log($"[GameplayTweaks] Added {_xpAdjustAmount} XP directly to {val.current}");
					}
				}
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] XP plus failed: {arg}");
			}
		}

		private static void OnXpCycleAmount()
		{
			if (_xpAdjustAmount == 100)
			{
				_xpAdjustAmount = 500;
			}
			else if (_xpAdjustAmount == 500)
			{
				_xpAdjustAmount = 1000;
			}
			else if (_xpAdjustAmount == 1000)
			{
				_xpAdjustAmount = 50;
			}
			else
			{
				_xpAdjustAmount = 100;
			}
			if ((UnityEngine.Object)(object)_btnXpMinus != (UnityEngine.Object)null)
			{
				((Component)_btnXpMinus).GetComponentInChildren<Text>().text = $"-{_xpAdjustAmount}";
			}
			if ((UnityEngine.Object)(object)_btnXpPlus != (UnityEngine.Object)null)
			{
				((Component)_btnXpPlus).GetComponentInChildren<Text>().text = $"+{_xpAdjustAmount}";
			}
			Debug.Log($"[GameplayTweaks] XP adjustment amount set to {_xpAdjustAmount}");
		}

		private static void OnPepTalk()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState != null)
			{
				int days = G.GetNow().days;
				if (days != _pepTalkLastTurnDay)
				{
					_pepTalkUsesThisTurn = 0;
					_pepTalkLastTurnDay = days;
				}
				if (_pepTalkUsesThisTurn < 3)
				{
					_pepTalkUsesThisTurn++;
					orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue + 0.05f);
					RefreshHandlerUI();
				}
			}
		}

		private static void OnToggleNickname()
		{
			if ((UnityEngine.Object)(object)_nicknameRow == (UnityEngine.Object)null || _selectedPeep == null)
			{
				return;
			}
			bool flag = !_nicknameRow.activeSelf;
			_nicknameRow.SetActive(flag);
			if (flag)
			{
				try
				{
					_inputNickname.text = _selectedPeep.data.person.nickname ?? "";
				}
				catch
				{
					_inputNickname.text = "";
				}
			}
			if (flag && (UnityEngine.Object)(object)_renameRow != (UnityEngine.Object)null && _renameRow.activeSelf)
			{
				_renameRow.SetActive(false);
			}
			RefreshHandlerUI();
		}

		private static void OnSaveNickname()
		{
			if (_selectedPeep == null || (UnityEngine.Object)(object)_inputNickname == (UnityEngine.Object)null)
			{
				return;
			}
			string text = _inputNickname.text?.Trim();
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			try
			{
				MethodInfo method = ((object)_selectedPeep.data.person).GetType().GetMethod("SetNickname", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					method.Invoke(_selectedPeep.data.person, new object[1] { text });
				}
				else
				{
					_selectedPeep.data.person.nickname = text;
				}
				_nicknameRow.SetActive(false);
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Save nickname failed: {arg}");
			}
		}

		private static void OnClearNickname()
		{
			if (_selectedPeep == null)
			{
				return;
			}
			try
			{
				MethodInfo method = ((object)_selectedPeep.data.person).GetType().GetMethod("SetNickname", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					method.Invoke(_selectedPeep.data.person, new object[1] { "" });
				}
				else
				{
					_selectedPeep.data.person.nickname = "";
				}
				Type type = ((object)_selectedPeep.data.person).GetType();
				string[] array = new string[4] { "_fullname", "_shortname", "_fullName", "_shortName" };
				foreach (string name in array)
				{
					FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
					if (field != null && field.FieldType == typeof(string))
					{
						field.SetValue(_selectedPeep.data.person, null);
					}
				}
				_nicknameRow.SetActive(false);
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Clear nickname failed: {arg}");
			}
		}

		private static void OnBribeMayor()
		{

			if (!_globalMayorBribeActive)
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				try
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-10000)), (MoneyReason)1);
					_globalMayorBribeActive = true;
					_globalMayorBribeExpireDay = G.GetNow().days + 180;
					Debug.Log("[GameplayTweaks] Mayor bribed! All crew benefit for 6 months.");
				}
				catch
				{
				}
				RefreshHandlerUI();
			}
		}

		private static void OnBribeJudge()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState != null && !orCreateCrewState.JudgeBribeActive)
			{
				int num = GetBribeCost(orCreateCrewState.WantedLevel) * 2;
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				try
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-num)), (MoneyReason)1);
					orCreateCrewState.JudgeBribeActive = true;
				}
				catch
				{
				}
				RefreshHandlerUI();
			}
		}

		private static void OnPayLawyer()
		{

			if (_selectedPeep == null || !JailSystem.IsInJail(_selectedPeep.Id) || GetOrCreateCrewState(_selectedPeep.Id) == null)
			{
				return;
			}
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer != null)
			{
				try
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-1000)), (MoneyReason)1);
					JailSystem.PayLawyerRetainer(_selectedPeep.Id, 1000);
					Debug.Log($"[GameplayTweaks] Paid ${1000} for lawyer for {_selectedPeep.data.person.FullName}");
				}
				catch (Exception arg)
				{
					Debug.LogError($"[GameplayTweaks] OnPayLawyer failed: {arg}");
				}
				RefreshHandlerUI();
			}
		}

		private static void OnOpenBoozeSell()
		{

			if (_selectedPeep == null || !JailSystem.IsInJail(_selectedPeep.Id))
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			int days = G.GetNow().days;
			if (orCreateCrewState.LastBoozeSellTurn != 0 && days - orCreateCrewState.LastBoozeSellTurn < 28)
			{
				return;
			}
			if ((UnityEngine.Object)(object)_boozeSellPopup == (UnityEngine.Object)null)
			{
				object obj = _goField?.GetValue(_popupInstance);
				GameObject val = obj as GameObject;
				if ((UnityEngine.Object)(object)val == (UnityEngine.Object)null)
				{
					return;
				}
				CreateBoozeSellPopup(val);
			}
			_boozeSellVisible = !_boozeSellVisible;
			_boozeSellPopup.SetActive(_boozeSellVisible);
			if (_boozeSellVisible)
			{
				PopulateBoozeSellPopup();
			}
		}

		private static void CreateBoozeSellPopup(GameObject parent)
		{

			_boozeSellPopup = new GameObject("BoozeSellPopup", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup)
			});
			_boozeSellPopup.transform.SetParent(parent.transform, false);
			RectTransform component = _boozeSellPopup.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0.5f, 0.5f);
			component.anchorMax = new Vector2(0.5f, 0.5f);
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(380f, 400f);
			component.anchoredPosition = new Vector2(200f, 0f);
			((Graphic)_boozeSellPopup.GetComponent<Image>()).color = new Color(0.1f, 0.08f, 0.12f, 0.98f);
			VerticalLayoutGroup component2 = _boozeSellPopup.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component2).padding = new RectOffset(8, 8, 8, 8);
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = false;
			GameObject obj = CreateHorizontalRow(_boozeSellPopup.transform, "BS_Header");
			obj.GetComponent<LayoutElement>().minHeight = 30f;
			CreateLabel(obj.transform, "BS_Title", "Sell Booze (3x Price)", 13, (FontStyle)1);
			((Component)CreateButton(obj.transform, "BS_Close", "X", delegate
			{
				_boozeSellVisible = false;
				_boozeSellPopup.SetActive(false);
			})).GetComponent<LayoutElement>().preferredWidth = 30f;
			_boozeSellStatusText = CreateLabel(_boozeSellPopup.transform, "BS_Status", "Scanning safehouse...", 10, (FontStyle)2);
			CreateButton(CreateHorizontalRow(_boozeSellPopup.transform, "BS_SellAllRow").transform, "BS_SellAll", "Sell All Booze", OnSellAllBooze);
			GameObject val = new GameObject("BS_ScrollArea", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(ScrollRect),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_boozeSellPopup.transform, false);
			LayoutElement component3 = val.GetComponent<LayoutElement>();
			component3.minHeight = 260f;
			component3.flexibleHeight = 1f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.06f, 0.05f, 0.08f, 0.9f);
			GameObject val2 = new GameObject("Viewport", new Type[3]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Mask)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component4 = val2.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = new Vector2(2f, 2f);
			component4.offsetMax = new Vector2(-2f, -2f);
			((Graphic)val2.GetComponent<Image>()).color = new Color(1f, 1f, 1f, 0.01f);
			val2.GetComponent<Mask>().showMaskGraphic = false;
			GameObject val3 = new GameObject("Content", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter)
			});
			val3.transform.SetParent(val2.transform, false);
			RectTransform component5 = val3.GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0f, 1f);
			component5.anchorMax = new Vector2(1f, 1f);
			component5.pivot = new Vector2(0.5f, 1f);
			component5.sizeDelta = new Vector2(0f, 0f);
			val3.GetComponent<ContentSizeFitter>().verticalFit = (ContentSizeFitter.FitMode)2;
			VerticalLayoutGroup component6 = val3.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component6).spacing = 3f;
			((HorizontalOrVerticalLayoutGroup)component6).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component6).childForceExpandHeight = false;
			((LayoutGroup)component6).padding = new RectOffset(4, 4, 4, 4);
			_boozeSellContent = val3.transform;
			ScrollRect component7 = val.GetComponent<ScrollRect>();
			component7.viewport = component4;
			component7.content = component5;
			component7.horizontal = false;
			component7.vertical = true;
			component7.scrollSensitivity = 30f;
			component7.movementType = (ScrollRect.MovementType)2;
			_boozeSellPopup.SetActive(false);
		}

		private static void PopulateBoozeSellPopup()
		{

			if ((UnityEngine.Object)(object)_boozeSellContent != (UnityEngine.Object)null)
			{
				for (int num = _boozeSellContent.childCount - 1; num >= 0; num--)
				{
					UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)_boozeSellContent.GetChild(num)).gameObject);
				}
			}
			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					_boozeSellStatusText.text = "No player found";
					return;
				}
				EntityID safehouse = humanPlayer.territory.Safehouse;
				if (safehouse.IsNotValid)
				{
					_boozeSellStatusText.text = "No safehouse found";
					return;
				}
				Entity val = EntityIDExtensions.FindEntity(safehouse);
				if (val == null)
				{
					_boozeSellStatusText.text = "Safehouse entity not found";
					return;
				}
				InventoryModule inventory = ModulesUtil.GetInventory(val);
				if (inventory == null)
				{
					_boozeSellStatusText.text = "No inventory found";
					return;
				}
				int num2 = 0;
				int num3 = 0;
				Label val2 = default(Label);
				foreach (KeyValuePair<string, int> boozePrice in _boozePrices)
				{
					try
					{
						val2 = new Label(boozePrice.Key);
						int num4 = ExtractFixnumAsInt(((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory).data.Get(val2).qty);
						if (num4 > 0)
						{
							int num5 = boozePrice.Value * 3;
							int num6 = num5 * num4;
							num2 += num6;
							num3++;
							Debug.Log($"[GameplayTweaks] Found {num4}x {boozePrice.Key} worth ${num6} (3x)");
							CreateBoozeSellEntry(boozePrice.Key, num4, num5, num6, val2);
						}
					}
					catch
					{
					}
				}
				if (num3 == 0)
				{
					_boozeSellStatusText.text = "No booze in safehouse";
				}
				else
				{
					_boozeSellStatusText.text = $"{num3} types found - Total value: ${num2}";
				}
			}
			catch (Exception arg)
			{
				_boozeSellStatusText.text = "Error reading inventory";
				Debug.LogError($"[GameplayTweaks] PopulateBoozeSellPopup: {arg}");
			}
		}

		private static void CreateBoozeSellEntry(string itemName, int qty, int unitPrice, int totalPrice, Label label)
		{

			GameObject val = new GameObject("BS_Entry_" + itemName, new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_boozeSellContent, false);
			((Graphic)val.GetComponent<Image>()).color = new Color(0.15f, 0.12f, 0.18f, 0.8f);
			LayoutElement component = val.GetComponent<LayoutElement>();
			component.minHeight = 40f;
			component.preferredHeight = 40f;
			HorizontalLayoutGroup component2 = val.GetComponent<HorizontalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = false;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = true;
			((LayoutGroup)component2).padding = new RectOffset(4, 4, 2, 2);
			GameObject val2 = new GameObject("Info", new Type[3]
			{
				typeof(RectTransform),
				typeof(VerticalLayoutGroup),
				typeof(LayoutElement)
			});
			val2.transform.SetParent(val.transform, false);
			val2.GetComponent<LayoutElement>().flexibleWidth = 1f;
			VerticalLayoutGroup component3 = val2.GetComponent<VerticalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
			((HorizontalOrVerticalLayoutGroup)component3).spacing = 1f;
			string str = itemName.Replace("-", " ");
			Text obj = CreateLabel(text: CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str), parent: val2.transform, name: "Name", size: 11, style: (FontStyle)1);
			((Component)obj).GetComponent<LayoutElement>().minHeight = 14f;
			((Component)obj).GetComponent<LayoutElement>().preferredHeight = 14f;
			Text obj2 = CreateLabel(val2.transform, "Detail", $"x{qty} @ ${unitPrice}/ea = ${totalPrice}", 9, (FontStyle)0);
			((Graphic)obj2).color = new Color(0.8f, 0.9f, 0.7f);
			((Component)obj2).GetComponent<LayoutElement>().minHeight = 12f;
			((Component)obj2).GetComponent<LayoutElement>().preferredHeight = 12f;
			GameObject val3 = new GameObject("SellBtn", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Button),
				typeof(LayoutElement)
			});
			val3.transform.SetParent(val.transform, false);
			val3.GetComponent<LayoutElement>().preferredWidth = 65f;
			val3.GetComponent<LayoutElement>().minWidth = 65f;
			((Graphic)val3.GetComponent<Image>()).color = new Color(0.2f, 0.5f, 0.2f, 0.95f);
			Button component4 = val3.GetComponent<Button>();
			((Selectable)component4).targetGraphic = (Graphic)(object)val3.GetComponent<Image>();
			string capturedKey = itemName;
			int capturedBasePrice = unitPrice / 3;
			((UnityEvent)component4.onClick).AddListener((UnityAction)delegate
			{
				SellBoozeItem(capturedKey, capturedBasePrice);
			});
			GameObject val4 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val4.transform.SetParent(val3.transform, false);
			RectTransform component5 = val4.GetComponent<RectTransform>();
			component5.anchorMin = Vector2.zero;
			component5.anchorMax = Vector2.one;
			component5.offsetMin = Vector2.zero;
			component5.offsetMax = Vector2.zero;
			Text component6 = val4.GetComponent<Text>();
			component6.text = $"Sell ${totalPrice}";
			component6.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component6.fontSize = 10;
			((Graphic)component6).color = Color.white;
			component6.alignment = (TextAnchor)4;
		}

		private static void SellBoozeItem(string itemKey, int basePrice)
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					Debug.LogWarning("[GameplayTweaks] No human player for booze sale");
					return;
				}
				EntityID safehouse = humanPlayer.territory.Safehouse;
				if (safehouse.IsNotValid)
				{
					Debug.LogWarning("[GameplayTweaks] Invalid safehouse for booze sale");
					return;
				}
				Entity val = EntityIDExtensions.FindEntity(safehouse);
				if (val == null)
				{
					Debug.LogWarning("[GameplayTweaks] Safehouse entity null");
					return;
				}
				InventoryModule inventory = ModulesUtil.GetInventory(val);
				if (inventory == null)
				{
					Debug.LogWarning("[GameplayTweaks] Safehouse inventory null");
					return;
				}
				Label val2 = default(Label);
				val2 = new Label(itemKey);
				int num = 0;
				try
				{
					num = ExtractFixnumAsInt(((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory).data.Get(val2).qty);
					Debug.Log($"[GameplayTweaks] Booze sale: {itemKey} has qty={num} in inventory");
				}
				catch (Exception ex)
				{
					Debug.LogError(("[GameplayTweaks] Could not check " + itemKey + " quantity: " + ex.Message));
					return;
				}
				if (num <= 0)
				{
					Debug.Log(("[GameplayTweaks] No " + itemKey + " to sell (qty=0)"));
					PopulateBoozeSellPopup();
					return;
				}
				int num2 = 0;
				try
				{
					int removed = inventory.TryRemoveResourcesUpToAll(val2, -num);
					num2 = Math.Abs(removed);
					Debug.Log($"[GameplayTweaks] TryRemoveResourcesUpToAll removed: {num2} (raw={removed})");
				}
				catch (Exception ex2)
				{
					Debug.LogWarning(("[GameplayTweaks] TryRemoveResourcesUpToAll failed: " + ex2.Message));
					try
					{
						MethodInfo method = ((object)inventory).GetType().GetMethod("Increment", BindingFlags.Instance | BindingFlags.Public);
						if (method != null)
						{
							method.Invoke(inventory, new object[2]
							{
								val2,
								-num
							});
							num2 = num;
							Debug.Log($"[GameplayTweaks] Used Increment(-{num}) via reflection");
						}
						else
						{
							num2 = num;
							Debug.Log($"[GameplayTweaks] Assuming {num} items removed");
						}
					}
					catch (Exception ex3)
					{
						Debug.LogError(("[GameplayTweaks] Booze removal fallback failed: " + ex3.Message));
					}
				}
				if (num2 <= 0)
				{
					Debug.Log(("[GameplayTweaks] Failed to remove " + itemKey + " from inventory"));
					PopulateBoozeSellPopup();
					return;
				}
				int num3 = basePrice * 3 * num2;
				Label val3 = default(Label);
				val3 = new Label("dirty-cash");
				try
				{
					inventory.ForceAddResourcesRegardlessOfSpace(val3, num3);
					Debug.Log($"[GameplayTweaks] Added ${num3} dirty cash from booze sale");
				}
				catch
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(num3)), (MoneyReason)1);
					Debug.Log($"[GameplayTweaks] Added ${num3} clean cash (dirty-cash not found)");
				}
				orCreateCrewState.LastBoozeSellTurn = G.GetNow().days;
				orCreateCrewState.StreetCreditProgress += 0.15f;
				if (orCreateCrewState.StreetCreditProgress >= 1f)
				{
					orCreateCrewState.StreetCreditProgress -= 1f;
					orCreateCrewState.StreetCreditLevel++;
				}
				Debug.Log($"[GameplayTweaks] Sold {num2}x {itemKey} for ${num3} (3x). Street credit +0.15");
				PopulateBoozeSellPopup();
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] SellBoozeItem failed: {arg}");
			}
		}

		private static int ExtractFixnumAsInt(object fixnum)
		{
			if (fixnum == null)
			{
				return 0;
			}
			try
			{
				return Convert.ToInt32(fixnum);
			}
			catch
			{
			}
			try
			{
				Type type = fixnum.GetType();
				MethodInfo method = type.GetMethod("op_Explicit", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { type }, null);
				if (method != null && method.ReturnType == typeof(int))
				{
					return (int)method.Invoke(null, new object[1] { fixnum });
				}
			}
			catch
			{
			}
			Type type2 = fixnum.GetType();
			string[] array = new string[5] { "v", "_value", "raw", "value", "_v" };
			foreach (string name in array)
			{
				FieldInfo field = type2.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					int num = Convert.ToInt32(field.GetValue(fixnum));
					if (num >= 1000 && num % 1000 == 0)
					{
						return num / 1000;
					}
					return num;
				}
			}
			try
			{
				string s = fixnum.ToString();
				if (int.TryParse(s, out var result))
				{
					return result;
				}
				if (float.TryParse(s, out var result2))
				{
					return (int)result2;
				}
			}
			catch
			{
			}
			return 0;
		}

		private static void OnSellAllBooze()
		{

			if (_selectedPeep == null)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(_selectedPeep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			int days = G.GetNow().days;
			if (orCreateCrewState.LastBoozeSellTurn != 0 && days - orCreateCrewState.LastBoozeSellTurn < 28)
			{
				return;
			}
			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					return;
				}
				EntityID safehouse = humanPlayer.territory.Safehouse;
				if (safehouse.IsNotValid)
				{
					return;
				}
				Entity val = EntityIDExtensions.FindEntity(safehouse);
				if (val == null)
				{
					return;
				}
				InventoryModule inventory = ModulesUtil.GetInventory(val);
				if (inventory == null)
				{
					return;
				}
				int num = 0;
				int num2 = 0;
				Label val2 = default(Label);
				foreach (KeyValuePair<string, int> boozePrice in _boozePrices)
				{
					try
					{
						val2 = new Label(boozePrice.Key);
						int num3 = 0;
						try
						{
							num3 = ExtractFixnumAsInt(((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory).data.Get(val2).qty);
						}
						catch
						{
							continue;
						}
						if (num3 > 0)
						{
							int num4 = 0;
							try
							{
								num4 = ExtractFixnumAsInt(inventory.TryRemoveResourcesUpToAll(val2, num3));
							}
							catch
							{
								num4 = num3;
							}
							if (num4 > 0)
							{
								num += boozePrice.Value * 3 * num4;
								num2 += num4;
								Debug.Log($"[GameplayTweaks] Sold {num4}x {boozePrice.Key} for ${boozePrice.Value * 3 * num4}");
							}
						}
						;
					}
					catch
					{
					}
				}
				if (num2 > 0)
				{
					Label val3 = default(Label);
					val3 = new Label("dirty-cash");
					try
					{
						inventory.ForceAddResourcesRegardlessOfSpace(val3, num);
						Debug.Log($"[GameplayTweaks] Added ${num} dirty cash to safehouse");
					}
					catch
					{
						humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(num)), (MoneyReason)1);
						Debug.Log($"[GameplayTweaks] Added ${num} clean cash (dirty-cash resource not found)");
					}
					orCreateCrewState.LastBoozeSellTurn = days;
					orCreateCrewState.StreetCreditProgress += 0.15f;
					if (orCreateCrewState.StreetCreditProgress >= 1f)
					{
						orCreateCrewState.StreetCreditProgress -= 1f;
						orCreateCrewState.StreetCreditLevel++;
					}
					Debug.Log($"[GameplayTweaks] Sold all booze: {num2} items for ${num} dirty cash. Street credit +0.15");
				}
				else
				{
					Debug.Log("[GameplayTweaks] No booze found in safehouse to sell");
				}
				PopulateBoozeSellPopup();
				RefreshHandlerUI();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] OnSellAllBooze failed: {arg}");
			}
		}

		internal static Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style)
		{

			GameObject val = new GameObject(name, new Type[3]
			{
				typeof(RectTransform),
				typeof(Text),
				typeof(LayoutElement)
			});
			val.transform.SetParent(parent, false);
			LayoutElement component = val.GetComponent<LayoutElement>();
			component.minHeight = size + 8;
			component.preferredHeight = size + 8;
			Text component2 = val.GetComponent<Text>();
			component2.text = text;
			component2.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component2.fontSize = size;
			((Graphic)component2).color = Color.white;
			component2.fontStyle = style;
			component2.alignment = (TextAnchor)3;
			return component2;
		}

		internal static Button CreateButton(Transform parent, string name, string label, Action onClick)
		{

			GameObject val = new GameObject(name, new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Button),
				typeof(LayoutElement)
			});
			val.transform.SetParent(parent, false);
			LayoutElement component = val.GetComponent<LayoutElement>();
			component.minHeight = 28f;
			component.preferredHeight = 28f;
			component.flexibleWidth = 1f;
			Image component2 = val.GetComponent<Image>();
			((Graphic)component2).color = new Color(0.3f, 0.25f, 0.2f, 0.95f);
			Button component3 = val.GetComponent<Button>();
			((Selectable)component3).targetGraphic = (Graphic)(object)component2;
			((UnityEvent)component3.onClick).AddListener((UnityAction)delegate
			{
				onClick();
			});
			GameObject val2 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component4 = val2.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = Vector2.zero;
			component4.offsetMax = Vector2.zero;
			Text component5 = val2.GetComponent<Text>();
			component5.text = label;
			component5.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component5.fontSize = 11;
			((Graphic)component5).color = Color.white;
			component5.alignment = (TextAnchor)4;
			return component3;
		}

		private static GameObject CreateStatBar(Transform parent, string name, Color fillColor, out Image fillImage)
		{

			GameObject val = new GameObject(name, new Type[2]
			{
				typeof(RectTransform),
				typeof(LayoutElement)
			});
			val.transform.SetParent(parent, false);
			LayoutElement component = val.GetComponent<LayoutElement>();
			component.minHeight = 20f;
			component.preferredHeight = 20f;
			GameObject val2 = new GameObject("Bg", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component2 = val2.GetComponent<RectTransform>();
			component2.anchorMin = Vector2.zero;
			component2.anchorMax = Vector2.one;
			component2.offsetMin = Vector2.zero;
			component2.offsetMax = Vector2.zero;
			((Graphic)val2.GetComponent<Image>()).color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
			GameObject val3 = new GameObject("Fill", new Type[2]
			{
				typeof(RectTransform),
				typeof(Image)
			});
			val3.transform.SetParent(val2.transform, false);
			RectTransform component3 = val3.GetComponent<RectTransform>();
			component3.anchorMin = Vector2.zero;
			component3.anchorMax = Vector2.one;
			component3.offsetMin = new Vector2(2f, 2f);
			component3.offsetMax = new Vector2(-2f, -2f);
			fillImage = val3.GetComponent<Image>();
			((Graphic)fillImage).color = fillColor;
			fillImage.type = (Image.Type)3;
			fillImage.fillMethod = (Image.FillMethod)0;
			fillImage.fillAmount = 0.5f;
			return val;
		}

		internal static GameObject CreateHorizontalRow(Transform parent, string name)
		{

			GameObject val = new GameObject(name, new Type[3]
			{
				typeof(RectTransform),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			val.transform.SetParent(parent, false);
			LayoutElement component = val.GetComponent<LayoutElement>();
			component.minHeight = 30f;
			component.preferredHeight = 30f;
			HorizontalLayoutGroup component2 = val.GetComponent<HorizontalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component2).spacing = 5f;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component2).childForceExpandHeight = true;
			return val;
		}

		private static List<Entity> FindAllRelatives(Entity peep)
		{

			List<Entity> list = new List<Entity>();
			RelationshipTracker rels = G.GetRels();
			SimTime now = G.GetNow();
			if (rels == null)
			{
				return list;
			}
			RelationshipList listOrNull = rels.GetListOrNull(peep.Id);
			if (listOrNull == null)
			{
				return list;
			}
			foreach (Relationship datum in listOrNull.data)
			{
				if ((int)datum.type == 20 || (int)datum.type == 50 || (int)datum.type == 10 || (int)datum.type == 30 || (int)datum.type == 40 || (int)datum.type == 90)
				{
					Entity val = EntityIDExtensions.FindEntity(datum.to);
					if (val != null && PlayerSocial.IsEligibleCrewMember(now, val))
					{
						list.Add(val);
					}
				}
			}
			return list;
		}

		private static void ShowGangRenameInput(PlayerInfo gang, Text nameLabel)
		{

			if ((UnityEngine.Object)(object)_gangRenameRow != (UnityEngine.Object)null)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)_gangRenameRow);
				_gangRenameRow = null;
			}
			_gangRenameTarget = gang;
			_gangRenameNameText = nameLabel;
			PlayerSocial social = gang.social;
			string text = ((social != null) ? social.PlayerGroupName : null) ?? "";
			Transform parent = ((Component)nameLabel).transform.parent;
			_gangRenameRow = new GameObject("RenameRow", new Type[3]
			{
				typeof(RectTransform),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			_gangRenameRow.transform.SetParent(parent, false);
			_gangRenameRow.GetComponent<LayoutElement>().minHeight = 24f;
			HorizontalLayoutGroup component = _gangRenameRow.GetComponent<HorizontalLayoutGroup>();
			((HorizontalOrVerticalLayoutGroup)component).spacing = 4f;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandWidth = false;
			((HorizontalOrVerticalLayoutGroup)component).childForceExpandHeight = true;
			GameObject val = new GameObject("Input", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(InputField),
				typeof(LayoutElement)
			});
			val.transform.SetParent(_gangRenameRow.transform, false);
			val.GetComponent<LayoutElement>().flexibleWidth = 1f;
			val.GetComponent<LayoutElement>().minHeight = 22f;
			((Graphic)val.GetComponent<Image>()).color = new Color(0.12f, 0.1f, 0.08f, 1f);
			_gangRenameInput = val.GetComponent<InputField>();
			_gangRenameInput.characterLimit = 30;
			GameObject val2 = new GameObject("Text", new Type[2]
			{
				typeof(RectTransform),
				typeof(Text)
			});
			val2.transform.SetParent(val.transform, false);
			RectTransform component2 = val2.GetComponent<RectTransform>();
			component2.anchorMin = Vector2.zero;
			component2.anchorMax = Vector2.one;
			component2.offsetMin = new Vector2(4f, 0f);
			component2.offsetMax = new Vector2(-4f, 0f);
			Text component3 = val2.GetComponent<Text>();
			component3.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			component3.fontSize = 11;
			((Graphic)component3).color = Color.white;
			component3.supportRichText = false;
			_gangRenameInput.textComponent = component3;
			_gangRenameInput.text = text;
			val.AddComponent<InputFieldBlocker>();
			Button obj = CreateButton(_gangRenameRow.transform, "SaveRename", "Save", OnSaveGangRename);
			((Component)obj).GetComponent<LayoutElement>().preferredWidth = 45f;
			((Graphic)((Component)obj).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
		}

		private static void OnSaveGangRename()
		{
			if (_gangRenameTarget == null || (UnityEngine.Object)(object)_gangRenameInput == (UnityEngine.Object)null)
			{
				return;
			}
			string text = _gangRenameInput.text?.Trim();
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			try
			{
				PlayerSocial social = _gangRenameTarget.social;
				if (social != null)
				{
					MethodInfo method = ((object)social).GetType().GetMethod("ForceGroupNameIfValid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method != null)
					{
						method.Invoke(social, new object[1] { text });
						Debug.Log(("[GameplayTweaks] Renamed gang to: " + text));
					}
					else
					{
						FieldInfo field = ((object)social).GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
						if (field != null)
						{
							object value = field.GetValue(social);
							FieldInfo field2 = value.GetType().GetField("social", BindingFlags.Instance | BindingFlags.Public);
							if (field2 != null)
							{
								object value2 = field2.GetValue(value);
								FieldInfo field3 = value2.GetType().GetField("groupname", BindingFlags.Instance | BindingFlags.Public);
								if (field3 != null)
								{
									field3.SetValue(value2, text);
									field2.SetValue(value, value2);
									field.SetValue(social, value);
									Debug.Log(("[GameplayTweaks] Renamed gang via fallback to: " + text));
								}
							}
						}
					}
				}
				if ((UnityEngine.Object)(object)_gangRenameNameText != (UnityEngine.Object)null)
				{
					_gangRenameNameText.text = text;
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Gang rename failed: {arg}");
			}
			if ((UnityEngine.Object)(object)_gangRenameRow != (UnityEngine.Object)null)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)_gangRenameRow);
				_gangRenameRow = null;
			}
			_gangRenameTarget = null;
			_gangRenameNameText = null;
		}
	}

	private static class CrewOutingEvent
	{
		public static void ShowOutingPrompt(GameObject ignored = null)
		{

			if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingPopup != (UnityEngine.Object)null)
			{
				CrewRelationshipHandlerPatch._outingPopup.SetActive(true);
				CrewRelationshipHandlerPatch._outingPopup.transform.SetAsLastSibling();
				RefreshOutingText();
				return;
			}
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if ((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null)
			{
				Debug.LogWarning("[GameplayTweaks] Could not create overlay canvas for outing popup");
				return;
			}
			GameObject val = new GameObject("OutingEventPopup", new Type[5]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(VerticalLayoutGroup),
				typeof(Canvas),
				typeof(GraphicRaycaster)
			});
			val.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
			Canvas component = val.GetComponent<Canvas>();
			component.overrideSorting = true;
			component.sortingOrder = 999;
			RectTransform component2 = val.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(0.5f, 0.7f);
			component2.anchorMax = new Vector2(0.5f, 0.7f);
			component2.pivot = new Vector2(0.5f, 0.5f);
			component2.sizeDelta = new Vector2(380f, 220f);
			((Graphic)val.GetComponent<Image>()).color = new Color(0.1f, 0.08f, 0.06f, 0.98f);
			VerticalLayoutGroup component3 = val.GetComponent<VerticalLayoutGroup>();
			((LayoutGroup)component3).padding = new RectOffset(14, 14, 12, 12);
			((HorizontalOrVerticalLayoutGroup)component3).spacing = 8f;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
			((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
			Outline obj = val.AddComponent<Outline>();
			((Shadow)obj).effectColor = new Color(0.85f, 0.7f, 0.3f, 0.8f);
			((Shadow)obj).effectDistance = new Vector2(2f, -2f);
			Text obj2 = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "OE_Title", "-- Gang Meeting Proposal --", 15, (FontStyle)1);
			obj2.alignment = (TextAnchor)4;
			((Graphic)obj2).color = new Color(0.95f, 0.85f, 0.4f);
			CrewRelationshipHandlerPatch._outingText = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "OE_Desc", "", 12, (FontStyle)0);
			CrewRelationshipHandlerPatch._outingText.alignment = (TextAnchor)4;
			((Graphic)CrewRelationshipHandlerPatch._outingText).color = new Color(0.9f, 0.85f, 0.7f);
			RefreshOutingText();
			GameObject obj3 = CrewRelationshipHandlerPatch.CreateHorizontalRow(val.transform, "OE_Buttons");
			obj3.GetComponent<LayoutElement>().minHeight = 38f;
			((Graphic)((Component)CrewRelationshipHandlerPatch.CreateButton(obj3.transform, "OE_Accept", "Accept", OnAcceptOuting)).GetComponent<Image>()).color = new Color(0.2f, 0.45f, 0.2f, 0.95f);
			((Graphic)((Component)CrewRelationshipHandlerPatch.CreateButton(obj3.transform, "OE_Deny", "Deny", OnDenyOuting)).GetComponent<Image>()).color = new Color(0.45f, 0.2f, 0.2f, 0.95f);
			CrewRelationshipHandlerPatch._outingPopup = val;
			val.transform.SetAsLastSibling();
			Debug.Log("[GameplayTweaks] Outing event popup created and shown");
		}

		private static void RefreshOutingText()
		{
			if (!((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingText == (UnityEngine.Object)null))
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				int num = ((humanCrew != null) ? humanCrew.LivingCrewCount : 0);
				int num2 = num * 25;
				int playerCleanCash = GetPlayerCleanCash();
				int totalDirtyCash = GetTotalDirtyCash();
				int num3 = playerCleanCash + totalDirtyCash;
				string arg = ((totalDirtyCash > 0) ? $"Available: ${playerCleanCash} clean + ${totalDirtyCash} dirty = ${num3}" : $"Available: ${playerCleanCash}");
				CrewRelationshipHandlerPatch._outingText.text = $"Your crew is calling a gang meeting!\n\nCost: ${num2} ($25 x {num} members)\n{arg}\nEffect: All crew happiness restored to full";
			}
		}

		private static void OnAcceptOuting()
		{

			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					return;
				}
				PlayerCrew crew = humanPlayer.crew;
				if (crew == null)
				{
					return;
				}
				int livingCrewCount = crew.LivingCrewCount;
				int num = livingCrewCount * 25;
				int playerCleanCash = GetPlayerCleanCash();
				int totalDirtyCash = GetTotalDirtyCash();
				int num2 = playerCleanCash + totalDirtyCash;
				if (num2 < num)
				{
					Debug.Log($"[GameplayTweaks] Can't afford gang meeting: need ${num}, have ${num2} (clean=${playerCleanCash}, dirty=${totalDirtyCash})");
					if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingText != (UnityEngine.Object)null)
					{
						CrewRelationshipHandlerPatch._outingText.text = $"Not enough money! Need ${num}, have ${num2}";
					}
					return;
				}
				int num3 = Math.Min(playerCleanCash, num);
				if (num3 > 0)
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-num3)), (MoneyReason)1);
				}
				int num4 = num - num3;
				if (num4 > 0)
				{
					EntityID safehouse = humanPlayer.territory.Safehouse;
					if (!safehouse.IsNotValid)
					{
						Entity val = EntityIDExtensions.FindEntity(safehouse);
						if (val != null)
						{
							RemoveDirtyCash(val, num4);
						}
					}
				}
				foreach (CrewAssignment item in crew.GetLiving())
				{
					CrewAssignment current = item;
					Entity peep = current.GetPeep();
					if (peep != null)
					{
						CrewModState orCreateCrewState = GetOrCreateCrewState(peep.Id);
						if (orCreateCrewState != null)
						{
							orCreateCrewState.HappinessValue = 1f;
							orCreateCrewState.TurnsUnhappy = 0;
						}
					}
				}
				Debug.Log($"[GameplayTweaks] Gang meeting accepted! Spent ${num} for {livingCrewCount} crew members");
				CrewRelationshipHandlerPatch._outingPopup.SetActive(false);
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Outing accept failed: {arg}");
			}
		}

		private static void OnDenyOuting()
		{
			if ((UnityEngine.Object)(object)CrewRelationshipHandlerPatch._outingPopup != (UnityEngine.Object)null)
			{
				CrewRelationshipHandlerPatch._outingPopup.SetActive(false);
			}
			Debug.Log("[GameplayTweaks] Gang meeting denied");
		}
	}

	private static class PactInvitationEvent
	{
		private static GameObject _invitePopup;

		private static AlliancePact _invitingPact;

		internal static void ShowInvitation(AlliancePact pact)
		{

			_invitingPact = pact;
			if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
			{
				_invitePopup.SetActive(true);
				RefreshText();
				return;
			}
			Canvas orCreateOverlayCanvas = GetOrCreateOverlayCanvas();
			if (!((UnityEngine.Object)(object)orCreateOverlayCanvas == (UnityEngine.Object)null))
			{
				GameObject val = new GameObject("PactInvitePopup", new Type[5]
				{
					typeof(RectTransform),
					typeof(Image),
					typeof(VerticalLayoutGroup),
					typeof(Canvas),
					typeof(GraphicRaycaster)
				});
				val.transform.SetParent(((Component)orCreateOverlayCanvas).transform, false);
				Canvas component = val.GetComponent<Canvas>();
				component.overrideSorting = true;
				component.sortingOrder = 998;
				RectTransform component2 = val.GetComponent<RectTransform>();
				component2.anchorMin = new Vector2(0.5f, 0.65f);
				component2.anchorMax = new Vector2(0.5f, 0.65f);
				component2.pivot = new Vector2(0.5f, 0.5f);
				component2.sizeDelta = new Vector2(380f, 200f);
				((Graphic)val.GetComponent<Image>()).color = new Color(0.08f, 0.1f, 0.15f, 0.98f);
				VerticalLayoutGroup component3 = val.GetComponent<VerticalLayoutGroup>();
				((LayoutGroup)component3).padding = new RectOffset(14, 14, 12, 12);
				((HorizontalOrVerticalLayoutGroup)component3).spacing = 8f;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandWidth = true;
				((HorizontalOrVerticalLayoutGroup)component3).childForceExpandHeight = false;
				Outline obj = val.AddComponent<Outline>();
				((Shadow)obj).effectColor = pact.SharedColor;
				((Shadow)obj).effectDistance = new Vector2(2f, -2f);
				Text obj2 = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "PI_Title", "-- Pact Invitation --", 15, (FontStyle)1);
				obj2.alignment = (TextAnchor)4;
				((Graphic)obj2).color = pact.SharedColor;
				int num = pact.MemberIds.Count + ((pact.LeaderGangId >= 0) ? 1 : 0);
				Text obj3 = CrewRelationshipHandlerPatch.CreateLabel(val.transform, "PI_Body", $"The {pact.DisplayName} ({num} gangs) is inviting\nyour outfit to join their alliance!", 12, (FontStyle)0);
				obj3.alignment = (TextAnchor)4;
				((Graphic)obj3).color = new Color(0.85f, 0.85f, 0.8f);
				GameObject obj4 = CrewRelationshipHandlerPatch.CreateHorizontalRow(val.transform, "PI_Buttons");
				obj4.GetComponent<LayoutElement>().minHeight = 35f;
				Button obj5 = CrewRelationshipHandlerPatch.CreateButton(obj4.transform, "PI_Accept", "Join Pact", OnAcceptInvite);
				((Graphic)((Component)obj5).GetComponent<Image>()).color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
				((Component)obj5).GetComponent<LayoutElement>().flexibleWidth = 1f;
				Button obj6 = CrewRelationshipHandlerPatch.CreateButton(obj4.transform, "PI_Deny", "Decline", OnDeclineInvite);
				((Graphic)((Component)obj6).GetComponent<Image>()).color = new Color(0.4f, 0.2f, 0.2f, 0.95f);
				((Component)obj6).GetComponent<LayoutElement>().flexibleWidth = 1f;
				val.transform.SetAsLastSibling();
				_invitePopup = val;
			}
		}

		private static void RefreshText()
		{
		}

		private static void OnAcceptInvite()
		{
			if (_invitingPact == null)
			{
				return;
			}
			try
			{
				AlliancePact alliancePact = SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.ColorIndex == 6);
				if (alliancePact != null)
				{
					CrewRelationshipHandlerPatch.LeavePlayerPactForWar(alliancePact);
				}
				if (SaveData.PlayerJoinedPactIndex >= 0)
				{
					CrewRelationshipHandlerPatch.LeaveCurrentAIPact();
				}
				SaveData.PlayerJoinedPactIndex = _invitingPact.ColorIndex;
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer != null && !_invitingPact.MemberIds.Contains(humanPlayer.PID.id))
				{
					_invitingPact.MemberIds.Add(humanPlayer.PID.id);
				}
				RefreshPactCache();
				TerritoryColorPatch.RefreshAllTerritoryColors();
				Debug.Log(("[GameplayTweaks] Player accepted invitation to " + _invitingPact.DisplayName + "!"));
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Accept invite failed: {arg}");
			}
			if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
			{
				_invitePopup.SetActive(false);
			}
		}

		private static void OnDeclineInvite()
		{
			Debug.Log("[GameplayTweaks] Player declined pact invitation.");
			if ((UnityEngine.Object)(object)_invitePopup != (UnityEngine.Object)null)
			{
				_invitePopup.SetActive(false);
			}
		}
	}

	private static class StatTrackingPatch
	{
		private static FieldInfo _entityField;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Entities.AgentComponent");
				if (!(type == null))
				{
					MethodInfo method = type.GetMethod("IncrementStat", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(StatTrackingPatch), "IncrementStatPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Stat tracking enabled");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] StatTrackingPatch failed: {arg}");
			}
		}

		private static void IncrementStatPostfix(object __instance, CrewStats key, int delta)
		{

			if (!EnableCrewStats.Value)
			{
				return;
			}
			try
			{
				if (_entityField == null)
				{
					_entityField = typeof(AgentComponent).BaseType?.GetField("_entity", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				object obj = _entityField?.GetValue(__instance);
				Entity val = obj as Entity;
				if (val == null)
				{
					return;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || val.data.agent.pid != humanPlayer.PID)
				{
					return;
				}
				CrewModState orCreateCrewState = GetOrCreateCrewState(val.Id);
				if (orCreateCrewState == null)
				{
					return;
				}
				if ((int)key == 25)
				{
					orCreateCrewState.StreetCreditProgress += (float)delta * 0.05f;
					orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - 0.15f * (float)delta);
				}
				else if ((int)key == 0)
				{
					orCreateCrewState.StreetCreditProgress += (float)delta * 0.3f;
					string fullName = val.data.person.FullName;
					LogGrapevine($"DEATH: {delta} rival(s) fell to {fullName}");
					orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - 0.25f * (float)delta);
					for (int i = 0; i < delta; i++)
					{
						if (SharedRng.NextDouble() < 0.30000001192092896)
						{
							orCreateCrewState.HasWitness = true;
							orCreateCrewState.WitnessThreatAttempted = false;
							orCreateCrewState.WitnessThreatenedSuccessfully = false;
							orCreateCrewState.WantedProgress = Mathf.Clamp01(orCreateCrewState.WantedProgress + 0.25f);
							if (orCreateCrewState.WantedProgress >= 0.75f)
							{
								orCreateCrewState.WantedLevel = WantedLevel.High;
							}
							else if (orCreateCrewState.WantedProgress >= 0.5f)
							{
								orCreateCrewState.WantedLevel = WantedLevel.Medium;
							}
							else if (orCreateCrewState.WantedProgress >= 0.25f)
							{
								orCreateCrewState.WantedLevel = WantedLevel.Low;
							}
							Debug.Log(("[GameplayTweaks] Witness saw " + val.data.person.FullName + " commit a kill!"));
						}
					}
				}
				else if ((int)key == 1)
				{
					int value = 0;
					val.data.agent.crewHistoryStats.TryGetValue((CrewStats)1, out value);
					int num = value / 25;
					int num2 = orCreateCrewState.LastBoozeSoldCount / 25;
					if (num > num2)
					{
						orCreateCrewState.StreetCreditProgress += (float)(num - num2) * 0.1f;
					}
					orCreateCrewState.LastBoozeSoldCount = value;
				}
				orCreateCrewState.StreetCreditProgress = Mathf.Clamp01(orCreateCrewState.StreetCreditProgress);
				orCreateCrewState.WantedProgress = Mathf.Clamp01(orCreateCrewState.WantedProgress);
			}
			catch
			{
			}
		}
	}

	private static class TurnUpdatePatch
	{
		private static MethodInfo _returnCrewMethod;

		private static MethodInfo _addCrewMethod;

		private static MethodInfo _removeCrewCompletelyMethod;

		private static MethodInfo _addToCrewMethod;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				MethodInfo method = typeof(PlayerCrew).GetMethod("OnPlayerTurnStarted", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(TurnUpdatePatch), "OnTurnPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Turn update enabled");
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] TurnUpdatePatch failed: {arg}");
			}
		}

		private static void OnTurnPostfix(PlayerCrew __instance)
		{

			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || __instance != humanPlayer.crew)
				{
					return;
				}
				SimTime now = G.GetNow();
				foreach (CrewAssignment item in __instance.GetLiving().ToList())
				{
					CrewAssignment current = item;
					Entity peep = current.GetPeep();
					if (peep != null)
					{
						ProcessCrewMemberTurn(peep, now, humanPlayer);
					}
				}
				int days = now.days;
				if (days != _lastGangTrackDay)
				{
					_lastGangTrackDay = days;
					RefreshGangTracker();
				}
				if (EnableAIAlliances.Value)
				{
					ProcessAIAlliances(now);
					ProcessPactEarnings();
				}
				int days2 = now.days;
				if (CrewRelationshipHandlerPatch._lastOutingDay < 0)
				{
					CrewRelationshipHandlerPatch._lastOutingDay = days2;
				}
				if (days2 - CrewRelationshipHandlerPatch._lastOutingDay >= CrewRelationshipHandlerPatch._outingIntervalDays)
				{
					CrewRelationshipHandlerPatch._lastOutingDay = days2;
					try
					{
						CrewOutingEvent.ShowOutingPrompt();
					}
					catch (Exception arg)
					{
						Debug.LogError($"[GameplayTweaks] Outing prompt failed: {arg}");
					}
				}
				if (EnableDirtyCash.Value)
				{
					try
					{
						DirtyCashPatches.ProcessLaundering();
					}
					catch (Exception arg2)
					{
						Debug.LogError($"[GameplayTweaks] Laundering failed: {arg2}");
					}
				}
				// CopWarSystem truce logic handled by CopKilling mod
				if (EnableAIAlliances.Value && days2 % 90 == 0 && !SaveData.NeverAcceptPacts && SaveData.PlayerJoinedPactIndex < 0 && !SaveData.Pacts.Any((AlliancePact p) => p.ColorIndex == 6) && CalculateGangPower(humanPlayer) >= 30 && SharedRng.NextDouble() < 0.35)
				{
					List<AlliancePact> list = SaveData.Pacts.Where((AlliancePact p) => p.ColorIndex < 6 && !p.IsPending).ToList();
					if (list.Count > 0)
					{
						AlliancePact pact = list[SharedRng.Next(list.Count)];
						try
						{
							PactInvitationEvent.ShowInvitation(pact);
						}
						catch (Exception arg3)
						{
							Debug.LogError($"[GameplayTweaks] Pact invitation failed: {arg3}");
						}
					}
				}
				SaveModData();
			}
			catch (Exception arg4)
			{
				Debug.LogError($"[GameplayTweaks] OnTurnPostfix: {arg4}");
			}
		}

		private static void ProcessCrewMemberTurn(Entity peep, SimTime now, PlayerInfo player)
		{

			if (!EnableCrewStats.Value)
			{
				return;
			}
			CrewModState orCreateCrewState = GetOrCreateCrewState(peep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			if (orCreateCrewState.AwaitingChildBirth)
			{
				int count = peep.data.person.futurekids.Count;
				if (count < orCreateCrewState.LastFutureKidsCount)
				{
					orCreateCrewState.AwaitingChildBirth = false;
				}
				orCreateCrewState.LastFutureKidsCount = count;
			}
			if (orCreateCrewState.StreetCreditProgress >= 1f)
			{
				orCreateCrewState.StreetCreditLevel++;
				orCreateCrewState.StreetCreditProgress = 0f;
				GrantStreetCredit(player, 7);
			}
			if (orCreateCrewState.StreetCreditLevel < 2)
			{
				orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - 0.03f);
			}
			if (orCreateCrewState.HappinessValue <= 0.25f)
			{
				orCreateCrewState.TurnsUnhappy++;
			}
			else
			{
				orCreateCrewState.TurnsUnhappy = Math.Max(0, orCreateCrewState.TurnsUnhappy - 1);
			}
			if (orCreateCrewState.TurnsUnhappy >= 6)
			{
				try
				{
					CrewAssignment crewForIndex = player.crew.GetCrewForIndex(0);
					if (crewForIndex.IsValid && crewForIndex.peepId == peep.Id)
					{
						orCreateCrewState.TurnsUnhappy = 4;
					}
					else if (SharedRng.NextDouble() < 0.5)
					{
						if (_removeCrewCompletelyMethod == null)
						{
							_removeCrewCompletelyMethod = typeof(PlayerCrew).GetMethod("RemoveFromCrewCompletely", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							_addToCrewMethod = typeof(PlayerCrew).GetMethod("AddToCrew", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						}
						List<PlayerInfo> list = TrackedGangs.Where((PlayerInfo g) => g.crew != null && !g.crew.IsCrewDefeated && g.crew.LivingCrewCount > 0).ToList();
						if (list.Count > 0 && _removeCrewCompletelyMethod != null)
						{
							PlayerInfo val = list[SharedRng.Next(list.Count)];
							string fullName = peep.data.person.FullName;
							PlayerSocial social = val.social;
							string text = ((social != null) ? social.PlayerGroupName : null) ?? "unknown";
							_removeCrewCompletelyMethod.Invoke(player.crew, new object[1] { peep });
							if (_addToCrewMethod != null)
							{
								_addToCrewMethod.Invoke(val.crew, new object[2] { peep, null });
								Debug.Log(("[GameplayTweaks] " + fullName + " defected to " + text + " - too unhappy!"));
							}
							else
							{
								Debug.Log(("[GameplayTweaks] " + fullName + " left the crew (couldn't join another gang)"));
							}
						}
					}
				}
				catch (Exception arg)
				{
					Debug.LogError($"[GameplayTweaks] Crew defection failed: {arg}");
				}
			}
			if (orCreateCrewState.VacationPending && !orCreateCrewState.OnVacation)
			{
				try
				{
					CrewAssignment crewForPeep = player.crew.GetCrewForPeep(peep.Id);
					if (crewForPeep.IsValid && player.crew.IsOnBoard(peep.Id))
					{
						player.crew.RemoveCrewFromBoard(crewForPeep, (PlayerCrewData.OffBoardReason)3, false, orCreateCrewState.VacationDuration);
						orCreateCrewState.OnVacation = true;
						orCreateCrewState.VacationPending = false;
						orCreateCrewState.VacationReturns = now.IncrementDays(orCreateCrewState.VacationDuration);
						Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} left for vacation, returns in {orCreateCrewState.VacationDuration} days");
					}
				}
				catch (Exception arg2)
				{
					Debug.LogError($"[GameplayTweaks] Vacation departure failed: {arg2}");
				}
			}
			if (orCreateCrewState.OnVacation && now >= orCreateCrewState.VacationReturns)
			{
				orCreateCrewState.OnVacation = false;
				orCreateCrewState.VacationPending = false;
				try
				{
					CrewAssignment crewForPeep2 = player.crew.GetCrewForPeep(peep.Id);
					if (crewForPeep2.IsValid && !player.crew.IsOnBoard(peep.Id))
					{
						if (_returnCrewMethod == null)
						{
							_returnCrewMethod = typeof(PlayerCrew).GetMethod("ReturnCrewToBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							_addCrewMethod = typeof(PlayerCrew).GetMethod("AddCrewToBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						}
						if (_returnCrewMethod != null)
						{
							_returnCrewMethod.Invoke(player.crew, new object[1] { crewForPeep2 });
							Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " returned from vacation"));
						}
						else if (_addCrewMethod != null)
						{
							_addCrewMethod.Invoke(player.crew, new object[1] { crewForPeep2 });
							Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " returned from vacation (fallback)"));
						}
					}
				}
				catch (Exception arg3)
				{
					Debug.LogError($"[GameplayTweaks] Vacation return failed for {peep.data.person.FullName}: {arg3}");
				}
			}
			if (CrewRelationshipHandlerPatch._globalMayorBribeActive && now.days >= CrewRelationshipHandlerPatch._globalMayorBribeExpireDay)
			{
				CrewRelationshipHandlerPatch._globalMayorBribeActive = false;
				Debug.Log("[GameplayTweaks] Mayor bribe expired.");
			}
			if (orCreateCrewState.WantedProgress >= 0.75f)
			{
				orCreateCrewState.WantedLevel = WantedLevel.High;
			}
			else if (orCreateCrewState.WantedProgress >= 0.5f)
			{
				orCreateCrewState.WantedLevel = WantedLevel.Medium;
			}
			else if (orCreateCrewState.WantedProgress >= 0.25f)
			{
				orCreateCrewState.WantedLevel = WantedLevel.Low;
			}
			else
			{
				orCreateCrewState.WantedLevel = WantedLevel.None;
			}
			if (orCreateCrewState.WitnessThreatenedSuccessfully && !orCreateCrewState.HasWitness)
			{
				orCreateCrewState.WantedProgress = Mathf.Max(0f, orCreateCrewState.WantedProgress - 0.05f);
				if (orCreateCrewState.WantedProgress < 0.25f)
				{
					orCreateCrewState.WantedLevel = WantedLevel.None;
					orCreateCrewState.WitnessThreatenedSuccessfully = false;
					orCreateCrewState.WitnessThreatAttempted = false;
				}
			}
			bool globalMayorBribeActive = CrewRelationshipHandlerPatch._globalMayorBribeActive;
			if (orCreateCrewState.WantedLevel != WantedLevel.None)
			{
				float num = 0f;
				float num2 = (orCreateCrewState.WitnessThreatenedSuccessfully ? 0.1f : 0f);
				if (globalMayorBribeActive)
				{
					num += 0.15f + num2;
				}
				if (orCreateCrewState.JudgeBribeActive)
				{
					num += 0.25f + num2;
				}
				if (orCreateCrewState.LawyerRetainer >= 3000)
				{
					num += 0.3f;
				}
				else if (orCreateCrewState.LawyerRetainer >= 2000)
				{
					num += 0.2f;
				}
				else if (orCreateCrewState.LawyerRetainer >= 1000)
				{
					num += 0.1f;
				}
				if (num > 0f && SharedRng.NextDouble() < (double)num)
				{
					orCreateCrewState.CaseDismissed = true;
					orCreateCrewState.WantedLevel = WantedLevel.None;
					orCreateCrewState.WantedProgress = 0f;
					orCreateCrewState.HasWitness = false;
					orCreateCrewState.FedsIncoming = false;
					orCreateCrewState.FedArrivalCountdown = 0;
					orCreateCrewState.LawyerRetainer = 0;
					orCreateCrewState.JudgeBribeActive = false;
					orCreateCrewState.WitnessThreatAttempted = false;
					orCreateCrewState.WitnessThreatenedSuccessfully = false;
					Debug.Log(("[GameplayTweaks] Case dismissed for " + peep.data.person.FullName + "!"));
				}
			}
			if (orCreateCrewState.WantedLevel != WantedLevel.None && !orCreateCrewState.FedsIncoming && !CrewRelationshipHandlerPatch._globalMayorBribeActive && !orCreateCrewState.JudgeBribeActive)
			{
				orCreateCrewState.FedsIncoming = true;
				switch (orCreateCrewState.WantedLevel)
				{
				case WantedLevel.High:
					orCreateCrewState.FedArrivalCountdown = 2;
					break;
				case WantedLevel.Medium:
					orCreateCrewState.FedArrivalCountdown = 5;
					break;
				case WantedLevel.Low:
					orCreateCrewState.FedArrivalCountdown = 10;
					break;
				}
				Debug.Log($"[GameplayTweaks] Feds tracking {peep.data.person.FullName}, arrival in {orCreateCrewState.FedArrivalCountdown} days");
			}
			if (orCreateCrewState.FedsIncoming && !CrewRelationshipHandlerPatch._globalMayorBribeActive && !orCreateCrewState.JudgeBribeActive)
			{
				orCreateCrewState.FedArrivalCountdown--;
				if (orCreateCrewState.FedArrivalCountdown <= 0)
				{
					try
					{
						if (player.crew.IsOnBoard(peep.Id))
						{
							if (JailSystem.ArrestCrew(player, peep))
							{
								orCreateCrewState.FedsIncoming = false;
								orCreateCrewState.WantedLevel = WantedLevel.None;
								orCreateCrewState.WantedProgress = 0f;
								Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " arrested by feds (game system)!"));
							}
							else
							{
								CrewAssignment crewForPeep3 = player.crew.GetCrewForPeep(peep.Id);
								if (crewForPeep3.IsValid)
								{
									player.crew.RemoveCrewFromBoard(crewForPeep3, (PlayerCrewData.OffBoardReason)0, false, 7);
									orCreateCrewState.FedsIncoming = false;
									orCreateCrewState.WantedLevel = WantedLevel.None;
									orCreateCrewState.WantedProgress = 0f;
									Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " arrested by feds (fallback)!"));
								}
							}
						}
					}
					catch (Exception arg4)
					{
						Debug.LogError($"[GameplayTweaks] Fed arrest failed: {arg4}");
					}
				}
			}
			if (orCreateCrewState.MayorBribeActive || orCreateCrewState.JudgeBribeActive)
			{
				_ = orCreateCrewState.FedsIncoming;
			}
			if (orCreateCrewState.WantedLevel == WantedLevel.None && orCreateCrewState.FedsIncoming)
			{
				orCreateCrewState.FedsIncoming = false;
				orCreateCrewState.FedArrivalCountdown = 0;
			}
			try
			{
				SimTimeSpan age = peep.data.person.GetAge(now);
				float yearsFloat = age.YearsFloat;
				if (!(yearsFloat >= 50f))
				{
					return;
				}
				CrewAssignment crewForIndex2 = player.crew.GetCrewForIndex(0);
				if (crewForIndex2.IsValid && crewForIndex2.peepId == peep.Id)
				{
					return;
				}
				float num3 = Math.Min(0.6f, (yearsFloat - 50f) * 0.02f);
				if (SharedRng.NextDouble() < (double)num3)
				{
					string fullName2 = peep.data.person.FullName;
					int num4 = (int)yearsFloat;
					MethodInfo method = ((object)player.crew).GetType().GetMethod("ProcessCrewMemberDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method != null)
					{
						method.Invoke(player.crew, new object[1] { peep });
					}
					LogGrapevine($"DEATH: {fullName2} passed away at age {num4} (natural causes)");
				}
			}
			catch
			{
			}
		}

		private static void ProcessAIAlliances(SimTime now)
		{

			try
			{
				List<PlayerInfo> source = G.GetAllPlayers().ToList();
				List<PlayerInfo> list = source.Where(delegate(PlayerInfo p)
				{

					if (p.IsJustGang && !p.crew.IsCrewDefeated)
					{
						PlayerID pID = p.PID;
						return !pID.IsHumanPlayer;
					}
					return false;
				}).ToList();
				List<AlliancePact> list2 = new List<AlliancePact>();
				foreach (AlliancePact pact in SaveData.Pacts)
				{
					PlayerInfo val = source.FirstOrDefault((PlayerInfo p) => p.PID.id == pact.LeaderGangId);
					if (val == null || val.crew.IsCrewDefeated)
					{
						list2.Add(pact);
						Debug.Log(("[GameplayTweaks] Pact " + pact.PactId + " dissolved - leader defeated"));
						LogGrapevine("WAR: " + pact.DisplayName + " collapsed - leader gang defeated!");
					}
				}
				foreach (AlliancePact item in list2)
				{
					if (SaveData.PlayerPactId >= 0 && item.PactId == $"pact_{SaveData.PlayerPactId}")
					{
						SaveData.PlayerPactId = -1;
					}
					if (SaveData.PlayerJoinedPactIndex == item.ColorIndex)
					{
						SaveData.PlayerJoinedPactIndex = -1;
					}
					SaveData.Pacts.Remove(item);
				}
				if (list.Count < 2)
				{
					return;
				}
				var list3 = (from g in list
					select new
					{
						Gang = g,
						Power = CalculateGangPower(g)
					} into x
					orderby x.Power descending
					select x).ToList();
				int num = SaveData.Pacts.Count((AlliancePact p) => p.ColorIndex < 6);
				if (now.days % 30 == 0 && num < 6)
				{
					int num2 = -1;
					HashSet<int> hashSet = new HashSet<int>(SaveData.Pacts.Select((AlliancePact p) => p.ColorIndex));
					for (int num3 = 0; num3 < 6; num3++)
					{
						if (!hashSet.Contains(num3))
						{
							num2 = num3;
							break;
						}
					}
					if (num2 < 0)
					{
						return;
					}
					var list4 = list3.Where(x => GetPactForPlayer(x.Gang.PID) == null).ToList();
					if (list4.Count >= 2)
					{
						var leader = list4[0];
						var anon = list4.Skip(1).FirstOrDefault(x => x.Power > leader.Power / 3);
						if (anon != null)
						{
							AlliancePact alliancePact = new AlliancePact
							{
								PactId = $"pact_{SaveData.NextPactId++}",
								PactName = ModConstants.PACT_COLOR_NAMES[num2] + " Alliance",
								ColorIndex = num2,
								LeaderGangId = leader.Gang.PID.id,
								MemberIds = new List<int> { anon.Gang.PID.id },
								SharedColor = ModConstants.PACT_COLORS[num2],
								Formed = now
							};
							SaveData.Pacts.Add(alliancePact);
							Debug.Log(("[GameplayTweaks] AI Alliance: " + leader.Gang.social.PlayerGroupName + " + " + anon.Gang.social.PlayerGroupName + " (" + alliancePact.PactName + ")"));
							LogGrapevine("PACT: " + leader.Gang.social.PlayerGroupName + " and " + anon.Gang.social.PlayerGroupName + " formed " + alliancePact.PactName);
						}
					}
				}
				if (now.days % 14 != 0 || list.Count < 2)
				{
					return;
				}
				if (SharedRng.NextDouble() < 0.4)
				{
					var anon2 = list3[SharedRng.Next(Math.Min(4, list3.Count))];
					var anon3 = list3[SharedRng.Next(list3.Count)];
					if (anon2.Gang.PID.id != anon3.Gang.PID.id)
					{
						PlayerSocial social = anon2.Gang.social;
						string text = ((social != null) ? social.PlayerGroupName : null) ?? "Unknown";
						PlayerSocial social2 = anon3.Gang.social;
						string text2 = ((social2 != null) ? social2.PlayerGroupName : null) ?? "Unknown";
						LogGrapevine("DEATH: A member of " + text2 + " was killed by " + text);
					}
				}
				if (SharedRng.NextDouble() < 0.15)
				{
					var anon4 = list3[SharedRng.Next(list3.Count)];
					var anon5 = list3[SharedRng.Next(list3.Count)];
					if (anon4.Gang.PID.id != anon5.Gang.PID.id)
					{
						PlayerSocial social3 = anon4.Gang.social;
						string text3 = ((social3 != null) ? social3.PlayerGroupName : null) ?? "Unknown";
						PlayerSocial social4 = anon5.Gang.social;
						string text4 = ((social4 != null) ? social4.PlayerGroupName : null) ?? "Unknown";
						LogGrapevine("WAR: " + text3 + " started a turf war with " + text4 + "!");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] ProcessAIAlliances: {arg}");
			}
		}

		internal static void ProcessPactEarnings()
		{

			try
			{
				List<PlayerInfo> source = G.GetAllPlayers().ToList();
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int item = ((humanPlayer != null) ? humanPlayer.PID.id : (-1));
				foreach (AlliancePact pact in SaveData.Pacts)
				{
					List<int> list = new List<int>();
					if (pact.LeaderGangId >= 0)
					{
						list.Add(pact.LeaderGangId);
					}
					list.AddRange(pact.MemberIds);
					if (SaveData.PlayerJoinedPactIndex == pact.ColorIndex && humanPlayer != null && !list.Contains(item))
					{
						list.Add(item);
					}
					if (list.Count < 1)
					{
						continue;
					}
					int num = 0;
					int strongestId = -1;
					int num2 = -1;
					string text = "Unknown";
					foreach (int gid in list)
					{
						PlayerInfo val = source.FirstOrDefault((PlayerInfo p) => p.PID.id == gid);
						if (val != null && !val.crew.IsCrewDefeated)
						{
							int num3 = CalculateGangPower(val);
							int num4 = (int)((float)num3 * 0.05f * 10f);
							num += num4;
							if (num3 > num2)
							{
								num2 = num3;
								strongestId = gid;
								PlayerSocial social = val.social;
								text = ((social != null) ? social.PlayerGroupName : null) ?? "Unknown";
							}
						}
					}
					if (num > 0 && strongestId >= 0)
					{
						PlayerInfo val2 = source.FirstOrDefault((PlayerInfo p) => p.PID.id == strongestId);
						if (val2 != null)
						{
							val2.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(num)), (MoneyReason)1);
							LogGrapevine($"PACT: {text} earned ${num} as the strongest in {pact.DisplayName}");
							Debug.Log($"[GameplayTweaks] Pact {pact.DisplayName}: ${num} -> {text} (power={num2})");
						}
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] ProcessPactEarnings: {arg}");
			}
		}
	}

	private static class AICrewCapPatch
	{
		private static FieldInfo _playerField;

		private static HashSet<int> _topGangIds;

		private static int _topGangCacheDay = -1;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Sim.Advisors.UnitsAdvisor");
				if (!(type == null))
				{
					MethodInfo method = type.GetMethod("ShouldGrow", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, new HarmonyMethod(typeof(AICrewCapPatch), "ShouldGrowPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] AI crew cap enabled");
					}
				}
			}
			catch
			{
			}
		}

		private static void ShouldGrowPrefix(object __instance, ref int ___maxCrew)
		{

			if (!EnableAIAlliances.Value)
			{
				return;
			}
			try
			{
				if (_playerField == null)
				{
					_playerField = __instance.GetType().GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				object obj = _playerField?.GetValue(__instance);
				PlayerInfo val = obj as PlayerInfo;
				if (val == null)
				{
					return;
				}
				PlayerID pID = val.PID;
				if (pID.IsHumanPlayer)
				{
					return;
				}
				int days = G.GetNow().days;
				if (_topGangIds == null || _topGangCacheDay != days)
				{
					_topGangCacheDay = days;
					_topGangIds = new HashSet<int>(from x in (from p in G.GetAllPlayers()
							where p.IsJustGang && !p.crew.IsCrewDefeated
							select new
							{
								PID = p.PID,
								Power = CalculateGangPower(p)
							} into x
							orderby x.Power descending
							select x).Take(6)
						select (int)x.PID.id);
				}
				if (_topGangIds.Contains(val.PID.id))
				{
					___maxCrew = 40;
				}
			}
			catch
			{
			}
		}
	}

	private static class TerritoryColorPatch
	{
		private static FieldInfo _colorsField;

		private static object _mapDisplayInstance;

		private static FieldInfo _borderManagerField;

		private static FieldInfo _borderPlayerField;

		private static PropertyInfo _borderPlayerProp;

		private static FieldInfo _borderColorInfoField;

		private static FieldInfo _borderColorField;

		private static FieldInfo _borderBorderColorField;

		private static PropertyInfo _displayColorProp;

		private static FieldInfo _displayColorField;

		private static bool _borderReflectionCached;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Board.MapDisplayManager");
				if (type != null)
				{
					_colorsField = type.GetField("_colors", BindingFlags.Instance | BindingFlags.NonPublic);
					MethodInfo method = type.GetMethod("GetColorForPlayer", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(TerritoryColorPatch), "GetColorPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Territory color patch applied to MapDisplayManager");
					}
				}
				Type type2 = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay");
				if (type2 != null)
				{
					MethodInfo method2 = type2.GetMethod("RefreshBorder", BindingFlags.Instance | BindingFlags.Public);
					if (method2 != null)
					{
						harmony.Patch((MethodBase)method2, new HarmonyMethod(typeof(TerritoryColorPatch), "RefreshBorderPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Territory border refresh patch applied");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] TerritoryColorPatch failed: {arg}");
			}
		}

		private static void GetColorPostfix(ref Color __result, PlayerInfo player, object __instance)
		{

			if (!EnableAIAlliances.Value || player == null)
			{
				return;
			}
			try
			{
				_mapDisplayInstance = __instance;
				AlliancePact pactForPlayer = GetPactForPlayer(player.PID);
				if (pactForPlayer != null && pactForPlayer.IsActive)
				{
					__result = pactForPlayer.SharedColor;
					if (_colorsField != null && _colorsField.GetValue(__instance) is IDictionary dictionary)
					{
						dictionary[player.PID] = pactForPlayer.SharedColor;
					}
				}
			}
			catch
			{
			}
		}

		private static void RefreshBorderPrefix(object __instance)
		{

			if (!EnableAIAlliances.Value)
			{
				return;
			}
			try
			{
				if (!_borderReflectionCached)
				{
					_borderReflectionCached = true;
					_borderManagerField = __instance.GetType().GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic);
					_displayColorProp = __instance.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
					_displayColorField = __instance.GetType().GetField("_color", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				if (_borderManagerField == null)
				{
					return;
				}
				object value = _borderManagerField.GetValue(__instance);
				if (value == null)
				{
					return;
				}
				if (_borderPlayerField == null && _borderPlayerProp == null)
				{
					_borderPlayerField = value.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
					if (_borderPlayerField == null)
					{
						_borderPlayerProp = value.GetType().GetProperty("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
					_borderColorInfoField = value.GetType().GetField("colorInfo", BindingFlags.Instance | BindingFlags.Public);
				}
				PlayerInfo val = (_borderPlayerField != null)
				? _borderPlayerField.GetValue(value) as PlayerInfo
				: _borderPlayerProp?.GetValue(value) as PlayerInfo;
				if (val == null)
				{
					return;
				}
				AlliancePact pactForPlayer = GetPactForPlayer(val.PID);
				if (pactForPlayer == null || !pactForPlayer.IsActive)
				{
					return;
				}
				if (_borderColorInfoField != null)
				{
					object value2 = _borderColorInfoField.GetValue(value);
					if (value2 != null)
					{
						if (_borderColorField == null)
						{
							_borderColorField = value2.GetType().GetField("color", BindingFlags.Instance | BindingFlags.Public) ?? value2.GetType().GetField("Color", BindingFlags.Instance | BindingFlags.Public);
							_borderBorderColorField = value2.GetType().GetField("borderColor", BindingFlags.Instance | BindingFlags.Public) ?? value2.GetType().GetField("BorderColor", BindingFlags.Instance | BindingFlags.Public);
						}
						if (_borderColorField != null)
						{
							_borderColorField.SetValue(value2, pactForPlayer.SharedColor);
							_borderColorInfoField.SetValue(value, value2);
						}
						if (_borderBorderColorField != null)
						{
							_borderBorderColorField.SetValue(value2, pactForPlayer.SharedColor);
							_borderColorInfoField.SetValue(value, value2);
						}
					}
				}
				if (_displayColorProp != null && _displayColorProp.CanWrite)
				{
					_displayColorProp.SetValue(__instance, pactForPlayer.SharedColor);
				}
				if (_displayColorField != null)
				{
					_displayColorField.SetValue(__instance, pactForPlayer.SharedColor);
				}
			}
			catch
			{
			}
		}

		public static void RefreshAllTerritoryColors()
		{

			try
			{
				if (_mapDisplayInstance == null)
				{
					try
					{
						Type ctxType = typeof(GameClock).Assembly.GetType("Game.Core.Game");
						if (ctxType != null)
						{
							PropertyInfo ctxProp = ctxType.GetProperty("ctx", BindingFlags.Static | BindingFlags.Public);
							object ctx = ctxProp?.GetValue(null);
							if (ctx != null)
							{
								PropertyInfo mdProp = ctx.GetType().GetProperty("mapdisplay") ?? ctx.GetType().GetProperty("MapDisplay");
								FieldInfo mdField = ctx.GetType().GetField("mapdisplay") ?? ctx.GetType().GetField("MapDisplay");
								object md = mdProp?.GetValue(ctx) ?? mdField?.GetValue(ctx);
								if (md != null)
								{
									_mapDisplayInstance = md;
									if (_colorsField == null)
									{
										_colorsField = md.GetType().GetField("_colors", BindingFlags.Instance | BindingFlags.NonPublic);
									}
								}
							}
						}
					}
					catch
					{
					}
				}
				if (_mapDisplayInstance != null && _colorsField != null && _colorsField.GetValue(_mapDisplayInstance) is IDictionary dictionary)
				{
					foreach (AlliancePact pact in SaveData.Pacts)
					{
						if (!pact.IsActive)
						{
							continue;
						}
						foreach (PlayerInfo allPlayer in G.GetAllPlayers())
						{
							if (allPlayer != null && pact.IsMember(allPlayer.PID))
							{
								try
								{
									dictionary[allPlayer.PID] = pact.SharedColor;
								}
								catch
								{
								}
							}
						}
					}
				}
				MethodInfo methodInfo = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay")?.GetMethod("RefreshBorder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo field = typeof(PlayerTerritory).GetField("_display", BindingFlags.Instance | BindingFlags.NonPublic);
				MethodInfo method = typeof(PlayerTerritory).GetMethod("RefreshDisplay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(PlayerTerritory).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (PlayerInfo allPlayer2 in G.GetAllPlayers())
				{
					if (allPlayer2?.territory == null)
					{
						continue;
					}
					try
					{
						if (method != null)
						{
							method.Invoke(allPlayer2.territory, null);
						}
						else if (method2 != null)
						{
							method2.Invoke(allPlayer2.territory, null);
						}
					}
					catch
					{
					}
					object obj3 = field?.GetValue(allPlayer2.territory);
					if (obj3 == null)
					{
						continue;
					}
					try
					{
						if (methodInfo != null)
						{
							methodInfo.Invoke(obj3, null);
						}
					}
					catch
					{
					}
				}
				if (_mapDisplayInstance != null)
				{
					try
					{
						MethodInfo method3 = _mapDisplayInstance.GetType().GetMethod("RefreshColors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (method3 != null)
						{
							method3.Invoke(_mapDisplayInstance, null);
						}
						else
						{
							MethodInfo method4 = _mapDisplayInstance.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							if (method4 != null)
							{
								method4.Invoke(_mapDisplayInstance, null);
							}
						}
					}
					catch
					{
					}
				}
				Debug.Log("[GameplayTweaks] Territory colors refreshed for all players");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] RefreshAllTerritoryColors failed: {arg}");
			}
		}
	}

	private static class BossArrestPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CopTracker");
				if (type == null)
				{
					Debug.LogWarning("[GameplayTweaks] CopTracker type not found");
					return;
				}
				MethodInfo method = type.GetMethod("CanBeArrested", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(BossArrestPatch), "CanBeArrestedPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Boss arrest patch enabled");
				}
				else
				{
					Debug.LogWarning("[GameplayTweaks] CanBeArrested method not found");
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] BossArrestPatch failed: {arg}");
			}
		}

		private static void CanBeArrestedPostfix(ref bool __result, CrewAssignment crew, PlayerInfo player)
		{

			try
			{
				if (player != null && !(crew.peepId != player.social.PlayerPeepId))
				{
					CrewModState crewStateOrNull = GetCrewStateOrNull(crew.peepId);
					if (crewStateOrNull != null && crewStateOrNull.WantedLevel == WantedLevel.High)
					{
						__result = true;
						Debug.Log("[GameplayTweaks] Boss can be arrested - wanted level is High");
					}
				}
			}
			catch
			{
			}
		}
	}

	private static class JailSystem
	{
		private static Type _copTrackerType;

		private static object _copTrackerInstance;

		private static MethodInfo _startHumanArrestMethod;

		private static PropertyInfo _dataProperty;

		public static void Initialize()
		{
			try
			{
				_copTrackerType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CopTracker");
				if (!(_copTrackerType == null))
				{
					_startHumanArrestMethod = _copTrackerType.GetMethod("StartHumanArrest", BindingFlags.Instance | BindingFlags.Public);
					_dataProperty = _copTrackerType.GetProperty("data", BindingFlags.Instance | BindingFlags.Public);
					Debug.Log("[GameplayTweaks] JailSystem initialized");
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] JailSystem init failed: {arg}");
			}
		}

		private static object GetCopTracker()
		{
			if (_copTrackerInstance != null)
			{
				return _copTrackerInstance;
			}
			try
			{
				object ctx = G.GetCtx();
				if (ctx == null)
				{
					return null;
				}
				object obj = ctx.GetType().GetField("simman")?.GetValue(ctx);
				if (obj == null)
				{
					return null;
				}
				_copTrackerInstance = (obj.GetType().GetField("copTracker") ?? obj.GetType().GetField("cops"))?.GetValue(obj);
				return _copTrackerInstance;
			}
			catch
			{
				return null;
			}
		}

		public static bool ArrestCrew(PlayerInfo player, Entity peep)
		{

			try
			{
				object copTracker = GetCopTracker();
				if (copTracker == null || _startHumanArrestMethod == null)
				{
					return false;
				}
				CrewAssignment crewForPeep = player.crew.GetCrewForPeep(peep.Id);
				if (!crewForPeep.IsValid)
				{
					return false;
				}
				_startHumanArrestMethod.Invoke(copTracker, new object[2] { player, crewForPeep });
				CrewModState orCreateCrewState = GetOrCreateCrewState(peep.Id);
				if (orCreateCrewState != null)
				{
					orCreateCrewState.InJail = true;
					orCreateCrewState.DaysInJail = 0;
					orCreateCrewState.TrialDaysRemaining = 7;
					orCreateCrewState.FedsIncoming = false;
				}
				Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " arrested via game system"));
				return true;
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] ArrestCrew failed: {arg}");
				return false;
			}
		}

		public static bool IsInJail(EntityID peepId)
		{
			try
			{
				object copTracker = GetCopTracker();
				if (copTracker == null)
				{
					return false;
				}
				MethodInfo method = _copTrackerType.GetMethod("IsArrestedOrImprisoned", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					return (bool)method.Invoke(copTracker, new object[1] { peepId });
				}
			}
			catch
			{
			}
			return false;
		}

		public static (int daysToTrial, int bribeCost, bool isPaidOff) GetArrestInfo(EntityID peepId)
		{

			try
			{
				object copTracker = GetCopTracker();
				if (copTracker == null)
				{
					return (daysToTrial: 0, bribeCost: 0, isPaidOff: false);
				}
				object obj = _copTrackerType.GetField("data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(copTracker);
				if (obj == null)
				{
					return (daysToTrial: 0, bribeCost: 0, isPaidOff: false);
				}
				if (!(obj.GetType().GetField("fedArrests", BindingFlags.Instance | BindingFlags.Public)?.GetValue(obj) is IList list))
				{
					return (daysToTrial: 0, bribeCost: 0, isPaidOff: false);
				}
				foreach (object item4 in list)
				{
					if ((EntityID)item4.GetType().GetField("peepId", BindingFlags.Instance | BindingFlags.Public).GetValue(item4) == peepId)
					{
						FieldInfo field = item4.GetType().GetField("trialDate", BindingFlags.Instance | BindingFlags.Public);
						FieldInfo field2 = item4.GetType().GetField("payOffCost", BindingFlags.Instance | BindingFlags.Public);
						FieldInfo field3 = item4.GetType().GetField("paidOff", BindingFlags.Instance | BindingFlags.Public);
						SimTime val = (SimTime)field.GetValue(item4);
						object value = field2.GetValue(item4);
						bool item = (bool)field3.GetValue(item4);
						int item2 = val.days - G.GetNow().days;
						int item3 = 0;
						try
						{
							item3 = (int)value.GetType().GetField("cash", BindingFlags.Instance | BindingFlags.Public).GetValue(value);
						}
						catch
						{
						}
						return (daysToTrial: item2, bribeCost: item3, isPaidOff: item);
					}
				}
			}
			catch
			{
			}
			return (daysToTrial: 0, bribeCost: 0, isPaidOff: false);
		}

		public static (int daysRemaining, int totalYears) GetImprisonmentInfo(EntityID peepId)
		{

			try
			{
				object copTracker = GetCopTracker();
				if (copTracker == null)
				{
					return (daysRemaining: 0, totalYears: 0);
				}
				object obj = _copTrackerType.GetField("data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(copTracker);
				if (obj == null)
				{
					return (daysRemaining: 0, totalYears: 0);
				}
				if (!(obj.GetType().GetField("fedImprisoned", BindingFlags.Instance | BindingFlags.Public)?.GetValue(obj) is IList list))
				{
					return (daysRemaining: 0, totalYears: 0);
				}
				foreach (object item2 in list)
				{
					if ((EntityID)item2.GetType().GetField("peepId", BindingFlags.Instance | BindingFlags.Public).GetValue(item2) == peepId)
					{
						FieldInfo field = item2.GetType().GetField("endDate", BindingFlags.Instance | BindingFlags.Public);
						FieldInfo field2 = item2.GetType().GetField("years", BindingFlags.Instance | BindingFlags.Public);
						SimTime val = (SimTime)field.GetValue(item2);
						int item = (int)field2.GetValue(item2);
						return (daysRemaining: val.days - G.GetNow().days, totalYears: item);
					}
				}
			}
			catch
			{
			}
			return (daysRemaining: 0, totalYears: 0);
		}

		public static void PayLawyerRetainer(EntityID peepId, int amount)
		{
			CrewModState orCreateCrewState = GetOrCreateCrewState(peepId);
			if (orCreateCrewState != null)
			{
				orCreateCrewState.LawyerRetainer += amount;
				Debug.Log($"[GameplayTweaks] Lawyer retainer paid: ${amount}, total: ${orCreateCrewState.LawyerRetainer}");
			}
		}

		public static string GetJailStatusString(EntityID peepId)
		{

			if (!IsInJail(peepId))
			{
				return null;
			}
			var (num, num2, flag) = GetArrestInfo(peepId);
			if (num > 0)
			{
				if (flag)
				{
					return $"Arrested - Trial in {num} days (Bribed)";
				}
				return $"Arrested - Trial in {num} days (Bribe: ${num2})";
			}
			var (num3, num4) = GetImprisonmentInfo(peepId);
			if (num3 > 0)
			{
				return $"In Prison - {num3} days remaining ({num4} year sentence)";
			}
			return "In Jail";
		}
	}

	private static class SaveLoadPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Services.SaveManager");
				if (!(type == null))
				{
					MethodInfo method = type.GetMethod("LoadGame", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(SaveLoadPatch), "LoadPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					}
					MethodInfo method2 = type.GetMethod("SaveGame", BindingFlags.Instance | BindingFlags.Public);
					if (method2 != null)
					{
						harmony.Patch((MethodBase)method2, (HarmonyMethod)null, new HarmonyMethod(typeof(SaveLoadPatch), "SavePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					}
					Debug.Log("[GameplayTweaks] Save/Load enabled");
				}
			}
			catch
			{
			}
		}

		private static void LoadPostfix(string saveName)
		{
			LoadModData(saveName);
			_lastGangTrackDay = -1;
		}

		private static void SavePostfix(string saveName)
		{
			SaveModData();
		}
	}

	internal static ConfigEntry<bool> EnableSpouseEthnicity;

	internal static ConfigEntry<float> SpouseEthnicityChance;

	internal static ConfigEntry<bool> EnableHireableAge;

	internal static ConfigEntry<float> HireableMinAge;

	internal static ConfigEntry<int> MarriageMinAge;

	internal static ConfigEntry<int> MarriageMaxAgeDiff;

	internal static ConfigEntry<bool> EnableCrewStats;

	internal static ConfigEntry<bool> EnableAIAlliances;

	internal static ConfigEntry<bool> EnableDirtyCash;

	internal static bool ForceSameEthnicity;

	internal static System.Random SharedRng = new System.Random();

	internal static ModSaveData SaveData = new ModSaveData();

	private static string _saveFilePath;

	internal static GameplayTweaksPlugin Instance;

	internal static List<PlayerInfo> TrackedGangs = new List<PlayerInfo>();

	private static int _lastGangTrackDay = -1;

	private static bool _hasOwnPact;

	private static Canvas _modOverlayCanvas;

	internal static bool HasPlayerPact
	{
		get
		{
			if (SaveData.PlayerJoinedPactIndex < 0)
			{
				return _hasOwnPact;
			}
			return true;
		}
	}

	internal static void RefreshPactCache()
	{
		_hasOwnPact = SaveData.Pacts.Any((AlliancePact p) => p.ColorIndex == 6);
	}

	internal static void RefreshGangTracker()
	{

		TrackedGangs.Clear();
		try
		{
			foreach (PlayerInfo allPlayer in G.GetAllPlayers())
			{
				if (allPlayer != null)
				{
					PlayerID pID = allPlayer.PID;
					if (!pID.IsHumanPlayer && allPlayer.crew != null && !allPlayer.crew.IsCrewDefeated && allPlayer.crew.LivingCrewCount > 0)
					{
						TrackedGangs.Add(allPlayer);
					}
				}
			}
			RefreshPactCache();
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Gang tracker error: {arg}");
		}
	}

	internal static int ReadFixnum(object qty)
	{
		if (qty == null)
		{
			return 0;
		}
		try
		{
			return Convert.ToInt32(qty);
		}
		catch
		{
			try
			{
				FieldInfo fieldInfo = qty.GetType().GetField("v") ?? qty.GetType().GetField("_value") ?? qty.GetType().GetField("raw");
				if (fieldInfo != null)
				{
					return Convert.ToInt32(fieldInfo.GetValue(qty));
				}
			}
			catch
			{
			}
			return 0;
		}
	}

	internal static int ReadInventoryAmount(Entity entity, string labelName)
	{

		if (entity == null)
		{
			return 0;
		}
		try
		{
			InventoryModule inventory = ModulesUtil.GetInventory(entity);
			if (((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory)?.data == null)
			{
				return 0;
			}
			Label val = default(Label);
			val = new Label(labelName);
			return ReadFixnum(((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory).data.Get(val).qty);
		}
		catch
		{
			return 0;
		}
	}

	internal static int GetPlayerCleanCash()
	{
		try
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return 0;
			}
			return (int)humanPlayer.finances.GetMoneyTotal();
		}
		catch (Exception ex)
		{
			Debug.LogWarning(("[GameplayTweaks] GetPlayerCleanCash failed: " + ex.Message));
			return 0;
		}
	}

	internal static void AddDirtyCash(Entity entity, int amount)
	{

		if (entity == null || amount <= 0)
		{
			return;
		}
		try
		{
			InventoryModule inventory = ModulesUtil.GetInventory(entity);
			if (((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory)?.data != null)
			{
				Label val = default(Label);
				val = new Label(ModConstants.DIRTY_CASH_LABEL);
				((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory).data.Increment(val, (Fixnum)(amount));
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] AddDirtyCash failed: {arg}");
		}
	}

	internal static int RemoveDirtyCash(Entity entity, int amount)
	{

		if (entity == null || amount <= 0)
		{
			return 0;
		}
		try
		{
			InventoryModule inventory = ModulesUtil.GetInventory(entity);
			if (((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory)?.data == null)
			{
				return 0;
			}
			Label val = default(Label);
			val = new Label(ModConstants.DIRTY_CASH_LABEL);
			int num = Math.Min(ReadFixnum(((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory).data.Get(val).qty), amount);
			if (num > 0)
			{
				inventory.TryRemoveResourcesUpToAll(val, num);
			}
			return num;
		}
		catch
		{
			return 0;
		}
	}

	internal static int GetTotalDirtyCash()
	{

		int num = 0;
		try
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return 0;
			}
			EntityID safehouse = humanPlayer.territory.Safehouse;
			if (!safehouse.IsNotValid)
			{
				Entity entity = EntityIDExtensions.FindEntity(safehouse);
				num += ReadInventoryAmount(entity, ModConstants.DIRTY_CASH_LABEL);
			}
			try
			{
				PlayerTerritory territory = humanPlayer.territory;
				if (territory != null)
				{
					FieldInfo bldgField = territory.GetType().GetField("_buildings", BindingFlags.Instance | BindingFlags.NonPublic)
						?? territory.GetType().GetField("buildings", BindingFlags.Instance | BindingFlags.NonPublic);
					PropertyInfo bldgProp = territory.GetType().GetProperty("OwnedBuildings", BindingFlags.Instance | BindingFlags.Public)
						?? territory.GetType().GetProperty("Buildings", BindingFlags.Instance | BindingFlags.Public);
					object bldgList = bldgField?.GetValue(territory) ?? bldgProp?.GetValue(territory);
					if (bldgList is IEnumerable<EntityID> buildings)
					{
						foreach (EntityID bldgId in buildings)
						{
							if (bldgId == safehouse || bldgId.IsNotValid) continue;
							Entity bldgEntity = EntityIDExtensions.FindEntity(bldgId);
							if (bldgEntity != null)
							{
								num += ReadInventoryAmount(bldgEntity, ModConstants.DIRTY_CASH_LABEL);
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
		return num;
	}

	private void Awake()
	{

		Instance = this;
		EnableSpouseEthnicity = ((BaseUnityPlugin)this).Config.Bind<bool>("SpouseEthnicity", "Enabled", true, "Spouses share ethnicity.");
		SpouseEthnicityChance = ((BaseUnityPlugin)this).Config.Bind<float>("SpouseEthnicity", "SameEthnicityChance", 0.8f, "Probability same ethnicity.");
		EnableHireableAge = ((BaseUnityPlugin)this).Config.Bind<bool>("HireableAge", "Enabled", true, "Change min hire age.");
		HireableMinAge = ((BaseUnityPlugin)this).Config.Bind<float>("HireableAge", "MinAge", 16f, "Minimum age to hire.");
		MarriageMinAge = ((BaseUnityPlugin)this).Config.Bind<int>("Marriage", "MinAge", 18, "Minimum marriage age.");
		MarriageMaxAgeDiff = ((BaseUnityPlugin)this).Config.Bind<int>("Marriage", "MaxAgeDifference", 10, "Max age difference.");
		EnableCrewStats = ((BaseUnityPlugin)this).Config.Bind<bool>("CrewStats", "Enabled", true, "Enable crew stat tracking.");
		EnableAIAlliances = ((BaseUnityPlugin)this).Config.Bind<bool>("AIAlliances", "Enabled", true, "Enable AI alliances.");
		EnableDirtyCash = ((BaseUnityPlugin)this).Config.Bind<bool>("DirtyCash", "Enabled", true, "Enable dirty cash economy.");
		Harmony val = new Harmony("com.mods.gameplaytweaks");
		val.PatchAll(typeof(SpouseEthnicityLinkPatch));
		val.PatchAll(typeof(SpouseEthnicityCandidatePatch));
		FindRandoToMarryPatch.ApplyPatch(val);
		HireableAgePatch.ApplyManualDetour();
		CrewRelationshipHandlerPatch.ApplyPatch(val);
		StatTrackingPatch.ApplyPatch(val);
		TurnUpdatePatch.ApplyPatch(val);
		AICrewCapPatch.ApplyPatch(val);
		TerritoryColorPatch.ApplyPatch(val);
		BossArrestPatch.ApplyPatch(val);
		JailSystem.Initialize();
		SaveLoadPatch.ApplyPatch(val);
		AttackAdvisorPatch.ApplyPatch(val);
		DirtyCashPatches.ApplyPatches(val);
		FrontTrackingPatch.ApplyPatch(val);
		KeyboardBlockerPatch.ApplyPatch(val);
		// CopWarSystem and CombatNameDisplayPatch moved to CopKilling mod
		CheckForConflictingMods();
		Debug.Log("[GameplayTweaks] Gameplay Tweaks Extended loaded");
	}

	private void CheckForConflictingMods()
	{
		string text = Path.Combine(Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location), "");
		try
		{
			text = Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location);
		}
		catch
		{
			return;
		}
		string[] array = new string[3] { "DirtyCashEconomy.dll", "DirtyCashVolumeFix.dll", "Safebox.dll" };
		foreach (string text2 in array)
		{
			if (File.Exists(Path.Combine(text, text2)))
			{
				Debug.LogWarning(("[GameplayTweaks] WARNING: '" + text2 + "' detected in plugins folder. This mod now includes dirty cash features. Remove '" + text2 + "' to avoid conflicts."));
			}
		}
	}

	private void Update()
	{
		if (!Input.GetKeyDown((KeyCode)112))
		{
			return;
		}
		if (InputFieldBlocker.ShouldBlockKeyboard)
		{
			return;
		}
		if ((UnityEngine.Object)(object)EventSystem.current != (UnityEngine.Object)null)
		{
			GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
			if ((UnityEngine.Object)(object)currentSelectedGameObject != (UnityEngine.Object)null)
			{
				if ((UnityEngine.Object)(object)currentSelectedGameObject.GetComponent<InputField>() != (UnityEngine.Object)null)
				{
					return;
				}
				if ((UnityEngine.Object)(object)currentSelectedGameObject.GetComponent<TMP_InputField>() != (UnityEngine.Object)null)
				{
					return;
				}
			}
		}
		Debug.Log("[GameplayTweaks] P key pressed - toggling crew relations popup");
		CrewRelationshipHandlerPatch.ToggleCrewRelationsPopup();
	}

	internal static CrewModState GetOrCreateCrewState(EntityID crewId)
	{
		long num = (crewId.IsNotValid ? (-1L) : ((long)crewId.id));
		if (num < 0)
		{
			return null;
		}
		if (!SaveData.CrewStates.TryGetValue(num, out var value))
		{
			value = new CrewModState();
			SaveData.CrewStates[num] = value;
		}
		return value;
	}

	internal static CrewModState GetCrewStateOrNull(EntityID crewId)
	{
		long num = (crewId.IsNotValid ? (-1L) : ((long)crewId.id));
		if (num < 0)
		{
			return null;
		}
		SaveData.CrewStates.TryGetValue(num, out var value);
		return value;
	}

	internal static AlliancePact GetPactForPlayer(PlayerID pid)
	{

		return SaveData.Pacts.FirstOrDefault((AlliancePact p) => p.IsMember(pid));
	}

	internal static void LogGrapevine(string msg)
	{
		SaveData.GrapevineEvents.Insert(0, msg);
		if (SaveData.GrapevineEvents.Count > 50)
		{
			SaveData.GrapevineEvents.RemoveRange(50, SaveData.GrapevineEvents.Count - 50);
		}
	}

	internal static int GetBribeCost(WantedLevel level)
	{
		return 520 * (1 << (int)level);
	}

	internal static void GrantStreetCredit(PlayerInfo player, int amount)
	{

		try
		{
			EntityID safehouse = player.territory.Safehouse;
			if (safehouse.IsNotValid)
			{
				Debug.LogWarning("[GameplayTweaks] No safehouse found for street credit grant");
				return;
			}
			Entity val = EntityIDExtensions.FindEntity(safehouse);
			if (val == null)
			{
				Debug.LogWarning("[GameplayTweaks] Safehouse entity not found");
				return;
			}
			InventoryModule inventory = ModulesUtil.GetInventory(val);
			if (inventory != null)
			{
				Label val2 = default(Label);
				val2 = new Label("streetcredit");
				int num = inventory.ForceAddResourcesRegardlessOfSpace(val2, amount);
				Debug.Log($"[GameplayTweaks] Granted {num} streetcredit to safehouse (requested: {amount})");
			}
			else
			{
				Debug.LogWarning("[GameplayTweaks] Safehouse inventory not found");
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] GrantStreetCredit failed: {arg}");
		}
	}

	internal static void SaveModData()
	{
		try
		{
			if (string.IsNullOrEmpty(_saveFilePath))
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("{\"NextPactId\":" + SaveData.NextPactId + ",\"PlayerPactId\":" + SaveData.PlayerPactId + ",\"PJI\":" + SaveData.PlayerJoinedPactIndex + ",\"LPJD\":" + SaveData.LastPactJoinDay + ",\"NAP\":" + SaveData.NeverAcceptPacts.ToString().ToLower() + ",\"LOD\":" + CrewRelationshipHandlerPatch._lastOutingDay + ",\"GMB\":" + CrewRelationshipHandlerPatch._globalMayorBribeActive.ToString().ToLower() + ",\"GMBD\":" + CrewRelationshipHandlerPatch._globalMayorBribeExpireDay + ",\"CrewStates\":{");
			bool flag = true;
			foreach (KeyValuePair<long, CrewModState> crewState in SaveData.CrewStates)
			{
				if (!flag)
				{
					stringBuilder.Append(",");
				}
				flag = false;
				CrewModState value = crewState.Value;
				stringBuilder.Append($"\"{crewState.Key}\":{{\"SCP\":{value.StreetCreditProgress},\"SCL\":{value.StreetCreditLevel},");
				stringBuilder.Append($"\"WL\":{(int)value.WantedLevel},\"WP\":{value.WantedProgress},\"MBA\":{value.MayorBribeActive.ToString().ToLower()},");
				stringBuilder.Append($"\"JBA\":{value.JudgeBribeActive.ToString().ToLower()},\"BER\":{value.BribeExpiresRaw},\"HV\":{value.HappinessValue},");
				stringBuilder.Append($"\"TU\":{value.TurnsUnhappy},\"OV\":{value.OnVacation.ToString().ToLower()},\"VRR\":{value.VacationReturnsRaw},");
				stringBuilder.Append("\"IU\":" + value.IsUnderboss.ToString().ToLower() + ",\"ACB\":" + value.AwaitingChildBirth.ToString().ToLower() + ",");
				stringBuilder.Append($"\"LFKC\":{value.LastFutureKidsCount},\"LBSC\":{value.LastBoozeSoldCount},");
				stringBuilder.Append($"\"VP\":{value.VacationPending.ToString().ToLower()},\"VD\":{value.VacationDuration},");
				stringBuilder.Append($"\"FAC\":{value.FedArrivalCountdown},\"FI\":{value.FedsIncoming.ToString().ToLower()},");
				stringBuilder.Append("\"HW\":" + value.HasWitness.ToString().ToLower() + ",\"WTS\":" + value.WitnessThreatenedSuccessfully.ToString().ToLower() + ",");
				stringBuilder.Append($"\"WTA\":{value.WitnessThreatAttempted.ToString().ToLower()},\"EJY\":{value.ExtraJailYears},");
				stringBuilder.Append($"\"LR\":{value.LawyerRetainer},\"CD\":{value.CaseDismissed.ToString().ToLower()},\"LBST\":{value.LastBoozeSellTurn}}}");
			}
			stringBuilder.Append("},\"Pacts\":[");
			bool flag2 = true;
			foreach (AlliancePact pact in SaveData.Pacts)
			{
				if (!flag2)
				{
					stringBuilder.Append(",");
				}
				flag2 = false;
				stringBuilder.Append(string.Format("{{\"PI\":\"{0}\",\"PN\":\"{1}\",\"CI\":{2},", pact.PactId, EscapeJsonString(pact.PactName ?? ""), pact.ColorIndex));
				stringBuilder.Append($"\"LG\":{pact.LeaderGangId},\"CR\":{pact.ColorR},\"CG\":{pact.ColorG},\"CB\":{pact.ColorB},");
				stringBuilder.Append($"\"FD\":{pact.FormedDays},\"IP\":{pact.IsPending.ToString().ToLower()},\"PV\":{pact.PlayerInvited.ToString().ToLower()},");
				stringBuilder.Append("\"MI\":[");
				for (int i = 0; i < pact.MemberIds.Count; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append(",");
					}
					stringBuilder.Append(pact.MemberIds[i]);
				}
				stringBuilder.Append("]}");
			}
			stringBuilder.Append("],\"GV\":[");
			for (int j = 0; j < SaveData.GrapevineEvents.Count; j++)
			{
				if (j > 0)
				{
					stringBuilder.Append(",");
				}
				stringBuilder.Append("\"" + EscapeJsonString(SaveData.GrapevineEvents[j]) + "\"");
			}
			stringBuilder.AppendLine("]}");
			File.WriteAllText(_saveFilePath, stringBuilder.ToString());
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Save failed: {arg}");
		}
	}

	private static string EscapeJsonString(string s)
	{
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	internal static void LoadModData(string saveName)
	{
		try
		{
			_saveFilePath = Path.Combine(Path.Combine(Application.persistentDataPath, "Saves"), saveName + "_tweaks.json");
			SaveData = new ModSaveData();
			if (!File.Exists(_saveFilePath))
			{
				Debug.Log("[GameplayTweaks] No save file found, starting fresh");
				return;
			}
			string text = File.ReadAllText(_saveFilePath);
			SaveData.NextPactId = JInt(text, "NextPactId", 0);
			SaveData.PlayerPactId = JInt(text, "PlayerPactId", -1);
			SaveData.PlayerJoinedPactIndex = JInt(text, "PJI", -1);
			SaveData.LastPactJoinDay = JInt(text, "LPJD", -1);
			SaveData.NeverAcceptPacts = JBool(text, "NAP", d: false);
			CrewRelationshipHandlerPatch._lastOutingDay = JInt(text, "LOD", -1);
			CrewRelationshipHandlerPatch._globalMayorBribeActive = JBool(text, "GMB", d: false);
			CrewRelationshipHandlerPatch._globalMayorBribeExpireDay = JInt(text, "GMBD", -1);
			int num = text.IndexOf("\"CrewStates\":{");
			if (num >= 0)
			{
				int num2 = text.IndexOf('{', num + 13);
				int num3 = MatchBrace(text, num2);
				if (num3 > num2)
				{
					string text2 = text.Substring(num2 + 1, num3 - num2 - 1);
					int num4 = 0;
					while (num4 < text2.Length)
					{
						int num5 = text2.IndexOf('"', num4);
						if (num5 < 0)
						{
							break;
						}
						int num6 = text2.IndexOf('"', num5 + 1);
						if (num6 < 0)
						{
							break;
						}
						string s = text2.Substring(num5 + 1, num6 - num5 - 1);
						int num7 = text2.IndexOf('{', num6);
						if (num7 < 0)
						{
							break;
						}
						int num8 = MatchBrace(text2, num7);
						if (num8 < 0)
						{
							break;
						}
						string j = text2.Substring(num7, num8 - num7 + 1);
						CrewModState crewModState = new CrewModState();
						crewModState.StreetCreditProgress = JFloat(j, "SCP", 0f);
						crewModState.StreetCreditLevel = JInt(j, "SCL", 0);
						crewModState.WantedLevel = (WantedLevel)JInt(j, "WL", 0);
						crewModState.WantedProgress = JFloat(j, "WP", 0f);
						crewModState.MayorBribeActive = JBool(j, "MBA", d: false);
						crewModState.JudgeBribeActive = JBool(j, "JBA", d: false);
						crewModState.BribeExpiresRaw = JInt(j, "BER", 0);
						crewModState.HappinessValue = JFloat(j, "HV", 1f);
						crewModState.TurnsUnhappy = JInt(j, "TU", 0);
						crewModState.OnVacation = JBool(j, "OV", d: false);
						crewModState.VacationReturnsRaw = JInt(j, "VRR", 0);
						crewModState.IsUnderboss = JBool(j, "IU", d: false);
						crewModState.AwaitingChildBirth = JBool(j, "ACB", d: false);
						crewModState.LastFutureKidsCount = JInt(j, "LFKC", 0);
						crewModState.LastBoozeSoldCount = JInt(j, "LBSC", 0);
						crewModState.VacationPending = JBool(j, "VP", d: false);
						crewModState.VacationDuration = JInt(j, "VD", 0);
						crewModState.FedArrivalCountdown = JInt(j, "FAC", 0);
						crewModState.FedsIncoming = JBool(j, "FI", d: false);
						crewModState.HasWitness = JBool(j, "HW", d: false);
						crewModState.WitnessThreatenedSuccessfully = JBool(j, "WTS", d: false);
						crewModState.WitnessThreatAttempted = JBool(j, "WTA", d: false);
						crewModState.ExtraJailYears = JInt(j, "EJY", 0);
						crewModState.LawyerRetainer = JInt(j, "LR", 0);
						crewModState.CaseDismissed = JBool(j, "CD", d: false);
						crewModState.LastBoozeSellTurn = JInt(j, "LBST", 0);
						if (long.TryParse(s, out var result))
						{
							SaveData.CrewStates[result] = crewModState;
						}
						num4 = num8 + 1;
					}
				}
			}
			int num9 = text.IndexOf("\"Pacts\":[");
			if (num9 >= 0)
			{
				int num10 = text.IndexOf('[', num9);
				int num11 = MatchBracket(text, num10);
				if (num11 > num10)
				{
					string text3 = text.Substring(num10 + 1, num11 - num10 - 1);
					int num12 = 0;
					while (num12 < text3.Length)
					{
						int num13 = text3.IndexOf('{', num12);
						if (num13 < 0)
						{
							break;
						}
						int num14 = MatchBrace(text3, num13);
						if (num14 < 0)
						{
							break;
						}
						string text4 = text3.Substring(num13, num14 - num13 + 1);
						AlliancePact alliancePact = new AlliancePact();
						alliancePact.PactId = JStr(text4, "PI", "");
						alliancePact.PactName = JStr(text4, "PN", "");
						alliancePact.ColorIndex = JInt(text4, "CI", 0);
						alliancePact.LeaderGangId = JInt(text4, "LG", -1);
						alliancePact.ColorR = JFloat(text4, "CR", 1f);
						alliancePact.ColorG = JFloat(text4, "CG", 1f);
						alliancePact.ColorB = JFloat(text4, "CB", 1f);
						alliancePact.FormedDays = JInt(text4, "FD", 0);
						alliancePact.IsPending = JBool(text4, "IP", d: false);
						alliancePact.PlayerInvited = JBool(text4, "PV", d: false);
						int num15 = text4.IndexOf("\"MI\":[");
						if (num15 >= 0)
						{
							int num16 = text4.IndexOf('[', num15);
							int num17 = text4.IndexOf(']', num16);
							if (num17 > num16 + 1)
							{
								string[] array = text4.Substring(num16 + 1, num17 - num16 - 1).Split(',');
								for (int i = 0; i < array.Length; i++)
								{
									if (int.TryParse(array[i].Trim(), out var result2))
									{
										alliancePact.MemberIds.Add(result2);
									}
								}
							}
						}
						SaveData.Pacts.Add(alliancePact);
						num12 = num14 + 1;
					}
				}
			}
			int num18 = text.IndexOf("\"GV\":[");
			if (num18 >= 0)
			{
				int num19 = text.IndexOf('[', num18);
				int num20 = MatchBracket(text, num19);
				if (num20 > num19 + 1)
				{
					string text5 = text.Substring(num19 + 1, num20 - num19 - 1);
					int num21 = 0;
					while (num21 < text5.Length)
					{
						int num22 = text5.IndexOf('"', num21);
						if (num22 < 0)
						{
							break;
						}
						int num23 = text5.IndexOf('"', num22 + 1);
						if (num23 < 0)
						{
							break;
						}
						SaveData.GrapevineEvents.Add(text5.Substring(num22 + 1, num23 - num22 - 1));
						num21 = num23 + 1;
					}
				}
			}
			RefreshPactCache();
			Debug.Log($"[GameplayTweaks] Loaded {SaveData.CrewStates.Count} crew states, {SaveData.Pacts.Count} pacts, {SaveData.GrapevineEvents.Count} grapevine events");
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Load failed: {arg}");
			SaveData = new ModSaveData();
		}
	}

	private static int MatchBrace(string s, int open)
	{
		int num = 0;
		for (int i = open; i < s.Length; i++)
		{
			if (s[i] == '{')
			{
				num++;
			}
			else if (s[i] == '}')
			{
				num--;
				if (num == 0)
				{
					return i;
				}
			}
		}
		return -1;
	}

	private static int MatchBracket(string s, int open)
	{
		int num = 0;
		for (int i = open; i < s.Length; i++)
		{
			if (s[i] == '[')
			{
				num++;
			}
			else if (s[i] == ']')
			{
				num--;
				if (num == 0)
				{
					return i;
				}
			}
		}
		return -1;
	}

	private static string JRaw(string json, string key)
	{
		string text = "\"" + key + "\":";
		int num = json.IndexOf(text);
		if (num < 0)
		{
			return null;
		}
		int num2 = num + text.Length;
		int i;
		for (i = num2; i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']'; i++)
		{
		}
		return json.Substring(num2, i - num2).Trim();
	}

	private static int JInt(string j, string k, int d)
	{
		string text = JRaw(j, k);
		if (text == null || !int.TryParse(text, out var result))
		{
			return d;
		}
		return result;
	}

	private static float JFloat(string j, string k, float d)
	{
		string text = JRaw(j, k);
		if (text == null || !float.TryParse(text, out var result))
		{
			return d;
		}
		return result;
	}

	private static bool JBool(string j, string k, bool d)
	{
		string text = JRaw(j, k);
		if (text == null)
		{
			return d;
		}
		return text == "true";
	}

	private static string JStr(string j, string k, string d)
	{
		string text = "\"" + k + "\":\"";
		int num = j.IndexOf(text);
		if (num < 0)
		{
			return d;
		}
		int num2 = num + text.Length;
		int num3 = j.IndexOf('"', num2);
		if (num3 <= num2)
		{
			return d;
		}
		return j.Substring(num2, num3 - num2);
	}

	internal static int CalculateGangPower(PlayerInfo player)
	{
		if (player == null)
		{
			return 0;
		}
		PlayerCrew crew = player.crew;
		return ((crew != null) ? crew.LivingCrewCount : 0) * 10 + 25;
	}

	internal static Canvas GetOrCreateOverlayCanvas()
	{

		if ((UnityEngine.Object)(object)_modOverlayCanvas != (UnityEngine.Object)null && (UnityEngine.Object)(object)((Component)_modOverlayCanvas).gameObject != (UnityEngine.Object)null)
		{
			return _modOverlayCanvas;
		}
		GameObject val = new GameObject("ModOverlayCanvas", new Type[3]
		{
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster)
		});
		UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)val);
		_modOverlayCanvas = val.GetComponent<Canvas>();
		_modOverlayCanvas.renderMode = (RenderMode)0;
		_modOverlayCanvas.sortingOrder = 990;
		CanvasScaler component = val.GetComponent<CanvasScaler>();
		component.uiScaleMode = (CanvasScaler.ScaleMode)1;
		component.referenceResolution = new Vector2(1920f, 1080f);
		return _modOverlayCanvas;
	}
}
internal static class AttackAdvisorPatch
{
	public static void ApplyPatch(Harmony harmony)
	{

		try
		{
			Type type = typeof(GameClock).Assembly.GetType("Game.Session.Player.AI.AttackAdvisor");
			if (type == null)
			{
				Debug.LogWarning("[GameplayTweaks] AttackAdvisor type not found");
				return;
			}
			MethodInfo method = type.GetMethod("TrySellOutToFeds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				Debug.LogWarning("[GameplayTweaks] TrySellOutToFeds method not found");
				return;
			}
			harmony.Patch((MethodBase)method, new HarmonyMethod(typeof(AttackAdvisorPatch), "TrySellOutToFedsPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Debug.Log("[GameplayTweaks] AttackAdvisor.TrySellOutToFeds crash fix applied");
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] AttackAdvisorPatch failed: {arg}");
		}
	}

	private static bool TrySellOutToFedsPrefix(object __instance)
	{
		try
		{
			FieldInfo fieldInfo = __instance.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic) ?? __instance.GetType().GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);
			if (fieldInfo == null)
			{
				return false;
			}
			object value = fieldInfo.GetValue(__instance);
			if (value == null)
			{
				return false;
			}
			PropertyInfo propertyInfo = value.GetType().GetProperty("territory") ?? value.GetType().GetProperty("Territory");
			object obj = null;
			if (propertyInfo != null)
			{
				obj = propertyInfo.GetValue(value);
			}
			else
			{
				FieldInfo fieldInfo2 = value.GetType().GetField("territory") ?? value.GetType().GetField("_territory", BindingFlags.Instance | BindingFlags.NonPublic);
				if (fieldInfo2 != null)
				{
					obj = fieldInfo2.GetValue(value);
				}
			}
			if (obj == null)
			{
				return false;
			}
			MethodInfo method = obj.GetType().GetMethod("GetHeadquartersNode");
			if (method != null && method.Invoke(obj, null) == null)
			{
				return false;
			}
			FieldInfo field = __instance.GetType().GetField("_def", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
			{
				object value2 = field.GetValue(__instance);
				if (value2 != null)
				{
					object obj2 = null;
					PropertyInfo property = value2.GetType().GetProperty("sellOutToFeds");
					if (property != null)
					{
						obj2 = property.GetValue(value2);
					}
					else
					{
						FieldInfo field2 = value2.GetType().GetField("sellOutToFeds");
						if (field2 != null)
						{
							obj2 = field2.GetValue(value2);
						}
					}
					if (obj2 == null)
					{
						return false;
					}
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning(("[GameplayTweaks] TrySellOutToFeds safety check failed: " + ex.Message));
			return false;
		}
	}
}
internal static class DirtyCashPatches
{
	public static void ApplyPatches(Harmony harmony)
	{

		if (!GameplayTweaksPlugin.EnableDirtyCash.Value)
		{
			return;
		}
		try
		{
			Type type = typeof(GameClock).Assembly.GetType("Game.Services.Resource");
			if (type != null)
			{
				MethodInfo method = type.GetMethod("FindTotalVolume", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(DirtyCashPatches), "VolumePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Dirty cash volume fix applied");
				}
			}
			MethodInfo[] methods = typeof(PlayerFinances).GetMethods(BindingFlags.Instance | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name == "DoChangeMoney")
				{
					ParameterInfo[] parameters = methodInfo.GetParameters();
					if (parameters.Length >= 3)
					{
						harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(DirtyCashPatches), "DoChangeMoneyPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log($"[GameplayTweaks] Dirty cash income conversion patched (params: {parameters.Length})");
						break;
					}
				}
			}
			Debug.Log("[GameplayTweaks] Dirty cash economy initialized");
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] DirtyCashPatches failed: {arg}");
		}
	}

	private static void VolumePostfix(object __instance, ref object __result)
	{
		try
		{
			string text = __instance.ToString();
			if (text != null && text.Contains("dirty-cash"))
			{
				ConstructorInfo constructor = __result.GetType().GetConstructor(new Type[1] { typeof(int) });
				if (constructor != null)
				{
					__result = constructor.Invoke(new object[1] { 1 });
				}
			}
		}
		catch
		{
		}
	}

	private static bool DoChangeMoneyPrefix(object __instance, object[] __args)
	{

		try
		{
			if (__args == null || __args.Length < 2)
			{
				return true;
			}
			object obj = null;
			object obj2 = null;
			foreach (object obj3 in __args)
			{
				if (obj3 != null)
				{
					string name = obj3.GetType().Name;
					if (name == "MoneyReason" || name.Contains("MoneyReason"))
					{
						obj = obj3;
					}
					else if (name == "Price")
					{
						obj2 = obj3;
					}
				}
			}
			if (obj == null || obj2 == null)
			{
				return true;
			}
			int num = Convert.ToInt32(obj);
			switch (num)
			{
			default:
				if (num != 80 && num != 60 && num != 61)
				{
					return true;
				}
				break;
			case 40:
			case 41:
			case 50:
			case 51:
			case 71:
			case 72:
			case 73:
			case 74:
			case 75:
				break;
			}
			int num2 = 0;
			try
			{
				FieldInfo field = obj2.GetType().GetField("cash");
				if (field != null)
				{
					num2 = GameplayTweaksPlugin.ReadFixnum(field.GetValue(obj2));
				}
			}
			catch
			{
			}
			if (num2 <= 0)
			{
				return true;
			}
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return true;
			}
			FieldInfo field2 = __instance.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field2 != null)
			{
				object value = field2.GetValue(__instance);
				PlayerInfo val = value as PlayerInfo;
				if (val != null && val.PID.id != humanPlayer.PID.id)
				{
					return true;
				}
			}
			EntityID safehouse = humanPlayer.territory.Safehouse;
			if (safehouse.IsNotValid)
			{
				return true;
			}
			Entity val2 = EntityIDExtensions.FindEntity(safehouse);
			if (val2 == null)
			{
				return true;
			}
			GameplayTweaksPlugin.AddDirtyCash(val2, num2);
			return false;
		}
		catch
		{
			return true;
		}
	}

	internal static void ProcessLaundering()
	{

		try
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
			{
				return;
			}
			EntityID safehouse = humanPlayer.territory.Safehouse;
			if (safehouse.IsNotValid)
			{
				return;
			}
			Entity val = EntityIDExtensions.FindEntity(safehouse);
			if (val == null)
			{
				return;
			}
			int num = GameplayTweaksPlugin.ReadInventoryAmount(val, ModConstants.DIRTY_CASH_LABEL);
			if (num <= 0)
			{
				return;
			}
			int amount = Math.Max(1, (int)((float)num * 0.05f));
			int num2 = GameplayTweaksPlugin.RemoveDirtyCash(val, amount);
			if (num2 > 0)
			{
				humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(num2)), (MoneyReason)1);
				if (num2 >= 10)
				{
					Debug.Log($"[GameplayTweaks] Laundered ${num2} dirty cash");
				}
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Laundering failed: {arg}");
		}
	}
}
internal static class FrontTrackingPatch
{
	public static void ApplyPatch(Harmony harmony)
	{

		try
		{
			MethodInfo method = typeof(PlayerTerritory).GetMethod("PerformTakeover", BindingFlags.Instance | BindingFlags.Public);
			if (method != null)
			{
				harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(FrontTrackingPatch), "TakeoverPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				Debug.Log("[GameplayTweaks] Front tracking patch applied");
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] FrontTrackingPatch failed: {arg}");
		}
	}

	private static void TakeoverPostfix(object __instance, Entity __result)
	{
		try
		{
			if (__result == null)
			{
				return;
			}
			PlayerTerritory val = __instance as PlayerTerritory;
			if (val != null)
			{
				PlayerInfo playerInfo = ((PlayerSubmanager)val).PlayerInfo;
				if (playerInfo != null)
				{
					PlayerSocial social = playerInfo.social;
					string text = ((social != null) ? social.PlayerGroupName : null) ?? $"Gang #{playerInfo.PID.id}";
					string text2 = ((object)__result).ToString() ?? "a building";
					GameplayTweaksPlugin.LogGrapevine("FRONT: " + text + " opened a front at " + text2);
				}
			}
		}
		catch
		{
		}
	}
}
internal static class KeyboardBlockerPatch
{
	public static void ApplyPatch(Harmony harmony)
	{

		try
		{
			Type type = typeof(GameClock).Assembly.GetType("Game.Services.Input.KeyboardService");
			bool flag = false;
			if (type != null)
			{
				MethodInfo method = type.GetMethod("OnUpdateKeyboard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					harmony.Patch((MethodBase)method, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (OnUpdateKeyboard)");
					flag = true;
				}
				MethodInfo method2 = type.GetMethod("ProcessKeys", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method2 != null)
				{
					harmony.Patch((MethodBase)method2, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (ProcessKeys)");
					flag = true;
				}
				MethodInfo method3 = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method3 != null)
				{
					harmony.Patch((MethodBase)method3, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (Update)");
					flag = true;
				}
			}
			Type type2 = typeof(GameClock).Assembly.GetType("Game.Services.Input.KeyboardHandler");
			if (type2 != null)
			{
				MethodInfo method4 = type2.GetMethod("ProcessEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method4 != null)
				{
					harmony.Patch((MethodBase)method4, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (ProcessEvent)");
					flag = true;
				}
			}
			if (!flag)
			{
				Debug.LogWarning("[GameplayTweaks] Could not find any keyboard methods to patch!");
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] KeyboardBlockerPatch failed: {arg}");
		}
	}

	private static bool BlockKeyboardPrefix()
	{
		if (InputFieldBlocker.ShouldBlockKeyboard)
		{
			return false;
		}
		return true;
	}
}
public class InputFieldBlocker : MonoBehaviour
{
	private InputField _inputField;

	private TMP_InputField _tmpInputField;

	public static bool ShouldBlockKeyboard
	{
		get
		{
			try
			{
				EventSystem current = EventSystem.current;
				if ((UnityEngine.Object)(object)current != (UnityEngine.Object)null && (UnityEngine.Object)(object)current.currentSelectedGameObject != (UnityEngine.Object)null)
				{
					GameObject currentSelectedGameObject = current.currentSelectedGameObject;
					InputField component = currentSelectedGameObject.GetComponent<InputField>();
					if ((UnityEngine.Object)(object)component != (UnityEngine.Object)null && component.isFocused)
					{
						return true;
					}
					TMP_InputField component2 = currentSelectedGameObject.GetComponent<TMP_InputField>();
					if ((UnityEngine.Object)(object)component2 != (UnityEngine.Object)null && component2.isFocused)
					{
						return true;
					}
				}
				InputField[] array = UnityEngine.Object.FindObjectsOfType<InputField>();
				foreach (InputField val in array)
				{
					if ((UnityEngine.Object)(object)val != (UnityEngine.Object)null && val.isFocused)
					{
						return true;
					}
				}
				TMP_InputField[] array2 = UnityEngine.Object.FindObjectsOfType<TMP_InputField>();
				foreach (TMP_InputField val2 in array2)
				{
					if ((UnityEngine.Object)(object)val2 != (UnityEngine.Object)null && val2.isFocused)
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning(("[GameplayTweaks] ShouldBlockKeyboard check failed: " + ex.Message));
			}
			return false;
		}
	}

	private void Start()
	{
		_inputField = ((Component)this).GetComponent<InputField>();
		_tmpInputField = ((Component)this).GetComponent<TMP_InputField>();
	}
}
public class PopupDragHandler : MonoBehaviour
{
	public RectTransform rectTransform;

	private Vector2 _dragOffset;

	private bool _isDragging;

	private void Update()
	{

		if ((UnityEngine.Object)(object)rectTransform == (UnityEngine.Object)null)
		{
			return;
		}
		Vector2 val = default(Vector2);
		if (Input.GetMouseButtonDown(0) && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, (Vector2)Input.mousePosition, (Camera)null, out val))
		{
			float y = val.y;
			Rect rect = rectTransform.rect;
			if (y > rect.height / 2f - 40f)
			{
				_isDragging = true;
				_dragOffset = (Vector2)rectTransform.position - (Vector2)Input.mousePosition;
			}
		}
		if (Input.GetMouseButtonUp(0))
		{
			_isDragging = false;
		}
		if (_isDragging)
		{
			rectTransform.position = (Vector3)((Vector2)Input.mousePosition + _dragOffset);
		}
		if (Input.GetMouseButtonDown(1))
		{
			gameObject.SetActive(false);
		}
	}
}}
