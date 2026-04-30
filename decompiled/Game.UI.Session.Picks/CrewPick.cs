using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class CrewPick : BasePick
{
	private class CrewPickState
	{
		public string text;

		public bool isHuman;

		public bool showBar;

		public bool showWarning;
	}

	private static class CrewPickUtils
	{
		public static string MakeCrewMouseover(PlayerID pid, Entity peep, Entity vehicle, bool dead)
		{
			bool flag = pid.IsAIPlayer && dead;
			if (pid.IsNotValid)
			{
				return Loc.Get("ui.crewpick.scavenge");
			}
			if (flag)
			{
				NodeID nodeId = vehicle.components.mobile.FindNodeNearThisMobile();
				List<CrewAssignment> list = Game.ctx.players.Human.crew.FindAllDriversAtNode(nodeId);
				return Loc.GetPluralized("ui.crewpick.scavenge", list.Count);
			}
			if (pid.IsHumanPlayer && peep == null)
			{
				return Loc.Get("ui.crewpick.unassigned");
			}
			PlayerInfo playerInfo = Game.ctx.players.WithID(pid);
			CrewAssignment crew = peep.components.agent.FindCrewAssignment();
			if (!playerInfo.PID.IsAIPlayer)
			{
				return playerInfo.crew.GetCrewPeepName(crew);
			}
			return playerInfo.crew.GetCrewPeepAndGroupName(crew);
		}

		public static CrewPickState MakePickState(PlayerID pid, Entity peep, bool dead)
		{
			bool num = pid.IsAIPlayer && dead;
			bool isHumanPlayer = pid.IsHumanPlayer;
			string text = (num ? Loc.Get("ui.crewpick.icon-dead") : ((peep == null) ? Loc.Get("ui.crewpick.icon-empty") : ""));
			bool showBar = !dead && peep.components.agent.IsWounded;
			bool showWarning = IsAggroOnHuman(pid);
			return new CrewPickState
			{
				isHuman = isHumanPlayer,
				text = text,
				showBar = showBar,
				showWarning = showWarning
			};
		}

		private static bool IsAggroOnHuman(PlayerID pid)
		{
			PlayerInfo playerInfo = pid.FindPlayer();
			if (playerInfo != null && playerInfo.IsGangOrGoon)
			{
				return playerInfo.ai.combat.IsAttackAllowed(PlayerID.HumanPlayer);
			}
			return false;
		}
	}

	private const string FRAME_HUMAN = "Frame Human";

	private const string FRAME_HUMAN_PREORDER = "Frame Human PreOrder";

	private const string FRAME_COPS = "Frame Police";

	private const string FRAME_GOON = "Frame Goon";

	private const string FRAME_GANG = "Frame Gang";

	private const string FRAME_GANG_MASK = "Frame Gang/Mask";

	private const string COMMANDS_CONTAINER = "Commands";

	private const string HEALTH_CONTAINER = "Bar Panel";

	private const string HEALTH_BAR = "Bar Panel/Bar";

	private const string WARN_BORDER = "Warn Border";

	private const string BUTTON = "Button";

	private const string BG_COLOR = "Button/Color Overlay";

	private const string PORTRAIT = "Button/Portrait";

	private const string TEXT = "Button/Text";

	private const string STATE = "State Icon";

	private const string STATE_TEXT = "State Icon/Icon Text";

	public const float BAR_ASSET_MAX_WIDTH = 56f;

	private PlayerID _pid = PlayerID.INVALID;

	private CrewAssignment _crew;

	private Entity _peep;

	private Entity _vehicle;

	private Color _playerColor;

	private Sprite _peepSprite;

	private bool _isActive;

	private const float PICK_Y_HEIGHT = 1f;

	private const float MIN_ZOOM = 150f;

	private const float MAX_ZOOM = 250f;

	private const float ZOOM_SCALING_FACTOR = 200f;

	private const float MIN_DISTANCE_BETWEEN_PICKS = 30f;

	private const float SPACING = 20f;

	private static readonly string BG_PATH = "UI Images/UI Buttons and Panels/Vehicle_Pick_Gradient";

	private static readonly string PICK_PATH = "UI Images/UI Buttons and Panels/Vehicle_Pick_Human";

	private static readonly string BOSS_PICK_PATH = "UI Images/UI Buttons and Panels/Vehicle_Pick_Boss";

	private static readonly string BOSS_PICK_FALLBACK = "UI Images/UI Buttons and Panels/Vehicle_Pick_PreOrder";

	public override PickType Type => PickType.CrewPick;

	private bool IsPeepDead
	{
		get
		{
			if (_peep != null)
			{
				return !_peep.data.person.IsAlive;
			}
			return true;
		}
	}

	public override void SetTarget(PickTarget target)
	{
		base.SetTarget(target);
		_vehicle = base.Target.FindEntity();
		_pid = _vehicle.data.mobile.pid;
		PlayerInfo playerInfo = Game.ctx.players.WithID(_pid);
		_crew = playerInfo.crew.GetCrewForTarget(_vehicle.Id);
		_peep = _crew.peepId.FindEntity();
		_peepSprite = HUDUtil.GetCrewSprite(_peep);
		_playerColor = _pid.FindPlayer().territory.colorInfo.GetPlayerColor();
	}

	public override void Reset()
	{
		_playerColor = Color.black;
		_peepSprite = null;
		_peep = null;
		_vehicle = null;
		_crew = default(CrewAssignment);
		_pid = PlayerID.INVALID;
		_isActive = false;
		base.Reset();
	}

	public override string MakeMouseoverMessage()
	{
		return CrewPickUtils.MakeCrewMouseover(_pid, _peep, _vehicle, IsPeepDead);
	}

	public override void OnClick()
	{
		Game.ctx.selection.SetActive(base.Target.FindEntity());
	}

	public override Vector3 MakeSceneVector()
	{
		return Game.serv.camera.WorldToSceneVector(_vehicle.data.mobile.worldpos).SetY(1f);
	}

	public override Vector2 MakeScreenVector()
	{
		Vector2 vector = Game.serv.camera.SceneToScreenPos(MakeSceneVector());
		Vector2 vector2 = new Vector2(0f, 0f);
		float num = MathUtil.Clamp(Game.serv.camera.GetZoom(), 150f, 250f) / 200f;
		Vector2 vector3 = Game.serv.camera.WorldToScreenPos(_vehicle.data.mobile.worldpos);
		if (_peep == null || _vehicle.data.mobile.InTravel)
		{
			return vector / Game.serv.ui.UIScaleFactor;
		}
		List<EntityID> allAgentsAtNodeUnsafe = Game.ctx.transit.GetAllAgentsAtNodeUnsafe(_peep.data.agent.nid);
		if (allAgentsAtNodeUnsafe.Count <= 1)
		{
			return vector / Game.serv.ui.UIScaleFactor;
		}
		foreach (EntityID item in allAgentsAtNodeUnsafe)
		{
			Vector2 vector4 = vector3 + vector2;
			if (item == _crew.peepId)
			{
				continue;
			}
			Entity entity = item.FindEntity().data.agent.pid.FindPlayer().crew.FindVehicleAssignedToPeep(item).FindEntity();
			if (entity != null)
			{
				Vector2 vector5 = Game.serv.camera.WorldToScreenPos(entity.data.mobile.worldpos);
				if ((vector4 - vector5).magnitude < 30f)
				{
					vector2 += vector4 - vector5;
				}
			}
		}
		return (vector + new Vector2(vector2.x, vector2.y).normalized * 20f * num) / Game.serv.ui.UIScaleFactor;
	}

	internal void OnActivationChange(bool active)
	{
		_isActive = active;
	}

	public override void RefreshContents()
	{
		bool isPeepDead = IsPeepDead;
		if (isPeepDead && _peep != null)
		{
			_peep = null;
			SetTarget(_vehicle);
		}
		CrewPickState crewPickState = CrewPickUtils.MakePickState(_pid, _peep, isPeepDead);
		var (sprite, flag) = TryFindAppropriateBG();
		go.SetImage("Button/Color Overlay", sprite);
		if (!flag)
		{
			go.GetImage("Button/Color Overlay").color = _playerColor;
		}
		go.GetImage("Button/Portrait").sprite = _peepSprite;
		go.SetActive("Warn Border", crewPickState.showWarning);
		go.SetText("Button/Text", crewPickState.text);
		PlayerInfo playerInfo = _pid.FindPlayer();
		bool isHuman = playerInfo.IsHuman;
		bool isJustGang = playerInfo.IsJustGang;
		bool isCopOrFed = playerInfo.IsCopOrFed;
		bool value = !isHuman && !isJustGang && !isCopOrFed;
		bool flag2 = isHuman && _peep != null && _peep.Id == playerInfo.social.PlayerPeepId;
		bool flag3 = flag2 && Game.ctx.hud.picks.IsPreorder;
		bool flag4 = PlayerCrew.HasEthPackForCurrEth();
		go.SetActive("Frame Human PreOrder", isHuman && flag2 && (flag3 || flag4));
		Sprite sprite2 = TryFindEthBossFrameReplacement();
		if (sprite2 != null)
		{
			go.SetImage("Frame Human PreOrder", sprite2);
		}
		go.SetActive("Frame Human", isHuman && !flag3);
		Sprite sprite3 = TryFindEthFrameReplacement();
		if (sprite3 != null)
		{
			go.SetImage("Frame Human", sprite3);
		}
		go.SetActive("Frame Police", isCopOrFed);
		go.SetActive("Frame Goon", value);
		go.SetActive("Frame Gang", isJustGang);
		bool flag5 = Game.ctx.players.Human.automation.GetAutoOrNull(_crew)?.IsAutoActive ?? false;
		bool flag6 = Game.ctx.players.Human.schemes.IsInScheme(_crew.GetPeep());
		bool flag7 = isHuman && (flag5 || flag6);
		bool isNotValid = _crew.peepId.IsNotValid;
		go.SetActive("State Icon", flag7 && !isNotValid);
		if (flag7 && !isNotValid)
		{
			if (flag6)
			{
				string text = Loc.Get(_crew.GetPeep().data.agent.xp.GetCrewRole().locicon);
				go.SetText("State Icon/Icon Text", text);
			}
			else if (flag5)
			{
				go.SetText("State Icon/Icon Text", Loc.Get("ui.crewpick.state-automated"));
			}
		}
		go.GetImage("Frame Gang/Mask").color = _playerColor.SetAlpha(0.5f);
		RefreshHealthBar(crewPickState.showBar);
	}

	private void RefreshHealthBar(bool showBar)
	{
		go.SetActive("Bar Panel", showBar);
		if (showBar)
		{
			float currentHealthAsFraction = _peep.components.agent.CurrentHealthAsFraction;
			go.GetChild("Bar Panel/Bar").SetUIElementWidth(currentHealthAsFraction * 56f);
			go.GetImage("Bar Panel/Bar").color = _peep.components.agent.FindHealthInfo().color;
		}
	}

	private (Sprite sprite, bool isEth) TryFindAppropriateBG()
	{
		Sprite sprite = null;
		bool item = false;
		if (_pid != PlayerID.HumanPlayer)
		{
			return (sprite: sprite, isEth: item);
		}
		Label ethnicity = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
		if (!PlayerCrew.HasEthPackAndIsEth(ethnicity))
		{
			return (sprite: sprite, isEth: item);
		}
		if (_pid == PlayerID.HumanPlayer)
		{
			sprite = Resources.Load<Sprite>(BG_PATH + ethnicity.ToString().ToUpper());
			item = true;
		}
		if (sprite == null)
		{
			sprite = Resources.Load<Sprite>(BG_PATH);
		}
		return (sprite: sprite, isEth: item);
	}

	private Sprite TryFindEthFrameReplacement()
	{
		if (_pid != PlayerID.HumanPlayer)
		{
			return null;
		}
		Label ethnicity = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
		if (!PlayerCrew.HasEthPackAndIsEth(ethnicity))
		{
			return null;
		}
		Sprite sprite = Resources.Load<Sprite>(PICK_PATH + ethnicity.ToString().ToUpper());
		if (sprite == null)
		{
			sprite = Resources.Load<Sprite>(PICK_PATH);
		}
		return sprite;
	}

	private Sprite TryFindEthBossFrameReplacement()
	{
		if (_pid != PlayerID.HumanPlayer)
		{
			return null;
		}
		Label ethnicity = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
		if (!PlayerCrew.HasEthPackAndIsEth(ethnicity))
		{
			return null;
		}
		Sprite sprite = Resources.Load<Sprite>(BOSS_PICK_PATH + ethnicity.ToString().ToUpper());
		if (sprite == null)
		{
			sprite = Resources.Load<Sprite>(BOSS_PICK_FALLBACK);
		}
		return sprite;
	}
}
