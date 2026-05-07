# Global Data Service Usage Guide

This document explains how to use the Global Data Service to access current user and shop details throughout the application.

## Overview

The Global Data Service provides centralized access to:
- Current user details (from `/api/v1/users/current`)
- Shop details (from `/api/v1/shop-info`)

## API Endpoints

- **Current User**: `https://pos-go-api-dev.delivergate.com/api/v1/users/current`
- **Shop Details**: `https://pos-go-api-dev.delivergate.com/api/v1/shop-info?code={outletCode}&brandId={brandId}`

## Models

### CurrentUserModel
```csharp
public class CurrentUserModel
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public string ContactNo { get; set; }
    public string Status { get; set; }
    public string RoleId { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => string.Join("", (FirstName + " " + LastName).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => n[0])).ToUpper();
}
```

### ShopModel
```csharp
public class ShopModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public string ContactNo { get; set; }
    public string Status { get; set; }
    public string Currency { get; set; }
    public string CurrencyCode { get; set; }
    public bool HasCashPayment { get; set; }
    public bool HasCardPayment { get; set; }
    // ... and many more properties
}
```

## Usage in ViewModels

### 1. Direct Access
```csharp
public class MyViewModel : BaseViewModel
{
    // Direct access to global data
    public CurrentUserModel CurrentUser => GlobalDataService.Instance.CurrentUser;
    public ShopModel ShopDetails => GlobalDataService.Instance.ShopDetails;
    
    // Convenience properties
    public string CurrentUserName => CurrentUser?.FullName ?? "Unknown User";
    public string CurrentUserRole => CurrentUser?.Role ?? "Unknown Role";
    public string ShopName => ShopDetails?.Name ?? "Unknown Shop";
    public string ShopAddress => ShopDetails?.Address ?? "Unknown Address";
}
```

### 2. Event-Based Updates
```csharp
public class MyViewModel : BaseViewModel
{
    public MyViewModel()
    {
        // Subscribe to data changes
        GlobalDataService.Instance.CurrentUserChanged += OnCurrentUserChanged;
        GlobalDataService.Instance.ShopDetailsChanged += OnShopDetailsChanged;
    }
    
    private void OnCurrentUserChanged(CurrentUserModel user)
    {
        // Handle user data changes
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(CurrentUserRole));
    }
    
    private void OnShopDetailsChanged(ShopModel shop)
    {
        // Handle shop data changes
        OnPropertyChanged(nameof(ShopName));
        OnPropertyChanged(nameof(ShopAddress));
    }
}
```

## Usage in Views (XAML)

### 1. Direct Binding
```xml
<TextBlock Text="{Binding CurrentUserName}" />
<TextBlock Text="{Binding CurrentUserRole}" />
<TextBlock Text="{Binding ShopName}" />
<TextBlock Text="{Binding ShopAddress}" />
```

### 2. Conditional Display
```xml
<StackPanel>
    <TextBlock Text="Welcome, " />
    <TextBlock Text="{Binding CurrentUserName}" FontWeight="Bold" />
    <TextBlock Text=" (" />
    <TextBlock Text="{Binding CurrentUserRole}" />
    <TextBlock Text=")" />
</StackPanel>
```

### 3. Shop Information Display
```xml
<StackPanel>
    <TextBlock Text="{Binding ShopName}" FontSize="18" FontWeight="Bold" />
    <TextBlock Text="{Binding ShopAddress}" FontSize="12" />
    <TextBlock Text="{Binding ShopDetails.ContactNo}" FontSize="12" />
</StackPanel>
```

## Service Methods

### GlobalDataService.Instance

#### Properties
- `CurrentUser` - Current user details
- `ShopDetails` - Shop details

#### Methods
- `LoadDataAfterLoginAsync()` - Load data after successful login
- `LoadDataFromStorage()` - Load data from local storage
- `ClearData()` - Clear all data (called on logout)
- `RefreshCurrentUserAsync()` - Refresh current user data
- `RefreshShopDetailsAsync()` - Refresh shop data

#### Events
- `CurrentUserChanged` - Fired when current user data changes
- `ShopDetailsChanged` - Fired when shop data changes

## Automatic Data Management

### Login Process
1. User logs in successfully
2. `LoginViewModel` calls `GlobalDataService.Instance.LoadDataAfterLoginAsync()`
3. Both current user and shop details are fetched and stored locally
4. Data becomes available globally

### Application Startup
1. `App.xaml.cs` calls `GlobalDataService.Instance.LoadDataFromStorage()`
2. Previously stored data is loaded from local storage
3. Data is available immediately if user was previously logged in

### Logout Process
1. User clicks logout
2. `GlobalDataService.Instance.ClearData()` is called
3. All data is cleared from memory and local storage
4. User is redirected to login page

## Error Handling

The service includes built-in error handling:
- API failures are logged but don't crash the application
- Missing data returns null or default values
- Local storage failures are handled gracefully

## Best Practices

1. **Always use null-conditional operators** when accessing data:
   ```csharp
   var userName = CurrentUser?.FullName ?? "Unknown User";
   ```

2. **Subscribe to events** for reactive updates:
   ```csharp
   GlobalDataService.Instance.CurrentUserChanged += OnUserChanged;
   ```

3. **Use convenience properties** for common data:
   ```csharp
   public string CurrentUserName => CurrentUser?.FullName ?? "Unknown User";
   ```

4. **Handle missing data gracefully** in UI:
   ```xml
   <TextBlock Text="{Binding CurrentUserName, FallbackValue='Loading...'}" />
   ```

## Example Implementation

See `CashierHomeViewModel.cs` for a complete example of how to integrate global data into a ViewModel. 