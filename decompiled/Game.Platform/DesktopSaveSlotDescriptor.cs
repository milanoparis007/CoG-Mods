using UnityEngine;

namespace Game.Platform;

internal class DesktopSaveSlotDescriptor : IPlatformSaveSlotDescriptor
{
	public string Directory { get; set; }

	public SaveFileMetadata Metadata { get; set; }

	public int SizeInBytes { get; set; }

	public Texture2D PreviewImage { get; set; }
}
