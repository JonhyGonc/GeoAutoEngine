# Relatório final da Etapa 3 — DrawingPlan independente do CAD

Data de fechamento: 2026-08-07

Estado: **concluída**

## Resultado

O GeoAutoEngine 3.0 executa integralmente, sem AutoCAD aberto:

```text
CSV(s)
  -> importação individual
  -> ImportBatch consolidado
  -> interpretação
  -> DrawingPlan tipado e determinístico
```

Nenhum código da Etapa 3 abre DWG, consulta entidades Autodesk, cria arquivos intermediários ou depende de AutoLISP/SCR.

## Estrutura final do DrawingPlan

O contrato de schema `3.0` contém:

- `Layers` e `TextStyles`: recursos declarativos necessários à aplicação;
- `Texts`: tipo, conteúdo, layer, âncora, altura, estilo, cor, offset e intenção de rotação;
- `Blocks`: família, chave, nome, ativo DWG, layer, posição, escala XYZ, alinhamento e rotação;
- `RainStructures`: grelha/boca de lobo, quatro vértices, centro e código;
- `CircleAlerts`: código desconhecido ou revisão pluvial, centro, raio, cor e quantidade relacionada;
- `XData`: intenção persistente com tipo, chave estável, código, ponto e arquivo/linha de origem;
- `SuppressedItems`: tipo, regra, motivo, origens e item mantido quando aplicável;
- `Diagnostics`: diagnósticos do lote e da preparação, incluindo fonte/origens;
- `RejectedInputs`: valores brutos, origem e motivos de rejeição do `ImportBatch`;
- `Summary`: contagens prontas para diagnóstico/UI.

Cada item ativo ou suprimido tem `StableKey` e `PlanItemId` derivados de `SourceFileId`, índice da fonte, tipo, regra e discriminador. `DrawingPlanId` deriva das fontes ordenadas e opções de preparação. Reimportar as mesmas fontes produz os mesmos IDs e o mesmo JSON canônico, ainda que o `BatchId` transitório seja novo.

## Tipos e regras portadas

### Textos

- profundidade `P.0.00`, com duas casas;
- diâmetro `∅n`, com formatação compacta;
- anotação complementar de divisa no layer `-Texto`, cor 7 e estilo `GEO_ARIAL_BOLD`;
- estilo de rede `GEO_ARIAL`, altura padrão `0,5`;
- offsets legados `+0,10` e `-(altura + 0,10)`;
- layer e layers de curvas resolvidos pelo catálogo das 11 redes;
- `FollowNetwork` para textos de rede e `Fixed` para anotações.

O legado não cria um terceiro texto para complementos comuns de rede; esses complementos continuam presentes na interpretação, mas somente as anotações de divisa comprovadamente emitidas viram item gráfico nesta etapa.

### Blocos

Foram portadas as sete associações confirmadas:

| Família/chave | Regras | Ativo | Rotação |
|---|---|---|---|
| `PV_ESGOTO_SANITARIO` | `PV_ES`, `PVE`, `PV_ENCOBERTO_PROF` | `pv_esgoto_sanitario.dwg` | Fixed |
| `PV_PLUVIAL` | `PV_AP` | `pv_pluvial.dwg` | Fixed |
| `PV_TELEFONE` | `PV_TEL_LEGADO` | `pv_telefone.dwg` | Fixed |
| `PV_GENERICO` | `PV_GENERICO`, `PV_ENCOBERTO`, `POSSIVEL_PV` | `pv.dwg` | Fixed |
| `REGISTRO_AGUA_POTAVEL` | `REGISTRO_AF` | `rg_agua_potavel.dwg` | FollowNetwork |
| `HIDRANTE` | `HIDRANTE` | `hidrante.dwg` | Fixed |
| `POSTE` | `PT`, `PTIL` | `poste.dwg` | Fixed |

Regras sem associação segura são suprimidas com diagnóstico, sem inventar bloco. Escalas XYZ, posição original, alinhamento e layers de referência ficam explícitos.

### Estruturas e alertas

