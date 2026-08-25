# GeoAutoEngine 3.0 — registro de aprovação arquitetural

Data da aprovação: 2026-08-07  
Estado: aprovado para iniciar a Etapa 0

## Plataforma Autodesk oficial

O alvo exclusivo do GeoAutoEngine 3.0 é AutoCAD 2026 e Civil 3D 2026. AutoCAD/Civil 3D 2025 está fora do escopo e não existe requisito de compatibilidade retroativa. Arquitetura, testes, referências gerenciadas Autodesk, bundle de implantação e certificação devem usar somente a linha 2026.

## Decisões aprovadas

1. C# com .NET 8 como tecnologia do GeoAutoEngine 3.0.
2. Plugin nativo executado dentro do AutoCAD/Civil 3D.
3. API gerenciada do AutoCAD como base das funcionalidades existentes.
4. Dependência da API específica do Civil 3D opcional e mínima.
5. Palette WPF com fluxo principal simples.
6. `DrawingPlan` tipado e mantido em memória como fronteira entre regras e CAD.
7. Código executável atual como baseline quando código e documentação divergirem.
8. Migração incremental, com comparação lado a lado e gates de equivalência.
9. Remoção de Python, AutoLISP e SCR somente após o corte aprovado.
10. Nenhuma inclusão de Pipe Networks, COGO Points, Surfaces, Feature Lines ou BIM durante a migração.

## Alteração funcional autorizada

A importação Multi-CSV faz parte do escopo oficial do GeoAutoEngine 3.0 desde o início do desenho. Toda entrada, inclusive um único CSV, será representada por um `ImportBatch` contendo uma coleção ordenada de fontes.

Esta é a única funcionalidade nova autorizada durante a reconstrução. As demais regras de negócio devem conservar o comportamento efetivo da versão 2.1.3 até que uma mudança seja analisada e aprovada separadamente.

## Consequência para a sequência de migração

O porte de CSV, domínio e casos de uso não poderá expor uma API principal orientada a arquivo único. Uma conveniência para um arquivo poderá existir somente se construir internamente um `ImportBatch` com uma fonte.
