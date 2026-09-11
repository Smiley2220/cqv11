# CampusQ Service Layer Architecture

## Overview

The service layer separates business logic from presentation concerns (MVP pattern).

### Architecture Layers

```
┌─────────────────────────────────────────┐
│      Presentation Layer (MVP Views)    │  ← User Interface
│   - Windows Forms (Desktop)             │
│   - Razor Pages (Web)                   │
└─────────────────────────────────────────┘
					↓
┌─────────────────────────────────────────┐
│      Service Layer (Business Logic)    │  ← CampusQ.Core.Services.*
│   - QueueService                        │
│   - UserManagementService (to be added) │
│   - ReportingService (future)           │
└─────────────────────────────────────────┘
					↓
┌─────────────────────────────────────────┐
│      Data Access Layer (Repository)    │  ← CampusQ.MVP.Data.*
│   - QueueRepository                     │
│   - UserRepository                      │
└─────────────────────────────────────────┘
					↓
┌─────────────────────────────────────────┐
│         Database (SQL Server)           │  ← Persisted Data
└─────────────────────────────────────────┘
```

## Service Layer Benefits

✓ **Separation of Concerns** - Business logic separated from UI
✓ **Reusability** - Share services across desktop and web apps  
✓ **Testability** - Services can be unit tested independently
✓ **Maintainability** - Easier to modify business rules
✓ **Single Responsibility** - Each service has one clear purpose

## Current Services

### QueueService
Located: `CampusQ.Core/Services/QueueService.cs`

Orchestrates all queue-related business operations:

```csharp
// Get queue entries
var service = new QueueService(queueRepository);
var allQueue = service.GetAllQueue();
var serviceQueue = service.GetQueueByService("Admission");
var ticket = service.GetTicket(12345);

// Add to queue
var newTicket = service.AddToQueue("ID Processing", "Admission");

// Get statistics
var stats = service.GetQueueStats("Cashier");
// Returns: total, average wait time, first/last ticket times

// Queue management
service.ClearQueue();
var history = service.GetQueueHistory();
```

**Key Methods:**
- `GetAllQueue()` - Get all active tickets
- `GetQueueByService(service)` - Filter by office
- `AddToQueue(purpose, service)` - Create new ticket
- `GetPositionInQueue(ticket#, service, purpose)` - Position calculation
- `GetQueueStats(service)` - Queue metrics
- `ClearQueue()` - Admin action
- `GetQueueHistory()` - View archived tickets

## Adding Service Layer to Presenters

### Before (Monolithic Presenter)
```csharp
public class AdminPresenter
{
	private readonly QueueRepository _queueRepository;
	private readonly UserRepository _userRepository;

	public void RefreshQueue()
	{
		// Business logic mixed with presentation
		var items = _queueRepository.GetHistoryAll();
		var dtos = items.Select(/* complex mapping */).ToList();
		_view.ShowQueue(dtos);
	}
}
```

### After (Service-Oriented Presenter)
```csharp
public class AdminPresenter
{
	private readonly QueueService _queueService;

	public void RefreshQueue()
	{
		// Simplified - delegate to service
		var items = _queueService.GetQueueHistory();
		_view.ShowQueue(items);
	}
}
```

## Creating New Services

### Pattern Template

```csharp
namespace CampusQ.Core.Services
{
	public class MyService
	{
		private readonly MyRepository _repository;
		private readonly ILogger? _logger;

		public MyService(MyRepository repository, ILogger? logger = null)
		{
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
			_logger = logger;
		}

		public MyDto GetSomething(int id)
		{
			try
			{
				_logger?.LogDebug("Getting something: {Id}", id);
				var data = _repository.Get(id);
				return MapToDto(data);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "Failed to get something");
				throw;
			}
		}
	}
}
```

### Best Practices

1. **Dependency Injection**
   ```csharp
   // Service dependency on Repository
   public MyService(MyRepository repository, ILogger? logger = null)
   ```

