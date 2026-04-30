namespace Game.Services;

public sealed class GlobalSettings
{
	public GeneralSettings general = new GeneralSettings();

	public EthnicitySettings ethnicities = new EthnicitySettings();

	public PeopleSettings people = new PeopleSettings();

	public ResourceSettings resources = new ResourceSettings();

	public TagSettings tags = new TagSettings();

	public NPCSettings npc = new NPCSettings();

	public QuestSettings quests = new QuestSettings();

	public SkillSettings skills = new SkillSettings();

	public ModelImportSettings modelimport = new ModelImportSettings();

	public GamblingSettings gambling = new GamblingSettings();

	public SchemeSettings schemes = new SchemeSettings();

	public ThroneSettings throne = new ThroneSettings();

	public PoliticsSettings politics = new PoliticsSettings();
}
