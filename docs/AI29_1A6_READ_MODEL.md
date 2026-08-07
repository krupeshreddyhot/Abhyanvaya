# AI29.1A.6 — Academic Hierarchy Read Model

## Principle

`AcademicHierarchyReadModel` / `AcademicHierarchyNode` are **immutable projections**.

- No writes
- No persistence through these types
- No business logic

## Node shape

| Field | Purpose |
|-------|---------|
| NodeId | Stable key (`EntityType:EntityId`) |
| ParentNodeId | Tree edge |
| EntityId / EntityType | Source identity |
| DisplayName / DisplayOrder / IsActive | Presentation metadata |
| ChildrenCount / HasChildren | Navigation aids |
| NodeType / Icon / ThemeColor / HierarchyLevel / EntityStatus | Metadata only |

## Endpoint

`GET /api/v1/academic-structure/read-model`
