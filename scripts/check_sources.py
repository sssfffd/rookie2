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

# 5) 괄호 균형 — 문서 주석 안의 문자 상수 때문에 절대 개수는 원래 어긋납니다.
#    그래서 HEAD 와 견줘 "이번에 새로 어긋난 것" 만 잡습니다.
import subprocess
def counts(t):
    t = re.sub(r'@"(?:[^"]|"")*"', '""', t)
    t = re.sub(r'"(?:\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(?:\\.|[^'\\])'", "'x'", t)
    t = re.sub(r'//[^\n]*', '', t)
    t = re.sub(r'/\*.*?\*/', '', t, flags=re.S)
    return tuple(t.count(o) - t.count(c) for o, c in (('{','}'), ('(',')'), ('[',']')))
for c in css:
    rel = os.path.relpath(c, ROOT)
    now = counts(read(c))
    try:
        head = counts(subprocess.check_output(['git','-C',ROOT,'show','HEAD:'+rel]).decode('utf-8'))
    except Exception:
        head = (0, 0, 0)          # 새 파일
    if now != head:
        bad('괄호 균형이 바뀜 %s HEAD=%s 지금=%s' % (rel, head, now))

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
