using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player.Commands;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class CommandExecutor : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public class CommandTuple
	{
		public bool markedForRemoval;

		public EntityID peepId;

		public List<Command> queue;

		public Command active;

		private int Count
		{
			get
			{
				if (active == null)
				{
					return queue.Count;
				}
				return 1 + queue.Count;
			}
		}

		public bool HasAnyCommands => Count > 0;

		public bool HasExactlyOne => Count == 1;

		public Command NextInQueue => queue.FirstOrDefaultFast();

		public Command ActiveOrQueued => active ?? queue.FirstOrDefaultFast();

		public CommandTuple()
		{
		}

		public CommandTuple(EntityID peepId, Command cmd)
		{
			this.peepId = peepId;
			queue = new List<Command>();
			Enqueue(cmd);
		}

		public void Clear()
		{
			queue.Clear();
		}

		public void Enqueue(Command cmd)
		{
			queue.Add(cmd);
		}

		private Command Dequeue()
		{
			return queue.RemoveAndReturnOrDefault(0);
		}

		public Command ActivateFirst()
		{
			active = Dequeue();
			return active;
		}

		public void SetActive(Command newactive)
		{
			active = newactive;
		}

		public void Deactivate()
		{
			active = null;
		}

		public void PopulateListWithCommands(List<Command> topopulate)
		{
			if (active != null)
			{
				topopulate.Add(active);
			}
			foreach (Command item in queue)
			{
				topopulate.Add(item);
			}
		}
	}

	public List<CommandTuple> tuples;

	public override void OnPostSetDataSource(bool loaded)
	{
		if (_data.queues == null)
		{
			_data.queues = new List<CommandTuple>();
		}
		tuples = _data.queues;
	}

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		Game.ctx.events.AddListener(SessionEventType.CrewMemberKilled, OnPeepDeath);
	}

	public override void OnPreRelease()
	{
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberKilled, OnPeepDeath);
		base.OnPreRelease();
	}

	public void OnPlayerTurnStarted()
	{
		RefillPlayerCrewActions();
		ExecuteCommandsInQueue();
		ConsumeMovementForBusyCrew();
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	private void RefillPlayerCrewActions()
	{
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			item.peepId.FindEntity().components.agent.Refill();
		}
	}

	private void ConsumeMovementForBusyCrew()
	{
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			ConsumeMovementForBusyCrewMember(item.peepId);
		}
	}

	private void ConsumeMovementForBusyCrewMember(EntityID peepId)
	{
		if (FindCommand(peepId, onlyActive: true) != null)
		{
			peepId.FindEntity().components.agent.ConsumeAllPoints(actions: false);
		}
	}

	private void ExecuteCommandsInQueue()
	{
		if (tuples.Count == 0)
		{
			return;
		}
		foreach (CommandTuple tuple in tuples)
		{
			if (!tuple.markedForRemoval)
			{
				ProcessCommandQueue(tuple);
			}
		}
		RemoveDeprecatedTuples();
	}

	private void ProcessCommandQueue(CommandTuple tuple)
	{
		while (tuple.HasAnyCommands)
		{
			if (tuple.active == null)
			{
				if (!tuple.NextInQueue.CanActivateAfterDequeue())
				{
					break;
				}
				tuple.ActivateFirst();
				switch (tuple.active.Start())
				{
				case Command.StartStatus.Failed:
					tuple.Deactivate();
					continue;
				case Command.StartStatus.SkipThisTurn:
					return;
				}
			}
			if (!tuple.active.ExecuteSingleTurn())
			{
				tuple.active.Finish(success: true);
				tuple.Deactivate();
				continue;
			}
			break;
		}
	}

	private void CancelActiveCommand(CommandTuple tuple)
	{
		if (tuple.active != null)
		{
			tuple.active.Finish(success: false);
			tuple.SetActive(null);
		}
	}

	private CommandTuple AddCommandToQueue(Command cmd)
	{
		int num = FindQueueIndex(cmd.peepId);
		if (num >= 0)
		{
			tuples[num].Enqueue(cmd);
			return tuples[num];
		}
		CommandTuple commandTuple = new CommandTuple(cmd.peepId, cmd);
		tuples.Add(commandTuple);
		SortQueues();
		return commandTuple;
	}

	private void OnPeepDeath(SessionEvent sev)
	{
		int num = FindQueueIndex(sev.eid);
		if (num >= 0)
		{
			tuples[num].markedForRemoval = true;
		}
	}

	private void RemoveDeprecatedTuples()
	{
		for (int num = tuples.Count - 1; num >= 0; num--)
		{
			if (tuples[num].markedForRemoval)
			{
				tuples.RemoveAt(num);
			}
		}
		SortQueues();
	}

	private int FindQueueIndex(EntityID peepid)
	{
		int i = 0;
		for (int count = tuples.Count; i < count; i++)
		{
			if (tuples[i].peepId == peepid)
			{
				return i;
			}
		}
		return -1;
	}

	private void SortQueues()
	{
		tuples.Sort((CommandTuple t1, CommandTuple t2) => t1.peepId.index - t2.peepId.index);
	}

	public bool PeepHasTask(EntityID peepId)
	{
		int num = FindQueueIndex(peepId);
		if (num >= 0)
		{
			return tuples[num].HasAnyCommands;
		}
		return false;
	}

	public Command FindCommand(EntityID peepId, bool onlyActive)
	{
		int num = FindQueueIndex(peepId);
		if (num >= 0)
		{
			if (!onlyActive)
			{
				return tuples[num].ActiveOrQueued;
			}
			return tuples[num].active;
		}
		return null;
	}

	public void FlushQueue(EntityID peepId, bool cancelActive)
	{
		int num = FindQueueIndex(peepId);
		if (num >= 0)
		{
			CommandTuple commandTuple = tuples[num];
			commandTuple.Clear();
			if (cancelActive && commandTuple.active != null)
			{
				CancelActiveCommand(commandTuple);
			}
		}
	}

	public IEnumerable<Command> EnumerateCommands(EntityID peepId)
	{
		int num = FindQueueIndex(peepId);
		if (num < 0)
		{
			yield break;
		}
		CommandTuple queue = tuples[num];
		if (queue == null || !queue.HasAnyCommands)
		{
			yield break;
		}
		yield return queue.active;
		foreach (Command item in queue.queue)
		{
			yield return item;
		}
	}

	public void AddCommandImmediate(Command cmd)
	{
		HandleAddingCommand(cmd, flush: true);
	}

	public void AddCommandImmediate(Command first, params Command[] cmds)
	{
		HandleAddingCommand(first, flush: true);
		foreach (Command cmd in cmds)
		{
			HandleAddingCommand(cmd, flush: false);
		}
	}

	public void AddCommand(Command cmd)
	{
		HandleAddingCommand(cmd, flush: false);
	}

	public void AddCommand(Command first, params Command[] cmds)
	{
		AddCommand(first);
		foreach (Command cmd in cmds)
		{
			AddCommand(cmd);
		}
	}

	private void HandleAddingCommand(Command cmd, bool flush)
	{
		if (cmd.pid != _pid)
		{
			Logger.Error($"Command from: {cmd.pid} on player: {_pid}—ignoring");
			return;
		}
		if (flush)
		{
			FlushQueue(cmd.peepId, cancelActive: true);
		}
		CommandTuple commandTuple = AddCommandToQueue(cmd);
		if (commandTuple.HasExactlyOne)
		{
			ProcessCommandQueue(commandTuple);
		}
	}

	public void PopulateListWithCommands(EntityID peepId, List<Command> topopulate)
	{
		int num = FindQueueIndex(peepId);
		if (num >= 0)
		{
			tuples[num].PopulateListWithCommands(topopulate);
		}
	}
}
