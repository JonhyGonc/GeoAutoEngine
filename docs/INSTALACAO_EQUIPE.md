# Instalação do GeoAutoEngine na equipe

Como instalar uma versão **já compilada** (por exemplo, a 3.2.0) no computador de outro desenhista, **sem compilar nada lá**.

## Antes de tudo: o bundle serve para qual Civil 3D?

O plugin é uma pasta `GeoAutoEngine.bundle` com **um host por ano**:

```
GeoAutoEngine.bundle\
  PackageContents.xml        ← versão (AppVersion) e lista de comandos
  Contents\2024\...          ← Civil 3D 2024 (.NET Framework 4.8)
  Contents\2025\...          ← Civil 3D 2025 (.NET 8)
  Contents\2026\...          ← Civil 3D 2026 (.NET 8)
  Contents\Assets\...        ← blocos, modelos e logo
```

- O `PackageContents.xml` declara os três anos, mas **cada ano só funciona se a pasta `Contents\<ano>` existir e tiver sido compilada para ele**.
- O `scripts\deploy.ps1 -Version 2026` compila **só** o ano pedido. Um bundle copiado da sua máquina **serve apenas para os anos que você compilou nela**.
- A pasta `Contents\<ano>` de outro ano pode ter sobrado de uma build antiga, com versão velha. **Sempre confira a versão do ano que vai ser usado** (passo 3).
- Para levar **os três anos de uma vez**, use o **instalador** (opção B). Ele exige que a máquina de build tenha as DLLs do AutoCAD 2024, 2025 e 2026.

## Opção A — copiar o bundle da sua máquina (mesmo ano do Civil 3D)

**1. Na sua máquina (onde rodou o `deploy.ps1`), copie a pasta:**
```
%APPDATA%\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle
```
para um pendrive ou para a rede. Leve só se o colega usa **o mesmo ano** que você compilou.

**2. No computador do colega, com o Civil 3D fechado, faça backup do plugin atual (se existir):**
```powershell
$plugins = "$env:APPDATA\Autodesk\ApplicationPlugins"
if (Test-Path "$plugins\GeoAutoEngine.bundle") {
    Copy-Item "$plugins\GeoAutoEngine.bundle" "$env:USERPROFILE\Desktop\GeoAutoEngine.bundle-backup-$(Get-Date -Format yyyyMMdd)" -Recurse
    Remove-Item "$plugins\GeoAutoEngine.bundle" -Recurse
}
```

**3. Copie a pasta nova e confira a versão** (troque `X:\GeoAutoEngine.bundle` pelo local da cópia e `2026` pelo ano do colega):
```powershell
Copy-Item "X:\GeoAutoEngine.bundle" "$env:APPDATA\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle" -Recurse
Get-ChildItem "$env:APPDATA\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle" -Recurse | Unblock-File
(Get-Item "$env:APPDATA\Autodesk\ApplicationPlugins\GeoAutoEngine.bundle\Contents\2026\GeoAutoEngine.Plugin.dll").VersionInfo.FileVersion
```
✅ Deve mostrar **`3.2.0.0`**. O `Unblock-File` evita que o Windows bloqueie DLLs vindas da rede ou de um `.zip`.

**4. Abra o Civil 3D** e digite `GEOAUTO3PING`. O plugin deve carregar. O texto ainda diz "3.0"; quem confirma a versão é o passo 3.

## Opção B — instalador com os três anos

Na máquina de build (com as DLLs do AutoCAD 2024, 2025 e 2026; para o 2024 serve o ObjectARX em `C:\Autodesk\ObjectARX_for_AutoCAD_2024_Win_64bit_dlm\inc`):
```powershell
.\scripts\build-release.ps1
```
O script roda os testes, compila os 3 hosts, recusa empacotar DLLs da Autodesk e gera:
- `artifacts\release\installer\GeoAutoEngine3_Setup.exe`
- `artifacts\release\installer\GeoAutoEngine3_Setup.exe.sha256.txt`

Esses dois arquivos podem ser anexados à Release `v3.2.0` no GitHub.

No computador do colega:
1. faça o **backup do passo 2** da opção A (o instalador só guarda um backup temporário durante a instalação);
2. feche o Civil 3D e rode o `GeoAutoEngine3_Setup.exe`;
3. confira a versão como no **passo 3** da opção A.

## Voltar à versão anterior (rollback)

Com o Civil 3D fechado:
```powershell
$plugins = "$env:APPDATA\Autodesk\ApplicationPlugins"
Remove-Item "$plugins\GeoAutoEngine.bundle" -Recurse
Copy-Item "$env:USERPROFILE\Desktop\GeoAutoEngine.bundle-backup-AAAAMMDD" "$plugins\GeoAutoEngine.bundle" -Recurse
```
Troque `AAAAMMDD` pela data do backup. Depois confira a versão com o comando do passo 3 (deve mostrar a anterior, por exemplo `3.1.1.0`).

## Problemas comuns
- **O comando não existe no Civil 3D:** a pasta `Contents\<ano>` do ano do colega não existe. Use um bundle compilado para esse ano ou o instalador (opção B).
- **A versão mostra outro número:** a pasta daquele ano veio de uma build antiga. Recompile esse ano na máquina de build (`deploy.ps1 -Version <ano>`) e copie de novo.
- **O Civil 3D avisa sobre carregar um arquivo não confiável:** confira se a pasta está exatamente em `%APPDATA%\Autodesk\ApplicationPlugins` e se o `Unblock-File` foi executado.
