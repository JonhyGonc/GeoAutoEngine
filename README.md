# GeoAutoEngine 3

Plugin nativo em .NET para Civil 3D 2024, 2025 e 2026.

| Civil 3D | Release | Runtime |
|---|---|---|
| 2024 | R24.3 | .NET Framework 4.8 |
| 2025 | R25.0 | .NET 8 |
| 2026 | R25.1 | .NET 8 |

O bundle traz um host por versão. O estado da validação de cada uma (build e smoke no CAD) está em [COMPATIBILITY.md](COMPATIBILITY.md).

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

## Testes que dependem de dados externos

Os testes marcados com `[Trait("Category", "EXTERNAL")]` leem pastas que ficam **fora** deste repositório:

- `../reference`: CSVs de levantamento e oráculos de regressão (`legacy_csv/`, `regression_data/`). Os CSVs são de obras reais e **não** são versionados aqui;
- `../GeoAutoEngine.Legacy`: o logo original, para comparação.

A CI (`.github/workflows/tests.yml`, Windows) roda a suíte inteira **menos** essa categoria. Para rodá-los, coloque as duas pastas ao lado do clone e execute:

```powershell
.\.dotnet\dotnet.exe test tests\GeoAutoEngine.Tests\GeoAutoEngine.Tests.csproj -c Release --filter "Category=EXTERNAL"
```

Sem essas pastas, os testes `EXTERNAL` falham, inclusive o que também faz parte da suíte `fast`.

`DrawingPlanBuilderTests.Stage4CadSmokeFixture_ProducesFourFixedAndThreeGeometryPendingItems` lê `samples/stage4_cad_smoke_day*.csv` pelo caminho `../reference/../GeoAutoEngine3/samples`, ou seja, também exige que o clone esteja numa pasta chamada `GeoAutoEngine3`.

O fluxo nativo completo executa `CSV(s) -> ImportBatch -> DrawingPlan -> resolução geométrica -> entidades no DWG` pela Managed .NET API. O fechamento final da versão 3.0.15 está em `docs/STAGE_6_FINAL_REPORT.md`.

## Instalação na equipe

Para instalar uma versão já compilada no computador de outro desenhista, com backup, conferência da versão e rollback, veja [docs/INSTALACAO_EQUIPE.md](docs/INSTALACAO_EQUIPE.md).

## Integração GeoMap

O comando `GEOAUTO3GEOMAPEXPORT` exporta Polylines 2D do Civil 3D para o contrato GeoMap v0.1. Consulte [docs/GEOMAP_EXPORT.md](docs/GEOMAP_EXPORT.md) para escopo, instalação e uso.
