using System.Collections.Generic;
using UnityEngine;

namespace Game.Services.Input;

public class KeyboardService : AbstractService, IUpdateService, IService
{
	private List<IKeyboardHandler> _handlers;

	public KeyMapper keymapper;

	private List<IKeyboardHandler> _tmp_handlers = new List<IKeyboardHandler>(8);

	public override void OnInitialized()
	{
		_handlers = new List<IKeyboardHandler>();
		keymapper = new KeyMapper();
		if (Game.serv.saveload.prefs.game.keymapper != null)
		{
			keymapper.LoadFrom(Game.serv.saveload.prefs.game.keymapper);
		}
	}

	public void OnUpdate()
	{
		DoInputUpdate();
	}

	public override void OnReleased()
	{
		keymapper = null;
		_handlers = null;
	}

	public void PushHandler(IKeyboardHandler handler)
	{
		_handlers.Add(handler);
		_handlers.Sort(IKeyboardHandlerComparison);
	}

	public void RemoveHandler(IKeyboardHandler handler)
	{
		_handlers.Remove(handler);
		_handlers.Sort(IKeyboardHandlerComparison);
	}

	private static int IKeyboardHandlerComparison(IKeyboardHandler a, IKeyboardHandler b)
	{
		return a.GetKeyHandler().priority - b.GetKeyHandler().priority;
	}

	private void DoInputUpdate()
	{
		if (_handlers.Count == 0)
		{
			if (Game.settings != null)
			{
				Logger.Warning("No keyboard handlers are registered");
			}
			return;
		}
		_tmp_handlers.Clear();
		_tmp_handlers.AddRange(_handlers);
		bool flag = false;
		for (int i = 0; i < _tmp_handlers.Count; i++)
		{
			KeyboardHandler keyHandler = _tmp_handlers[i].GetKeyHandler();
			if (keyHandler == null)
			{
				continue;
			}
			bool flag2 = false;
			foreach (KeyInput keyhandler in keyHandler.keyhandlers)
			{
				if (IsActive(keyhandler))
				{
					flag2 = true;
					flag = true;
					keyhandler.onSuccess();
				}
			}
			if (keyHandler.fallthrough == KeyboardHandler.Fallthrough.Never || (flag2 && keyHandler.fallthrough == KeyboardHandler.Fallthrough.OnlyIfNotProcessed))
			{
				break;
			}
		}
		if (flag)
		{
			Game.serv.input.RegisterLastInputTime();
		}
	}

	public bool IsActive(KeyInput key)
	{
		KeyInput.PressType type = key.type;
		switch (key.input)
		{
		case KeyInput.InputType.KeyCode:
			return WasKeyPressed(type, key.keycode);
		case KeyInput.InputType.KeyAction:
		{
			KeyMappingTuple? keyMappingTuple = keymapper.FindMapping(key.keyaction);
			if (!keyMappingTuple.HasValue)
			{
				return false;
			}
			if (!WasKeyPressed(type, keyMappingTuple.Value.mainkey))
			{
				return WasKeyPressed(type, keyMappingTuple.Value.altkey);
			}
			return true;
		}
		default:
			return false;
		}
	}

	private bool WasKeyPressed(KeyInput.PressType type, KeyCode keycode)
	{
		if (keycode != KeyCode.None)
		{
			switch (type)
			{
			case KeyInput.PressType.Pressed:
				return UnityEngine.Input.GetKeyDown(keycode);
			case KeyInput.PressType.Released:
				return UnityEngine.Input.GetKeyUp(keycode);
			case KeyInput.PressType.Held:
				return UnityEngine.Input.GetKey(keycode);
			}
			Logger.Error("If only C# had branch analysis for switches it could see this is unreachable");
		}
		return false;
	}

	public void ModifyMapping(KeyAction action, KeyCode? main, KeyCode? alt)
	{
		KeyMappingTuple? keyMappingTuple = keymapper.FindMapping(action);
		if (!keyMappingTuple.HasValue)
		{
			Logger.Error("Missing key action: " + action);
			return;
		}
		KeyMappingTuple value = keyMappingTuple.Value;
		if (main.HasValue)
		{
			value.mainkey = main.Value;
		}
		if (alt.HasValue)
		{
			value.altkey = alt.Value;
		}
		keymapper.UpdateMapping(value);
	}
}
