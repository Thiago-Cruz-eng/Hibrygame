namespace Orchestrator.Infra.Utils;

public interface IServiceFactory
{
    T Create<T>();
}