# Relatório final da Etapa 4 — escrita nativa no DWG

Data de fechamento: 2026-08-07

Estado: **concluída**

Target oficial: **AutoCAD 2026 / Civil 3D 2026**. AutoCAD/Civil 3D 2025 permanece fora do escopo.

## Resultado

O GeoAutoEngine 3.0 possui seu primeiro fluxo CAD utilizável inteiramente gerenciado:

```text
CSV(s) -> ImportBatch -> DrawingPlan -> Managed .NET API -> entidades no DWG
```

O comando `GEOAUTO3IMPORT` aceita uma ou várias fontes, executa o pipeline em memória e entrega o plano ao adaptador `GeoAutoEngine.Cad2026`. Não existe geração ou execução de AutoLISP/SCR nem arquivo intermediário entre o plano e o DWG.

## Arquitetura aplicada

- `Domain`: modelos e regras sem Autodesk;
- `Csv`: leitura individual das fontes e resolução de colunas;
- `Application`: orquestra `ImportBatch`, `DrawingPlan` e contrato do aplicador;
- `Cad2026`: interpreta o plano e concentra toda a Managed .NET API;
- `Plugin`: composition root, seleção Multi-CSV e comandos do host;
- `GeoAutoEngine.bundle`: manifesto R25.1 e os sete ativos de bloco aprovados.

O `DrawingPlan` continua independente da Autodesk. A referência do plugin a `Csv` existe apenas para compor o fluxo na borda; `Application` não depende de UI nem do host.

## Escrita implementada

O aplicador nativo:

1. adquire `DocumentLock`;
2. abre uma única `Transaction`;
3. garante layers, estilos e RegApps;
4. indexa as chaves XData já existentes;
5. cria `DBText`, `BlockReference`, `Polyline` fechada e `Circle`;
6. anexa XData rastreável a cada entidade;
7. faz um único commit ou rollback integral;
8. regenera uma vez somente quando houve criação.

As definições `hidrante`, `poste`, `pv`, `pv_esgoto_sanitario`, `pv_pluvial`, `pv_telefone` e `rg_agua_potavel` foram copiadas byte a byte do legado congelado, possuem manifesto SHA-256 e são empacotadas no bundle. As assemblies Autodesk continuam com `CopyLocal=false` e não são distribuídas.

## Identidade, XData e idempotência

Cada entidade recebe XData sob `GEOAUTOENGINE`, incluindo:

- `REGISTRO`: chave estável do item;
- `PLAN_ID`: identidade determinística do plano;
- `TIPO` e `CODIGO`;
- `ORIGEM`;
- `SOURCE_FILE` e `SOURCE_LINE`.

Antes da escrita, o adaptador lê as chaves existentes no espaço corrente. No smoke Multi-CSV, a primeira aplicação criou quatro entidades; a segunda criou zero, ignorou as quatro existentes e não regenerou o desenho.

## Política para dependência geométrica

Itens `Fixed` são aplicados com os valores exatos do plano. Itens `FollowNetwork` permanecem pendentes e produzem `CAD_FOLLOW_NETWORK_PENDING`; nenhuma entidade incompleta e nenhum ângulo arbitrário é criado.

Na fixture CAD existem sete itens ativos: quatro fixos foram materializados e três dependentes da rede ficaram pendentes. Essa divisão é parte observável de `DrawingPlanApplicationResult`.

## Equivalência e gate CAD

A regressão C# continua comparando duas fixtures reais, 358 registros e todas as propriedades independentes do DWG com o oráculo congelado da versão 2.1.3. O gate CAD acrescenta comparação exata das propriedades já resolvíveis.

O smoke usa dois CSVs com aliases e delimitadores diferentes na mesma sessão: 8 registros importados, 7 reconhecidos, 1 desconhecido e nenhuma rejeição. Em cada host, a aplicação bem-sucedida produziu:

| Entidade | Quantidade | Propriedade principal validada |
|---|---:|---|
| `DBText` | 1 | `Nº113`, layer `-Texto`, posição `(500,1000,0)` |
| `BlockReference` | 1 | `pv_esgoto_sanitario`, escala 1, layer `GF-Rede Esgoto` |
| `Polyline` | 1 | layer `GF-Grelha`, fechada, quatro vértices |
| `Circle` | 1 | layer `GF-Diagnostico`, raio 0,75, cor 1 |

Matriz executada e aprovada:

| Host | Sucesso + 2ª execução | Falha injetada/rollback | Undo |
|---|---:|---:|---:|
| AutoCAD 2026 | aprovado | aprovado, 0 entidades | aprovado, 0 entidades |
| Civil 3D 2026 | aprovado | aprovado, 0 entidades | aprovado, 0 entidades |

O undo funciona como uma unidade para o comando modal: após `UNDO 1`, nenhuma entidade GeoAutoEngine permanece. A falha injetada depois de duas criações aborta a transação e também deixa zero entidades.

## Testes e desempenho do gate

- build Release: zero erros e zero avisos;
- FAST: 45 testes em 197 ms;
- REGRESSION C#: 64 testes totais em 779 ms, incluindo equivalência, determinismo, Multi-CSV e ativos;
- LEGACY/FULL: 406 testes aprovados, 4 skips gráficos esperados e 286 subtests aprovados em 13,70 s;
- CAD: 6 cenários reais, todos aprovados;
- tempo CAD acumulado observado: aproximadamente 105 s nos dois hosts e três cenários;
- bundle: somente assemblies GeoAutoEngine e os sete DWGs aprovados;
- sessões AutoCAD preexistentes: preservadas pelo harness.

Os JSONs, logs, DWGs resultantes, tempos e hashes SHA-256 estão em `docs/evidence/stage4/cad`.

## Limitações deliberadas

Foram deixados exclusivamente para a Etapa 5:

- busca e indexação espacial de curvas do DWG;
- associação de rede e resolução de ambiguidade;
- tangente e rotação legível;
- colisão e offset inteligente;
- materialização final dos itens `FollowNetwork`;
- política de atualização de entidades quando a mesma identidade mudar de conteúdo.

Essas limitações não contaminam o Core e não bloqueiam itens fixos, estruturas ou alertas.

## Continuidade

O plano de implementação da Etapa 5 está em `STAGE_5_PLAN.md`.
