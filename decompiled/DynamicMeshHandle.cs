public class DynamicMeshHandle
{
	private DynamicMesh _meshRef;

	public DynamicMeshHandle(DynamicMesh mesh)
	{
		_meshRef = mesh;
	}

	public void Remove()
	{
		_meshRef.RemoveMesh(this);
	}
}
