# Auditoria e estratégia seletiva da suíte de testes

Data: 2026-08-07

## Resultado

A suíte foi reorganizada por risco, sem retirar nenhuma regra vigente. Cada item coletado recebe exatamente uma classificação primária (`fast`, `regression` ou `legacy`). O marker `cad` é transversal e pode coexistir com qualquer classificação primária.

O mecanismo está centralizado em `tests/conftest.py`. A coleta falha se um novo teste não for classificado, evitando que casos futuros entrem silenciosamente na suíte errada. Os markers são declarados como estritos no `pyproject.toml`.

| Categoria | Casos coletados | Finalidade |
|---|---:|---|
| FAST | 124 | Regras fundamentais durante desenvolvimento contínuo |
| REGRESSION | 24 | Integrações e compatibilidade não legadas |
| LEGACY BASELINE | 262 | Python, AutoLISP, SCR, UI e fingerprints antigos |
| Total primário | 410 | Soma sem sobreposição |
| CAD | 332 | Marker transversal de impacto CAD; sobrepõe as categorias acima |
| OBSOLETE removidos | 4 | Casos redundantes consolidados no fingerprint parametrizado |

Os 286 `subtests` do `unittest` são verificações internas desses 410 casos coletados e não são somados novamente.

## Comandos

```powershell
# Desenvolvimento contínuo
.\scripts\test_suites.ps1 fast

# Conclusão de módulo: FAST + REGRESSION
.\scripts\test_suites.ps1 regression

# Contratos específicos ainda não migrados
.\scripts\test_suites.ps1 legacy

# Contratos estáticos que podem afetar CAD/DWG
.\scripts\test_suites.ps1 cad

# Todos os testes pytest
.\scripts\test_suites.ps1 full
```

O gate CAD real não é confundido com testes estáticos. Quando uma mudança afetar integração Autodesk, geometria, associação, rotação, blocos, XData, idempotência ou resultado DWG, executar também os harnesses 2026 em um diretório novo:

```powershell
.\.venv\Scripts\python.exe .\scripts\baseline\run_cad_baseline.py --version 2026 --product ACAD --engine desktop --output-directory <diretorio-novo>
.\.venv\Scripts\python.exe .\scripts\baseline\run_cad_baseline.py --version 2026 --product C3D --engine desktop --output-directory <diretorio-novo>
```

## Gatilhos de execução

| Mudança | Suíte mínima |
|---|---|
| Regra de parsing, normalização, interpretação, coordenada ou diagnóstico | FAST |
| Conclusão de módulo C# ou alteração em integração Python não legada | REGRESSION |
| Alteração em componente Python/AutoLISP/SCR ainda usado | FAST + LEGACY relacionado |
| Alteração com impacto potencial no DWG | CAD estática + gate real no host afetado |
| Commit importante | REGRESSION + LEGACY relacionado |
| Fechamento de etapa, release ou remoção de legado | FULL pytest + gates CAD ACAD/C3D 2026 |

## Classificação FAST

| Grupo | Casos | Risco protegido |
|---|---:|---|
| Multi-CSV acceptance | 3 | Estruturas diferentes, resolvedor individual e duplicidade rastreável |
| Block mapping | 13 | Mapeamento, escala, offsets, rotação e regras não inventadas |
| Block processor — núcleo selecionado | 8 | Precisão, deduplicação, limiar, rede, layer, escala e rotação |
| Code interpreter | 12 | Redes, profundidade, diâmetro, blocos e códigos desconhecidos |
| Code normalizer | 8 | Regras de normalização topográfica |
| Coordinate column resolver | 7 | Aliases, fallback e plausibilidade de coordenadas |
| CSV reader | 3 | Encoding, delimitador e cabeçalho |
| Diagnostic engine | 8 | Pendências, coordenadas, redes, materiais e processáveis |
| Layer mapping | 16 | Catálogo de redes, aliases, XRef e ausência de sobreposição |
| Polyline rotation configuration | 13 | Raios, tolerâncias, tipos e layers de geometria |
| Text normalization | 4 | Acentos, símbolos, aliases e valores vazios |
| Architecture services | 2 | Preparação de profundidade/diâmetro e limite de deduplicação |
| Orientation math | 27 | Rotação, tangentes, vértices, prioridade e projeção XY |
| **Total** | **124** | |

## Classificação REGRESSION

| Grupo | Casos | Risco protegido |
|---|---:|---|
| Block processor — integração restante | 13 | Arquivos, fallbacks, diagnósticos e compatibilidade de DataFrame |
| CAD text configuration | 9 | Parâmetros de afastamento, colisão e XData |
| Embedded block library | 2 | Presença e uso da biblioteca DWG oficial |
| **Total** | **24** | |

A execução denominada `regression` inclui automaticamente FAST e REGRESSION: 148 casos.

## Classificação LEGACY BASELINE

