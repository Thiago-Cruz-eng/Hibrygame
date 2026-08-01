---
name: usuarios-e-atribuicoes
description: >
  Domínio de usuário do Orchestrator: ciclo de vida (criação anônima, login, atualização,
  troca de senha, exclusão), papel primário versus Assignments com TeamId/RoleId/HierarchyNodes,
  normalização de e-mail e papel, flag MustChangePassword, auditoria de criação e modificação,
  e as regras de unicidade de e-mail. Use ao mexer em User, UserAssignment, HierarchyNode,
  qualquer {Ação}UserUseCase, UserController, cadastro, perfil, papel de usuário ou hierarquia
  de time.
metadata:
  type: domain-skill
---

# Usuários e atribuições

> **Mantendo esta skill**
>
> Atualize sempre que uma regra de usuário mudar. Divergência entre skill e código sem decisão
> registrada é escalada para o humano, não resolvida por conta própria.

## Visão geral do domínio

`User` é a única identidade do sistema: autentica no `/login`, carrega o papel que alimenta as
policies e guarda `Assignments` — vínculos com time e hierarquia. É a raiz de agregado; nada mais
tem coleção própria além de `RefreshToken` e `Validation`.

```
Domain/User.cs             raiz do agregado
Domain/UserAssignment.cs   vínculo embutido + record HierarchyNode aninhado
Domain/AuditInformation.cs CreationInformation / ModificationInformation
UseCases/CreateUserUseCase, GetUserUseCase, UpdateUserUseCase, DeleteUserUseCase,
         ChangePasswordUseCase, LoginAsyncUseCase, RefreshTokenUseCase
Presentation/UserController.cs
```

## Estrutura de `User`

| Campo | Regra |
|---|---|
| `Id` | `Guid` de `BaseEntity`, gerado na construção |
| `Name` | `Trim()` aplicado no caso de uso |
| `Email` | **normalizado** para `Trim().ToLowerInvariant()`; identidade de login |
| `Role` | papel primário, string em português normalizada por `RoleHierarchy.NormalizeRole` |
| `PasswordHash` / `Salt` | PBKDF2 — ver skill `autenticacao-e-autorizacao` |
| `MustChangePassword` | `false` no `Create`; `true` só via `MarkPasswordChangeRequired` |
| `Assignments` | `List<UserAssignment>`, embutida no documento |
| `CreationInformations` | preenchida no `Create` |
| `ModificationInformations` | `null` até a primeira mutação |

Mutadores fluentes: `ChangeName`, `ChangeEmail`, `ChangeRole`, `ChangePassword`,
`ChangeAssignments`, `MarkPasswordChangeRequired`. Todos atualizam
`ModificationInformations`. `Equals`/`GetHashCode` são por `Id`.

**`Role` (papel primário) é o que autoriza.** É o único que vira claim
(`ClaimTypes.Role` em `TokenService`) e o único que `MinimumRoleHandler` lê.
`UserAssignment.RoleName`/`RoleId` **não** participam de autorização hoje — são metadado
organizacional. Não escreva código assumindo que atribuição concede permissão.

## `UserAssignment` e `HierarchyNode`

Documento embutido (sem `BaseEntity`, sem coleção):

```
UserAssignment { TeamName, TeamId, RoleName, RoleId, HierarchyNodes[] }
HierarchyNode  { NodeId, NodeName }   // record, criado por HierarchyNode.Create
```

Operações: `Create`, `ChangeTeam`, `ChangeRole`, `ChangeHierarchy`, `AddHierarchyNode`
(idempotente — não duplica), `RemoveHierarchyNode(nodeId)`. `ToString()` é sobrescrito para
depuração.

Duas ressalvas honestas sobre esta classe:

- **`createdBy`/`modifiedBy` são recebidos e ignorados** — `UserAssignment` não tem auditoria,
  ao contrário de `User`. A assinatura promete o que não entrega.
- `HierarchyNodes` é substituída inteira por `ChangeAssignments`/`ChangeHierarchy`: não há merge.
  `UpdateUserUseCase` recria **todas** as atribuições a partir do request — atualização parcial de
  atribuição não existe, o cliente precisa enviar a lista completa ou perde o que omitir.

## Ciclo de vida

### Criação — `POST /users` (anônimo)

`CreateUserUseCase`:

1. normaliza e-mail; recusa se já existir usuário com ele (`"User already has a account"`);
2. valida o papel com `RoleHierarchy.TryGetLevel`; recusa `"Invalid role"`;
3. normaliza o papel; hasheia a senha; mapeia `Assignments` do DTO;
4. `User.Create(...)` e `Save`.

