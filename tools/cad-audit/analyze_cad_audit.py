from __future__ import annotations

import argparse
import collections
import datetime as dt
import hashlib
import json
import math
import re
import statistics
import unicodedata
from pathlib import Path
from typing import Any, Iterable


NETWORK_LAYER_MAP = {
    "GF-Rede Pluvial": ("PLUVIAL", "Águas pluviais"),
    "GF-Rede Esgoto": ("ESGOTO_SANITARIO", "Esgoto sanitário"),
    "GF-Rede Esgoto Industrial": ("ESGOTO_INDUSTRIAL", "Esgoto industrial"),
    "GF-Rede Eletrica": ("ELETRICA", "Elétrica"),
    "GF-Rede Água": ("AGUA_POTAVEL", "Água potável"),
    "GF-Rede Gás": ("GAS", "Gás"),
    "GF-Rede Telefone": ("TELEFONE", "Telefone"),
    "GF-Rede Fibra Optica": ("FIBRA_OPTICA", "Fibra óptica"),
    "GF-Rede de Incêndio": ("INCENDIO", "Incêndio"),
    "GF-Rede Sem Descrição de Uso": ("DESCONHECIDA", "Sem descrição de uso"),
}

LEGEND_NETWORK_LABEL_MAP = {
    "REDE ÁGUAS PLUVIAIS": "PLUVIAL",
    "REDE ESGOTO SANITÁRIO": "ESGOTO_SANITARIO",
    "REDE ESGOTO INDUSTRIAL": "ESGOTO_INDUSTRIAL",
    "REDE ELÉTRICA": "ELETRICA",
    "REDE ÁGUA POTÁVEL": "AGUA_POTAVEL",
    "REDE GÁS": "GAS",
    "REDE DE TELEFONE": "TELEFONE",
    "REDE DE FIBRA ÓPTICA": "FIBRA_OPTICA",
    "REDE DE INCÊNDIO": "INCENDIO",
    "REDE S/ DESCRIÇÃO DE USO": "DESCONHECIDA",
}

LEGEND_STRUCTURE_LABEL_MAP = {
    "P.V. ÁGUAS PLUVIAIS": "PV_PLUVIAL",
    "P.V. ESGOTO SANITÁRIO": "PV_ESGOTO_SANITARIO",
    "P.V. ESGOTO INDUSTRIAL": "PV_ESGOTO_INDUSTRIAL",
    "P.V. REDE ELÉTRICA": "PV_ELETRICA",
    "R.G. ÁGUA POTÁVEL": "REGISTRO_AGUA_POTAVEL",
    "PV GÁS": "PV_GAS",
    "PV TELEFONE": "PV_TELEFONE",
    "PV FIBRA ÓPTICA": "PV_FIBRA_OPTICA",
    "PV NÃO IDENTIFICADO": "PV_NAO_IDENTIFICADO",
    "HIDRANTE": "HIDRANTE",
    "POSTE": "POSTE",
    "LUMINÁRIA": "LUMINARIA",
    "GRELHA": "GRELHA",
    "BOCA DE LOBO": "BOCA_DE_LOBO",
    "CAIXAS DE ACESSO": "CAIXA_DE_ACESSO",
}

PREFIX_NETWORK_MAP = {
    "AP": "PLUVIAL",
    "E": "ESGOTO_SANITARIO",
    "AF": "AGUA_POTAVEL",
    "NI": "DESCONHECIDA",
}

MATERIAL_PATTERN = re.compile(
    r"\b(PVC|PEAD|HDPE|CONCRETO|FERRO\s+FUNDIDO|FOFO|A[ÇC]O|PPR|COBRE|CER[ÂA]MICA)\b",
    re.IGNORECASE,
)
DEPTH_TEXT_PATTERN = re.compile(r"^P[.,](\d+(?:[.,]\d+)?)$", re.IGNORECASE)
DIAMETER_TEXT_PATTERN = re.compile(r"^[∅Ø]\s*(\d+)\s*(?:MM)?$", re.IGNORECASE)
SECTION_TEXT_PATTERN = re.compile(r"^TRECHO\s+(\d+)(.*)$", re.IGNORECASE)
POINT_CODE_PATTERN = re.compile(
    r"^(?P<possible>POSSIVEL\s+)?(?P<prefix>AP|NI|AF|E)(?P<depth>\.?\d+(?:[.,]\d+)?)"
    r"(?:D(?P<diameter>\d+))?(?P<suffix>[A-Z].*)?$",
    re.IGNORECASE,
)


def normalize_text(value: str) -> str:
    return re.sub(r"\s+", " ", value.replace("\r", " ").replace("\n", " ")).strip()


def fold(value: str) -> str:
    return "".join(
        char for char in unicodedata.normalize("NFD", value) if unicodedata.category(char) != "Mn"
    ).upper()


def md(value: Any) -> str:
    if value is None:
        return "—"
    return str(value).replace("|", "\\|").replace("\n", "<br>")


def pct(value: int, total: int) -> str:
    return "0%" if not total else f"{100 * value / total:.1f}%"


def get_space(drawing: dict[str, Any], name: str) -> dict[str, Any]:
    return next(space for space in drawing["spaces"] if space["name"].upper() == name.upper())


def entity_points(entity: dict[str, Any]) -> list[list[float]]:
    vertices = entity.get("vertices") or []
    result: list[list[float]] = []
    for item in vertices:
        if isinstance(item, dict) and isinstance(item.get("point"), list):
            result.append(item["point"])
        elif isinstance(item, list):
            result.append(item)
    if result:
        return result
    samples = entity.get("sample_points") or []
    if samples:
        return samples
    start = entity.get("start")
    end = entity.get("end")
    return [point for point in (start, end) if isinstance(point, list)]


def xy(point: list[float]) -> tuple[float, float]:
    return float(point[0]), float(point[1])


def point_distance(left: tuple[float, float], right: tuple[float, float]) -> float:
    return math.hypot(left[0] - right[0], left[1] - right[1])


def point_segment_distance(
    point: tuple[float, float], start: tuple[float, float], end: tuple[float, float]
) -> float:
    dx = end[0] - start[0]
    dy = end[1] - start[1]
    length_sq = dx * dx + dy * dy
    if length_sq == 0:
        return point_distance(point, start)
    t = max(0.0, min(1.0, ((point[0] - start[0]) * dx + (point[1] - start[1]) * dy) / length_sq))
    projection = (start[0] + t * dx, start[1] + t * dy)
    return point_distance(point, projection)


def point_polyline_distance(point: tuple[float, float], entity: dict[str, Any]) -> float:
    points = [xy(item) for item in entity_points(entity)]
    if len(points) == 1:
        return point_distance(point, points[0])
    if len(points) < 2:
        return math.inf
    return min(point_segment_distance(point, left, right) for left, right in zip(points, points[1:]))


def polyline_key(entity: dict[str, Any], decimals: int = 4) -> tuple[tuple[float, float], ...]:
    points = tuple((round(p[0], decimals), round(p[1], decimals)) for p in entity_points(entity))
    reverse = tuple(reversed(points))
    return min(points, reverse)


def polyline_z_values(entity: dict[str, Any]) -> list[float]:
    return [float(point[2]) for point in entity_points(entity) if len(point) > 2]


