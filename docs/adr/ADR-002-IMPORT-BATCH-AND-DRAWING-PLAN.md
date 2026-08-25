# ADR 002 — ImportBatch e DrawingPlan como contratos canônicos

Estado: aceito em 2026-08-07

## Decisão

Toda importação recebe uma coleção ordenada de fontes. A conveniência de arquivo único devolve a mesma coleção com um elemento; não existe pipeline paralelo para CSV único.

Cada fonte conserva hash, encoding, delimitador, colunas resolvidas e contagem. Cada registro importado ou rejeitado conserva `SourceFileId`, arquivo, índice lógico, linha física e ordinal consolidado. `SurveyRecordId` é derivado deterministicamente de `SourceFileId` e `SourceRecordIndex`.

O `ImportBatchService` chama o importador uma vez por fonte, consolida os registros em memória e só depois executa a política de candidatos a duplicidade. Candidatos nunca removem registros.

`DrawingPlan` é uma estrutura tipada, sem Autodesk, capaz de representar layers, estilos, textos, blocos, estruturas pluviais, alertas circulares, intenção de XData, diagnósticos, rejeições e itens suprimidos. Sua serialização canônica ordena itens e dicionários para permitir comparação com a baseline 2.1.3.

Cada item possui `PlanItemId` e `StableKey` derivados de origem, tipo e regra. A identidade não depende da execução. O `DrawingPlanId` é derivado das fontes ordenadas e das opções de preparação. Uma nova importação dos mesmos conteúdos produz o mesmo plano canônico mesmo que o `BatchId` transitório seja diferente.

Posicionamento que exige uma curva no desenho é expresso como intenção. Textos de rede e o bloco de registro de água usam `FollowNetwork`, conservam a âncora original e os layers admissíveis; a tangente e o ângulo final pertencem ao adaptador CAD. Nenhum valor geométrico é inventado no Core.

## Consequências

O contrato pode ser construído e comparado sem AutoCAD. Consultas ao DWG, rotação por tangente, colisão, idempotência efetiva e criação de entidades continuam fora do Domain/Application e serão realizadas somente pelos adaptadores CAD.
