using System.Linq.Expressions;
using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;

namespace Orchestrator.Infra.Repositories;

/// <summary>
/// Base de todos os repositórios de entidade. Implementa
/// <see cref="IGenericRepositoryNoSql{T}"/> inteiro delegando ao CRUD genérico, e é por isso
/// que os repositórios concretos podem ser vazios.
///
/// <para>
/// <b>Como criar um repositório novo</b> — três arquivos, nesta ordem:
/// </para>
/// <list type="number">
///   <item><description>
///   A entidade, herdando de <see cref="BaseEntity"/> e com
///   <c>[CollectionName(nameof(Entidade))]</c>.
///   </description></item>
///   <item><description>
///   A interface: <c>public interface IEntidadeRepositoryNoSql : IGenericRepositoryNoSql&lt;Entidade&gt; { }</c>
///   em <c>Infra/Interfaces/</c>.
///   </description></item>
///   <item><description>
///   A classe: <c>public class EntidadeRepositoryNoSql : BaseRepositoryNoSql&lt;Entidade&gt;, IEntidadeRepositoryNoSql</c>
///   em <c>Infra/Repositories/</c>, com só o construtor repassando o
///   <see cref="IGenericRepository"/>. Depois registre o par em <c>Program.cs</c>.
///   </description></item>
/// </list>
///
/// <para>
/// Query específica de uma entidade (buscar usuário por e-mail, por exemplo) entra na classe
/// concreta — não aqui, que é compartilhada por todas as entidades, e não no
/// <see cref="IGenericRepository"/>, que não conhece entidade alguma.
/// </para>
/// </summary>
/// <typeparam name="T">A entidade persistida por este repositório.</typeparam>
public abstract class BaseRepositoryNoSql<T> : IGenericRepositoryNoSql<T> where T : BaseEntity
{
    /// <summary>
    /// O CRUD genérico. <c>private</c> de propósito: as subclasses não devem acessar o driver
    /// por baixo desta camada. Se uma subclasse precisar de algo que não está aqui, o caminho é
    /// receber o <see cref="IGenericRepository"/> no próprio construtor dela.
    /// </summary>
    private readonly IGenericRepository _genericRepository;

    protected BaseRepositoryNoSql(IGenericRepository genericRepository)
    {
        _genericRepository = genericRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindByFilter(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        => await _genericRepository.GetAll(filter, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<T?> GetById(Guid id, CancellationToken cancellationToken = default)
        => _genericRepository.GetFirstOrDefault<T>(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task Save(T entity, CancellationToken cancellationToken = default)
        => _genericRepository.Save(entity, cancellationToken);

    /// <inheritdoc />
    public Task<bool> Update(string id, T entity, CancellationToken cancellationToken = default)
    {
        // Id que não é Guid é "não encontrado", não erro: quem chama recebeu esse texto de uma
        // rota ou de um corpo de request, e estourar exceção aqui transformaria digitação
        // errada do cliente em 500.
        if (!Guid.TryParse(id, out var guid))
            return Task.FromResult(false);

        return _genericRepository.ReplaceOne<T>(x => x.Id == guid, entity, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> Delete(string id, T entity, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return Task.FromResult(false);

        // `entity` não entra na operação: identidade basta para remover. Ver a nota no
        // contrato.
        return _genericRepository.DeleteOne<T>(x => x.Id == guid, cancellationToken);
    }
}
