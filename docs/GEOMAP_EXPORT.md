# Exportação de Polyline para o GeoMap

O comando `GEOAUTO3GEOMAPEXPORT` exporta uma rede do desenho ativo para o contrato GeoMap v0.1 (`*.geomap.json`). Ele está disponível no bundle do GeoAutoEngine para Civil 3D 2024, 2025 e 2026.

## Escopo desta versão

- entidades `Polyline` 2D (`LWPOLYLINE`), abertas ou fechadas;
- segmentos retos;
- até 10.000 Polylines por arquivo e 100.000 posições por feição;
- preservação de handle, layer, quantidade de vértices, fechamento e elevação CAD;
- coordenadas X/Y no CRS declarado pelo operador;
- unidades `metre`, `foot`, `us-survey-foot` ou `degree`.

`Polyline2d`, `Polyline3d`, Feature Line, Pipe Network, Pressure Network e Polylines com arcos (bulge) não são exportadas nesta versão. Quando a seleção contém geometria não suportada, o comando cancela o arquivo inteiro e informa o handle ou o tipo problemático.

## Como usar

1. Instale ou atualize o bundle `GeoAutoEngine.bundle` normalmente.
2. Abra o DWG no Civil 3D e confirme o sistema de coordenadas e a unidade do desenho.
3. Execute `GEOAUTO3GEOMAPEXPORT` na linha de comando.
4. Selecione as Polylines da mesma rede e pressione Enter.
5. Confirme projeto, rede e tipo. Quando o layer pertence ao catálogo oficial do GeoAutoEngine, a rede é sugerida automaticamente.
6. Informe o EPSG de origem, por exemplo `EPSG:31982`. Também é aceito informar somente `31982`.
7. Confirme a unidade e escolha o arquivo `*.geomap.json`.
8. Importe o arquivo pelo importador do GeoMap.

O EPSG nunca recebe um valor silencioso: ele deve ser informado em cada exportação. A unidade é sugerida somente quando `INSUNITS` está configurado como metros, pés ou US survey feet.

## Política geométrica

Polylines abertas são exportadas como `LineString` com os vértices na ordem original. Em Polylines fechadas, a primeira posição é repetida no final para representar o fechamento. A coordenada Z não faz parte do contrato v0.1; a propriedade `projectedTo2D` registra quando os pontos CAD possuíam elevação diferente de zero, e `cadElevation` preserva a elevação original como metadado.

O arquivo é um snapshot completo da rede informada. Uma nova importação com o mesmo identificador externo substitui o snapshot anterior no GeoMap.
