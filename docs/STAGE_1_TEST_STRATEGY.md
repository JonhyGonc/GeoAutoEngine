# Estratégia seletiva de testes da Etapa 1

## Suites

| Suite | Conteúdo | Gatilho |
|---|---|---|
| FAST | testes C# com trait `Category=FAST` | desenvolvimento contínuo e arquivo/módulo alterado |
| REGRESSION | todos os testes C# e build da solução | conclusão de módulo e gates internos |
| LEGACY | testes Python/AutoLISP classificados como `legacy` | gates de equivalência e alterações no baseline |
| CAD | build Release, validação do bundle e smokes reais nos dois hosts 2026 | mudanças em Plugin, Cad2026, bundle ou Autodesk |
| FULL | REGRESSION C# + Python completo + CAD | fechamento de etapa e release |

O ponto de entrada é:

```powershell
.\scripts\test_stage1.ps1 -Suite fast
.\scripts\test_stage1.ps1 -Suite regression
.\scripts\test_stage1.ps1 -Suite legacy
.\scripts\test_stage1.ps1 -Suite cad
.\scripts\test_stage1.ps1 -Suite full
```

Execute `dotnet restore GeoAutoEngine.sln` uma vez após clonar ou alterar dependências. As suites FAST, REGRESSION e CAD usam `--no-restore` para não acessar rede nem cache NuGet incidentalmente durante o ciclo de desenvolvimento; FULL faz o restore explicitamente.

Durante a implementação comum, a suíte Python completa de 406 testes não é executada. Ela permanece obrigatória no fechamento da Etapa 1.
