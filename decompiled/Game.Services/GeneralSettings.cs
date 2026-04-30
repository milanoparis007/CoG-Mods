namespace Game.Services;

public sealed class GeneralSettings : ISettingsLoadObserver
{
	public DebugSettings debug = new DebugSettings();

	public VisualSettings visuals = new VisualSettings();

	public PaletteSettings palette = new PaletteSettings();

	public TerritorySettings territory = new TerritorySettings();

	public TrainSettings trains = new TrainSettings();

	public TutorialSettings tutorial = new TutorialSettings();

	public DLCSettings dlcs = new DLCSettings();

	public GeneratorSettings generator = new GeneratorSettings();

	public VictorySettings victory = new VictorySettings();

	public EncyclopediaSettings encyclopedia = new EncyclopediaSettings();

	public void OnAfterSettingsLoaded()
	{
	}
}
