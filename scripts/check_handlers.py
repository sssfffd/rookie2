# -*- coding: utf-8 -*-
"""XAML 이벤트 처리기의 <b>서명</b>까지 확인합니다.

이름만 있으면 되는 게 아닙니다. WPF 는 XAML 을 컴파일할 때 이벤트마다
정해진 대리자에 맞는 메서드를 찾습니다. 두 번째 인자 타입이 다르면
빌드가 실패하는데, 이름만 보는 검사로는 절대 잡히지 않습니다.
"""
import io, re, os, sys, glob

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 이벤트 -> 두 번째 인자로 와야 하는 타입 (짧은 이름으로 비교)
ARGS = {
    'Click': 'RoutedEventArgs', 'Checked': 'RoutedEventArgs', 'Unchecked': 'RoutedEventArgs',
    'Loaded': 'RoutedEventArgs', 'Unloaded': 'RoutedEventArgs',
    'GotFocus': 'RoutedEventArgs', 'LostFocus': 'RoutedEventArgs',
    'Expanded': 'RoutedEventArgs', 'Collapsed': 'RoutedEventArgs',
    'SelectionChanged': 'SelectionChangedEventArgs',
    'TextChanged': 'TextChangedEventArgs',
    'MouseDoubleClick': 'MouseButtonEventArgs',
    'MouseLeftButtonUp': 'MouseButtonEventArgs',
    'MouseLeftButtonDown': 'MouseButtonEventArgs',
    'MouseRightButtonUp': 'MouseButtonEventArgs',
    'MouseRightButtonDown': 'MouseButtonEventArgs',
    'MouseMove': 'MouseEventArgs', 'MouseEnter': 'MouseEventArgs', 'MouseLeave': 'MouseEventArgs',
    'MouseWheel': 'MouseWheelEventArgs',
    'KeyDown': 'KeyEventArgs', 'PreviewKeyDown': 'KeyEventArgs', 'KeyUp': 'KeyEventArgs',
    'Drop': 'DragEventArgs', 'DragEnter': 'DragEventArgs',
    'DragOver': 'DragEventArgs', 'DragLeave': 'DragEventArgs',
    'Closing': 'CancelEventArgs', 'Closed': 'EventArgs',
    'SizeChanged': 'SizeChangedEventArgs', 'Activated': 'EventArgs',
    'Scroll': 'ScrollEventArgs',
}

hits = []
for x in sorted(glob.glob(os.path.join(ROOT, 'src/LogScope.App/**/*.xaml'), recursive=True)):
    cb = x + '.cs'
    if not os.path.exists(cb):
        continue
    code = io.open(cb, encoding='utf-8-sig').read()
    code = re.sub(r'//[^\n]*', '', code)
    t = io.open(x, encoding='utf-8-sig').read()

    # 코드 비하인드의 메서드 서명을 모읍니다.
    sigs = {}
    for m in re.finditer(r'\b(?:private|public|internal|protected)?\s*(?:static\s+)?void\s+(\w+)\s*\(([^)]*)\)', code):
        sigs.setdefault(m.group(1), []).append(m.group(2))

    for ev, handler in re.findall(r'\b(\w+)="([A-Za-z_]\w*)"', t):
        if ev not in ARGS:
            continue
        want = ARGS[ev]
        if handler not in sigs:
            hits.append((x, ev, handler, '메서드가 없음'))
            continue
        ok = False
        for params in sigs[handler]:
            parts = [p.strip() for p in params.split(',') if p.strip()]
            if len(parts) != 2:
                continue
            second = parts[1].rsplit(' ', 1)[0].strip().split('.')[-1]
            if second == want or (want == 'EventArgs' and second.endswith('EventArgs')):
                ok = True
                break
        if not ok:
            hits.append((x, ev, handler,
                         '두 번째 인자가 %s 여야 하는데 (%s)' % (want, ' | '.join(sigs[handler]))))

# ---- 아무도 듣지 않는 이벤트 ----
#
# 뷰모델이 "바뀌었다" 고 알려도 듣는 쪽이 없으면 조용히 아무 일도 안 납니다.
# 컴파일도 되고 검사도 통과합니다. 실제로 그렇게 해서, 그룹을 고쳐도
# GroupsChanged 를 아무도 듣지 않아 <b>저장되지 않고 있었습니다</b> (0.61).
#
# 선언한 이벤트마다 어딘가에서 "+=" 로 붙는지만 봅니다.
import glob as _glob
APPDIR = os.path.join(ROOT, 'src', 'LogScope.App')
srcs = {}
for f in _glob.glob(APPDIR + '/**/*.cs', recursive=True):
    srcs[f] = io.open(f, encoding='utf-8-sig').read()

# XAML 에서 속성으로 붙는 것도 "듣는 것" 입니다 (CloseRequested="OnX").
xamltext = '\n'.join(io.open(f, encoding='utf-8-sig').read()
                     for f in _glob.glob(APPDIR + '/**/*.xaml', recursive=True))
def nocomment(t):
    """줄 주석을 떼어 냅니다. 주석에 적힌 "+=" 가 듣는 것으로 세면,
    구독을 주석으로 막아 놓고도 검사가 통과합니다 (실제로 겪었습니다)."""
    out = []
    for line in t.split('\n'):
        i = line.find('//')
        while i >= 0:
            if line.count('"', 0, i) % 2 == 0:       # 글자열 안이 아니면 주석
                line = line[:i]
                break
            i = line.find('//', i + 2)
        out.append(line)
    return '\n'.join(out)

alltext = '\n'.join(nocomment(v) for v in srcs.values())

# 런타임이 대신 듣는 것들. WPF 가 바인딩에서 PropertyChanged 를 붙입니다.
BUILTIN = set(['PropertyChanged', 'CollectionChanged', 'ErrorsChanged'])

deaf = []
for f, t in sorted(srcs.items()):
    for m in re.finditer(r'public\s+event\s+[\w\.<>,\s\[\]]+?\s+(\w+)\s*;', t):
        name = m.group(1)
        if name in BUILTIN: continue
        if re.search(r'\b' + re.escape(name) + r'\s*\+=', alltext): continue
        if re.search(r'\b' + re.escape(name) + r'\s*=\s*"', xamltext): continue
        deaf.append((f, name))

for f, name in deaf:
    print('아무도 듣지 않는 이벤트  %s: %s' % (os.path.relpath(f, ROOT), name))

for x, ev, h, why in hits:
    print('서명 불일치  %s: %s="%s" — %s' % (os.path.relpath(x, ROOT), ev, h, why))
print('---')
n = len(hits) + len(deaf)
print('지적 %d 건' % n if n else '이벤트 처리기 서명 모두 맞음 · 듣지 않는 이벤트 없음')
sys.exit(1 if n else 0)
