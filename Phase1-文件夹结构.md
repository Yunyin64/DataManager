# 阶段 1：文件夹结构

把项目目录从"默认 WPF 空壳"整理成可维护的工程结构。

设计思想来自游戏项目 Scripts/ 的架构风格：
- **Core / Domain 分层** — 框架基础设施与业务功能分离
- **按领域聚合** — 同一功能模块的 View + ViewModel 放在一起，而非按技术角色拆分
- **Data 独立** — 数据模型层不从属于任何 UI 模块，供多方引用
- **Interface 紧邻基类** — 契约与实现同层

## 目标结构

```
DataManager/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
│
├── Core/                                  # 框架层：与具体业务无关
│   ├── Base/                              #   基类 + 契约
│   │   ├── ViewModelBase.cs               #     ViewModel 基类（继承 ObservableObject）
│   │   └── Interface/                     #     核心接口定义
│   │       ├── IDataWorkspace.cs          #       工作区契约
│   │       └── IJsonDataFile.cs           #       JSON 文件契约
│   ├── Services/                          #   全局服务（横切关注点）
│   │   └── JsonFileService.cs             #     JSON 读写、校验
│   └── Utils/                             #   纯工具
│       ├── Converters/                    #     WPF 值转换器
│       └── Extensions/                    #     扩展方法（预留）
│
├── Data/                                  # 数据层：模型 + 数据抽象
│   ├── DataWorkspace.cs                   #   IDataWorkspace 实现（一个文件夹 = 一个工作区）
│   ├── JsonDataFile.cs                    #   IJsonDataFile 实现（单个 JSON 文件抽象）
│   └── JsonNode/                          #   JSON 节点模型
│       └── JsonNodeVM.cs                  #     节点 ViewModel 包装（阶段 2 实现）
│
├── Domain/                                # 领域层：按功能模块聚合 View + ViewModel
│   ├── Main/                              #   主窗口模块
│   │   └── MainViewModel.cs               #     主窗口 ViewModel
│   ├── FileTree/                          #   文件导航模块
│   │   ├── FileTreeViewModel.cs           #     文件树 ViewModel
│   │   └── FileTreeView.xaml/.cs          #     文件树面板（UserControl）
│   └── JsonEditor/                        #   JSON 编辑模块
│       ├── JsonEditorViewModel.cs         #     JSON 编辑器 ViewModel
│       └── JsonEditorView.xaml/.cs        #     JSON 编辑/显示面板（UserControl）
│
├── Cli/                                   # CLI SDK（从 integration/src/ 引入）
│   ├── CommonCliServer.cs                 #   命名管道服务端
│   ├── WorkspaceRegistry.cs               #   工作区注册表
│   ├── CliArgParser.cs                    #   CLI 参数解析器
│   └── CliService.cs                      #   CLI 服务封装（阶段 4 实现）
│
├── Resources/                             # 样式、图标、资源字典
│
└── DataManager.csproj
```


## 任务清单

- [ ] 1.1 创建文件夹结构
  - 创建 `Core/Base/Interface/`, `Core/Services/`, `Core/Utils/Converters/`, `Core/Utils/Extensions/`
  - 创建 `Data/JsonNode/`
  - 创建 `Domain/Main/`, `Domain/FileTree/`, `Domain/JsonEditor/`
  - 创建 `Cli/`, `Resources/`

- [ ] 1.2 引入 CLI SDK 源文件
  - 把 `integration/src/` 的 `CommonCliServer.cs`, `WorkspaceRegistry.cs`, `CliArgParser.cs` 拷入 `Cli/` 目录
  - 调整命名空间：`CommonCli` → `DataManager.Cli`

- [ ] 1.3 配置 .csproj 依赖
  - 添加 `Newtonsoft.Json` NuGet 包（WorkspaceRegistry 依赖）
  - 添加 `System.IO.Pipes.AccessControl` NuGet 包（CommonCliServer 依赖）
  - 添加 `CommunityToolkit.Mvvm` NuGet 包（MVVM 基础设施）

- [ ] 1.4 搭建骨架文件
  - `Core/Base/ViewModelBase.cs` — 继承 `ObservableObject` 的基类
  - `Core/Base/Interface/IDataWorkspace.cs` — 空接口占位
  - `Core/Base/Interface/IJsonDataFile.cs` — 空接口占位
  - `Core/Services/JsonFileService.cs` — 空壳服务类
  - `Data/DataWorkspace.cs` — 空壳实现类
  - `Data/JsonDataFile.cs` — 空壳实现类
  - `Domain/Main/MainViewModel.cs` — 空壳 ViewModel
  - `Domain/FileTree/FileTreeViewModel.cs` — 空壳 ViewModel
  - `Domain/JsonEditor/JsonEditorViewModel.cs` — 空壳 ViewModel
  - `Cli/CliService.cs` — 空壳服务类（阶段 4 填充）
  - 确保整个项目能编译通过

## 完成标准

- 目录结构清晰，各层职责分明
  - `Core/` 不引用 `Data/`、`Domain/`、`Cli/` 的任何类型
  - `Data/` 可引用 `Core/`，不引用 `Domain/`
  - `Domain/` 可引用 `Core/` 和 `Data/`
  - `Cli/` 保持独立，仅被 `Domain/` 或顶层引用
- CLI SDK 文件已引入，命名空间已调整为 `DataManager.Cli`
- NuGet 依赖已配置（Newtonsoft.Json、System.IO.Pipes.AccessControl、CommunityToolkit.Mvvm）
- 项目编译通过，无报错
