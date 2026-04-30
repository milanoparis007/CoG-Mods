using UnityEngine;

namespace Game.Services.Input;

internal class MouseInputSource : InputSource
{
	public class MouseStateMachine
	{
		public InputButton button;

		public bool dohover;

		public MouseInputSource source;

		private bool _firstrun = true;

		private Vector2 _lastpos;

		private bool _wasdown;

		public bool SendInputs(Vector2 pos)
		{
			if (source.Service == null)
			{
				return false;
			}
			bool result = false;
			if (_firstrun)
			{
				_firstrun = false;
				_lastpos = pos;
			}
			bool mouseButtonUp = UnityEngine.Input.GetMouseButtonUp((int)button);
			bool mouseButtonDown = UnityEngine.Input.GetMouseButtonDown((int)button);
			bool mouseButton = UnityEngine.Input.GetMouseButton((int)button);
			if (_wasdown && (mouseButtonUp || !mouseButton))
			{
				source.Service.SendInput(_lastpos, pos, button, InputPhase.ButtonEnded);
				_wasdown = false;
				result = true;
			}
			if (_wasdown && mouseButton)
			{
				source.Service.SendInput(_lastpos, pos, button, InputPhase.ButtonMoved);
				result = true;
			}
			if (!_wasdown && mouseButtonDown)
			{
				source.Service.SendInput(_lastpos, pos, button, InputPhase.ButtonBegan);
				_wasdown = true;
				result = true;
			}
			if (!mouseButton && dohover)
			{
				source.Service.SendInput(_lastpos, pos, button, InputPhase.HoverMoved);
			}
			_lastpos = pos;
			return result;
		}
	}

	public string MouseScrollWheelName = "Mouse ScrollWheel";

	private float _zoomgain = 20f;

	private MouseStateMachine _left;

	private MouseStateMachine _right;

	private MouseStateMachine _middle;

	internal InputService Service => _service;

	public MouseInputSource()
	{
		_left = new MouseStateMachine
		{
			button = InputButton.Left,
			dohover = true,
			source = this
		};
		_right = new MouseStateMachine
		{
			button = InputButton.Right,
			dohover = false,
			source = this
		};
		_middle = new MouseStateMachine
		{
			button = InputButton.Middle,
			dohover = false,
			source = this
		};
	}

	public override bool Update()
	{
		if (!UnityEngine.Input.mousePresent)
		{
			return false;
		}
		Vector2 pos = UnityEngine.Input.mousePosition;
		bool num = _left.SendInputs(pos);
		bool flag = _right.SendInputs(pos);
		bool flag2 = _middle.SendInputs(pos);
		bool result = num || flag || flag2;
		float axis = UnityEngine.Input.GetAxis(MouseScrollWheelName);
		if (axis != 0f)
		{
			_service.SendZoom(_zoomgain * axis, tween: true);
			result = true;
		}
		return result;
	}
}
