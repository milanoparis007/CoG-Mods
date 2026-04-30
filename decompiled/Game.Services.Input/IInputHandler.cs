using Game.Core;
using UnityEngine;

namespace Game.Services.Input;

public interface IInputHandler
{
	void OnHandlerActivated();

	void OnHandlerDeactivated();

	bool OnZoom(float zoom, bool tween);

	bool OnRotate(float direction, bool tween);

	bool OnPan(WorldPos delta);

	bool OnPrimary(Vector2 before, Vector2 after, InputPhase phase);

	bool OnSecondary(Vector2 before, Vector2 after, InputPhase phase);

	bool OnTertiary(Vector2 before, Vector2 after, InputPhase phase);
}
