## [Título]

### Resumo

[O que muda e por quê, em uma ou duas frases.]

### Contexto

[Onde a mudança afeta: engine, hub, casos de uso, persistência, autenticação.]

### Mudanças

1. [Mudança 1]
2. [Mudança 2]

### Evidências

[Saída de `dotnet test`, print do Swagger, log do hub — o que comprovar que funciona.]

### Checklist

1. [ ] `dotnet build` sem erro
2. [ ] `dotnet test` verde (baseline: 453 aprovados, 1 ignorado)
3. [ ] Constituição respeitada — Princípios I (camadas) e II (autoridade do servidor)
4. [ ] Se mudou contrato de FE (hub, DTO, rota): `docs/FRONTEND_CHANGES.md` atualizado
5. [ ] Se mudou estrutura ou convenção: `AGENTS.md` / `README.md` atualizados
6. [ ] Débito novo ou resolvido: `docs/debito-tecnico.md` atualizado
7. [ ] Nenhum `bin/`, `obj/`, `.dll` ou segredo no diff

### Notas

[Pontos de atenção para quem revisa, se houver.]
