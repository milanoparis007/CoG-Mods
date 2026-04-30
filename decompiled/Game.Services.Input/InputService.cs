using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Services.Input;

public class InputService : AbstractService, IUpdateService, IService
{
	private List<InputSource> _sources;

	private List<IInputHandler> _handlers;

	public float dpi { get; private set; }

	public float LastInputTime { get; private set; }

	public float SecondsSinceLastInput => Time.realtimeSinceStartup - LastInputTime;

	public override void OnInitialized()
	{
		dpi = ((Screen.dpi > 0f) ? Screen.dpi : 72f);
		_sources = new List<InputSource>();
		if (Game.settings.IsDesktop)
		{
			_sources.Add(new MouseInputSource());
		}
		_sources.Add(new KeyboardInputSource());
		_sources.ForEach(delegate(InputSource src)
		{
			src.Initialize(this);
		});
		_handlers = new List<IInputHandler>();
		RegisterLastInputTime();
	}

	public void OnUpdate()
	{
		DoInputUpdate();
	}

	public override void OnReleased()
	{
		while (_handlers.Count > 0)
		{
			Pop();
		}
		_handlers = null;
		_sources.ForEach(delegate(InputSource src)
		{
			src.Release();
		});
		_sources = null;
	}

	public void RegisterLastInputTime()
	{
		LastInputTime = Time.realtimeSinceStartup;
	}

	public void Push(IInputHandler handler)
	{
		if (_handlers.Count > 0)
		{
			_handlers[0].OnHandlerDeactivated();
		}
		_handlers.Insert(0, handler);
		handler.OnHandlerActivated();
	}

	public IInputHandler Pop()
	{
		IInputHandler inputHandler = null;
		if (_handlers.Count > 0)
		{
			inputHandler = _handlers[0];
			_handlers.RemoveAt(0);
			inputHandler.OnHandlerDeactivated();
		}
		if (_handlers.Count > 0)
		{
			_handlers[0].OnHandlerActivated();
		}
		return inputHandler;
	}

	public IInputHandler Peek()
	{
		if (_handlers.Count <= 0)
		{
			return null;
		}
		return _handlers[0];
	}

	public void Replace(IInputHandler handler)
	{
		Flush();
		Push(handler);
	}

	public void Flush()
	{
		while (_handlers.Count > 0)
		{
			Pop();
		}
	}

	private void DoInputUpdate()
	{
		bool flag = false;
		int i = 0;
		for (int count = _sources.Count; i < count; i++)
		{
			bool flag2 = _sources[i].Update();
			flag = flag || flag2;
		}
		if (flag)
		{
			RegisterLastInputTime();
		}
	}

	internal void SendInput(Vector2 before, Vector2 after, InputButton button, InputPhase phase)
	{
		foreach (IInputHandler handler in _handlers)
		{
			bool num;
			if (button != InputButton.Left)
			{
				if (button != InputButton.Right)
				{
					if (button != InputButton.Middle)
					{
						continue;
					}
					num = handler.OnTertiary(before, after, phase);
				}
				else
				{
					num = handler.OnSecondary(before, after, phase);
				}
			}
			else
			{
				num = handler.OnPrimary(before, after, phase);
			}
			if (num)
			{
				break;
			}
		}
	}

	internal void SendZoom(float zoom, bool tween)
	{
		using List<IInputHandler>.Enumerator enumerator = _handlers.GetEnumerator();
		while (enumerator.MoveNext() && !enumerator.Current.OnZoom(zoom, tween))
		{
		}
	}

	internal void SendRotate(float delta, bool tween)
	{
		using List<IInputHandler>.Enumerator enumerator = _handlers.GetEnumerator();
		while (enumerator.MoveNext() && !enumerator.Current.OnRotate(delta, tween))
		{
		}
	}

	internal void SendPan(WorldPos delta)
	{
		using List<IInputHandler>.Enumerator enumerator = _handlers.GetEnumerator();
		while (enumerator.MoveNext() && !enumerator.Current.OnPan(delta))
		{
		}
	}
}
