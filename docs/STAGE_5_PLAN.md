# Plano detalhado da Etapa 5 — motor geométrico CAD 2026

## Objetivo

Resolver, contra a geometria real do DWG, as intenções `FollowNetwork` que a Etapa 4 preservou corretamente. O resultado será uma projeção CAD interna e resolvida, aplicada pelo escritor transacional existente sem introduzir Autodesk no Domain/Application.

```text
DrawingPlan
  -> preflight somente leitura
  -> índice espacial de redes/obstáculos
  -> associação e tangente
  -> rotação e posição finais
  -> projeção CAD resolvida
  -> aplicador transacional da Etapa 4
```

## 5.1 — Contrato interno de resolução

- criar tipos simples e imutáveis em `GeoAutoEngine.Cad2026` para candidatos, associações e itens resolvidos;
- conservar `StableKey`, origem, âncora e regra do `DrawingPlan`;
- registrar entidade associada, distância, layer, parâmetro, tangente e motivo da decisão;
- representar explicitamente `Resolved`, `Unresolved` e `Ambiguous`;
- manter o escritor desacoplado da busca geométrica.

## 5.2 — Leitura e índice espacial

- ler somente os tipos e layers relevantes em transação read-only;
- suportar `Line`, `Polyline`, `Polyline2d`, `Polyline3d`, `Arc` e `Spline` conforme o legado;
- distinguir desenho principal e XREF de forma explícita;
- normalizar aliases de layer sem correspondência parcial acidental;
- construir o índice uma vez por execução para evitar varredura DWG por item;
- estabelecer benchmark com desenhos reais e cenário sintético denso.

## 5.3 — Associação de rede

- portar a busca progressiva em `0,10`, `0,50` e `0,70 m`;
- permitir recuperação de até `5,00 m` somente para a mesma rede;
- manter a regra estrita de `0,10 m` para rede não identificada;
- diagnosticar empates e ambiguidade em vez de escolher silenciosamente;
- comparar entidade/layer/distância escolhidos com o oráculo legado.

## 5.4 — Tangente e rotação

- calcular ponto mais próximo e tangente válida por tipo de curva;
- tratar vértices, curvas degeneradas e derivada nula;
- normalizar a orientação para leitura de textos;
- resolver `FollowNetwork` para profundidade, diâmetro e registro de água;
- nunca substituir uma falha geométrica por ângulo inventado.

## 5.5 — Offset e colisão

- portar offsets perpendicular e longitudinal da baseline;
- proteger textos próximos de PV;
- considerar somente entidades configuradas e itens da aplicação corrente;
- evitar interferência com textos, blocos e polylines relevantes;
- limitar tentativas e emitir diagnóstico quando não houver posição aceitável;
- usar estruturas espaciais para impedir comportamento quadrático.

## 5.6 — Integração e idempotência

- resolver a geometria antes de iniciar a transação de escrita;
- reaproveitar layers, estilos, blocos, XData, rollback, undo e regeneração da Etapa 4;
- estender a segunda execução aos itens anteriormente pendentes;
- definir a política segura de atualizar/substituir quando geometria ou conteúdo mudou;
- manter todo acesso Autodesk dentro de `Cad2026`/`Plugin`.

## Estratégia de testes

- **FAST:** matemática pura de projeção, tangente, normalização angular, ranking, offsets e colisão;
- **REGRESSION:** fixtures geométricas por tipo de curva, rede, raio, empate, degenerescência e ausência de candidato;
- **LEGACY:** comparação de associação, posição e rotação com a baseline 2.1.3;
- **CAD:** AutoCAD 2026 e Civil 3D 2026, primeira/segunda aplicação, undo e rollback;
- **PERFORMANCE:** tempo de leitura/índice/resolução separado do tempo de escrita;
- **FULL:** Release, todas as regressões, fixtures reais e os dois hosts no fechamento.

## Gates

1. nenhuma regressão dos itens fixos da Etapa 4;
2. associação equivalente por rede e tipo de curva;
3. posição e rotação dentro das tolerâncias aprovadas;
4. diagnósticos rastreáveis para ausência/ambiguidade;
5. colisão e offsets equivalentes nas fixtures selecionadas;
6. zero duplicatas na segunda execução;
7. rollback e undo integrais;
8. desempenho sem varredura global por item;
9. AutoCAD 2026 e Civil 3D 2026 aprovados;
10. Git limpo e etapa versionada.

## Fora do escopo

- AutoCAD/Civil 3D 2025;
- alteração das regras de importação/interpretação;
- novas famílias funcionais não existentes no legado;
- dependência Aecc sem necessidade comprovada;
- retorno a AutoLISP, SCR ou arquivos intermediários.
