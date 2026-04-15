# Nginx Proxy Manager Setup

This guide configures `arkham-change.autonate.dev` in Nginx Proxy Manager for the Arkham Change Request App running through [`docker-compose.prod.yml`](/Users/nathan/Dev/Arkham-Change-Request-App/docker-compose.prod.yml).

## Prerequisites

- The app container is running on the VPS:

  ```bash
  docker compose -f docker-compose.prod.yml up --build -d
  ```

- The production Entra app registration includes:

  ```text
  https://arkham-change.autonate.dev/signin-oidc
  https://arkham-change.autonate.dev/signout-callback-oidc
  ```

- Nginx Proxy Manager is running in Docker and attached to the external `edge-net` network.

## Attach Nginx Proxy Manager To `edge-net`

If Nginx Proxy Manager is not already on `edge-net`, connect it once:

```bash
docker network connect edge-net <nginx-proxy-manager-container-name>
```

You can confirm both containers share the network with:

```bash
docker network inspect edge-net
```

## Proxy Host Configuration

In Nginx Proxy Manager, create a new **Proxy Host** with:

- **Domain Names**: `arkham-change.autonate.dev`
- **Scheme**: `http`
- **Forward Hostname / IP**: `arkham-change-request`
- **Forward Port**: `8080`
- **Block Common Exploits**: enabled
- **Websockets Support**: enabled
- **Cache Assets**: disabled for the first deployment

If Nginx Proxy Manager cannot resolve the Docker service name, use the VPS IP address instead, with port `8080`.

## SSL Configuration

In the **SSL** tab:

- request a new Let's Encrypt certificate for `arkham-change.autonate.dev`
- enable **Force SSL**
- enable **HTTP/2**
- enable **HSTS** if you are happy to commit the domain to HTTPS

## Verification

After saving the proxy host:

1. open `https://arkham-change.autonate.dev/health`
2. confirm it returns HTTP `200`
3. open `https://arkham-change.autonate.dev`
4. sign in with a production Entra account
5. confirm the app returns to the site after login

## Troubleshooting

- If the proxy host is offline, verify the app is reachable on `http://127.0.0.1:8080/health`
- If Nginx Proxy Manager cannot reach the app by container name, verify both containers are attached to `edge-net`
- If Entra sign-in fails after redirect, verify the production app registration uses the exact HTTPS callback URLs
- If you hit a redirect loop, confirm Nginx Proxy Manager is forwarding `X-Forwarded-Proto` and the app is still running with `App__DisableHttpsRedirection=false`
