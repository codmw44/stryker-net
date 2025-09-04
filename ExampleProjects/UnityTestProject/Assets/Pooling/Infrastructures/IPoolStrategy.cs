
namespace Hypemasters.Pooling.Infrastructures
{
	public interface IPoolStrategy : ICreatePoolStrategy, IGetPoolStrategy, IReturnPoolStrategy
	{
	}
}
