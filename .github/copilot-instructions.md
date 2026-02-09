# Documentation Standards

當我要求產生 Markdown 文件時，請依照下列邏輯決定檔案路徑：

## 1. 檔案位置 (File Location Strategy)

### 基準點 (Anchor)
請偵測**目前正在編輯**或**對話中引用**的程式碼檔案 (`.cs`, `.xaml`) 所在的目錄。

### 向上搜尋
從該檔案所在目錄向上尋找最近的 `.csproj` 檔案。該 `.csproj` 所在的目錄即為**「目標專案根目錄」**。

### 最終路徑
將 Markdown 檔案產生於 `[目標專案根目錄]/Markdown/`。

### 情境範例 (Scenario)

假設方案結構：

```
MySolution.sln
├── CoreProject/
│   ├── Core.csproj
│   └── Utils/
│       └── Logger.cs  <-- 目前正在看這個檔案 (Active Context)
└── UIProject/
    ├── UI.csproj
    └── MainWindow.xaml
```

若我針對 `Logger.cs` 要求產生文件：
- AI 應識別 `Logger.cs` 屬於 `CoreProject`。
- ✅ **正確路徑**: `CoreProject/Markdown/LoggerDocs.md`
- ❌ **禁止路徑**: `Markdown/LoggerDocs.md` (這是方案層級)
- ❌ **禁止路徑**: `UIProject/Markdown/LoggerDocs.md` (跑錯專案)

---

## 2. 檔案編碼 (File Encoding)
- 所有 Markdown 文件必須使用 UTF-8 編碼儲存
- ✅ DO save all .md files with UTF-8 encoding (without BOM)
- ❌ DO NOT use ANSI, Big5, or other encodings

---

## 3. 檔案標頭 (Metadata)

每個 Markdown 檔案開頭必須包含 YAML front matter：

```yaml
---
created: YYYY-MM-DD
updated: YYYY-MM-DD
tags: [C#, MVVM, Service, DI]
related: 
  - ../Services/IProductService.cs
  - ./ArchitectureDocs.md
---
```

### 欄位說明
- **created**: 文件初次建立日期
- **updated**: 最後更新日期
- **tags**: 相關技術標籤 (便於搜尋)
- **related**: 相關檔案或文件的相對路徑

---

## 4. 連結參照 (Cross-References)

使用相對路徑連結其他文件或程式碼：

```markdown
## 參考資料
- [Logger 實作說明](./LoggerDocs.md)
- [IProductService 介面定義](../Services/IProductService.cs)
- [MVVM 架構概述](./MVVMPattern.md)
```

### 連結規則
- **同目錄**: `./FileName.md`
- **上層目錄**: `../ParentFolder/FileName.md`
- **程式碼檔案**: 使用完整相對路徑，例如 `../Services/ProductService.cs`

---

# C# Coding Conventions

## Naming Rules (命名規則)

### 1. Private Fields (私有欄位)
- 必須使用 `my` 作為前綴，並採用 **camelCase** (小駝峰式命名)。
- ❌ **DO NOT** use the underscore prefix `_` (e.g., `_temp`)
- ✅ **DO** use the `my` prefix (e.g., `myTemp`, `myCount`, `myService`)

**範例：**

```csharp
// ❌ Bad
private int _count;
private string _userName;
private ILogger _logger;

// ✅ Good
private int myCount;
private string myUserName;
private ILogger myLogger;
```

### 2. Public Properties (公開屬性)
- 使用 **PascalCase** (大駝峰式命名)。

**範例：**

```csharp
// ✅ Good
public string UserName { get; set; }
public int TotalCount { get; private set; }
public IProductService ProductService { get; }
```

### 3. Methods (方法)
- 使用 **PascalCase** + **動詞開頭**。
- Private methods 也遵循相同規則。

**範例：**

```csharp
// ✅ Good
public void LoadData() { }
public async Task<bool> SaveChangesAsync() { }
private bool ValidateInput(string input) { }
private void InitializeComponents() { }
```

### 4. Constants (常數)
- 使用 **UPPER_CASE** + **底線分隔**。

**範例：**

```csharp
// ✅ Good
private const int MAX_RETRY_COUNT = 3;
public const string DEFAULT_CONNECTION_STRING = "Server=localhost;";
```

### 5. Local Variables (區域變數)
- 使用 **camelCase**。

**範例：**

```csharp
// ✅ Good
public void ProcessData()
{
    int itemCount = 0;
    string userName = GetUserName();
    var productList = new List<Product>();
}
```

---

