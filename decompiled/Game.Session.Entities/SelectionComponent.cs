using System;
using Game.Core;
using Game.Session.Input;
using Game.Session.Player;

namespace Game.Session.Entities;

public sealed class SelectionComponent : BaseComponent
{
	public sealed class VizStack
	{
		[Flags]
		public enum State
		{
			None = 0,
			Highlight = 1,
			Focus = 2,
			Active = 4
		}

		public static readonly State[] ALL_STATES = Enum.GetValues(typeof(State)) as State[];

		public State state;

		public bool IsSet(State value)
		{
			return (state & value) == value;
		}

		public void Set(State value)
		{
			state |= value;
		}

		public void Clear(State value)
		{
			state &= ~value;
		}

		public void Toggle(State value, bool set)
		{
			if (set)
			{
				Set(value);
			}
			else
			{
				Clear(value);
			}
		}

		public State GetMax()
		{
			for (int num = ALL_STATES.Length - 1; num >= 0; num--)
			{
				State state = ALL_STATES[num];
				if ((this.state & state) == state)
				{
					return state;
				}
			}
			return State.None;
		}
	}

	public VizStack vizstate = new VizStack();

	private SelectionConfig Config => _baseConfig as SelectionConfig;

	public bool CanHighlight => Config.onfocus != SelectionConfig.OnFocus.None;

	public bool CanFocus => Config.onfocus != SelectionConfig.OnFocus.None;

	public bool CanActivate
	{
		get
		{
			if (CanFocus)
			{
				return Config.onaction != SelectionConfig.OnAction.None;
			}
			return false;
		}
	}

	public bool IsHighlight => vizstate.IsSet(VizStack.State.Highlight);

	public bool IsFocused => vizstate.IsSet(VizStack.State.Focus);

	public bool IsActive => vizstate.IsSet(VizStack.State.Active);

	internal void OnFocusChange(bool focused)
	{
		vizstate.Toggle(VizStack.State.Focus, focused);
		_entity.components.SendEvent(EntityEventType.EntityFocusChanged);
	}

	internal void OnHighlightChange(bool highlighted)
	{
		vizstate.Toggle(VizStack.State.Highlight, highlighted);
		_entity.components.SendEvent(EntityEventType.EntityHighlightChanged);
	}

	internal void OnActivationChange(bool activated)
	{
		switch (Config.onaction)
		{
		case SelectionConfig.OnAction.None:
		case SelectionConfig.OnAction.Building:
			Game.serv.input.Replace(new DefaultInputMode());
			break;
		case SelectionConfig.OnAction.Corner:
		{
			Node node = _entity.data.corner.FindNode();
			var (flag2, entity) = Game.ctx.board.CornerCache.FindCorner(node.id);
			if (flag2 && entity != null)
			{
				Game.ctx.hud.cornerInfo.Show(CrewAssignment.EMPTY, node);
			}
			break;
		}
		case SelectionConfig.OnAction.Car:
		case SelectionConfig.OnAction.CarAmbient:
		{
			bool flag = false;
			if (activated)
			{
				PlayerID pid = base.entity.data.mobile?.pid ?? PlayerID.INVALID;
				if (pid.IsHumanPlayer && pid.FindPlayer().crew.FindPeepAssignedToVehicle(base.entity.Id).IsValid)
				{
					flag = true;
				}
			}
			Game.serv.input.Replace(flag ? new CarInputMode(base.entity, fromButton: false) : new DefaultInputMode());
			break;
		}
		default:
			Game.serv.input.Replace(new DefaultInputMode());
			break;
		}
		vizstate.Toggle(VizStack.State.Active, activated);
		_entity.components.SendEvent(EntityEventType.EntityActivationChanged);
	}
}
