namespace Orchestrator.UseCases.Interfaces;

public interface ISecureHashingService
{
    (string Hash, string Salt) HashValue(string value);
    bool Verify(string value, string hash, string salt);
}
