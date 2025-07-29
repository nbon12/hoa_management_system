# Development Guidelines

## Entity Framework Update Patterns

### ✅ Safe Update Pattern (Use This)

When updating entities with foreign key relationships, always use the `SafeUpdateAsync` pattern:

```csharp
public async Task UpdateEntityAsync(Entity entity)
{
    await SafeUpdateAsync(entity, existingEntity =>
    {
        existingEntity.Property = entity.Property;
        existingEntity.ForeignKeyId = entity.ForeignKeyId; // Foreign key updates work correctly
    });
}
```

### ❌ Avoid This Pattern

Never use `context.Set<T>().Update(entity)` for entities with navigation properties:

```csharp
// DON'T DO THIS - Can cause change tracking issues
public async Task UpdateEntityAsync(Entity entity)
{
    using var context = _dbContextFactory.CreateDbContext();
    context.Entities.Update(entity); // ❌ Problematic with navigation properties
    await context.SaveChangesAsync();
}
```

## Testing Foreign Key Relationships

### Required Test Patterns

When creating new entities with foreign key relationships, include these test patterns:

1. **Basic Foreign Key Update Test**
   - Test updating the foreign key property directly
   - Verify the foreign key is updated in the database

2. **Frontend Scenario Test**
   - Test updating with navigation properties loaded (using `Include()`)
   - Simulate the exact frontend workflow
   - Verify both foreign key and navigation property are updated

3. **Service Layer Test**
   - Test the service method specifically
   - Ensure the service handles the update correctly

### Test Template

Use the templates in `HOAManagementCompany.Tests/TestTemplates.cs` as a starting point.

## Code Review Checklist

When reviewing code that updates entities:

- [ ] Does the update method use `SafeUpdateAsync` or explicit property updates?
- [ ] Are there tests for foreign key relationship updates?
- [ ] Are there tests that simulate frontend scenarios (with `Include()`)?
- [ ] Does the service handle navigation properties correctly?

## Common Pitfalls

1. **Using `Update()` with navigation properties loaded**
   - Can cause foreign key updates to fail silently
   - Entity Framework change tracking gets confused

2. **Not testing frontend scenarios**
   - Frontend loads entities with `Include()`
   - Updates may work in isolation but fail in real usage

3. **Assuming foreign key updates work automatically**
   - Always test foreign key relationship updates explicitly
   - Verify both the foreign key and navigation property are updated

## When to Use Each Pattern

### Use `SafeUpdateAsync` When:
- Updating entities with foreign key relationships
- Entities have navigation properties
- You need predictable update behavior

### Use `context.Set<T>().Update()` When:
- Simple entities without navigation properties
- You're updating all properties
- No foreign key relationships involved

## Migration Checklist

When adding new entities with foreign keys:

1. [ ] Create the entity with proper foreign key properties
2. [ ] Add navigation properties if needed
3. [ ] Create the service inheriting from `BaseService`
4. [ ] Implement `GetEntityId` method
5. [ ] Use `SafeUpdateAsync` for update methods
6. [ ] Add integration tests for foreign key updates
7. [ ] Test frontend scenarios with navigation properties
8. [ ] Update this checklist if new patterns are discovered 