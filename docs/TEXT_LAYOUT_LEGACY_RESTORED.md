# TEXT LAYOUT — LEGACY RESTORED

## Política restaurada

O `HybridSimple` usa a geometria produzida pelo Legacy real como quadro local oficial da anotação.
Profundidade e diâmetro formam um único grupo: os membros preservam os mesmos identificadores,
papéis, rotação e vetor relativo. Uma correção automática pode somente transladar rigidamente esse
grupo em pequenos passos tangenciais ou normais.

A ordem de produção é:

1. manter um posicionamento gerenciado existente quando ele ainda respeita o grupo e não colide;
2. manter exatamente o Legacy quando ele é válido;
3. após colisão real, testar translações rígidas de 1×, 2× e 3× o passo preferencial;
4. aceitar o primeiro nível válido, sem trocar lado, ordem ou rotação;
5. retornar `RequiresReview` se nenhum deslocamento pequeno preservar as invariantes.

Foram desativados na política de produção o fallback de lado oposto e qualquer possibilidade de o
score escolher uma orientação alternativa. O score permanece somente como desempate entre
translações do mesmo nível que já passaram por todas as restrições rígidas.

## Invariantes explícitas

`AnnotationGroupInvariantValidator` compara toda decisão com o grupo Legacy imutável e rejeita:

- profundidade que não esteja acima do diâmetro no referencial local Legacy;
- rotação diferente da rotação Legacy em qualquer membro;
- mudança ou desaparecimento de membros e papéis;
- deslocamentos diferentes aplicados aos membros do mesmo grupo;
- cruzamento ou chegada de um membro ao lado oposto do anchor Legacy.

As métricas publicadas são `OrderInvariantViolations`, `RotationInvariantViolations`,
`SideInvariantViolations`, `GroupIntegrityViolations` e `UnnecessaryMoves`.

Ao reabrir um DWG, o footprint do texto gerenciado é reconstruído no mesmo referencial orientado
usado no primeiro processamento. O `GeometricExtents` real continua sendo usado como obstáculo para
outras anotações; ele apenas deixou de transformar o próprio grupo salvo em uma caixa alinhada aos
eixos e artificialmente diferente no segundo processamento.

## DWGs reais

Os testes foram executados no Civil 3D 2026, sempre em cópias dos desenhos e após remover somente as
entidades GeoAutoEngine da cópia. O SHA-256 dos arquivos-fonte permaneceu inalterado.

| DWG / entradas reais | Passe | KeepExisting | KeepLegacy | Corrected | RequiresReview | Ordem | Rotação | Lado | Integridade | Desnecessários |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Rua São Francisco + dois CSVs reais | 1 | 0 | 55 | 50 | 34 | 0 | 0 | 0 | 0 | 0 |
| Rua São Francisco + dois CSVs reais | 2 | 105 | 0 | 0 | 34 | 0 | 0 | 0 | 0 | 0 |
| Rua Renato/Cravíneas + CSV real | 1 | 0 | 22 | 12 | 14 | 0 | 0 | 0 | 0 | 0 |
| Rua Renato/Cravíneas + CSV real | 2 | 34 | 0 | 0 | 14 | 0 | 0 | 0 | 0 | 0 |

Nos dois desenhos, a geometria gerada no segundo processamento foi byte a byte igual no snapshot
geométrico de posição, rotação, escala, raio e vértices. Nenhuma anotação sofreu deslocamento
progressivo.

A inspeção visual dos recortes exportados pelo Civil 3D confirmou pares `P.2.00` sobre `Ø150` tanto
em `KeepLegacy` quanto em correção rígida de 0,50 m. Nas regiões congestionadas, os pares mantiveram
ordem e rotação e ficaram em `RequiresReview`, sem inversão ou troca automática de lado.

Evidências locais reproduzíveis:

- São Francisco: `artifacts/real-dwg-layout/20260814-152320/summary.json` e
  `visual-pairs.png`;
- Renato/Cravíneas: `artifacts/real-dwg-layout/20260814-152606/summary.json` e
  `visual-diameter-review.png`.

## Testes e build

- testes C# Release: 157 aprovados;
- suíte Legacy congelada: 406 aprovados, 4 gráficos ignorados por ausência de `DISPLAY`,
  286 subtestes aprovados;
- builds Release AutoCAD 2024, 2025 e 2026: zero erros e zero avisos;
- bundle e instalador gerados em `artifacts/release`.

Benchmarks sintéticos não foram usados como prova de correção visual.
