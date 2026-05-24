# Auth Contract Additions

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24

These schemas and endpoints are **additions** to `neko-hoa/api/openapi.yaml` introduced during the clarification phase. They must be merged into `openapi.yaml` before or during implementation.

---

## Modified Schemas

### `AuthResponse` (modified)

Add `refreshToken` field alongside the existing `token` field:

```yaml
AuthResponse:
  type: object
  required: [token, refreshToken, expiresAt, user]
  properties:
    token:
      type: string
      description: "Short-lived JWT access token (15-min expiry). Include as `Authorization: Bearer <token>`."
      example: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    refreshToken:
      type: string
      description: "Opaque refresh token (30-day expiry). Store securely; use with POST /auth/refresh."
      example: d3f8a1b2c4e5...
    expiresAt:
      type: string
      format: date-time
      description: "Expiry timestamp of the access token."
      example: "2026-05-24T15:15:00Z"
    user:
      $ref: '#/components/schemas/CurrentUser'
```

### `RegisterRequest` (modified)

Add required `accountNumber` field:

```yaml
RegisterRequest:
  type: object
  required: [email, password, firstName, lastName, accountNumber]
  properties:
    email:
      type: string
      format: email
    password:
      type: string
      format: password
      minLength: 8
    firstName:
      type: string
      example: Jane
    lastName:
      type: string
      example: Doe
    accountNumber:
      type: string
      description: "HOA account number from the resident's welcome letter. Links the new user to their property."
      example: R0670853L0541192
```

### `CurrentUser` (modified)

Add `properties` array to the `/auth/me` response:

```yaml
CurrentUser:
  type: object
  required: [id, firstName, lastName, email, initials, properties]
  properties:
    id:
      type: string
      format: uuid
    firstName:
      type: string
    lastName:
      type: string
    email:
      type: string
      format: email
    initials:
      type: string
      minLength: 1
      maxLength: 3
    properties:
      type: array
      description: "All properties linked to this user."
      items:
        type: object
        required: [id, address, accountNumber]
        properties:
          id:
            type: string
            format: uuid
          accountNumber:
            type: string
          address:
            type: string
```

---

## New Endpoints

### `POST /auth/refresh`

Exchange a valid refresh token for a new access + refresh token pair (token rotation).

```yaml
/auth/refresh:
  post:
    tags: [Auth]
    summary: Refresh access token
    operationId: RefreshToken
    security: []
    requestBody:
      required: true
      content:
        application/json:
          schema:
            type: object
            required: [refreshToken]
            properties:
              refreshToken:
                type: string
                example: d3f8a1b2c4e5...
    responses:
      '200':
        description: New token pair issued
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/AuthResponse'
      '401':
        description: Refresh token invalid, expired, or revoked
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/ErrorResponse'
            example:
              code: INVALID_REFRESH_TOKEN
              message: The refresh token is invalid or has expired.
```

### `POST /auth/switch-property`

Re-issue a token pair scoped to a different property owned by the authenticated user.

```yaml
/auth/switch-property:
  post:
    tags: [Auth]
    summary: Switch active property
    operationId: SwitchProperty
    security:
      - bearerAuth: []
    requestBody:
      required: true
      content:
        application/json:
          schema:
            type: object
            required: [propertyId]
            properties:
              propertyId:
                type: string
                format: uuid
                description: "UUID of the target property. Must be linked to the authenticated user."
    responses:
      '200':
        description: New token pair issued with updated propertyId claim
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/AuthResponse'
      '403':
        description: User is not linked to the requested property
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/ErrorResponse'
            example:
              code: PROPERTY_ACCESS_DENIED
              message: You are not linked to the requested property.
      '401':
        $ref: '#/components/responses/Unauthorized'
```

---

## New Error Codes (to add to `openapi.yaml` documentation)

| Code | Meaning |
|------|---------|
| `ACCOUNT_NOT_FOUND` | `accountNumber` in `RegisterRequest` does not match any property |
| `ACCOUNT_ALREADY_CLAIMED` | `accountNumber` is already linked to another user |
| `INVALID_REFRESH_TOKEN` | Refresh token is expired, revoked, or not found |
| `PROPERTY_ACCESS_DENIED` | User is not linked to the requested property (switch-property) |
