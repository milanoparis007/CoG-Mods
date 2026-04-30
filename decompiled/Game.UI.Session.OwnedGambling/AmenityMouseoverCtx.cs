using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using UnityEngine;

namespace Game.UI.Session.OwnedGambling;

public class AmenityMouseoverCtx : MonoBehaviour
{
	public OwnedGamblingModel model;

	public AmenityDef def;

	public AmenityData.LastTurnResults lastTurn;

	public bool funded;

	public bool hasManager;

	public bool notDamaged;

	internal void Set(OwnedGamblingModel model, AmenityDef def, AmenityData data)
	{
		ModQuery query = model.Module.MakeManagerBasedModQuery(Game.ctx.players.Human, model.visit.building);
		this.model = model;
		this.def = def;
		lastTurn = data.lastTurnResults;
		PlayerGambling.GamblingHouseStatus gamblingHouseStatus = Game.ctx.players.Human.gambling.GetGamblingHouseStatus(model.visit.building);
		funded = Game.ctx.players.Human.gambling.AmenityIsFunded(model.visit.building, data, query);
		hasManager = gamblingHouseStatus.isManagerPresent;
		notDamaged = gamblingHouseStatus.isNotDamaged;
	}
}
