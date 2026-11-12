# ADR 004 — exceção única ao "sem async void" na fronteira da paleta

Estado: aceito em 2026-11-12 (Chat 00, decisão E do PR #16; marco M1 do GeoEcosystem)

## Contexto

Desde a versão inicial, `ArchitectureTests.PaletteBoundary_HasNoAsyncOrApplicationCurrentCallbacks` proíbe `async void`, `Task.Run` e `Application.Current` no `GeoAutoEngine.Plugin`. Callbacks da paleta que mexem no desenho passam pela fila de comandos (`SendStringToExecute`), no contexto de documento do AutoCAD.

No M1.2 (#8, commits `513286a`/`559d8b1`), o botão **Exportar correções para GeoAutoDocs** passou a esperar a confirmação do GeoAutoDocs de forma assíncrona. A confirmação depende de revisão humana e pode levar minutos. A versão síncrona travava o Civil 3D por até 5 minutos (auditoria GAE-05). O handler de clique do WPF é necessariamente `async void`.

## Decisão

É permitido **exatamente um** `async void` no Plugin: `ExportCorrectionsClick` em `src/GeoAutoEngine.Plugin/VideoInspection/VideoInspectionView.xaml.cs`. Qualquer outro continua proibido, assim como `Task.Run` e `Application.Current`.

Condições da exceção, verificadas pelo teste:

- o handler trata toda exceção com `try/catch` e registra no `PluginLog`, já que uma exceção não tratada num `async void` derrubaria o host;
- o fluxo não acessa o DWG: só a sessão em memória (`GeoAutoEngine.Application`), o pipe e a UI. Por isso não precisa de contexto de documento nem de bloqueio.

A experiência do M1 (botão sem travar o Civil 3D, com o resultado mostrado na janela) fica mantida. O comando `GEOAUTO3VIEXPORTCORRECTIONS` (#14) reaproveita o mesmo fluxo pelo `Dispatcher` da thread principal, sem novo `async void`.

## Consequências

- `PaletteBoundary_HasNoAsyncOrApplicationCurrentCallbacks` falha se surgir um segundo `async void`, se este mudar de nome ou de arquivo, ou se perder o `catch` com log.
- Um novo caso precisa de outra decisão registrada aqui.
