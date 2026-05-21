using System.ComponentModel;
using System.Windows.Controls;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace DataManager.Domain.LuaViewer
{
    /// <summary>
    /// Lua 查阅/编辑面板。
    /// </summary>
    public partial class LuaViewerView : UserControl
    {
        private LuaViewerViewModel? _viewModel;

        public LuaViewerView()
        {
            InitializeComponent();

            // DataContext 变更时绑定 ViewModel
            DataContextChanged += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                _viewModel = DataContext as LuaViewerViewModel;

                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                }
            };

            // 监听编辑器内容变更
            LuaEditor.TextChanged += (s, e) =>
            {
                _viewModel?.MarkContentChanged(LuaEditor.Text);
            };

            // 设置 Lua 语法高亮
            SetupLuaHighlighting();
        }

        /// <summary>
        /// ViewModel 属性变更时，同步 Content 到编辑器。
        /// AvalonEdit 不支持直接绑定 Text 属性，需手动同步。
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LuaViewerViewModel.Content))
            {
                if (_viewModel != null && LuaEditor.Text != _viewModel.Content)
                {
                    LuaEditor.Text = _viewModel.Content;
                }
            }
        }

        /// <summary>
        /// 配置 Lua 语法高亮。使用内联定义。
        /// </summary>
        private void SetupLuaHighlighting()
        {
            var luaHighlighting = CreateLuaHighlighting();
            if (luaHighlighting != null)
            {
                LuaEditor.SyntaxHighlighting = luaHighlighting;
            }
        }

        /// <summary>
        /// 创建 Lua 语法高亮定义。
        /// </summary>
        private static IHighlightingDefinition? CreateLuaHighlighting()
        {
            // 适合白色背景的配色
            var xshd = """
                <?xml version="1.0"?>
                <SyntaxDefinition name="Lua" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                  <Color name="Comment" foreground="#008000" fontStyle="italic" />
                  <Color name="String" foreground="#A31515" />
                  <Color name="Keyword" foreground="#0000FF" fontWeight="bold" />
                  <Color name="Number" foreground="#098658" />
                  <Color name="Builtin" foreground="#795E26" />

                  <RuleSet>
                    <Span color="Comment" multiline="true">
                      <Begin>--\[\[</Begin>
                      <End>\]\]</End>
                    </Span>
                    <Span color="Comment">
                      <Begin>--</Begin>
                    </Span>
                    <Span color="String">
                      <Begin>"</Begin>
                      <End>"</End>
                      <RuleSet>
                        <Span begin="\\" end="." />
                      </RuleSet>
                    </Span>
                    <Span color="String">
                      <Begin>'</Begin>
                      <End>'</End>
                      <RuleSet>
                        <Span begin="\\" end="." />
                      </RuleSet>
                    </Span>
                    <Span color="String" multiline="true">
                      <Begin>\[\[</Begin>
                      <End>\]\]</End>
                    </Span>

                    <Keywords color="Keyword">
                      <Word>and</Word>
                      <Word>break</Word>
                      <Word>do</Word>
                      <Word>else</Word>
                      <Word>elseif</Word>
                      <Word>end</Word>
                      <Word>false</Word>
                      <Word>for</Word>
                      <Word>function</Word>
                      <Word>goto</Word>
                      <Word>if</Word>
                      <Word>in</Word>
                      <Word>local</Word>
                      <Word>nil</Word>
                      <Word>not</Word>
                      <Word>or</Word>
                      <Word>repeat</Word>
                      <Word>return</Word>
                      <Word>then</Word>
                      <Word>true</Word>
                      <Word>until</Word>
                      <Word>while</Word>
                    </Keywords>

                    <Keywords color="Builtin">
                      <Word>print</Word>
                      <Word>type</Word>
                      <Word>tostring</Word>
                      <Word>tonumber</Word>
                      <Word>pairs</Word>
                      <Word>ipairs</Word>
                      <Word>next</Word>
                      <Word>select</Word>
                      <Word>unpack</Word>
                      <Word>require</Word>
                      <Word>pcall</Word>
                      <Word>xpcall</Word>
                      <Word>error</Word>
                      <Word>assert</Word>
                      <Word>setmetatable</Word>
                      <Word>getmetatable</Word>
                      <Word>rawget</Word>
                      <Word>rawset</Word>
                      <Word>self</Word>
                    </Keywords>

                    <Rule color="Number">
                      \b0[xX][0-9a-fA-F]+\b|\b\d+\.?\d*([eE][+-]?\d+)?\b
                    </Rule>
                  </RuleSet>
                </SyntaxDefinition>
                """;

            try
            {
                using var reader = new XmlTextReader(new System.IO.StringReader(xshd));
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch
            {
                return null;
            }
        }
    }
}
