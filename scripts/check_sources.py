# -*- coding: utf-8 -*-
"""빌드 도구가 없는 환경에서 돌리는 정적 점검. 컴파일의 대용품."""
import io, os, re, sys, glob, xml.dom.minidom, xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APP  = os.path.join(ROOT, 'src/LogScope.App')
fail = []
def bad(m): fail.append(m)

def read(p): return io.open(p, encoding='utf-8-sig').read()

xamls = sorted(glob.glob(APP + '/**/*.xaml', recursive=True))
css   = sorted(glob.glob(ROOT + '/src/**/*.cs', recursive=True))

# 1) XAML 이 XML 로 읽히는가
for x in xamls:
    try: xml.dom.minidom.parse(x)
    except Exception as e: bad('XML %s: %s' % (os.path.relpath(x, ROOT), e))

# 2) Light / Dark 열쇠가 같은가
def keys(p):
    t = read(p)
    return set(re.findall(r'x:Key="([^"]+)"', t))
lk = keys(APP + '/Themes/Light.xaml')
dk = keys(APP + '/Themes/Dark.xaml')
if lk != dk:
    bad('테마 열쇠 불일치 Light-Dark=%s Dark-Light=%s' % (sorted(lk-dk), sorted(dk-lk)))

# 3) 쓰이는 리소스 열쇠가 모두 있는가
defined = set()
for x in xamls:
    defined |= keys(x)
for x in xamls:
    t = read(x)
    for k in re.findall(r'\{(?:Dynamic|Static)Resource\s+([^\}\s]+)\}', t):
        if k.startswith('{x:Type'): continue
        if k not in defined:
            bad('리소스 없음 %s <- %s' % (k, os.path.relpath(x, ROOT)))

# 4) XAML 이벤트 처리기가 코드 비하인드에 있는가
EVENTS = ('Click','Checked','Unchecked','SelectionChanged','TextChanged','MouseDoubleClick',
          'Loaded','Closing','Closed','KeyDown','MouseMove','MouseLeftButtonUp','ValueChanged',
          'DragEnter','Drop','PreviewKeyDown','SourceUpdated','SizeChanged','Scroll','Activated')
for x in xamls:
    cb = x + '.cs'
    if not os.path.exists(cb): continue
    code = read(cb)
    t = read(x)
    for ev, h in re.findall(r'\b(%s)="([A-Za-z_][A-Za-z0-9_]*)"' % '|'.join(EVENTS), t):
        if not re.search(r'\b(void|private|public|internal|protected)[^\n]*\b%s\s*\(' % re.escape(h), code):
            bad('처리기 없음 %s.%s <- %s' % (os.path.basename(cb), h, os.path.relpath(x, ROOT)))

# 4-1) 자기 DataContext 를 바꾸는 뷰에 "맨 경로" 바인딩을 걸지 않았는가
#
#    코드 비하인드에서 DataContext 를 자기 뷰모델로 바꾸는 뷰가 있습니다
#    (DbView 가 DbVm 으로). 그런 뷰를 쓰는 자리에서 Visibility="{Binding X}" 처럼
#    맨 경로로 적으면, 그 X 를 <b>창의 뷰모델이 아니라 그 뷰의 뷰모델</b>에서
#    찾습니다. 없으면 바인딩이 조용히 실패하고, 실패한 Visibility 는 기본값
#    Visible 로 남아 그 화면이 다른 화면 위에 계속 덮입니다.
#
#    실제로 그렇게 해서 DB 화면이 메인 화면을 가렸습니다. 컴파일도 되고 XAML
#    파싱도 되므로, 이 검사가 없으면 윈도우에서 눈으로 봐야 압니다.
SELFCTX = set()
for cb in sorted(glob.glob(APP + '/**/*.xaml.cs', recursive=True)):
    if re.search(r'\bDataContext\s*=\s*', read(cb)):
        SELFCTX.add(os.path.basename(cb)[:-len('.xaml.cs')])

for x in xamls:
    t = read(x)
    own = re.search(r'x:Class="[\w\.]*\.(\w+)"', t)
    own = own.group(1) if own else None
    for m in re.finditer(r'<(?:v|local|ctl|views):(\w+)\b([^>]*)>', t, re.S):
        tag, attrs = m.group(1), m.group(2)
        if tag not in SELFCTX or tag == own: continue
        # 속성값 안에 Converter={StaticResource ...} 처럼 중괄호가 또 들어
        # 있으므로, 닫는 중괄호까지가 아니라 <b>닫는 따옴표까지</b> 봅니다.
        # (XAML 속성값에는 따옴표가 들어갈 수 없습니다.)
        for b in re.finditer(r'(\w+)="(\{Binding[^"]*)"', attrs, re.S):
            prop, expr = b.group(1), b.group(2)
            if 'RelativeSource' in expr or 'ElementName' in expr or 'Source=' in expr: continue
            if expr.lstrip().startswith('DataContext.'): continue
            bad('%s 의 %s 는 맨 경로 바인딩 (%s 는 자기 DataContext 를 바꿉니다) <- %s'
                % (tag, prop, tag, os.path.relpath(x, ROOT)))

