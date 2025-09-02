# Authentication System Implementation

## Overview

The FieldOpsOptimizer API now includes a comprehensive JWT-based authentication and authorization system with support for multi-tenancy and role-based access control.

## Components Added

### 1. Domain Layer
- **UserRole Enum**: Defines user roles (Admin, Dispatcher, Technician, etc.)
- **User Entity**: Core user domain entity with authentication-related properties
- Domain methods for user management, role assignment, and token handling

### 2. Application Layer
- **IAuthService Interface**: Defines authentication service contract
- **AuthResult Model**: Result wrapper for authentication operations
- **JwtSettings Configuration**: JWT token settings configuration class

### 3. Infrastructure Layer
- **AuthService Implementation**: Complete JWT authentication service
- **UserConfiguration**: Entity Framework configuration for User entity
- Database integration with proper indexes and constraints

### 4. API Layer
- **AuthController**: REST endpoints for authentication operations
- JWT middleware configuration in Program.cs
- Swagger integration with JWT Bearer authentication support

## API Endpoints

### Authentication Endpoints

#### POST `/api/auth/login`
Authenticates a user and returns JWT tokens.

**Request:**
```json
{
  "usernameOrEmail": "user@example.com",
  "password": "SecurePassword123!",
  "tenantId": "default-tenant"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-refresh-token",
  "expiresAt": "2024-01-01T12:00:00Z",
  "user": {
    "id": "guid",
    "username": "username",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["Technician"],
    "technicianId": "technician-guid-if-applicable"
  }
}
```

#### POST `/api/auth/register`
Registers a new user account.

**Request:**
```json
{
  "username": "newuser",
  "email": "newuser@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "default-tenant",
  "role": "Technician"
}
```

#### POST `/api/auth/refresh`
Refreshes an access token using a valid refresh token.

**Request:**
```json
{
  "refreshToken": "base64-encoded-refresh-token"
}
```

#### POST `/api/auth/logout`
Revokes a refresh token (requires authentication).

**Request:**
```json
{
  "refreshToken": "base64-encoded-refresh-token"
}
```

#### GET `/api/auth/profile`
Gets the current user's profile information (requires authentication).

## User Roles

- **Admin**: Full system administration access
- **Dispatcher**: Job dispatching and route management
- **FieldManager**: Field operations oversight
- **Technician**: Service job execution
- **Customer**: Customer portal access
- **Viewer**: Read-only access

## JWT Configuration

The JWT settings are configured in `appsettings.json`:

```json
{
  "JwtSettings": {
    "Secret": "YourSecretKeyThatShouldBeAtLeast256BitsLongForHmacSha256Encryption!",
    "Issuer": "FieldOpsOptimizer",
    "Audience": "FieldOpsOptimizer.Api",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  }
}
```

## Security Features

1. **Password Security**: BCrypt hashing with salt rounds
2. **Password Policy**: Enforced strong password requirements
3. **JWT Security**: HMAC SHA-256 signing with configurable expiry
4. **Refresh Tokens**: Secure token refresh mechanism
5. **Multi-tenancy**: Tenant isolation for user accounts
6. **Role-based Access**: Flexible role assignment and verification

## Usage in Controllers

To protect endpoints, use the `[Authorize]` attribute:

```csharp
[HttpGet]
[Authorize] // Requires any authenticated user
public IActionResult GetData()
{
    // Access user claims
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
    return Ok();
}

[HttpPost]
[Authorize(Roles = "Admin,Dispatcher")] // Requires specific roles
public IActionResult AdminOperation()
{
    return Ok();
}
```

## Testing Authentication

1. **Start the API**: Run the application in development mode
2. **Access Swagger UI**: Navigate to the Swagger interface
3. **Register a User**: Use the `/api/auth/register` endpoint
4. **Login**: Use the `/api/auth/login` endpoint to get tokens
5. **Authorize in Swagger**: Click the "Authorize" button and enter `Bearer <access_token>`
6. **Test Protected Endpoints**: Try accessing other API endpoints

## Database Schema

The User entity includes the following key properties:
- **Id**: Primary key (GUID)
- **Username**: Unique username per tenant
- **Email**: Unique email per tenant
- **PasswordHash**: BCrypt hashed password
- **TenantId**: Multi-tenancy support
- **Roles**: Comma-separated role list
- **RefreshToken**: Current refresh token
- **RefreshTokenExpiryTime**: Token expiry timestamp
- **TechnicianId**: Optional link to technician record

## Production Considerations

1. **Secret Management**: Store JWT secret in secure configuration (Azure Key Vault, etc.)
2. **HTTPS Only**: Set `RequireHttpsMetadata = true` in production
3. **Token Expiry**: Adjust token expiry times based on security requirements
4. **Rate Limiting**: Implement rate limiting for authentication endpoints
5. **Audit Logging**: Log authentication events for security monitoring
6. **Database Security**: Use proper connection string security and encrypted connections

## Next Steps

The authentication system is now ready for use. Consider implementing:

1. **Authorization Policies**: Custom authorization policies for fine-grained access control
2. **Account Management**: Password reset, email verification, account lockout
3. **Two-Factor Authentication**: TOTP-based 2FA for enhanced security
4. **OAuth Integration**: Support for external identity providers
5. **Session Management**: Advanced session handling and concurrent login limits
