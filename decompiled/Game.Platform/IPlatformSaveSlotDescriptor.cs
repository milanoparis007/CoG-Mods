using UnityEngine;

namespace Game.Platform;

public interface IPlatformSaveSlotDescriptor
{
	string Directory { get; }

	SaveFileMetadata Metadata { get; }

	Texture2D PreviewImage { get; }

	int SizeInBytes { get; }
}
