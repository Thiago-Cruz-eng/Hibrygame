namespace Orchestrator.Infra.Settings;

/// <summary>
/// A seção <c>"Jwt"</c> do <c>appsettings.json</c>, tipada.
///
/// <para>
/// Chega ao código por duas vias: <c>IOptions&lt;JwtSettings&gt;</c> injetado (é como
/// <c>TokenService</c> a recebe) e leitura direta no <c>Program.cs</c>, que precisa dos valores
/// antes de a injeção de dependências existir, para configurar a validação do token.
/// </para>
///
/// <para>
/// Os setters são <c>public</c> e as propriedades têm valor padrão porque é assim que o binder de
/// configuração do .NET preenche a classe. Não é entidade de domínio e não segue o contrato de
/// <c>BaseEntity</c>.
/// </para>
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Chave secreta que assina e valida os tokens. Quem tem esta chave pode <b>forjar</b>
    /// qualquer token, para qualquer usuário e qualquer papel.
    ///
    /// <para>
    /// <b>Mínimo de 32 bytes.</b> HS256 exige 256 bits, e o <c>Program.cs</c> falha na subida da
    /// aplicação se a chave for menor — de propósito. Antes dessa verificação, uma chave curta
    /// deixava o app subir normalmente e fazia <b>todo</b> login falhar com "Login failed"
    /// genérico, indistinguível de senha errada.
    /// </para>
    ///
    /// <para>
    /// <b>O valor no <c>appsettings.json</c> é de desenvolvimento.</b> Em produção tem de vir de
    /// variável de ambiente ou cofre de segredos, nunca do arquivo versionado.
    /// </para>
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Quem emitiu o token. Gravado no token e conferido na validação, então
    /// <b>tem de ser o mesmo valor nas duas pontas</b> — divergência faz todo token ser recusado.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Para quem o token vale. Mesma regra do <see cref="Issuer"/>: precisa casar com o que a
    /// validação espera.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Validade do access token, em minutos.
    ///
    /// <para>
    /// Curto de propósito: o access token não é revogável — não há lista de tokens inválidos, então
    /// um token vazado vale até expirar. A validade curta é o que limita esse dano, e o refresh
    /// token (esse sim revogável) é o que evita pedir a senha de novo a cada expiração.
    /// </para>
    /// </summary>
    public int ExpiresMinutes { get; set; } = 60;

    /// <summary>
    /// Validade do refresh token, em dias. Longo porque define de quanto em quanto tempo o usuário
    /// tem de digitar a senha outra vez; é seguro ser longo porque este token pode ser revogado.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 30;
}
