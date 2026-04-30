using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services.Input;

internal class KeyboardInputSource : InputSource, IKeyboardHandler
{
	private BasicKeyboardHandler _keyhandler;

	private float _scrollgain = 0.4f;

	private float _zoomgain = 6f;

	private float _rotgain = 30f;

	public KeyboardHandler GetKeyHandler()
	{
		return _keyhandler;
	}

	public override void Initialize(InputService input)
	{
		base.Initialize(input);
		List<KeyInput> handlers = new List<KeyInput>
		{
			new KeyInput(KeyAction.CameraUp, delegate
			{
				Pan(0f, -1f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraDown, delegate
			{
				Pan(0f, 1f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraLeft, delegate
			{
				Pan(1f, 0f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraRight, delegate
			{
				Pan(-1f, 0f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraZoomOut, delegate
			{
				Zoom(-1f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraZoomIn, delegate
			{
				Zoom(1f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraTurnLeft, delegate
			{
				Rotate(-1f);
			}, KeyInput.PressType.Held),
			new KeyInput(KeyAction.CameraTurnRight, delegate
			{
				Rotate(1f);
			}, KeyInput.PressType.Held)
		};
		_keyhandler = new BasicKeyboardHandler(KeyboardHandler.Priority.Lowest, handlers, KeyboardHandler.Fallthrough.Always);
		Game.serv.keyboard.PushHandler(this);
	}

	public override void Release()
	{
		Game.serv.keyboard.RemoveHandler(this);
		_keyhandler.Reset();
		_keyhandler = null;
		base.Release();
	}

	public override bool Update()
	{
		return false;
	}

	private void Pan(float dx, float dy)
	{
		Vector3 normalized = Game.serv.camera.GetCameraForward().SetY(0f).normalized;
		float num = MathUtil.Clamp(Game.serv.camera.GetZoom(), 50f, 150f);
		if (normalized.magnitude < float.Epsilon)
		{
			normalized = Game.serv.camera.GetCameraUp().SetY(0f).normalized;
		}
		Vector3 vector = Vector3.Cross(normalized, Vector3.up);
		Vector3 vector2 = normalized * dy + -vector * dx;
		WorldPos delta = new WorldPos(vector2 * _scrollgain * num * Time.deltaTime);
		_service.SendPan(delta);
	}

	private void Rotate(float dir)
	{
		_service.SendRotate(dir * _rotgain * Time.deltaTime, tween: false);
	}

	private void Zoom(float dir)
	{
		_service.SendZoom(dir * _zoomgain * Time.deltaTime, tween: false);
	}
}
