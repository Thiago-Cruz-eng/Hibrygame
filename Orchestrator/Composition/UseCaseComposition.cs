using Orchestrator.UseCases;
using Orchestrator.UseCases.Interfaces;
using Orchestrator.UseCases.Security;

namespace Orchestrator.Composition;

/// <summary>
/// Casos de uso e serviços de aplicação.
/// </summary>
public static class UseCaseComposition
{
    /// <summary>
    /// Registra os casos de uso e os serviços de que eles dependem.
    ///
    /// <para>
    /// <b>Caso de uso novo tem de entrar nesta lista.</b> Não há varredura de assembly nem
    /// registro por convenção — é deliberado, para que a composição da aplicação seja legível num
    /// arquivo. O preço é este: esquecer de registrar compila normalmente e só falha quando a
    /// requisição chega ao controller.
    /// </para>
    ///
    /// <para>
    /// <b>Casos de uso são registrados pela classe concreta</b>, sem interface, porque nada
    /// substitui um caso de uso — nem em teste, onde eles são instanciados diretamente com mocks
    /// das dependências. Já <c>ISecureHashingService</c>, <c>ITokenService</c> e
    /// <c>IValidationService</c> têm interface porque <b>são</b> substituídos: um caso de uso
    /// depende deles e o teste passa um dublê.
    /// </para>
    /// </summary>
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.AddScoped<CreateUserUseCase>();
        services.AddScoped<GetUserUseCase>();
        services.AddScoped<LoginAsyncUseCase>();
        services.AddScoped<UpdateUserUseCase>();
        services.AddScoped<DeleteUserUseCase>();
        services.AddScoped<ChangePasswordUseCase>();
        services.AddScoped<RefreshTokenUseCase>();

        // Depende de CreateUserUseCase e LoginAsyncUseCase, os dois registrados acima: ele compõe
        // os dois em vez de repetir cadastro e emissão de token.
        services.AddScoped<RegisterUserUseCase>();

        services.AddScoped<ISecureHashingService, SecureHashingService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IValidationService, ValidationService>();

        return services;
    }
}
