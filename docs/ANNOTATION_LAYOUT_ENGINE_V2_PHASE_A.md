# Annotation Layout Engine V2 — Fase A

## Limites da fase

A Fase A resolve somente layout padrão de textos. Ela não cria Polyline de chamada, MLeader ou
qualquer solver global. `LayoutMode.Callout` existe apenas como contrato de evolução para a Fase B.

## Fluxo

```text
DrawingPlan
  -> captura CAD síncrona (curvas elegíveis, textos, blocos e estruturas relevantes)
  -> LayoutScene imutável
  -> AnnotationLayoutEngine (Domain, sem Autodesk)
  -> LayoutDecision
  -> uma materialização final por DBText na transação existente
```

O host nunca consulta ou materializa uma entidade CAD para cada candidato. Objetos Autodesk não
saem da thread/document lock: somente pontos, segmentos, retângulos e strings chegam ao motor.

## Classificação espacial

- Curvas abertas em layers reconhecidas pelo `NetworkCadCatalog` formam `LayoutNetworkPath` e podem
  fornecer associação/tangente.
- Curvas fechadas, inclusive geometrias do tipo `REC`, nunca fornecem orientação.
- Textos existentes são obstáculos; os gerenciados por XData também fornecem posição anterior.
- Blocos relevantes (PV, poste, hidrante, registro, grelha e boca de lobo) e estruturas pluviais
  são obstáculos.
- Blocos e estruturas já planejados entram no snapshot antes do layout dos textos.
- O índice é um spatial hash de AABBs; footprints e segmentos são indexados por células e consultas
  examinam somente a vizinhança do candidato. Itens excepcionalmente extensos usam uma lista global
  limitada, evitando explosão de células.

## Métrica e footprint de texto

Textos já existentes usam `GeometricExtents` reais do CAD como obstáculo. Para textos ainda não
materializados, o Core usa `ITextMetricsProvider`; a implementação inicial
`ApproximateTextMetricsProvider` estima largura por quantidade de glifos, altura e width factor.
Essa estimativa é marcada explicitamente como aproximada e recebe `Clearance` centralizado. Não é
criado um `DBText` temporário para descobrir bounds porque isso tornaria a busca dependente do host,
do estado do estilo e de comportamento de extents antes da inserção. Uma futura validação CAD pode
fornecer métricas reais pela mesma interface sem alterar o solver.

O footprint é um OBB. Colisão OBB × OBB usa Separating Axis Theorem; cruzamento de rede transforma
o segmento para as coordenadas locais do OBB. Toque de borda, depois do clearance, conta como
conflito.

## Candidatos e score

Profundidade e diâmetro formam um único `AnnotationLayoutRequest`. Os dois preservam offsets
relativos no referencial local `Anchor + T*t + N*n`, mantendo profundidade acima e diâmetro abaixo.
O conjunto padrão contém posição preferencial, offsets tangenciais positivos/negativos, offsets
normais nos dois lados e combinações controladas (até 23 candidatos com altura usual).

O score explicável separa distância, posição preferencial, estabilidade, lado, cruzamentos de rede,
clearance, obstáculos soft e conflitos hard. Sobreposição com texto, bloco ou estrutura é hard.
Cruzamentos com a rede associada e com outras redes têm pesos distintos. Empates usam tolerância
central e `CandidateId` ordinal.

## Estabilidade e recuperação

O XData existente identifica os textos pelo stable key. Quando todos os membros de uma anotação já
existem, posição, rotação e bounds atuais entram como candidato `previous`; ele recebe forte
preferência enquanto continuar válido e dentro do deslocamento máximo. Isso preserva idempotência e
ajustes humanos seguros sem tentar inferir manual override por heurística.

Se não houver associação de rede ou nenhum candidato aceitável, a decisão fica
`LayoutStatus.RequiresReview`, gera `ANNOTATION_LAYOUT_UNRESOLVED` e incrementa `PendingItemCount`.
Uma falha estrutural inesperada ao montar/executar o snapshot aciona o algoritmo anterior como
contingência e gera `ANNOTATION_LAYOUT_LEGACY_FALLBACK`.

## Metadados

Os textos recebem somente metadados pequenos e versionados: `LAYOUT_VERSION=2A`, `LAYOUT_MODE`,
`LAYOUT_STATUS`, `LAYOUT_CANDIDATE` e `AUTO_POSITION`. Os IDs determinísticos e o XData anterior são
preservados.

## Cenários de referência

Os testes puros cobrem os cenários reproduzíveis equivalentes a:

- `LAYOUT_001_HORIZONTAL_CLEAR`
- `LAYOUT_002_VERTICAL_CLEAR`
- `LAYOUT_003_ALMOST_VERTICAL`
- `LAYOUT_004_DIAGONAL`
- `LAYOUT_005_CURVE_LOCAL_TANGENT`
- `LAYOUT_006_TEXT_COLLISION`
- `LAYOUT_007_BLOCK_COLLISION`
- `LAYOUT_008_PLANNED_TEXT_COLLISION`
- `LAYOUT_009_DENSE_OR_BLOCKED`
- `LAYOUT_010_UNRESOLVED`
- `LAYOUT_011_DETERMINISM_AND_LOCALITY`
- `LAYOUT_012_DEPTH_DIAMETER_INTEGRITY`

## Benchmark Release

Medição sintética em Windows 11 Pro 10.0.26200, Intel Core i7-13650HX (14 cores/20 threads),
31,7 GB de RAM, .NET 8.0.23. Cada anotação gerou/avaliou 23 candidatos; a cena usa uma rede longa e
anotações espaçadas, exercitando associação, footprint, score e índices sem conflitos artificiais.

| Anotações | Candidatos | Consultas espaciais | Tempo (ms) | Não resolvidas |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 2.300 | 4.700 | 6,074 | 0 |
| 500 | 11.500 | 23.500 | 32,317 | 0 |
| 1.000 | 23.000 | 47.000 | 62,121 | 0 |
| 2.000 | 46.000 | 94.000 | 141,958 | 0 |
| 5.000 | 115.000 | 235.000 | 910,556 | 0 |

As consultas crescem exatamente 47 por anotação nesse cenário; não há scan completo da cena por
candidato. Tempos são checkpoints locais, não garantia independente de hardware.

## Limitações conhecidas

- A métrica de texto planejado ainda é aproximada; estilos com fontes ou width factors incomuns
  precisam de um provider CAD específico ou validação posterior.
- Curvas CAD são convertidas em uma polyline leve com passo máximo de 0,25 m e limite de 2.048
  segmentos. Tangentes locais são estáveis, mas splines muito longas/curvadas podem perder detalhe.
- Bounds de blocos planejados que ainda não têm uma referência materializada são conservadores por
  família/escala; blocos existentes usam `GeometricExtents`.
- Não há callout, reorganização por seleção/região nem otimização global de grupos nesta fase.
- Não existe um marcador explícito de manual override; a posição anterior válida é preservada, mas
  não se tenta adivinhar intenção humana quando o XData não a registra.
