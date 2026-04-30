using System;

namespace Game.Session.Data;

[Flags]
public enum GrantReq
{
	Nothing = 0,
	PlayerID = 1,
	VisitState = 2,
	VisitPeep = 4,
	VisitVehicle = 8,
	VisitNpc = 0x10,
	VisitBuilding = 0x20,
	QuestUUID = 0x40
}
