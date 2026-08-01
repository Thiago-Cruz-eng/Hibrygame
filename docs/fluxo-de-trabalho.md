# Fluxo de trabalho

O sistema tem duas pontas em repositórios separados — `Hibrygame` (API + hub + motor) e
`KrockSide` (interface) — e um contrato de fio entre elas. Este documento é o passo a passo de
como abrir, testar e mergear uma demanda em cada um dos três formatos possíveis.

- [Onde cada teste mora](#onde-cada-teste-mora)
- [Cenário 1 — back e front juntos](#cenário-1--back-e-front-juntos)
- [Cenário 2 — só back](#cenário-2--só-back)
- [Cenário 3 — só front](#cenário-3--só-front)
- [A janela entre os dois merges](#a-janela-entre-os-dois-merges)
- [Como o CI escolhe o outro lado](#como-o-ci-escolhe-o-outro-lado)
- [Com vários devs](#com-vários-devs)

---

## Onde cada teste mora

Antes de qualquer coisa: **teste novo vai na branch do dev, no mesmo PR da mudança.** Nunca
depois, nunca em PR separado. O PR precisa ser autocontido — quem revisa tem que conseguir ver a
mudança e a prova de que ela funciona no mesmo diff.

Três camadas, e escolher errado custa caro nos dois sentidos (teste lento demais para o que
prova, ou rápido demais para pegar o que precisava):

| A mudança é sobre… | Camada | Onde | Custo |
|---|---|---|---|
| Regra de xadrez, caso de uso, entidade, autorização | xUnit + Moq | `Hibrygame.Test/`, `Orchestrator.Test/` | ~30s a suíte toda |
| Renderização, estado de componente, hook, chamada REST isolada | Vitest (+ MSW) | `KrockSide/src/**`, `src/integration/` | ~30s |
| **Junção**: rota, formato do payload, nome do claim, momento em que o SignalR negocia, dois jogadores interagindo, turno, reconexão | **Playwright** | `KrockSide/tests-e2e/` | ~2 min |

A regra prática para a terceira linha: **se o bug só existe quando as partes reais conversam,
nenhuma das duas primeiras camadas vai pegá-lo.** Foi assim com os quatro bloqueadores que
impediam jogar pela interface — rota REST inexistente, campo de resposta divergente, claim `sub`
nunca encontrado, SignalR negociando antes do login. Todos com teste unitário verde do lado que
os continha.

E o inverso também vale: cobrir uma regra de xadrez com Playwright é desperdício. Se dá para
provar com xUnit, prove com xUnit.

> **Cuidado com o dublê que espelha o bug.** A versão anterior da suíte E2E interceptava
> `**/get/**` — exatamente a rota **errada** que o front chamava. O mock reproduzia o erro do
> código, o teste ficava verde, e a operação respondia 404 em produção. Ao escrever mock,
> pergunte de onde veio a URL: se veio do código que você está testando, o teste não prova nada.

### TDD quando a mudança é de regra

Para qualquer coisa que mexa em regra de jogo, a ordem é: escrever o teste **antes**, vê-lo
falhar, então corrigir. Se o teste documenta um bug existente, escreva-o esperando o
comportamento **correto** e confirme que ele falha pelo motivo certo antes de tocar no código.
Teste escrito depois tende a descrever o que o código faz, não o que deveria fazer.

---

## Cenário 1 — back e front juntos

Mudança de contrato: evento novo no hub, campo novo num DTO, endpoint novo consumido pela tela.

### Passo a passo

1. **Mesmo nome de branch nos dois repos.**

   ```bash
   git -C Hibrygame checkout -b feat/promocao-de-peao
   git -C KrockSide  checkout -b feat/promocao-de-peao
   ```

   Não é estética: é assim que o CI descobre que os dois PRs são as duas metades da mesma
   mudança. Ver [como o CI escolhe o outro lado](#como-o-ci-escolhe-o-outro-lado).

2. **Back primeiro, e de forma retrocompatível.** Campo é adicionado, não renomeado. Endpoint
   novo coexiste com o antigo. Parâmetro novo é opcional. Isso não é preciosismo — é o que faz a
   [janela entre os merges](#a-janela-entre-os-dois-merges) desaparecer.

3. **Testes de cada lado, no PR de cada lado.** xUnit para a regra nova no back; Vitest para a
   tela no front; e o Playwright para o caminho ponta a ponta — que mora **sempre** em
   `KrockSide/tests-e2e/`, mesmo quando a coisa sendo provada é do backend.

4. **Atualize `docs/FRONTEND_CHANGES.md`** com uma entrada datada. É o histórico do contrato.

5. **Abra os dois PRs.** Cada gate `e2e` acha a branch homônima do outro lado sozinho e testa o
   par coordenado. Os dois ficam verdes.

6. **Mergeie o back, depois o front.** Sendo retrocompatível, os dois `main` ficam válidos o
   tempo todo e nada fica vermelho no meio.

7. **PR de limpeza depois**, removendo o que ficou órfão (o campo antigo, o endpoint antigo).

### O que o CI faz

| Repo | Gate | Contra o quê |
|---|---|---|
| Hibrygame | `dotnet-test` | ele mesmo |
| Hibrygame | `e2e` | `KrockSide@feat/promocao-de-peao` |
| KrockSide | `build-and-test` | ele mesmo |
| KrockSide | `e2e` | `Hibrygame@feat/promocao-de-peao` |

---

## Cenário 2 — só back

Correção no motor, refactor interno, endpoint que ninguém consome ainda, ajuste de performance.

### Passo a passo

1. Branch só no `Hibrygame`.
2. Teste xUnit no mesmo PR — TDD se for regra de jogo.
3. Abra o PR. Rodam `dotnet-test` e `e2e`.
4. O `e2e` não acha branch homônima no `KrockSide`, então cai para `main` e testa **o seu backend
   novo contra o frontend que está em produção**. Isso é o ponto: prova que você não quebrou
   ninguém.
5. Verde nos dois? Mergeie.

### Se o `e2e` ficar vermelho num PR só de back

Não é falso positivo, e não trate como flaky. Significa uma de duas coisas:

- **Você quebrou o contrato sem perceber.** Aconteceu de verdade neste projeto: renomear um campo
  de resposta do login derrubou a tela inteira, com todos os testes unitários verdes. Ou você
  torna a mudança retrocompatível, ou a demanda virou o [cenário 1](#cenário-1--back-e-front-juntos)
  e precisa de uma branch homônima no front.
- **O comportamento mudou de propósito** e a suíte E2E ainda descreve o antigo. Aí o teste é que
  precisa ser atualizado — e isso nos leva à pegadinha abaixo.

### A pegadinha: mudança de back cujo teste é Playwright

O teste E2E mora no `KrockSide`. Se a sua correção de backend precisa de um teste Playwright novo
— porque é exatamente o tipo de bug de junção que só ele pega — **você não consegue entregá-lo
num PR só de back.** Não existe onde colocar o arquivo.

Nesse caso, mesmo sendo "só back", abra o par:

```bash
git -C KrockSide checkout -b fix/turno-apos-reconexao   # mesmo nome do branch do back
# adiciona só o teste em tests-e2e/
```

O PR do front contém apenas o teste. O gate do back, achando a branch homônima, roda contra ele —
e o teste novo é exercido contra a correção nova, que é o que você queria. Mergeie o back
primeiro, depois o do teste.

Isso é atrito real, e é consequência de o E2E viver num dos dois repos. A alternativa estrutural
(monorepo, ou suíte de contrato em repositório próprio) está registrada em
[`debito-tecnico.md`](./debito-tecnico.md).

---

## Cenário 3 — só front

Ajuste visual, acessibilidade, estado de componente, correção de tela que não muda o que se pede
ao servidor.

### Passo a passo

1. Branch só no `KrockSide`.
2. Vitest no mesmo PR. Playwright também, se a mudança tem caminho de usuário que valha guardar.
3. Abra o PR. Rodam `build-and-test` (lint + tipo + unit/integração + build) e `e2e`.
4. O `e2e` não acha branch homônima no `Hibrygame`, cai para `main` e testa **o seu front novo
   contra a API em produção**.
5. Verde? Mergeie. Não há segunda ponta para coordenar.

### Se o `e2e` ficar vermelho num PR só de front

Quase sempre é o front pedindo algo que a API `main` ainda não oferece — ou seja, a demanda não
era só de front. Vire [cenário 1](#cenário-1--back-e-front-juntos).

---

## A janela entre os dois merges

O único caso que o CI não resolve, e não deveria.

Mergeada a primeira metade de uma mudança **não** retrocompatível, os dois `main` ficam
genuinamente incompatíveis até a segunda subir. O gate vermelho ali está falando a verdade.

O que elimina a janela é a ordem, não configuração: **contrato retrocompatível e back primeiro.**
O front antigo continua funcionando contra o back novo, os dois gates ficam verdes durante toda a
janela, e a remoção do que ficou órfão vira um PR separado depois.

Quando a quebra é inevitável — renomear um evento do hub, mudar o formato de `MapSquare`:

1. Os dois merges na mesma janela, um logo após o outro.
2. Aceite `main` vermelho no meio. É informação verdadeira.
3. **Não use `[skip ci]` para calar o gate.** Se ele incomoda nessa janela, é porque a quebra é
   real. Calar o gate é perder o único aviso que você tem.

---

## Como o CI escolhe o outro lado

Cada job `e2e` resolve a ref do repositório oposto nesta ordem:

1. **Variável de repositório** (`KROCKSIDE_REF` aqui, `HIBRYGAME_REF` lá), se existir.
2. **Branch de mesmo nome** no outro repo, se existir.
3. **`main`.**

A regra 2 é a que faz o dia a dia funcionar sem configurar nada, e é por isso que a convenção de
nome importa.

A regra 1 é escape manual, para quando os nomes precisam divergir. Use com parcimônia: a variável
é **global ao repositório**, então enquanto existir ela desvia *todos* os PRs, inclusive os que
nada têm a ver com aquela mudança. Apague assim que os dois PRs entrarem.

```bash
gh variable list                                  # ver o que está configurado
gh variable set HIBRYGAME_REF --body "minha-branch"
gh variable delete HIBRYGAME_REF                  # sempre, depois do merge
```

---

## Com vários devs

Tudo acima continua valendo. O que muda é que três suposições silenciosas do fluxo deixam de se
sustentar sozinhas, e passam a precisar de regra explícita.

### 1. O nome da branch vira identificador, e precisa ser único

O pareamento automático assume que um nome de branch identifica **uma** mudança. Com um dev, é
verdade por construção. Com seis, dois `fix/turno` em repos diferentes, de pessoas diferentes,
seriam pareados com toda a confiança — e o resultado é **verde enganoso**, que é pior que
vermelho: o gate afirma ter verificado uma integração que nunca existiu.

Por isso o nome da branch **precisa conter o identificador da tarefa**:

```bash
git checkout -b feat/HIB-123-promocao-de-peao     # nos DOIS repos
```

Isto é verificado pelo CI, não apenas recomendado. Se existe branch homônima no outro repo e o
nome não tem identificador de tarefa, o job **falha** com a instrução de renomear. Falhar é a
opção certa aqui: as duas alternativas — parear assim mesmo, ou cair para `main` calado —
produzem verde falso em direções opostas.

O identificador pode ser qualquer coisa que a equipe use de forma única: chave de Jira
(`HIB-123`), número de issue (`#1234` → `1234`), o que for. A verificação exige apenas
`letras-números` ou uma sequência de 3+ dígitos.

### 2. A variável de override passa de inconveniente a perigosa

`HIBRYGAME_REF` / `KROCKSIDE_REF` são variáveis **de repositório**. Com um dev, esquecer de
apagar é chateação. Com uma equipe, enquanto ela existir **todo PR de todo mundo** é testado
contra a branch de uma pessoa — e ninguém tem por que desconfiar disso.

Mitigado, não resolvido: quando o override está em uso, o job emite uma anotação de
**warning visível na aba de checks de cada PR afetado**, dizendo contra o que aquele PR foi
testado e como apagar a variável. Fica difícil de passar despercebido.

Ainda assim, com equipe a regra é: **prefira renomear as branches a usar a variável.** Ela existe
para o caso raro em que renomear não é possível.

### 3. `main` vermelho para de ser problema seu

Com um dev, um `main` quebrado é um incômodo pessoal. Com uma equipe, é bloqueio coletivo:
ninguém consegue distinguir "quebrei agora" de "já estava quebrado", e o hábito de ignorar
vermelho começa exatamente aí.

Consequências práticas:

- **Contrato retrocompatível deixa de ser preferência e vira obrigação.** É o que mantém `main`
  válido durante a [janela entre merges](#a-janela-entre-os-dois-merges).
- **Os checks precisam ser obrigatórios** (Settings → Branches → Require status checks). Hoje eles
  rodam e reportam, mas nada impede mergear no vermelho — o que, com uma pessoa, é disciplina, e
  com seis é só questão de tempo.
- **Considere merge queue** quando o volume justificar. Ele testa a combinação *pós-merge*, não a
  branch isolada, e é o que pega a quebra semântica entre dois PRs que passam sozinhos.

### 4. Suíte E2E é bem comum, e degrada em silêncio

O E2E é o único teste compartilhado entre as duas equipes, e o mais fácil de deixar apodrecer.
Duas regras que evitam isso:

- **Nunca "é só re-rodar".** A suíte já roda com `retries: 2` no CI: o que você vê vermelho já
  falhou três vezes. Re-rodar sem diagnosticar é como o hábito de ignorar começa. Se for
  genuinamente instável, marque `test.fixme` com link para a issue — o teste sai do caminho
  **registrado**, não esquecido.
- **Mantenha-a fina.** Hoje são 28 testes em ~2 min. Ela cobre caminho crítico e junção; detalhe
  desce para xUnit ou Vitest. Uma suíte E2E de 20 minutos é uma suíte que a equipe vai contornar.

### 5. O que ainda não escala, e é bom saber antes

- **Estado do hub é estático e em processo** (`ConcurrentDictionary` no `ChessHub`). No CI não
  incomoda: cada job tem a sua API e o seu MongoDB isolados. Mas um **ambiente compartilhado de
  homologação com mais de uma instância** não funciona sem backplane de Redis. Está no
  [`debito-tecnico.md`](./debito-tecnico.md).
- **A suíte semeia três usuários de e-mail fixo.** Isolado por job, tudo bem. Contra um MongoDB
  compartilhado, duas execuções simultâneas disputam os mesmos usuários. Se um dia apontar o E2E
  para banco compartilhado, os e-mails precisam virar únicos por execução.
- **Nomes de sala já são únicos por teste** (`newRoom()`), então paralelismo dentro do job é
  seguro. Foi de propósito.

### Resumo do que muda

| | Um dev | Equipe |
|---|---|---|
| Nome da branch | qualquer | **com identificador de tarefa** (verificado pelo CI) |
| Variável de override | incômodo se esquecer | evitar; avisa em todo PR afetado |
| Contrato retrocompatível | recomendado | **obrigatório** |
| Checks obrigatórios | opcional | **necessário** |
| Merge queue | desnecessário | considerar conforme o volume |
| E2E instável | irrita | apodrece a suíte — `test.fixme` com issue |

---

## Checklist antes de pedir review

- [ ] Teste novo no mesmo PR, na camada certa (tabela [acima](#onde-cada-teste-mora))
- [ ] Se mexeu em regra de jogo: o teste foi escrito antes e foi visto falhando
- [ ] `dotnet build` sem warning, `dotnet test` verde · `npm run lint` e `npx tsc --noEmit` limpos
- [ ] Se o contrato mudou: branch homônima no outro repo e `docs/FRONTEND_CHANGES.md` atualizado
- [ ] Se criou ou resolveu dívida: [`debito-tecnico.md`](./debito-tecnico.md) atualizado
- [ ] Nenhuma variável `KROCKSIDE_REF` / `HIBRYGAME_REF` sobrando
