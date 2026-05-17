---
title: N+1 Query Problem
impact: HIGH
category: performance
tags: database, performance, orm, queries
---

# Avoid N+1 Query Problem

> **The N+1 query problem occurs when code executes 1 query to fetch a list, then N additional queries to fetch related data for each item.**

## Impact

| Items | Queries | Latency (1ms/query) | Latency (10ms/query) |
|-------|---------|---------------------|----------------------|
| 10 | 11 | 11ms | 110ms |
| 100 | 101 | 101ms | 1.01s |
| 1000 | 1001 | 1s | 10s |

## ❌ Problem Pattern

```python
# 1 query to get posts
posts = Post.objects.all()

# N queries - one per post!
for post in posts:
    print(post.author.name)  # Each access = 1 query
```

## ✅ Solutions

### 1. Eager Loading (JOIN)

**Django:**
```python
# Single query with JOIN
posts = Post.objects.select_related('author').all()
for post in posts:
    print(post.author.name)  # No additional query
```

**SQLAlchemy:**
```python
from sqlalchemy.orm import joinedload

posts = session.query(Post).options(joinedload(Post.author)).all()
```

**Entity Framework:**
```csharp
var posts = context.Posts
    .Include(p => p.Author)
    .ToList();
```

**Prisma:**
```javascript
const posts = await prisma.post.findMany({
  include: { author: true }
});
```

### 2. Batch Loading (Many-to-Many)

**Django:**
```python
# 2 queries: posts + all related tags
posts = Post.objects.prefetch_related('tags').all()
```

**SQLAlchemy:**
```python
from sqlalchemy.orm import selectinload

posts = session.query(Post).options(selectinload(Post.tags)).all()
```

### 3. Manual Batching

```python
# Get all posts
posts = Post.objects.all()

# Get all authors in one query
author_ids = [p.author_id for p in posts]
authors = {a.id: a for a in Author.objects.filter(id__in=author_ids)}

# Map without queries
for post in posts:
    author = authors[post.author_id]
```

### 4. DataLoader (GraphQL)

```javascript
const authorLoader = new DataLoader(async (ids) => {
  const authors = await Author.findByIds(ids);
  return ids.map(id => authors.find(a => a.id === id));
});

// Batches all author requests in one query
const author = await authorLoader.load(post.authorId);
```

## Detection

### Code Patterns to Find
```python
# Look for DB access inside loops
for item in items:
    item.relation.field      # ORM lazy load
    db.query(..., item.id)   # Manual query
    Model.objects.get(...)   # Django ORM
```

### Tools
- **Django:** Django Debug Toolbar, `django-querycount`
- **SQLAlchemy:** `echo=True`, `sqlalchemy-utils`
- **Rails:** `bullet` gem
- **Node:** Query logging middleware

### Logging Queries
```python
# Django settings.py
LOGGING = {
    'handlers': {'console': {...}},
    'loggers': {
        'django.db.backends': {
            'level': 'DEBUG',
            'handlers': ['console'],
        }
    }
}
```

## Quick Reference

| Relationship | Django | SQLAlchemy | EF Core |
|-------------|--------|------------|---------|
| FK (to-one) | `select_related()` | `joinedload()` | `.Include()` |
| M2M (to-many) | `prefetch_related()` | `selectinload()` | `.Include()` |

## References

- [Django select_related](https://docs.djangoproject.com/en/stable/ref/models/querysets/#select-related)
- [SQLAlchemy Loading Techniques](https://docs.sqlalchemy.org/en/14/orm/loading_relationships.html)
