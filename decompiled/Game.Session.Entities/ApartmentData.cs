using Game.Core;

namespace Game.Session.Entities;

public struct ApartmentData
{
	public int count;

	public string lastname;

	public Label eth;

	public bool IsSet => count > 0;
}