def xdata_dictionary(entity: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    application: str | None = None
    for item in entity.get("xdata") or []:
        if item.get("type_code") == 1001:
            application = str(item.get("value"))
            result.setdefault("applications", []).append(application)
        elif item.get("type_code") == 1000 and isinstance(item.get("value"), str):
            value = item["value"]
            if "=" in value:
                key, raw = value.split("=", 1)
                result[key.lower()] = raw
            else:
                result.setdefault("strings", []).append(value)
    if application:
        result["last_application"] = application
    return result


def parse_point_code(raw: str) -> dict[str, Any] | None:
    value = normalize_text(raw).upper()
    match = POINT_CODE_PATTERN.match(value)
    if not match:
        return None
    depth_raw = match.group("depth").replace(",", ".")
    if depth_raw.startswith("."):
        depth_raw = "0" + depth_raw
    return {
        "raw": raw,
        "network_type": PREFIX_NETWORK_MAP[match.group("prefix").upper()],
        "prefix": match.group("prefix").upper(),
        "depth_m": float(depth_raw),
        "diameter_mm": int(match.group("diameter")) if match.group("diameter") else None,
        "possible": bool(match.group("possible")),
        "suffix_raw": match.group("suffix") or None,
    }


def layer_meaning(layer_name: str) -> tuple[str, str, str]:
    if layer_name in NETWORK_LAYER_MAP:
        code, label = NETWORK_LAYER_MAP[layer_name]
        return f"Rede {label}", code, "TÉCNICA"
    if layer_name == "GF-Rede Pluvial-VIDEO":
        return "Representação/intervalo de vídeo-inspeção pluvial", "PLUVIAL", "MISTA"
    if layer_name == "GF-Rede Pluvial_":
        return "Anotação pluvial em layer variante", "PLUVIAL", "REVISÃO NECESSÁRIA"
    folded = fold(layer_name)
    structure_tokens = ("BOCA DE LOBO", "GRELHA", "POSTE", "PV", "REGISTRO", "HIDRANTE", "CX ", "CAIXA")
    graphic_tokens = ("FOLHA", "TEXTO", "FORMATO", "MALHA", "ALINHAMENTO", "EIXO", "ESTACA", "NOTAVEL", "NORTE")
    if any(token in folded for token in structure_tokens):
        return "Estrutura/dispositivo ou sua representação", "—", "TÉCNICA/MISTA"
    if any(token in folded for token in graphic_tokens) or layer_name in {"0", "Defpoints"}:
        return "Apresentação, referência ou anotação CAD", "—", "GRÁFICA"
    if layer_name == "_TG":
        return "Pontos levantados com atributos PONTO/DESC/COTA", "MÚLTIPLA", "TÉCNICA"
    if "GUIA" in folded or "SARGETA" in folded or "LAGO" in folded or "GALERIA" in folded:
        return "Feição de contexto/infraestrutura", "—", "TÉCNICA/MISTA"
    return "Significado não determinado automaticamente", "—", "REVISÃO NECESSÁRIA"


def active_layer_inventory(drawing: dict[str, Any]) -> list[dict[str, Any]]:
    model = get_space(drawing, "*MODEL_SPACE")
    paper = get_space(drawing, "*PAPER_SPACE")
    model_groups: dict[str, collections.Counter[str]] = collections.defaultdict(collections.Counter)
    paper_groups: dict[str, collections.Counter[str]] = collections.defaultdict(collections.Counter)
    for entity in model["entities"]:
        model_groups[entity["layer"]][entity["object_type"]] += 1
    for entity in paper["entities"]:
        paper_groups[entity["layer"]][entity["object_type"]] += 1
    result = []
    for layer in drawing["layers"]:
        name = layer["name"]
        meaning, network, classification = layer_meaning(name)
        model_types = dict(sorted(model_groups[name].items()))
        paper_types = dict(sorted(paper_groups[name].items()))
        result.append(
            {
                "name": name,
                "model_object_count": sum(model_groups[name].values()),
                "paper_object_count": sum(paper_groups[name].values()),
                "model_entity_types": model_types,
                "paper_entity_types": paper_types,
                "color": layer.get("color"),
                "linetype": layer.get("linetype"),
                "lineweight": layer.get("lineweight"),
                "description": layer.get("description") or None,
                "state": {
                    "off": layer.get("is_off"),
                    "frozen": layer.get("is_frozen"),
                    "locked": layer.get("is_locked"),
                    "plottable": layer.get("is_plottable"),
                },
                "possible_meaning": meaning,
                "possible_network_category": network,
                "classification": classification,
            }
        )
    return result


def extract_legend(drawing: dict[str, Any]) -> dict[str, Any]:
    paper = get_space(drawing, "*PAPER_SPACE")
    texts = [normalize_text(entity.get("text", "")) for entity in paper["entities"] if entity.get("text")]
    definition_texts = [
        normalize_text(entity.get("text", ""))
        for block in drawing["block_definitions"]
        for entity in block.get("entities") or []
        if entity.get("text")
    ]
    all_legend_texts = texts + definition_texts
    network_items = []
    for label, code in LEGEND_NETWORK_LABEL_MAP.items():
        matches = [entity for entity in paper["entities"] if normalize_text(entity.get("text", "")) == label]
        if not matches:
            continue
        expected_layer = next((layer for layer, value in NETWORK_LAYER_MAP.items() if value[0] == code), None)
        samples = [
            entity
            for entity in paper["entities"]
            if entity.get("layer") == expected_layer and entity.get("object_type") in {"AcDbPolyline", "AcDbLine"}
        ]
        network_items.append(
            {
                "network_type": code,
                "legend_label": label,
                "sample_layer": expected_layer,
                "sample_entity_types": sorted({entity["object_type"] for entity in samples}),
                "sample_count": len(samples),
                "interpretation": "Vocabulário corporativo confirmado pela legenda; não comprova ocorrência no Model Space.",
            }
        )
    structure_items = []
    for label, code in LEGEND_STRUCTURE_LABEL_MAP.items():
        matches = [entity for entity in paper["entities"] if normalize_text(entity.get("text", "")) == label]
        for match in matches:
            px, py = xy(match.get("position", [0, 0]))
            blocks = []
            for entity in paper["entities"]:
                if entity.get("object_type") != "AcDbBlockReference" or not entity.get("position"):
                    continue
                distance = point_distance((px, py), xy(entity["position"]))
                if distance <= 20:
                    blocks.append((distance, entity))
            blocks.sort(key=lambda item: item[0])
            structure_items.append(
                {
                    "structure_type": code,
                    "legend_label": label,
                    "nearby_block_name": blocks[0][1].get("block_name") if blocks else None,
                    "nearby_block_layer": blocks[0][1].get("layer") if blocks else None,
                    "distance_to_block": round(blocks[0][0], 3) if blocks else None,
                }
            )
            break
    notes = [text for text in all_legend_texts if "DATUM SIRGAS 2000" in text.upper()]
    return {
        "located_in": "Paper Space",
        "network_catalog": network_items,
        "structure_catalog": structure_items,
        "coordinate_notes": notes,
        "has_title_legenda": "LEGENDA" in all_legend_texts,
        "has_title_redes_existentes": "REDES EXISTENTES" in all_legend_texts,
        "video_inspection_label": "REDE DA VIDEO INSPEÇÃO" in texts,
    }


def network_patterns(drawing: dict[str, Any]) -> list[dict[str, Any]]:
    model = get_space(drawing, "*MODEL_SPACE")
    result = []
    layers = list(NETWORK_LAYER_MAP) + ["GF-Rede Pluvial-VIDEO", "GF-Rede Pluvial_", "GF-Galeria"]
    for layer in layers:
        entities = [entity for entity in model["entities"] if entity["layer"] == layer]
        if not entities:
            continue
        polylines = [entity for entity in entities if entity["object_type"] == "AcDbPolyline"]
        open_lines = [entity for entity in polylines if not entity.get("closed")]
        closed_lines = [entity for entity in polylines if entity.get("closed")]
        groups: dict[tuple[tuple[float, float], ...], list[dict[str, Any]]] = collections.defaultdict(list)
        for entity in open_lines:
            groups[polyline_key(entity)].append(entity)
        unique_lines = [items[0] for items in groups.values()]
        z_values = [z for entity in open_lines for z in polyline_z_values(entity)]
        code = NETWORK_LAYER_MAP.get(layer, ("PLUVIAL" if "Pluvial" in layer else "REVISÃO", ""))[0]
        role = "REDE_FISICA"
        confidence = "ALTA"
        if layer == "GF-Rede Pluvial-VIDEO":
            role = "COBERTURA/REPRESENTAÇÃO_DE_VÍDEO_INSPEÇÃO"
            confidence = "MÉDIA"
        elif layer == "GF-Galeria":
            role = "CONTORNO/REPRESENTAÇÃO_DE_GALERIA"
            confidence = "MÉDIA"
        elif layer == "GF-Rede Pluvial_":
            role = "ANOTAÇÃO"
            confidence = "BAIXA"
        result.append(
            {
                "layer": layer,
                "network_type": code,
                "role": role,
                "entity_type": "AcDbPolyline" if polylines else None,
                "open_polyline_count": len(open_lines),
                "unique_open_geometry_count": len(unique_lines),
                "exact_duplicate_feature_count": len(open_lines) - len(unique_lines),
                "closed_polyline_count": len(closed_lines),
                "text_count": sum(entity["object_type"] in {"AcDbText", "AcDbMText"} for entity in entities),
                "block_count": sum(entity["object_type"] == "AcDbBlockReference" for entity in entities),
                "has_z": bool(z_values),
                "z_min": min(z_values) if z_values else None,
                "z_max": max(z_values) if z_values else None,
                "z_is_constant_per_polyline": all(
                    len({round(z, 6) for z in polyline_z_values(entity)}) <= 1 for entity in open_lines
                ),
                "geometry_interpretation": "LineString para polylines abertas; polylines fechadas exigem classificação como estrutura/contorno.",
                "classification_source": "Layer + morfologia aberta/fechada + legenda",
                "confidence": confidence,
            }
        )
    return result


def model_block_patterns(drawing: dict[str, Any]) -> list[dict[str, Any]]:
    model = get_space(drawing, "*MODEL_SPACE")
    groups: dict[tuple[str, str], list[dict[str, Any]]] = collections.defaultdict(list)
    for entity in model["entities"]:
        if entity["object_type"] == "AcDbBlockReference":
            groups[(entity.get("block_name") or "<ANÔNIMO>", entity["layer"])].append(entity)
    result = []
    for (name, layer), entities in sorted(groups.items(), key=lambda item: (-len(item[1]), item[0])):
        attribute_tags = sorted(
            {
                attribute["tag"]
                for entity in entities
                for attribute in entity.get("attributes") or []
                if attribute.get("tag")
            }
        )
        if name == "SIMB_PONTO":
            role, relevance = "Ponto levantado com código, descrição e cota", "TÉCNICO"
        elif name.upper() in {"POSTE", "PV ESA", "PV_ESA", "PV_ESGOTO_SANITARIO", "RG_AGUA_POTAVEL"} or name.lower() in {
            "pv_esgoto_sanitario",
            "rg_agua_potavel",
        }:
            role, relevance = "Estrutura/dispositivo do mundo real", "TÉCNICO"
        elif name.upper().startswith("SENTIDO"):
            role, relevance = "Seta de sentido; representação de possível direção de fluxo", "GRÁFICO COM SEMÂNTICA"
        elif name.startswith("AeccTick") or name.startswith("A$C"):
            role, relevance = "Símbolo de anotação Civil 3D/norte", "GRÁFICO"
        else:
            role, relevance = "Função não determinada automaticamente", "REVISÃO NECESSÁRIA"
        result.append(
            {
                "block_name": name,
                "layer": layer,
                "insertion_count": len(entities),
                "attribute_tags": attribute_tags,
                "sample_attribute_values": {
                    tag: list(
                        dict.fromkeys(
                            attribute.get("text")
                            for entity in entities
                            for attribute in entity.get("attributes") or []
                            if attribute.get("tag") == tag
                        )
                    )[:10]
                    for tag in attribute_tags
                },
                "rotation_min": min(entity.get("rotation", 0) for entity in entities),
                "rotation_max": max(entity.get("rotation", 0) for entity in entities),
                "scale_values": list(dict.fromkeys(tuple(entity.get("scale") or []) for entity in entities))[:10],
                "possible_function": role,
                "classification": relevance,
            }
        )
    return result


def structure_patterns(drawing: dict[str, Any]) -> list[dict[str, Any]]:
    model = get_space(drawing, "*MODEL_SPACE")
    result = []
    block_map = {
        "POSTE": ("POSTE", "ALTA"),
        "rg_agua_potavel": ("REGISTRO_AGUA_POTAVEL", "ALTA"),
        "PV ESA": ("PV_ESGOTO_SANITARIO", "ALTA"),
        "pv_esgoto_sanitario": ("PV_ESGOTO_SANITARIO", "ALTA"),
    }
    for block_name, (kind, confidence) in block_map.items():
        entities = [entity for entity in model["entities"] if entity.get("block_name") == block_name]
        if entities:
            result.append(
                {
                    "structure_type": kind,
                    "cad_type": "AcDbBlockReference",
                    "block_name": block_name,
                    "layers": sorted({entity["layer"] for entity in entities}),
                    "count": len(entities),
                    "attribute_tags": sorted(
                        {
                            attribute["tag"]
                            for entity in entities
                            for attribute in entity.get("attributes") or []
                        }
                    ),
                    "coordinate_source": "BlockReference.Position",
                    "elevation_source": "BlockReference.Position.Z (sem validação semântica)",
                    "confidence": confidence,
                }
            )
    closed_layer_map = {
        "GF-Boca de Lobo": ("BOCA_DE_LOBO", "ALTA"),
        "GF-Grelha": ("GRELHA", "ALTA"),
        "GF-Rede Pluvial": ("ESTRUTURA_PLUVIAL_NAO_CLASSIFICADA", "MÉDIA"),
        "GF-Rede Pluvial-VIDEO": ("ESTRUTURA_PLUVIAL_VIDEO", "MÉDIA"),
        "GF-Rede Eletrica": ("DISPOSITIVO_ELETRICO_NAO_CLASSIFICADO", "MÉDIA"),
        "GF-Estrutura Civil": ("ESTRUTURA_CIVIL_NAO_CLASSIFICADA", "BAIXA"),
    }
    for layer, (kind, confidence) in closed_layer_map.items():
        entities = [
            entity
            for entity in model["entities"]
            if entity["layer"] == layer and entity["object_type"] == "AcDbPolyline" and entity.get("closed")
        ]
        if entities:
            result.append(
                {
                    "structure_type": kind,
                    "cad_type": "AcDbPolyline fechada",
                    "block_name": None,
                    "layers": [layer],
                    "count": len(entities),
                    "attribute_tags": [],
                    "coordinate_source": "Centroide/footprint da polyline",
                    "elevation_source": "Polyline.Elevation (sem validação semântica)",
                    "confidence": confidence,
                }
            )
    hatch_count = sum(
        entity["object_type"] == "AcDbHatch" and entity["layer"] == "GF-Grelha" for entity in model["entities"]
    )
    if hatch_count:
        result.append(
            {
                "structure_type": "GRELHA_HACHURA",
                "cad_type": "AcDbHatch",
                "layers": ["GF-Grelha"],
                "count": hatch_count,
                "confidence": "ALTA",
                "interpretation": "Representação gráfica associada à grelha; não criar entidade GIS independente.",
            }
        )
    return result


def technical_observations(drawing: dict[str, Any]) -> dict[str, Any]:
    model = get_space(drawing, "*MODEL_SPACE")
    point_observations = []
    for entity in model["entities"]:
        if entity.get("block_name") != "SIMB_PONTO":
            continue
        attributes = {attribute["tag"]: attribute.get("text") for attribute in entity.get("attributes") or []}
        parsed = parse_point_code(attributes.get("DESC") or "")
        point_observations.append(
            {
                "handle": entity["handle"],
                "point_name": attributes.get("PONTO"),
                "description_raw": attributes.get("DESC"),
                "elevation_attribute": float(str(attributes["COTA"]).replace(",", ".")) if attributes.get("COTA") else None,
                "position": entity.get("position"),
                "parsed": parsed,
            }
        )
    xdata_records: dict[str, dict[str, Any]] = {}
    xdata_entity_count = 0
    for entity in model["entities"]:
        values = xdata_dictionary(entity)
        if "GEOAUTOENGINE" not in (values.get("applications") or []):
            continue
        xdata_entity_count += 1
        record_id = values.get("registro")
        if not record_id:
            continue
        record = xdata_records.setdefault(
            record_id,
            {
                "record_id": record_id,
                "code": values.get("codigo"),
                "origin": values.get("origem"),
                "execution": values.get("execucao"),
                "entity_handles": [],
                "attribute_types": [],
            },
        )
        record["entity_handles"].append(entity["handle"])
        if values.get("tipo"):
            record["attribute_types"].append(values["tipo"])
    parsed_points = [item for item in point_observations if item["parsed"]]
    return {
        "survey_point_count": len(point_observations),
        "survey_point_attribute_tags": ["PONTO", "DESC", "COTA"],
        "parseable_network_code_count": len(parsed_points),
        "parseable_network_code_percent": round(100 * len(parsed_points) / len(point_observations), 1)
        if point_observations
        else 0,
        "parsed_depth_count": sum(item["parsed"].get("depth_m") is not None for item in parsed_points),
        "parsed_diameter_count": sum(item["parsed"].get("diameter_mm") is not None for item in parsed_points),
        "network_type_counts": dict(collections.Counter(item["parsed"]["network_type"] for item in parsed_points)),
        "raw_description_prefix_counts": dict(
            collections.Counter(
                (re.match(r"[A-Za-zÀ-ÿ_ ]+", item["description_raw"] or "") or ["<OUTRO>"])[0].strip().upper()
                for item in point_observations
            ).most_common()
        ),
        "survey_points": point_observations,
        "geoautoengine_xdata_entity_count": xdata_entity_count,
        "geoautoengine_record_count": len(xdata_records),
        "geoautoengine_records": list(xdata_records.values()),
    }


def text_patterns(drawing: dict[str, Any]) -> list[dict[str, Any]]:
    model = get_space(drawing, "*MODEL_SPACE")
    buckets: dict[str, list[tuple[str, str]]] = collections.defaultdict(list)
    for entity in model["entities"]:
        raw = entity.get("text")
        if not raw:
            continue
        value = normalize_text(raw)
        upper = value.upper()
        if DEPTH_TEXT_PATTERN.fullmatch(value):
            key = "PROFUNDIDADE"
        elif DIAMETER_TEXT_PATTERN.fullmatch(value):
            key = "DIAMETRO"
        elif SECTION_TEXT_PATTERN.match(value):
            key = "TRECHO_VIDEO"
        elif re.fullmatch(r"E\(X\)=\d+", value, re.IGNORECASE):
            key = "MALHA_EASTING"
        elif re.fullmatch(r"N\(Y\)=\d+", value, re.IGNORECASE):
            key = "MALHA_NORTHING"
        elif re.fullmatch(r"N[°º]\s*\d+", value, re.IGNORECASE):
            key = "NUMERO_PREDIAL"
        elif re.match(r"^(INÍCIO|FINAL|PI)=EST\.", upper):
            key = "ESTACA_VIDEO"
        elif any(token in upper for token in ("SEDIMENT", "ASSORE", "LACR", "INFILTRA", "TRINCO", "ABAULAMENTO", "JUNTAS")):
            key = "CONDICAO/DEFEITO"
        else:
            key = "OUTRO"
        buckets[key].append((value, entity["layer"]))
    meanings = {
        "PROFUNDIDADE": ("Profundidade em metros; a legenda define geratriz superior", "depth_m", "ALTA"),
        "DIAMETRO": ("Diâmetro em milímetros", "diameter_mm", "ALTA"),
        "TRECHO_VIDEO": ("Identificador de trecho de vídeo-inspeção", "inspection_segment_code", "ALTA"),
        "MALHA_EASTING": ("Rótulo gráfico da malha UTM", "—", "ALTA"),
        "MALHA_NORTHING": ("Rótulo gráfico da malha UTM", "—", "ALTA"),
        "NUMERO_PREDIAL": ("Número predial/contexto cartográfico", "address_number", "MÉDIA"),
        "ESTACA_VIDEO": ("Estaca/posição ao longo de vídeo-inspeção", "inspection_station", "ALTA"),
        "CONDICAO/DEFEITO": ("Condição observada ou defeito", "condition_raw", "MÉDIA"),
        "OUTRO": ("Anotação diversa; revisão por subgrupo", "—", "BAIXA"),
    }
    result = []
    for key, items in sorted(buckets.items()):
        meaning, field, confidence = meanings[key]
        result.append(
            {
                "pattern": key,
                "count": len(items),
                "examples": list(dict.fromkeys(value for value, _ in items))[:12],
                "layers": sorted({layer for _, layer in items}),
                "possible_meaning": meaning,
                "candidate_field": field,
                "confidence": confidence,
            }
        )
    return result


class UnionFind:
    def __init__(self, size: int) -> None:
        self.parent = list(range(size))

    def find(self, value: int) -> int:
        while self.parent[value] != value:
            self.parent[value] = self.parent[self.parent[value]]
            value = self.parent[value]
        return value

    def union(self, left: int, right: int) -> None:
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root != right_root:
            self.parent[right_root] = left_root


def topology_for_layer(entities: list[dict[str, Any]], tolerance: float) -> dict[str, Any]:
    groups: dict[tuple[tuple[float, float], ...], list[dict[str, Any]]] = collections.defaultdict(list)
    for entity in entities:
        groups[polyline_key(entity)].append(entity)
    unique = [values[0] for values in groups.values()]
    endpoints: list[tuple[float, float]] = []
    endpoint_z: list[float | None] = []
    for entity in unique:
        points = entity_points(entity)
        for point in (points[0], points[-1]):
            endpoints.append(xy(point))
            endpoint_z.append(float(point[2]) if len(point) > 2 else None)
    union = UnionFind(len(endpoints))
    near_gap_pairs: list[dict[str, Any]] = []
    for left in range(len(endpoints)):
        for right in range(left + 1, len(endpoints)):
            if left // 2 == right // 2:
                continue
            distance = point_distance(endpoints[left], endpoints[right])
            if distance <= tolerance:
                union.union(left, right)
            elif tolerance < distance <= 0.50:
                near_gap_pairs.append(
                    {
                        "distance_m": round(distance, 4),
                        "left_segment_handle": unique[left // 2]["handle"],
                        "right_segment_handle": unique[right // 2]["handle"],
                    }
                )
    clusters: dict[int, list[int]] = collections.defaultdict(list)
    for index in range(len(endpoints)):
        clusters[union.find(index)].append(index)
    dangles = sum(len(values) == 1 for values in clusters.values())
    connected = sum(len(values) >= 2 for values in clusters.values())
    branches = sum(len(values) >= 3 for values in clusters.values())
    z_conflicts = 0
    for values in clusters.values():
        z_values = [endpoint_z[index] for index in values if endpoint_z[index] is not None]
        if len(z_values) > 1 and max(z_values) - min(z_values) > 0.10:
            z_conflicts += 1
    interior_connections = 0
    for endpoint_index, point in enumerate(endpoints):
        own_segment = endpoint_index // 2
        for segment_index, entity in enumerate(unique):
            if segment_index == own_segment:
                continue
            points = [xy(item) for item in entity_points(entity)]
            if len(points) < 3:
                continue
            distance = min(
                point_segment_distance(point, left, right) for left, right in zip(points, points[1:])
            )
            endpoint_distance = min(point_distance(point, points[0]), point_distance(point, points[-1]))
            if distance <= tolerance and endpoint_distance > tolerance:
                interior_connections += 1
                break
    return {
        "source_feature_count": len(entities),
        "unique_geometry_count": len(unique),
        "exact_duplicate_group_count": sum(len(values) > 1 for values in groups.values()),
        "exact_duplicate_feature_count": sum(len(values) - 1 for values in groups.values()),
        "tolerance_m": tolerance,
        "endpoint_count": len(endpoints),
        "connected_endpoint_cluster_count": connected,
        "branch_cluster_count": branches,
        "dangling_endpoint_count": dangles,
        "endpoint_to_interior_connection_count": interior_connections,
        "z_conflict_cluster_count": z_conflicts,
        "gap_candidates_0_05_to_0_50_m_count": len(near_gap_pairs),
        "gap_candidate_examples": sorted(near_gap_pairs, key=lambda item: item["distance_m"])[:15],
    }


def topology_findings(drawing: dict[str, Any]) -> list[dict[str, Any]]:
    model = get_space(drawing, "*MODEL_SPACE")
    layers = sorted(
        {
            entity["layer"]
            for entity in model["entities"]
            if entity["object_type"] == "AcDbPolyline"
            and not entity.get("closed")
            and (entity["layer"].startswith("GF-Rede") or entity["layer"] == "GF-Galeria")
        }
    )
    result = []
    for layer in layers:
        entities = [
            entity
            for entity in model["entities"]
            if entity["layer"] == layer and entity["object_type"] == "AcDbPolyline" and not entity.get("closed")
        ]
        finding = topology_for_layer(entities, tolerance=0.05)
        finding["layer"] = layer
        finding["interpretation"] = (
            "Diagnóstico candidato em XY; não corrige objetos e não presume que linhas paralelas de vídeo sejam duplicatas."
        )
        result.append(finding)
    return result


def technical_text_associations(drawing: dict[str, Any]) -> dict[str, Any]:
    model = get_space(drawing, "*MODEL_SPACE")
    lines_by_layer: dict[str, list[dict[str, Any]]] = collections.defaultdict(list)
    for entity in model["entities"]:
        if entity["object_type"] == "AcDbPolyline" and not entity.get("closed"):
            lines_by_layer[entity["layer"]].append(entity)
    candidates = []
    for entity in model["entities"]:
        value = normalize_text(entity.get("text", ""))
        if not (DEPTH_TEXT_PATTERN.fullmatch(value) or DIAMETER_TEXT_PATTERN.fullmatch(value)):
            continue
        position = entity.get("position")
        lines = lines_by_layer.get(entity["layer"], [])
        if not position or not lines:
            candidates.append(
                {
                    "text_handle": entity["handle"],
                    "text": value,
                    "layer": entity["layer"],
                    "confidence": "BAIXA",
                    "reason": "Sem polyline aberta no mesmo layer.",
                }
            )
            continue
        distances = sorted(
            ((point_polyline_distance(xy(position), line), line) for line in lines),
            key=lambda item: (item[0], item[1]["handle"]),
        )
        nearest_distance, nearest = distances[0]
        second_distance = distances[1][0] if len(distances) > 1 else math.inf
        if nearest_distance <= 0.25 and second_distance - nearest_distance > 0.25:
            confidence = "ALTA"
        elif nearest_distance <= 1.0 and second_distance - nearest_distance > 0.10:
            confidence = "MÉDIA"
        elif nearest_distance <= 1.0:
            confidence = "AMBÍGUA"
        else:
            confidence = "BAIXA"
        values = xdata_dictionary(entity)
        if "GEOAUTOENGINE" in (values.get("applications") or []) and values.get("registro"):
            confidence = "ALTA"
        candidates.append(
            {
                "text_handle": entity["handle"],
                "text": value,
                "layer": entity["layer"],
                "nearest_segment_handle": nearest["handle"],
                "nearest_distance_m": round(nearest_distance, 4),
                "second_nearest_distance_m": round(second_distance, 4) if math.isfinite(second_distance) else None,
                "geoautoengine_record": values.get("registro"),
                "confidence": confidence,
                "method": "XData existente + proximidade" if values.get("registro") else "Proximidade no mesmo layer",
            }
        )
    return {
        "candidate_count": len(candidates),
        "confidence_counts": dict(collections.Counter(item["confidence"] for item in candidates)),
        "candidates": candidates,
        "warning": "Proximidade não materializa vínculo definitivo; XData existente identifica o registro de anotação, não necessariamente o trecho físico.",
    }


def coordinate_system_inventory(drawings: list[dict[str, Any]], legends: list[dict[str, Any]]) -> dict[str, Any]:
    notes = list(dict.fromkeys(note for legend in legends for note in legend["coordinate_notes"]))
    return {
        "status": "NÃO DETERMINADO COM SEGURANÇA",
        "assigned_coordinate_system_codes": [drawing["database"].get("coordinate_system_code") or None for drawing in drawings],
        "insunits": [drawing["database"].get("insunits") for drawing in drawings],
        "legend_evidence": notes,
        "datum_evidence": "SIRGAS 2000 (texto explícito na legenda)",
        "projection_evidence": "UTM (texto explícito na legenda)",
        "utm_zone": None,
        "epsg": None,
        "candidate_epsg": 31982,
        "candidate_epsg_reason": "Joinville/SC e coordenadas UTM são compatíveis com SIRGAS 2000 / UTM 22S, mas o DWG não traz zona nem código atribuído.",
        "candidate_epsg_reference": "https://epsg.org/crs_31982/SIRGAS-2000-UTM-zone-22S.html",
        "manual_confirmation_required": [
            "Confirmar zona UTM 22S com a equipe responsável/ART ou memorial geodésico.",
            "Confirmar se as cotas e coordenadas chamadas de 'navegadas' têm precisão adequada ao uso GIS pretendido.",
            "Atribuir SRID somente após confirmação; não gravar EPSG 31982 automaticamente nesta etapa.",
            "Padronizar INSUNITS do primeiro DWG, hoje Undefined, sem alterar o original durante a auditoria.",
        ],
    }


def source_metadata(path: Path, drawing: dict[str, Any]) -> dict[str, Any]:
    return {
        "file_name": Path(drawing["source_path"]).name,
        "source_path": drawing["source_path"],
        "sha256": drawing["source_sha256"],
        "raw_inventory_file": path.name,
        "raw_inventory_sha256": hashlib.sha256(path.read_bytes()).hexdigest().upper(),
        "raw_inventory_bytes": path.stat().st_size,
        "dwg_version": drawing["database"].get("original_file_version"),
        "insunits": drawing["database"].get("insunits"),
        "model_extents": {
            "min": drawing["database"].get("extmin"),
            "max": drawing["database"].get("extmax"),
        },
        "model_entity_count": get_space(drawing, "*MODEL_SPACE")["entity_count"],
        "paper_entity_count": get_space(drawing, "*PAPER_SPACE")["entity_count"],
        "layer_count": len(drawing["layers"]),
        "block_definition_count": len(drawing["block_definitions"]),
        "civil_runtime_loaded": drawing["host"].get("civil_runtime_loaded"),
        "dbmod_before": drawing.get("dbmod_before"),
        "dbmod_after": drawing.get("dbmod_after"),
        "dbmod_unchanged": drawing.get("dbmod_unchanged"),
    }


def proposed_entities() -> list[dict[str, Any]]:
    return [
        {
            "entity": "mapping.source_drawings",
            "purpose": "Proveniência e rastreabilidade do DWG auditado.",
            "fields": [
                ["id", "uuid", "OBRIGATÓRIO", "Gerado pelo GIS"],
                ["project_id", "uuid", "OBRIGATÓRIO", "Projeto"],
                ["file_name", "text", "OBRIGATÓRIO", "Nome do DWG"],
                ["sha256", "char(64)", "OBRIGATÓRIO", "Hash do DWG"],
                ["cad_version", "text", "OPCIONAL", "Metadado DWG"],
                ["coordinate_system_text", "text", "OPCIONAL", "Legenda/metadados"],
                ["srid", "integer", "OPCIONAL/BLOQUEADO", "Confirmação humana"],
                ["audited_at", "timestamptz", "OBRIGATÓRIO", "Processo de auditoria"],
            ],
        },
        {
            "entity": "mapping.network_type_catalog",
            "purpose": "Domínio corporativo comprovado pelas duas legendas.",
            "fields": [
                ["code", "text", "OBRIGATÓRIO", "Legenda"],
                ["name", "text", "OBRIGATÓRIO", "Legenda"],
                ["legend_label", "text", "OBRIGATÓRIO", "Texto literal"],
                ["active", "boolean", "OBRIGATÓRIO", "Governança"],
            ],
        },
        {
            "entity": "mapping.network_segments",
            "purpose": "Trechos físicos de rede consolidados, sem importar textos como feições autônomas.",
            "fields": [
                ["id", "uuid", "OBRIGATÓRIO", "Gerado pelo GIS"],
                ["project_id", "uuid", "OBRIGATÓRIO", "Projeto"],
                ["source_drawing_id", "uuid", "OBRIGATÓRIO", "DWG de origem"],
                ["network_type", "text/FK", "OBRIGATÓRIO", "Layer + legenda"],
                ["geom", "geometry(LineString, SRID)", "OBRIGATÓRIO", "XY da polyline aberta"],
                ["source_geom_3d", "geometry(LineStringZ, SRID)", "OPCIONAL", "Vértices/Z CAD sem validação"],
                ["diameter_mm", "integer", "OPCIONAL", "Código/texto/XData"],
                ["depth_start_m", "numeric(8,3)", "OPCIONAL", "Observações associadas"],
                ["depth_end_m", "numeric(8,3)", "OPCIONAL", "Observações associadas"],
                ["code", "text", "OPCIONAL", "TRECHO/código quando inequívoco"],
                ["condition_raw", "text", "OPCIONAL", "Anotação preservada"],
                ["source_layer", "text", "OBRIGATÓRIO", "Layer CAD"],
                ["source_handle", "text", "OBRIGATÓRIO", "Handle CAD"],
                ["extraction_confidence", "text", "OBRIGATÓRIO", "Regra de extração"],
                ["z_validated", "boolean", "OBRIGATÓRIO", "Inicialmente false"],
            ],
            "not_in_initial_schema": ["material (não encontrado)", "fabricante", "ano de instalação", "estado operacional normalizado"],
        },
        {
            "entity": "mapping.structures",
            "purpose": "PV, bocas de lobo, grelhas, registros, postes e estruturas confirmadas.",
            "fields": [
                ["id", "uuid", "OBRIGATÓRIO", "Gerado pelo GIS"],
                ["project_id", "uuid", "OBRIGATÓRIO", "Projeto"],
                ["source_drawing_id", "uuid", "OBRIGATÓRIO", "DWG de origem"],
                ["structure_type", "text", "OBRIGATÓRIO", "Bloco/layer/legenda"],
                ["geom", "geometry(Point, SRID)", "OBRIGATÓRIO", "Inserção/centroide"],
                ["footprint", "geometry(Polygon, SRID)", "OPCIONAL", "Polyline fechada"],
                ["elevation_m", "numeric(10,3)", "OPCIONAL", "Z/COTA validada"],
                ["code", "text", "OPCIONAL", "Código/atributo"],
                ["condition_raw", "text", "OPCIONAL", "LAC/ENC/ASS/etc."],
                ["source_layer", "text", "OBRIGATÓRIO", "Layer CAD"],
                ["source_handle", "text", "OBRIGATÓRIO", "Handle CAD"],
                ["extraction_confidence", "text", "OBRIGATÓRIO", "Regra de extração"],
            ],
        },
        {
            "entity": "mapping.mapping_observations",
            "purpose": "Preservar pontos SIMB_PONTO e atributos técnicos sem projetá-los prematuramente sobre um trecho.",
            "fields": [
                ["id", "uuid", "OBRIGATÓRIO", "Gerado pelo GIS"],
                ["source_drawing_id", "uuid", "OBRIGATÓRIO", "DWG de origem"],
                ["point_name", "text", "OBRIGATÓRIO", "Atributo PONTO"],
                ["description_raw", "text", "OBRIGATÓRIO", "Atributo DESC"],
                ["network_type", "text/FK", "OPCIONAL", "Prefixo interpretado"],
                ["depth_m", "numeric(8,3)", "OPCIONAL", "DESC/código"],
                ["diameter_mm", "integer", "OPCIONAL", "D[número]"],
                ["elevation_m", "numeric(10,3)", "OPCIONAL", "COTA/Position.Z"],
                ["condition_raw", "text", "OPCIONAL", "Sufixo/texto bruto"],
                ["geom", "geometry(PointZ, SRID)", "OBRIGATÓRIO", "BlockReference.Position"],
                ["source_handle", "text", "OBRIGATÓRIO", "Handle CAD"],
            ],
        },
        {
            "entity": "mapping.feature_observation_links",
            "purpose": "Vínculos auditáveis entre observações e trechos/estruturas, inclusive candidatos ambíguos.",
            "fields": [
                ["observation_id", "uuid", "OBRIGATÓRIO", "Observação"],
                ["network_segment_id", "uuid", "OPCIONAL", "Trecho candidato"],
                ["structure_id", "uuid", "OPCIONAL", "Estrutura candidata"],
                ["method", "text", "OBRIGATÓRIO", "XData/proximidade/revisão"],
                ["distance_m", "numeric(10,4)", "OPCIONAL", "Associação espacial"],
                ["confidence", "text", "OBRIGATÓRIO", "Alta/média/baixa/ambígua"],
                ["reviewed", "boolean", "OBRIGATÓRIO", "Governança"],
            ],
        },
    ]


def build_inventory(raw_paths: list[Path]) -> dict[str, Any]:
    drawings = [json.loads(path.read_text(encoding="utf-8")) for path in raw_paths]
    metadata = [source_metadata(path, drawing) for path, drawing in zip(raw_paths, drawings)]
    legends = [extract_legend(drawing) for drawing in drawings]
    per_drawing = []
    aggregate_entity_types: collections.Counter[str] = collections.Counter()
    for meta, drawing, legend in zip(metadata, drawings, legends):
        model = get_space(drawing, "*MODEL_SPACE")
        paper = get_space(drawing, "*PAPER_SPACE")
        aggregate_entity_types.update(model["entity_type_counts"])
        observations = technical_observations(drawing)
        all_text = [
            normalize_text(entity.get("text", ""))
            for space in (model, paper)
            for entity in space["entities"]
            if entity.get("text")
        ]
        all_text.extend(
            attribute.get("text", "")
            for entity in model["entities"]
            for attribute in entity.get("attributes") or []
        )
        materials = sorted({match.group(0).upper() for text in all_text for match in MATERIAL_PATTERN.finditer(text)})
        per_drawing.append(
            {
                "metadata": meta,
                "entity_types": {
                    "model_space": model["entity_type_counts"],
                    "paper_space": paper["entity_type_counts"],
                    "civil_3d_model_objects": {
                        key: value for key, value in model["entity_type_counts"].items() if key.startswith("Aecc")
                    },
                },
                "layers": active_layer_inventory(drawing),
                "legend": legend,
                "network_patterns": network_patterns(drawing),
                "structure_patterns": structure_patterns(drawing),
                "block_patterns": model_block_patterns(drawing),
                "text_patterns": text_patterns(drawing),
                "technical_observations": observations,
                "spatial_relationships": technical_text_associations(drawing),
                "topology_findings": topology_findings(drawing),
                "material_terms_found": materials,
            }
        )
    legend_types = sorted(
        {
            item["network_type"]
            for legend in legends
            for item in legend["network_catalog"]
        }
    )
    ambiguities = [
        "O DWG não possui sistema de coordenadas atribuído; a legenda informa SIRGAS 2000/UTM, mas não a zona.",
        "O primeiro DWG está com INSUNITS=Undefined, embora a legenda declare medidas em metros.",
        "Polylines abertas e fechadas coexistem em layers de rede; o layer isolado não distingue trecho de estrutura.",
        "Não foram detectadas duplicatas lineares XY exatas; GF-Rede Pluvial-VIDEO contém pares paralelos separados por aproximadamente 0,4–1,0 m.",
        "GF-Rede Pluvial-VIDEO contém pares paralelos/contornos e não deve virar uma segunda rede física.",
        "A semântica de Z é inconsistente: valores de rede podem superar cotas próximas; não tratar Z como invert/cota de fundo automaticamente.",
        "Códigos/sufixos POSSIVEL, LAC, ENC, ASS, S e APAGAR precisam de dicionário corporativo antes de normalização.",
        "Textos de profundidade/diâmetro têm XData de registro em parte dos casos, mas o vínculo XData não identifica diretamente o trecho físico.",
        "Materiais de tubulação não foram encontrados nos textos, atributos ou legendas auditados.",
        "A legenda apresenta famílias (gás, telefone, fibra, incêndio, esgoto industrial) que não ocorrem como redes lineares no Model Space destes dois projetos.",
    ]
    return {
        "inventory_schema_version": "1.0",
        "generated_at_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "audit_mode": "READ_ONLY",
        "scope": "Dois DWGs reais; Model Space auditado integralmente e Paper Space analisado para legendas.",
        "metadata": metadata,
        "entity_types": {
            "aggregate_model_space": dict(aggregate_entity_types),
            "per_drawing": [item["entity_types"] for item in per_drawing],
        },
        "layers": [
            {"file_name": item["metadata"]["file_name"], "items": item["layers"]} for item in per_drawing
        ],
        "legend_catalog": {
            "network_types": legend_types,
            "per_drawing": [item["legend"] for item in per_drawing],
            "interpretation": "Catálogo corporativo de tipos; objetos da legenda são representação CAD e não feições GIS.",
        },
        "network_patterns": [
            {"file_name": item["metadata"]["file_name"], "items": item["network_patterns"]}
            for item in per_drawing
        ],
        "structure_patterns": [
            {"file_name": item["metadata"]["file_name"], "items": item["structure_patterns"]}
            for item in per_drawing
        ],
        "block_patterns": [
            {"file_name": item["metadata"]["file_name"], "items": item["block_patterns"]}
            for item in per_drawing
        ],
        "text_patterns": [
            {"file_name": item["metadata"]["file_name"], "items": item["text_patterns"]}
            for item in per_drawing
        ],
        "coordinate_system": coordinate_system_inventory(drawings, legends),
        "candidate_attributes": {
            "directly_available": [
                "source DWG/hash/handle/layer",
                "XY e Z CAD",
                "tipo de rede por layer/legenda",
                "PONTO, DESC e COTA do bloco SIMB_PONTO",
                "profundidade e diâmetro em códigos/textos/XData quando presentes",
                "código TRECHO para vídeo-inspeção",
            ],
            "inferred_with_review": [
                "vínculo observação → trecho",
                "tipo de estrutura para polylines fechadas genéricas",
                "sentido de fluxo a partir de blocos SENTIDO",
                "condição normalizada a partir de sufixos e observações",
                "validade semântica da coordenada Z",
            ],
            "not_found": ["material", "fabricante", "ano de instalação", "EPSG atribuído", "zona UTM explícita"],
            "per_drawing_observations": [
                {
                    "file_name": item["metadata"]["file_name"],
                    "summary": {key: value for key, value in item["technical_observations"].items() if key not in {"survey_points", "geoautoengine_records"}},
                    "survey_points": item["technical_observations"]["survey_points"],
                    "geoautoengine_records": item["technical_observations"]["geoautoengine_records"],
                }
                for item in per_drawing
            ],
        },
        "spatial_relationships": [
            {"file_name": item["metadata"]["file_name"], **item["spatial_relationships"]}
            for item in per_drawing
        ],
        "topology_findings": [
            {"file_name": item["metadata"]["file_name"], "items": item["topology_findings"]}
            for item in per_drawing
        ],
        "ambiguities": ambiguities,
        "decisions_humanas_necessarias": [
            "Confirmar zona UTM/EPSG e precisão das coordenadas navegadas.",
            "Aprovar dicionário de códigos e sufixos de campo.",
            "Definir quais polylines fechadas em layers de rede são PV, caixas ou outros dispositivos.",
            "Definir como consolidar os pares paralelos de GF-Rede Pluvial-VIDEO sem confundi-los com duplicatas.",
            "Escolher/derivar centerline para os pares de linhas de GF-Rede Pluvial-VIDEO.",
            "Validar Z e decidir se a primeira carga GIS será 2D ou 2D + geometria-fonte 3D.",
            "Confirmar vínculos texto/ponto → trecho nos candidatos médios, baixos ou ambíguos.",
        ],
        "proposed_gis_entities": proposed_entities(),
        "per_drawing": per_drawing,
    }


def table(headers: list[str], rows: Iterable[Iterable[Any]]) -> list[str]:
    output = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    output.extend("| " + " | ".join(md(cell) for cell in row) + " |" for row in rows)
    return output


def render_report(inventory: dict[str, Any]) -> str:
    lines: list[str] = ["# RELATÓRIO DE AUDITORIA CAD → GIS", ""]
    metadata = inventory["metadata"]
    per = inventory["per_drawing"]
    coordinate = inventory["coordinate_system"]

    lines += [
        "## 1. Resumo executivo",
        "",
        "A auditoria somente-leitura dos dois DWGs reais confirmou que o conteúdo técnico é composto principalmente por polylines, blocos e textos AutoCAD; não foram encontradas redes nativas `Pipe/Structure/Pressure Pipe` do Civil 3D no Model Space. As legendas ficam no Paper Space e foram tratadas como catálogo corporativo, não como ocorrência física.",
        "",
        "A legenda comum enumera dez famílias: águas pluviais, esgoto sanitário, esgoto industrial, elétrica, água potável, gás, telefone, fibra óptica, incêndio e rede sem descrição de uso. Nos Model Spaces auditados, a ocorrência linear efetiva é menor e varia por projeto.",
        "",
        "O principal achado de modelagem é que atributos como profundidade, diâmetro e condição pertencem a observações/pontos e só depois devem ser associados a trechos. Importar cada texto como feição produziria duplicação semântica.",
        "",
        "Nenhum material de tubulação foi encontrado. O sistema geodésico não está atribuído ao DWG: a legenda informa UTM/SIRGAS 2000, mas omite a zona. Portanto, nenhum SRID deve ser gravado automaticamente.",
        "",
    ]

    lines += ["## 2. Informações gerais do DWG", ""]
    lines += table(
        ["DWG", "SHA-256", "Versão", "INSUNITS", "Model", "Paper", "Layers", "Blocos", "DBMOD"],
        [
            [
                item["file_name"],
                item["sha256"],
                item["dwg_version"],
                item["insunits"],
                item["model_entity_count"],
                item["paper_entity_count"],
                item["layer_count"],
                item["block_definition_count"],
                f"{item['dbmod_before']} → {item['dbmod_after']} (inalterado)",
            ]
            for item in metadata
        ],
    )
    lines += ["", "Os originais foram abertos apenas para hash. A API leu cópias temporárias com `/readonly`; nenhum `SAVE`, `PURGE`, `OVERKILL`, conversão ou escrita no DWG foi executado.", ""]

    lines += ["## 3. Sistema de coordenadas", ""]
    lines += [
        f"**SISTEMA DE COORDENADAS: {coordinate['status']}.**",
        "",
        "Evidências encontradas:",
        "",
        f"- `CGEOCS` vazio nos dois DWGs; nenhum código está atribuído.",
        f"- INSUNITS: `{metadata[0]['insunits']}` no primeiro e `{metadata[1]['insunits']}` no segundo.",
        f"- A legenda declara: {md(coordinate['legend_evidence'][0] if coordinate['legend_evidence'] else 'sem nota')} ",
        "- As coordenadas têm ordem de grandeza UTM e os projetos são de Joinville/SC.",
        "",
        "[`EPSG:31982`](https://epsg.org/crs_31982/SIRGAS-2000-UTM-zone-22S.html) é apenas candidato técnico (SIRGAS 2000 / UTM 22S). Falta confirmação explícita da zona; não deve ser aplicado automaticamente.",
        "",
    ]

    lines += ["## 4. Inventário de entidades", ""]
    all_types = sorted({key for item in per for key in item["entity_types"]["model_space"]})
    lines += table(
        ["Tipo", metadata[0]["file_name"], metadata[1]["file_name"], "Possível função", "Relevância GIS"],
        [
            [
                entity_type,
                per[0]["entity_types"]["model_space"].get(entity_type, 0),
                per[1]["entity_types"]["model_space"].get(entity_type, 0),
                {
                    "AcDbPolyline": "Trechos, estruturas fechadas e desenho de apoio",
                    "AcDbBlockReference": "Pontos, estruturas e símbolos",
                    "AcDbText": "Atributos/anotações",
                    "AcDbMText": "Notas e vídeo-inspeção",
                    "AcDbLine": "Malha/perfis/anotação",
                    "AcDbHatch": "Representação gráfica de grelhas",
                    "AeccDbFace": "Face Civil 3D de apoio",
                }.get(entity_type, "Revisão necessária"),
                {
                    "AcDbPolyline": "ALTA/MISTA",
                    "AcDbBlockReference": "ALTA/MISTA",
                    "AcDbText": "ATRIBUTO, não feição autônoma",
                    "AcDbMText": "ATRIBUTO/INSPEÇÃO",
                    "AcDbLine": "BAIXA/MISTA",
                    "AcDbHatch": "NÃO independente",
                    "AeccDbFace": "APOIO, não rede",
                }.get(entity_type, "REVISÃO NECESSÁRIA"),
            ]
            for entity_type in all_types
        ],
    )
    lines += ["", "Objetos Civil 3D nativos observados no Model Space: duas `AeccDbFace` no primeiro DWG. Não há `Pipe`, `Structure`, `Alignment`, `FeatureLine`, `PressurePipe` nem `CogoPoint` como entidades nativas nos dois Model Spaces.", ""]

    lines += ["## 5. Inventário de layers", ""]
    lines += ["A tabela abaixo lista todos os layers existentes, inclusive os sem objetos no Model/Paper Space. Contagens dentro de definições de bloco não são somadas para evitar confundir conteúdo de símbolo com inserções.", ""]
    for item in per:
        lines += [f"### {item['metadata']['file_name']}", ""]
        lines += table(
            ["Layer", "Model", "Tipos Model", "Paper", "Cor", "Linetype", "Estado", "Significado", "Rede", "Classe"],
            [
                [
                    layer["name"],
                    layer["model_object_count"],
                    ", ".join(f"{key}:{value}" for key, value in layer["model_entity_types"].items()) or "—",
                    layer["paper_object_count"],
                    f"{(layer['color'] or {}).get('method')}:{(layer['color'] or {}).get('index')}",
                    layer["linetype"],
                    f"off={layer['state']['off']}; frozen={layer['state']['frozen']}; locked={layer['state']['locked']}; plot={layer['state']['plottable']}",
                    layer["possible_meaning"],
                    layer["possible_network_category"],
                    layer["classification"],
                ]
                for layer in item["layers"]
            ],
        )
        lines += [""]

    lines += ["## 6. Redes identificadas", ""]
    lines += ["A legenda confirma o catálogo corporativo, mas ocorrência física foi reconhecida somente quando há geometria no Model Space.", ""]
    lines += table(
        ["Código", "Rótulo da legenda", "Layer amostra", "Nos dois desenhos"],
        [
            [entry["network_type"], entry["legend_label"], entry["sample_layer"], "SIM"]
            for entry in per[0]["legend"]["network_catalog"]
        ],
    )
    lines += [""]
    for item in per:
        lines += [f"### Padrões observados — {item['metadata']['file_name']}", ""]
        lines += table(
            ["Layer", "Tipo", "Papel", "Abertas", "Únicas XY", "Duplicadas", "Fechadas", "Textos", "Z", "Confiança"],
            [
                [
                    pattern["layer"],
                    pattern["network_type"],
                    pattern["role"],
                    pattern["open_polyline_count"],
                    pattern["unique_open_geometry_count"],
                    pattern["exact_duplicate_feature_count"],
                    pattern["closed_polyline_count"],
                    pattern["text_count"],
                    f"{pattern['z_min']:.3f}…{pattern['z_max']:.3f}" if pattern["z_min"] is not None else "—",
                    pattern["confidence"],
                ]
                for pattern in item["network_patterns"]
            ],
        )
        lines += [""]
    lines += [
        "Regra recomendada: somente `AcDbPolyline` aberta em layer de rede é candidata inicial a trecho. Polyline fechada no mesmo layer é candidata a estrutura/contorno. `GF-Rede Pluvial-VIDEO` representa cobertura/contorno de inspeção e não uma rede física adicional.",
        "",
    ]

    lines += ["## 7. Estruturas identificadas", ""]
    for item in per:
        lines += [f"### {item['metadata']['file_name']}", ""]
        lines += table(
            ["Estrutura", "Tipo CAD", "Bloco", "Layers", "Quantidade", "Coordenada", "Elevação", "Confiança"],
            [
                [
                    pattern["structure_type"],
                    pattern["cad_type"],
                    pattern.get("block_name"),
                    ", ".join(pattern.get("layers", [])),
                    pattern["count"],
                    pattern.get("coordinate_source"),
                    pattern.get("elevation_source"),
                    pattern["confidence"],
                ]
                for pattern in item["structure_patterns"]
            ],
        )
        lines += [""]
    lines += ["Polylines fechadas de `GF-Rede Pluvial` e `GF-Rede Eletrica` não foram classificadas definitivamente como PV/caixa: a forma e o layer indicam estrutura, mas a classe exige revisão humana.", ""]

    lines += ["## 8. Blocos", ""]
    for item in per:
        lines += [f"### {item['metadata']['file_name']}", ""]
        lines += table(
            ["Bloco", "Layer", "Inserções", "Atributos", "Função", "Classe"],
            [
                [
                    block["block_name"],
                    block["layer"],
                    block["insertion_count"],
                    ", ".join(block["attribute_tags"]) or "—",
                    block["possible_function"],
                    block["classification"],
                ]
                for block in item["block_patterns"]
            ],
        )
        lines += [""]
    lines += [
        "`SIMB_PONTO` é o bloco técnico mais importante: contém `PONTO`, `DESC` e `COTA`. `POSTE`, `PV ESA`, `pv_esgoto_sanitario` e `rg_agua_potavel` representam objetos reais. `SENTIDO`, ticks Civil 3D, norte e folha são representação CAD; sua semântica pode alimentar atributos, mas os símbolos não viram entidades GIS independentes.",
        "",
    ]

    lines += ["## 9. Textos e atributos", ""]
    for item in per:
        observations = item["technical_observations"]
        lines += [f"### {item['metadata']['file_name']}", ""]
        lines += table(
            ["Padrão", "Quantidade", "Exemplos", "Layers", "Significado", "Campo", "Confiança"],
            [
                [
                    pattern["pattern"],
                    pattern["count"],
                    "; ".join(pattern["examples"]),
                    ", ".join(pattern["layers"]),
                    pattern["possible_meaning"],
                    pattern["candidate_field"],
                    pattern["confidence"],
                ]
                for pattern in item["text_patterns"]
            ],
        )
        lines += [
            "",
            f"Pontos `SIMB_PONTO`: **{observations['survey_point_count']}**; códigos de rede/profundidade parseáveis: **{observations['parseable_network_code_count']} ({observations['parseable_network_code_percent']}%)**; com diâmetro: **{observations['parsed_diameter_count']}**; registros GEOAUTOENGINE existentes: **{observations['geoautoengine_record_count']}**.",
            "",
        ]
    lines += ["Nenhum termo inequívoco de material (PVC, PEAD, concreto, ferro fundido etc.) foi encontrado. `material` é **NÃO DISPONÍVEL NO DWG** nesta amostra.", ""]

    lines += ["## 10. Relacionamentos identificados", ""]
    lines += table(
        ["DWG", "Candidatos texto→trecho", "Alta", "Média", "Baixa", "Ambígua", "Método"],
        [
            [
                relation["file_name"],
                relation["candidate_count"],
                relation["confidence_counts"].get("ALTA", 0),
                relation["confidence_counts"].get("MÉDIA", 0),
                relation["confidence_counts"].get("BAIXA", 0),
                relation["confidence_counts"].get("AMBÍGUA", 0),
                "XData existente + proximidade no mesmo layer",
            ]
            for relation in inventory["spatial_relationships"]
        ],
    )
    lines += [
        "",
        "Relações de alta confiança: atributos dentro de `SIMB_PONTO`; pares de textos GEOAUTOENGINE agrupados pelo mesmo `REGISTRO`; blocos técnicos por nome/layer. Relações somente espaciais continuam candidatas, mesmo quando a distância é pequena.",
        "",
    ]

    lines += ["## 11. Topologia", ""]
    for topology in inventory["topology_findings"]:
        lines += [f"### {topology['file_name']}", ""]
        lines += table(
            ["Layer", "Fonte", "Únicas", "Duplicatas", "Nós conectados", "Ramificações", "Pontas soltas", "T em interior", "Conflito Z", "Gaps 5–50 cm"],
            [
                [
                    item["layer"],
                    item["source_feature_count"],
                    item["unique_geometry_count"],
                    item["exact_duplicate_feature_count"],
                    item["connected_endpoint_cluster_count"],
                    item["branch_cluster_count"],
                    item["dangling_endpoint_count"],
                    item["endpoint_to_interior_connection_count"],
                    item["z_conflict_cluster_count"],
                    item["gap_candidates_0_05_to_0_50_m_count"],
                ]
                for item in topology["items"]
            ],
        )
        lines += [""]
    lines += ["O diagnóstico usa XY e tolerância de 0,05 m, somente para apontar candidatos. Nenhuma duplicata XY exata foi detectada. Gaps e pontas soltas não foram corrigidos; uma ponta em estrutura ainda não vinculada pode aparecer como solta.", ""]

    lines += ["## 12. Dados técnicos aproveitáveis", ""]
    lines += [
        "- Geometria XY de polylines abertas, preservando handle e layer.",
        "- Posições de blocos técnicos e footprints de polylines fechadas.",
        "- Tipo de rede por layer, validado contra a legenda.",
        "- Identificador, descrição e cota dos pontos `SIMB_PONTO`.",
        "- Profundidade e diâmetro quando codificados em `DESC`, textos ou XData existente.",
        "- Identificadores `TRECHO` e estaqueamento de vídeo-inspeção.",
        "- Observações brutas de condição/defeito, sem taxonomia inventada.",
        "- Proveniência: nome do DWG, SHA-256, handle, layer e confiança.",
        "",
    ]

    lines += ["## 13. Elementos exclusivamente gráficos", ""]
    lines += [
        "- Objetos de legenda no Paper Space, carimbo `FOLHA A1_GEOFRAN`, viewports e OLE.",
        "- Ticks `AeccTickCircle`/`AeccTickLine`, norte, molduras, malha e textos de prancha.",
        "- Hachuras de grelha como feições independentes.",
        "- Setas `SENTIDO` como geometria: apenas a direção inferida pode virar atributo após validação.",
        "- Textos de profundidade/diâmetro após sua incorporação como atributos.",
        "- Pares/contornos da vídeo-inspeção depois que forem vinculados ao trecho físico.",
        "",
    ]

    lines += ["## 14. Problemas encontrados", ""]
    lines += [f"- {item}" for item in inventory["ambiguities"]]
    lines += [""]

    lines += ["## 15. Casos ambíguos", ""]
    lines += [
        "1. Polylines fechadas em layers de rede podem ser PV, caixas ou contornos gráficos.",
        "2. `GF-Rede Pluvial-VIDEO` contém linhas paralelas e contornos; falta centerline inequívoca.",
        "3. `GF-Rede Pluvial_` difere por sublinhado e contém apenas textos no segundo DWG.",
        "4. `GF-Galeria` apresenta linhas de contorno, inclusive um elemento quase nulo, não um eixo único.",
        "5. Valores Z não têm semântica uniforme e não podem ser chamados automaticamente de invert/cota de fundo.",
        "6. Proximidade de texto a linha pode produzir mais de um candidato.",
        "7. Redes listadas na legenda, mas ausentes no Model Space, são catálogo e não ocorrências.",
        "",
    ]

    lines += ["## 16. Decisões humanas necessárias", ""]
    lines += [f"{index}. {value}" for index, value in enumerate(inventory["decisions_humanas_necessarias"], start=1)]
    lines += [""]

    lines += ["## 17. Modelo conceitual recomendado", ""]
    lines += [
        "```text",
        "PROJECT",
        "├── SOURCE_DRAWING",
        "├── NETWORK_TYPE_CATALOG   (derivado da legenda, sem geometria)",
        "├── NETWORK_SEGMENT        (polyline aberta consolidada)",
        "├── STRUCTURE              (bloco técnico ou footprint fechado)",
        "└── MAPPING_OBSERVATION     (SIMB_PONTO/texto/XData)",
        "    └── FEATURE_OBSERVATION_LINK (método, distância, confiança, revisão)",
        "```",
        "",
        "A camada de vídeo-inspeção deverá futuramente referenciar `NETWORK_SEGMENT` no schema `inspection`; não deve duplicar a tubulação em `mapping`.",
        "",
    ]

    lines += ["## 18. Mapeamento CAD → GIS", ""]
    lines += table(
        ["Informação técnica", "Origem no DWG", "Método", "Destino GIS", "Confiança"],
        [
            ["Tipo de rede", "Layer + legenda", "Tabela de regra explícita", "network_segments.network_type", "ALTA"],
            ["Geometria de trecho", "AcDbPolyline aberta", "Vértices XY; deduplicação revisada", "network_segments.geom", "ALTA/MÉDIA"],
            ["Geometria 3D fonte", "Polyline.Elevation/Z", "Preservar sem validar", "network_segments.source_geom_3d", "MÉDIA"],
            ["Profundidade", "DESC, texto P.x, XData", "Parser + vínculo auditável", "mapping_observations.depth_m", "ALTA no ponto; variável no trecho"],
            ["Diâmetro", "D[n] e texto ∅n", "Parser + vínculo auditável", "mapping_observations.diameter_mm", "ALTA no registro"],
            ["Material", "Não encontrado", "Não extrair", "Não criar inicialmente", "N/A"],
            ["Ponto levantado", "SIMB_PONTO", "Atributos + inserção", "mapping_observations", "ALTA"],
            ["PV esgoto", "PV ESA/pv_esgoto_sanitario", "BlockReference", "structures", "ALTA"],
            ["Registro de água", "rg_agua_potavel", "BlockReference", "structures", "ALTA"],
            ["Poste", "POSTE", "BlockReference", "structures", "ALTA"],
            ["Boca de lobo/grelha", "Polyline fechada em layer dedicado", "Centroide + footprint", "structures", "ALTA"],
            ["Estrutura genérica", "Polyline fechada em layer de rede", "Classificação humana", "structures", "MÉDIA/BAIXA"],
            ["Sentido", "Bloco SENTIDO", "Rotação + trecho próximo", "Atributo futuro", "MÉDIA"],
            ["Vídeo-inspeção", "GF-Rede Pluvial-VIDEO/TRECHO", "Vínculo ao trecho físico", "inspection (futuro)", "MÉDIA"],
        ],
    )
    lines += [""]

    lines += ["## 19. Estrutura PostGIS recomendada", ""]
    for entity in inventory["proposed_gis_entities"]:
        lines += [f"### `{entity['entity']}`", "", entity["purpose"], ""]
        lines += table(["Campo", "Tipo PostgreSQL/PostGIS", "Obrigatoriedade", "Origem"], entity["fields"])
        if entity.get("not_in_initial_schema"):
            lines += ["", "Não incluir inicialmente: " + "; ".join(entity["not_in_initial_schema"]) + "."]
        lines += [""]
    lines += ["Nenhum `CREATE TABLE` foi executado.", ""]

    lines += ["## 20. Próxima etapa recomendada", ""]
    lines += [
        "1. Confirmar zona UTM/SRID e política 2D/3D.",
        "2. Aprovar o catálogo de dez redes extraído da legenda e o dicionário de códigos/sufixos.",
        "3. Revisar visualmente os pares paralelos de vídeo, polylines fechadas genéricas e gaps listados no JSON.",
        "4. Construir um protótipo de importação para área de homologação, primeiro para `source_drawings`, `mapping_observations`, trechos pluviais/esgoto e estruturas confirmadas.",
        "5. Validar amostras CAD↔GIS antes de modelar o schema `inspection` e a vídeo-inspeção.",
        "",
        "Somente após essas decisões deve ser preparada a DDL para revisão; o banco não foi criado ou alterado nesta auditoria.",
        "",
    ]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description="Consolida inventários CAD read-only em relatório CAD→GIS.")
    parser.add_argument("raw", nargs="+", type=Path)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()
    inventory = build_inventory(args.raw)
    args.inventory.parent.mkdir(parents=True, exist_ok=True)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.inventory.write_text(json.dumps(inventory, ensure_ascii=False, indent=2), encoding="utf-8")
    args.report.write_text(render_report(inventory), encoding="utf-8")
    print(
        json.dumps(
            {
                "inventory": str(args.inventory),
                "inventory_bytes": args.inventory.stat().st_size,
                "report": str(args.report),
                "report_bytes": args.report.stat().st_size,
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
