using System;

namespace Game.Session.Sim.Modules;

[Flags]
public enum ModuleResult
{
	Default = 0,
	MfgCompleted = 1,
	MfgOutOfInputs = 2,
	MfgOutOfStorageSpace = 4,
	MfgLegitOverproduced = 8,
	ConsCompleted = 0x10,
	ConsOutOfInputs = 0x20,
	BuildingDamaged = 0x40
}
