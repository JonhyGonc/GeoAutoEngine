# Gate CAD da Etapa 0

Data: 2026-08-07  
Estado: **aprovado**

## Escopo oficial

- AutoCAD 2026.
- Civil 3D 2026.
- AutoCAD/Civil 3D 2025 fora do escopo, sem requisito de compatibilidade retroativa.

## Startup do Core Console

Os smokes isolados `2026/ACAD` e `2026/C3D` encerraram com código zero, chegaram ao comando `QUIT` e não registraram referência ao TopoCAD. A interferência externa observada anteriormente desapareceu.

O log `core_console_startup_interference_2026_c3d.log` foi mantido somente como cadeia histórica do bloqueio anterior. Os logs e relatórios `core_console_startup_smoke_2026_*` são a evidência vigente.

## Execução funcional do oráculo legado

O runtime legado foi executado no host desktop real. Essa escolha é obrigatória para a baseline porque o código atual usa ActiveX (`IAcadApplication`, `ActiveDocument` e `ModelSpace`), indisponível no Core Console.

Resultados idênticos em AutoCAD 2026 e Civil 3D 2026:

| Verificação | Resultado |
|---|---:|
| Registros da fixture | 10 |
| Textos preparados | 14 |
| Blocos preparados | 2 |
| Entidades de entrada | 9 |
| Entidades de saída | 26 |
| Entidades geradas com XData | 17 |
| Entidades do usuário preservadas | Sim |
| Manifestos iguais após segunda execução | Sim |
| Hash semântico comum entre hosts | `c82c2a0dfa614d42db3349b2cf64dc06c27f3488927ddd27e1ac5691d171ffa1` |

O manifesto cobre tipo, layer, texto, posição, rotação, bloco, escalas, vértices/coordenadas, início/fim, raio e XData. Handles são preservados na evidência bruta, mas excluídos da comparação semântica.

## Segurança e rastreabilidade

- Nenhum DWG de produção foi aberto ou alterado.
- O desenho controlado foi criado a partir de uma cópia temporária de um DWG Autodesk.
- `SECURELOAD=0` e `ISAVEBAK=0` foram definidos somente nos processos isolados do baseline.
- O perfil normal do usuário não foi alterado.
- Cada host possui DWG de entrada, duas saídas, runtime gerado, três manifestos, marcadores, logs, relatório JSON e hashes SHA-256.

## Evidências consolidadas

- `CAD_BASELINE_SUMMARY.json`: resultado agregado dos gates.
- `CAD_EVIDENCE_SHA256.csv`: inventário e SHA-256 dos artefatos.
- `cad_baseline_2026_acad.json` e `cad_baseline_2026_c3d.json`: resultados por host.

Conclusão: o gate CAD oficial da Etapa 0 está aprovado.
