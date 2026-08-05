# AI31.8.2 — Dashboard Layout

## Final section order

1. Dashboard title  
2. Sticky operations toolbar (Last Updated / Next Refresh / tools)  
3. **Executive Context** (≤70px ribbon)  
4. **Active Filters** (collapsible; only when filters set)  
5. **Morning Brief**  
6. **Executive Summary** (8 operational KPIs)  
7. Action banners (if any)  
8. **Attention Required**  
9. **Today's Academic Timeline**  
10. **Today's Operations**  
11. **Attendance Operations**  
12. **Timetable Operations**  
13. **Analytics** (historical charts)  
14. **Academic Resources**  
15. **System Health**  
16. **Quick Actions**

## Density

| Token | AI31.8.1A | AI31.8.2 |
|-------|-----------|----------|
| `--dash-card-sm` | 140px | 112px (~20–30% shorter) |
| `--dash-card-md` | 180px | 160px |
| `--dash-card-lg` | 280px | 240px |
| `--dash-gap` | 10px | 8px |
| `--dash-context-max-h` | — | 70px |

## KPI card design system

```
[Icon] [Status]
Large Value   [Trend]
Subtitle
(Open on hover)
```

- No repeated timestamps / View Details / Information badges  
- Hover tooltip for explanation / impact  
- Overflow menu for Open / Module / Report / Help  

## Responsive

Fluid max-widths unchanged (1320 / 1500 / 1750). Toolbar collapses on tablet. Context ribbon wraps compactly.