# XAML Naming Conventions

## View Naming Rules (視圖命名規則)

### 1. UserControl (使用者控制項)
- 建立 UserControl 時，檔案名稱必須以 `ViewControl` 結尾。
- ✅ **DO** use the `ViewControl` suffix

**範例：**
- `DashboardViewControl.xaml`
- `SettingsViewControl.xaml`

### 2. Window (視窗)
- 建立 Window 時，檔案名稱必須以 `Window` 結尾。
- ✅ **DO** use the `Window` suffix

**範例：**
- `MainWindow.xaml`
- `ConfigWindow.xaml`

**錯誤範例：**

```
❌ Bad
Dashboard.xaml
Settings.xaml
Config.xaml

✅ Good (UserControl)
DashboardViewControl.xaml
SettingsViewControl.xaml

✅ Good (Window)
MainWindow.xaml
ConfigWindow.xaml
```

---

## Element Naming Rules (元素命名規則)

### 具名元素 (Named Elements)
- 使用 **PascalCase** + **控制項類型後綴**。
- ✅ **DO** use suffixes like `Button`, `TextBox`, `ListView`, `Grid`

**範例：**

```xml
<!-- ❌ Bad -->
<Button x:Name="submit"/>
<TextBox x:Name="user"/>
<ListView x:Name="items"/>

<!-- ✅ Good -->
<Button x:Name="SubmitButton"/>
<TextBox x:Name="UserNameTextBox"/>
<ListView x:Name="ProductListView"/>
<Grid x:Name="MainContentGrid"/>
```

### 常見控制項後綴表

| 控制項類型 | 後綴範例 |
|-----------|---------|
| Button | `SubmitButton`, `CancelButton` |
| TextBox | `UserNameTextBox`, `PasswordTextBox` |
| ComboBox | `CategoryComboBox`, `StatusComboBox` |
| ListView | `ProductListView`, `OrderListView` |
| DataGrid | `CustomerDataGrid` |
| Grid | `MainContentGrid`, `HeaderGrid` |
| StackPanel | `ToolbarStackPanel` |
| TextBlock | `TitleTextBlock`, `ErrorMessageTextBlock` |

---

# Software Architecture & Design Patterns

## Dependency Injection (DI) Strategy

### 1. Dependency Inversion Principle (依賴反轉原則)

- 所有的商業邏輯類別 (Services, ViewModels) 必須**依賴於介面 (interface)**，而非具體實作 (class)。
- ❌ **DO NOT** instantiate dependencies using `new` inside a class
- ✅ **DO** inject dependencies via the **Constructor** (建構式注入)

### 2. Service Definition (服務定義)

建立 Service 時，必須成對建立：
- `I{ServiceName}.cs` (Interface) - 定義合約
- `{ServiceName}.cs` (Implementation) - 實作邏輯

**範例：**

```csharp
// ✅ Good: Interface Definition
public interface IProductService
{
    Task<List<Product>> GetAllProductsAsync();
    Task<bool> SaveProductAsync(Product product);
}

// ✅ Good: Implementation
public class ProductService : IProductService
{
    private readonly ILogger<ProductService> myLogger;
    private readonly IDbContext myDbContext;

    public ProductService(ILogger<ProductService> logger, IDbContext dbContext)
    {
        myLogger = logger;
        myDbContext = dbContext;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        myLogger.LogInformation("Fetching all products");
        return await myDbContext.Products.ToListAsync();
    }

    public async Task<bool> SaveProductAsync(Product product)
    {
        // Implementation...
        return true;
    }
}
```

### 3. Lifecycle Awareness (生命週期意識)

當產生 Service 程式碼時，必須考慮並建議適當的生命週期：

| 生命週期 | 使用時機 | 範例 |
|---------|---------|------|
| **Transient** | 輕量、無狀態服務 (Stateless) | 資料驗證器、計算引擎 |
| **Singleton** | 快取、全域設定、Log 服務 | Logger, Configuration, Cache |
| **Scoped** | (Web Context) 單次請求內共用 | DbContext, UnitOfWork |

### 4. DI Registration (註冊範例)

在 `Program.cs` 或 `Startup.cs` 中註冊服務：

```csharp
// ✅ Good: Service Registration
var builder = WebApplication.CreateBuilder(args);

// Transient: 每次注入都是新實例
builder.Services.AddTransient<IProductService, ProductService>();
builder.Services.AddTransient<IValidator, InputValidator>();

// Singleton: 全應用程式單一實例
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// Scoped: (僅 Web) 每次請求單一實例
builder.Services.AddScoped<IDbContext, ApplicationDbContext>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
```

