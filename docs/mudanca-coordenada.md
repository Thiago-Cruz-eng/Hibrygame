# Mudança coordenada entre back e front

O contrato de fio entre `Hibrygame` (API + hub) e `KrockSide` (interface) tem duas pontas em
repositórios separados, e o gate de E2E (`e2e.yml` aqui, `ci.yml` lá) roda o front real contra o
back real. Isso levanta uma pergunta legítima: **se a mudança precisa das duas metades, e uma
sobe antes da outra, o gate não reprova por um motivo falso?**

Resposta curta: durante os PRs, não — o CI resolve. **Entre os dois merges, sim** — e aí não é
falso, é real.

## Durante os PRs: resolvido automaticamente

Cada job resolve a ref do outro repositório nesta ordem:

1. Variável de repositório (`KROCKSIDE_REF` / `HIBRYGAME_REF`), se existir. Escape manual.
2. **Branch de mesmo nome no outro repo**, se existir.
3. `main`.

Então a convenção que faz tudo funcionar sem configurar nada é: **dê o mesmo nome à branch nos
dois repos.** `feat/promocao-de-peao` aqui e `feat/promocao-de-peao` lá, e cada PR passa a ser
testado contra a metade correspondente do outro lado, automaticamente.

Nomes diferentes exigem a variável — que é global ao repositório e desvia *todos* os PRs enquanto
existir. Use como exceção, e apague depois.

## Entre os dois merges: é problema real, não de CI

Mergeada a primeira metade, os dois `main` ficam genuinamente incompatíveis até a segunda subir.
Nenhuma configuração esconde isso, e não deveria: o gate está certo em apontar.

O que elimina a janela é a ordem, não o CI:

**Mude o contrato de forma retrocompatível e mergeie o back primeiro.** Campo novo é adicionado,
não renomeado; endpoint novo coexiste com o antigo; parâmetro novo é opcional. Assim o front
antigo continua funcionando contra o back novo, o gate fica verde nos dois lados durante a
janela, e o front sobe quando quiser. Só depois, num PR separado, remove-se o que ficou órfão.

Quando a quebra é inevitável (renomear um evento do hub, mudar o formato de `MapSquare`), então:

1. Mergeie back e front na mesma janela, um logo após o outro.
2. Enquanto isso, aceite `main` vermelho — é informação verdadeira, não ruído.
3. Não use `[skip ci]` para calar o gate. Se ele incomoda nessa janela, é porque a quebra é real.

A alternativa estrutural — monorepo, ou contrato versionado e verificado por schema nas duas
pontas de forma independente — resolveria a raiz. Não está feito, e é decisão maior que isto:
está registrado em [`debito-tecnico.md`](./debito-tecnico.md).

## Checklist

- [ ] Mesma branch, mesmo nome, nos dois repos
- [ ] O contrato é retrocompatível? Se sim, back primeiro e a janela some
- [ ] Se não é, os dois merges na mesma janela e `docs/FRONTEND_CHANGES.md` atualizado
- [ ] Nenhuma variável `KROCKSIDE_REF` / `HIBRYGAME_REF` sobrando depois
