# Plano executado da Etapa 4 — aplicação CAD nativa

## Escopo aprovado

A Etapa 4 materializa diretamente no DWG os itens do `DrawingPlan` que não exigem consulta geométrica ao desenho. O fluxo nativo é:

```text
CSV(s)
  -> ImportBatch
  -> DrawingPlan
  -> GeoAutoEngine.Cad2026
  -> DocumentLock + Transaction
  -> entidades reais + XData
  -> commit ou rollback integral
  -> uma regeneração controlada
```

O escopo originalmente esboçado para associação, tangente, colisão e rotação foi transferido para a Etapa 5 por decisão arquitetural aprovada. A Etapa 4 não inventa resultados geométricos: itens `FollowNetwork` ficam pendentes, preservam sua identidade e geram diagnóstico explícito.

## Pacotes concluídos

### 4.1 — Fronteira transacional

- documento ativo e `DocumentLock` no limite do adaptador;
- uma `Transaction` de escrita por aplicação;
- commit atômico ou `Abort` integral;
- falha injetável para provar rollback;
- uma chamada a `Editor.Regen()` somente depois de commit com criações;
- integração Civil 3D sem dependência Aecc.

### 4.2 — Recursos e escritores tipados

- garantia de layers, inclusive cor ACI, estado ligado e descongelado;
- garantia dos estilos `GEO_ARIAL` e `GEO_ARIAL_BOLD`;
- `DBText` para textos com rotação fixa;
- carregamento de sete definições DWG e criação de `BlockReference`;
- escala XYZ, posição, layer, cor e rotação fixa vindas do plano;
- `Polyline` fechada para estruturas pluviais;
- `Circle` para alertas e revisões.

### 4.3 — Identidade e idempotência

- registro da aplicação XData `GEOAUTOENGINE`;
- gravação de tipo, chave estável, código, origem, `PLAN_ID`, arquivo e linha;
- leitura das chaves existentes no espaço corrente antes da escrita;
- segunda execução ignora os itens já materializados;
- itens `FollowNetwork` não recebem entidade provisória nem ângulo fictício.

### 4.4 — Composição e comandos

- `GEOAUTO3IMPORT`: seleção múltipla de CSV, importação, construção e aplicação;
- `GEOAUTO3VERIFY`: inspeção somente leitura das entidades nativas geradas;
- `GEOAUTO3PING`: smoke de carregamento mantido;
- nenhuma dependência de AutoLISP, SCR ou arquivo intermediário no caminho de produção.

## Gate de encerramento

O fechamento exige build/test Release, regressão do Core, comparação com o oráculo congelado e smokes reais em AutoCAD 2026 e Civil 3D 2026 para sucesso, segunda execução, rollback e undo. Os resultados consolidados estão em `STAGE_4_FINAL_REPORT.md`.
