using System;
using UnityEngine;

namespace Game.Services.Input;

public class KeyInput
{
	public enum InputType
	{
		KeyAction,
		KeyCode
	}

	public enum PressType
	{
		Pressed,
		Released,
		Held
	}

	public readonly InputType input;

	public readonly KeyAction keyaction;

	public readonly KeyCode keycode;

	public readonly PressType type;

	public readonly Action onSuccess;

	public KeyInput(KeyCode key, Action callback, PressType type = PressType.Pressed)
	{
		input = InputType.KeyCode;
		keyaction = KeyAction.None;
		keycode = key;
		this.type = type;
		onSuccess = callback;
	}

	public KeyInput(KeyAction action, Action callback, PressType type = PressType.Pressed)
	{
		input = InputType.KeyAction;
		keyaction = action;
		keycode = KeyCode.None;
		this.type = type;
		onSuccess = callback;
	}
}
