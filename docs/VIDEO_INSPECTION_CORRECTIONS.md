# Correções de vídeo inspeção — GeoAutoEngine ↔ GeoAutoDocs

Este documento registra o contrato usado para devolver ao GeoAutoDocs correções feitas localmente nas anomalias da vídeo inspeção dentro do GeoAutoEngine.

## Transporte

- Named Pipe: `GeoAutoEngine.Inspection`
- Magic: `GADX`
- Versão do protocolo: `1`
- Header: 16 bytes
- JSON: UTF-8, propriedades em camelCase
- Limite de payload: 16 MiB

Tipos adicionais de mensagem:

- `CorrectionPush = 6`: GeoAutoEngine → GeoAutoDocs
- `CorrectionAck = 7`: GeoAutoDocs → GeoAutoEngine

O `requestId` do `CorrectionAck` deve ser exatamente o mesmo do `CorrectionPush` correspondente.

## CorrectionPush

O GeoAutoEngine envia somente ocorrências atualmente alteradas em relação ao último baseline confirmado pelo GeoAutoDocs.

Exemplo:

```json
{
  "reportId": "22222222-2222-2222-2222-222222222222",
  "generatedAtUtc": "2026-09-10T12:00:00Z",
  "corrections": [
    {
      "sectionId": "44444444-4444-4444-4444-444444444444",
      "occurrenceId": "55555555-5555-5555-5555-555555555555",
      "locationMode": "Point",
      "meterage": 13.75,
      "endMeterage": null,
      "meteragePoints": [],
      "description": "DESLOCAMENTO DAS JUNTAS"
    }
  ]
}
```

### Campos

`reportId` identifica o relatório de origem. Cada item de `corrections` preserva obrigatoriamente `sectionId` e `occurrenceId`; o GeoAutoDocs deve atualizar a ocorrência existente, nunca criar outra ocorrência com esses dados.

Campos editáveis enviados:

- `description`: texto corrigido;
- `meterage`: metragem de `Point` e início de `Range`/`From`;
- `endMeterage`: fim de `Range`;
- `meteragePoints`: pontos de `MultiPoint`;
- `locationMode`: modo original da ocorrência, usado para validação.

`EntireSection` normalmente envia somente alteração de `description`, mantendo metragens nulas/vazias.

## CorrectionAck

O GeoAutoDocs deve responder somente depois que o usuário revisar a solicitação e houver um resultado conhecido para a aplicação.

Exemplo de aplicação total:

```json
{
  "accepted": true,
  "status": "Success",
  "message": "Correções aplicadas.",
  "requestedCount": 1,
  "appliedCount": 1,
  "rejectedCount": 0,
  "results": [
    {
      "sectionId": "44444444-4444-4444-4444-444444444444",
      "occurrenceId": "55555555-5555-5555-5555-555555555555",
      "applied": true,
      "status": "Applied",
      "message": "Correção aplicada."
    }
  ]
}
```

Para aplicação parcial, usar `status = "Partial"` e retornar resultados individuais. O GeoAutoEngine considera sincronizado somente um item com `applied = true`, `status = "Applied"` ou `status = "Unchanged"`. Itens rejeitados ou não confirmados permanecem marcados como correções locais pendentes.

Se o receptor não puder localizar `ReportId`, `SectionId` ou `OccurrenceId`, não deve aplicar a correção silenciosamente. O item deve ser retornado como rejeitado, com mensagem explicando o motivo.

## Segurança de persistência

O GeoAutoDocs deve atualizar apenas os campos explicitamente autorizados pela correção. Não alterar vídeos, evidências, capturas, thumbnails, locais, trechos ou outros campos do projeto.

O GeoAutoEngine não considera a correção sincronizada apenas porque o frame foi transmitido. A alteração local somente vira novo baseline após confirmação no `CorrectionAck`.

Enquanto houver correções locais não sincronizadas, o GeoAutoEngine protege a sessão contra substituição silenciosa por outra inspeção recebida e pede confirmação antes de descartar alterações em importações/remoções manuais.
