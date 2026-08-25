# ADR 003 — integração Autodesk 2026

Estado: aceito em 2026-08-07

## Decisão

O alvo exclusivo é AutoCAD 2026 e Civil 3D 2026. Os projetos CAD usam `net8.0-windows` e referenciam `AcCoreMgd`, `AcDbMgd` e `AcMgd` da instalação `AutoCAD 2026` por configuração centralizada. Todas as referências possuem `Private=false`; assemblies Autodesk nunca entram no bundle.

O manifesto limita o runtime à série `R25.1` e registra somente o comando de diagnóstico `GEOAUTO3PING`. O carregamento é sob demanda. `IExtensionApplication.Initialize` faz apenas a leitura de `DBMOD` usada pelo gate; não abre transação, não bloqueia documento e não modifica o database.

O comando de diagnóstico lê `ACADVER` e `DBMOD`. O gate compara o `DBMOD` observado em `Initialize` com o valor ao final do comando; o valor deve permanecer idêntico. Ele só grava um marcador externo quando a variável de ambiente do harness CAD está definida. Nenhuma entidade, layer, estilo, dicionário ou XData é criado. O hash físico do arquivo aberto é registrado apenas como observação, porque o próprio ciclo desktop da Autodesk pode regravar o DWG mesmo sem mudança causada pelo plugin.

## Escopos transacionais futuros

Toda leitura ou escrita de documento, editor, database, document lock e transaction deverá permanecer em `GeoAutoEngine.Cad2026`. A aplicação completa de `DrawingPlan`, inclusive rollback, está fora da Etapa 1.

## Compatibilidade

AutoCAD/Civil 3D 2025 não é referenciado, testado, empacotado ou certificado. A API específica Civil 3D continua com dependência zero nesta etapa.
