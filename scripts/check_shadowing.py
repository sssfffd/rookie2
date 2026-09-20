# -*- coding: utf-8 -*-
"""
컴파일러 없이 CS0136 을 잡는 좁고 확실한 검사.

무엇을 잡나:
  한 메서드 안에서 같은 이름을 <b>메서드 맨 바깥 수준</b>과
  <b>더 안쪽 블록</b>에서 둘 다 선언한 경우.

  void M() {
      for (...) { int jump = ...; }    // 안쪽
      ChannelDiff jump = ...;          // 바깥  ← CS0136
  }

무엇을 일부러 안 잡나:
  형제 블록끼리 같은 이름을 쓰는 것 (for 두 개가 각각 int i). 정상입니다.
  그래서 헛경보가 나지 않습니다. 대신 안쪽-안쪽 중첩은 놓칩니다.
"""
import io, re, sys, glob, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 타입 자리에 올 수 없는 낱말들. 빠뜨리면 "try { x = 1; }" 의 try 가
# 타입으로 읽혀서 x 를 선언으로 잡습니다 (실제로 겪었습니다).
NOT_TYPE = {
    'return','if','else','while','switch','case','throw','new','in','is','as',
    'do','lock','yield','break','continue','goto','using','await','delegate',
    'and','or','not','when','where','select','from','base','this','typeof',
    'try','catch','finally','checked','unchecked','fixed','unsafe','for',
    'foreach','default','sizeof','stackalloc','nameof','params','readonly',
    'static','public','private','protected','internal','abstract','virtual',
    'override','sealed','partial','extern','volatile','event','operator',
    'implicit','explicit','null','true','false','void','ref','out','const',
    'namespace','class','struct','interface','enum','get','set','add','remove',
}

def strip(t):
    t = re.sub(r'@"(?:[^"]|"")*"', '""', t)
    t = re.sub(r'"(?:\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(?:\\.|[^'\\])'", "'x'", t)
    t = re.sub(r'//[^\n]*', '', t)
    t = re.sub(r'/\*.*?\*/', '', t, flags=re.S)
    return t

TYPE = r'(?:var|[A-Za-z_][\w\.]*(?:<[^<>;{}()]*>)?(?:\?)?(?:\[\s*[,\s]*\])?)'
DECL = re.compile(r'(?:^|[;{}(]|\bout\s+)\s*(' + TYPE + r')\s+([a-z_]\w*)\s*(?==[^=>]|;|\bin\s)', re.M)

def decls(text):
    out = []
    for m in DECL.finditer(text):
        if m.group(1) in NOT_TYPE: continue
        out.append(m.group(2))
    return out

def method_bodies(t):
    """매개변수 목록 ')' 바로 뒤에 오는 블록. 가장 바깥 것만."""
    bodies, stack = [], []
    for i, c in enumerate(t):
        if c == '{':
            head = t[max(0, i - 300):i]
            stack.append((i, bool(re.search(r'\)\s*(?:where[^;{]*)?\s*$', head))))
        elif c == '}':
            if not stack: continue
            start, is_m = stack.pop()
            if is_m and not any(m for _, m in stack):
                bodies.append((start + 1, i))
    return bodies

def pull_headers(body):
    """
    for / foreach / using / fixed 의 괄호 안 선언을 떼어 냅니다.

    이 선언들은 이어지는 블록(또는 문장) 것이지 바깥 것이 아닙니다.
    중괄호가 없는 for 도 마찬가지라, 중괄호를 보고 판단하면 안 됩니다.
      for (int i = 0; i < n; i++) if (...) x++;      <- 중괄호 없음
    괄호 짝을 직접 세어 떼어 내고, 그 자리는 공백으로 채웁니다.
    """
    out, chars, i = [], list(body), 0
    while True:
        m = re.compile(r'\b(?:for|foreach|using|fixed)\s*\(').search(''.join(chars), i)
        if not m: break
        j, d = m.end() - 1, 0
        while j < len(chars):
            if chars[j] == '(': d += 1
            elif chars[j] == ')':
                d -= 1
                if d == 0: break
            j += 1
        out.append(''.join(chars[m.end():j]))
        for k in range(m.start(), min(j + 1, len(chars))): chars[k] = ' '
        i = j + 1
    return ''.join(chars), out


def scan(path):
    raw = io.open(path, encoding='utf-8-sig').read()
    t = strip(raw)
    hits = []
    for start, end in method_bodies(t):
        body, headers = pull_headers(t[start:end])

        # 깊이 0 구간과 깊이 1 이상 구간을 나눕니다.
        outer, inner, depth, buf = [], list(headers), 0, []
        i = 0
        while i < len(body):
            c = body[i]
            if c == '{':
                seg = ''.join(buf); buf = []
                (outer if depth == 0 else inner).append(seg)
                depth += 1
            elif c == '}':
                # 닫는 괄호 직전까지의 내용은 <b>지금 깊이</b>의 것입니다.
                # 여기서 깊이는 반드시 1 이상이므로 전부 안쪽입니다.
                inner.append(''.join(buf)); buf = []
                depth -= 1
                if depth < 0: depth = 0
            else:
                buf.append(c)
            i += 1
        (outer if depth == 0 else inner).append(''.join(buf))

        top = set(decls('\n'.join(outer)))
        deep = set(decls('\n'.join(inner)))
        for name in sorted(top & deep):
            hits.append((path, raw.count('\n', 0, start) + 1, name))
    return hits

hits = []
for f in sorted(glob.glob(os.path.join(ROOT, 'src', '**', '*.cs'), recursive=True)):
    if f.endswith('.g.cs'): continue
    hits += scan(f)

for f, line, n in hits:
    print('CS0136 위험  %s (메서드 ~%d행): "%s" 를 메서드 바깥 수준과 안쪽 블록에서 모두 선언'
          % (os.path.relpath(f, ROOT), line, n))
print('---')
print('지적 %d 건' % len(hits) if hits else '이름 겹침 없음')
sys.exit(1 if hits else 0)
