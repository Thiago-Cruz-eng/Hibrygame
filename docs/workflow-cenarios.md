# Workflow por cenário

Complemento prático de [`fluxo-de-trabalho.md`](./fluxo-de-trabalho.md). Lá estão as regras; aqui
está cada uma delas aplicada a cinco demandas realistas, do primeiro comando ao merge.

- [Os gates que existem](#os-gates-que-existem)
- [Regras que valem sempre](#regras-que-valem-sempre)
- [Cenário A — feature back + front](#cenário-a--feature-back--front-promoção-de-peão)
- [Cenário B — feature só back](#cenário-b--feature-só-back-histórico-de-lances-na-api)
- [Cenário C — feature só front](#cenário-c--feature-só-front-relógio-na-lateral)
- [Cenário D — bug no back](#cenário-d--bug-no-back-roque-permite-atravessar-xeque)
- [Cenário E — bug no front](#cenário-e--bug-no-front-tabuleiro-congela-após-reconexão)
- [Tabela de decisão rápida](#tabela-de-decisão-rápida)

---

## Os gates que existem

| Gate | Repo | Quando | Bloqueia | Duração |
|---|---|---|---|---|
| Compilação + `TreatWarningsAsErrors` | Hibrygame | todo PR/push | sim | ~30s |
| 566 testes xUnit | Hibrygame | todo PR/push | sim | incluso |
| Piso de cobertura (75%/projeto) | Hibrygame | todo PR/push | sim | incluso |
| Cobertura do código novo (80%) | Hibrygame | só PR | sim | incluso |
| Lint (`--max-warnings 0`) | KrockSide | todo PR/push | sim | ~30s |
| Tipo (`tsc --noEmit`) | KrockSide | todo PR/push | sim | incluso |
| 56 testes Vitest + piso de cobertura | KrockSide | todo PR/push | sim | incluso |
| Cobertura do código novo (80%) | KrockSide | só PR | sim | incluso |
| Build de produção | KrockSide | todo PR/push | sim | incluso |
| **E2E Playwright (28) + axe (5)** | **ambos** | todo PR/push | sim | ~2-3 min |
| Pareamento de branch | ambos | todo PR | sim | segundos |
| CodeQL (SAST) | ambos | PR + semanal | sim | 2-20 min |
| NuGet audit | Hibrygame | todo build | não (warning) | — |
| Dependabot | ambos | semanal | não (abre PR) | — |
| Mutação (Stryker) | Hibrygame | semanal | não (relatório) | ~30 min |

---

## Regras que valem sempre

**1. Teste no mesmo PR da mudança.** Nunca depois, nunca em PR separado. Quem revisa precisa ver
a mudança e a prova no mesmo diff.

**2. Nome de branch com identificador de tarefa, igual nos dois repos quando a mudança é
coordenada.** `feat/HIB-123-promocao`. **Verificado pelo CI**: se existe branch homônima no outro
repo e o nome não tem identificador, o job falha. Sem identificador, dois `fix/turno` de pessoas
diferentes seriam pareados — verde afirmando ter verificado uma integração que nunca existiu.

**3. O E2E mora no harness do frontend** (`KrockSide/tests-e2e/`), sempre — inclusive quando o que
está sendo provado é do backend. Não existe suíte Playwright no repo do back. A consequência está
no [cenário D](#cenário-d--bug-no-back-roque-permite-atravessar-xeque).

**4. Escolha a camada certa.** Regra de xadrez → xUnit. Componente → Vitest. Junção → Playwright.
Provar regra de xadrez com Playwright é desperdício; provar junção com mock é ilusão.

**5. TDD para regra de jogo.** Teste antes, visto falhando, então o código. Se documenta bug
existente, escreva esperando o comportamento **correto** e confirme que falha pelo motivo certo.

**6. Código novo vem com 80% de cobertura.** É catraca sobre o diff, não meta global.

**7. Contrato retrocompatível, back primeiro.** É o que faz a janela entre merges desaparecer.

---

## Cenário A — feature back + front (promoção de peão)

*O peão chega à oitava fileira e deve virar dama; a interface precisa perguntar qual peça.*

Toca as duas pontas: regra nova no motor, evento novo no hub, diálogo novo na tela.

### 1. Branch

```bash
git -C Hibrygame checkout -b feat/HIB-140-promocao-de-peao
git -C KrockSide  checkout -b feat/HIB-140-promocao-de-peao
```

Mesmo nome, com identificador. É o que faz cada gate `e2e` encontrar a outra metade sozinho.

### 2. Backend, com TDD e retrocompatível

Teste primeiro, em `Hibrygame.Test`: peão em e7 movendo para e8 vira dama; peão em e2 não promove.
Rode, veja falhar, então implemente em `Move.cs`.

Retrocompatibilidade é o ponto delicado. `MakeMove(room, from, to)` ganha um parâmetro **opcional**
`promoteTo`; sem ele, promove a dama por padrão. Assim o front antigo continua funcionando contra
o back novo, e a janela entre merges desaparece.

Cobertura: o código novo precisa de 80%. Se você escreveu o teste antes, já está resolvido.

### 3. Frontend

Vitest para o diálogo de escolha (renderiza as quatro peças, dispara o callback certo). Playwright
para o caminho inteiro — é onde a integração real acontece:

```
tests-e2e/game.spec.ts → 'peão promovido a dama aparece nos dois tabuleiros'
```

E acessibilidade: o diálogo novo precisa de rótulo, foco e papel corretos, senão o `a11y.spec.ts`
reprova.

### 4. Documentação

`docs/FRONTEND_CHANGES.md` com entrada datada — o parâmetro novo é mudança de contrato.

### 5. Abrir e mergear

Cada PR fica verde contra a metade correspondente. Depois: **back primeiro**, front em seguida, e
um PR de limpeza depois removendo o comportamento padrão se ele deixar de fazer sentido.

| Repo | Gates | Contra |
|---|---|---|
| Hibrygame | dotnet-test, e2e, codeql | `KrockSide@feat/HIB-140-promocao-de-peao` |
| KrockSide | build-and-test, e2e, codeql | `Hibrygame@feat/HIB-140-promocao-de-peao` |

---

## Cenário B — feature só back (histórico de lances na API)

*Novo endpoint `GET /rooms/{room}/moves`. O front ainda não consome.*

### 1. Branch

```bash
git -C Hibrygame checkout -b feat/HIB-155-historico-de-lances
```

Só no back. **Não** crie branch homônima no front — sem ela, o gate testa contra `main`, que é
exatamente o que você quer: prova que o endpoint novo não quebrou quem já existe.

### 2. Implementar

xUnit para o caso de uso e para o controller. Se o histórico exige guardar estado novo no
`GameRoom`, teste de hub também.

Atenção ao `TreatWarningsAsErrors`: parâmetro não usado ou resultado descartado agora **quebra o
build**, não avisa.

### 3. O que o CI faz

`dotnet-test`, `codeql` e `e2e` contra `KrockSide@main`.

### 4. Se o `e2e` ficar vermelho

Não é falso positivo. Ou você quebrou o contrato sem perceber — e aí a demanda virou o
[cenário A](#cenário-a--feature-back--front-promoção-de-peão), precisando de branch homônima — ou
mudou comportamento de propósito e a suíte descreve o antigo.

### 5. Mergear

Direto, sem coordenação. O front passa a consumir quando quiser, em PR próprio.

---

## Cenário C — feature só front (relógio na lateral)

*Cronômetro decrescente por jogador. Puramente visual: o servidor não controla tempo.*

### 1. Branch

```bash
git -C KrockSide checkout -b feat/KRK-88-relogio-da-partida
```

### 2. Implementar

Vitest para o hook do relógio (conta para baixo, pausa fora da vez, zera ao fim). Playwright se há
caminho de usuário que valha guardar. E `a11y.spec.ts` vai passar sobre a tela nova
automaticamente — se o relógio não tiver rótulo acessível, reprova.

Cuidado com o piso de cobertura em `vite.config.ts`: componente grande sem teste **derruba a
média** e reprova mesmo com o diff coberto.

### 3. O que o CI faz

`build-and-test`, `codeql` e `e2e` contra `Hibrygame@main` — seu front novo contra a API real.

### 4. Se o `e2e` ficar vermelho

Quase sempre é o front pedindo algo que a API `main` não oferece — então não era só de front.
Vire [cenário A](#cenário-a--feature-back--front-promoção-de-peão).

### 5. Mergear

Direto.

---

## Cenário D — bug no back (roque permite atravessar xeque)

*O rei roca passando por casa atacada. Regra ilegal aceita pelo servidor.*

Este cenário existe para mostrar a pegadinha que o resto não mostra.

### 1. Reproduzir com teste, antes de tocar no código

```bash
git -C Hibrygame checkout -b fix/HIB-161-roque-atraves-de-xeque
```

Em `Hibrygame.Test`, escreva o teste esperando o comportamento **correto** — roque recusado.
Rode. Ele tem de falhar, e **pelo motivo certo**: se falhar por montagem errada do tabuleiro, você
está corrigindo o teste, não o bug.

### 2. A pegadinha: e se o teste certo for E2E?

Se o bug fosse de junção — servidor aceita mas o front não reflete, ou só aparece com dois
jogadores reais — o teste pertence ao Playwright. E o Playwright mora no `KrockSide`. **Não existe
onde colocar o arquivo num PR só de back.**

Nesse caso, mesmo sendo bug de backend, abra o par:

```bash
git -C KrockSide checkout -b fix/HIB-161-roque-atraves-de-xeque   # mesmo nome
# contém APENAS o teste novo em tests-e2e/
```

O PR do front não tem mudança de código de aplicação — só o teste. O gate do back, achando a
homônima, roda o teste novo contra a correção nova. Mergeie o back primeiro, o do teste depois.

Este é o único caso em que um PR de "só teste" é legítimo, e a razão é estrutural: a suíte E2E
vive num dos dois repos.

### 3. Corrigir

Agora sim, `Move.cs`. O teste passa a verde. Rode a suíte inteira: correção em geometria de peça é
onde regressão mais aparece.

### 4. Fechar o ciclo

Duas coisas que o CI não faz por você:

- Se havia dívida técnica relacionada, atualize [`debito-tecnico.md`](./debito-tecnico.md).
- Pergunte por que passou. Faltava teste? A skill do domínio descrevia errado? Se sim, corrija a
  skill em `.agents/skills/` — senão o mesmo bug volta por outro caminho.

### 5. Mergear

Se abriu o par, back primeiro.

---

## Cenário E — bug no front (tabuleiro congela após reconexão)

*O jogador recarrega a página e o tabuleiro para de responder. Nenhum erro visível.*

### 1. Branch

```bash
git -C KrockSide checkout -b fix/KRK-172-tabuleiro-apos-reconexao
```

### 2. Reproduzir na camada certa — e esta é a decisão que importa

A tentação é escrever um teste de componente com hub mockado. **Cuidado**: bug de reconexão vive
na junção entre `sessionStorage`, ciclo de vida do SignalR e estado do servidor. Um dublê que você
escreve a partir do código que está depurando reproduz a sua interpretação do bug, não o bug.

Foi exatamente assim que quatro bloqueadores passaram batido aqui. A regra: **se o bug só existe
quando as partes reais conversam, o teste é Playwright.**

```
tests-e2e/game.spec.ts → 'recarregar no meio da partida mantém o lado e o estado'
```

### 3. Corrigir e verificar sem ocupar as portas do time

```bash
npx playwright test tests-e2e/game.spec.ts -g "recarregar"   # só o que interessa
```

Rodar a suíte inteira a cada tentativa é o erro que transforma um ciclo de 20 segundos em um de
3 minutos. Rode o alvo; a suíte completa uma vez, no fim.

### 4. Cuidado com teste que trava em vez de falhar

Elemento com `pointer-events: none` faz `click()` esperar até estourar o timeout. Se o seu teste
está levando minutos, quase sempre é isto — e não lentidão da aplicação. Verifique estado inerte
por classe ou CSS, não tentando clicar.

### 5. Mergear

Direto, salvo se a investigação revelar causa no servidor — aí vire
[cenário D](#cenário-d--bug-no-back-roque-permite-atravessar-xeque).

---

## Tabela de decisão rápida

| Situação | Branch no back | Branch no front | E2E testa contra | Ordem de merge |
|---|---|---|---|---|
| Feature nas duas pontas | sim | sim, mesmo nome | a metade oposta | back → front → limpeza |
| Feature só back | sim | não | front `main` | direto |
| Feature só front | não | sim | back `main` | direto |
| Bug no back, teste xUnit | sim | não | front `main` | direto |
| Bug no back, teste E2E | sim | sim (só o teste) | a metade oposta | back → teste |
| Bug no front | não | sim | back `main` | direto |

**Se está em dúvida entre "só back" e "as duas pontas":** abra só o back. Se o `e2e` contra o
front `main` passar, era só back mesmo. Se reprovar, o CI acabou de te dizer que era coordenado —
e é bem mais barato descobrir assim do que em produção.
