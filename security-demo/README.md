# Security Demo — Elastic ↔ Azure OpenAI Data Controls

This folder contains the build guide and live demo script for demonstrating Elastic's data governance and security controls to the Zenith Bank security team.

## Files

| File | Purpose |
|---|---|
| `POC - Elastic to Azure OpenAI Security Controls.md` | Build guide — the what and why for each layer |
| `POC - Kibana Dev Console Demo Script.md` | Step-by-step Kibana Dev Console commands to run live |

## Environment

- **Elastic:** Cloud (ECH) — `northeurope.azure.elastic-cloud.com`
- **OTel Demo:** Running on GCP Kubernetes (`gke_elastic-sa_us-central1_jacques-vlaming-cluster`)
- **Data:** OpenTelemetry demo app — 18 microservices generating traces, logs, and metrics

## Demo Flow (25–35 min)

1. **Orientate** — Show live accounting service logs with order details, addresses, amounts
2. **Layer 1: Pipeline** — Create `zenith-otel-field-masking` pipeline, simulate before/after
3. **Layer 2: Field filtering** — Show projected search (only safe fields reach the LLM)
4. **Layer 3: RBAC** — Create `zenith-otel-analyst` role with field allowlist
5. **Agent Builder** — Add custom instructions + scoped index search tool
6. **Live test** — Ask agent for card numbers / raw IDs → agent refuses
7. **Observability** — Agent Builder Overview dashboard + GenAI Settings privacy toggles

## Key Indices

| Index | Contents |
|---|---|
| `traces-apm-default` | Distributed traces from all OTel demo services |
| `logs-apm.app.accounting-default` | Accounting service logs — order details, addresses, amounts |
| `logs-apm.app.payment-default` | Payment service logs |
| `logs-apm.app.checkout-default` | Checkout service logs |
| `metrics-apm.service_transaction.1m-default` | Aggregated service metrics |
| `traces-agent_builder.otel-default` | Agent Builder conversation traces (LLM observability) |

## Pipeline

The ingest pipeline `zenith-otel-field-masking` applies:
- `gsub` — masks card number patterns in `message` and `body` fields → `[CARD-REDACTED]`
- `fingerprint` — SHA-256 hashes `labels.order_id` and `labels.transaction_id`
- `remove` — drops raw order/transaction ID fields after hashing
