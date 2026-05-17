---
name: code-reviewer
description: Code review skill for security, performance, correctness, and maintainability analysis
allowed-tools: Read, Grep, Glob, Task
version: 2.0.0
priority: HIGH
---

# Code Reviewer Skill

> **Priority Order:** Security → Performance → Correctness → Maintainability → Testing

---

## Review Categories

| Priority | Category | Focus |
|----------|----------|-------|
| 🔴 **CRITICAL** | Security | SQL injection, XSS, auth bypass, hardcoded secrets |
| 🟠 **HIGH** | Performance | N+1 queries, missing indexes, memory leaks |
| 🟠 **HIGH** | Correctness | Error handling, race conditions, null checks |
| 🟡 **MEDIUM** | Maintainability | Naming, type hints, DRY, SRP |

---

## Security Rules (CRITICAL)

### SQL Injection Prevention
> **Never construct SQL queries with string concatenation. Always use parameterized queries.**

| ❌ Vulnerable | ✅ Secure |
|--------------|----------|
| `f"SELECT * FROM users WHERE id = {user_id}"` | `db.execute("SELECT * FROM users WHERE id = ?", (user_id,))` |
| String interpolation in queries | ORM methods with bound parameters |

**Threat:** Attackers can access unauthorized data, modify/delete records, execute admin operations.

### XSS Prevention
> **Never use innerHTML with unsanitized input.**

| ❌ Vulnerable | ✅ Secure |
|--------------|----------|
| `element.innerHTML = userInput` | `element.textContent = userInput` |
| `dangerouslySetInnerHTML={{__html: data}}` | Use DOMPurify: `DOMPurify.sanitize(data)` |

**Defense-in-Depth:**
- Content Security Policy headers
- HTTPOnly cookies
- Server-side input validation

---

## Performance Rules (HIGH)

### N+1 Query Problem
> **1 query to fetch list + N queries for related data = SLOW**

| ❌ Problem | ✅ Solution |
|-----------|------------|
| Loop fetching related data | Eager loading / JOIN |
| `for post in posts: post.author` | `select_related('author')` |

**Framework Solutions:**
- **Django:** `select_related()`, `prefetch_related()`
- **SQLAlchemy:** `joinedload()`, `selectinload()`
- **EF Core:** `.Include()`, `.ThenInclude()`
- **Prisma:** `include: { relation: true }`

**Impact:** 100 items = 101 queries → 1-5 seconds delay with network latency.

---

## Correctness Rules (HIGH)

### Error Handling
> **Never silently ignore errors. Catch specific exceptions.**

| ❌ Wrong | ✅ Right |
|---------|---------|
| `except: pass` | `except ValueError as e: log.error(e)` |
| `catch (e) {}` | `catch (e) { logger.error(e); throw }` |
| Bare except/catch | Specific exception types with context |

**Best Practices:**
- Catch specific exceptions
- Preserve stack traces when re-raising
- Log with context (`exc_info=True`)
- Clean up resources (finally/using/with)

---

## Maintainability Rules (MEDIUM)

### Naming Conventions
> **Code is read 10x more than written. Names should reveal intent.**

| ❌ Bad | ✅ Good |
|-------|--------|
| `n`, `x`, `data` | `userCount`, `maxRetries`, `orderItems` |
| `process()` | `calculateTotalWithDiscount()` |
| `flag` | `isActive`, `hasPermission` |

### Type Hints
> **Types catch bugs before runtime and enable IDE support.**

```python
# ❌ No types
def get_user(id):
    return users.get(id)

# ✅ With types
def get_user(id: int) -> Optional[User]:
    return users.get(id)
```

---

## Review Output Format

```markdown
## Code Review: [file/component]

### 🔴 Critical Issues (Security)
- **[File:Line]** Issue description
  - **Impact:** What could happen
  - **Fix:** Corrected code example

### 🟠 High Priority (Performance/Correctness)
- **[File:Line]** Issue description
  - **Impact:** Performance/correctness impact
  - **Fix:** Corrected code example

### 🟡 Suggestions (Maintainability)
- **[File:Line]** Suggestion
  - **Reason:** Why this improves the code

### ✅ Good Practices Found
- List of things done well
```

---

## Review Checklist

### Security
- [ ] No SQL string concatenation
- [ ] No unsanitized HTML rendering
- [ ] No hardcoded secrets/credentials
- [ ] Auth checks on sensitive operations
- [ ] Input validation at boundaries

### Performance
- [ ] No N+1 queries (check loops with DB calls)
- [ ] Indexes exist for WHERE/JOIN columns
- [ ] No unnecessary API calls in loops
- [ ] Pagination for large datasets

### Correctness
- [ ] Specific exception handling
- [ ] Null/undefined checks where needed
- [ ] Race condition prevention
- [ ] Resource cleanup (connections, files)

### Maintainability
- [ ] Descriptive variable names
- [ ] Type annotations on public APIs
- [ ] No code duplication
- [ ] Functions do one thing

---

## References

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [CWE-89: SQL Injection](https://cwe.mitre.org/data/definitions/89.html)
- [Clean Code - Robert Martin](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)
