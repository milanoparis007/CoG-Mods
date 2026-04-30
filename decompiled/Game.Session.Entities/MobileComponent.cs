using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Crew;
using Game.UI.Session.Picks;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class MobileComponent : BaseComponent, IEntityEventObserverComponent, ILoadObserverComponent
{
	public MobileConfig Config => _baseConfig as MobileConfig;

	public MobileData Data => _entity.data.mobile;

	public bool IsActivated => Game.ctx.selection.CurrentActive == _entity;

	public float CurrentHealthAsFraction => (float)Fixnum.Clamp(CurrentHealth / MaxHealth(), 0, 1);

	public bool HasHealthPointsLeft => _entity.data.mobile.health > 0;

	public Fixnum CurrentHealth => _entity.data.mobile.health;

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
		if (!loaded)
		{
			ModValue maxVehicleHealth = Game.serv.globals.settings.people.vehicleSettings.maxVehicleHealth;
			_entity.data.mobile.health = maxVehicleHealth.Evaluate(new ModQuery(PlayerID.INVALID));
		}
	}

	public void OnEntityEvent(EntityEventType type)
	{
		if (type == EntityEventType.EntityActivationChanged)
		{
			OnEntityActivationChanged();
		}
	}

	private void OnEntityActivationChanged()
	{
		if (IsActivated && _entity.config.selection.onaction == SelectionConfig.OnAction.CarAmbient)
		{
			Game.ctx.selection.ClearActive();
			return;
		}
		PlayerID pid = _entity.data.mobile.pid;
		if (!pid.IsValid)
		{
			return;
		}
		if (IsActivated)
		{
			Entity peep = pid.FindPlayer().crew.GetCrewForTarget(_entity.Id).GetPeep();
			if (!pid.IsHumanPlayer)
			{
				if (peep == null || !peep.data.person.IsAlive)
				{
					ScavengeUtil.ShowVehicleScavengingPopup(_entity);
				}
				else
				{
					NodeID nid = peep.data.agent.nid;
					if (Game.ctx.players.Human.crew.FindFirstCrewAtLocation(nid).IsNotValid)
					{
						Game.ctx.hud.personInfo.Show(peep);
					}
					else
					{
						StartConversationWithNPC(peep);
					}
				}
			}
		}
		else
		{
			Game.ctx.hud.HideGroup(BaseHUDDialog.GroupType.ConvoGroup);
			Game.ctx.overlays.arrows.HideRelationshipArrows();
		}
		RefreshTravelLine();
		RefreshAvatarMarker();
	}

	private static void StartConversationWithNPC(Entity peep)
	{
		EntitySelectionPopup.ShowCrewSelector(peep.data.agent.nid, Loc.Get("ui.crewinfo.pickone"), delegate(EntityID eid)
		{
			Game.ctx.hud.convoDialog.Controller.StartCrewVisit(peep, Game.ctx.players.Human.crew.GetCrewForPeep(eid));
		});
	}

	public void OnAfterLoading()
	{
		MobileData mobile = _entity.data.mobile;
		_entity.components?.model.MoveModel(mobile.worldpos, mobile.deg);
	}

	public void Move(WorldPos from, WorldPos to)
	{
		float facingDegrees = GetFacingDegrees(from, to);
		Move(to, facingDegrees);
	}

	public void Move(WorldPos pos, float deg)
	{
		_entity.data.mobile.Set(pos, deg);
		_entity.components?.model.MoveModel(pos, deg);
	}

	public void Rotate(int deg)
	{
		WorldPos worldpos = _entity.data.mobile.worldpos;
		Move(worldpos, deg);
	}

	public void Remove(bool _)
	{
		_entity.data.mobile.Clear();
	}

	private float GetFacingDegrees(WorldPos current, WorldPos lookat)
	{
		return WorldPos.GetFacingDegrees(current, lookat) ?? _entity.data.mobile.deg;
	}

	internal float GetVelocity(PlayerID pid)
	{
		float num = _entity.config.mobile.velocityTilesPerSecond;
		if (pid.IsAIPlayer)
		{
			num *= FindAISpeedMultiplier();
		}
		return num;
	}

	private float FindAISpeedMultiplier()
	{
		DebugSettings debug = Game.serv.globals.settings.general.debug;
		SessionContext ctx = Game.ctx;
		if (ctx == null || !ctx.clock.CurrentPlayer.IsAIPlayer)
		{
			if (debug.runOnlyAIUntil <= 0)
			{
				if (!debug.showGoonsAtStartup)
				{
					return 2f;
				}
				return 10f;
			}
			return 100f;
		}
		return 0f;
	}

	public NodeID FindNodeNearThisMobile()
	{
		return Game.ctx.board.nodes.FindNearestNodeAround(_entity.data.mobile.worldpos, 4f)?.id ?? NodeID.INVALID;
	}

	private void RefreshAvatarMarker()
	{
		ToggleAvatarMarker(IsActivated);
	}

	private void ToggleAvatarMarker(bool show)
	{
		VFXManager vfx = Game.ctx.vfx;
		bool num = vfx.IsMarkerShowing(_entity);
		if (!num && show)
		{
			vfx.ShowMarkerFor(_entity);
		}
		if (num && !show)
		{
			vfx.ShowMarkerFor(null);
		}
	}

	internal void HandleTravelStart(List<WorldPos> target)
	{
		_entity.data.mobile.travelContext = target;
		RefreshTravelLine();
	}

	internal void HandleTravelEnd()
	{
		_entity.data.mobile.travelContext = null;
		RefreshTravelLine();
	}

	private void RefreshTravelLine()
	{
		bool show = _entity.data.mobile.pid.IsHumanPlayer && IsActivated && _entity.data.mobile.InTravel;
		ToggleTravelLine(show);
	}

	private void ToggleTravelLine(bool show)
	{
		VFXManager vfx = Game.ctx.vfx;
		bool flag = vfx.IsNavLineShowing(_entity);
		if (show && !flag)
		{
			vfx.ShowNavLineTraveling(_entity);
		}
		if (!show && flag)
		{
			vfx.HideNavLine(_entity);
		}
	}

	public Fixnum MaxHealth()
	{
		return Game.serv.globals.settings.people.vehicleSettings.maxVehicleHealth.Evaluate(_entity.data.mobile.pid);
	}

	public bool IsJunk()
	{
		return FindHealthInfo().IsJunk;
	}

	public Fixnum IncrementHealth(CrewAssignment crew, Fixnum delta)
	{
		return SetHealth(crew, _entity.data.mobile.health + delta);
	}

	public Fixnum SetHealthToMax(CrewAssignment crew)
	{
		return SetHealth(crew, MaxHealth());
	}

	public Fixnum SetHealth(CrewAssignment crew, Fixnum points)
	{
		_entity.data.mobile.health = Fixnum.Clamp(points, 0, MaxHealth());
		if (crew.peepId.IsValid)
		{
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewVehicleHealthChanged, crew.peepId, _entity.data.mobile.pid));
		}
		return points;
	}

	public VehicleSettings.HealthInfo FindHealthInfo()
	{
		return Game.serv.globals.settings.people.vehicleSettings.FindHealthInfo(CurrentHealth);
	}

	public string MakeConditionString(bool shortinfo)
	{
		return Loc.GetVehicleCondition(FindHealthInfo().category, shortinfo);
	}

	public void ProcessAttack(CrewAssignment mycrew, WeaponConfig weapon)
	{
		if (weapon.firearm && _entity.data.mobile.pid.IsHumanPlayer)
		{
			UpdateHealthFrom(mycrew, VehicleHealthSource.FromFirearm);
		}
	}

	public void UpdateHealthFrom(CrewAssignment crew, VehicleHealthSource source)
	{
		if (_entity.data.mobile.pid.IsHumanPlayer)
		{
			VehicleSettings.HealthInfo healthInfo = FindHealthInfo();
			Fixnum delta = FindHealthLossFor(crew, source);
			if (delta.IsNotZero)
			{
				IncrementHealth(crew, delta);
			}
			VehicleSettings.HealthInfo healthInfo2 = FindHealthInfo();
			if (!healthInfo.IsJunk && healthInfo2.IsJunk)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.VEHICLE_JUNK, TickerTitle.VEHICLE_JUNK, Loc.Get("ui.tickers.vehicle.junk"), crew.VehicleID, TickerPersistType.CarDamagePersist);
			}
			else if (healthInfo.IsGood && !healthInfo2.IsGood)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.VEHICLE_CLUNKER, TickerTitle.VEHICLE_CLUNKER, Loc.Get("ui.tickers.vehicle.clunk"), crew.VehicleID, TickerPersistType.CarDamagePersist);
			}
		}
	}

	private Fixnum FindHealthLossFor(CrewAssignment crew, VehicleHealthSource source)
	{
		VehicleSettings.HealthLossInfo healthLossInfo = Game.serv.globals.settings.people.vehicleSettings.FindHealthLossInfo(source);
		if (healthLossInfo == null)
		{
			return 0;
		}
		ModQuery query = new ModQuery(_entity.data.mobile.pid, crew.peepId, crew.peepId);
		return healthLossInfo.points.Evaluate(query);
	}
}
