# RELATÓRIO DE AUDITORIA CAD → GIS

## 1. Resumo executivo

A auditoria somente-leitura dos dois DWGs reais confirmou que o conteúdo técnico é composto principalmente por polylines, blocos e textos AutoCAD; não foram encontradas redes nativas `Pipe/Structure/Pressure Pipe` do Civil 3D no Model Space. As legendas ficam no Paper Space e foram tratadas como catálogo corporativo, não como ocorrência física.

A legenda comum enumera dez famílias: águas pluviais, esgoto sanitário, esgoto industrial, elétrica, água potável, gás, telefone, fibra óptica, incêndio e rede sem descrição de uso. Nos Model Spaces auditados, a ocorrência linear efetiva é menor e varia por projeto.

O principal achado de modelagem é que atributos como profundidade, diâmetro e condição pertencem a observações/pontos e só depois devem ser associados a trechos. Importar cada texto como feição produziria duplicação semântica.

Nenhum material de tubulação foi encontrado. O sistema geodésico não está atribuído ao DWG: a legenda informa UTM/SIRGAS 2000, mas omite a zona. Portanto, nenhum SRID deve ser gravado automaticamente.

## 2. Informações gerais do DWG

| DWG | SHA-256 | Versão | INSUNITS | Model | Paper | Layers | Blocos | DBMOD |
|---|---|---|---|---|---|---|---|---|
| GF-02-EP01-18-530.26-001.dwg | 70345F4C536EBBC6395CD862A9B75A4C3445301C46910D24525DEB48AB8A836F | AC1014 | Undefined | 675 | 106 | 251 | 186 | 0 → 0 (inalterado) |
| GF-02-EP01-22-530.26-001.dwg | 4DE06E4FB7855585FE26FD1F5E8A2F36557DEBB040FCF780A6239754D02CE473 | AC1014 | Meters | 1322 | 106 | 61 | 35 | 0 → 0 (inalterado) |

Os originais foram abertos apenas para hash. A API leu cópias temporárias com `/readonly`; nenhum `SAVE`, `PURGE`, `OVERKILL`, conversão ou escrita no DWG foi executado.

## 3. Sistema de coordenadas

**SISTEMA DE COORDENADAS: NÃO DETERMINADO COM SEGURANÇA.**

Evidências encontradas:

- `CGEOCS` vazio nos dois DWGs; nenhum código está atribuído.
- INSUNITS: `Undefined` no primeiro e `Meters` no segundo.
- A legenda declara: 4- MEDIDAS EM METROS (M ) E DIÂMETROS EM MILÍMETROS (MM ) COORDENADAS E COTAS UTM, DATUM SIRGAS 2000 - COORDENADAS NAVEGADAS. 
- As coordenadas têm ordem de grandeza UTM e os projetos são de Joinville/SC.

