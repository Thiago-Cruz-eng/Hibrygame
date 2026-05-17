---
title: Proper Error Handling
impact: HIGH
category: correctness
tags: errors, exceptions, debugging, logging
---

# Proper Error Handling

> **Catch specific exceptions, provide context, never silently ignore errors.**

## Why This Matters

- **Prevents silent failures** - Errors surface instead of causing undefined behavior
- **Aids debugging** - Clear messages with context
- **Enables recovery** - Graceful degradation when possible
- **Supports monitoring** - Structured logging for alerts

## ❌ Wrong Patterns

```python
# Silent ignore - WORST
try:
    risky_operation()
except:
    pass

# Bare except - catches EVERYTHING including KeyboardInterrupt
try:
    process_data()
except:
    print("error")

# Generic catch without context
try:
    fetch_user(id)
except Exception as e:
    raise Exception("Failed")  # Lost original error!
```

```javascript
// Silent catch
try {
    riskyOperation();
} catch (e) {
    // do nothing
}

// Swallowing errors
.catch(() => {})
```

## ✅ Correct Patterns

### Python

```python
# Specific exceptions with context
try:
    user = fetch_user(user_id)
except UserNotFoundError:
    logger.warning(f"User {user_id} not found")
    return None
except DatabaseConnectionError as e:
    logger.error(f"DB connection failed: {e}", exc_info=True)
    raise ServiceUnavailableError("Database unavailable") from e

# Multiple specific exceptions
try:
    data = json.loads(response.text)
except json.JSONDecodeError as e:
    logger.error(f"Invalid JSON at position {e.pos}: {e.msg}")
    raise
except UnicodeDecodeError as e:
    logger.error(f"Encoding error: {e}")
    raise
```

### TypeScript/JavaScript

```typescript
// Specific error types
try {
    const user = await fetchUser(userId);
} catch (error) {
    if (error instanceof NotFoundError) {
        logger.warn(`User ${userId} not found`);
        return null;
    }
    if (error instanceof NetworkError) {
        logger.error('Network failure', { error, userId });
        throw new ServiceUnavailableError('Service unavailable');
    }
    // Re-throw unknown errors
    throw error;
}

// With timeout
const controller = new AbortController();
const timeout = setTimeout(() => controller.abort(), 5000);

try {
    const response = await fetch(url, { signal: controller.signal });
    return await response.json();
} catch (error) {
    if (error.name === 'AbortError') {
        throw new TimeoutError(`Request to ${url} timed out`);
    }
    throw error;
} finally {
    clearTimeout(timeout);
}
```

### C#

```csharp
try
{
    var user = await _userRepository.GetByIdAsync(userId);
}
catch (EntityNotFoundException)
{
    _logger.LogWarning("User {UserId} not found", userId);
    return null;
}
catch (DbException ex)
{
    _logger.LogError(ex, "Database error fetching user {UserId}", userId);
    throw new ServiceException("Database unavailable", ex);
}
```

## Custom Exception Classes

### Python
```python
class ServiceError(Exception):
    """Base exception for service errors."""
    pass

class UserNotFoundError(ServiceError):
    def __init__(self, user_id: int):
        self.user_id = user_id
        super().__init__(f"User {user_id} not found")

class ValidationError(ServiceError):
    def __init__(self, field: str, message: str):
        self.field = field
        super().__init__(f"{field}: {message}")
```

### TypeScript
```typescript
class ServiceError extends Error {
    constructor(message: string, public readonly code: string) {
        super(message);
        this.name = 'ServiceError';
    }
}

class NotFoundError extends ServiceError {
    constructor(resource: string, id: string) {
        super(`${resource} ${id} not found`, 'NOT_FOUND');
        this.name = 'NotFoundError';
    }
}
```

## Resource Cleanup

```python
# Python - context manager
with open('file.txt') as f:
    data = f.read()
# File automatically closed even on error

# Or try/finally
conn = get_connection()
try:
    result = conn.execute(query)
finally:
    conn.close()
```

```typescript
// TypeScript - finally
const connection = await pool.getConnection();
try {
    return await connection.query(sql);
} finally {
    connection.release();
}
```

## Logging Best Practices

```python
import logging

logger = logging.getLogger(__name__)

try:
    process_order(order_id)
except OrderError as e:
    # Include context and stack trace
    logger.error(
        "Failed to process order",
        extra={
            'order_id': order_id,
            'error_type': type(e).__name__,
        },
        exc_info=True  # Include stack trace
    )
    raise
```

## Checklist

- [ ] No bare `except:` or `catch {}`
- [ ] Specific exception types caught
- [ ] Original error preserved when re-raising
- [ ] Context included in error messages
- [ ] Resources cleaned up (finally/with/using)
- [ ] Errors logged with stack traces
- [ ] Unknown errors re-raised, not swallowed