# 4-2) 끌어다 놓는 칸: 되돌릴 배경이 XAML 과 코드에서 같은지
#
#    칸을 밝혔다가 되돌릴 때, 코드는 배경을 <b>다시 걸어 줘야</b> 합니다
#    (ClearValue 로 지우면 XAML 값까지 지워져 칸이 사라집니다 — 0.42).
#    그런데 "무엇으로 되돌릴지" 를 코드에 박아 두면, XAML 의 배경을 바꾼
#    날부터 한 번 끌어다 놓은 칸만 색이 달라진 채 남습니다 (0.59 에서 로그
#    칸이 분석 1 안으로 들어가며 Brush.Panel → Brush.Window 가 됐습니다).
#
#    그래서 되돌릴 열쇠를 Tag 에 함께 적습니다 ("before|Brush.Window").
#    여기서는 그 열쇠가 <b>그 칸의 Background 와 같은지</b>만 봅니다.
#    컴파일도 되고 XAML 파싱도 되는 어긋남이라, 이 검사가 없으면 윈도우에서
#    파일을 한 번 끌어다 놓아 봐야 압니다.
#    코드가 배경을 되걸지 않는 화면은 보지 않습니다. 그래프 화면의 그룹 줄은
#    뷰모델 깃발(IsDropTarget)과 XAML 트리거로 색을 바꾸므로 XAML 혼자
#    주인이고, 어긋날 데가 없습니다.
for x in xamls:
    t = read(x)
    cb = x + '.cs'
    if not os.path.exists(cb): continue
    code = read(cb)
    # 되돌리는 일을 CardTag 로 옮겼으므로(0.76) 그 호출도 셉니다. 전에는
    # SetResourceReference 글자만 찾아서, 그 줄을 다른 파일로 옮긴 날부터
    # 이 검사가 조용히 두 화면을 건너뛰게 됩니다.
    if ('SetResourceReference(Border.BackgroundProperty' not in code
            and 'CardTag.Lit' not in code): continue
    for m in re.finditer(r'<(\w[\w\.:]*)\b([^>]*?AllowDrop="True"[^>]*?)/?>', t, re.S):
        el, attrs = m.group(1), m.group(2)
        tag = re.search(r'\bTag="([^"]*)"', attrs)
        back = re.search(r'\bBackground="\{DynamicResource\s+([\w\.]+)\}"', attrs)
        if back is None: continue        # 배경을 XAML 에서 안 정하면 되돌릴 것도 없습니다
        want = back.group(1)
        got = 'Brush.Panel'             # Tag 에 안 적으면 코드가 쓰는 기본값
        if tag and '|' in tag.group(1):
            got = tag.group(1).split('|', 1)[1]
        if got != want:
            bad('%s 의 놓는 칸: 배경은 %s 인데 되돌릴 열쇠는 %s <- %s'
                % (el, want, got, os.path.relpath(x, ROOT)))

# 5) 괄호 균형 — 문서 주석 안의 문자 상수 때문에 절대 개수는 원래 어긋납니다.
#    그래서 HEAD 와 견줘 "이번에 새로 어긋난 것" 만 잡습니다.
import subprocess
def counts(t):
    """괄호 수. 문자열·문자 상수·주석은 센 것에서 뺍니다.

    전에는 정규식으로 하나씩 지웠는데, 그 순서가 틀리면 조용히 엉뚱한 답이
    나왔습니다. '"' 처럼 따옴표 한 글자를 담은 상수나 // 주석 안의 \" 가
    문자열 시작으로 읽혀 뒤의 코드를 통째로 삼켰습니다. 그러면 괄호가
    안 맞는다고 하거나, 반대로 안 맞는 것을 놓칩니다.

    그래서 한 번 훑으면서 상태로 가립니다 — 컴파일러가 하는 방식입니다.
    """
    out = []
    i, n = 0, len(t)
    while i < n:
        c = t[i]

        if c == '/' and i + 1 < n and t[i + 1] == '/':
            while i < n and t[i] != '\n': i += 1
            continue
        if c == '/' and i + 1 < n and t[i + 1] == '*':
            i += 2
            while i + 1 < n and not (t[i] == '*' and t[i + 1] == '/'): i += 1
            i += 2
            continue
        if c == '@' and i + 1 < n and t[i + 1] == '"':
            i += 2
            while i < n:
                if t[i] == '"':
                    if i + 1 < n and t[i + 1] == '"': i += 2; continue
                    i += 1; break
                i += 1
            continue
        if c == '"':
            i += 1
            while i < n:
                if t[i] == '\\': i += 2; continue
                if t[i] == '"': i += 1; break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if t[i] == '\\': i += 2; continue
                if t[i] == "'": i += 1; break
                i += 1
            continue

        out.append(c)
        i += 1

    code = ''.join(out)
    return tuple(code.count(o) - code.count(cl) for o, cl in (('{','}'), ('(',')'), ('[',']')))

