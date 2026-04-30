namespace Game.Session;

public enum SessionState
{
	None,
	InitializeStarted,
	InitializeDone,
	BoardInitStarted,
	BoardInitDone,
	CityGenStarted,
	CityGenDone,
	PreInteractiveDone,
	PreInteractiveAIGenDone,
	Interactive,
	PreReleased,
	Released,
	Destroyed
}
