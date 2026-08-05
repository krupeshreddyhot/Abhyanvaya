# AI31.8.1A — Responsive Validation

Validation is layout-token / CSS-grid based (no functionality changes). Target viewports:

| Viewport | Expectation |
|----------|-------------|
| 1366×768 | Hero 4-col KPIs; sticky toolbar full; attention 2-col; timeline horizontal scroll if needed |
| 1536×864 | Comfortable 4-col hero; operational sections 3–4 col |
| 1920×1080 | Fluid max-width ~1500–1750; full toolbars; 3-col attention |
| 2560×1440 | Content capped at `xl: 1750`; no sparse stretch beyond container |
| Tablet portrait | Toolbar collapsed behind **Tools**; hero 2-col; attention 1-col; timeline scrollable |
| Tablet landscape | Toolbar expanded at `md+`; hero 4-col where width allows |

## Fixes applied (presentation)

- `fluidDashboardSx` max-widths: 1320 / 1500 / 1750
- Grid `minmax(0, 1fr)` to prevent overflow
- Horizontal timeline `overflowX: auto` with `minWidth`
- Reduced vertical padding/gaps (`--dash-gap: 10px`)
- Toolbar sticky with tablet collapse (`display: { xs: toggle, md: block }`)

## Checklist (manual UAT)

- [ ] No horizontal page overflow at 1366×768
- [ ] Sticky toolbar remains usable while scrolling
- [ ] Hero shows only 8 core KPIs
- [ ] Attention cards wrap without clipping buttons
- [ ] Quick action tiles readable on tablet portrait
- [ ] Timeline stages scroll instead of wrapping into tall stacks
- [ ] Print / Export still reachable from toolbar

## Screenshots

Before/after screenshots were not checked into the repository for this phase. Capture during UAT if required for release notes.
