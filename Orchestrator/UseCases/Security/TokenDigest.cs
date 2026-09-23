using System.Security.Cryptography;
using System.Text;

namespace Orchestrator.UseCases.Security;

/// <summary>
/// Resumo criptográfico (SHA-256) de um access token, para que a coleção <c>Validation</c> deixe
/// de guardar o token utilizável.
///
/// <para>
/// <b>Por que SHA-256 e não PBKDF2 aqui.</b> O access token já é um valor de alta entropia
/// (assinatura HS256 sobre claims), não uma senha escolhida por humano. Contra um segredo de alta
/// entropia, o custo artificial do PBKDF2 não compra nada — não há dicionário a percorrer — e
/// custaria dezenas de milissegundos em <b>toda</b> chamada de <c>/validation</c>. O que se quer
/// aqui é apenas que quem leia a coleção não saia com sessões prontas para usar, e para isso um
/// resumo determinístico basta.
/// </para>
///
/// <para>
/// <b>Determinístico de propósito</b>, sem salt: o filtro do Mongo compara por igualdade, então o
/// mesmo token precisa produzir sempre o mesmo texto. É a diferença em relação a
/// <c>SecureHashingService</c>, que sorteia salt por registro e por isso só pode ser verificado
/// um a um.
/// </para>
///
/// <para>
/// <b>Compatibilidade.</b> Os registros gravados antes desta mudança guardam o token em claro e
/// deixam de casar com o filtro — na prática, quem estava logado precisa autenticar de novo. O
/// alcance é de no máximo uma validade de access token (60 minutos), e o nome do campo
/// (<c>AcessToken</c>, com o erro de digitação herdado) foi mantido justamente para não exigir
/// migração de schema.
/// </para>
/// </summary>
public static class TokenDigest
{
    /// <summary>
    /// O resumo de <paramref name="token"/>, em Base64.
    /// </summary>
    /// <param name="token">
    /// O access token como o cliente o enviou. <c>null</c> ou vazio devolvem o resumo do texto
    /// vazio — não estouram, porque quem chama está num caminho de consulta e ausência já é
    /// tratada como "não encontrado".
    /// </param>
    public static string Compute(string? token)
    {
        var bytes = Encoding.UTF8.GetBytes(token ?? string.Empty);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }
}