2. **Logging**
   ```csharp
   _logger?.LogDebug("Operation started");
   _logger?.LogError(ex, "Operation failed");
   ```

3. **Error Handling**
   ```csharp
   catch (ValidationException)
   {
	   throw; // Known error, propagate
   }
   catch (Exception ex)
   {
	   _logger?.LogError(ex, "Unexpected error");
	   throw new RepositoryException("User-friendly message", ex);
   }
   ```

4. **Business Rule Validation**
   ```csharp
   public QueueEntry AddToQueue(string purpose, string service)
   {
	   QueueInputValidator.ValidateQueueEntry(purpose, service);
	   // ... proceed with business logic
   }
   ```

## Refactoring Presenters to Use Services

### Step-by-Step Migration

**Phase 1: Add Service Dependency**
```csharp
public class AdminPresenter
{
	private QueueService _queueService;

	public AdminPresenter(IAdminView view)
	{
		_queueService = new QueueService(
			new QueueRepository(DbConfig.ConnectionString)
		);
	}
}
```

**Phase 2: Replace Repository Calls**
```csharp
// Old
var items = _queueRepository.GetAll();

// New
var items = _queueService.GetAllQueue();
```

**Phase 3: Reduce Presenter Complexity**
- Move all business logic to service
- Presenter only handles UI coordination
- Each service method = one clear operation

### Example Refactoring

**Before:**
```csharp
public void RefreshActiveQueueView()
{
	try
	{
		var entries = _queue_repo.GetAll() ?? new List<QueueEntry>();
		var dtos = entries.Select(e => new QueuePersistDto
		{
			TicketNumber = e.TicketNumber,
			ServiceTicketNumber = e.ServiceTicketNumber,
			Purpose = e.Purpose,
			Service = e.Service,
			TimeAdded = e.TimeAdded
		}).ToList();
		_view.ShowQueue(dtos);
	}
	catch (Exception ex)
	{
		_view.ShowMessage($"Failed: {ex.Message}", "Error", MessageBoxIcon.Error);
	}
}
```

**After:**
```csharp
public void RefreshActiveQueueView()
{
	try
	{
		var queue = _queueService.GetAllQueue();
		_view.ShowQueue(queue);
	}
	catch (Exception ex)
	{
		_view.ShowMessage($"Failed: {ex.Message}", "Error", MessageBoxIcon.Error);
	}
}
```

## Testing Services

Services are unit-test-friendly due to clean separation:

```csharp
[TestClass]
public class QueueServiceTests
{
	private QueueService _service;
	private Mock<QueueRepository> _mockRepo;

	[TestInitialize]
	public void Setup()
	{
		_mockRepo = new Mock<QueueRepository>();
		_service = new QueueService(_mockRepo.Object);
	}

	[TestMethod]
	public void GetAllQueue_ReturnsEmptyList_WhenRepositoryEmpty()
	{
		_mockRepo.Setup(r => r.GetAll()).Returns(new List<QueueEntry>());
		var result = _service.GetAllQueue();
		Assert.AreEqual(0, result.Count);
	}
}
```

## Future Service Expansion

Planned services:

- **UserManagementService** - User account operations
- **ReportingService** - Queue analytics and reports
- **NotificationService** - Ticket status notifications
- **SchedulingService** - Time-based queue operations

Each follows the same pattern for consistency.

## Integration with Dependency Injection

When using .NET DI (ASP.NET Core):

```csharp
// Program.cs
builder.Services.AddScoped<QueueRepository>();
builder.Services.AddScoped<QueueService>();

// Usage in controller/page
public class QueueController(QueueService queueService)
{
	public IActionResult GetQueue()
	{
		var queue = queueService.GetAllQueue();
		return Json(queue);
	}
}
```

## Summary

The service layer provides:
- **Single source of truth** for business logic
- **Consistent error handling** and logging
- **Easy testing** through dependency injection
- **Code reuse** across applications
- **Clear separation** of concerns
