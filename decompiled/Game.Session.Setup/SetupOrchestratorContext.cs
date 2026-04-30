using Game.Session.Board;

namespace Game.Session.Setup;

internal sealed class SetupOrchestratorContext
{
	public GridConnectionData gridConnectionData;

	public LotPlacementData lotPlacementData;

	public BusinessSetupData bizSetupData;

	public WaterRegionData waterRegionData;

	public MountainRegionData mountainRegionData;

	public HeightmapData heightmapData;

	public SpecialEdgeData specialEdgeData;

	public RoadNetworkData roadNetworkData;

	public TransitTileData transitTileData;

	public SpecialBuildingLocationData copStationData;

	public SpecialBuildingLocationData trainStationData;

	public PlayerSetup playerSetup;

	public TerrainGenData MakeTerrainGenData()
	{
		return new TerrainGenData(waterRegionData, mountainRegionData, heightmapData);
	}
}
