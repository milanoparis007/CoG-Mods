using System.Collections;

namespace Game.Services;

public interface IEntityLoadProvider
{
	IEnumerator DelayedEntityLoad();
}
