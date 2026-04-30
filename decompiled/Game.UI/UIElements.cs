using Game.Services;

namespace Game.UI;

public static class UIElements
{
	public static readonly UIReference MainMenu = new UIReference("Main Menu", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference GeneratingCity = new UIReference("Generating Overlay", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference Mouseovers = new UIReference("Mouseovers", UIType.Container, UIScene.GameUI);

	public static readonly UIReference NewGameCityPopup = new UIReference("New Game City Popup", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference NewGameBossPopup = new UIReference("New Game Boss Popup", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference NewGameCustomizePopup = new UIReference("New Game Customize Popup", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference SaveLoad = new UIReference("Save Load Popup", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference OptionsPopup = new UIReference("Options Popup", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference OkCancelPopup = new UIReference("Ok Cancel Popup", UIType.ModalStacked, UIScene.GameUI);

	public static readonly UIReference Picks = new UIReference("Picks", UIType.Container, UIScene.MovingUI);

	public static readonly UIReference Flyouts = new UIReference("Flyouts", UIType.Container, UIScene.MovingUI);

	public static readonly UIReference HUDBar = new UIReference("HUD Bar", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference TickerBar = new UIReference("Ticker Bar", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference QuestBar = new UIReference("Quest Bar", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference CrewDialog = new UIReference("Crew Dialog", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference OverlaysBar = new UIReference("Overlays Bar", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference ReportsBar = new UIReference("Reports Bar", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference ResourcesBar = new UIReference("Resources Bar", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference FrancineDialog = new UIReference("Francine Dialog", UIType.NonModalHUD, UIScene.SessionUI);

	public static readonly UIReference HUDConvo = new UIReference("Conversation", UIType.NonModalSelected, UIScene.SessionUI);

	public static readonly UIReference HUDUnknownBuildingInfo = new UIReference("Unknown Building Dialog", UIType.NonModalSelected, UIScene.SessionUI);

	public static readonly UIReference HUDOwnedBiz = new UIReference("Owned Biz", UIType.NonModalSelected, UIScene.SessionUI);

	public static readonly UIReference HUDOwnedGambling = new UIReference("Gambling House", UIType.NonModalSelected, UIScene.SessionUI);

	public static readonly UIReference HUDPolitics = new UIReference("Ward Politics Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference StationInfoDialog = new UIReference("Station Info Dialog", UIType.NonModalSelected, UIScene.SessionUI);

	public static readonly UIReference HUDDeliveries = new UIReference("Deliveries Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference HUDCornerInfo = new UIReference("Corner Info", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference PersonInfoDialog = new UIReference("Person Info Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference ItemListDialog = new UIReference("Item List Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference LedgerDialog = new UIReference("Ledger Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference VictoryDialog = new UIReference("Victory Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference CrewInfoListDialog = new UIReference("Crew Info List Dialog", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference DebugConsole = new UIReference("Debug Console", UIType.NonModalFloating, UIScene.SessionUI);

	public static readonly UIReference PortraitPopup = new UIReference("Portrait Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference RenamePopup = new UIReference("Rename Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference ItemListPopup = new UIReference("Item List Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference CombatPopupPlanning = new UIReference("Combat Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference CombatPopupResults = new UIReference("Combat Results Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference NewspaperPopup = new UIReference("Newspaper Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference PeepDeathPopup = new UIReference("Peep Death Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference OwnedBizModulePopup = new UIReference("Module Install Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference OwnedBizModuleOkPopup = new UIReference("Module Install Ok Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference OwnedGamblingAmenityPopup = new UIReference("Amenity Install Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference DestroyResourcePopup = new UIReference("Destroy Resource Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference CrewManagementPopup = new UIReference("Crew Mgmt Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference ItemOrderPopup = new UIReference("Item Order Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference InventoryPopup = new UIReference("Inventory Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference SelectBuildingPopup = new UIReference("Select Building Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference SelectPersonPopup = new UIReference("Select Person Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference EntitySelectionPopup = new UIReference("Entity Selection Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference LevelupPopup = new UIReference("Levelup Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference EncyclopediaPopup = new UIReference("Encyclopedia Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference VictoryPopup = new UIReference("Victory Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference PhotoPopup = new UIReference("Photos Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference NewGameIntroPopup = new UIReference("New Game Intro Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference GameOverPopup = new UIReference("Game Over Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference SaveQuitPopup = new UIReference("Save Quit Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference ExportMapPopup = new UIReference("Export Map Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference CrewPeepInspectPopup = new UIReference("Crew Peep Inspect Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference SchemePopup = new UIReference("Scheme Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference OrgChartPopup = new UIReference("Org Chart Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference ThroneRoomPopup = new UIReference("Throne Room Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference ThroneRoomSelect = new UIReference("Throne Selection Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference RoleAssignPopup = new UIReference("Role Assign Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference LawShopPopup = new UIReference("Law Shop Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference PoliticianSelectPopup = new UIReference("Politician Select Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference PoliticianInfoPopup = new UIReference("Politician Info Popup", UIType.ModalStacked, UIScene.SessionUI);

	public static readonly UIReference ElectionDayPopup = new UIReference("Election Day Popup", UIType.ModalStacked, UIScene.SessionUI);
}
