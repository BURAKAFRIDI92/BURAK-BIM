# Security

- **Network exposure.** The BURAK BIM server talks to AI clients only over stdio. Host bridges listen on `127.0.0.1` only. Each bridge uses a random port and a random 192-bit token, which is stored in `<install>\runtime\*.json` (user profile permissions) and deleted when the host exits.
- **What a connected agent can do.** `script_run`, `api_call`, `com_*` and `ui_*` give a connected AI agent the same power as the logged-in user inside Autodesk software. Treat AI clients with the same trust as a person at the keyboard. To limit this, set `"readOnly": true`, `"allowScripts": false`, `"allowComAutomation": false` and/or `"allowUiAutomation": false` in `<install>\config\burakbim.json`. Read-only mode cannot be switched off by an AI agent.
- **Audit trail.** Every tool call is recorded in `<install>\audit\*.jsonl`.
- **Reporting.** Report vulnerabilities privately to the BURAK BIM maintainers through GitHub Security Advisories on this repository.
