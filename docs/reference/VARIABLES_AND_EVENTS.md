# Graph Variables & Events

NodeEditor.Net supports graph-scoped **variables** and **events** that enable shared state and event-driven execution within your graphs.

## Graph Variables

### What They Are

Graph variables are named, typed values scoped to the graph. When you create a variable, the system automatically generates two node types:

- **Get Variable** — A data node that outputs the variable's current value
- **Set Variable** — A callable node that sets the variable to a new value

### Why They Matter

Variables let you share state across distant parts of your graph without threading long connection wires. They're essential for:
- **Counters and accumulators** — Increment a value across iterations
- **Configuration** — Set parameters that multiple nodes read
- **State machines** — Track the current state of a process
- **Loop variables** — Carry data between iterations

### Creating Variables

**Through the UI:**
Open the **Variables Panel** in the editor sidebar, click "Add Variable", and specify a name and type.

**Programmatically:**

```csharp
editorState.AddVariable(new GraphVariable
{
    Id = Guid.NewGuid().ToString(),
    Name = "Score",
    TypeName = "double",
    DefaultValue = new SocketValue(0.0)
});
```

### Auto-Generated Nodes

When you create a variable named "Score" of type `double`, two node definitions are automatically registered:

| Node | Definition ID | Description |
|------|---------------|-------------|
| **Get Score** | `variable.get.{variableId}` | Outputs the current value of Score |
| **Set Score** | `variable.set.{variableId}` | Sets Score to a new value (callable) |

These nodes appear in the context menu under the "Variables" category.

### How Variables Work During Execution

1. **Seeding**: Before execution begins, `VariableNodeExecutor.SeedVariables()` initializes all variables to their default values in the execution context
2. **Get nodes**: When a Get node executes, it reads the variable's current value from the execution context
3. **Set nodes**: When a Set node executes, it writes a new value to the execution context
4. **Synchronization**: All Get/Set nodes for the same variable share the same storage, so changes are immediately visible

### Variable Factory

The `VariableNodeFactory` service handles creating the node definitions:

```csharp
// This happens automatically when variables are added to state
var factory = serviceProvider.GetRequiredService<VariableNodeFactory>();
var (getterDef, setterDef) = factory.CreateDefinitions(variable);
registry.RegisterDefinitions(new[] { getterDef, setterDef });
```

---

## Graph Events

### What They Are

Graph events provide an event-driven execution model. When you create an event, the system generates two node types:

- **Trigger Event** — Fires the event, starting execution on all listeners
- **Custom Event (Listener)** — Begins execution when the event is fired

### Why They Matter

Events decouple parts of your graph. Instead of hard-wiring execution paths, you can:
- **Modularize**: Trigger events from one graph section, handle them in another
- **React**: Build reactive flows where actions trigger cascading responses
- **Organize**: Keep event triggers near the cause and listeners near the effect

### Creating Events

**Through the UI:**
Open the **Events Panel** in the editor sidebar, click "Add Event", and specify a name.

**Programmatically:**

```csharp
editorState.AddEvent(new GraphEvent
{
    Id = Guid.NewGuid().ToString(),
    Name = "OnPlayerHit"
});
```

### Auto-Generated Nodes

When you create an event named "OnPlayerHit", two node definitions are registered:

| Node | Definition ID | Description |
|------|---------------|-------------|
| **Trigger OnPlayerHit** | `event.trigger.{eventId}` | Fires the event (callable) |
| **OnPlayerHit** | `event.listener.{eventId}` | Starts execution when the event fires (execution initiator) |

These nodes appear in the context menu under the "Events" category.

### How Events Work During Execution

1. **Trigger node executes**: When a Trigger Event node runs, it signals the event
2. **Listeners activate**: All Listener nodes for that event begin their execution chains
3. **Independent execution**: Each listener runs its own execution chain independently

### Event Factory

The `EventNodeFactory` service handles creating the node definitions, mirroring the pattern used by `VariableNodeFactory`:

```csharp
var factory = serviceProvider.GetRequiredService<EventNodeFactory>();
var (triggerDef, listenerDef) = factory.CreateDefinitions(graphEvent);
registry.RegisterDefinitions(new[] { triggerDef, listenerDef });
```

---

## Serialization

Both variables and events are serialized as part of the `GraphData` model:

```json
{
  "version": 1,
  "nodes": [...],
  "connections": [...],
  "variables": [
    {
      "id": "var-1",
      "name": "Score",
      "typeName": "double",
      "defaultValue": { "value": 0.0 }
    }
  ],
  "events": [
    {
      "id": "evt-1",
      "name": "OnPlayerHit"
    }
  ]
}
```

When a graph is loaded, variables and events are restored and their auto-generated nodes are re-registered with the registry.

## MCP Integration

Variables and events are fully accessible via MCP abilities:

| Ability | Description |
|---------|-------------|
| `graph.variable_list` | List all variables in the graph |
| `graph.variable_add` | Add a new variable |
| `graph.variable_remove` | Remove a variable |
| `graph.event_list` | List all events in the graph |
| `graph.event_add` | Add a new event |
| `graph.event_remove` | Remove an event |

## Namespaces

| Type | Namespace |
|------|-----------|
| `GraphVariable` | `NodeEditor.Net.Models` |
| `GraphEvent` | `NodeEditor.Net.Models` |
| `VariableNodeFactory` | `NodeEditor.Net.Services` |
| `EventNodeFactory` | `NodeEditor.Net.Services` |
| `VariableNodeExecutor` | `NodeEditor.Net.Services` |
