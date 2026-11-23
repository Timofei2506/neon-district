"""Санити-чек C# без Unity: баланс скобок с учётом строк/комментариев, namespace, устаревшее API."""
import glob, re, sys
from pathlib import Path

def strip_noise(src: str) -> str:
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '/' and i + 1 < n and src[i+1] == '/':
            while i < n and src[i] != '\n': i += 1
        elif c == '/' and i + 1 < n and src[i+1] == '*':
            i += 2
            while i + 1 < n and not (src[i] == '*' and src[i+1] == '/'): i += 1
            i += 2
        elif c == '"':
            verbatim = i > 0 and src[i-1] == '@'
            i += 1
            while i < n:
                if src[i] == '\\' and not verbatim: i += 2; continue
                if src[i] == '"':
                    if verbatim and i + 1 < n and src[i+1] == '"': i += 2; continue
                    i += 1; break
                i += 1
        elif c == "'":
            i += 1
            while i < n:
                if src[i] == '\\': i += 2; continue
                if src[i] == "'": i += 1; break
                i += 1
        else:
            out.append(c); i += 1
    return ''.join(out)

problems = []
files = sorted(glob.glob('Assets/Scripts/**/*.cs', recursive=True))
for path in files:
    raw = open(path, encoding='utf-8').read()
    src = strip_noise(raw)
    if src.count('{') != src.count('}'):
        problems.append(f'{path}: несбалансированные фигурные скобки ({src.count("{")}/{src.count("}")})')
    if src.count('(') != src.count(')'):
        problems.append(f'{path}: несбалансированные круглые скобки ({src.count("(")}/{src.count(")")})')
    if 'namespace ' not in src:
        problems.append(f'{path}: нет namespace')
    # устаревшее API Unity 6 — только в активном коде (вне #if UNITY_6000_0_OR_NEWER)
    active = re.sub(r'#if UNITY_6000_0_OR_NEWER(.*?)#else.*?#endif', r'\1', src, flags=re.S)
    active = re.sub(r'#if !UNITY_6000_0_OR_NEWER.*?#endif', '', active, flags=re.S)
    for m in re.finditer(r'\b(_?rb)\.(drag|angularDrag)\b', active):
        line = src[:m.start()].count('\n') + 1
        problems.append(f'{path}:{line}: устаревшее {m.group(0)} (Unity 6: linearDamping/angularDamping)')

# самопроверка вырезания #if-блоков
_sample = '''#if UNITY_6000_0_OR_NEWER
rb.linearDamping = 1f;
#else
rb.drag = 1f;
#endif
'''
_active = re.sub(r'#if UNITY_6000_0_OR_NEWER(.*?)#else.*?#endif', r'\1', _sample, flags=re.S)
if 'rb.drag' in _active:
    print('ВНИМАНИЕ: самопроверка препроцессора не сработала, пропускаю проверку legacy API')
    _skip_legacy = True
else:
    _skip_legacy = False

problems = [p for p in problems if not (_skip_legacy and 'устаревшее' in p)]
print(f'проверено файлов: {len(files)}')
if problems:
    print('\n'.join(problems))
    sys.exit(1)
print('C# чист')