### 5. ViewModel Injection (ViewModel 注入範例)

```csharp
// ❌ Bad: Tight Coupling & Violation of DIP (依賴反轉原則)
public class ProductViewModel
{
    // ❌ 直接實例化 → 無法抽換實作、難以測試
    private ProductService _service = new ProductService();
    
    public void LoadData()
    {
        _service.FetchItems();
    }
}

// ✅ Good: Dependency Injection & Correct Naming
public class ProductViewModel
{
    // ✅ 遵循 "my" 前綴規則
    private readonly IProductService myProductService;
    private readonly ILogger<ProductViewModel> myLogger;

    // ✅ Constructor Injection
    public ProductViewModel(IProductService productService, ILogger<ProductViewModel> logger)
    {
        myProductService = productService;
        myLogger = logger;
    }

    public async Task LoadDataAsync()
    {
        try
        {
            myLogger.LogInformation("Loading product data");
            var products = await myProductService.GetAllProductsAsync();
            // Update UI...
        }
        catch (Exception ex)
        {
            myLogger.LogError(ex, "Failed to load product data");
        }
    }
}
```

---

## 完整範例 (Complete Example)

### 檔案結構

```
MyApp/
├── Services/
│   ├── IProductService.cs
│   └── ProductService.cs
├── ViewModels/
│   └── ProductViewModel.cs
└── Views/
    └── ProductViewControl.xaml
```

### IProductService.cs

```csharp
public interface IProductService
{
    Task<List<Product>> GetAllProductsAsync();
    Task<bool> SaveProductAsync(Product product);
    Task<bool> DeleteProductAsync(int productId);
}
```

### ProductService.cs

```csharp
public class ProductService : IProductService
{
    private readonly IDbContext myDbContext;
    private readonly ILogger<ProductService> myLogger;

    public ProductService(IDbContext dbContext, ILogger<ProductService> logger)
    {
        myDbContext = dbContext;
        myLogger = logger;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        myLogger.LogInformation("Fetching all products");
        return await myDbContext.Products.ToListAsync();
    }

    public async Task<bool> SaveProductAsync(Product product)
    {
        try
        {
            myDbContext.Products.Add(product);
            await myDbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            myLogger.LogError(ex, "Failed to save product");
            return false;
        }
    }

    public async Task<bool> DeleteProductAsync(int productId)
    {
        var product = await myDbContext.Products.FindAsync(productId);
        if (product == null) return false;

        myDbContext.Products.Remove(product);
        await myDbContext.SaveChangesAsync();
        return true;
    }
}
```

### ProductViewModel.cs

```csharp
public class ProductViewModel : ViewModelBase
{
    private readonly IProductService myProductService;
    private readonly ILogger<ProductViewModel> myLogger;
    
    private ObservableCollection<Product> myProducts;
    public ObservableCollection<Product> Products
    {
        get => myProducts;
        set => SetProperty(ref myProducts, value);
    }

    public ProductViewModel(IProductService productService, ILogger<ProductViewModel> logger)
    {
        myProductService = productService;
        myLogger = logger;
        
        LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
    }

    public ICommand LoadDataCommand { get; }

    private async Task LoadDataAsync()
    {
        try
        {
            myLogger.LogInformation("Loading product data");
            var products = await myProductService.GetAllProductsAsync();
            Products = new ObservableCollection<Product>(products);
        }
        catch (Exception ex)
        {
            myLogger.LogError(ex, "Failed to load product data");
        }
    }
}
```

---

## 總結 (Summary)

### ✅ 必須遵守的規則

1. Private fields 使用 `my` 前綴 + camelCase
2. Public properties/methods 使用 PascalCase
3. UserControl 檔名以 `ViewControl` 結尾
4. Window 檔名以 `Window` 結尾
5. XAML 元素命名使用 PascalCase + 控制項後綴
6. 所有依賴必須透過建構式注入
7. Service 必須定義 Interface + Implementation
8. Markdown 文件放在專案根目錄的 `Markdown/` 資料夾
9. 所有 Markdown 文件必須使用 UTF-8 編碼儲存

### ❌ 禁止的做法

1. Private fields 使用底線前綴 `_`
2. 在類別內部直接 `new` 實例化依賴項
3. UserControl 或 Window 不遵循命名後綴
4. XAML 元素使用 camelCase 或無類型後綴
5. Markdown 文件放錯專案目錄
6. Markdown 文件使用非 UTF-8 編碼 (如 ANSI, Big5)