for c in css:
    rel = os.path.relpath(c, ROOT)
    now = counts(read(c))
    try:
        head = counts(subprocess.check_output(['git','-C',ROOT,'show','HEAD:'+rel],
                                              stderr=subprocess.PIPE).decode('utf-8'))
    except Exception:
        head = (0, 0, 0)          # 새 파일. 균형이 맞으면 (0,0,0) 입니다.
    if now != head:
        bad('괄호 균형이 바뀜 %s HEAD=%s 지금=%s' % (rel, head, now))

# 7) XAML 의 x:Static 이 가리키는 상수가 실제로 있는가
#
#    글자 열쇠(상태 · 갈래 · 칸 이름)를 한 곳에 모으고 XAML 이 x:Static 으로
#    읽게 했습니다. 그런데 XAML 은 여기서 컴파일되지 않습니다 — 이름을 잘못
#    적으면 Windows 에서 창이 뜰 때 터지거나, 트리거가 조용히 안 걸립니다.
#    그래서 가리키는 이름이 코드에 있는지 여기서 봅니다.
#    타입은 파일 이름으로 찾지 않습니다 — 한 파일에 클래스가 여럿 있을 수
#    있습니다 (ScoreBands 는 AnalysisCardVm.cs 안에 있습니다).
cs_text = {}
for c in css:
    body = read(c)
    for typ in re.findall(r'\b(?:class|struct|enum)\s+(\w+)', body):
        cs_text[typ] = body

for x in xamls:
    rel = os.path.relpath(x, ROOT)
    for ref in re.findall(r'\{x:Static\s+([A-Za-z_][\w]*):([A-Za-z_]\w*)\.([A-Za-z_]\w*)\}', read(x)):
        _pfx, typ, member = ref
        src = cs_text.get(typ)
        if src is None:
            bad('x:Static 대상 없음 %s.%s (%s 파일을 못 찾음) <- %s' % (typ, member, typ, rel))
            continue
        if not re.search(r'(?:const|static|readonly)[^;=\n]*\b' + member + r'\b\s*(?:=|\{|;)', src):
            bad('x:Static 대상 없음 %s.%s <- %s' % (typ, member, rel))

# 8) XAML 의 Tag 글자를 코드 비하인드가 아는가
#
#    Tag="before" 처럼 적어 둔 글자를 code-behind 가 == 로 견줍니다. 한쪽만
#    고치면 컴파일도 되고 화면도 뜨는데 단추가 반대쪽으로 듭니다 (0.59 에
#    드롭 칸 색이 그렇게 틀렸습니다).
#    쪽 글자는 CardTag 한 곳에 상수로 있습니다. 그래서 그 화면의 코드
#    비하인드만 보지 않고 App 쪽 코드 전체에서 찾습니다.
app_code = ''.join(read(c) for c in css if '/LogScope.App/' in c.replace('\\', '/'))
for x in xamls:
    rel = os.path.relpath(x, ROOT)
    for raw in re.findall(r'\bTag="([^"{}]+)"', read(x)):
        word = raw.split('|')[0].strip()
        if not word: continue
        if ('"%s"' % word) not in app_code:
            bad('Tag 글자를 코드가 모름 "%s" <- %s' % (word, rel))

# 6) csproj 파일 목록과 디스크가 맞는가
for proj in glob.glob(ROOT + '/src/*/*.csproj'):
    d = os.path.dirname(proj)
    listed = set()
    for m in re.findall(r'<(?:Compile|Page|ApplicationDefinition|None|Resource) Include="([^"]+)"', read(proj)):
        listed.add(os.path.normpath(m.replace('\\', '/')))
    for root, dirs, files in os.walk(d):
        dirs[:] = [x for x in dirs if x not in ('bin','obj','Properties')]
        for f in files:
            if not f.endswith(('.cs','.xaml')): continue
            rel = os.path.normpath(os.path.relpath(os.path.join(root, f), d))
            if rel.startswith('obj') or rel.endswith('.g.cs'): continue
            if rel not in listed:
                bad('csproj 누락 %s <- %s' % (rel, os.path.basename(proj)))
    for rel in sorted(listed):
        if rel.endswith(('.cs','.xaml')) and not os.path.exists(os.path.join(d, rel)):
            if 'BuildInfo.g.cs' in rel: continue
            bad('파일 없음 %s (%s 에 적혀 있음)' % (rel, os.path.basename(proj)))

print('\n'.join(fail) if fail else 'OK — 지적 사항 없음')
print('-- xaml %d, cs %d' % (len(xamls), len(css)))
sys.exit(1 if fail else 0)
