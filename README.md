# Document Platform Integration

Document Platform Integration models a REST-first document platform with a .NET SDK wrapper, a server-rendered platform web client, a third-party consumer application, and a Keycloak-backed OAuth2/OIDC identity boundary.

The project focuses on integration shape rather than vendor internals: first-party browser operations call the REST platform through a web client, while external .NET integrations use a typed SDK over the same REST API.

## Core Capabilities

- REST API as the primary document platform boundary.
- Platform-provided .NET SDK wrapper over REST endpoints.
- Server-rendered web client using OIDC authorization code flow and server-side cookies.
- Third-party consumer app using client credentials and SDK-based integration.
- Keycloak realm with users, clients, roles, and service account configuration.
- Docker Compose orchestration for identity, platform API, web client, and consumer app.

## Architecture

```mermaid
flowchart TB
    subgraph A["A. Browser user flow"]
        A1[A1 Browser opens Platform.WebClient]
        A2[A2 WebClient redirects to Keycloak]
        A3[A3 Keycloak returns user tokens]
        A4[A4 WebClient calls Platform.RestApi]
        A5[A5 RestApi validates user roles]
        A6[A6 WebClient renders documents]
        A1 --> A2 --> A3 --> A4 --> A5 --> A6
    end

    subgraph B["B. Third-party integration flow"]
        B1[B1 ThirdParty.Consumer requests app token]
        B2[B2 Keycloak returns client credentials token]
        B3[B3 Consumer calls Platform.DotNetSdk]
        B4[B4 SDK calls Platform.RestApi]
        B5[B5 RestApi validates integration role]
        B6[B6 Consumer returns typed result]
        B1 --> B2 --> B3 --> B4 --> B5 --> B6
    end
```

## Authentication Flows

### Web Client User Flow

```mermaid
sequenceDiagram
    participant A1 as A1 Browser
    participant A2 as A2 Platform.WebClient
    participant A3 as A3 Keycloak
    participant A4 as A4 Platform.RestApi

    A1->>A2: Open document platform
    A2->>A3: Start OIDC authorization code login
    A3-->>A2: Return authorization code callback
    A2->>A3: Exchange code for user tokens
    A3-->>A2: Return user access token
    A2-->>A1: Create server-side application cookie
    A1->>A2: Request document page
    A2->>A4: Call REST API with user bearer token
    A4-->>A2: Return documents or 403
    A2-->>A1: Render page
```

### Third-party Integration Flow

```mermaid
sequenceDiagram
    participant B1 as B1 ThirdParty.Consumer
    participant B2 as B2 Keycloak
    participant B3 as B3 Platform.DotNetSdk
    participant B4 as B4 Platform.RestApi

    B1->>B2: Request client credentials token
    B2-->>B1: Return application access token
    B1->>B3: Call typed SDK client
    B3->>B4: Forward bearer token to REST API
    B4-->>B3: Return integration result or 403
    B3-->>B1: Return typed result
```

Flow labels:

- `A*` steps are first-party browser and WebClient operations.
- `B*` steps are third-party machine-to-machine integration operations.

## Component Responsibilities

| Component | Responsibility |
| --- | --- |
| `Platform.RestApi` | protected document API and role-based authorization |
| `Platform.DotNetSdk` | typed .NET wrapper over REST calls |
| `Platform.WebClient` | first-party web UI with server-side OIDC session |
| `ThirdParty.Consumer` | external integration app using SDK and client credentials |
| `identity/keycloak` | realm import with clients, users, roles, and service account setup |

## Run Locally

Build all projects:

```powershell
dotnet build DocumentPlatformIntegration.slnx
```

Run the full Docker Compose stack:

```powershell
.\start-docker-with-swagger.ps1 -Build
```

Local URLs:

| Service | URL |
| --- | --- |
| REST API Swagger | http://localhost:5000/swagger |
| WebClient UI | http://localhost:5001 |
| ThirdParty Consumer Swagger | http://localhost:5002/swagger |
| Keycloak Admin Console | http://localhost:8080/admin/master/console/ |

Keycloak admin credentials: `admin / admin`.

Imported test accounts:

| Account | Password | Access |
| --- | --- | --- |
| `architect.user` | `password` | standard documents |
| `architect.admin` | `password` | standard and confidential documents |
| `thirdparty-consumer` | client secret | integration export |

## Verify

Expected results:

- `architect.user` can view standard documents.
- `architect.admin` can view standard and confidential documents.
- `thirdparty-consumer` client token can call `/api/documents/integration-export`.
- `thirdparty-consumer` client token receives `403` for user-only document endpoints.
