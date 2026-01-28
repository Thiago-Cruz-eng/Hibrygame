namespace Orchestrator.Infra.Utils;

public class ServiceFactory(IServiceProvider provider) : IServiceFactory
{
    /*
     * Errors in this class are not internationalized since they're not user-facing.
     */
    public T Create<T>()
    {
        try
        {
            var service = provider.GetService<T>();
            if (service != null)
                return service;

            throw new Exception($"Expected service not found: {typeof(T).Name}.");
        }
        catch (Exception e)
        {
            // Log the exception if necessary
            Console.WriteLine($"Error creating service of type {typeof(T).Name}.");
            throw e;
        }
    }
}