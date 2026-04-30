using System.Collections;
using SomaSim.SION;

namespace Game.Services;

public interface ISaveLoadProvider
{
	void Save(Serializer s, ConcurrentSaveTable outdata);

	IEnumerator Load(Hashtable data);
}
