using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Domain;
using Orchestrator.Infra.Settings;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases.Security;

/// <summary>
/// Emite os dois tokens descritos em <see cref="ITokenService"/>.
///
/// <para>
/// <b>O que é um JWT, em uma frase:</b> um texto em três partes separadas por ponto —
/// cabeçalho, dados (os "claims") e assinatura. As duas primeiras são apenas Base64, portanto
/// <b>legíveis por qualquer um</b>: cole um token em jwt.io e você lê o conteúdo. O que a
/// assinatura garante não é sigilo, é integridade — que ninguém alterou os dados, porque
/// recalcular a assinatura exige a chave secreta.
/// </para>
///
/// <para>
/// A consequência prática é a regra mais importante deste arquivo: <b>nunca coloque num claim
/// algo que o usuário não possa ver</b>. Nada de hash de senha, nada de dado sensível de
/// terceiro.
/// </para>
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly ISecureHashingService _hashingService;

    public TokenService(IOptions<JwtSettings> settings, ISecureHashingService hashingService)
    {
        // .Value resolve a configuração agora; IOptions é o que permite que ela venha de
        // appsettings, variável de ambiente ou cofre sem esta classe saber de qual.
        _settings = settings.Value;
        _hashingService = hashingService;
    }

    /// <inheritdoc />
    public AccessTokenResult CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: BuildClaims(user),
            expires: expires,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>
    /// Os dados que vão dentro do token.
    ///
    /// <para>
    /// <b>Estes nomes de claim são contrato.</b> O servidor os lê de volta exatamente assim, e o
    /// frontend também decodifica o token para saber quem está logado. Renomear um claim aqui
    /// quebra as duas pontas de uma vez, e o sintoma é silencioso: a leitura devolve <c>null</c>
    /// em vez de estourar.
    /// </para>
    ///
    /// <para>
    /// Há uma armadilha relacionada, já resolvida mas que vale conhecer: por padrão o handler do
    /// ASP.NET <b>renomeia</b> claims na entrada — <c>sub</c> viraria
    /// <c>ClaimTypes.NameIdentifier</c>. É por isso que o <c>Program.cs</c> desliga o mapeamento
    /// com <c>MapInboundClaims = false</c>. Sem isso, procurar por <c>sub</c> no servidor não
    /// encontra nada.
    /// </para>
    /// </summary>
    private static List<Claim> BuildClaims(User user) =>
    [
        // `sub` (subject) é o identificador do dono do token no padrão JWT. É o claim que
        // ValidationController compara com o userId do corpo do request, para impedir que
        // alguém opere sobre a conta de outro.
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

        new(JwtRegisteredClaimNames.Email, user.Email),
        new("name", user.Name),

        // ClaimTypes.Role é o nome que MinimumRoleHandler procura, e também o que o ASP.NET
        // entende como papel. O valor é o texto em português gravado no usuário.
        new(ClaimTypes.Role, user.Role),

        // `jti` (JWT ID) é um identificador único deste token. Não é usado hoje: serviria para
        // uma lista de tokens invalidados, que o projeto não tem.
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    ];

    /// <inheritdoc />
    public RefreshTokenIssueResult CreateRefreshToken(User user)
    {
        // 64 bytes de aleatoriedade criptográfica. Diferente do access token, este valor não
        // carrega informação nenhuma e não é assinado — ele é apenas grande e imprevisível o
        // bastante para não ser adivinhado. Base64 só para trafegar como texto.
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        // Vai para o banco hasheado e com salt, igual a uma senha: quem ler a coleção não
        // consegue usar o token.
        var (hash, salt) = _hashingService.HashValue(rawToken);

        var expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);
        var refreshToken = RefreshToken.Create(user.Id, hash, salt, expiresAt);

        // Devolve os dois lados: o valor em claro (que só existe aqui e na resposta HTTP) e a
        // entidade a persistir. Ver a nota em RefreshTokenIssueResult.
        return new RefreshTokenIssueResult(rawToken, refreshToken);
    }
}
