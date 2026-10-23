# Ticket Node System: Discussion Summary

**Date:** 23 October 2026
**Participants:** Neow, Alex Alternator, Vincent Le Vries
**Source:** Team chat log (`wplace husichat`), 07:45 to 09:43

---

## 1. Purpose of the Discussion

The team reviewed the high-level architecture of a distributed task system (working name: **UniSK**, Unified Services Kit, not yet final). The goal was to agree on the core components, the ticket model, and the interfaces between them, and to define the next steps for the MVP.

---

## 2. Architecture Overview

The system consists of three main elements:

| Component | Role |
|---|---|
| **Client** | A user-facing application (Slack-like interface) that creates tickets for jobs. It may run a coordinator superagent that splits multi-discipline jobs into single-discipline tickets. |
| **Router** | The only component exposed to the public internet. It handles authentication, maintains the node registry, checks access rights, and forwards tickets to nodes. It does not make routing decisions beyond access control. |
| **Node** | A self-hosted unit that exposes the HTTP endpoints required to complete a ticket. A node may be a single Claude Code instance, a multi-agent container (e.g. "ClankWorks"), or a human. Its internal implementation is opaque to the system. |

### Key Principles

- **Nodes are opaque.** Any internal structure is acceptable as long as the node completes the tickets it receives.
- **Jobs map to one discipline.** A job requiring several disciplines is decomposed by the client before submission.
- **Only the router is publicly reachable.** Nodes sit behind the local network and must share a LAN with the router. Clients may be located anywhere.
- **Git is the shared workspace.** A single linked repository is used, and each node creates its own branch for its work.

---

## 3. Registration

Nodes register with the router via a POST request. The proposed payload is:

- `name`: human-readable name (not guaranteed unique in practice)
- `id`: unique identifier (preferred over name for uniqueness across networks)
- `host`: network address of the node
- `type`: designation in the form of an image-style tag (e.g. `keinstein0:research-agent-1.3`)

The router stores registrations internally.

---

## 4. Permissions and Access Control

- Authentication is handled via GitHub.
- The router is responsible for access control, not the individual node.
- Permission levels proposed: **use** (delegate tasks), **modify** (change code or configuration), **full** (delete and manage permissions).
- For the MVP, a simpler model was favoured: either a user has access to all nodes or to none, managed via a whitelist of users.

---

## 5. Open Questions and Concerns

- **Large file handling:** Git has size limits for large binary files (e.g. video output). Options raised include chunking, or a dedicated tool ("Vit"). This was deemed solvable but not yet designed.
- **Remote container deployment:** Whether nodes can be deployed remotely (e.g. pulling a container from a registry) was discussed but deferred as a separate system.
- **Ticket schema:** The structure of a ticket (fields, permissions, context) is not yet defined.
- **HTTP endpoints:** The exact endpoints for the client, router, and node interactions are not yet specified.
- **Rollback:** Git branching is expected to provide rollback capability.
- **Task result handling:** Tasks, reports, and results were identified as the main data types, but their formats are not yet defined.

---

## 6. Action Items

| Owner | Task |
|---|---|
| Team | Define the API (ticket format, HTTP endpoints for client, router, and node) |
| Team | Decide the tech stack (the team reported it had reached a decision; the specific choice is not recorded in the chat) |
| Team | Create individual project folders in the shared GitHub repository |
| Vincent | Set up the frontend project on a new branch named `frontend setup` (a framework such as Svelte was suggested) |
| Team | Present the tech stack and architecture at the session |

---

## 7. Out of Scope for This Discussion

- Hackathon network setup (cables, switch, access point, Starlink)
- Internal administrative and academic coordination

---

## Consolidated Result

```text
UniSK: Consolidated Architecture (Draft v0.1)

Components
  Client   - Creates tickets; runs a coordinator superagent for decomposition.
  Router   - Sole public entry point. Handles auth (GitHub), node registry,
             and access checks. Forwards tickets without further routing logic.
  Node     - Self-hosted. Exposes the endpoints needed to complete a ticket.
             Internals are opaque. May be a single agent, a multi-agent
             container, or a human.

Constraints
  - Nodes share a LAN with the router; clients may be remote.
  - One job = one discipline. Multi-discipline jobs are split by the client.
  - Git: one repository; each node works on its own branch.

Registration (POST to router)
  { name, id, host, type }
  - id is the unique identifier; name is for display only.
  - type follows an image-tag convention (e.g. keinstein0:research-agent-1.3).

Access (MVP)
  - Auth via GitHub. Access controlled at the router.
  - Whitelist of users; all-or-nothing access per user.
  - Future: use / modify / full permission levels.

Open Items
  - Ticket schema
  - HTTP endpoint specification (client, router, node)
  - Large-file handling for git
  - Remote container deployment (deferred)
  - Result and report formats

Next Steps
  - Define API (ticket format and endpoints)
  - Finalise tech stack
  - Create project folders in GitHub repository
  - Frontend project setup (branch: frontend setup)
```
