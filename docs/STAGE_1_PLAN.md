# Plano detalhado da Etapa 1 — fundação nativa e contratos

Estado: concluída — fundação e contratos entregues em 2026-08-07

Resultado: a topologia foi simplificada para cinco projetos de produção. `Presentation` e `Civil3D2026` não foram criados vazios; a justificativa está em `docs/adr/ADR-001-PROJECT-BOUNDARIES.md`.

Alvo: AutoCAD 2026 e Civil 3D 2026 exclusivamente

## Objetivo

Criar a fundação compilável do GeoAutoEngine 3.0 e transformar as decisões aprovadas em contratos C# verificáveis, sem ainda substituir a geração geométrica legado no DWG.

Ao final da etapa existirá uma solução .NET 8 com fronteiras explícitas, modelos tipados de `ImportBatch` e `DrawingPlan`, referências Autodesk 2026 isoladas e testes arquiteturais. Python/AutoLISP/SCR permanecem intactos como oráculo.

## Entregas

### 1. Estrutura da solução

- `GeoAutoEngine.Domain`: modelos e regras puras, sem UI, arquivo, Python ou Autodesk.
- `GeoAutoEngine.Application`: casos de uso, portas, resultados e orquestração.
- `GeoAutoEngine.Csv`: leitura de fontes, resolução de estrutura e produção do lote tipado.
- `GeoAutoEngine.Cad2026`: adaptador da API gerenciada do AutoCAD 2026.
- `GeoAutoEngine.Civil3D2026`: integração opcional e mínima com a API específica do Civil 3D 2026.
- `GeoAutoEngine.Plugin`: composição, comandos e ciclo de vida do plugin.
- `GeoAutoEngine.Presentation`: palette WPF e estado de sessão, sem regras de negócio.
- projetos de testes unitários, integração, arquitetura e contrato.

### 2. Build reproduzível

- solution e projetos em .NET 8;
- nullable e analyzers habilitados;
- versões centralizadas;
- builds `Debug` e `Release` determinísticos;
- referências `AcCoreMgd`, `AcDbMgd`, `AcMgd` e, quando necessário, `AeccDbMgd` exclusivamente da instalação 2026;
- assemblies Autodesk com `Private/CopyLocal=false`;
- falha explícita do build quando o SDK 2026 não estiver configurado;
- nenhum caminho Autodesk 2025.

### 3. Contratos de domínio Multi-CSV

Implementar os tipos fundamentais, inicialmente sem portar todas as regras legadas:

- `ImportBatch`, `SourceFile`, `ResolvedColumns` e `RecordOrigin`;
- `SurveyRecord`, `RejectedRecord`, `Diagnostic` e severidades;
- `DuplicateCandidate` com as duas origens e sem remoção automática;
- identificadores fortes e ordem determinística de fonte/registro/lote;
- resultados imutáveis ou somente leitura após consolidação;
- conveniência de arquivo único apenas como criação de lote com uma fonte.

### 4. Contratos da aplicação

- porta de seleção/leitura de múltiplas fontes;
- porta de importação por fonte;
- serviço de consolidação de lote;
- política substituível de diagnóstico de duplicidade;
- fronteira `DrawingPlan` independente de Autodesk;
- resultado de aplicação CAD com diagnóstico e rastreabilidade;
- cancelamento e progresso sem acoplamento à WPF.

### 5. Modelo inicial do DrawingPlan

Definir estruturas tipadas suficientes para representar a saída legada observada:

- textos, blocos, círculos/alertas e metadados XData;
- layer, posição XYZ, rotação, escala, conteúdo e identidade estável;
- origem única ou múltipla por item;
- itens suprimidos com regra e motivo;
- versão do schema do plano;
- nenhuma chamada à API CAD dentro do modelo.

### 6. Fronteira Autodesk 2026

- resolver documento, database, editor e transação somente no adaptador;
- introduzir interfaces para consulta de curvas/layers e aplicação do plano;
- manter a dependência Civil 3D opcional;
- definir bundle `.bundle` e manifesto de carregamento 2026, sem distribuir ainda como versão final;
- documentar thread affinity, document lock, transaction scope e rollback.

### 7. Registros arquiteturais

Criar ADRs para:

- matriz de projetos e direção das dependências;
- alvo exclusivo Autodesk 2026;
- `ImportBatch` como única entrada canônica;
- `DrawingPlan` como fronteira funcional/CAD;
- IDs, origem, ordenação e duplicidades;
- estratégia de referências Autodesk e empacotamento;
- convivência temporária com o oráculo legado.

## Testes da Etapa 1

A execução seguirá os tiers e gatilhos definidos em `docs/TEST_SUITE_AUDIT.md`. Testes C# novos deverão adotar a mesma separação conceitual entre feedback contínuo, regressão, legado e integração CAD.

### Unitários

- invariantes e igualdade dos value objects;
- ordenação determinística de fontes e registros;
- linha física versus índice de registro;
- lote de uma fonte seguindo o mesmo fluxo de lote múltiplo;
- candidato a duplicidade preservando as duas origens;
- diagnósticos sempre associados à origem quando aplicável;
- serialização/canonicalização do `DrawingPlan`.

### Arquiteturais

- Domain não referencia Application, CSV, WPF ou Autodesk;
- Application referencia apenas Domain;
- Csv não referencia WPF ou Autodesk;
- somente projetos `Cad2026`, `Civil3D2026` e de composição podem referenciar assemblies Autodesk;
- nenhuma referência, caminho, símbolo ou target 2025;
- nenhuma API pública principal orientada a CSV único.

### Build e smoke

- `dotnet restore`, `dotnet build` e `dotnet test` em Debug e Release;
- verificação de `CopyLocal=false` para assemblies Autodesk;
- carregamento mínimo do bundle em AutoCAD 2026 e Civil 3D 2026;
- comando de diagnóstico sem modificar o desenho;
- suíte Python legado continua verde durante toda a etapa.

## Sequência de execução

1. Registrar ADRs e convenções da solução.
2. Criar solution, propriedades comuns e projetos vazios.
3. Implementar modelos e invariantes do Domain.
4. Implementar portas e resultados da Application.
5. Criar contratos do Csv já orientados a coleção de fontes.
6. Definir `DrawingPlan` e sua forma canônica de comparação.
7. Isolar referências AutoCAD/Civil 3D 2026 nos adaptadores.
8. Criar composição e bundle mínimo.
9. Adicionar testes unitários e arquiteturais.
10. Executar builds, testes e smokes 2026; documentar evidências.

## Fora do escopo

- porte completo das regras de parsing/interpretação;
- aplicação geométrica equivalente no DWG;
- remoção de Python, AutoLISP ou SCR;
- redesign visual definitivo;
- suporte ou certificação 2025;
- Pipe Networks, COGO Points, Surfaces, Feature Lines ou novas funções BIM;
- remoção automática de duplicidades Multi-CSV.

## Critérios de saída

- solução compila e todos os testes passam;
- dependências respeitam as fronteiras definidas;
- somente assemblies Autodesk 2026 são referenciados;
- `ImportBatch` e `RecordOrigin` cobrem integralmente o contrato aprovado;
- `DrawingPlan` possui representação canônica pronta para comparação com a baseline;
- bundle mínimo carrega nos dois hosts 2026 sem alterar o DWG;
- nenhuma regressão na suíte legado;
- evidências e ADRs versionados;
- aprovação do usuário antes de iniciar o porte funcional da etapa seguinte.
