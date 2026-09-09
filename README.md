# GeoAutoEngine 3

Reconstrução nativa em .NET 8 para AutoCAD 2026 e Civil 3D 2026.

## Estrutura

- `src/`: projetos de produção;
- `tests/`: testes C# seletivos;
- `docs/`: arquitetura, etapas e evidências;
- `scripts/`: build, testes e gates;
- `tools/`: ferramentas auxiliares de manutenção;
- `build/`: manifesto e estrutura do bundle Autodesk;
- `assets/`: ativos exclusivos da versão 3;
- `samples/`: exemplos exclusivos da versão 3.

## Validação rápida

```powershell
.\.dotnet\dotnet.exe restore GeoAutoEngine.sln
.\scripts\test.ps1 -Suite fast
.\.dotnet\dotnet.exe build GeoAutoEngine.sln -c Release --no-restore
```

O código legado e seu ambiente Python estão em `../GeoAutoEngine.Legacy`. Dados de equivalência compartilhados ficam em `../reference`.

O fluxo nativo completo executa `CSV(s) -> ImportBatch -> DrawingPlan -> resolução geométrica -> entidades no DWG` pela Managed .NET API. O fechamento final da versão 3.0.15 está em `docs/STAGE_6_FINAL_REPORT.md`.

## Integração GeoMap

O comando `GEOAUTO3GEOMAPEXPORT` exporta Polylines 2D do Civil 3D para o contrato GeoMap v0.1. Consulte [docs/GEOMAP_EXPORT.md](docs/GEOMAP_EXPORT.md) para escopo, instalação e uso.
