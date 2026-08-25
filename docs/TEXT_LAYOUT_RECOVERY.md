# Text Layout Recovery

## Evidência dos desenhos reais

Os prints mostram textos do GeoAutoEngine disputando a mesma região com anotações grandes, símbolos
e pontos de levantamento. A causa confirmada no snapshot anterior era objetiva: somente `DBText` do
Model Space, alguns blocos de infraestrutura e duas famílias de estruturas viravam obstáculos.
`MText` e blocos topográficos anotados ficavam invisíveis para o layout.

A auditoria somente-leitura já existente dos dois DWGs fornecidos confirma o padrão dos prints:

| DWG | DBText | MText antes ignorado | Blocos anotados antes ignorados | Atributos cobertos pelos blocos |
| --- | ---: | ---: | ---: | ---: |
| GF-02-EP01-18-530.26-001 | 168 | 30 | 271 | 813 |
| GF-02-EP01-22-530.26-001 | 336 | 118 | 374 | 1.122 |

Os blocos anotados são predominantemente `SIMB_PONTO` no layer `_TG`, com atributos `PONTO`,
`DESC` e `COTA`. Esse conteúdo corresponde aos rótulos pequenos em ciano/vermelho/amarelo visíveis
nos prints. Capturar o `GeometricExtents` do bloco pai inclui o símbolo e seus três textos como uma
única região visual coerente.

## Política de produção

O modo padrão agora se chama `HybridSimple` e segue uma ordem rígida:

1. preservar uma posição existente gerenciada que continue sem colisão;
2. preservar exatamente a posição produzida pelo algoritmo Legacy real;
3. somente após colisão comprovada, testar três níveis cardinais proporcionais à altura do texto
   (0,25, 0,50 e 0,75 m para a altura padrão de 0,50 m);
4. escolher o menor deslocamento válido, mantendo lado e rotação;
5. retornar `RequiresReview` quando a região continuar congestionada.

O score só desempata candidatos já válidos. Clearance inferior a 0,15 m é instrumentado como
observação soft e não provoca movimento por si só. Sobreposição real continua hard. Footprints
recebem tolerância de contato para que toque de borda ou erro mínimo de métrica aproximada não seja
interpretado como colisão visual.

A distância entre uma posição existente e o Legacy deixou de invalidar a posição existente. O
limite padrão de 0,75 m vale somente para uma nova correção automática e escala com a altura.

## Reality Audit

Definir a variável de ambiente abaixo habilita o relatório apenas para desenvolvimento:

```text
GEOAUTOENGINE_ANNOTATION_REALITY_AUDIT_PATH=C:\temp\annotation-reality-audit.json
```

O JSON contém, por anotação:

- ID, origem e anchor;
- entidades CAD num raio de 5 m, com tipo, layer, handle, bounds e caminho de bloco;
- se a entidade entrou no `LayoutScene` e como foi classificada;
- entidades ignoradas e o motivo;
- colisões da posição Legacy;
- decisão final e distância movimentada;
- métricas agregadas, inclusive faixas de distância das correções.

Os smokes reais do AutoCAD e Civil 3D produziram relatórios com 9 entidades examinadas, 6 obstáculos
reconhecidos, 1 entidade ignorada e 1 anotação em cada host. A anotação próxima registrou `AcDbText`,
bloco de infraestrutura e linha de rede; na segunda execução, a decisão foi `KeepExisting`, movimento
zero, sem colisão Legacy e sem `UnnecessaryMoves`.

## Tipos reconhecidos

| Entidade CAD | Tratamento |
| --- | --- |
| `DBText` | obstáculo de texto; XData ainda identifica posição gerenciada existente |
| `MText` | obstáculo de texto por `GeometricExtents` |
| `AttributeReference` solto | obstáculo de texto |
| bloco importante conhecido | obstáculo de bloco |
| bloco com atributos ou texto na definição | obstáculo único pelo extent do bloco pai |
| Civil/AEC contendo `Label`, `Text` ou `Annotation` | obstáculo de texto quando há extent confiável |
| `AeccDbCogoPoint` | símbolo visual quando há extent confiável |
| estruturas planejadas/existentes reconhecidas | obstáculo de estrutura |
| anotações planejadas do GeoAutoEngine | obstáculo de texto aproximado |

## Tipos deliberadamente não capturados de forma cega

- XREF não vira um único obstáculo porque seu extent normalmente cobre uma região enorme;
- proxy desconhecido é auditado como `UnsupportedProxyEntity`;
- objeto Civil/AEC sem semântica visual reconhecida é auditado como `UnsupportedCivilEntityType`;
- linha que não pertence a uma rede reconhecida não vira barreira automaticamente;
- bloco genérico sem atributos/texto e fora do catálogo de infraestrutura permanece ignorado;
- hatches, faces e superfícies não são tratados como obstáculos sem evidência específica.

Essas entidades permanecem visíveis no Reality Audit para permitir suporte posterior baseado em um
DWG concreto, sem transformar o desenho inteiro em área proibida.

## Limites

Não foram adicionados callout, Polyline, MLeader, solver global ou heurística de manual override.
Casos congestionados são intencionalmente expostos como `RequiresReview`. A validação visual final
continua dependendo de abrir os DWGs que originaram os prints com o novo bundle; nenhum instalador é
gerado nesta etapa.

## Benchmark Release

| Cenário | Anotações | KeepLegacy | Corrected | RequiresReview | Unnecessary | Movimento máximo | Candidatos | Consultas | Tempo local |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Limpo | 5.000 | 5.000 | 0 | 0 | 0 | 0 m | 5.000 | 15.000 | 124,109 ms |
| Denso | 1.000 | 650 | 350 | 0 | 0 | 0,75 m | 5.200 | 11.400 | 155,379 ms |

No cenário denso, as 350 correções ficaram na faixa de 0,50–0,75 m e nenhuma ultrapassou o limite.
O modo histórico de score avaliou 23.000 candidatos e 49.000 consultas na mesma cena. Tempos são
checkpoints locais, não garantia independente de hardware.