`CreateUserRequest` exige `Name`, `Email`, `Password`, `Role`, `CreatedBy`, com
`[Compare("Password")]` em `PasswordConfirmation`.

> **Área crítica (DT-04):** o endpoint é `[AllowAnonymous]` e `Role` vem do corpo — qualquer
> anônimo cria `"super adm"`. `CreatedBy` também vem do corpo, então a auditoria de criação é
> forjável. Não amplie a superfície deste endpoint sem resolver a autorização primeiro.

### Login — `POST /login` (anônimo)

`LoginAsyncUseCase`: busca por e-mail normalizado, `Verify` da senha, emite access + refresh,
salva o refresh e **cria um registro de `Validation`** com o access token (ver skill
`validacao-de-sessao-de-jogo`). Falha sempre com a mesma mensagem `"Invalid credentials"`, tanto
para usuário inexistente quanto para senha errada — mantenha assim, é o correto contra
enumeração de usuário.

Retorna `MustChangePassword` para o cliente decidir o fluxo de primeiro acesso. O servidor **não**
bloqueia nada quando a flag é `true` — é dica de UI, não gate.

### Leitura — `GET /users/{id}` (Role:Player)

`GetUserUseCase` devolve `null` (→ `404`) tanto para "não existe" quanto para exceção. Não expõe
`PasswordHash`, `Salt` nem tokens: `GetUserResponse` tem `Id`, `Name`, `Email`, `Role`,
`MustChangePassword`, `Assignments`. **Nunca** adicione hash ou salt a um DTO de resposta.

Não há autorização por dono: qualquer `"jogador"` autenticado lê qualquer usuário por Id.

### Atualização — `PUT /users/{id}` (Role:TeamLeader)

`UpdateUserUseCase`: carrega o usuário; recusa se o e-mail novo já pertencer a **outro** usuário
(`"Email already in use"`); valida e normaliza o papel; então encadeia
`ChangeName().ChangeEmail().ChangeRole().ChangeAssignments()` e faz `Update`.

Consequências: (a) é substituição total, não patch — campo omitido no request vira valor omitido
no banco; (b) `Role` pode ser **elevado** por quem tem `Role:TeamLeader` (nível 3), inclusive para
`"super adm"` (nível 5). Escalonamento de privilégio: se for endurecer, o gate é comparar o nível
do solicitante com o nível-alvo.

### Troca de senha — `POST /users/change-password` (Role:Player)

`ChangePasswordUseCase`: confere a senha atual com `Verify`, gera hash/salt novos, chama
`user.ChangePassword(...)` (que zera `MustChangePassword`) e persiste com `Update` **direcionado**
(`$set` só em `PasswordHash`, `Salt`, `MustChangePassword`, `ModificationInformations`).

Esse `$set` direcionado é o padrão certo para mudança parcial — melhor que `ReplaceOne`, que
sobrescreve o documento inteiro. Mas note dois desvios: o caso de uso injeta `IGenericRepository`
direto em vez de `IUserRepositoryNoSql` (DT-16), e o `UserId` vem do corpo do request, sem
comparar com o claim `sub` — **qualquer `"jogador"` autenticado troca a senha de qualquer usuário
desde que saiba a senha atual**. Ao mexer aqui, aplique o padrão de
`ValidationController.IsCallerAuthorizedFor`.

### Exclusão — `DELETE /users/{id}` (Role:Admin)

`DeleteUserUseCase`: carrega, deleta por Id, devolve `"User not deleted"` se `DeletedCount == 0`.

Exclusão é **física** e não cascateia: `RefreshToken` e `Validation` do usuário ficam órfãos na
base. Tokens órfãos continuam válidos até expirar (não há checagem de existência do usuário no
`MinimumRoleHandler`). Ao implementar exclusão de dados relacionados, lembre que não há transação.

## Regras transversais

- **E-mail é a chave natural de login** e é comparado normalizado. Toda busca por e-mail passa por
  `NormalizeEmail`; nunca compare e-mail cru.
- **Papel sempre normalizado na escrita e validado na leitura** — `TryGetLevel` +
  `NormalizeRole`. Papel desconhecido no banco é negação silenciosa nas policies.
- **Sem unicidade no banco.** A checagem de e-mail duplicado é feita em código (consulta + `if`),
  sem índice único no Mongo: duas criações simultâneas com o mesmo e-mail passam as duas. Índice
  único em `User.Email` é a correção real.
- **Busca por Id usa `user.Id.ToString() == id`** em quatro casos de uso, o que depende de o
  driver traduzir `ToString()` para a comparação com o `Guid` serializado como string.
  `BaseRepositoryNoSql.GetById` faz o certo (`Guid.TryParse` + `x.Id == guid`) — prefira essa
  forma em código novo (DT-17).
