# Estratégia seletiva de testes da Etapa 2

O runner canônico da versão 3 é `scripts/test.ps1`. O antigo `test_stage1.ps1` permanece como wrapper para reprodutibilidade documental.

| Suite | Conteúdo na Etapa 2 | Gatilho |
|---|---|---|
| FAST | contratos, normalização, regras principais, parser, Multi-CSV, rejeições e duplicidades | ciclo contínuo |
| REGRESSION | todos os testes C#, incluindo as 358 linhas do oráculo semântico e testes arquiteturais | checkpoint de módulo |
| LEGACY | seleção classificada do Python/AutoLISP | alteração de baseline ou regra portada |
| CAD | plugin e hosts Autodesk 2026 | somente mudanças em Plugin, Cad2026, bundle ou integração |
| FULL | restore, Release, REGRESSION, legado completo e CAD | fechamento de etapa ou release |

```powershell
.\scripts\test.ps1 -Suite fast
.\scripts\test.ps1 -Suite regression -Configuration Release
.\scripts\test.ps1 -Suite legacy
.\scripts\test.ps1 -Suite cad
.\scripts\test.ps1 -Suite full
```

As mudanças da Etapa 2 estão restritas a Domain, Application, Csv e testes puros. O gate CAD não é disparado por essas alterações.
