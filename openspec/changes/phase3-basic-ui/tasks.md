## 1. 项目配置

- [x] 1.1 ~~在 `DataManager.csproj` 中添加 `<UseWindowsForms>true</UseWindowsForms>`~~ 改用 .NET 8 内置 `Microsoft.Win32.OpenFolderDialog`，无需额外配置
- [x] 1.2 在 `App.xaml` 中引用 `Resources/Styles.xaml` 资源字典

## 2. ViewModel 层

- [x] 2.1 实现 `FileTreeViewModel`：`Files` 集合属性（ObservableCollection）、`SelectedFile` 属性、`LoadWorkspace(DataWorkspace)` 方法
- [x] 2.2 创建 `JsonRowViewModel`（继承 DynamicObject）：持有 `Dictionary<string, object?>` 存储行数据，支持动态属性访问；添加 `CellTypes` 字典记录每个单元格的 JsonNodeType
- [x] 2.3 实现 `JsonEditorViewModel`：`Columns` 集合、`Rows` 集合、`LoadFile(IJsonDataFile)` 方法（解析 Array of Objects 生成列和行）、`UnsupportedMessage` 属性、浓缩值生成逻辑
- [x] 2.4 实现 `MainViewModel`：`FileTreeVM`/`JsonEditorVM` 子 ViewModel 属性、`OpenFolderCommand`（调用 FolderBrowserDialog）、`SaveCommand`、`StatusText` 属性、文件选中联动逻辑

## 3. View 层 — 布局

- [x] 3.1 实现 `MainWindow.xaml` 布局：顶部 ToolBar（打开文件夹 + 保存按钮）、Grid 左右分栏（FileTreeView + GridSplitter + JsonEditorView）、底部 StatusBar 绑定 StatusText
- [x] 3.2 在 `MainWindow.xaml.cs` 构造函数中创建 MainViewModel 并赋给 DataContext

## 4. View 层 — FileTreeView

- [x] 4.1 实现 `FileTreeView.xaml`：ListView 绑定 Files 集合，显示文件名（脏文件追加 `*`），SelectedItem 双向绑定 SelectedFile

## 5. View 层 — JsonEditorView

- [x] 5.1 实现 `JsonEditorView.xaml`：DataGrid 区域 + 不支持结构提示文本区域，DataGrid 设为 IsReadOnly、AutoGenerateColumns=False
- [x] 5.2 在 `JsonEditorView.xaml.cs` 中实现动态列生成逻辑：监听 ViewModel 的 Columns 变化，代码创建 DataGridTextColumn 并绑定到行 ViewModel 的动态属性
- [x] 5.3 添加行号列（RowHeader 或第一列显示行索引）

## 6. 样式与颜色

- [x] 6.1 在 `Styles.xaml` 中定义值类型颜色常量（String 橙色、Number 紫色、Boolean 绿/红、Null 灰色、复杂值蓝色）
- [x] 6.2 创建 `JsonValueTypeToColorConverter`（IValueConverter），根据 JsonNodeType 返回对应颜色画刷
- [x] 6.3 在 DataGrid 单元格样式中应用颜色转换器

## 7. 集成验证

- [x] 7.1 启动应用 → 打开包含 Traits.json 的文件夹 → 验证左侧显示文件列表
- [x] 7.2 点击 Traits.json → 验证右侧 DataGrid 显示 4 列（id, displayName, description, StatModifiers）、8 行数据
- [x] 7.3 验证 StatModifiers 列浓缩显示效果（如 `[1] {move_speed, flat, 0.3}`、`[]`）
- [x] 7.4 验证状态栏信息正确更新
- [x] 7.5 验证不同类型值的颜色区分
