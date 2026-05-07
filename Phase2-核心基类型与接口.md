# 阶段 2：核心基类型设计、接口抽象

设计数据模型和接口，为 JSON 数据的加载/访问/修改建立统一的抽象层。

## 架构设计

```
                ┌──────────────────┐
                │  IDataWorkspace  │  一个工作目录
                │  - Path          │
                │  - Files[]       │
                │  - Load()        │
                │  - Save()        │
                └────────┬─────────┘
                         │ 包含多个
                         ▼
                ┌──────────────────┐
                │  IJsonDataFile   │  一个 .json 文件
                │  - FilePath      │
                │  - FileName      │
                │  - RootNode      │
                │  - IsDirty       │
                │  - Load()        │
                │  - Save()        │
                │  - Query(path)   │  JSONPath 查询
                │  - Modify(...)   │  修改某节点
                └────────┬─────────┘
                         │ 内容是
                         ▼
                ┌──────────────────┐
                │  JsonNodeVM      │  JSON 节点的 ViewModel 包装
                │  - Key           │
                │  - Value         │
                │  - NodeType      │  Object / Array / Value
                │  - Children[]    │
                │  - Parent        │
                └──────────────────┘
```

## 当前文件结构

```
DataManager/
├── Core/
│   ├── Base/
│   │   ├── Interface/
│   │   │   ├── IDataWorkspace.cs      ✅ 接口已定义（RootPath / Files / Load / Save / GetFile）
│   │   │   └── IJsonDataFile.cs       ✅ 接口已定义（FilePath / FileName / RootToken / IsDirty / Load / Save / Query / Modify）
│   │   └── ViewModelBase.cs           ✅ 已实现（继承 CommunityToolkit.Mvvm.ObservableObject）
│   ├── Services/
│   │   └── JsonFileService.cs         ✅ 已实现（ReadJson / WriteJson / ScanJsonFiles）
│   └── Utils/
│       ├── Converters/
│       │   └── .gitkeep
│       └── Extensions/
│           └── .gitkeep
├── Data/
│   ├── DataWorkspace.cs               ✅ 已实现（扫描加载 / 批量保存 / 按名获取）
│   ├── JsonDataFile.cs                ✅ 已实现（JToken 存储 / Load / Save / Query / Modify / IsDirty）
│   └── JsonNode/
│       └── JsonNodeVM.cs              ✅ 已实现（递归包装 / NodeType / Children / Parent / Value）
├── Domain/
│   ├── FileTree/
│   │   ├── FileTreeView.xaml
│   │   ├── FileTreeView.xaml.cs
│   │   └── FileTreeViewModel.cs       （阶段 3 实现）
│   ├── JsonEditor/
│   │   ├── JsonEditorView.xaml
│   │   ├── JsonEditorView.xaml.cs
│   │   └── JsonEditorViewModel.cs     （阶段 3 实现）
│   └── Main/
│       └── MainViewModel.cs           （阶段 3 实现）
├── Cli/
│   ├── CliArgParser.cs
│   ├── CliService.cs
│   ├── CommonCliServer.cs
│   └── WorkspaceRegistry.cs
├── Resources/
│   └── Styles.xaml
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── AssemblyInfo.cs
└── DataManager.csproj
```

### NuGet 依赖（已引入）

| 包名 | 版本 | 用途 |
|---|---|---|
| `CommunityToolkit.Mvvm` | 8.* | MVVM 基础设施：`ObservableObject`、`RelayCommand` 等 |
| `Newtonsoft.Json` | 13.* | JSON 解析/序列化，底层使用 `JToken` |
| `System.IO.Pipes.AccessControl` | 5.* | CLI 管道通信 |

## 任务清单

- [x] 2.1 MVVM 基础设施
  - ✅ 已引入 `CommunityToolkit.Mvvm` (8.*)
  - ✅ `ViewModelBase` 已实现，继承 `ObservableObject`（路径：`Core/Base/ViewModelBase.cs`）
  - 所有 ViewModel（`FileTreeViewModel`、`JsonEditorViewModel`、`MainViewModel`、`JsonNodeVM`）均已继承 `ViewModelBase`

- [x] 2.2 设计 `IDataWorkspace` 接口
  - ✅ `Core/Base/Interface/IDataWorkspace.cs`
  - `RootPath` — 工作区根目录路径
  - `Files` — `IReadOnlyList<IJsonDataFile>` 文件列表
  - `Load()` — 扫描文件夹下所有 `.json` 并加载
  - `Save()` — 保存所有脏文件
  - `GetFile(fileName)` — 按名称获取文件

- [x] 2.3 设计 `IJsonDataFile` 接口
  - ✅ `Core/Base/Interface/IJsonDataFile.cs`
  - `FilePath` / `FileName` — 文件路径信息
  - `RootToken` — `JToken?` JSON 根节点
  - `IsDirty` — 是否有未保存的修改
  - `Load()` / `Save()` — 读写磁盘
  - `Query(jsonPath)` — `IEnumerable<JToken>` JSONPath 查询
  - `Modify(jsonPath, value)` — 修改指定节点

- [x] 2.4 实现 `DataWorkspace`
  - ✅ `Data/DataWorkspace.cs` — 实现 `IDataWorkspace`
  - 通过 `JsonFileService.ScanJsonFiles()` 扫描目录
  - 为每个文件创建 `JsonDataFile` 并调用 `Load()`
  - `Save()` 只保存 `IsDirty` 的文件
  - `GetFile()` 按文件名忽略大小写匹配

- [x] 2.5 实现 `JsonDataFile`
  - ✅ `Data/JsonDataFile.cs` — 实现 `IJsonDataFile`
  - 基于 `Newtonsoft.Json` 的 `JToken` 做底层数据存储
  - `Load()` 通过 `JsonFileService.ReadJson()` 加载
  - `Save()` 通过 `JsonFileService.WriteJson()` 写回
  - `Query()` 使用 `JToken.SelectTokens(jsonPath)`
  - `Modify()` 使用 `JToken.SelectToken()` + `Replace()`
  - 脏标记：`Modify()` 后标记 dirty，`Load()` / `Save()` 后清除

- [x] 2.6 实现 `JsonNodeVM`
  - ✅ `Data/JsonNode/JsonNodeVM.cs` — 继承 `ViewModelBase`，使用 `[ObservableProperty]` 源生成
  - `JsonNodeType` 枚举：Object / Array / String / Number / Boolean / Null
  - 递归 `BuildChildren()`：Object 按 property 展开，Array 按 index 展开
  - `Parent` 反向引用
  - `Value` 属性：读写 `JValue.Value`，修改后自动通知
  - `DisplayValue`：Object 显示 `{ N items }`，Array 显示 `[ N items ]`
  - `FromToken()` 静态工厂方法
  - `Rebuild()` 重建子树

- [x] 2.7 实现 `JsonFileService`
  - ✅ `Core/Services/JsonFileService.cs`
  - `ReadJson(filePath)` — 读取文件 → `JToken.Parse()`，含 FileNotFound / JSON 格式异常处理
  - `WriteJson(filePath, token)` — `Formatting.Indented` 序列化写出，自动创建目录
  - `ScanJsonFiles(directoryPath, recursive)` — 扫描 `*.json`，支持递归/非递归

## 完成标准

- 接口定义清晰，职责单一
- 能在代码层面加载一个文件夹、遍历所有 JSON 文件、访问节点树
- `JsonNodeVM` 能正确递归展开任意 JSON 结构
- 单元测试能跑通基本的 Load → Query → Modify → Save 流程
