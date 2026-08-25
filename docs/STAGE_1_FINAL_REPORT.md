# Relatório final da Etapa 1 — fundação nativa e contratos

Data de encerramento: 2026-08-07

Estado: **concluída tecnicamente; aguardando aprovação para a etapa seguinte**

Alvo exclusivo: **AutoCAD 2026 / Civil 3D 2026**

## Resultado

A fundação do GeoAutoEngine 3.0 foi criada em .NET 8, compilada, testada e carregada nos dois hosts Autodesk oficiais. A etapa não portou regras geométricas nem gravou entidades no DWG.

A topologia final possui somente os projetos necessários agora:

| Projeto | Alvo | Responsabilidade | Dependências diretas |
|---|---|---|---|
| `GeoAutoEngine.Domain` | `net8.0` | contratos, invariantes, ImportBatch e DrawingPlan | nenhuma |
| `GeoAutoEngine.Application` | `net8.0` | orquestração, consolidação e portas | Domain |
| `GeoAutoEngine.Csv` | `net8.0` | coleção canônica de fontes CSV | Application |
| `GeoAutoEngine.Cad2026` | `net8.0-windows` | leitura isolada do contexto AutoCAD | Application + Autodesk 2026 |
| `GeoAutoEngine.Plugin` | `net8.0-windows` | ciclo de vida, composição mínima e comando de smoke | Cad2026 + Autodesk 2026 |

`Presentation` não foi criado porque não existe UI na Etapa 1. `Civil3D2026` não foi criado porque nenhuma chamada Aecc é necessária; o mesmo plugin baseado na API AutoCAD carrega nos dois produtos. Isso reduz projetos e dependências sem fechar a possibilidade de introduzi-los quando houver uso real.

## Contratos entregues

### Importação Multi-CSV

- `ImportBatch`, `SourceFile`, `ResolvedColumns`, `RecordOrigin` e identificadores fortes;
- `SurveyRecord`, `RejectedRecord`, `Diagnostic` e severidades/escopos;
- `DuplicateCandidate` com as duas origens e sem remoção automática;
- hash, encoding, delimitador, mapeamento de colunas e total por fonte;
- índice lógico, linha física e ordinal consolidado preservados separadamente;
- `SurveyRecordId` determinístico por fonte e índice;
- importação individual por `ISourceImporter` e consolidação única pelo `ImportBatchService`;
- conveniência de um arquivo implementada como coleção com uma fonte;
- coleções defensivas somente leitura e ordem determinística.

O projeto CSV desta etapa define a entrada orientada a coleção. Detecção de encoding/delimitador, parser real, aliases, fallback e interpretação permanecem para o porte funcional; não foram simulados por implementações vazias.

### DrawingPlan

O contrato tipado e independente de Autodesk representa:

- textos;
- blocos e atributos;
- círculos de alerta;
- XData associado a item ativo;
- itens suprimidos com regra, motivo e origens;
- layer, XYZ, rotação, escala e identidade;
- schema `1.0` e JSON canônico ordenado para comparação sem IDs de envelope efêmeros.

IDs ativos são únicos, XData deve apontar para item ativo e um item não pode estar simultaneamente ativo e suprimido.

## Build e empacotamento

- SDK fixado em `8.0.415` por `global.json`;
- nullable, analyzers, warnings como erros e build determinístico centralizados;
- versões NuGet centralizadas em `Directory.Packages.props`;
- referências `AcCoreMgd`, `AcDbMgd` e `AcMgd` obtidas somente de `AutoCAD 2026`;
- `Private=false` para todas as referências Autodesk;
- validação explícita de ausência dos assemblies 2026 no host de build;
- conflitos de facade `MSB3277` conhecidos dos assemblies Autodesk tratados como mensagens somente nos dois projetos CAD; build final permanece com zero avisos e o runtime é validado nos hosts reais;
- nenhuma referência `AeccDbMgd` e nenhuma referência/caminho 2025;
- bundle restrito a `R25.1`, carregado sob demanda pelo comando `GEOAUTO3PING`;
- assemblies Autodesk ausentes do bundle produzido.

O SDK local usado pelo workspace fica em `.dotnet/` e não é versionado.

## Testes seletivos

O runner `scripts/test_stage1.ps1` implementa os níveis:

- FAST: 18 testes C# de domínio, contratos e Multi-CSV;
- REGRESSION: 26 testes C# no total, incluindo 8 testes adicionais de arquitetura e integração de contratos;
- LEGACY: seleção Python/AutoLISP já classificada;
- CAD: bundle e smokes nos dois hosts 2026;
- FULL: restore + Release build + REGRESSION + Python completo + CAD.

Na medição final incremental, FAST executou os testes em 50 ms e levou 2,402 s incluindo build/test host. REGRESSION executou 26 testes em menos de 0,1 s; o tempo de parede varia com o build incremental.

## Gate FULL

- `dotnet restore`: aprovado;
- build Release: 0 erros, 0 avisos;
- testes C#: 26 aprovados;
- regressão legado: 406 aprovados, 4 skips gráficos esperados e 286 subtests aprovados;
- AutoCAD 2026: bundle reconhecido, comando executado, `ACADVER=25.1`, `DBMOD` invariável;
- Civil 3D 2026: bundle reconhecido, comando executado, `ACADVER=25.1`, `DBMOD` invariável;
- sessão Autodesk preexistente: preservada pelo harness;
- bundle temporário: removido ao final.

O desktop Autodesk regrava bytes da cópia da fixture durante o ciclo do host mesmo quando `DBMOD` permanece estável. Por isso o hash físico antes/depois é preservado como observação, não como atribuição ao plugin. O critério de não modificação do plugin é `DBMOD` idêntico entre `Initialize` e o fim do comando, corroborado pela inexistência de transaction, document lock ou API de escrita no código da Etapa 1.

## Fronteiras verificadas automaticamente

- Domain sem projeto, pacote, WPF, arquivo ou Autodesk;
- Application somente com Domain e sem UI/host;
- CSV somente com Application e sem Autodesk;
- Autodesk restrito a Cad2026 e Plugin;
- `CopyLocal=false` nas três referências Autodesk;
- nenhuma API principal orientada exclusivamente a arquivo único;
- nenhuma referência 2025 ou Aecc na fundação;
- bundle limitado à série Autodesk `R25.1`.

## Fora do escopo preservado

Continuam deliberadamente não implementados:

- parser e interpretação funcional completa dos CSVs;
- motor de rotação;
- associação geométrica;
- colisão;
- criação definitiva de textos e blocos;
- runtime AutoLISP no produto 3.0;
- aplicação completa do DrawingPlan no DWG;
- UI/palette;
- remoção de Python, AutoLISP, SCR ou testes do oráculo.

## Evidências

- `docs/evidence/stage1/FULL_GATE_REPORT.json`;
- `docs/evidence/stage1/cad/PLUGIN_SMOKE_SUMMARY.json`;
- `docs/evidence/stage1/cad/plugin_smoke_2026_acad.json`;
- `docs/evidence/stage1/cad/plugin_smoke_2026_c3d.json`;
- `docs/adr/ADR-001-PROJECT-BOUNDARIES.md`;
- `docs/adr/ADR-002-IMPORT-BATCH-AND-DRAWING-PLAN.md`;
- `docs/adr/ADR-003-AUTODESK-2026-INTEGRATION.md`.

## Gate de continuidade

A Etapa 1 encerra somente fundação e contratos. Nenhum porte funcional da etapa seguinte deve começar sem aprovação explícita deste fechamento.
