from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
LEGACY_ROOT = REPOSITORY_ROOT / "GeoAutoEngine.Legacy"
REFERENCE_ROOT = REPOSITORY_ROOT / "reference"
sys.path.insert(0, str(LEGACY_ROOT))

from app.services.pipeline_service import ProcessingPipeline  # noqa: E402
from app.services.normalization_service import NormalizadorCodigos  # noqa: E402


FIELDS = (
    "codigo_original",
    "codigo_normalizado",
    "natureza",
    "tipo",
    "rede",
    "profundidade",
    "diametro",
    "material",
    "complemento",
    "reconhecido",
    "confianca",
    "regra",
    "observacao",
)

CATALOG_CODES = (
    "", "CODIGO-NOVO-XYZ", "DIVISA 113", "POSS-CX0.75", "POSSIVEL PVE",
    "AFCAP1.20", "CXAP1.20", "CXEL1.20", "CX1.20", "CAP1.20", "PVENCOBERTO-1.20",
    "APL", "ESI", "ESA", "ELE", "TEL", "PVTL", "PV-AF-ENCOBERTO", "PVAF", "PVAP2",
    "PVES", "PVE03-AFG", "PV-ENCOBERTO", "PV", "CXAP-EL", "CXEL-LAC", "CXEI-AFG",
    "CXESAFG", "CXAF", "CXNI", "CX VALVULA", "CX GORDURA", "CXE-AFG", "CXLAC2",
    "CX ASS", "CXCEGA", "RG", "CAP-AF", "CAP-NI", "CURVA AF", "CRUZETA AF", "CVLT AF",
    "GR AFG", "BL ASS", "PI EI", "PI1.20", "PTIL", "PT", "PONTO HIDRATANTE", "RALO",
    "SUBESTACAO", "TRANSFORMADOR", "PAINEL ELETRONICO", "BOMBA", "COMPORTA DE EFLUENTES",
    "CASA DE BOMBAS", "TANQUE DE EFLUENTES INDUSTRIAL", "ESTACAO DE TRATAMENTO", "GAS",
    "CASA GAS", "PN EL", "POCO", "AF", "AF MACRO", "AF-DESCARGA", "AF SUBIDA D50",
    "AP SAIDA SP", "EL SP", "INICIO- ESGOTO", "INICIA-AF", "QUEDA-EL", "TBQ INC",
    "AFVISIVELD100PVC", "APD300APARENTE", "APGSD100", "TBQD100", "TEL-ENV-2.00-60X40",
    "AF0,95 Ø250 PVC SUB", "AFT1.10", "AP1.20", "EI1.30", "ES1.40", "E1.50", "EL1.60",
    "NI1.70", "TL1.80", "FB1.90", "GAP2.00", "BASE", "ESTACAO E10", "GUIA", "AREA VERDE",
    "MATACAO1.20", "PVES 1.20 D150 OBS", "RG PROF 0.80", "PT POSTE NOVO",
)


def json_value(value: Any) -> Any:
    if value is None:
        return None
    if isinstance(value, float) and math.isnan(value):
        return None
    if hasattr(value, "item"):
        value = value.item()
    return value


def main() -> None:
    fixtures = []
    pipeline = ProcessingPipeline()
    real_root = REFERENCE_ROOT / "legacy_csv" / "real"
    for file_name in (
        "Rua S#U00e3o Francisco 1407.csv",
        "RUA BOEHMERWALD 1407(1).csv",
    ):
        path = real_root / file_name
        processed = pipeline.process(path)
        rows = []
        for source_index, (_, row) in enumerate(processed.dataframe.iterrows()):
            rows.append(
                {
                    "source_index": source_index,
                    **{
                        field: json_value(row[f"norm_{field}"])
                        for field in FIELDS
                    },
                }
            )
        fixtures.append(
            {
                "file_name": file_name,
                "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                "delimiter": processed.delimiter,
                "encoding": processed.encoding,
                "rows": rows,
            }
        )

    normalizer = NormalizadorCodigos()
    catalog_cases = []
    for code in CATALOG_CODES:
        result = normalizer.normalizar(code)
        catalog_cases.append({field: json_value(getattr(result, field)) for field in FIELDS})

    output = REFERENCE_ROOT / "regression_data" / "stage2_interpretation_oracle.json"
    output.write_text(
        json.dumps(
            {"schema": "1.1", "fixtures": fixtures, "catalog_cases": catalog_cases},
            ensure_ascii=False,
            indent=2,
        ) + "\n",
        encoding="utf-8",
    )
    print(f"Stage 2 interpretation oracle: {output} ({sum(len(item['rows']) for item in fixtures)} rows)")


if __name__ == "__main__":
    main()
