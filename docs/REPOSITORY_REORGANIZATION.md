# Reorganização do repositório

Data: 2026-08-07

## Decisão

O GeoAutoEngine 2.1.3 e o GeoAutoEngine 3.0 possuem raízes independentes. Os arquivos foram movidos com renames rastreáveis pelo Git; nenhum histórico foi descartado e nenhum comportamento do legado foi alterado.

| Raiz | Propriedade | Política |
|---|---|---|
| `GeoAutoEngine.Legacy/` | Python, AutoLISP, SCR, UI antiga, empacotamento e testes legados | congelada; somente correções necessárias ao uso como oráculo |
| `GeoAutoEngine3/` | solução .NET 8, plugin, contratos, código novo, testes e documentação da migração | raiz exclusiva do desenvolvimento 3.0 |
| `reference/` | cópias estáveis de CSVs, DWGs, saídas e dados de regressão | somente dados de equivalência e seus hashes |

Ambientes locais acompanham seus proprietários: `.venv` no legado e `.dotnet` na versão 3. Artefatos do bundle 3.0 ficam em `GeoAutoEngine3/artifacts`; artefatos históricos permanecem no legado.

## Regras de fronteira

1. A solução 3.0 não referencia código executável da árvore legado.
2. Testes de equivalência podem ler `../reference` ou invocar o legado explicitamente como oráculo.
3. O legado preserva suas próprias fixtures para continuar autônomo; `reference` não substitui entradas internas dele.
4. Evidências encerradas das Etapas 0 e 1 permanecem em `GeoAutoEngine3/docs/evidence`.
5. Alterações futuras devem ser executadas a partir da raiz proprietária correspondente.

## Validação da mudança

A reorganização exige FAST do legado, FAST do C#, build Release e verificação dos caminhos arquiteturais. O gate CAD não é repetido porque código do plugin e assemblies não mudaram; o manifesto apenas acompanhou sua raiz, e sua inclusão no bundle continua verificada no build.

Resultado em 2026-08-07:

- FAST C#: 18 aprovados;
- REGRESSION C#: 26 aprovados;
- build Release: 0 erros e 0 avisos, incluindo Plugin e Cad2026;
- FAST legado: 124 aprovados, 286 não selecionados e 123 subtests aprovados;
- FULL legado: 406 aprovados, 4 skips gráficos esperados e 286 subtests aprovados;
- acervo compartilhado: 31 arquivos de dados com SHA-256 registrado em `../reference/SHA256SUMS.csv`.
