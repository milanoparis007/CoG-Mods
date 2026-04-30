namespace Game.Platform;

public enum kSaveFileResult
{
	Success,
	GenericFailure,
	Canceled,
	NotEnoughSpace,
	ExceededJournalSize
}
