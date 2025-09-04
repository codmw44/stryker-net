namespace Hypemasters.Pooling.Infrastructures
{
	public interface IPrefabPoolCallbacks
	{
		void BeforeSpawn();
		void AfterReturnToPool();
	}
}
