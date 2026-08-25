# GCP Lab Resources — Zenith Bank APM Lab

## Project
- **Project:** elastic-sa
- **Region/Zone:** africa-south1 / africa-south1-a

## Resources Created

### Compute Instances
| Name           | Zone            | Machine Type  | Status                  |
| -------------- | --------------- | ------------- | ----------------------- |
| zenith-apm-lab | africa-south1-a | e2-standard-4 | RUNNING — 34.35.125.118 |

### Firewall Rules
| Name | Ports | Target | Status |
|---|---|---|---|
| zenith-apm-lab-rdp | TCP 3389 | tag: zenith-apm-lab | ACTIVE |

## Destroy Commands
```bash
# Delete VM
gcloud compute instances delete zenith-apm-lab --zone=africa-south1-a --project=elastic-sa --quiet

# Delete firewall rule
gcloud compute firewall-rules delete zenith-apm-lab-rdp --project=elastic-sa --quiet
```
