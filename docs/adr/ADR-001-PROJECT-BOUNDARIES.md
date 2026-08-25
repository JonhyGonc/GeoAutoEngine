# ADR 001 — fronteiras da solução nativa

Estado: aceito em 2026-08-07

## Decisão

A Etapa 1 contém somente cinco projetos de produção:

- `GeoAutoEngine.Domain`: modelos, invariantes, `ImportBatch` e `DrawingPlan` puros;
- `GeoAutoEngine.Application`: orquestração e portas de entrada/saída;
- `GeoAutoEngine.Csv`: contrato de seleção de uma coleção de fontes CSV;
- `GeoAutoEngine.Cad2026`: acesso isolado à API gerenciada do AutoCAD 2026;
- `GeoAutoEngine.Plugin`: composição e ciclo de vida dentro do host Autodesk.

A direção permitida é `Csv -> Application -> Domain` e `Plugin -> Cad2026 -> Application -> Domain`. `Domain` não possui dependências de projeto ou pacote. `Application` não referencia UI nem Autodesk.

## Simplificação deliberada

Não foram criados projetos `Presentation` e `Civil3D2026` vazios. A Etapa 1 não entrega UI e não usa nenhuma API Aecc. O mesmo plugin baseado somente na API AutoCAD é carregado por AutoCAD 2026 e Civil 3D 2026. Um projeto Civil específico só será criado quando uma necessidade funcional real exigir `AeccDbMgd`.

## Consequências

Testes arquiteturais verificam a direção das referências. O isolamento permite executar regras e contratos sem instalar ou iniciar o AutoCAD. O número de assemblies permanece pequeno.
