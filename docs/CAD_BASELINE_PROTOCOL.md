# Protocolo do baseline AutoCAD/Civil 3D

## Finalidade

Medir o runtime legado dentro de hosts Autodesk reais e produzir uma referência semântica para o futuro adaptador .NET. O protocolo usa AutoLISP apenas como oráculo legado; isso não autoriza AutoLISP na distribuição 3.0.

## Matriz de host

| Host | Versão | Obrigatório |
|---|---:|---|
| AutoCAD | 2026 | Sim |
| Civil 3D | 2026 | Sim |

AutoCAD e Civil 3D 2025 estão fora do escopo. Não são alvo de teste, empacotamento, referência de API nem certificação do GeoAutoEngine 3.0.

## Desenho controlado

O desenho de entrada deve conter, em ModelSpace:

- `Line`, `LwPolyline`, `Polyline2d`, `Polyline3d`, `Arc` e `Spline` abertas;
- uma curva fechada que deve ser ignorada;
- layers de cada rede catalogada e aliases relevantes;
- casos a 0,10 m, 0,50 m, 0,70 m, acima de 0,70 m e até 5,00 m;
- empate geométrico determinístico;
- obstáculos de texto e blocos;
- blocos já carregados e ausentes;
- cenário de rede `NI` inequívoco e ambíguo;
- cenário de colisão densa;
- estrutura de grelha e boca de lobo.

## Execução

O gate possui duas partes. O smoke de startup usa o Core Console com perfil temporário `/isolate` para comprovar que o script chega ao comando `QUIT` sem interferência externa. A captura funcional usa o host desktop real porque o oráculo legado depende de ActiveX (`IAcadApplication`, `ActiveDocument` e `ModelSpace`), superfície que não está disponível no Core Console.

O harness define `SECURELOAD=0` somente no processo isolado para carregar o oráculo AutoLISP versionado fora de um diretório Autodesk confiável. O perfil normal não é alterado. Essa exceção pertence exclusivamente ao baseline legado; o plugin 3.0 deverá usar bundle confiável e assinatura digital.

Para cada host:

1. Copiar o desenho de entrada para uma pasta temporária específica do host.
2. Gerar o LSP legado a partir da fixture fixa.
3. Registrar hash do DWG de entrada e do LSP.
4. Carregar e executar `GEOAUTOPROF`.
5. Salvar o primeiro resultado e exportar o manifesto de entidades.
6. Executar novamente o mesmo LSP.
7. Salvar o segundo resultado e exportar novo manifesto.
8. Comparar contagens, conteúdo, posição, rotação, layers, blocos, XData e entidades removidas.
9. Registrar tempos e mensagens de erro.

## Manifesto semântico

O relatório deve registrar, sem depender do handle:

- host e versão;
- tipo de entidade;
- identificador GeoAutoEngine em XData;
- layer;
- conteúdo textual;
- posição XYZ;
- rotação;
- nome, escala e rotação de bloco;
- vértices de polylines;
- centro e raio de círculos;
- origem do registro quando disponível;
- contagens por tipo;
- duração de cada fase.

## Critérios de aprovação

- nenhuma exceção não tratada;
- entidades esperadas presentes;
- curva fechada ignorada;
- resultados semanticamente iguais entre primeira e segunda execução;
- nenhum objeto do usuário removido;
- diferenças entre hosts explicadas e classificadas;
- arquivos e logs preservados com SHA-256.
