---
title: Type Hints
impact: MEDIUM
category: maintainability
tags: types, python, typescript, type-safety
---

# Add Type Hints

> **Type annotations make code self-documenting and catch errors early.**

## Why This Matters

- **Static analysis** - Catch bugs before runtime
- **IDE support** - Better autocomplete and refactoring
- **Documentation** - Types explain intent
- **Confidence** - Safer refactoring

## Python

### ❌ Without Types
```python
def get_user(id):
    return users.get(id)

def process_order(order, discount):
    if discount:
        return order['total'] * (1 - discount)
    return order['total']
```

### ✅ With Types
```python
from typing import Optional, Dict, Any, List

def get_user(id: int) -> Optional[Dict[str, Any]]:
    """Fetch user by ID."""
    return users.get(id)

def process_order(
    order: Dict[str, Any],
    discount: Optional[float] = None
) -> float:
    """Calculate order total with optional discount."""
    if discount:
        return order['total'] * (1 - discount)
    return order['total']
```

### Common Python Types
```python
from typing import (
    Optional,    # Optional[X] = X | None
    List,        # List[X] = list of X
    Dict,        # Dict[K, V] = dict with K keys, V values
    Tuple,       # Tuple[X, Y] = fixed tuple
    Set,         # Set[X] = set of X
    Union,       # Union[X, Y] = X or Y
    Callable,    # Callable[[Args], Return]
    Any,         # Any type (avoid if possible)
)

# Python 3.10+ simplified syntax
def process(items: list[str]) -> dict[str, int]:
    return {item: len(item) for item in items}

# Optional is X | None in 3.10+
def find_user(id: int) -> User | None:
    return users.get(id)
```

### TypedDict for Dictionaries
```python
from typing import TypedDict

class UserDict(TypedDict):
    id: int
    name: str
    email: str
    is_active: bool

def create_user(data: UserDict) -> User:
    return User(**data)
```

### Dataclasses
```python
from dataclasses import dataclass
from typing import Optional

@dataclass
class User:
    id: int
    name: str
    email: str
    is_active: bool = True
    avatar_url: Optional[str] = None
```

## TypeScript

### ❌ Without Types
```typescript
function getUser(id) {
    return users.get(id);
}

function processOrder(order, discount) {
    if (discount) {
        return order.total * (1 - discount);
    }
    return order.total;
}
```

### ✅ With Types
```typescript
interface User {
    id: number;
    name: string;
    email: string;
    isActive: boolean;
}

interface Order {
    id: number;
    items: OrderItem[];
    total: number;
}

function getUser(id: number): User | null {
    return users.get(id) ?? null;
}

function processOrder(order: Order, discount?: number): number {
    if (discount) {
        return order.total * (1 - discount);
    }
    return order.total;
}
```

### Generics
```typescript
// Generic function
function first<T>(items: T[]): T | undefined {
    return items[0];
}

// Generic interface
interface Repository<T> {
    findById(id: number): Promise<T | null>;
    save(entity: T): Promise<T>;
    delete(id: number): Promise<void>;
}

// Usage
class UserRepository implements Repository<User> {
    async findById(id: number): Promise<User | null> {
        // ...
    }
}
```

### Utility Types
```typescript
// Partial - all properties optional
function updateUser(id: number, updates: Partial<User>): User {}

// Pick - select properties
type UserPreview = Pick<User, 'id' | 'name'>;

// Omit - exclude properties
type CreateUserInput = Omit<User, 'id'>;

// Required - all properties required
type CompleteUser = Required<User>;

// Readonly - immutable
type ImmutableUser = Readonly<User>;
```

## C#

```csharp
// C# is already strongly typed, but use explicit types over var for clarity

// ❌ Less clear
var result = GetUser(id);
var items = ProcessOrders(orders);

// ✅ Clear intent
User? result = GetUser(id);
List<ProcessedOrder> items = ProcessOrders(orders);

// Nullable reference types (C# 8+)
public User? FindUser(int id)  // May return null
public User GetUser(int id)    // Never returns null
```

## When to Skip Types

```python
# OK to skip for obvious local variables
users = get_users()  # Obviously List[User]
count = len(users)   # Obviously int

# Always type:
# - Function parameters
# - Function return types
# - Class attributes
# - Public API
```

## Type Checking Tools

| Language | Tool | Command |
|----------|------|---------|
| Python | mypy | `mypy src/` |
| Python | pyright | `pyright src/` |
| TypeScript | tsc | `tsc --noEmit` |
| C# | Built-in | Compile-time |

## References

- [Python typing module](https://docs.python.org/3/library/typing.html)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/handbook/intro.html)
- [mypy documentation](https://mypy.readthedocs.io/)
