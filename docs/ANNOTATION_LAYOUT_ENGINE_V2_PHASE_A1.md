# Annotation Layout Engine V2 — Fase A.1

## Diagnóstico da Fase A

A Fase A não recebia a posição produzida pelo algoritmo legado. O candidato chamado de
`preferred` era reconstruído a partir da tangente local e competia por score com até 22 outras
posições. Portanto, uma posição V1 aceitável podia nem existir no conjunto avaliado e podia ser
substituída apenas porque outro candidato acumulava mais pontos. Além disso, cruzamentos com a
própria rede participavam da escolha como penalidade, embora essa proximidade seja natural.

A infraestrutura geométrica da Fase A foi preservada. A correção é de política: o motor padrão
agora é um validador e corretor conservador, não um concurso global de score.

## Fluxo híbrido

1. O host executa uma única vez o `ResolveTextsLegacy` real e entrega suas posições, rotações e
   footprints ao Core como `LegacyAnnotationLayout`/`CandidateKind.Legacy`.
2. O Core associa a rede, calcula um `MovementBudget` dinâmico e valida separadamente a posição
   existente gerenciada e a posição Legacy.
3. No primeiro passe, posições existentes válidas têm prioridade absoluta; na ausência delas,
   Legacy válido recebe `KeepLegacy`. Nenhuma alternativa é gerada nesses casos.
4. Somente baselines inválidos seguem para o segundo passe. Os candidatos são translações do grupo
   Legacy completo (profundidade + diâmetro), organizadas em tiers locais.
5. O primeiro tier com solução válida encerra a busca. Dentro dele, a ordenação é: menor movimento,
   mesmo lado, menor componente tangencial, menor componente normal, clearance, score secundário e
   ID ordinal.
6. Se nenhuma solução existir dentro do limite, o resultado é `RequiresReview`; não há callout ou
   deslocamento ilimitado nesta fase.

Os estados explícitos são `KeepExisting`, `KeepLegacy`, `Corrected` e `RequiresReview`. Score não
consegue compensar uma restrição hard nem substituir uma posição Legacy válida.

## Validação e diagnóstico

`LegacyValidationResult` separa validade, clearance mínimo e motivos objetivos. Os motivos hard
incluem colisão com texto, bloco ou estrutura, colisão profundidade/diâmetro, rotação ilegível,
distância excessiva e geometria inválida. Clearance insuficiente passou a ser observação soft na
recuperação posterior. Cada `LayoutDecision` contém:

- posição Legacy e posição final;
- candidato escolhido, movimento e mudança de lado;
- validade e problemas Legacy;
- motivo da correção e `CorrectionQuality`;
- quantidade de candidatos e diagnósticos de desenvolvimento.

Colisões originadas de textos existentes identificam
`ExistingTextGeometricExtents`. Bounds CAD, métricas planejadas aproximadas e bounds planejados
conservadores também têm fonte própria. Footprints aproximados recebem uma tolerância de contato
centralizada, evitando correção por uma interseção marginal da estimativa.

`ANNOTATION_LAYOUT_UNNECESSARY_MOVE` é produzido no modo de comparação quando a estratégia antiga
escolheria outra posição apesar de Legacy válido. No modo híbrido, esse caso termina antes em
`KeepLegacy`, portanto a métrica oficial deve permanecer zero.

## MovementBudget e tiers

Os valores ficam em `AnnotationLayoutOptions`. A recuperação posterior tornou a busca ainda mais
conservadora:

- `PreferredAdjustment = max(TextHeight * 0,50, Clearance)`;
- `MaximumStandardAdjustment` usa altura, clearance e dimensão do footprint; a contribuição do
  footprint é limitada pelo fator padrão de `1,5 * TextHeight`;
- o Legacy real mantém o limite absoluto preexistente de `MaximumDisplacement=2,5 m`, que cobre a
  faixa máxima do posicionador V1; o orçamento padrão abaixo é sempre relativo ao Legacy;
- a posição existente possui orçamento separado, inicialmente duas vezes o orçamento padrão;
- offsets locais cardinais usam 1x, 2x e 3x o ajuste preferido, até o máximo de 0,75 m com a altura
  padrão de 0,50 m;
