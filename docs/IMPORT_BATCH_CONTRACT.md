# Contrato de importação Multi-CSV

## Objetivo

Importar uma coleção ordenada de CSVs pertencentes ao mesmo levantamento sem concatenar, copiar ou reescrever fisicamente os arquivos. Cada fonte é lida e resolvida de forma independente; somente os registros tipados são consolidados em memória.

## Fluxo obrigatório

```text
SourceFile[]
  -> leitura individual
  -> resolução individual de colunas
  -> normalização e interpretação por registro
  -> validação por fonte
  -> consolidação tipada em memória
  -> diagnóstico do lote
  -> DrawingPlan único
  -> aplicação única no DWG
```

## Modelo conceitual

```text
ImportBatch
  BatchId
  SourceFiles[]
  ImportedRecords[]
  RejectedRecords[]
  Diagnostics
  DuplicateCandidates[]

SourceFile
  SourceFileId
  SelectionOrdinal
  DisplayName
  CanonicalPath
  ContentSha256
  Encoding
  Delimiter
  ResolvedColumns
  RecordCount
  Status (Imported/Rejected)

RecordOrigin
  SourceFileId
  SourceFileName
  SourceRecordIndex
  SourceLineNumber
  BatchOrdinal

SurveyRecord
  RecordId
  Origin
  RawValues
  ParsedValues
  Interpretation
```

## Identidade, ordem e rastreabilidade

- `SourceRecordIndex` é o ordinal base zero do registro de dados dentro da fonte, sem contar o cabeçalho.
- `SourceLineNumber` é a linha física base um apresentada ao usuário. O importador deve obtê-la do parser e não reconstruí-la a partir do DataFrame.
- `BatchOrdinal` preserva uma ordem determinística: ordem de seleção das fontes e, dentro de cada fonte, ordem original dos registros.
- `RecordId` deve ser estável dentro do lote e derivado de `SourceFileId` e `SourceRecordIndex`, sem depender do nome isolado do arquivo.
- Diagnósticos, rejeições, itens do `DrawingPlan` e entidades suprimidas por regras existentes devem apontar para um ou mais `RecordOrigin`.

Arquivos CSV com campos quoted que contenham quebras de linha exigem que o parser registre a linha física real. Não é aceitável calcular `SourceLineNumber` apenas como índice mais dois.

## Processamento independente por fonte

Cada `SourceFile` passa individualmente por:

- detecção de encoding;
- detecção de delimitador;
- leitura do cabeçalho;
- aliases;
- fallback posicional;
- resolução de código, Norte/Y e Leste/X;
- parsing numérico;
- validação estrutural.

Mapeamentos de colunas não são compartilhados por inferência entre arquivos. Dois arquivos com estruturas diferentes podem pertencer ao mesmo lote se ambos forem resolvidos de forma válida.

## Falhas e atomicidade

- Registros inválidos são preservados como `RejectedRecord`, com origem e motivo.
- Um arquivo inteiramente ilegível ou sem colunas mínimas válidas torna o lote não aplicável ao DWG até que seja corrigido ou removido da sessão.
- Um lote com rejeições de registros pode ser diagnosticado, mas a política de permitir sua aplicação deve ser explícita na tela de confirmação.
- A aplicação ao DWG é uma operação única sobre o `DrawingPlan` consolidado; não existe uma transação CAD por CSV.

## Duplicidades entre fontes

A detecção de duplicidades é diagnóstica. Nenhum `SurveyRecord` pode ser removido do `ImportBatch` por suspeita.

O modelo deve distinguir pelo menos:

- `ExactDuplicate`: mesmos valores normalizados relevantes e mesma posição;
- `SpatialDuplicateCandidate`: mesmo código normalizado em posições dentro da tolerância da política;
- motivo, distância e as duas origens envolvidas.

A política será implementada como componente substituível. A decisão de consolidar registros duplicados fica fora desta etapa.

As regras legadas de deduplicação de entidades de saída continuam sendo caracterizadas separadamente. Quando uma delas suprimir um texto ou bloco, o registro não desaparece: o `DrawingPlan` deve registrar a supressão, a regra aplicada e todas as origens relacionadas.

## Diagnóstico

O diagnóstico deve possuir duas visões:

1. Por arquivo: estrutura detectada, encoding, delimitador, colunas, importados, rejeitados e avisos.
2. Consolidada: totais, redes, tipos, materiais, códigos desconhecidos, coordenadas inválidas e candidatos a duplicidade.

Na implementação da Etapa 2, a visão consolidada é exposta por `ImportBatch.Summary`; ocorrências detalhadas permanecem em `Diagnostics`. Fontes estruturalmente inválidas continuam no lote com `Status=Rejected`, zero registros e diagnóstico de erro, permitindo que as demais fontes sejam analisadas sem perda de contexto.

Toda ocorrência deve permitir navegação até arquivo e linha.

## Interface

O fluxo principal permanece:

1. Selecionar levantamento(s).
2. Processar.
3. Diagnóstico.

A seleção permite adicionar, listar e remover fontes antes do processamento. A lista mostra nome, estado e quantidade de registros. O cabeçalho da sessão mostra o total consolidado. Configurações avançadas ficam fora do fluxo principal.

## Critérios de aceitação mínimos

- Um CSV usa exatamente o mesmo fluxo de um lote com uma fonte.
- Um lote aceita simultaneamente `Norte/Leste/Código` e `Coord Y/Coord X/Descrição` quando cada fonte é válida.
- Remover uma fonte elimina somente seus registros e recalcula o diagnóstico.
- Reordenar fontes produz ordem determinística sem alterar a interpretação individual.
- Um erro aponta para nome do arquivo, linha física e índice do registro.
- Candidatos a duplicidade mantêm as duas origens e não removem registros.
- O restante da aplicação recebe somente registros consolidados e não contém branches “arquivo único versus múltiplos arquivos”.
