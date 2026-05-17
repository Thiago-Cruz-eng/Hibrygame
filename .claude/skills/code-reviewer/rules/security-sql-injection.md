---
title: SQL Injection Prevention
impact: CRITICAL
category: security
tags: sql, security, injection, database
---

# SQL Injection Prevention

> **Never construct SQL queries with string concatenation or f-strings. Always use parameterized queries.**

## Why This Matters

SQL injection enables attackers to:
- Access unauthorized data
- Modify or delete database records
- Execute admin operations
- Issue OS commands (in some configurations)

## ❌ Vulnerable Pattern

```python
# DANGEROUS: String interpolation
user_id = request.get("user_id")  # Could be: "1 OR 1=1"
query = f"SELECT * FROM users WHERE id = {user_id}"
db.execute(query)
# Result: Returns ALL users instead of one
```

## ✅ Secure Pattern

```python
# SAFE: Parameterized query
user_id = request.get("user_id")
query = "SELECT * FROM users WHERE id = ?"
db.execute(query, (user_id,))
# Input is treated as data, never as code
```

## Framework Examples

### SQLAlchemy (Python)
```python
# ORM (automatic parameterization)
user = session.query(User).filter(User.id == user_id).first()

# Raw SQL with parameters
from sqlalchemy import text
result = session.execute(text("SELECT * FROM users WHERE id = :id"), {"id": user_id})
```

### Django (Python)
```python
# ORM
user = User.objects.get(id=user_id)

# Raw with params
User.objects.raw("SELECT * FROM users WHERE id = %s", [user_id])
```

### Entity Framework (.NET)
```csharp
// LINQ (safe)
var user = context.Users.FirstOrDefault(u => u.Id == userId);

// Raw SQL with parameters
var user = context.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Id = {userId}")
    .FirstOrDefault();
```

### Node.js (PostgreSQL)
```javascript
// pg library
const result = await pool.query(
  'SELECT * FROM users WHERE id = $1',
  [userId]
);
```

### Node.js (MySQL)
```javascript
// mysql2 library
const [rows] = await connection.execute(
  'SELECT * FROM users WHERE id = ?',
  [userId]
);
```

## Best Practices

1. **Validate input types** - Ensure IDs are integers, emails match patterns
2. **Use ORM methods** - They parameterize automatically
3. **Least privilege** - Database users should have minimal permissions
4. **Defense in depth** - Input validation + parameterization

## Detection

Look for these patterns in code reviews:
- String concatenation with `+` in SQL strings
- f-strings or template literals in queries
- `format()` or `%` formatting in SQL
- Dynamic table/column names (harder to parameterize)

## References

- [OWASP SQL Injection](https://owasp.org/www-community/attacks/SQL_Injection)
- [CWE-89](https://cwe.mitre.org/data/definitions/89.html)
