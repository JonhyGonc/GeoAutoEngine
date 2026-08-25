# Protótipo visual GeoAutoEngine 3

Data: 2026-08-07

Estado: **funcionando no Civil 3D 2026**

## Entrega

- aba `GeoAutoEngine` no Ribbon;
- botão grande `Abrir GeoAutoEngine`;
- Palette `GeoAutoEngine 3`;
- ação `+ Adicionar CSV` com seleção múltipla e adições sucessivas;
- lista dos arquivos selecionados com indicador `✓`;
- botão primário destacado `PROCESSAR LEVANTAMENTOS`;
- processamento `CSV(s) -> ImportBatch -> DrawingPlan` inteiramente em memória;
- confirmação `✓ Processamento concluído` e resumo de arquivos, registros e itens preparados;
- problemas que exigem revisão resumidos sem exibir candidatos de duplicidade.

O botão da Palette não chama o aplicador CAD: não cria entidades, não executa geometria, não grava XData e não modifica o DWG.

## Correção ImportBatchSummary

As assemblies estavam usando a versão padrão `1.0.0.0`, permitindo que uma sessão CAD reutilizasse um `GeoAutoEngine.Domain` anterior ao contrato `ImportBatchSummary`. O conjunto nativo agora usa versão coerente `3.0.0.2`; a validação foi feita em processo novo do Civil 3D para evitar assemblies antigas já carregadas.

## Validação

- build incremental Debug do Plugin: aprovado, 0 erros/avisos;
- FAST Multi-CSV diretamente relacionado: 1 aprovado em 349 ms;
- smoke visual Civil 3D 2026: Ribbon, Palette, dois CSVs e Core aprovados;
- build Release: aprovado, 0 erros/avisos;
- REGRESSION C#: 64 aprovados em 909 ms;
- LEGACY: 406 aprovados, 4 skips gráficos esperados e 286 subtests;
- CAD final: sucesso, rollback e undo aprovados no AutoCAD 2026 e Civil 3D 2026.