- agrupamento consecutivo por regra/código para `GRELHA` e `BOCA_LOBO`;
- retângulo derivado dos primeiros três pontos;
- ortogonalização somente da boca de lobo dentro da tolerância legada;
- alerta no primeiro ponto excedente quando há mais de três;
- alerta em cada ponto quando o grupo não forma retângulo válido;
- círculo vermelho de raio `0,75` para códigos desconhecidos;
- layers `GF-Grelha`, `GF-Boca de Lobo` e `GF-Diagnostico`.

### Deduplicação

- textos: mesmo código normalizado e distância estritamente menor que o limite;
- textos próximos de PV: mantém primeiro o candidato mais distante do PV, como no legado;
- blocos: chave exata por regra/definição/XYZ;
- blocos próximos: família e grade espacial, preservando o primeiro mantido;
- Multi-CSV: a deduplicação ocorre depois da consolidação e atravessa limites de arquivo, mantendo as duas origens;
- itens nunca desaparecem silenciosamente: cada descarte gera `SuppressedPlanItem`.

## Equivalência com GeoAutoEngine 2.1.3

O oráculo congelado `../reference/regression_data/stage3_drawing_oracle.json` foi capturado diretamente dos preparadores Python, sem executar lógica C# e sem resultados dependentes do DWG.

| Fixture real | Linhas | Textos | Blocos | Estruturas | Alertas | Blocos sem mapeamento |
|---|---:|---:|---:|---:|---:|---:|
| RUA BOEHMERWALD | 152 | 72 | 6 | 3 | 19 | 0 |
| Rua São Francisco | 206 | 93 | 4 | 3 | 14 | 21 |

A regressão compara todos os 358 registros e cada item preparado por conteúdo, layer, âncora/posição, altura, estilo, offset, layers de curva, regra, código, rede, ativo, escala, alinhamento, modo de rotação, vértices, centro, raio e cor. Coordenadas calculadas são comparadas a `1e-8`. As contagens e os campos independentes do CAD são equivalentes.

## Desempenho

- cada registro participa de uma passagem principal;
- a seleção de textos usa agrupamento por código e grade espacial;
- a distância ao PV mais próximo usa árvore k-d;
- blocos usam `HashSet`/dicionário para duplicidade exata e grade por família;
- estruturas pluviais são finalizadas durante a passagem ordenada.

Benchmark Release no host do fechamento: **aproximadamente 392 ms** para construir um plano de 10.000 registros contendo 20.000 textos. O limite automatizado conservador é 5 s.

## Testes e gates

- build/test Release: 0 erros e 0 avisos;
- FAST C#: 44 aprovados em 189 ms de execução dos testes;
- REGRESSION C#: 61 aprovados no total em 699 ms de execução dos testes;
- equivalência semântica: duas fixtures reais e oráculo completo aprovados;
- Multi-CSV: fixtures heterogêneas e deduplicação cruzando fontes aprovadas;
- determinismo: reimportações independentes geram IDs e JSON canônico idênticos;
- LEGACY congelado: 258 aprovados, 4 skips gráficos esperados, 148 não selecionados e 163 subtests aprovados;
- CAD: não executado, conforme gatilho aprovado, pois Plugin, Cad2026, bundle e referências Autodesk não foram alterados.

## Limitações deliberadas que dependem do CAD

Permanecem como intenção no plano:

- busca e associação de curvas;
- tangente e ângulo final;
- inferência geométrica de rede não identificada;
- colisão com textos, blocos, polylines e entidades existentes;
- deslocamento final anticolisão;
- existência/carregamento físico de definição de bloco;
- criação de `DBText`, `BlockReference`, polyline, círculo, layer e estilo;
- XData real, consulta idempotente, `DocumentLock` e `Transaction`;
- comparação de entidades/resultados DWG.

Nenhum desses resultados foi simulado no Core. O plano conserva âncora, layers admissíveis, modo de rotação e metadados suficientes para resolvê-los na Etapa 4.

## Continuidade

O plano aprovado para a próxima etapa está em `STAGE_4_PLAN.md`. A Etapa 4 deverá apenas resolver intenções contra o desenho e aplicar os itens por uma fronteira Autodesk isolada, sem reprocessar CSV ou reinterpretar códigos.
