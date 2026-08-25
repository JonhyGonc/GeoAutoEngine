"""Capture the frozen GeoAutoEngine 2.1.3 pre-CAD drawing preparation oracle.

This tool reads the legacy implementation without changing it. The resulting
JSON deliberately excludes geometry-dependent rotation, collision and DWG
application results.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


WORKSPACE = Path(__file__).resolve().parents[2]
LEGACY = WORKSPACE / "GeoAutoEngine.Legacy"
REFERENCE = WORKSPACE / "reference"
sys.path.insert(0, str(LEGACY))

from app.config.cad_text_config import (  # noqa: E402
    NOME_ESTILO_ANOTACAO_DIVISA,
    NOME_ESTILO_TEXTO,
)
from app.config.network_catalog import obter_layers_geometria  # noqa: E402
from app.generators.autolisp_generator import LispGenerator  # noqa: E402
from app.generators.scr_generator import ScrGenerator  # noqa: E402
from app.services.block_preparation_service import BlockProcessor  # noqa: E402
from app.services.pipeline_service import ProcessingPipeline  # noqa: E402


FIXTURES = (
    "RUA BOEHMERWALD 1407(1).csv",
    "Rua S#U00e3o Francisco 1407.csv",
)


def point(x: object, y: object) -> list[float]:
    return [float(x), float(y), 0.0]


def capture(file_name: str) -> dict[str, object]:
    source = REFERENCE / "legacy_csv" / "real" / file_name
    frame = ProcessingPipeline().process(source).dataframe
    records = ScrGenerator().preparar_registros(frame, distancia_minima=1.0)
    block_result = BlockProcessor().preparar(frame, minimum_distance=1.0)
    generator = LispGenerator()
    annotations = generator._preparar_anotacoes_divisa(frame)
    unknown = generator._preparar_alertas_codigos_desconhecidos(frame)
    structures, rain_alerts = generator._preparar_estruturas_pluviais(frame)

    texts: list[dict[str, object]] = []
    for record in records:
        curve_layers = list(obter_layers_geometria(record["rede"], record["layer"]))
        texts.append(
            {
                "kind": "Depth",
                "content": f"P.{ScrGenerator._formatar_profundidade(float(record['profundidade']))}",
                "layer": record["layer"],
                "anchor": point(record["x"], record["y"]),
                "height": 0.5,
                "style": NOME_ESTILO_TEXTO,
                "rotation_mode": "FollowNetwork",
                "perpendicular_offset": 0.1,
                "curve_layers": curve_layers,
            }
        )
        if record["diametro"] is not None:
            texts.append(
                {
                    "kind": "Diameter",
                    "content": f"∅{ScrGenerator._formatar_diametro(float(record['diametro']))}",
                    "layer": record["layer"],
                    "anchor": point(record["x"], record["y"]),
                    "height": 0.5,
                    "style": NOME_ESTILO_TEXTO,
                    "rotation_mode": "FollowNetwork",
                    "perpendicular_offset": -0.6,
                    "curve_layers": curve_layers,
                }
            )

    for annotation in annotations:
        texts.append(
            {
                "kind": "BoundaryAnnotation",
                "content": annotation["texto"],
                "layer": annotation["layer"],
                "anchor": point(annotation["x"], annotation["y"]),
                "height": 0.5,
                "style": NOME_ESTILO_ANOTACAO_DIVISA,
                "rotation_mode": "Fixed",
                "perpendicular_offset": 0.0,
                "curve_layers": [],
            }
        )

    blocks = [
        {
            "rule": item.regra,
            "code": item.codigo,
            "network": item.rede,
            "key": item.block_key,
            "name": item.block_name,
            "asset": item.block_file.name,
            "layer": item.layer,
            "position": point(item.x, item.y),
            "scale": [item.scale_x, item.scale_y, item.scale_z],
            "rotation_mode": "FollowNetwork" if item.rotation_mode == "follow_network" else "Fixed",
            "fixed_rotation": item.rotation,
            "curve_layers": list(item.curve_layers),
            "alignment": "InsertionPoint" if item.alignment_mode == "insertion_point" else "BoundsCenter",
        }
        for item in block_result.instructions
    ]

    rain_structures = [
        {
            "kind": "Grate" if item["tipo"] == "GRELHA" else "CatchBasin",
            "code": item["codigo"],
            "layer": item["layer"],
            "vertices": [point(vertex[0], vertex[1]) for vertex in item["vertices"]],
            "center": point(item["centro"][0], item["centro"][1]),
        }
        for item in structures
    ]
    alerts = [
        {
            "kind": "UnknownCode",
            "code": item["codigo"],
            "layer": item["layer"],
            "center": point(item["x"], item["y"]),
            "radius": generator.RAIO_ATENCAO,
            "color": generator.COR_ATENCAO_AUTOCAD,
            "related_records": 1,
        }
        for item in unknown
    ]
    alerts.extend(
        {
            "kind": "RainStructureReview",
            "code": item["codigo"],
            "layer": item["layer"],
            "center": point(item["centro"][0], item["centro"][1]),
            "radius": generator.RAIO_ATENCAO,
            "color": generator.COR_ATENCAO_AUTOCAD,
            "related_records": item["quantidade_pontos"],
        }
        for item in rain_alerts
    )

    sort_key = lambda item: json.dumps(item, ensure_ascii=False, sort_keys=True)
    return {
        "file_name": file_name,
        "source_rows": len(frame),
        "texts": sorted(texts, key=sort_key),
        "blocks": sorted(blocks, key=sort_key),
        "rain_structures": sorted(rain_structures, key=sort_key),
        "alerts": sorted(alerts, key=sort_key),
        "block_suppressions": {
            "unmapped_rules": dict(sorted(block_result.unmapped_rules.items())),
            "exact_duplicates": dict(sorted(block_result.exact_duplicates.items())),
            "proximity_duplicates": dict(sorted(block_result.proximity_duplicates.items())),
        },
    }


def main() -> None:
    output = REFERENCE / "regression_data" / "stage3_drawing_oracle.json"
    payload = {
        "schema": "geoautoengine-stage3-legacy-oracle-v1",
        "legacy_version": "2.1.3",
        "cad_dependent_results_excluded": True,
        "fixtures": [capture(file_name) for file_name in FIXTURES],
    }
    output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(output)


if __name__ == "__main__":
    main()
