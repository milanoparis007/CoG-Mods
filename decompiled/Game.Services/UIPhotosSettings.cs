using System.Collections.Generic;
using Game.Core;

namespace Game.Services;

public sealed class UIPhotosSettings
{
	public sealed class CombatPhotos
	{
		public string attackGun;

		public string attackWeapon;

		public string attackOther;

		public string resultDeath;

		public string resultInjury;

		public string resultOther;
	}

	public CombatPhotos combat;

	public LabelDictionary<List<PhotoConfig>> hints;
}
