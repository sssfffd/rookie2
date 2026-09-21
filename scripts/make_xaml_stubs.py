# -*- coding: utf-8 -*-
"""XAML 이 만들어 주는 partial 클래스를 흉내 냅니다 (검사 전용).

진짜 빌드에서는 XAML 이 InitializeComponent() 와 x:Name 필드를 담은
.g.cs 를 만들어 줍니다. 리눅스에는 그 도구가 없어서, 코드 비하인드가
컴파일이라도 되게 같은 모양의 껍데기를 만들어 둡니다.

DataTemplate / ControlTemplate 안의 x:Name 은 필드가 생기지 않으므로
진짜 WPF 와 똑같이 빼 둡니다. 그래야 "템플릿 안의 이름을 코드에서 쓰는"
잘못을 여기서도 잡습니다.
"""
import io, os, re, sys, glob

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 우리가 만든 타입은 진짜 타입으로 붙입니다. 그래야 그 위의 메서드 호출까지 검사됩니다.
OURS = {
    'GraphCanvas': 'LogScope.App.Controls.GraphCanvas',
    'HeatmapCanvas': 'LogScope.App.Controls.HeatmapCanvas',
    'DashboardView': 'LogScope.App.Views.DashboardView',
    'GraphView': 'LogScope.App.Views.GraphView',
    'HeatmapView': 'LogScope.App.Views.HeatmapView',
    'AlignPanel': 'LogScope.App.Views.AlignPanel',
}
WPF = {
    'ListView': 'System.Windows.Controls.ListView',
    'ListBox': 'System.Windows.Controls.ListBox',
    'TextBox': 'System.Windows.Controls.TextBox',
    'TextBlock': 'System.Windows.Controls.TextBlock',
    'ComboBox': 'System.Windows.Controls.ComboBox',
    'Button': 'System.Windows.Controls.Button',
    'CheckBox': 'System.Windows.Controls.CheckBox',
    'RadioButton': 'System.Windows.Controls.RadioButton',
    'ToggleButton': 'System.Windows.Controls.Primitives.ToggleButton',
    'Border': 'System.Windows.Controls.Border',
    'Grid': 'System.Windows.Controls.Grid',
    'StackPanel': 'System.Windows.Controls.StackPanel',
    'ScrollBar': 'System.Windows.Controls.ScrollBar',
    'ProgressBar': 'System.Windows.Controls.ProgressBar',
    'TabControl': 'System.Windows.Controls.TabControl',
    'Image': 'System.Windows.Controls.Image',
    'Popup': 'System.Windows.Controls.Primitives.Popup',
    'WrapPanel': 'System.Windows.Controls.WrapPanel',
    'DockPanel': 'System.Windows.Controls.DockPanel',
}


def main(out_path):
    out = ['// XAML 이 생성하는 부분을 흉내 낸 것입니다 (scripts/make_xaml_stubs.py).']
    for x in sorted(glob.glob(os.path.join(ROOT, 'src/LogScope.App/**/*.xaml'), recursive=True)):
        t = io.open(x, encoding='utf-8-sig').read()
        m = re.search(r'x:Class="([\w\.]+)"', t)
        if not m:
            continue
        ns, cls = m.group(1).rsplit('.', 1)

        body = re.sub(r'<DataTemplate\b.*?</DataTemplate>', '', t, flags=re.S)
        body = re.sub(r'<ControlTemplate\b.*?</ControlTemplate>', '', body, flags=re.S)

        out.append('namespace %s {' % ns)
        out.append('    public partial class %s {' % cls)
        out.append('        internal void InitializeComponent() { }')
        for a, b, name in re.findall(r'<(\w+)(?::(\w+))?\b[^>]*x:Name="(\w+)"', body):
            typ = b or a
            out.append('        internal %s %s;'
                       % (OURS.get(typ) or WPF.get(typ) or 'System.Windows.FrameworkElement', name))
        out.append('    }')
        out.append('}')

    io.open(out_path, 'w', encoding='utf-8').write('\n'.join(out))
    print('  XAML 스텁 %d 줄' % len(out))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '/tmp/XamlStubs.cs')
