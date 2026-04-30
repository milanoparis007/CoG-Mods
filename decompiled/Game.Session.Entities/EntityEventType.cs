namespace Game.Session.Entities;

public enum EntityEventType
{
	EntityEnabled,
	EntityDisabled,
	EntityCreatedViaSim,
	EntityCreatedViaLoading,
	EntityCellStatusChanged,
	EntityActivationChanged,
	EntityFocusChanged,
	EntityHighlightChanged,
	EntityKnownChanged,
	EntityModelLoadedAsync
}
