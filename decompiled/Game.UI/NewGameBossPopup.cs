using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Services.Maps;
using Game.Services.Store;
using Game.Session;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Mouseovers;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public class NewGameBossPopup : BasePopup
{
	private class HideMouseoverOnClick : MonoBehaviour
	{
		public NewGameBossPopup popup;

		private void Update()
		{
			if (popup != null && !(popup._skillDropdown == null) && !(popup._traitDropdown == null) && (popup._skillDropdown.IsExpanded || popup._traitDropdown.IsExpanded))
			{
				Game.serv.mouseovers.OnMouseOut(MouseoverType.CustomGameSkillChoice);
				Game.serv.mouseovers.OnMouseOut(MouseoverType.CustomGameArchetypeChoice);
			}
		}
	}

	private class EthnicityEntry
	{
		public Label eth;

		public string loc;
	}

	public class SkillMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			if (!(Game.serv.ui.TopPopupUnsafe is NewGameBossPopup newGameBossPopup))
			{
				return null;
			}
			SkillDef selectedStarterSkill = newGameBossPopup.GetSelectedStarterSkill();
			if (selectedStarterSkill == null)
			{
				return null;
			}
			return selectedStarterSkill.GetIconAndName() + "\n\n" + selectedStarterSkill.GetDesc();
		}
	}

	public class ArchetypeMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			if (!(Game.serv.ui.TopPopupUnsafe is NewGameBossPopup newGameBossPopup))
			{
				return null;
			}
			GeneratorSettings.TraitType selectedArchetype = newGameBossPopup.GetSelectedArchetype();
			if (selectedArchetype == null)
			{
				return null;
			}
			if (selectedArchetype.traits == null)
			{
				return Loc.Get(selectedArchetype.locdesc);
			}
			List<string> list = new List<string>();
			TraitList traits = Game.serv.globals.settings.people.traits;
			foreach (Label trait2 in selectedArchetype.traits)
			{
				Trait trait = traits.Find(trait2);
				string item = trait.GetLocIcon() + " " + trait.GetLocName();
				list.Add(item);
			}
			return Loc.Get(selectedArchetype.locdesc, "trait1", list[0], "trait2", list[1], "trait3", list[2]);
		}
	}

	private const string CLOSE_BUTTON = "Close/";

	private const string CONTINUE_BUTTON = "Footer/Continue/";

	private const string CANCEL_BUTTON = "Footer/Cancel/";

	private const string CUSTOM_FNAME = "Character/Names/First Name";

	private const string CUSTOM_LNAME = "Character/Names/Last Name";

	private const string CUSTOM_GROUP = "Character/Names/Group Name";

	private const string PORTRAIT = "Character/Portrait/Portrait";

	private const string CHAR_BUTTON = "Character/Reroll";

	private const string GENDER_M = "Character/Gender/Masculine";

	private const string GENDER_F = "Character/Gender/Feminine";

	private const string ARCHETYPE_DROPDOWN = "Character/Trait/Dropdown";

	private const string ETHNICITY = "Character/Ethnicity/Dropdown";

	private const string SKILL_DROPDOWN = "Character/Skill/Dropdown";

	private readonly NewGameStartFunction _continuation;

	private readonly MapConfig _map;

	private readonly GameParameters _parameters;

	private PeepCreationDetails _playerDeets;

	private List<EthnicityEntry> _ethList;

	private Xorshift _rng;

	private List<SkillDef> _skills;

	private List<GeneratorSettings.TraitType> _traitBundles;

	private PortraitInfo _portraitInfo;

	private Sprite _portraitSprite;

	private TMP_InputField _fnameText;

	private TMP_InputField _lnameText;

	private TMP_InputField _groupText;

	private Toggle _genderM;

	private Toggle _genderF;

	private TMP_Dropdown _ethDropdown;

	private TMP_Dropdown _skillDropdown;

	private TMP_Dropdown _traitDropdown;

	private List<Skin> _skinTones;

	private List<PortraitMakerService.Piece> _availableFaces;

	private List<PortraitMakerService.Piece> _availableBusts;

	private List<PortraitMakerService.Piece> _availableHats;

	private int skinIndex;

	private int faceIndex;

	private int bustIndex;

	private int hatIndex;

	private static readonly Label AMERICAN = new Label("am");

	private const string SKIN_INC = "Character/Customize/Skin Increment";

	private const string SKIN_DEC = "Character/Customize/Skin Decrement";

	private const string FACE_INC = "Character/Customize/Face Increment";

	private const string FACE_DEC = "Character/Customize/Face Decrement";

	private const string BUST_INC = "Character/Customize/Bust Increment";

	private const string BUST_DEC = "Character/Customize/Bust Decrement";

	private const string HAT_INC = "Character/Customize/Hat Increment";

	private const string HAT_DEC = "Character/Customize/Hat Decrement";

	public override UIReference UIReference => UIElements.NewGameBossPopup;

	public NewGameBossPopup(GameParameters parameters, MapConfig map, NewGameStartFunction continuation)
	{
		_parameters = parameters;
		_map = map;
		_continuation = continuation;
	}

	protected override void InitializeOnPush()
	{
		_rng = new Xorshift(_parameters.userrng);
		_panel.SetButtonListener("Close/", Close);
		_panel.SetButtonListener("Footer/Cancel/", Close);
		_panel.SetButtonListener("Footer/Continue/", StartGame);
		_skills = Game.serv.globals.settings.skills.FindAllStarterSkills().ToList();
		_traitBundles = Game.serv.globals.settings.general.generator.traitTypes;
		foreach (GeneratorSettings.TraitType traitBundle in _traitBundles)
		{
			if (traitBundle.traits == null)
			{
				continue;
			}
			foreach (Label trait in traitBundle.traits)
			{
				_ = trait;
			}
		}
		_portraitInfo = Game.serv.portraits.GetPortraitPieces(0, 0, Gender.M, Skin.Light, iscop: false, isfed: false, AMERICAN);
		_portraitSprite = Game.serv.portraits.GenerateBlankPortrait(_portraitInfo);
		InitializeCharacterPanel();
		UpdateEthnicityListForMap();
		RerollEthAndPlayerFromSeed();
		RefreshPortraitOptions();
		Game.serv.mouseovers.Register(MouseoverType.CustomGameSkillChoice, new SkillMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CustomGameArchetypeChoice, new ArchetypeMouseover());
		_go.GetOrAddComponent<HideMouseoverOnClick>().popup = this;
	}

	protected override void ReleaseOnPop()
	{
		base.GameObject.DestroyComponentIfAdded<HideMouseoverOnClick>();
		Game.serv.mouseovers.Unregister(MouseoverType.CustomGameSkillChoice);
		Game.serv.mouseovers.Unregister(MouseoverType.CustomGameArchetypeChoice);
		Game.serv.portraits.DestroyPortrait(_portraitSprite);
		_portraitSprite = null;
		_portraitInfo = null;
		_skills = null;
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new PopupHandlerWithTabSupport();
	}

	private void StartGame()
	{
		Label startingSkill = GetSelectedStarterSkill()?.id ?? default(Label);
		List<Label> traits = GetSelectedArchetype()?.traits;
		_playerDeets.portrait = _portraitInfo;
		_playerDeets.traits = traits;
		GameParameters newGame = Game.serv.serializer.instance.Clone(_parameters);
		newGame.playerdetails = new PlayerStartupDetails(_playerDeets, tutorial: false, startingSkill);
		Game.serv.ui.storedStartupDetails = newGame.playerdetails;
		Close();
		_continuation(newGame, _map);
	}

	private void UpdateEthnicityListForMap()
	{
		_ethList = (from e in _map.MakePlayableEthnicitiesForThisMap().Select((EthnicityDef def, int index) => new EthnicityEntry
			{
				eth = def.id,
				loc = GetEthDisplayString(def)
			})
			orderby e.loc
			select e).ToList();
		int num = _ethList.FindIndex((EthnicityEntry e) => e.eth == EthnicitySettings.DEFAULT_ETHNICITY);
		if (num < 0)
		{
			num = 0;
		}
		_ethDropdown.SetOptions(_ethList.Select((EthnicityEntry t) => t.loc).ToList());
		_ethDropdown.SetValueWithoutNotify(num);
		static string GetEthDisplayString(EthnicityDef def)
		{
			if (!PlayerCrew.HasEthPackForEth(def.id))
			{
				return def.loc.GetEthnicity();
			}
			return Loc.Get(def.loc.icon) + " " + def.loc.GetEthnicity();
		}
	}

	private void RerollEthAndPlayerFromSeed()
	{
		long num = _rng.y % _ethList.Count;
		_ethDropdown.SetValueWithoutNotify(Math.Abs((int)num));
		RerollPlayerFromSeed();
	}

	private void OnEthnicityChanged(int _)
	{
		RerollPlayerFromSeed(_playerDeets.gender, ForceReroll: true);
	}

	private void InitializeCharacterPanel()
	{
		_ethDropdown = _panel.GetChild<TMP_Dropdown>("Character/Ethnicity/Dropdown");
		_ethDropdown.onValueChanged.SetListener(OnEthnicityChanged);
		_fnameText = _panel.GetChild<TMP_InputField>("Character/Names/First Name");
		_lnameText = _panel.GetChild<TMP_InputField>("Character/Names/Last Name");
		_groupText = _panel.GetChild<TMP_InputField>("Character/Names/Group Name");
		_fnameText.onValueChanged.SetListener(delegate(string text)
		{
			OnFNameSet(text);
		});
		_lnameText.onValueChanged.SetListener(delegate(string text)
		{
			OnLNameSet(text);
		});
		_groupText.onValueChanged.SetListener(delegate(string text)
		{
			OnGroupSet(text);
		});
		_genderM = _panel.GetChild<Toggle>("Character/Gender/Masculine");
		_genderF = _panel.GetChild<Toggle>("Character/Gender/Feminine");
		_genderM.onValueChanged.SetListener(delegate(bool isOn)
		{
			OnGenderSelect(Gender.M, isOn);
		});
		_genderF.onValueChanged.SetListener(delegate(bool isOn)
		{
			OnGenderSelect(Gender.F, isOn);
		});
		_panel.GetButton("Character/Reroll").onClick.SetListener(OnCharRerollButton);
		_skillDropdown = _panel.GetChild<TMP_Dropdown>("Character/Skill/Dropdown");
		_skillDropdown.SetOptions(_skills.Select((SkillDef def) => def.GetIconAndName()).ToList());
		_traitDropdown = _panel.GetChild<TMP_Dropdown>("Character/Trait/Dropdown");
		_traitDropdown.SetOptions(_traitBundles.Select((GeneratorSettings.TraitType def) => Loc.Get(def.locname)).ToList());
		InitializeCustomizeTab();
	}

	private void OnCharRerollButton()
	{
		RerollPlayerFromSeed(Gender.U, ForceReroll: true);
		RefreshPortraitOptions();
	}

	private void OnGenderSelect(Gender g, bool isOn)
	{
		_playerDeets.gender = g;
		if (isOn)
		{
			RerollPlayerFromSeed(g, ForceReroll: true);
		}
	}

	private static string Trim(string input, int length)
	{
		return input.TrimEnd().TrimToLength(length).Sanitize();
	}

	private void OnFNameSet(string text)
	{
		_playerDeets.fname = Trim(text, 20);
	}

	private void OnLNameSet(string text)
	{
		_playerDeets.lname = Trim(text, 20);
	}

	private void OnGroupSet(string text)
	{
		_playerDeets.group = Trim(text, 40);
	}

	private EthnicityEntry GetSelectedEthnicity()
	{
		return _ethList.GetOrDefaultFast(_ethDropdown.value) ?? _ethList.FirstOrDefaultFast();
	}

	private PortraitInfo QuickRandomPortrait()
	{
		return Game.serv.portraits.GetPortraitPieces((int)_rng.y, (int)_rng.y, _playerDeets.gender, _playerDeets.skin, iscop: false, isfed: false, _playerDeets.ethnicity);
	}

	private void RerollPlayerFromSeed(Gender gender = Gender.U, bool ForceReroll = false)
	{
		EthnicityEntry selectedEthnicity = GetSelectedEthnicity();
		Skin randomSkinForEth = _map.ethnicMakeup.GetRandomSkinForEth(_rng, selectedEthnicity.eth);
		PlayerStartupDetails storedStartupDetails = Game.serv.ui.storedStartupDetails;
		bool isNotSet = storedStartupDetails.startingSkill.IsNotSet;
		bool flag = !_ethList.Select((EthnicityEntry x) => x.eth).Contains(storedStartupDetails.player.ethnicity);
		if (isNotSet || flag || ForceReroll)
		{
			_playerDeets = PlayerStartupDetails.MakeRandomPeepDeets(selectedEthnicity.eth, _rng, gender, randomSkinForEth);
		}
		else
		{
			_playerDeets = Game.serv.ui.storedStartupDetails.player;
		}
		_fnameText.text = _playerDeets.fname;
		_lnameText.text = _playerDeets.lname;
		_groupText.text = _playerDeets.group;
		bool flag2 = _playerDeets.gender == Gender.M;
		_genderM.SetIsOnWithoutNotify(flag2);
		_genderF.SetIsOnWithoutNotify(!flag2);
		_portraitInfo = _playerDeets.portrait;
		RefreshPortraitOptions();
		RefreshPortraitsDisplay();
		_go.SetImage("Character/Portrait/Portrait", _portraitSprite);
		int valueWithoutNotify = (storedStartupDetails.startingSkill.IsSet ? _skills.FindIndex((SkillDef x) => x.id == storedStartupDetails.startingSkill) : (Math.Abs((int)_rng.y) % _skills.Count));
		_skillDropdown.SetValueWithoutNotify(valueWithoutNotify);
		int valueWithoutNotify2 = ((storedStartupDetails.player.traits != null) ? _traitBundles.FindIndex((GeneratorSettings.TraitType x) => x.traits != null && x.traits[0] == storedStartupDetails.player.traits[0]) : (Math.Abs((int)_rng.y) % _traitBundles.Count));
		_traitDropdown.SetValueWithoutNotify(valueWithoutNotify2);
	}

	internal SkillDef GetSelectedStarterSkill()
	{
		return _skills.GetOrDefaultFast(_skillDropdown.value) ?? _skills.FirstOrDefaultFast();
	}

	internal GeneratorSettings.TraitType GetSelectedArchetype()
	{
		return _traitBundles.GetOrDefaultFast(_traitDropdown.value) ?? _traitBundles.FirstOrDefaultFast();
	}

	private void InitializeCustomizeTab()
	{
		_panel.SetButtonListener("Character/Customize/Skin Increment", delegate
		{
			SelectSkin(1);
		});
		_panel.SetButtonListener("Character/Customize/Skin Decrement", delegate
		{
			SelectSkin(-1);
		});
		_panel.SetButtonListener("Character/Customize/Face Increment", delegate
		{
			SelectFace(1);
		});
		_panel.SetButtonListener("Character/Customize/Face Decrement", delegate
		{
			SelectFace(-1);
		});
		_panel.SetButtonListener("Character/Customize/Bust Increment", delegate
		{
			SelectBust(1);
		});
		_panel.SetButtonListener("Character/Customize/Bust Decrement", delegate
		{
			SelectBust(-1);
		});
		_panel.SetButtonListener("Character/Customize/Hat Increment", delegate
		{
			SelectHat(1);
		});
		_panel.SetButtonListener("Character/Customize/Hat Decrement", delegate
		{
			SelectHat(-1);
		});
	}

	private void RefreshPortraitsDisplay()
	{
		Game.serv.portraits.ResetPortrait(_portraitSprite);
		Game.serv.portraits.PopulateCompositePortrait(_portraitInfo, _portraitSprite);
	}

	private void RefreshPortraitOptions()
	{
		(List<PortraitMakerService.Piece> faces, List<PortraitMakerService.Piece> busts, List<PortraitMakerService.Piece> hats) allPortraitPiecesForCustom = Game.serv.portraits.GetAllPortraitPiecesForCustom(_playerDeets.gender, _playerDeets.skin);
		List<PortraitMakerService.Piece> item = allPortraitPiecesForCustom.faces;
		List<PortraitMakerService.Piece> item2 = allPortraitPiecesForCustom.busts;
		List<PortraitMakerService.Piece> item3 = allPortraitPiecesForCustom.hats;
		_skinTones = new List<Skin>
		{
			Skin.Dark,
			Skin.Medium,
			Skin.Light
		};
		skinIndex = _skinTones.FindIndex((Skin x) => x == _playerDeets.skin);
		_availableBusts = item2;
		bustIndex = _availableBusts.FindIndex((PortraitMakerService.Piece x) => x.asset == _portraitInfo.bust);
		_availableFaces = item;
		faceIndex = _availableFaces.FindIndex((PortraitMakerService.Piece x) => x.asset == _portraitInfo.head);
		_availableHats = item3;
		hatIndex = _availableHats.FindIndex((PortraitMakerService.Piece x) => x.asset == _portraitInfo.hat);
		if (hatIndex == -1)
		{
			hatIndex = _availableHats.Count;
		}
	}

	private void SelectSkin(int delta)
	{
		skinIndex = MathUtil.Modulus(skinIndex + delta, _skinTones.Count);
		_playerDeets.skin = _skinTones[skinIndex];
		PortraitInfo portraitInfo = QuickRandomPortrait();
		_portraitInfo = portraitInfo;
		RefreshPortraitOptions();
		RefreshPortraitsDisplay();
	}

	private void SelectFace(int delta)
	{
		faceIndex = MathUtil.Modulus(faceIndex + delta, _availableFaces.Count);
		_portraitInfo.head = _availableFaces[faceIndex].asset;
		RefreshPortraitsDisplay();
	}

	private void SelectBust(int delta)
	{
		bustIndex = MathUtil.Modulus(bustIndex + delta, _availableBusts.Count);
		_portraitInfo.bust = _availableBusts[bustIndex].asset;
		RefreshPortraitsDisplay();
	}

	private void SelectHat(int delta)
	{
		List<PackID> installedPacks = Game.serv.store.FindAllInstalledPacks().ToList();
		hatIndex = MathUtil.Modulus(hatIndex + delta, _availableHats.Count + 1);
		string hat = ((hatIndex == _availableHats.Count) ? null : _availableHats[hatIndex].asset);
		string text = ((hatIndex == _availableHats.Count) ? null : _availableHats[hatIndex].hatmaskOverride);
		_portraitInfo.hat = hat;
		_portraitInfo.hatmask = text ?? PortraitMakerService.HAT_MASKS.FindOrNull(_playerDeets.gender, (int)_rng.y, "HAT_MASK", installedPacks, _playerDeets.ethnicity);
		RefreshPortraitsDisplay();
	}
}