- mudança para o lado oposto é promovida ao último tier.

Esses são valores iniciais calibráveis, centralizados e testados; não são pesos soltos de score.

## Modos internos

`AnnotationLayoutStrategy` permite executar `HybridSimple`, `PhaseAFreeScore` ou `LegacyOnly` sobre a
mesma entrada. É um contrato interno para diagnóstico/teste e não foi adicionado à Palette. O host
de produção usa `HybridSimple`; `HybridV2` permanece somente como alias de compatibilidade. Uma
falha estrutural continua acionando o fallback Legacy existente.

Os metadados de texto agora usam `LAYOUT_VERSION=2A1` e incluem `LAYOUT_DECISION`. `AUTO_POSITION`
continua sendo persistido, mas não é um marcador confiável de intenção humana; não foi adicionada
heurística de manual override. Uma posição gerenciada existente válida é preservada diretamente.

## Comparação sintética Release

Cena densa determinística com 1.000 anotações, redes paralelas/cruzadas, 250 blocos e 100 textos
existentes. Ela contém 35% de Legacy inválidos.

| Métrica | Antes A.1 (`PhaseAFreeScore`) | Depois A.1 (`HybridV2`) |
| --- | ---: | ---: |
| Anotações | 1.000 | 1.000 |
| Legacy aceito / rejeitado | 650 / 350 | 650 / 350 |
| Mantido Legacy | 600 | 650 |
| Corrigido | 400 | 350 |
| RequiresReview | 0 | 0 |
| Movimento desnecessário | 50 | 0 |
| Movimento médio | 0,8750 m | 1,0000 m |
| Movimento máximo | 1,2500 m | 1,0000 m |
| Mudanças de lado | 0 | 0 |
| Candidatos | 23.000 | 10.800 |
| Consultas espaciais | 49.000 | 22.600 |
| Tempo local | 465,088 ms | 302,107 ms |

O movimento médio deve ser interpretado apenas sobre as correções: antes havia 50 movimentos
extras pequenos, que reduziam artificialmente essa média. Depois, somente os 350 conflitos reais
foram corrigidos e o máximo caiu.

## Benchmark limpo Release

| Anotações | Legacy aceito | Mantido Legacy | Corrigido | RequiresReview | Desnecessário | Movimento médio/máximo | Candidatos | Consultas | Tempo local |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 5.000 | 5.000 | 5.000 | 0 | 0 | 0 | 0 / 0 m | 5.000 | 15.000 | 123,806 ms |

O cenário limpo avalia exatamente um candidato por anotação, contra 23 candidatos por anotação na
Fase A. Os tempos são checkpoints locais e não constituem garantia independente de hardware.

## Cobertura A.1

As novas cenas cobrem Legacy limpo, orientações horizontal/vertical/quase vertical/diagonal/curva,
colisão com texto e bloco, correção mínima, MovementBudget, preservação de existente, lado oposto
como último recurso, localidade, determinismo, idempotência, prevenção de dominó, comparação da
política antiga e `RequiresReview`. Os IDs `LAYOUT_A1_001` a `LAYOUT_A1_010` estão registrados como
traits nos testes oficiais.

## Limitações honestas

- O DWG de produção que revelou o problema não foi fornecido ao workspace. Assim, as métricas acima
  são de cenas reproduzíveis; o julgamento visual e o relatório quantitativo desse DWG ainda
  dependem de executá-lo com a versão A.1.
- Métricas de textos planejados continuam aproximadas. A tolerância reduz falsos positivos mínimos,
  mas não substitui extents CAD reais de cada fonte/estilo.
- `GeometricExtents` de textos existentes pode ser conservador em alguns casos rotacionados; a fonte
  agora é explícita para permitir calibração baseada em evidência.
- O clearance permanece em 0,15 m e agora é instrumentado por decisão. Ele não foi reduzido sem uma
  amostra real que justificasse a alteração.
- Não há marcador confiável de manual override, callout, Polyline/MLeader ou solver global. Casos
  congestionados permanecem visíveis como `RequiresReview` para a futura Fase B.