[`EPSG:31982`](https://epsg.org/crs_31982/SIRGAS-2000-UTM-zone-22S.html) é apenas candidato técnico (SIRGAS 2000 / UTM 22S). Falta confirmação explícita da zona; não deve ser aplicado automaticamente.

## 4. Inventário de entidades

| Tipo | GF-02-EP01-18-530.26-001.dwg | GF-02-EP01-22-530.26-001.dwg | Possível função | Relevância GIS |
|---|---|---|---|---|
| AcDbBlockReference | 302 | 491 | Pontos, estruturas e símbolos | ALTA/MISTA |
| AcDbHatch | 0 | 4 | Representação gráfica de grelhas | NÃO independente |
| AcDbLine | 54 | 65 | Malha/perfis/anotação | BAIXA/MISTA |
| AcDbMText | 30 | 118 | Notas e vídeo-inspeção | ATRIBUTO/INSPEÇÃO |
| AcDbPolyline | 119 | 308 | Trechos, estruturas fechadas e desenho de apoio | ALTA/MISTA |
| AcDbText | 168 | 336 | Atributos/anotações | ATRIBUTO, não feição autônoma |
| AeccDbFace | 2 | 0 | Face Civil 3D de apoio | APOIO, não rede |

Objetos Civil 3D nativos observados no Model Space: duas `AeccDbFace` no primeiro DWG. Não há `Pipe`, `Structure`, `Alignment`, `FeatureLine`, `PressurePipe` nem `CogoPoint` como entidades nativas nos dois Model Spaces.

## 5. Inventário de layers

A tabela abaixo lista todos os layers existentes, inclusive os sem objetos no Model/Paper Space. Contagens dentro de definições de bloco não são somadas para evitar confundir conteúdo de símbolo com inserções.

### GF-02-EP01-18-530.26-001.dwg

| Layer | Model | Tipos Model | Paper | Cor | Linetype | Estado | Significado | Rede | Classe |
|---|---|---|---|---|---|---|---|---|---|
| -Texto | 30 | AcDbText:30 | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| 0 | 2 | AeccDbFace:2 | 10 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| 0_FORMATO | 0 | — | 31 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| A-ANOTACAO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-ANOTACAO-TABL-PATT | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-ARTICULACAO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-ARTICULACAO-TXT | 0 | — | 0 | ByAci:4 | CENTER | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-FEATURE_LINE | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-FORMATO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| A-MODELAGEM | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-MODELAGEM_FLUXO | 0 | — | 0 | ByAci:130 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-MODELAGEM_GRID | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-MODELAGEM_GRID_TXT | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-MODELAGEM_TABELA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-QUANTIDADES | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-RETICULA-UTM | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| A-SC-FORMATO | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| A-TEXTO_GERAL | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| A-WINDOWS | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-CONEXOES | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-EQUIPAMENTOS | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-EQUIPAMENTOS-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-REDE | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-TABELA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-TUBO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-TUBO-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| AGUA-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| B2-INTERFERENCIAS | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-AP-POLIGONAL | 0 | — | 0 | ByAci:20 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-DI_FIG | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-DI_NET | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-FIGURA | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-HM-COTAS-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-HM-CURVA_INTERMEDIARIA | 0 | — | 0 | ByAci:45 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-HM-CURVA_MESTRA | 0 | — | 0 | ByAci:20 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-HM-PTO-TOPO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-TA-NUVEM_PONTOS | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| C1-TABELA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| D4-SEDIMENTOS-SOLOS | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Defpoints | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=False | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F-1HZ-MLB | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F-HZ-ESTACA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F-HZ-TEXTO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F-SC-PROJECAO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F-VT-NOTAVEL | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F-VT-TEXTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-ACOSTAMENTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-ARREDONDAMENTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-AZIMUTE | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-BORDO | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-CORREDOR | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-CORREDOR-FEAT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-CURVAS | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-EIXO | 6 | AcDbPolyline:6 | 2 | ByAci:2 | DASHDOT | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-ESTACA | 16 | AcDbBlockReference:8, AcDbMText:8 | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-GUIA | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Feição de contexto/infraestrutura | — | TÉCNICA/MISTA |
| F1-HZ-HATCH | 0 | — | 0 | ByAci:251 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-INTERSECAO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-INTERSECAO-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-NOTAVEL | 47 | AcDbBlockReference:10, AcDbLine:12, AcDbMText:22, AcDbPolyline:3 | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-OFFSET | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-OFFSET-ATERRO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-OFFSET-CORTE | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-PASSEIO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-PI | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-QUILOMETROS | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-SARJETA | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-SECAO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-SECAO-TXT | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-SUPER | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-TABELA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-TALUDE | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-HZ-TEXTO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-VALETA | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-ASSM | 0 | — | 0 | ByAci:40 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-ASSM-BLIN | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-ASSM-OFFS | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-CAIXA | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| F1-SC-CORR | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-CORR-BNDY | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-DATUM | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-HATCH_LINHA | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-HATCH_MATERIAL | 0 | — | 0 | ByAci:253 | Continuous | off=False; frozen=False; locked=False; plot=False | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-MALHA | 0 | — | 0 | ByAci:253 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-SC-MALHA-TXT | 0 | — | 0 | ByAci:8 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-SC-NOTA | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-PROJECAO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-PROJETO | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-REGUA | 0 | — | 0 | ByAci:8 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-SAMPLE_LINE | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-SECAO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-SEC_TIPO | 0 | — | 0 | ByAci:150 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-SUPERFICIE | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-TABELA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-TABELA_DATA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-TABELA_DISCRIMINA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-SC-TABELA_ESTACA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-SC-TERRENO | 0 | — | 0 | ByAci:1 | HIDDEN | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-CAIXA | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| F1-VT-COTAS | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-COTAS-PROJETO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-COTAS-TERRENO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-ESTACA | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-VT-GRAFICO-SUPERELEVACAO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-GREIDE | 0 | — | 0 | ByAci:6 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-MALHA | 0 | — | 0 | ByAci:8 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-VT-NOTAVEL | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-VT-PLANIMETRIA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-PROJECAO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-REGUA | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-SC-TEXTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-VT-SEC-Nº DO PONTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-TERRENO | 0 | — | 0 | ByAci:3 | HIDDEN | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-TEXTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-VT-TITULO | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-TXT-SUPERELEVACAO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| F1-VT-VIEW | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| FL_Tabela de Terraplenagem | 0 | — | 2 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| FOLHA | 3 | AcDbPolyline:3 | 0 | ByAci:7 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Alinhamento | 38 | AcDbPolyline:38 | 1 | ByAci:140 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Boca de Lobo | 4 | AcDbPolyline:4 | 3 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Corrego | 0 | — | 0 | ByAci:130 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Cx Incêndio | 0 | — | 1 | ByAci:30 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Estrutura Civil | 3 | AcDbPolyline:3 | 0 | ByAci:140 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Grelha | 2 | AcDbPolyline:2 | 3 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Guia_Sargeta | 12 | AcDbPolyline:12 | 4 | ByAci:2 | DASHED | off=False; frozen=False; locked=False; plot=True | Feição de contexto/infraestrutura | — | TÉCNICA/MISTA |
| GF-Hidrante | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Lago | 2 | AcDbPolyline:2 | 1 | ByAci:160 | Continuous | off=False; frozen=False; locked=False; plot=True | Feição de contexto/infraestrutura | — | TÉCNICA/MISTA |
| GF-Linha de chamado | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Luminária | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Malha de Coordenada | 126 | AcDbLine:42, AcDbText:84 | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Poste | 12 | AcDbBlockReference:12 | 1 | ByAci:8 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PV Fibra Op | 0 | — | 1 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PV NI | 0 | — | 1 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PV Tel | 0 | — | 1 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVAPL | 0 | — | 2 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVELT | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVESA | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVESI | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVGAS | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Rede de Incêndio | 0 | — | 1 | ByAci:30 | LINHA | off=False; frozen=False; locked=False; plot=True | Rede Incêndio | INCENDIO | TÉCNICA |
| GF-Rede Eletrica | 1 | AcDbPolyline:1 | 2 | ByAci:10 | ELETRICA2X | off=False; frozen=False; locked=False; plot=True | Rede Elétrica | ELETRICA | TÉCNICA |
| GF-Rede Esgoto | 0 | — | 3 | ByAci:32 | DIVIDE2 | off=False; frozen=False; locked=False; plot=True | Rede Esgoto sanitário | ESGOTO_SANITARIO | TÉCNICA |
| GF-Rede Esgoto Industrial | 0 | — | 3 | ByAci:210 | ACAD_ISO14W100 | off=False; frozen=False; locked=False; plot=True | Rede Esgoto industrial | ESGOTO_INDUSTRIAL | TÉCNICA |
| GF-Rede Fibra Optica | 0 | — | 2 | ByAci:226 | FENCELINE2 | off=False; frozen=False; locked=False; plot=True | Rede Fibra óptica | FIBRA_OPTICA | TÉCNICA |
| GF-Rede Gás | 0 | — | 2 | ByAci:86 | FENCELINE1 | off=False; frozen=False; locked=False; plot=True | Rede Gás | GAS | TÉCNICA |
| GF-Rede Pluvial | 37 | AcDbPolyline:22, AcDbText:15 | 3 | ByAci:104 | GALERIA2 | off=False; frozen=False; locked=False; plot=True | Rede Águas pluviais | PLUVIAL | TÉCNICA |
| GF-Rede Pluvial-VIDEO | 14 | AcDbPolyline:8, AcDbText:6 | 3 | ByAci:220 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Representação/intervalo de vídeo-inspeção pluvial | PLUVIAL | MISTA |
| GF-Rede Pluvial_ | 0 | — | 1 | ByAci:104 | GALERIA2 | off=False; frozen=False; locked=False; plot=True | Anotação pluvial em layer variante | PLUVIAL | REVISÃO NECESSÁRIA |
| GF-Rede Sem Descrição de Uso | 48 | AcDbPolyline:15, AcDbText:33 | 2 | ByAci:7 | NAO_IDENTIF | off=False; frozen=False; locked=False; plot=True | Rede Sem descrição de uso | DESCONHECIDA | TÉCNICA |
| GF-Rede Telefone | 0 | — | 2 | ByAci:134 | TELESP | off=False; frozen=False; locked=False; plot=True | Rede Telefone | TELEFONE | TÉCNICA |
| GF-Rede Água | 1 | AcDbBlockReference:1 | 2 | ByAci:170 | BORDER2 | off=False; frozen=False; locked=False; plot=True | Rede Água potável | AGUA_POTAVEL | TÉCNICA |
| GF-Registro de Água | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Texto | 0 | — | 2 | ByAci:2 | DASHED | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| H2-BACIAS | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-DISPOSITIVO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-DISPOSITIVO-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-ESTUDO | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-ESTUDO-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-HZ-HATCH | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-HZ-REDE | 0 | — | 0 | ByAci:5 | DASHED | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-HZ-REDE-PV | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| H2-HZ-TEXTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| H2-PONTO-ALTO | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-PONTO-BAIXO | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-TEXTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| H2-TUBO | 0 | — | 0 | ByAci:3 | HIDDEN | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-TUBO-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-VT-HATCH | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-VT-REDE | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| H2-VT-RÉGUA | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-BASE | 0 | — | 0 | ByAci:171 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-BASE_HA | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-BORDO_PAV | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-CALÇADA | 0 | — | 0 | ByAci:171 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-CALÇADA_HA | 0 | — | 0 | ByAci:24 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MEIOFIO | 0 | — | 0 | ByAci:171 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MEIOFIO_HA | 0 | — | 0 | ByAci:9 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-BGS | 0 | — | 0 | ByAci:30 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-BGTC | 0 | — | 0 | ByAci:22 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-DEMOLICAO | 0 | — | 0 | ByAci:160 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-FRESAGEM | 0 | — | 0 | ByAci:220 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-GEOGRELHA | 0 | — | 0 | ByAci:250 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| I2-MO-IMPRIMADURA-IMPERMEABILIZANTE | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-IMPRIMADURA-LIGANTE | 0 | — | 0 | ByAci:255 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-MAT-ASF | 0 | — | 0 | ByAci:251 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-MSECO | 0 | — | 0 | ByAci:94 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-PREPARO-PAVIMENTO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-REF-SOLO | 0 | — | 0 | ByAci:161 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-REVESTIMENTO-PRIMARIO | 0 | — | 0 | ByAci:9 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-SOLO-CAL | 0 | — | 0 | ByAci:207 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-SOLO-CIM | 0 | — | 0 | ByAci:51 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-SOLO-MLH-CAL | 0 | — | 0 | ByAci:62 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-MO-SOLO-MLH-CIM | 0 | — | 0 | ByAci:55 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-PAV | 0 | — | 0 | ByAci:171 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-PAV_HA | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-SUBBASE | 0 | — | 0 | ByAci:171 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| I2-SUBBASE_HA | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-ABRAÇADEIRA | 0 | — | 0 | ByAci:251 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-ATENUADOR | 0 | — | 0 | ByAci:20 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-ATENUADOR-RET | 0 | — | 0 | ByAci:160 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-BARREIRA | 0 | — | 0 | ByAci:253 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-BARREIRA-RET | 0 | — | 0 | ByAci:160 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-DEFENSA-RET | 0 | — | 0 | ByAci:160 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-DEFENSA_CHAPA | 0 | — | 0 | ByAci:251 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-DEFENSA_POSTE | 0 | — | 0 | ByAci:250 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| J1-MO-FAIXA_PLACAS-BR | 0 | — | 0 | ByAci:255 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-FAIXA_PLACAS-PT | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-FAIXA_PLACAS-VM | 0 | — | 0 | ByAci:10 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-PINTURA-AMARELA | 0 | — | 0 | ByAci:40 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-PINTURA-BRANCA | 0 | — | 0 | ByAci:255 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-PINTURA-PRETA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-PINTURA-VERMELHA | 0 | — | 0 | ByAci:10 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-PLACA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-PLACA-RET | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-POSTE | 0 | — | 0 | ByAci:250 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| J1-MO-TACHA-AMARELA | 0 | — | 0 | ByAci:40 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| J1-MO-TACHA-BRANCA | 0 | — | 0 | ByAci:255 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| LOGO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-CADASTRO | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-CADASTRO-TXT | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-CONSTRUCAO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-LIMITES | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-LIMITES-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-TABELA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-ÁREA-INSTITUCIONAL | 0 | — | 0 | ByAci:41 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Q1-ÁREA_VERDE | 0 | — | 0 | ByAci:82 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| SIGA-TEXTO | 0 | — | 8 | ByAci:2 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| T-AP-POLIGONAL | 0 | — | 0 | ByAci:20 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| T-DI-FIGURA TOPOGRÁFICA | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| T-HM-COTAS-TXT | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| T-HM-PTO-TOPO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-DIAG-MASSA-VIEW-GRID-MINR | 0 | — | 0 | ByAci:254 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-DIAG-MASSA-VIEW-TEXT | 0 | — | 0 | ByAci:11 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-DIAG-MASSA-VIEW-TITL | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-DIAG-MASSA-VIEW-TTLB | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-DIAGRAMA_MASSA_VIEW | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-ORDENADAMASSA | 0 | — | 0 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-PLATO | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-PLATO-TXT | 0 | — | 0 | ByAci:5 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-SITE | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7-TABELA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| X7DIAG-MASSA-VIEW-GRID-MAJR | 0 | — | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Z9-VT-ENCONTRO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Z9-VT-ESTRUTURA_GENÉRICA | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Z9-VT-FUNDAÇÃO | 0 | — | 0 | ByAci:2 | HIDDEN | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Z9-VT-PILAR | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Z9-VT-PLATAFORMA | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| Z9-VT-TEXTO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| Z9-VT-VIGA | 0 | — | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| _TG | 271 | AcDbBlockReference:271 | 0 | ByAci:7 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Pontos levantados com atributos PONTO/DESC/COTA | MÚLTIPLA | TÉCNICA |

### GF-02-EP01-22-530.26-001.dwg

| Layer | Model | Tipos Model | Paper | Cor | Linetype | Estado | Significado | Rede | Classe |
|---|---|---|---|---|---|---|---|---|---|
| -Texto | 39 | AcDbText:39 | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| 0 | 0 | — | 11 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| 0_FORMATO | 0 | — | 31 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| A-FORMATO | 0 | — | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-EIXO | 19 | AcDbPolyline:19 | 1 | ByAci:2 | DASHDOT | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-ESTACA | 58 | AcDbBlockReference:29, AcDbMText:29 | 0 | ByAci:3 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| F1-HZ-NOTAVEL | 175 | AcDbBlockReference:39, AcDbLine:21, AcDbMText:89, AcDbPolyline:26 | 0 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| FL_Tabela de Terraplenagem | 0 | — | 2 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| FOLHA | 4 | AcDbPolyline:4 | 0 | ByAci:7 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| FOLHA 2 | 26 | AcDbText:26 | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Alinhamento | 81 | AcDbPolyline:81 | 1 | ByAci:140 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Boca de Lobo | 5 | AcDbPolyline:5 | 3 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Canaleta | 0 | — | 0 | ByAci:96 | DASHED | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Cerca | 0 | — | 0 | ByAci:1 | CERCA_DE_ARAME | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Cx Incêndio | 0 | — | 1 | ByAci:30 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Estrutura Civil | 0 | — | 0 | ByAci:6 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Galeria | 4 | AcDbPolyline:4 | 0 | ByAci:119 | GALERIA2 | off=False; frozen=False; locked=False; plot=True | Feição de contexto/infraestrutura | — | TÉCNICA/MISTA |
| GF-Grelha | 9 | AcDbHatch:4, AcDbPolyline:5 | 3 | ByAci:2 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Guia_Sargeta | 18 | AcDbPolyline:18 | 4 | ByAci:2 | DASHED | off=False; frozen=False; locked=False; plot=True | Feição de contexto/infraestrutura | — | TÉCNICA/MISTA |
| GF-Hidrante | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Lago | 3 | AcDbPolyline:3 | 0 | ByAci:130 | Continuous | off=False; frozen=False; locked=False; plot=True | Feição de contexto/infraestrutura | — | TÉCNICA/MISTA |
| GF-Linha de chamado | 4 | AcDbPolyline:4 | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Luminária | 0 | — | 1 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Malha de Coordenada | 132 | AcDbLine:44, AcDbText:88 | 0 | ByAci:252 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Mureta | 0 | — | 0 | ByAci:251 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Muro | 0 | — | 0 | ByAci:1 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Norte Magnético | 4 | AcDbBlockReference:4 | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Passeio | 0 | — | 0 | ByAci:252 | HIDDEN | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Poste | 19 | AcDbBlockReference:19 | 1 | ByAci:8 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PV Fibra Op | 0 | — | 1 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PV NI | 0 | — | 1 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PV Tel | 0 | — | 1 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVAPL | 0 | — | 2 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVELT | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVESA | 3 | AcDbBlockReference:3 | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVESI | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-PVGAS | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Rede de Incêndio | 0 | — | 1 | ByAci:30 | LINHA | off=False; frozen=False; locked=False; plot=True | Rede Incêndio | INCENDIO | TÉCNICA |
| GF-Rede Eletrica | 5 | AcDbPolyline:5 | 2 | ByAci:10 | ELETRICA2X | off=False; frozen=False; locked=False; plot=True | Rede Elétrica | ELETRICA | TÉCNICA |
| GF-Rede Esgoto | 85 | AcDbBlockReference:23, AcDbPolyline:17, AcDbText:45 | 3 | ByAci:32 | DIVIDE2 | off=False; frozen=False; locked=False; plot=True | Rede Esgoto sanitário | ESGOTO_SANITARIO | TÉCNICA |
| GF-Rede Esgoto Industrial | 0 | — | 3 | ByAci:210 | ACAD_ISO14W100 | off=False; frozen=False; locked=False; plot=True | Rede Esgoto industrial | ESGOTO_INDUSTRIAL | TÉCNICA |
| GF-Rede Fibra Optica | 0 | — | 2 | ByAci:226 | FENCELINE2 | off=False; frozen=False; locked=False; plot=True | Rede Fibra óptica | FIBRA_OPTICA | TÉCNICA |
| GF-Rede Gás | 0 | — | 2 | ByAci:86 | FENCELINE1 | off=False; frozen=False; locked=False; plot=True | Rede Gás | GAS | TÉCNICA |
| GF-Rede Pluvial | 107 | AcDbPolyline:62, AcDbText:45 | 2 | ByAci:104 | GALERIA2 | off=False; frozen=False; locked=False; plot=True | Rede Águas pluviais | PLUVIAL | TÉCNICA |
| GF-Rede Pluvial-VIDEO | 52 | AcDbPolyline:29, AcDbText:23 | 4 | ByAci:220 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Representação/intervalo de vídeo-inspeção pluvial | PLUVIAL | MISTA |
| GF-Rede Pluvial_ | 3 | AcDbText:3 | 1 | ByAci:104 | GALERIA2 | off=False; frozen=False; locked=False; plot=True | Anotação pluvial em layer variante | PLUVIAL | REVISÃO NECESSÁRIA |
| GF-Rede Sem Descrição de Uso | 90 | AcDbPolyline:25, AcDbText:65 | 2 | ByAci:7 | NAO_IDENTIF | off=False; frozen=False; locked=False; plot=True | Rede Sem descrição de uso | DESCONHECIDA | TÉCNICA |
| GF-Rede Telefone | 0 | — | 2 | ByAci:134 | TELESP | off=False; frozen=False; locked=False; plot=True | Rede Telefone | TELEFONE | TÉCNICA |
| GF-Rede Água | 3 | AcDbPolyline:1, AcDbText:2 | 2 | ByAci:170 | BORDER2 | off=False; frozen=False; locked=False; plot=True | Rede Água potável | AGUA_POTAVEL | TÉCNICA |
| GF-Registro de Água | 0 | — | 1 | ByAci:4 | Continuous | off=False; frozen=False; locked=False; plot=True | Estrutura/dispositivo ou sua representação | — | TÉCNICA/MISTA |
| GF-Talude | 0 | — | 0 | ByAci:36 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| GF-Texto | 0 | — | 2 | ByAci:2 | DASHED | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| GF-Árvores | 0 | — | 0 | ByAci:3 | AUDIT_I_240315075054-0 | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| HATCH-ASF | 0 | — | 0 | ByAci:254 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| LOGO | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| paisajismo | 0 | — | 0 | ByAci:7 | Continuous | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| SIGA-TEXTO | 0 | — | 8 | ByAci:2 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| TOPO-AL-DIVISA | 0 | — | 0 | ByAci:6 | TRACEJADO | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| TOPO-HATCH-ASFALTO | 0 | — | 0 | ByAci:253 | CERCA | off=False; frozen=False; locked=False; plot=True | Significado não determinado automaticamente | — | REVISÃO NECESSÁRIA |
| TOPO-TEXTO | 0 | — | 0 | ByAci:7 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Apresentação, referência ou anotação CAD | — | GRÁFICA |
| _TG | 374 | AcDbBlockReference:374 | 0 | ByAci:7 | CONTINUO | off=False; frozen=False; locked=False; plot=True | Pontos levantados com atributos PONTO/DESC/COTA | MÚLTIPLA | TÉCNICA |

## 6. Redes identificadas

A legenda confirma o catálogo corporativo, mas ocorrência física foi reconhecida somente quando há geometria no Model Space.

| Código | Rótulo da legenda | Layer amostra | Nos dois desenhos |
|---|---|---|---|
| PLUVIAL | REDE ÁGUAS PLUVIAIS | GF-Rede Pluvial | SIM |
| ESGOTO_SANITARIO | REDE ESGOTO SANITÁRIO | GF-Rede Esgoto | SIM |
| ESGOTO_INDUSTRIAL | REDE ESGOTO INDUSTRIAL | GF-Rede Esgoto Industrial | SIM |
| ELETRICA | REDE ELÉTRICA | GF-Rede Eletrica | SIM |
| AGUA_POTAVEL | REDE ÁGUA POTÁVEL | GF-Rede Água | SIM |
| GAS | REDE GÁS | GF-Rede Gás | SIM |
| TELEFONE | REDE DE TELEFONE | GF-Rede Telefone | SIM |
| FIBRA_OPTICA | REDE DE FIBRA ÓPTICA | GF-Rede Fibra Optica | SIM |
| INCENDIO | REDE DE INCÊNDIO | GF-Rede de Incêndio | SIM |
| DESCONHECIDA | REDE S/ DESCRIÇÃO DE USO | GF-Rede Sem Descrição de Uso | SIM |

### Padrões observados — GF-02-EP01-18-530.26-001.dwg

| Layer | Tipo | Papel | Abertas | Únicas XY | Duplicadas | Fechadas | Textos | Z | Confiança |
|---|---|---|---|---|---|---|---|---|---|
| GF-Rede Pluvial | PLUVIAL | REDE_FISICA | 18 | 18 | 0 | 4 | 15 | 8.076…9.655 | ALTA |
| GF-Rede Eletrica | ELETRICA | REDE_FISICA | 0 | 0 | 0 | 1 | 0 | — | ALTA |
| GF-Rede Água | AGUA_POTAVEL | REDE_FISICA | 0 | 0 | 0 | 0 | 0 | — | ALTA |
| GF-Rede Sem Descrição de Uso | DESCONHECIDA | REDE_FISICA | 15 | 15 | 0 | 0 | 33 | 7.848…8.966 | ALTA |
| GF-Rede Pluvial-VIDEO | PLUVIAL | COBERTURA/REPRESENTAÇÃO_DE_VÍDEO_INSPEÇÃO | 6 | 6 | 0 | 2 | 6 | 7.429…17.003 | MÉDIA |

### Padrões observados — GF-02-EP01-22-530.26-001.dwg

| Layer | Tipo | Papel | Abertas | Únicas XY | Duplicadas | Fechadas | Textos | Z | Confiança |
|---|---|---|---|---|---|---|---|---|---|
| GF-Rede Pluvial | PLUVIAL | REDE_FISICA | 40 | 40 | 0 | 22 | 45 | 13.881…17.004 | ALTA |
| GF-Rede Esgoto | ESGOTO_SANITARIO | REDE_FISICA | 17 | 17 | 0 | 0 | 45 | 14.094…16.963 | ALTA |
| GF-Rede Eletrica | ELETRICA | REDE_FISICA | 0 | 0 | 0 | 5 | 0 | — | ALTA |
| GF-Rede Água | AGUA_POTAVEL | REDE_FISICA | 1 | 1 | 0 | 0 | 2 | 14.002…14.002 | ALTA |
| GF-Rede Sem Descrição de Uso | DESCONHECIDA | REDE_FISICA | 25 | 25 | 0 | 0 | 65 | 14.307…784.053 | ALTA |
| GF-Rede Pluvial-VIDEO | PLUVIAL | COBERTURA/REPRESENTAÇÃO_DE_VÍDEO_INSPEÇÃO | 24 | 24 | 0 | 5 | 23 | 0.000…32.319 | MÉDIA |
| GF-Rede Pluvial_ | PLUVIAL | ANOTAÇÃO | 0 | 0 | 0 | 0 | 3 | — | BAIXA |
| GF-Galeria | REVISÃO | CONTORNO/REPRESENTAÇÃO_DE_GALERIA | 4 | 4 | 0 | 0 | 0 | 10.728…12.460 | MÉDIA |

Regra recomendada: somente `AcDbPolyline` aberta em layer de rede é candidata inicial a trecho. Polyline fechada no mesmo layer é candidata a estrutura/contorno. `GF-Rede Pluvial-VIDEO` representa cobertura/contorno de inspeção e não uma rede física adicional.

## 7. Estruturas identificadas

### GF-02-EP01-18-530.26-001.dwg

| Estrutura | Tipo CAD | Bloco | Layers | Quantidade | Coordenada | Elevação | Confiança |
|---|---|---|---|---|---|---|---|
| POSTE | AcDbBlockReference | POSTE | GF-Poste | 12 | BlockReference.Position | BlockReference.Position.Z (sem validação semântica) | ALTA |
| REGISTRO_AGUA_POTAVEL | AcDbBlockReference | rg_agua_potavel | GF-Rede Água | 1 | BlockReference.Position | BlockReference.Position.Z (sem validação semântica) | ALTA |
| BOCA_DE_LOBO | AcDbPolyline fechada | — | GF-Boca de Lobo | 4 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | ALTA |
| GRELHA | AcDbPolyline fechada | — | GF-Grelha | 2 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | ALTA |
| ESTRUTURA_PLUVIAL_NAO_CLASSIFICADA | AcDbPolyline fechada | — | GF-Rede Pluvial | 4 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | MÉDIA |
| ESTRUTURA_PLUVIAL_VIDEO | AcDbPolyline fechada | — | GF-Rede Pluvial-VIDEO | 2 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | MÉDIA |
| DISPOSITIVO_ELETRICO_NAO_CLASSIFICADO | AcDbPolyline fechada | — | GF-Rede Eletrica | 1 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | MÉDIA |

### GF-02-EP01-22-530.26-001.dwg

| Estrutura | Tipo CAD | Bloco | Layers | Quantidade | Coordenada | Elevação | Confiança |
|---|---|---|---|---|---|---|---|
| POSTE | AcDbBlockReference | POSTE | GF-Poste | 19 | BlockReference.Position | BlockReference.Position.Z (sem validação semântica) | ALTA |
| PV_ESGOTO_SANITARIO | AcDbBlockReference | PV ESA | GF-PVESA | 3 | BlockReference.Position | BlockReference.Position.Z (sem validação semântica) | ALTA |
| PV_ESGOTO_SANITARIO | AcDbBlockReference | pv_esgoto_sanitario | GF-Rede Esgoto | 7 | BlockReference.Position | BlockReference.Position.Z (sem validação semântica) | ALTA |
| BOCA_DE_LOBO | AcDbPolyline fechada | — | GF-Boca de Lobo | 5 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | ALTA |
| GRELHA | AcDbPolyline fechada | — | GF-Grelha | 5 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | ALTA |
| ESTRUTURA_PLUVIAL_NAO_CLASSIFICADA | AcDbPolyline fechada | — | GF-Rede Pluvial | 22 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | MÉDIA |
| ESTRUTURA_PLUVIAL_VIDEO | AcDbPolyline fechada | — | GF-Rede Pluvial-VIDEO | 5 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | MÉDIA |
| DISPOSITIVO_ELETRICO_NAO_CLASSIFICADO | AcDbPolyline fechada | — | GF-Rede Eletrica | 5 | Centroide/footprint da polyline | Polyline.Elevation (sem validação semântica) | MÉDIA |
| GRELHA_HACHURA | AcDbHatch | — | GF-Grelha | 4 | — | — | ALTA |

Polylines fechadas de `GF-Rede Pluvial` e `GF-Rede Eletrica` não foram classificadas definitivamente como PV/caixa: a forma e o layer indicam estrutura, mas a classe exige revisão humana.

## 8. Blocos

### GF-02-EP01-18-530.26-001.dwg

| Bloco | Layer | Inserções | Atributos | Função | Classe |
|---|---|---|---|---|---|
| SIMB_PONTO | _TG | 271 | COTA, DESC, PONTO | Ponto levantado com código, descrição e cota | TÉCNICO |
| POSTE | GF-Poste | 12 | — | Estrutura/dispositivo do mundo real | TÉCNICO |
| AeccTickCircle | F1-HZ-NOTAVEL | 10 | — | Símbolo de anotação Civil 3D/norte | GRÁFICO |
| AeccTickLine | F1-HZ-ESTACA | 8 | — | Símbolo de anotação Civil 3D/norte | GRÁFICO |
| rg_agua_potavel | GF-Rede Água | 1 | — | Estrutura/dispositivo do mundo real | TÉCNICO |

### GF-02-EP01-22-530.26-001.dwg

| Bloco | Layer | Inserções | Atributos | Função | Classe |
|---|---|---|---|---|---|
| SIMB_PONTO | _TG | 374 | COTA, DESC, PONTO | Ponto levantado com código, descrição e cota | TÉCNICO |
| AeccTickCircle | F1-HZ-NOTAVEL | 39 | — | Símbolo de anotação Civil 3D/norte | GRÁFICO |
| AeccTickLine | F1-HZ-ESTACA | 29 | — | Símbolo de anotação Civil 3D/norte | GRÁFICO |
| POSTE | GF-Poste | 19 | — | Estrutura/dispositivo do mundo real | TÉCNICO |
| SENTIDO | GF-Rede Esgoto | 16 | — | Seta de sentido; representação de possível direção de fluxo | GRÁFICO COM SEMÂNTICA |
| pv_esgoto_sanitario | GF-Rede Esgoto | 7 | — | Estrutura/dispositivo do mundo real | TÉCNICO |
| A$C32216DAA | GF-Norte Magnético | 4 | — | Símbolo de anotação Civil 3D/norte | GRÁFICO |
| PV ESA | GF-PVESA | 3 | — | Estrutura/dispositivo do mundo real | TÉCNICO |

`SIMB_PONTO` é o bloco técnico mais importante: contém `PONTO`, `DESC` e `COTA`. `POSTE`, `PV ESA`, `pv_esgoto_sanitario` e `rg_agua_potavel` representam objetos reais. `SENTIDO`, ticks Civil 3D, norte e folha são representação CAD; sua semântica pode alimentar atributos, mas os símbolos não viram entidades GIS independentes.

## 9. Textos e atributos

### GF-02-EP01-18-530.26-001.dwg

| Padrão | Quantidade | Exemplos | Layers | Significado | Campo | Confiança |
|---|---|---|---|---|---|---|
| CONDICAO/DEFEITO | 4 | JUNTAS DESLOCADAS E DESGASTAS COM TRINCOS E ABAULAMENTO; ABAULAMENTO NA G.S. COM ACUMULO DE SEDIMENTOS; JUNTAS DESLOCADAS E DESGASTAS COM INFILTRAÇÃO POR TODO TRECHO | F1-HZ-NOTAVEL | Condição observada ou defeito | condition_raw | MÉDIA |
| DIAMETRO | 5 | ∅400; ∅150; ∅1000 | GF-Rede Pluvial, GF-Rede Pluvial-VIDEO | Diâmetro em milímetros | diameter_mm | ALTA |
| ESTACA_VIDEO | 10 | INÍCIO=EST.0+0,000; PI=EST.0+8,000; PI=EST.0+10,000; PI=EST.1+4,000; FINAL=EST.1+17,042; FINAL=EST.0+19,000; PI=EST.0+19,000; FINAL=EST.1+4,000 | F1-HZ-NOTAVEL | Estaca/posição ao longo de vídeo-inspeção | inspection_station | ALTA |
| MALHA_EASTING | 40 | E(X)=719900; E(X)=719925; E(X)=719950; E(X)=719975; E(X)=720000; E(X)=720025; E(X)=720050; E(X)=720075; E(X)=720100; E(X)=720125 | GF-Malha de Coordenada | Rótulo gráfico da malha UTM | — | ALTA |
| MALHA_NORTHING | 44 | N(Y)=7084675; N(Y)=7084700; N(Y)=7084725; N(Y)=7084750; N(Y)=7084775; N(Y)=7084800; N(Y)=7084825; N(Y)=7084850; N(Y)=7084875; N(Y)=7084900; N(Y)=7084925; N(Y)=7084950 | GF-Malha de Coordenada | Rótulo gráfico da malha UTM | — | ALTA |
| NUMERO_PREDIAL | 23 | N°244; N°80; N°84; N°102; N°118; N°130; N°142; N°154; N°166; N°178; N°190; N°212 | -Texto | Número predial/contexto cartográfico | address_number | MÉDIA |
| OUTRO | 23 | SN; 0; 1; 1+17; 0+19; 1+4; CONTRIBUIÇÃO LATERAL; CAIXA DE PASSAGEM COM AUMENTO DA LÂMINA D'ÁGUA; CONTRIBUIÇÃO SUPERIOR; CAIXA DE PASSAGEM; RUA RENATO CAETANO DA SILVA FILHO; G.S.:7.429 E G.I.:5.929 | -Texto, F1-HZ-ESTACA, F1-HZ-NOTAVEL | Anotação diversa; revisão por subgrupo | — | BAIXA |
| PROFUNDIDADE | 46 | P.0.27; P.0.55; P.0.73; P.0.88; P.0.75; P.0.83; P.0.32; P.0.58; P.0.38; P.0.98; P.1.05; P.0.39 | GF-Rede Pluvial, GF-Rede Sem Descrição de Uso | Profundidade em metros; a legenda define geratriz superior | depth_m | ALTA |
| TRECHO_VIDEO | 3 | TRECHO 01; TRECHO 03; TRECHO 02 | GF-Rede Pluvial-VIDEO | Identificador de trecho de vídeo-inspeção | inspection_segment_code | ALTA |

Pontos `SIMB_PONTO`: **271**; códigos de rede/profundidade parseáveis: **98 (36.2%)**; com diâmetro: **6**; registros GEOAUTOENGINE existentes: **46**.

### GF-02-EP01-22-530.26-001.dwg

| Padrão | Quantidade | Exemplos | Layers | Significado | Campo | Confiança |
|---|---|---|---|---|---|---|
| CONDICAO/DEFEITO | 16 | ASSOREADO; LACRADO; JUNTAS DANIFICADAS E DESLOCADA COM ACUMULO DE SEDIMENTOS POR TODO TRECHO; REDE SEDIMENTADA; JUNTAS DESLOCADAS E DANIFICADAS POR TODO TRECHO; CAIXA DE PASSAGEM COM ACUMULO DE SEDIMENTOS; JUNTAS DANIFICADAS E DESLOCADAS POR TODO TRECHO; JUNTAS DANIFICAS, DESLOCADAS COM TRINCOS E ACUMULO DE SEDIMENTO POR TODO TRECHO; JUNTAS DANIFICADAS E DESLOCADAS COM TRINCAS E ACUMULO DE SEDIMENTO POR TODO TRECHO; ACUMULO DE SEDIMENTOS; ACUMULO DE SEDIMENTO | -Texto, F1-HZ-NOTAVEL, GF-Rede Pluvial-VIDEO | Condição observada ou defeito | condition_raw | MÉDIA |
| DIAMETRO | 30 | ∅800; ∅150; ∅1500; ∅600; ∅250; ∅200; ∅400 | GF-Rede Esgoto, GF-Rede Pluvial, GF-Rede Pluvial-VIDEO | Diâmetro em milímetros | diameter_mm | ALTA |
| ESTACA_VIDEO | 40 | INÍCIO=EST.0+0,000; PI=EST.0+3,000; PI=EST.0+6,000; FINAL=EST.1+1,000; FINAL=EST.0+6,000; FINAL=EST.0+2,000; FINAL=EST.1+13,000; PI=EST.0+9,000; PI=EST.1+1,000; PI=EST.1+9,000; PI=EST.1+10,000; PI=EST.2+1,000 | F1-HZ-NOTAVEL | Estaca/posição ao longo de vídeo-inspeção | inspection_station | ALTA |
| MALHA_EASTING | 38 | E(X)=718250; E(X)=718275; E(X)=718300; E(X)=718325; E(X)=718350; E(X)=718375; E(X)=718400; E(X)=718425; E(X)=718450; E(X)=718475; E(X)=718500; E(X)=718525 | GF-Malha de Coordenada | Rótulo gráfico da malha UTM | — | ALTA |
| MALHA_NORTHING | 50 | N(Y)=7082300; N(Y)=7082325; N(Y)=7082350; N(Y)=7082375; N(Y)=7082400; N(Y)=7082425; N(Y)=7082225; N(Y)=7082250; N(Y)=7082275; N(Y)=7082075; N(Y)=7082100; N(Y)=7082125 | GF-Malha de Coordenada | Rótulo gráfico da malha UTM | — | ALTA |
| NUMERO_PREDIAL | 50 | N°36; N°48; N°58; N°62; N°72; N°84; N°96; N°130; N°176; N°166; N°178; N°188 | -Texto, FOLHA 2 | Número predial/contexto cartográfico | address_number | MÉDIA |
| OUTRO | 83 | SN; RUA SÃO FRANCISCO DE ASSIS; RUA BOEHMERWALD; %%C250; %%C150; 0; 0+16; 1; 1+1; 0+6; 0+2; 1+13 | -Texto, F1-HZ-ESTACA, F1-HZ-NOTAVEL, FOLHA 2, GF-Rede Esgoto, GF-Rede Pluvial | Anotação diversa; revisão por subgrupo | — | BAIXA |
| PROFUNDIDADE | 135 | P.0.84; P.1.40; P.1.07; P.1.06; P.0.83; P.0.88; P.0.67; P.1.78; P.1.09; P.1.55; P.1.85; P.0.73 | GF-Rede Esgoto, GF-Rede Pluvial, GF-Rede Pluvial_, GF-Rede Sem Descrição de Uso, GF-Rede Água | Profundidade em metros; a legenda define geratriz superior | depth_m | ALTA |
| TRECHO_VIDEO | 12 | TRECHO 01; TRECHO 02; TRECHO 03; TRECHO 04; TRECHO 05 - GALERIA; TRECHO 06 ∅1500; TRECHO 07; TRECHO 08; TRECHO 09; TRECHO 10; TRECHO 11; TRECHO 12 | GF-Rede Pluvial-VIDEO | Identificador de trecho de vídeo-inspeção | inspection_segment_code | ALTA |

Pontos `SIMB_PONTO`: **374**; códigos de rede/profundidade parseáveis: **303 (81.0%)**; com diâmetro: **51**; registros GEOAUTOENGINE existentes: **59**.

Nenhum termo inequívoco de material (PVC, PEAD, concreto, ferro fundido etc.) foi encontrado. `material` é **NÃO DISPONÍVEL NO DWG** nesta amostra.

## 10. Relacionamentos identificados

| DWG | Candidatos texto→trecho | Alta | Média | Baixa | Ambígua | Método |
|---|---|---|---|---|---|---|
| GF-02-EP01-18-530.26-001.dwg | 51 | 48 | 3 | 0 | 0 | XData existente + proximidade no mesmo layer |
| GF-02-EP01-22-530.26-001.dwg | 165 | 86 | 73 | 5 | 1 | XData existente + proximidade no mesmo layer |

Relações de alta confiança: atributos dentro de `SIMB_PONTO`; pares de textos GEOAUTOENGINE agrupados pelo mesmo `REGISTRO`; blocos técnicos por nome/layer. Relações somente espaciais continuam candidatas, mesmo quando a distância é pequena.

## 11. Topologia

### GF-02-EP01-18-530.26-001.dwg

| Layer | Fonte | Únicas | Duplicatas | Nós conectados | Ramificações | Pontas soltas | T em interior | Conflito Z | Gaps 5–50 cm |
|---|---|---|---|---|---|---|---|---|---|
| GF-Rede Pluvial | 18 | 18 | 0 | 0 | 0 | 36 | 4 | 0 | 0 |
| GF-Rede Pluvial-VIDEO | 6 | 6 | 0 | 0 | 0 | 12 | 0 | 0 | 2 |
| GF-Rede Sem Descrição de Uso | 15 | 15 | 0 | 2 | 0 | 26 | 0 | 2 | 1 |

### GF-02-EP01-22-530.26-001.dwg

| Layer | Fonte | Únicas | Duplicatas | Nós conectados | Ramificações | Pontas soltas | T em interior | Conflito Z | Gaps 5–50 cm |
|---|---|---|---|---|---|---|---|---|---|
| GF-Galeria | 4 | 4 | 0 | 0 | 0 | 8 | 0 | 0 | 0 |
| GF-Rede Esgoto | 17 | 17 | 0 | 1 | 0 | 32 | 0 | 1 | 11 |
| GF-Rede Pluvial | 40 | 40 | 0 | 9 | 0 | 62 | 0 | 3 | 0 |
| GF-Rede Pluvial-VIDEO | 24 | 24 | 0 | 0 | 0 | 48 | 0 | 0 | 1 |
| GF-Rede Sem Descrição de Uso | 25 | 25 | 0 | 10 | 0 | 30 | 1 | 4 | 0 |
| GF-Rede Água | 1 | 1 | 0 | 0 | 0 | 2 | 0 | 0 | 0 |

O diagnóstico usa XY e tolerância de 0,05 m, somente para apontar candidatos. Nenhuma duplicata XY exata foi detectada. Gaps e pontas soltas não foram corrigidos; uma ponta em estrutura ainda não vinculada pode aparecer como solta.

## 12. Dados técnicos aproveitáveis

- Geometria XY de polylines abertas, preservando handle e layer.
- Posições de blocos técnicos e footprints de polylines fechadas.
- Tipo de rede por layer, validado contra a legenda.
- Identificador, descrição e cota dos pontos `SIMB_PONTO`.
- Profundidade e diâmetro quando codificados em `DESC`, textos ou XData existente.
- Identificadores `TRECHO` e estaqueamento de vídeo-inspeção.
- Observações brutas de condição/defeito, sem taxonomia inventada.
- Proveniência: nome do DWG, SHA-256, handle, layer e confiança.

## 13. Elementos exclusivamente gráficos

- Objetos de legenda no Paper Space, carimbo `FOLHA A1_GEOFRAN`, viewports e OLE.
- Ticks `AeccTickCircle`/`AeccTickLine`, norte, molduras, malha e textos de prancha.
- Hachuras de grelha como feições independentes.
- Setas `SENTIDO` como geometria: apenas a direção inferida pode virar atributo após validação.
- Textos de profundidade/diâmetro após sua incorporação como atributos.
- Pares/contornos da vídeo-inspeção depois que forem vinculados ao trecho físico.

## 14. Problemas encontrados

- O DWG não possui sistema de coordenadas atribuído; a legenda informa SIRGAS 2000/UTM, mas não a zona.
- O primeiro DWG está com INSUNITS=Undefined, embora a legenda declare medidas em metros.
- Polylines abertas e fechadas coexistem em layers de rede; o layer isolado não distingue trecho de estrutura.
- Não foram detectadas duplicatas lineares XY exatas; GF-Rede Pluvial-VIDEO contém pares paralelos separados por aproximadamente 0,4–1,0 m.
- GF-Rede Pluvial-VIDEO contém pares paralelos/contornos e não deve virar uma segunda rede física.
- A semântica de Z é inconsistente: valores de rede podem superar cotas próximas; não tratar Z como invert/cota de fundo automaticamente.
- Códigos/sufixos POSSIVEL, LAC, ENC, ASS, S e APAGAR precisam de dicionário corporativo antes de normalização.
- Textos de profundidade/diâmetro têm XData de registro em parte dos casos, mas o vínculo XData não identifica diretamente o trecho físico.
- Materiais de tubulação não foram encontrados nos textos, atributos ou legendas auditados.
- A legenda apresenta famílias (gás, telefone, fibra, incêndio, esgoto industrial) que não ocorrem como redes lineares no Model Space destes dois projetos.

## 15. Casos ambíguos

1. Polylines fechadas em layers de rede podem ser PV, caixas ou contornos gráficos.
2. `GF-Rede Pluvial-VIDEO` contém linhas paralelas e contornos; falta centerline inequívoca.
3. `GF-Rede Pluvial_` difere por sublinhado e contém apenas textos no segundo DWG.
4. `GF-Galeria` apresenta linhas de contorno, inclusive um elemento quase nulo, não um eixo único.
5. Valores Z não têm semântica uniforme e não podem ser chamados automaticamente de invert/cota de fundo.
6. Proximidade de texto a linha pode produzir mais de um candidato.
7. Redes listadas na legenda, mas ausentes no Model Space, são catálogo e não ocorrências.

## 16. Decisões humanas necessárias

1. Confirmar zona UTM/EPSG e precisão das coordenadas navegadas.
2. Aprovar dicionário de códigos e sufixos de campo.
3. Definir quais polylines fechadas em layers de rede são PV, caixas ou outros dispositivos.
4. Definir como consolidar os pares paralelos de GF-Rede Pluvial-VIDEO sem confundi-los com duplicatas.
5. Escolher/derivar centerline para os pares de linhas de GF-Rede Pluvial-VIDEO.
6. Validar Z e decidir se a primeira carga GIS será 2D ou 2D + geometria-fonte 3D.
7. Confirmar vínculos texto/ponto → trecho nos candidatos médios, baixos ou ambíguos.

## 17. Modelo conceitual recomendado

```text
PROJECT
├── SOURCE_DRAWING
├── NETWORK_TYPE_CATALOG   (derivado da legenda, sem geometria)
├── NETWORK_SEGMENT        (polyline aberta consolidada)
├── STRUCTURE              (bloco técnico ou footprint fechado)
└── MAPPING_OBSERVATION     (SIMB_PONTO/texto/XData)
    └── FEATURE_OBSERVATION_LINK (método, distância, confiança, revisão)
```

A camada de vídeo-inspeção deverá futuramente referenciar `NETWORK_SEGMENT` no schema `inspection`; não deve duplicar a tubulação em `mapping`.

## 18. Mapeamento CAD → GIS

| Informação técnica | Origem no DWG | Método | Destino GIS | Confiança |
|---|---|---|---|---|
| Tipo de rede | Layer + legenda | Tabela de regra explícita | network_segments.network_type | ALTA |
| Geometria de trecho | AcDbPolyline aberta | Vértices XY; deduplicação revisada | network_segments.geom | ALTA/MÉDIA |
| Geometria 3D fonte | Polyline.Elevation/Z | Preservar sem validar | network_segments.source_geom_3d | MÉDIA |
| Profundidade | DESC, texto P.x, XData | Parser + vínculo auditável | mapping_observations.depth_m | ALTA no ponto; variável no trecho |
| Diâmetro | D[n] e texto ∅n | Parser + vínculo auditável | mapping_observations.diameter_mm | ALTA no registro |
| Material | Não encontrado | Não extrair | Não criar inicialmente | N/A |
| Ponto levantado | SIMB_PONTO | Atributos + inserção | mapping_observations | ALTA |
| PV esgoto | PV ESA/pv_esgoto_sanitario | BlockReference | structures | ALTA |
| Registro de água | rg_agua_potavel | BlockReference | structures | ALTA |
| Poste | POSTE | BlockReference | structures | ALTA |
| Boca de lobo/grelha | Polyline fechada em layer dedicado | Centroide + footprint | structures | ALTA |
| Estrutura genérica | Polyline fechada em layer de rede | Classificação humana | structures | MÉDIA/BAIXA |
| Sentido | Bloco SENTIDO | Rotação + trecho próximo | Atributo futuro | MÉDIA |
| Vídeo-inspeção | GF-Rede Pluvial-VIDEO/TRECHO | Vínculo ao trecho físico | inspection (futuro) | MÉDIA |

## 19. Estrutura PostGIS recomendada

### `mapping.source_drawings`

Proveniência e rastreabilidade do DWG auditado.

| Campo | Tipo PostgreSQL/PostGIS | Obrigatoriedade | Origem |
|---|---|---|---|
| id | uuid | OBRIGATÓRIO | Gerado pelo GIS |
| project_id | uuid | OBRIGATÓRIO | Projeto |
| file_name | text | OBRIGATÓRIO | Nome do DWG |
| sha256 | char(64) | OBRIGATÓRIO | Hash do DWG |
| cad_version | text | OPCIONAL | Metadado DWG |
| coordinate_system_text | text | OPCIONAL | Legenda/metadados |
| srid | integer | OPCIONAL/BLOQUEADO | Confirmação humana |
| audited_at | timestamptz | OBRIGATÓRIO | Processo de auditoria |

### `mapping.network_type_catalog`

Domínio corporativo comprovado pelas duas legendas.

| Campo | Tipo PostgreSQL/PostGIS | Obrigatoriedade | Origem |
|---|---|---|---|
| code | text | OBRIGATÓRIO | Legenda |
| name | text | OBRIGATÓRIO | Legenda |
| legend_label | text | OBRIGATÓRIO | Texto literal |
| active | boolean | OBRIGATÓRIO | Governança |

### `mapping.network_segments`

Trechos físicos de rede consolidados, sem importar textos como feições autônomas.

| Campo | Tipo PostgreSQL/PostGIS | Obrigatoriedade | Origem |
|---|---|---|---|
| id | uuid | OBRIGATÓRIO | Gerado pelo GIS |
| project_id | uuid | OBRIGATÓRIO | Projeto |
| source_drawing_id | uuid | OBRIGATÓRIO | DWG de origem |
| network_type | text/FK | OBRIGATÓRIO | Layer + legenda |
| geom | geometry(LineString, SRID) | OBRIGATÓRIO | XY da polyline aberta |
| source_geom_3d | geometry(LineStringZ, SRID) | OPCIONAL | Vértices/Z CAD sem validação |
| diameter_mm | integer | OPCIONAL | Código/texto/XData |
| depth_start_m | numeric(8,3) | OPCIONAL | Observações associadas |
| depth_end_m | numeric(8,3) | OPCIONAL | Observações associadas |
| code | text | OPCIONAL | TRECHO/código quando inequívoco |
| condition_raw | text | OPCIONAL | Anotação preservada |
| source_layer | text | OBRIGATÓRIO | Layer CAD |
| source_handle | text | OBRIGATÓRIO | Handle CAD |
| extraction_confidence | text | OBRIGATÓRIO | Regra de extração |
| z_validated | boolean | OBRIGATÓRIO | Inicialmente false |

Não incluir inicialmente: material (não encontrado); fabricante; ano de instalação; estado operacional normalizado.

### `mapping.structures`

PV, bocas de lobo, grelhas, registros, postes e estruturas confirmadas.

| Campo | Tipo PostgreSQL/PostGIS | Obrigatoriedade | Origem |
|---|---|---|---|
| id | uuid | OBRIGATÓRIO | Gerado pelo GIS |
| project_id | uuid | OBRIGATÓRIO | Projeto |
| source_drawing_id | uuid | OBRIGATÓRIO | DWG de origem |
| structure_type | text | OBRIGATÓRIO | Bloco/layer/legenda |
| geom | geometry(Point, SRID) | OBRIGATÓRIO | Inserção/centroide |
| footprint | geometry(Polygon, SRID) | OPCIONAL | Polyline fechada |
| elevation_m | numeric(10,3) | OPCIONAL | Z/COTA validada |
| code | text | OPCIONAL | Código/atributo |
| condition_raw | text | OPCIONAL | LAC/ENC/ASS/etc. |
| source_layer | text | OBRIGATÓRIO | Layer CAD |
| source_handle | text | OBRIGATÓRIO | Handle CAD |
| extraction_confidence | text | OBRIGATÓRIO | Regra de extração |

### `mapping.mapping_observations`

Preservar pontos SIMB_PONTO e atributos técnicos sem projetá-los prematuramente sobre um trecho.

| Campo | Tipo PostgreSQL/PostGIS | Obrigatoriedade | Origem |
|---|---|---|---|
| id | uuid | OBRIGATÓRIO | Gerado pelo GIS |
| source_drawing_id | uuid | OBRIGATÓRIO | DWG de origem |
| point_name | text | OBRIGATÓRIO | Atributo PONTO |
| description_raw | text | OBRIGATÓRIO | Atributo DESC |
| network_type | text/FK | OPCIONAL | Prefixo interpretado |
| depth_m | numeric(8,3) | OPCIONAL | DESC/código |
| diameter_mm | integer | OPCIONAL | D[número] |
| elevation_m | numeric(10,3) | OPCIONAL | COTA/Position.Z |
| condition_raw | text | OPCIONAL | Sufixo/texto bruto |
| geom | geometry(PointZ, SRID) | OBRIGATÓRIO | BlockReference.Position |
| source_handle | text | OBRIGATÓRIO | Handle CAD |

### `mapping.feature_observation_links`

Vínculos auditáveis entre observações e trechos/estruturas, inclusive candidatos ambíguos.

| Campo | Tipo PostgreSQL/PostGIS | Obrigatoriedade | Origem |
|---|---|---|---|
| observation_id | uuid | OBRIGATÓRIO | Observação |
| network_segment_id | uuid | OPCIONAL | Trecho candidato |
| structure_id | uuid | OPCIONAL | Estrutura candidata |
| method | text | OBRIGATÓRIO | XData/proximidade/revisão |
| distance_m | numeric(10,4) | OPCIONAL | Associação espacial |
| confidence | text | OBRIGATÓRIO | Alta/média/baixa/ambígua |
| reviewed | boolean | OBRIGATÓRIO | Governança |

Nenhum `CREATE TABLE` foi executado.

## 20. Próxima etapa recomendada

1. Confirmar zona UTM/SRID e política 2D/3D.
2. Aprovar o catálogo de dez redes extraído da legenda e o dicionário de códigos/sufixos.
3. Revisar visualmente os pares paralelos de vídeo, polylines fechadas genéricas e gaps listados no JSON.
4. Construir um protótipo de importação para área de homologação, primeiro para `source_drawings`, `mapping_observations`, trechos pluviais/esgoto e estruturas confirmadas.
5. Validar amostras CAD↔GIS antes de modelar o schema `inspection` e a vídeo-inspeção.

Somente após essas decisões deve ser preparada a DDL para revisão; o banco não foi criado ou alterado nesta auditoria.
