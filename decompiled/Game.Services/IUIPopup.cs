using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public interface IUIPopup : ISmartStackElement
{
	bool IsShowing { get; }

	GameObject GameObject { get; }
}
