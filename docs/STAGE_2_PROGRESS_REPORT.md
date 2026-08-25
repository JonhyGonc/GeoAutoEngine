# Relatório de andamento da Etapa 2 — Core de importação

Data do checkpoint: 2026-08-07

Estado: **escopo funcional do Core concluído e validado; aguardando revisão antes de qualquer Etapa 3**

## Resultado

A Etapa 2 entrega um pipeline C# funcional, independente de Autodesk, que recebe sempre uma coleção de fontes. Um único CSV segue exatamente o mesmo caminho de um lote Multi-CSV. Nenhum arquivo é concatenado ou reescrito.

```text
ImportSourceRequest[]
  -> leitura independente por fonte
  -> encoding + delimitador + parser
  -> cabeçalho + aliases + fallback validado
  -> normalização + interpretação
  -> validação + rejeições rastreáveis
  -> consolidação determinística
  -> diagnóstico conjunto + duplicidades
  -> ImportBatch tipado em memória
```

## Componentes entregues

| Componente | Projeto | Responsabilidade |
|---|---|---|
| `SurveyCodeNormalizer` | Domain | normalização Unicode, decimais, diâmetros, aliases e correções de campo |
| `SurveyCodeInterpreter` | Domain | interpretação, tipo, rede, profundidade, diâmetro, material, complemento, confiança e regra |
| `CsvParser` | Csv | UTF-8/BOM, CP1252/Latin-1, quoted fields, multiline e linha física real |
| `CsvColumnResolver` | Csv | aliases independentes por fonte e fallback posicional com validação numérica/plausibilidade |
| `CsvSourceImporter` | Csv | SHA-256, origem, raw values, validação, diagnósticos e rejeições por CSV |
| `ImportBatchService` | Application | ordenação, rebasing de origens e consolidação de arquivo único ou múltiplos |
| `SpatialDuplicateDetector` | Application | candidatos exatos/espaciais por grade, sem remover registros |
| `ImportBatchSummary` | Domain | visão consolidada por fonte, rede, tipo, material, desconhecidos, rejeições e severidade |

## Multi-CSV e falhas

- cada fonte preserva path, nome, hash, encoding, delimitador, colunas e contagem;
- cada registro preserva índice na fonte, linha física e ordinal consolidado;
- cabeçalhos heterogêneos `Norte/Leste/Código` e `Coord Y/Coord X/Descrição` coexistem no mesmo lote;
- uma fonte estruturalmente inválida permanece em `SourceFiles` com `Status=Rejected`, zero registros e diagnóstico de erro;
- outras fontes válidas continuam sendo analisadas no mesmo lote;
- registros com código vazio ou coordenada inválida viram `RejectedRecord` com raw values, origem e motivos;
- código desconhecido continua importado para revisão e gera diagnóstico com arquivo e linha;
- duplicidades preservam os dois registros e as duas origens; nenhuma remoção automática ocorre.

## Equivalência funcional

Foi capturado um oráculo semântico reproduzível do Python 2.1.3 em `../reference/regression_data/stage2_interpretation_oracle.json`:

- 358 registros das duas fixtures reais;
- 96 casos sintéticos representativos;
- 88 regras distintas;
- comparação campo a campo de código original/normalizado, natureza, tipo, rede, profundidade, diâmetro, material, complemento, reconhecimento, confiança, regra e observação;
- contagens reais idênticas ao legado: `206 / 192 / 14` e `152 / 132 / 19` para total/reconhecidos/desconhecidos;
- distribuições por tipo e rede idênticas ao baseline oficial.

O script `tools/capture-stage2-interpretation-oracle.py` torna a captura auditável. O SHA-256 do oráculo integra `../reference/SHA256SUMS.csv`.

## Testes e desempenho

Resultados finais deste checkpoint:

- FAST C#: 35 aprovados; 278 ms de execução dos testes e 4,601 s de parede com build/test host;
- REGRESSION C#: 49 aprovados; 415 ms de execução dos testes e 2,680 s de parede incremental;
- build Release da solução completa: 0 erros e 0 avisos;
- checkpoint legado FAST+REGRESSION: 148 aprovados, 262 não selecionados e 123 subtests aprovados;
- legado FULL da reorganização: 406 aprovados, 4 skips gráficos esperados e 286 subtests aprovados;
- lote sintético de 10.000 registros, incluindo interpretação, consolidação e busca de duplicidades: 610 ms no host deste checkpoint (limite automatizado conservador de 10 s).

O detector espacial usa particionamento em grade e consulta apenas células vizinhas, evitando comparação quadrática global. Regexes de negócio são compiladas e mantidas em cache; isso reduziu o cenário de 10.000 registros de aproximadamente 2,7 s para 610 ms.

## Fronteiras preservadas

- Domain e Application continuam sem Autodesk, UI ou acesso direto ao host;
- Csv continua sem Autodesk e sem pacote externo de parsing;
- Plugin, Cad2026, bundle e `DrawingPlan` não foram alterados funcionalmente;
- nenhuma escrita, transaction ou lock de DWG foi introduzido;
- rotação, associação geométrica, colisão, blocos, textos, layers e aplicação do `DrawingPlan` permanecem fora do escopo;
- nenhum smoke CAD foi executado, conforme o gatilho aprovado: não houve mudança de integração Autodesk.

## Situação para continuidade

Não há bloqueio técnico conhecido no Core de importação. O escopo solicitado para a Etapa 2 está implementado e protegido por regressão. O próximo trabalho pertence a uma etapa posterior e não foi iniciado: composição de estruturas geométricas/`DrawingPlan`, adaptadores de consulta CAD e, somente quando autorizado, aplicação no DWG.
