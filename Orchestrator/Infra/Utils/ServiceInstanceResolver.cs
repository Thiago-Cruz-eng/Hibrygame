namespace Orchestrator.Infra.Utils;

public class ServiceInstanceResolver<IService>(IServiceFactory serviceFactory)
{
    private IService? _service;
    public IService Service
    {
        get
        {
            if (_service == null)
            {
                _service = serviceFactory.Create<IService>();
            }
            return _service;
        }
    }
}