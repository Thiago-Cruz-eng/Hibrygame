namespace Orchestrator.UseCases.Dto.Response;

// As quatro respostas de /validation, juntas pelo mesmo motivo dos requests: só fazem sentido
// lidas em conjunto.
//
// Note que elas NÃO seguem a forma { Success, Message } dos outros responses do projeto: cada uma
// tem um único campo booleano, com o nome da pergunta que responde. É uma inconsistência herdada,
// não um padrão a copiar — response novo segue a forma padrão.

/// <summary>
/// Resposta de <c>POST /validation/verify</c>.
/// </summary>
public class VerifyValidationResponse
{
    /// <summary>
    /// Existe autorização de sessão para este usuário?
    ///
    /// <para>
    /// <c>false</c> também é o que se recebe quando a consulta falhou — o serviço captura a exceção,
    /// registra em log e devolve <c>false</c>. Os dois casos são indistinguíveis daqui.
    /// </para>
    /// </summary>
    public bool Valid { get; set; }
}

/// <summary>
/// Resposta de <c>POST /validation/get</c>: a autorização de sessão do usuário.
///
/// <para>
/// <b>O access token não está aqui</b>, embora esteja gravado na entidade — e é justamente por
/// estar gravado em claro (DT-07) que não deve sair numa resposta. Se um campo novo for acrescentado
/// a este tipo, confira que não é o token.
/// </para>
/// </summary>
public class GetValidationResponse
{
    /// <summary>Id do registro de autorização.</summary>
    public string Id { get; set; } = null!;

    /// <summary>Id do usuário dono da autorização.</summary>
    public string UserId { get; set; } = null!;

    /// <summary>E-mail do usuário.</summary>
    public string UserEmail { get; set; } = null!;

    /// <summary>Sala. <c>null</c> enquanto o jogador não entrou em nenhuma.</summary>
    public string? Room { get; set; }

    /// <summary>Cor atribuída. <c>null</c> junto com <see cref="Room"/>.</summary>
    public string? PieceColor { get; set; }

    /// <summary>
    /// Dia da partida, em UTC. Informativo: nenhum filtro do sistema o usa, e a autorização não
    /// expira por data (DT-20).
    /// </summary>
    public DateTime DayOfGame { get; set; }
}

/// <summary>
/// Resposta de <c>POST /validation/update</c>.
/// </summary>
public class UpdateValidationResponse
{
    /// <summary>
    /// Gravou sala e cor? <c>false</c> tanto quando não existe autorização para aquele usuário e
    /// token quanto quando a gravação falhou.
    /// </summary>
    public bool Updated { get; set; }
}

/// <summary>
/// Resposta de <c>POST /validation/can-move</c>.
/// </summary>
public class CanMoveValidationResponse
{
    /// <summary>
    /// Existe autorização casando usuário, token, sala e cor?
    ///
    /// <para>
    /// <b>Não é permissão para jogar.</b> <c>ChessHub.MakeMove</c> decide sozinho e não consulta
    /// isto: um <c>true</c> aqui não garante que o lance será aceito, e um <c>false</c> não o
    /// impede. Serve ao frontend como checagem de tela.
    /// </para>
    /// </summary>
    public bool CanMove { get; set; }
}
