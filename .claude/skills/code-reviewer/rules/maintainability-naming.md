---
title: Meaningful Variable Names
impact: MEDIUM
category: maintainability
tags: naming, readability, code-quality
---

# Use Meaningful Variable Names

> **Code is read 10x more than it's written. Names should reveal intent.**

## Why This Matters

- **Self-documenting code** - Less need for comments
- **Reduced cognitive load** - Easier to understand
- **Fewer bugs** - Clear names prevent confusion
- **Easier refactoring** - Intent is clear

## ❌ Bad Names

```python
# Single letters (except loop counters)
n = get_users()
x = calculate(a, b)

# Abbreviations
usr_cnt = len(users)
btn_clk_hndlr = lambda: ...

# Generic names
data = fetch_data()
result = process(data)
temp = data['items']

# Misleading names
user_list = get_user()  # Returns single user, not list!
```

## ✅ Good Names

```python
# Descriptive
active_users = get_active_users()
total_price = calculate_total(items, discount)

# Intent-revealing
max_retry_attempts = 3
connection_timeout_seconds = 30

# Consistent vocabulary
# Pick one: fetch/get/retrieve - use consistently
user = get_user_by_id(user_id)
orders = get_orders_by_user(user_id)
```

## Naming Conventions by Language

### Python (PEP 8)
```python
# Variables and functions: snake_case
user_count = 0
def get_user_by_id(user_id: int) -> User:
    pass

# Classes: PascalCase
class UserRepository:
    pass

# Constants: UPPER_SNAKE_CASE
MAX_CONNECTIONS = 100
DEFAULT_TIMEOUT = 30
```

### JavaScript/TypeScript
```typescript
// Variables and functions: camelCase
const userCount = 0;
function getUserById(userId: number): User {}

// Classes: PascalCase
class UserRepository {}

// Constants: UPPER_SNAKE_CASE or camelCase
const MAX_CONNECTIONS = 100;
const defaultTimeout = 30;
```

### C#
```csharp
// Local variables and parameters: camelCase
var userCount = 0;
void ProcessOrder(int orderId) {}

// Properties and methods: PascalCase
public int UserCount { get; set; }
public User GetUserById(int userId) {}

// Private fields: _camelCase
private readonly ILogger _logger;

// Constants: PascalCase
public const int MaxConnections = 100;
```

## Boolean Naming

```python
# Use is_, has_, can_, should_ prefixes
is_active = True
has_permission = check_permission(user)
can_edit = user.role == 'admin'
should_retry = attempts < max_attempts
```

```typescript
// Same in TypeScript
const isActive = true;
const hasPermission = checkPermission(user);
const canEdit = user.role === 'admin';
const shouldRetry = attempts < maxAttempts;
```

## Function Naming

```python
# Verb + noun pattern
def get_user_by_id(user_id: int) -> User:
def calculate_total_price(items: List[Item]) -> Decimal:
def validate_email_format(email: str) -> bool:
def send_welcome_email(user: User) -> None:

# Avoid generic verbs
# ❌ process_data(), handle_request(), do_thing()
# ✅ parse_json_response(), route_http_request(), archive_old_orders()
```

## Collections

```python
# Pluralize for collections
users = get_all_users()        # List[User]
user = get_user_by_id(1)       # User

# Or use explicit suffix
user_list = get_all_users()
user_map = {u.id: u for u in users}
user_set = set(user_ids)
```

## Scope-Based Length

```python
# Short scope = shorter names OK
for i in range(10):           # OK for simple loop
    print(i)

for user in users:            # Better for clarity
    process(user)

# Long scope = longer, descriptive names
class OrderProcessor:
    def __init__(self):
        self.pending_orders_queue = []
        self.processed_order_count = 0
        self.max_concurrent_processing = 5
```

## Quick Reference

| Bad | Good | Why |
|-----|------|-----|
| `d` | `elapsed_days` | Reveals meaning |
| `lst` | `users` | Clear type |
| `temp` | `cached_result` | Purpose clear |
| `data` | `order_items` | Specific |
| `flag` | `is_verified` | Boolean clarity |
| `process()` | `validate_order()` | Action clear |
