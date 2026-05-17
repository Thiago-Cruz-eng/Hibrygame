---
title: XSS Prevention
impact: CRITICAL
category: security
tags: xss, security, frontend, sanitization
---

# Cross-Site Scripting (XSS) Prevention

> **Never render user input as HTML without sanitization.**

## Types of XSS

| Type | Vector | Example |
|------|--------|---------|
| **Reflected** | URL parameters | `?name=<script>alert(1)</script>` |
| **Stored** | Database content | Comment with malicious script |
| **DOM-based** | Client-side JS | `innerHTML = location.hash` |

## ❌ Vulnerable Patterns

```javascript
// DOM XSS
element.innerHTML = userInput;

// React bypass
<div dangerouslySetInnerHTML={{__html: userContent}} />

// jQuery
$('#output').html(userInput);
```

## ✅ Secure Patterns

```javascript
// Use textContent for text
element.textContent = userInput;

// Sanitize if HTML is needed
import DOMPurify from 'dompurify';
element.innerHTML = DOMPurify.sanitize(userInput);

// React - default is safe
<div>{userInput}</div>  // Auto-escaped
```

## Framework Defaults

| Framework | Default Behavior |
|-----------|-----------------|
| React | Auto-escapes `{}` expressions |
| Vue | Auto-escapes `{{ }}` interpolation |
| Angular | Auto-escapes by default |
| EJS | `<%= %>` escapes, `<%- %>` is raw |
| Jinja2 | Auto-escapes by default |

## Sanitization Libraries

### JavaScript
```javascript
import DOMPurify from 'dompurify';

const clean = DOMPurify.sanitize(dirty, {
  ALLOWED_TAGS: ['b', 'i', 'em', 'strong', 'a'],
  ALLOWED_ATTR: ['href']
});
```

### Python
```python
import bleach

clean = bleach.clean(
    dirty,
    tags=['b', 'i', 'em', 'strong', 'a'],
    attributes={'a': ['href']}
)
```

## Defense in Depth

### Content Security Policy
```http
Content-Security-Policy: default-src 'self'; script-src 'self'
```

### HTTPOnly Cookies
```javascript
// Server-side
res.cookie('session', token, { httpOnly: true, secure: true });
```

### Input Validation
```javascript
// Validate expected format
const email = input.match(/^[^\s@]+@[^\s@]+\.[^\s@]+$/);
if (!email) throw new Error('Invalid email');
```

## Detection Checklist

- [ ] Search for `innerHTML`, `outerHTML`
- [ ] Search for `dangerouslySetInnerHTML`
- [ ] Search for `$.html()`, `$(x).html()`
- [ ] Search for `document.write()`
- [ ] Check template raw output (`<%- %>`, `|safe`, `{!! !!}`)

## References

- [OWASP XSS Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)
- [DOMPurify](https://github.com/cure53/DOMPurify)
