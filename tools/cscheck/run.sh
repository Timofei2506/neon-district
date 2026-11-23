#!/usr/bin/env bash
# ============================================================================
#  Neon District — настоящая проверка компиляции C# БЕЗ Unity Editor
# ============================================================================
#  Компилирует все скрипты проекта (Assets/Scripts/**) настоящим Roslyn
#  против reference-сборок UnityEngine/UnityEditor из NuGet.
#
#  Что это даёт: ошибки вида CS0246/CS1061/CS0103/CS0117 — «метода нет,
#  поле переименовано, using забыт» — ловятся за 5 секунд прямо в CI,
#  до тяжёлой сборки Unity (которая идёт 20–40 минут).
#
#  Два прохода:
#    1) legacy-ветки  (#else у #if UNITY_6000_0_OR_NEWER) — должен быть чистым;
#    2) Unity 6       (#if UNITY_6000_0_OR_NEWER)        — должен быть чистым.
#  Расхождения reference-сборок 2021 года (linearVelocity, linearDamping,
#  angularDamping) считаются известными и не валят проверку.
#
#  Использование:   bash tools/cscheck/run.sh
# ============================================================================
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"

DOTNET_DIR="${DOTNET_DIR:-$HERE/.dotnet}"
PACKAGES="$HERE/packages"
UNITYLIB="$PACKAGES/lib"
SRC="$ROOT/Assets/Scripts"
OUT="$HERE/out"

# ---------------------------------------------------------------- 1. dotnet
if [ ! -x "$DOTNET_DIR/dotnet" ]; then
  echo "→ .NET SDK не найден, скачиваю в $DOTNET_DIR (~210 МБ, один раз)"
  mkdir -p "$DOTNET_DIR"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_DIR" --no-path > /tmp/dotnet-install.log 2>&1 \
    || { echo "не удалось поставить .NET"; tail -5 /tmp/dotnet-install.log; exit 2; }
fi
DOTNET="$DOTNET_DIR/dotnet"
CSC="$(ls -d "$DOTNET_DIR"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | tail -1)"
[ -z "$CSC" ] && { echo "нет Roslyn в SDK"; exit 2; }

# ------------------------------------------------- 2. reference-сборки Unity
fetch() { # $1 url, $2 файл
  [ -f "$2" ] || curl -sL -o "$2" "$1" || { echo "не скачалось: $1"; exit 2; }
}
mkdir -p "$UNITYLIB" "$PACKAGES"
fetch "https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg" "$PACKAGES/unityengine.modules.nupkg"
fetch "https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg" "$PACKAGES/unity3d.sdk.nupkg"
fetch "https://api.nuget.org/v3-flatcontainer/unity3d.unityengine.ui/2018.3.5.1/unity3d.unityengine.ui.2018.3.5.1.nupkg" "$PACKAGES/unityengine.ui.nupkg"

if [ ! -f "$UNITYLIB/UnityEditor.dll" ]; then
  echo "→ распаковываю reference-сборки"
  ( cd "$PACKAGES" && python3 - <<'PY'
import zipfile, os
lib = os.path.join('lib'); os.makedirs(lib, exist_ok=True)
z = zipfile.ZipFile('unityengine.modules.nupkg')
for n in z.namelist():
    if n.startswith('lib/net35/') and n.endswith('.dll'):
        open(os.path.join(lib, os.path.basename(n)), 'wb').write(z.read(n))
for pkg, name in [('unity3d.sdk.nupkg', 'UnityEditor.dll'),
                  ('unityengine.ui.nupkg', 'UnityEngine.UI.dll')]:
    z = zipfile.ZipFile(pkg)
    for n in z.namelist():
        if n.endswith(name):
            open(os.path.join(lib, name), 'wb').write(z.read(n))
print('   сборок:', len(os.listdir(lib)))
PY
  )
fi

mkdir -p "$OUT"
REFS_DIR=$(ls -d "$DOTNET_DIR"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 2>/dev/null | tail -1)
[ -z "$REFS_DIR" ] && { echo "нет reference-пакетов .NET"; exit 2; }

ARGS=()
for f in "$REFS_DIR"/*.dll; do ARGS+=("-r:$f"); done
for f in "$UNITYLIB"/*.dll; do ARGS+=("-r:$f"); done
FILES=()
while IFS= read -r f; do FILES+=("$f"); done < <(find "$SRC" -name '*.cs' | sort)
STUBS=()
for f in "$HERE"/stubs/*.cs; do STUBS+=("$f"); done

compile() { # $1 = лог, $2 = defines
  "$DOTNET" "$CSC" -nologo -noconfig -nostdlib+ \
    -target:library -langversion:9.0 -deterministic \
    -define:"$2" \
    -nowarn:CS0649,CS0169,CS0414,CS8632,CS1701,CS1702,CS0108,CS0114 \
    -out:"$OUT/NeonDistrict.CompileCheck.dll" \
    "${ARGS[@]}" "${FILES[@]}" "${STUBS[@]}" > "$1" 2>&1
}

echo "→ исходников: ${#FILES[@]}, reference-сборок Unity: $(ls "$UNITYLIB" | wc -l)"

KNOWN='linearVelocity|angularDamping|linearDamping'
RC=0

echo "── проход 1/2: legacy-ветки"
compile "$OUT/compile-legacy.log" "UNITY_EDITOR;UNITY_STANDALONE;UNITY_STANDALONE_WIN;ND_COMPILE_CHECK"
LEGACY=$(grep "error CS" "$OUT/compile-legacy.log" | grep -v '/stubs_' | grep -Ev "$KNOWN")
[ -n "$LEGACY" ] && { echo "$LEGACY"; RC=1; }
echo "   ошибок: $(printf '%s' "$LEGACY" | grep -c . )"

echo "── проход 2/2: Unity 6 (UNITY_6000_0_OR_NEWER)"
compile "$OUT/compile-unity6.log" "UNITY_EDITOR;UNITY_STANDALONE;UNITY_STANDALONE_WIN;UNITY_6000_0_OR_NEWER;ND_COMPILE_CHECK"
U6=$(grep "error CS" "$OUT/compile-unity6.log" | grep -v '/stubs_' | grep -Ev "$KNOWN")
[ -n "$U6" ] && { echo "$U6"; RC=1; }
echo "   ошибок: $(printf '%s' "$U6" | grep -c . )"

echo
if [ "$RC" -eq 0 ]; then echo "OK: C# компилируется (оба прохода чисты)"; else echo "FAIL: есть ошибки компиляции"; fi
exit "$RC"
