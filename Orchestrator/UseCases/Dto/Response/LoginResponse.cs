namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>POST /login</c>.
///
/// <para>
/// <b>Por que quase tudo é anulável.</b> O mesmo tipo serve para sucesso e para falha: em caso de
/// falha só <see cref="Success"/> e <see cref="Message"/> vêm preenchidos, e todo o resto fica
/// <c>null</c>. Antes de usar qualquer campo, teste <see cref="Success"/>.
/// </para>
///
/// <para>
/// <b>Este tipo é contrato com o frontend.</b> Os nomes viajam em JSON e o KrockSide os lê. Ao
/// alterar, registre em <c>docs/FRONTEND_CHANGES.md</c> no mesmo PR.
/// </para>
/// </summary>
public class LoginResponse
{
    /// <summary>Autenticou? É o primeiro campo a olhar; o resto só faz sentido se for <c>true</c>.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// O JWT, para o cabeçalho <c>Authorization: Bearer</c> nas chamadas REST e para o
    /// <c>?access_token=</c> na conexão com o <c>/chesshub</c> — o WebSocket não carrega cabeçalho
    /// próprio, e é por isso que existe essa exceção.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// O refresh token <b>em claro</b>. Esta resposta é a <b>única</b> vez em que ele existe: no
    /// banco há só o hash. Se o cliente não guardar agora, o valor está perdido e o usuário terá de
    /// fazer login outra vez.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Quando o <see cref="AccessToken"/> expira, em UTC. Vai junto para o cliente renovar
    /// <b>antes</b> de tomar 401, em vez de descobrir a expiração por uma chamada que falhou.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>E-mail do usuário, já na forma normalizada em que ficou gravado.</summary>
    public string? Email { get; set; }

    /// <summary>Id do usuário, como texto.</summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Papel do usuário, em português.
    ///
    /// <para>
    /// <b>É papel de autorização, não cor de peça.</b> O frontend já derivou a cor deste campo uma
    /// vez, e o resultado era sempre <c>'None'</c>: o tabuleiro ficava inerte. A cor vem do servidor
    /// em <c>JoinRoom</c>, no hub.
    /// </para>
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Verdadeiro quando a senha foi definida por outra pessoa. O frontend usa isto para levar o
    /// usuário à troca de senha em vez do lobby.
    ///
    /// <para>
    /// Não é anulável, então em resposta de falha vem <c>false</c> — o que não significa nada. Só
    /// leia se <see cref="Success"/> for <c>true</c>.
    /// </para>
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Mensagem legível. Em falha de credencial é sempre <c>"Invalid credentials"</c>, igual para
    /// e-mail inexistente e senha errada — ver <c>LoginAsyncUseCase</c>. <c>"Login failed"</c>
    /// indica falha do sistema, não credencial errada.
    /// </summary>
    public string Message { get; set; } = null!;
}
