namespace Game.Platform;

public class PlatformSaveSlotRequest
{
	public string guid = string.Empty;

	public SaveFileMetadata metadata;

	public SaveFileContents savedata;

	public byte[] texture;
}