| Grupo | Casos | Regra de retenção |
|---|---:|---|
| Fingerprints e harness da baseline CAD | 6 | Até equivalência e cadeia de custódia 3.0 estarem aprovadas |
| App controller/UI antiga | 13 | Até a palette WPF substituir o fluxo e seus estados |
| Geração e runtime AutoLISP | 215 | Remoção incremental somente após equivalência .NET da função correspondente |
| Contratos de orientação emitidos no LISP | 5 | Até o adaptador .NET passar matemática e baseline CAD |
| SCR | 5 | Até o plugin eliminar a execução por script |
| Paths e resolução de blocos legado | 13 | Até o bundle 2026 e a biblioteca nativa estarem certificados |
| Normalização por DataFrame/arquivo legado | 2 | Até o importador C# equivalente estar aprovado |
| Inicialização do pacote Python | 3 | Até o corte definitivo do executável Python |
| **Total** | **262** | |

Nenhum desses testes foi removido por lentidão. Eles ficam fora do ciclo FAST, mas continuam obrigatórios enquanto o componente correspondente não tiver substituto C# validado.

## Marker CAD

O marker transversal `cad` seleciona 332 contratos estáticos relacionados a bloco, layer, rotação, associação, colisão, geometria, LISP, SCR, XData, idempotência e manifesto. Ele não abre o AutoCAD. Os dois cenários reais — AutoCAD 2026 e Civil 3D 2026 — permanecem nos harnesses externos e são obrigatórios nos gates descritos acima.

## Testes removidos agora

### `tests/compatibility/test_real_generation_counts.py` — 2 casos

- Testava contagens de textos/blocos nas duas fixtures reais e presença dos comandos de autoexecução no LSP.
- As mesmas contagens já são verificadas pelos fingerprints parametrizados, junto com hashes integrais do DataFrame e do LSP.
- A presença/autoexecução dos comandos também é coberta por `test_lisp_generator.py`, `test_lisp_idempotency_xdata.py`, `test_lisp_blocks.py` e outros contratos específicos.
- Pode ser removido agora porque não possuía risco exclusivo.

### `tests/integration/test_pipeline_v2.py` — 2 casos

- Testava linhas, encoding, delimitador e registros processáveis das mesmas duas fixtures reais.
- Esses campos foram incorporados a `test_legacy_baseline_fingerprints.py`, que já executa o pipeline real e agora preserva explicitamente todos esses valores.
- Pode ser removido agora porque a cobertura foi consolidada antes da exclusão.

## OBSOLETE e candidatos futuros

Depois da consolidação, nenhum teste restante é classificado como OBSOLETE removível agora.

| Candidato futuro | O que cobre | Pode sair quando |
|---|---|---|
| Contratos AutoLISP por detalhe interno | Estrutura e comportamento do runtime emitido | A função correspondente existir em .NET, tiver teste equivalente e passar o manifesto CAD lado a lado |
| Testes do SCR | Geração e execução indireta pelo AutoCAD | O plugin nativo não depender mais de SCR e o comando .NET estiver certificado |
| UI/controller/theme antigos | Estados e fluxo da interface Python | A palette WPF tiver testes equivalentes e aceite funcional |
| Paths e inicialização Python | Layout legado, imports e biblioteca externa | Bundle 2026 e corte do executável Python aprovados |
| Fingerprints Python/LSP | Oráculo 2.1.3 completo | Corte final de todos os componentes migrados e retenção apenas como evidência imutável |

## Duplicações e parametrização

- As duplicações integrais sobre fixtures reais foram consolidadas nesta auditoria.
- Casos de códigos, aliases, paths e mapeamentos possuem formatos aptos a parametrização. A conversão reduziria a contagem visual, mas quase não reduziria o tempo e hoje diminuiria a localização semântica das falhas; foi adiada para o porte C#, onde serão tabelas de contrato compartilhadas.
- Verificações de autoexecução, balanceamento e estrutura aparecem em vários cenários LISP. Elas são sobreposição defensiva, pois cada arquivo gera um cenário diferente. Poderão usar helpers parametrizados sem apagar as asserções específicas, mas não são removíveis antes da migração do runtime.
- Testes de configuração e testes do LISP emitido parecem semelhantes, porém protegem camadas distintas: valor de domínio versus serialização para o host. Foram preservados.

## Evidência de execução

| Execução | Resultado | Tempo |
|---|---|---:|
| FAST | 124 aprovados, 286 desmarcados, 123 subtests | 0,72 s |
| REGRESSION seletiva | 148 aprovados, 262 desmarcados, 123 subtests | 0,94 s |
| Regressão completa pytest | 406 aprovados, 4 skips gráficos, 286 subtests | 13,73 s |

O gate CAD real não foi repetido nesta auditoria porque nenhum código de produção, gerador, geometria ou integração Autodesk foi alterado. Essa decisão segue a própria política seletiva criada aqui.
