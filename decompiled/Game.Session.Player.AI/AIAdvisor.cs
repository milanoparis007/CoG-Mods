using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.Session.Player.AI;

public abstract class AIAdvisor
{
	protected PlayerAI _manager;

	protected PlayerID _pid;

	protected PlayerInfo _player;

	public PlayerID PID => _pid;

	protected AIAdvisor(PlayerAI manager, NPCDefinition def)
	{
		_manager = manager;
		_pid = _manager.PID;
		_player = _manager.PlayerInfo;
	}

	protected T FindAdvisorConfig<T>(Label name) where T : AIAdvisorConfig
	{
		return Game.serv.globals.settings.npc.FindAdvisorConfig<T>(name);
	}

	public virtual void Initialize()
	{
	}

	public virtual void Release()
	{
	}

	public abstract void OnTurnUpdate();

	public abstract void ProduceRequests(List<AdvisorRequest> result);

	public virtual void OnRequestDispatched(AdvisorRequest req, bool dispatched)
	{
	}

	protected void CallWithCooldown(Action callback, ref SimTime timer, ModValue firstCheckDays, ModValue cooldownDays)
	{
		SimTime now = Game.ctx.clock.Now;
		if (timer.days <= 0)
		{
			int deltaDays = firstCheckDays.Evaluate(new ModQuery(_pid)).IntFloor();
			timer.days = Game.ctx.clock.LastDayOfProcGen.IncrementDays(deltaDays).days;
		}
		if (timer.days < now.days)
		{
			callback();
			int deltaDays2 = cooldownDays.Evaluate(new ModQuery(_pid)).IntFloor();
			timer = now.IncrementDays(deltaDays2);
		}
	}

	public virtual void OnCompliance(Demand demand)
	{
	}

	public virtual void OnDefiance(Demand demand)
	{
	}
}
