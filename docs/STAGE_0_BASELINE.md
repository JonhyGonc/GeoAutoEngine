# Etapa 0 — baseline da reconstrução nativa

## Objetivo

Congelar o comportamento observável do GeoAutoEngine 2.1.3 antes do porte para C#, registrar evidências reproduzíveis e estabelecer o gate de execução no AutoCAD/Civil 3D.

Esta baseline é diferente da baseline histórica descrita em `docs/BASELINE_MANIFEST.md`. A documentação antiga permanece como cadeia de custódia da V2; este documento representa a fonte atual recebida para a reconstrução 3.0.

## Escopo do congelamento

- código Python executável;
- regras e configurações;
- geradores AutoLISP/SCR;
- runtime AutoLISP emitido;
- 12 blocos DWG;
- duas fixtures CSV reais;
- fixtures de aceitação Multi-CSV;
- testes automatizados;
- ambiente Python;
- executáveis AutoCAD 2026/Civil 3D 2026;
- artefatos de build existentes, sem reconstruí-los.

## Evidências geradas

O comando abaixo, executado dentro de `GeoAutoEngine.Legacy`, atualizava a evidência da baseline. A captura está congelada em `../GeoAutoEngine3/docs/evidence`:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
.\.venv\Scripts\python.exe .\scripts\baseline\capture_legacy_baseline.py
```

Saídas:

- `source_manifest_sha256.csv`: tamanho e SHA-256 da árvore versionável, excluindo evidências autorreferentes;
- `environment.json`: sistema, Python, dependências e hosts Autodesk detectados;
- `legacy_baseline.json`: resultados, diagnósticos, fingerprints e métricas das fixtures reais.

## Gates

### Gate Python

- suíte completa aprovada;
- fixtures reais com contagens congeladas;
- fingerprint semântico reproduzível;
- geração LSP com estrutura válida;
- requisito Multi-CSV documentado e fixtures individuais legíveis.

### Gate CAD

- carga do LSP atual em desenho controlado;
- execução em AutoCAD 2026 e Civil 3D 2026;
- manifesto semântico das entidades resultantes;
- segunda execução comprovando idempotência;
- tempos de carga, análise, criação e total;
- desenhos de entrada e saída preservados por hash.

AutoCAD/Civil 3D 2025 está fora do escopo e não integra nenhum gate da reconstrução. O porte para C# não começa enquanto os gates AutoCAD 2026 e Civil 3D 2026 não estiverem aprovados.

## Estado em 2026-08-07

- Gate Python: **aprovado** — 410 testes aprovados, 4 testes gráficos ignorados e 286 subtestes aprovados.
- Fixtures reais: **aprovadas** — 206/93/4 e 152/72/6 registros/textos/blocos.
- Fixtures Multi-CSV: **aprovadas individualmente** — cabeçalhos e delimitadores diferentes resolvidos pelas regras atuais.
- Manifesto e ambiente: **capturados** em `docs/evidence`.
- Gate CAD: **aprovado** — startup limpo no Core Console e baseline funcional, preservação e idempotência aprovadas em AutoCAD 2026 e Civil 3D 2026. Consulte `docs/evidence/cad/CAD_GATE_STATUS.md`.

## Reprodutibilidade Git

A árvore será registrada em um repositório Git local com branch `main`, commit de baseline e tag anotada `legacy-v2.1.3-native-rebuild-baseline`.

Como a cópia recebida contém uma pasta `.git` vazia e nenhuma identidade global está configurada, o commit de congelamento usa identidade técnica local ao repositório. Isso não altera a configuração Git global do usuário.
