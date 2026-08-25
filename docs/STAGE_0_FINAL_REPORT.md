# Relatório final da Etapa 0

Data de encerramento: 2026-08-07

Estado: **concluída e aprovada**

## Escopo congelado

A Etapa 0 congelou o comportamento observável do GeoAutoEngine 2.1.3, incluindo fonte Python, configurações, regras, geradores AutoLISP/SCR, blocos DWG, fixtures reais, testes, ambiente, contrato Multi-CSV e execução no CAD.

O alvo Autodesk oficial do GeoAutoEngine 3.0 é exclusivamente AutoCAD 2026 e Civil 3D 2026. A linha 2025 está fora do escopo e não impõe requisito de código, teste, empacotamento ou certificação.

## Baseline Python

- 410 testes aprovados.
- 4 testes gráficos ignorados de forma prevista.
- 286 subtestes aprovados.
- Fixtures reais congeladas em 206/93/4 e 152/72/6 registros/textos/blocos.
- Fingerprints semânticos de importação, diagnóstico e LSP registrados.
- Ambiente e dependências capturados em JSON.
- Manifesto SHA-256 da árvore versionável registrado.

## Contrato Multi-CSV

A única ampliação funcional autorizada foi incorporada ao contrato arquitetural antes do porte:

- toda importação é um `ImportBatch`, inclusive com uma fonte;
- cada CSV resolve encoding, delimitador e colunas individualmente;
- registros preservam arquivo, índice, linha física e ordem no lote;
- registros aceitos, rejeitados, diagnósticos e candidatos a duplicidade permanecem rastreáveis;
- duplicidades são apenas diagnosticadas, sem remoção automática;
- o restante da aplicação recebe registros consolidados e gera um único `DrawingPlan`.

## Baseline CAD 2026

O bloqueio de startup do TopoCAD foi eliminado. Os smokes do Core Console passaram em `2026/ACAD` e `2026/C3D`, sem referência ao TopoCAD.

Como o oráculo legado depende de ActiveX, a captura funcional foi executada no host desktop 2026. Em ambos os produtos:

- 9 entidades controladas de entrada foram preservadas;
- 14 textos e 2 blocos foram preparados a partir de 10 registros;
- 17 entidades geradas receberam XData;
- a saída contém 26 entidades;
- a segunda execução produziu manifesto semanticamente idêntico à primeira;
- AutoCAD e Civil 3D produziram o mesmo hash semântico: `c82c2a0dfa614d42db3349b2cf64dc06c27f3488927ddd27e1ac5691d171ffa1`.

## Decisões de fechamento

1. O código executável 2.1.3 continua sendo o oráculo de equivalência até o corte aprovado.
2. A baseline oficial Autodesk é somente 2026.
3. Handles e bytes de DWG podem variar; equivalência é medida por manifesto semântico normalizado.
4. Entidades do usuário devem permanecer como subconjunto integral da saída.
5. Idempotência exige igualdade semântica entre duas execuções consecutivas do mesmo runtime.
6. Python, AutoLISP e SCR só poderão ser removidos após o plugin .NET alcançar os gates equivalentes.
7. A implementação da Etapa 1 não começou neste fechamento.

## Evidências principais

- `docs/evidence/legacy_baseline.json`.
- `docs/evidence/environment.json`.
- `docs/evidence/source_manifest_sha256.csv`.
- `docs/evidence/pytest_baseline.txt`.
- `docs/evidence/cad/CAD_BASELINE_SUMMARY.json`.
- `docs/evidence/cad/CAD_EVIDENCE_SHA256.csv`.
- `docs/evidence/cad/cad_baseline_2026_acad.json`.
- `docs/evidence/cad/cad_baseline_2026_c3d.json`.

## Gate para a Etapa 1

A Etapa 0 está encerrada. A Etapa 1 só começa após aprovação explícita do plano detalhado e deve manter todos os critérios de equivalência registrados neste relatório.
